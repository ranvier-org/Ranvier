From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.

(** The meaning of a reactive graph over an ordered input history, for any engine. *)

Global Instance outcome_eq_dec : EqDecision outcome.
Proof. solve_decision. Defined.

Record program := {
  program_signals : gmap node_id val;
  program_memos : gmap node_id cmd;
  program_effects : gmap node_id cmd;
}.

(** A host input: a signal write, the disposal of an effect or memo, or a read of a signal or memo. *)
Inductive host_input := RWrite (n : node_id) (v : val) | RDispose (n : node_id) | RRead (n : node_id).

Arguments RWrite _%_nat _%_Z.
Arguments RDispose _%_nat.
Arguments RRead _%_nat.

(** Each inner list is one batch of inputs, applied in order. *)
Definition history := list (list host_input).

(** The order of the effects in a round, by the round's index in the history and its effects. The
    initial round has index 0. *)
Definition effect_order := nat -> list node_id -> list node_id.

Definition is_effect_order (order : effect_order) : Prop := forall i l, order i l ≡ₚ l.

(** The state of one body's evaluation. *)
Record body_state := {
  body_signals : gmap node_id val;
  body_reads : list (node_id * outcome);     (* tracked reads, oldest first *)
  body_all_reads : list (node_id * outcome); (* every read of the run, oldest first *)
  body_cleanups : list (cmd * env);         (* registered cleanups and their environments, newest first *)
  body_events : list event;
  body_writes : list (node_id * val);
  body_retired : bool;                   (* the scope is disposed: a registered cleanup runs at once *)
}.

Definition body_start (sigs : gmap node_id val) : body_state :=
  {| body_signals := sigs; body_reads := []; body_all_reads := []; body_cleanups := []; body_events := []; body_writes := [];
     body_retired := false |}.

Definition body_start_retired (sigs : gmap node_id val) : body_state :=
  {| body_signals := sigs; body_reads := []; body_all_reads := []; body_cleanups := []; body_events := []; body_writes := [];
     body_retired := true |}.

Definition body_track (t : node_id) (o : outcome) (st : body_state) : body_state :=
  {| body_signals := body_signals st; body_reads := body_reads st ++ [(t, o)]; body_all_reads := body_all_reads st ++ [(t, o)];
     body_cleanups := body_cleanups st; body_events := body_events st; body_writes := body_writes st; body_retired := body_retired st |}.

Definition body_read_untracked (t : node_id) (o : outcome) (st : body_state) : body_state :=
  {| body_signals := body_signals st; body_reads := body_reads st; body_all_reads := body_all_reads st ++ [(t, o)];
     body_cleanups := body_cleanups st; body_events := body_events st; body_writes := body_writes st; body_retired := body_retired st |}.

Definition body_write (t : node_id) (v : val) (st : body_state) : body_state :=
  {| body_signals := <[t := v]> (body_signals st); body_reads := body_reads st; body_all_reads := body_all_reads st;
     body_cleanups := body_cleanups st; body_events := body_events st; body_writes := body_writes st ++ [(t, v)];
     body_retired := body_retired st |}.

Definition body_add_cleanup (b : cmd) (g : env) (st : body_state) : body_state :=
  {| body_signals := body_signals st; body_reads := body_reads st; body_all_reads := body_all_reads st;
     body_cleanups := (b, g) :: body_cleanups st; body_events := body_events st; body_writes := body_writes st;
     body_retired := body_retired st |}.

Definition body_emit (e : event) (st : body_state) : body_state :=
  {| body_signals := body_signals st; body_reads := body_reads st; body_all_reads := body_all_reads st; body_cleanups := body_cleanups st;
     body_events := body_events st ++ [e]; body_writes := body_writes st; body_retired := body_retired st |}.

(** Clears the tracked reads, so that a cleanup's reads wake nothing. *)
Definition body_untracked (st : body_state) : body_state :=
  {| body_signals := body_signals st; body_reads := []; body_all_reads := body_all_reads st; body_cleanups := body_cleanups st;
     body_events := body_events st; body_writes := body_writes st; body_retired := body_retired st |}.

(** Emits the failure of an outcome. *)
Definition note_fail (n : node_id) (o : outcome) (st : body_state) : body_state :=
  match o with
  | Ok _ => st
  | Fail f => body_emit (EvFail n f) st
  end.

(** The answer to a read of a node, given the signals current at the read. *)
Definition reader := gmap node_id val -> node_id -> option outcome.

Definition node_of (z : val) : option node_id :=
  if decide (0 <= z)%Z then Some (Z.to_nat z) else None.

(** Continues with the value of an expression, or ends the body with its failure. *)
Local Notation with_ok r st k :=
  (mbind (fun o => match o with Ok v => k v | Fail f => Some (Fail f, st) end) r).

(** Evaluates a body on behalf of node [n]. [None] marks a stuck body: an undefined expression, a
    read [rd] leaves unanswered, a write to a non-signal, or a command outside the fragment.
    A zero condition selects the else branch. A failed expression ends the body. A failed read binds
    the failure to [x] for [CTryRead], and ends the body for [CRead] and [CReadUntracked]. [CBatch] sequences
    its parts, and a failure ends the body and keeps the state reached. [CCleanup] registers its
    cleanup, or on a retired scope runs it at once, where a failure ends only the cleanup. *)
Fixpoint exec (p : program) (rd : reader) (n : node_id) (g : env) (c : cmd) (st : body_state)
    : option (outcome * body_state) :=
  match c with
  | CRet e => o ← eval_expr g None e; Some (o, st)
  | CThrow f => Some (Fail f, st)
  | CLet x e k => with_ok (eval_expr g None e) st (fun v => exec p rd n ((x, v) :: g) k st)
  | CIf e a b =>
      with_ok (eval_expr g None e) st (fun v =>
        if decide (v = 0%Z) then exec p rd n g b st else exec p rd n g a st)
  | CRead x m k =>
      with_ok (eval_expr g None m) st (fun z =>
        t ← node_of z;
        o ← rd (body_signals st) t;
        match o with
        | Ok v => exec p rd n ((x, v) :: g) k (body_track t o st)
        | Fail f => Some (Fail f, body_track t o st)
        end)
  | CTryRead x ok m k =>
      with_ok (eval_expr g None m) st (fun z =>
        t ← node_of z;
        o ← rd (body_signals st) t;
        match o with
        | Ok v => exec p rd n ((ok, 1%Z) :: (x, v) :: g) k (body_track t o st)
        | Fail f => exec p rd n ((ok, 0%Z) :: (x, f) :: g) k (body_track t o st)
        end)
  | CReadUntracked x m k =>
      with_ok (eval_expr g None m) st (fun z =>
        t ← node_of z;
        o ← rd (body_signals st) t;
        match o with
        | Ok v => exec p rd n ((x, v) :: g) k (body_read_untracked t o st)
        | Fail f => Some (Fail f, body_read_untracked t o st)
        end)
  | CWrite m e k =>
      with_ok (eval_expr g None m) st (fun z =>
        t ← node_of z;
        with_ok (eval_expr g None e) st (fun v =>
          if decide (is_Some (program_signals p !! t))
          then exec p rd n g k (body_write t v st)
          else None))
  | CCleanup b k =>
      if body_retired st then
        r ← exec p rd n g b (body_emit (EvCleanup n) st);
        exec p rd n g k (note_fail n r.1 r.2)
      else exec p rd n g k (body_add_cleanup b g st)
  | CBatch b k =>
      r ← exec p rd n g b st;
      match r.1 with
      | Ok _ => exec p rd n g k r.2
      | Fail _ => Some r
      end
  | CLog e k =>
      with_ok (eval_expr g None e) st (fun v => exec p rd n g k (body_emit (EvLog n v) st))
  | CSignal _ _ _ | CMemo _ _ _ _ _ | CEffect _ _ _ | CDispose _ _ => None
  end.

(** A node's value over [sigs], with at most [k] nested memo evaluations. A disposed memo answers
    from [frz]. A memo's logs are discarded. *)
Fixpoint node_value (k : nat) (p : program) (frz : gmap node_id outcome) (sigs : gmap node_id val) (n : node_id)
    : option outcome :=
  match k with
  | O => None
  | S k' =>
      match frz !! n with
      | Some o => Some o
      | None =>
          match program_signals p !! n, program_memos p !! n with
          | Some _, _ => Ok <$> sigs !! n
          | None, Some c => fst <$> exec p (fun _ => node_value k' p frz sigs) n [] c (body_start ∅)
          | None, None => None
          end
      end
  end.

(** The signals reached through the tracked reads of [n]'s evaluation over [sigs]. A disposed memo
    reaches none. *)
Fixpoint node_deps (k : nat) (p : program) (frz : gmap node_id outcome) (sigs : gmap node_id val) (n : node_id)
    : list node_id :=
  match k with
  | O => []
  | S k' =>
      match frz !! n with
      | Some _ => []
      | None =>
          match program_signals p !! n, program_memos p !! n with
          | Some _, _ => [n]
          | None, Some c =>
              match exec p (fun _ => node_value k' p frz sigs) n [] c (body_start ∅) with
              | Some (_, st) => flat_map (fun r => node_deps k' p frz sigs r.1) (body_reads st)
              | None => []
              end
          | None, None => []
          end
      end
  end.

(** The current value of a signal or memo. The fuel covers every memo chain of an acyclic
    program. *)
Definition memo_value (p : program) (frz : gmap node_id outcome) (sigs : gmap node_id val) (n : node_id)
    : option outcome :=
  node_value (S (size (program_memos p))) p frz sigs n.

Definition memo_deps (p : program) (frz : gmap node_id outcome) (sigs : gmap node_id val) (n : node_id)
    : list node_id :=
  node_deps (S (size (program_memos p))) p frz sigs n.

(** Every read answered from the signals current at the read. *)
Definition live_read (p : program) (frz : gmap node_id outcome) : reader :=
  fun sigs t => memo_value p frz sigs t.

(** Every read answered from the one snapshot [snap]. *)
Definition frozen_read (p : program) (frz : gmap node_id outcome) (snap : gmap node_id val) : reader :=
  fun _ t => memo_value p frz snap t.

(** Runs cleanups in list order. A failed cleanup ends only itself and emits its failure. Its reads
    are untracked. *)
Fixpoint run_cleanups (p : program) (rd : reader) (n : node_id) (cs : list (cmd * env)) (st : body_state)
    : option body_state :=
  match cs with
  | [] => Some st
  | (b, g) :: cs' =>
      r ← exec p rd n g b (body_emit (EvCleanup n) st);
      run_cleanups p rd n cs' (note_fail n r.1 r.2)
  end.

(** One run of effect [n]: the previous run's cleanups, then the body. A failed body emits its
    failure and keeps the reads and cleanups reached. *)
Definition run_effect (p : program) (rd : reader) (n : node_id) (body : cmd) (cs : list (cmd * env))
    (sigs : gmap node_id val) : option body_state :=
  st ← run_cleanups p rd n cs (body_start sigs);
  r ← exec p rd n [] body (body_emit (EvRun n) (body_untracked st));
  Some (note_fail n r.1 r.2).

(** The disposal of node [n]: its cleanups on the retired scope, then [EvDispose]. *)
Definition dispose_node (p : program) (rd : reader) (n : node_id) (cs : list (cmd * env))
    (sigs : gmap node_id val) : option body_state :=
  st ← run_cleanups p rd n cs (body_start_retired sigs);
  Some (body_emit (EvDispose n) st).

(** A host read of [n] over [sigs]. A failed memo's failure is the read's answer. *)
Definition host_read (p : program) (rd : reader) (n : node_id) (sigs : gmap node_id val) : option body_state :=
  o ← rd sigs n;
  Some (body_emit (EvHostRead n o) (body_start sigs)).

Inductive record_kind := RkEffect | RkDispose | RkHostRead.

Global Instance record_kind_eq_dec : EqDecision record_kind.
Proof. solve_decision. Defined.

(** The record of one effect run, disposal or host read: the signals at its start, the disposed memos' values,
    the cleanups it ran, its tracked reads, all its reads, its events and its signal writes.
    [run_all_reads] holds the tracked and untracked reads of its cleanups and body in order. *)
Record run := {
  run_node : node_id;
  run_kind : record_kind;
  run_snapshot : gmap node_id val;
  run_frozen : gmap node_id outcome;
  run_ran_cleanups : list (cmd * env);
  run_reads : list (node_id * outcome);
  run_all_reads : list (node_id * outcome);
  run_events : list event;
  run_writes : list (node_id * val);
}.

(** Signals, each effect's last-run reads and pending cleanups, each disposed memo's answer, the
    disposed nodes, and the index of the next round. *)
Record reference_state := {
  rs_signals : gmap node_id val;
  rs_effects : gmap node_id (list (node_id * outcome) * list (cmd * env));
  rs_frozen : gmap node_id outcome;
  rs_disposed : gset node_id;
  rs_round : nat;
}.

Definition effect_entry (st : reference_state) (n : node_id) : list (node_id * outcome) * list (cmd * env) :=
  default ([], []) (rs_effects st !! n).

Definition record_of (n : node_id) (kind : record_kind) (st : reference_state) (cs : list (cmd * env)) (bs : body_state)
    : run :=
  {| run_node := n; run_kind := kind; run_snapshot := rs_signals st;
     run_frozen := rs_frozen st; run_ran_cleanups := cs; run_reads := body_reads bs;
     run_all_reads := body_all_reads bs; run_events := body_events bs; run_writes := body_writes bs |}.

Fixpoint run_round (p : program) (ns : list node_id) (st : reference_state) : option (reference_state * list run) :=
  match ns with
  | [] => Some (st, [])
  | n :: ns' =>
      body ← program_effects p !! n;
      let cs := (effect_entry st n).2 in
      bs ← run_effect p (live_read p (rs_frozen st)) n body cs (rs_signals st);
      let st' := {| rs_signals := body_signals bs;
                    rs_effects := <[n := (body_reads bs, body_cleanups bs)]> (rs_effects st);
                    rs_frozen := rs_frozen st;
                    rs_disposed := rs_disposed st;
                    rs_round := rs_round st |} in
      '(st'', rs) ← run_round p ns' st';
      Some (st'', record_of n RkEffect st cs bs :: rs)
  end.

(** Whether some read differs from the current graph, evaluating reads oldest first and stopping at
    the first difference. *)
Fixpoint reads_moved (p : program) (frz : gmap node_id outcome) (sigs : gmap node_id val)
    (reads : list (node_id * outcome)) : option bool :=
  match reads with
  | [] => Some false
  | (t, o) :: reads' =>
      cur ← memo_value p frz sigs t;
      if decide (cur = o) then reads_moved p frz sigs reads' else Some true
  end.

Definition effect_ids (p : program) : list node_id := (map_to_list (program_effects p)).*1.

(** The effects a round runs: those whose last-run reads differ from the graph at the round's
    start. A disposed effect has no entry, and so no reads to differ. *)
Fixpoint run_set (p : program) (st : reference_state) (ns : list node_id) : option (list node_id) :=
  match ns with
  | [] => Some []
  | n :: ns' =>
      moved ← reads_moved p (rs_frozen st) (rs_signals st) (effect_entry st n).1;
      rest ← run_set p st ns';
      Some (if (moved : bool) then n :: rest else rest)
  end.

Definition next_round (st : reference_state) : reference_state :=
  {| rs_signals := rs_signals st; rs_effects := rs_effects st; rs_frozen := rs_frozen st;
     rs_disposed := rs_disposed st; rs_round := S (rs_round st) |}.

(** Runs rounds until a round's run set is empty. [None] when [fuel] rounds do not reach that. An
    empty run set takes no round index. *)
Fixpoint settle (fuel : nat) (order : effect_order) (p : program) (st : reference_state)
    : option (reference_state * list run) :=
  ns ← run_set p st (effect_ids p);
  match ns with
  | [] => Some (st, [])
  | _ :: _ =>
      match fuel with
      | O => None
      | S fuel' =>
          '(st1, r1) ← run_round p (order (rs_round st) ns) st;
          '(st2, r2) ← settle fuel' order p (next_round st1);
          Some (st2, r1 ++ r2)
      end
  end.

(** Disposes [n]. A second disposal of a node does nothing, and a signal or an unknown node is stuck.
    An effect runs its cleanups over the current signals. A memo's last value is its value over the
    current signals. *)
Definition apply_dispose (p : program) (n : node_id) (st : reference_state)
    : option (reference_state * list run) :=
  if decide (n ∈ rs_disposed st) then Some (st, [])
  else
    match program_effects p !! n, program_memos p !! n with
    | Some _, _ =>
        let cs := (effect_entry st n).2 in
        bs ← dispose_node p (live_read p (rs_frozen st)) n cs (rs_signals st);
        Some ({| rs_signals := body_signals bs;
                 rs_effects := delete n (rs_effects st);
                 rs_frozen := rs_frozen st;
                 rs_disposed := {[ n ]} ∪ rs_disposed st;
                 rs_round := rs_round st |},
              [record_of n RkDispose st cs bs])
    | None, Some _ =>
        o ← memo_value p (rs_frozen st) (rs_signals st) n;
        bs ← dispose_node p (live_read p (rs_frozen st)) n [] (rs_signals st);
        Some ({| rs_signals := body_signals bs;
                 rs_effects := rs_effects st;
                 rs_frozen := <[n := o]> (rs_frozen st);
                 rs_disposed := {[ n ]} ∪ rs_disposed st;
                 rs_round := rs_round st |},
              [record_of n RkDispose st [] bs])
    | None, None => None
    end.

Definition apply_input (p : program) (i : host_input) (st : reference_state)
    : option (reference_state * list run) :=
  match i with
  | RWrite t v =>
      if decide (is_Some (program_signals p !! t))
      then Some ({| rs_signals := <[t := v]> (rs_signals st);
                    rs_effects := rs_effects st;
                    rs_frozen := rs_frozen st;
                    rs_disposed := rs_disposed st;
                    rs_round := rs_round st |}, [])
      else None
  | RDispose n => apply_dispose p n st
  | RRead n =>
      bs ← host_read p (live_read p (rs_frozen st)) n (rs_signals st);
      Some (st, [record_of n RkHostRead st [] bs])
  end.

Fixpoint apply_batch (p : program) (b : list host_input) (st : reference_state)
    : option (reference_state * list run) :=
  match b with
  | [] => Some (st, [])
  | i :: b' =>
      '(st1, r1) ← apply_input p i st;
      '(st2, r2) ← apply_batch p b' st1;
      Some (st2, r1 ++ r2)
  end.

Fixpoint run_history (fuel : nat) (order : effect_order) (p : program) (h : history) (st : reference_state)
    : option (reference_state * list run) :=
  match h with
  | [] => Some (st, [])
  | b :: h' =>
      '(st0, r0) ← apply_batch p b st;
      '(st1, r1) ← settle fuel order p st0;
      '(st2, r2) ← run_history fuel order p h' st1;
      Some (st2, r0 ++ r1 ++ r2)
  end.

Definition reference_start (p : program) : reference_state :=
  {| rs_signals := program_signals p; rs_effects := ∅; rs_frozen := ∅; rs_disposed := ∅; rs_round := 0%nat |}.

(** Every effect run, disposal and host read of the history: an initial round of all effects, then settling, then
    each batch followed by settling. [fuel] bounds the rounds of each settling. *)
Definition reference_runs (fuel : nat) (order : effect_order) (p : program) (h : history)
    : option (list run) :=
  '(st1, r1) ← run_round p (order 0%nat (effect_ids p)) (reference_start p);
  '(st2, r2) ← settle fuel order p (next_round st1);
  '(_, r3) ← run_history fuel order p h st2;
  Some (r1 ++ r2 ++ r3).

Definition reference_trace (fuel : nat) (order : effect_order) (p : program) (h : history)
    : option (list event) :=
  rs ← reference_runs fuel order p h;
  Some (concat (run_events <$> rs)).

Fixpoint well_behaved_effect (c : cmd) : bool :=
  match c with
  | CRet e => expr_no_prev e
  | CThrow _ => true
  | CLet _ e k | CLog e k => expr_no_prev e && well_behaved_effect k
  | CIf e a b => expr_no_prev e && well_behaved_effect a && well_behaved_effect b
  | CRead _ n k | CTryRead _ _ n k | CReadUntracked _ n k => expr_no_prev n && well_behaved_effect k
  | CWrite n e k => expr_no_prev n && expr_no_prev e && well_behaved_effect k
  | CCleanup b k | CBatch b k => well_behaved_effect b && well_behaved_effect k
  | CSignal _ _ _ | CMemo _ _ _ _ _ | CEffect _ _ _ | CDispose _ _ => false
  end.

(** Whatever its reads answer, a memo body reads only memos of lower rank. *)
Definition memo_reads_below (p : program) (rank : node_id -> nat) (m : node_id) (c : cmd) : Prop :=
  forall (rd : node_id -> option outcome) o st t v,
    exec p (fun _ => rd) m [] c (body_start ∅) = Some (o, st) ->
    (t, v) ∈ body_reads st ->
    is_Some (program_memos p !! t) ->
    (rank t < rank m)%nat.

Definition memos_acyclic (p : program) : Prop :=
  exists rank, map_Forall (memo_reads_below p rank) (program_memos p).

Definition well_formed_program (p : program) : Prop :=
  dom (program_signals p) ## dom (program_memos p) /\
  dom (program_signals p) ## dom (program_effects p) /\
  dom (program_memos p) ## dom (program_effects p) /\
  map_Forall (fun _ c => well_behaved_memo c = true) (program_memos p) /\
  map_Forall (fun _ c => well_behaved_effect c = true) (program_effects p) /\
  memos_acyclic p.

(** The effect runs of [n] in [rs], without its disposal or host reads. *)
Definition runs_of (rs : list run) (n : node_id) : list run :=
  filter (fun r => run_node r = n /\ run_kind r = RkEffect) rs.

(** Run [S j] of effect [n] in [rs] is redundant when it and run [j] made the same reads, tracked and
    untracked, cleanups included, in the same order and with the same outcomes. The initial run has
    no previous run. *)
Definition redundant (p : program) (rs : list run) (n : node_id) (k : nat) : Prop :=
  is_Some (program_effects p !! n) /\
  exists j prev cur,
    k = S j /\
    runs_of rs n !! j = Some prev /\
    runs_of rs n !! k = Some cur /\
    run_all_reads cur = run_all_reads prev.

(** What must be preserved from a trace. *)
Record observation := { observed_type : Type; observe : list event -> observed_type }.

(** The whole trace, run counts included. *)
Definition observe_full : observation := {| observed_type := list event; observe := fun t => t |}.

(** A node's settled view: the logs and failures since its last [EvRun], the distinct failures of its
    whole history in order of first occurrence, and whether it is disposed. A repeated failure adds
    nothing to [sv_failures]. *)
Record settled_view := {
  sv_last : list event;
  sv_failures : list val;
  sv_disposed : bool;
}.

Definition settled_step (s : gmap node_id settled_view) (e : event) : gmap node_id settled_view :=
  let cur n := default {| sv_last := []; sv_failures := []; sv_disposed := false |} (s !! n) in
  match e with
  | EvRun n =>
      <[n := {| sv_last := []; sv_failures := sv_failures (cur n);
                sv_disposed := sv_disposed (cur n) |}]> s
  | EvLog n _ =>
      <[n := {| sv_last := sv_last (cur n) ++ [e]; sv_failures := sv_failures (cur n);
                sv_disposed := sv_disposed (cur n) |}]> s
  | EvCleanup _ => s
  | EvFail n f =>
      <[n := {| sv_last := sv_last (cur n) ++ [e];
                sv_failures := if decide (f ∈ sv_failures (cur n)) then sv_failures (cur n)
                               else sv_failures (cur n) ++ [f];
                sv_disposed := sv_disposed (cur n) |}]> s
  | EvDispose n =>
      <[n := {| sv_last := sv_last (cur n); sv_failures := sv_failures (cur n);
                sv_disposed := true |}]> s
  | EvHostRead _ _ => s
  end.

(** The host reads of a trace, in order. *)
Definition host_reads : list event -> list (node_id * outcome) :=
  omap (fun e => match e with EvHostRead n o => Some (n, o) | _ => None end).

(** Per node: the values of its last run, its failures and its disposal. For the whole trace: every
    host read, in order. *)
Definition observe_settled : observation :=
  {| observed_type := gmap node_id settled_view * list (node_id * outcome);
     observe := fun t => (foldl settled_step ∅ t, host_reads t) |}.

(** One valid order for the history, compared at every batch prefix. At each prefix of a history it
    answers, the engine answers with a prefix of the whole trace. An engine with no traces satisfies
    this, so an engine proof also owes [engine_answers]. *)
Definition refines (O : observation)
    (engine : program -> history -> option (list event)) : Prop :=
  forall p h t, well_formed_program p -> engine p h = Some t ->
    exists ord, is_effect_order ord /\
      forall h', prefix h' h ->
        exists te fuel tr, engine p h' = Some te /\ prefix te t /\
          reference_trace fuel ord p h' = Some tr /\ observe O te = observe O tr.

(** Some fuel of [engine] gives a trace whenever the reference settles under every valid order. *)
Definition engine_answers
    (engine : nat -> program -> history -> option (list event)) : Prop :=
  forall p h, well_formed_program p ->
    (forall ord, is_effect_order ord -> exists fuel t, reference_trace fuel ord p h = Some t) ->
    exists k t, engine k p h = Some t.
