From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.

(** The meaning of a reactive graph over an ordered input history, for any engine. *)

Global Instance outcome_eq_dec : EqDecision outcome.
Proof. solve_decision. Defined.

Record rprog := {
  rsignals : gmap nid val;
  rmemos : gmap nid cmd;
  reffects : gmap nid cmd;
}.

(** A host input: a signal write, or the disposal of an effect or memo. *)
Inductive rinput := RWrite (n : nid) (v : val) | RDispose (n : nid).

Arguments RWrite _%_nat _%_Z.
Arguments RDispose _%_nat.

(** Each inner list is one batch of inputs, applied in order. *)
Definition rhistory := list (list rinput).

(** The order of the effects in a round, by the round's index in the history and its effects. The
    initial round has index 0. *)
Definition effect_order := nat -> list nid -> list nid.

Definition is_effect_order (order : effect_order) : Prop := forall i l, order i l ≡ₚ l.

(** The state of one body's evaluation. *)
Record bstate := {
  bsigs : gmap nid val;
  breads : list (nid * outcome);     (* tracked reads, oldest first *)
  breads_all : list (nid * outcome); (* every read of the run, oldest first *)
  bclean : list (cmd * env);         (* registered cleanups and their environments, newest first *)
  bevs : list event;
  bwrites : list (nid * val);
  bretired : bool;                   (* the scope is disposed: a registered cleanup runs at once *)
}.

Definition bs_start (sigs : gmap nid val) : bstate :=
  {| bsigs := sigs; breads := []; breads_all := []; bclean := []; bevs := []; bwrites := [];
     bretired := false |}.

Definition bs_start_retired (sigs : gmap nid val) : bstate :=
  {| bsigs := sigs; breads := []; breads_all := []; bclean := []; bevs := []; bwrites := [];
     bretired := true |}.

Definition bs_track (t : nid) (o : outcome) (st : bstate) : bstate :=
  {| bsigs := bsigs st; breads := breads st ++ [(t, o)]; breads_all := breads_all st ++ [(t, o)];
     bclean := bclean st; bevs := bevs st; bwrites := bwrites st; bretired := bretired st |}.

Definition bs_read_untracked (t : nid) (o : outcome) (st : bstate) : bstate :=
  {| bsigs := bsigs st; breads := breads st; breads_all := breads_all st ++ [(t, o)];
     bclean := bclean st; bevs := bevs st; bwrites := bwrites st; bretired := bretired st |}.

Definition bs_write (t : nid) (v : val) (st : bstate) : bstate :=
  {| bsigs := <[t := v]> (bsigs st); breads := breads st; breads_all := breads_all st;
     bclean := bclean st; bevs := bevs st; bwrites := bwrites st ++ [(t, v)];
     bretired := bretired st |}.

Definition bs_cleanup (b : cmd) (g : env) (st : bstate) : bstate :=
  {| bsigs := bsigs st; breads := breads st; breads_all := breads_all st;
     bclean := (b, g) :: bclean st; bevs := bevs st; bwrites := bwrites st;
     bretired := bretired st |}.

Definition bs_emit (e : event) (st : bstate) : bstate :=
  {| bsigs := bsigs st; breads := breads st; breads_all := breads_all st; bclean := bclean st;
     bevs := bevs st ++ [e]; bwrites := bwrites st; bretired := bretired st |}.

(** Clears the tracked reads, so that a cleanup's reads wake nothing. *)
Definition bs_untracked (st : bstate) : bstate :=
  {| bsigs := bsigs st; breads := []; breads_all := breads_all st; bclean := bclean st;
     bevs := bevs st; bwrites := bwrites st; bretired := bretired st |}.

(** Emits the failure of an outcome. *)
Definition note_fail (n : nid) (o : outcome) (st : bstate) : bstate :=
  match o with
  | Ok _ => st
  | Fail f => bs_emit (EvFail n f) st
  end.

(** The answer to a read of a node, given the signals current at the read. *)
Definition reader := gmap nid val -> nid -> option outcome.

Definition node_of (z : val) : option nid :=
  if decide (0 <= z)%Z then Some (Z.to_nat z) else None.

(** Continues with the value of an expression, or ends the body with its failure. *)
Local Notation with_ok r st k :=
  (mbind (fun o => match o with Ok v => k v | Fail f => Some (Fail f, st) end) r).

(** Evaluates a body on behalf of node [n]. [None] marks a stuck body: an undefined expression, a
    read [rd] leaves unanswered, a write to a non-signal, or a command outside the fragment.
    A zero condition selects the else branch. A failed expression ends the body. A failed read binds
    the failure to [x] for [CTryRead], and ends the body for [CRead] and [CReadU]. [CBatch] sequences
    its parts, and a failure ends the body and keeps the state reached. [CCleanup] registers its
    cleanup, or on a retired scope runs it at once, where a failure ends only the cleanup. *)
Fixpoint exec (p : rprog) (rd : reader) (n : nid) (g : env) (c : cmd) (st : bstate)
    : option (outcome * bstate) :=
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
        o ← rd (bsigs st) t;
        match o with
        | Ok v => exec p rd n ((x, v) :: g) k (bs_track t o st)
        | Fail f => Some (Fail f, bs_track t o st)
        end)
  | CTryRead x ok m k =>
      with_ok (eval_expr g None m) st (fun z =>
        t ← node_of z;
        o ← rd (bsigs st) t;
        match o with
        | Ok v => exec p rd n ((ok, 1%Z) :: (x, v) :: g) k (bs_track t o st)
        | Fail f => exec p rd n ((ok, 0%Z) :: (x, f) :: g) k (bs_track t o st)
        end)
  | CReadU x m k =>
      with_ok (eval_expr g None m) st (fun z =>
        t ← node_of z;
        o ← rd (bsigs st) t;
        match o with
        | Ok v => exec p rd n ((x, v) :: g) k (bs_read_untracked t o st)
        | Fail f => Some (Fail f, bs_read_untracked t o st)
        end)
  | CWrite m e k =>
      with_ok (eval_expr g None m) st (fun z =>
        t ← node_of z;
        with_ok (eval_expr g None e) st (fun v =>
          if decide (is_Some (rsignals p !! t))
          then exec p rd n g k (bs_write t v st)
          else None))
  | CCleanup b k =>
      if bretired st then
        r ← exec p rd n g b (bs_emit (EvCleanup n) st);
        exec p rd n g k (note_fail n r.1 r.2)
      else exec p rd n g k (bs_cleanup b g st)
  | CBatch b k =>
      r ← exec p rd n g b st;
      match r.1 with
      | Ok _ => exec p rd n g k r.2
      | Fail _ => Some r
      end
  | CLog e k =>
      with_ok (eval_expr g None e) st (fun v => exec p rd n g k (bs_emit (EvLog n v) st))
  | CSignal _ _ _ | CMemo _ _ _ _ _ | CEffect _ _ _ | CDispose _ _ => None
  end.

(** A node's value over [sigs], with at most [k] nested memo evaluations. A disposed memo answers
    from [frz]. A memo's logs are discarded. *)
Fixpoint node_value (k : nat) (p : rprog) (frz : gmap nid outcome) (sigs : gmap nid val) (n : nid)
    : option outcome :=
  match k with
  | O => None
  | S k' =>
      match frz !! n with
      | Some o => Some o
      | None =>
          match rsignals p !! n, rmemos p !! n with
          | Some _, _ => Ok <$> sigs !! n
          | None, Some c => fst <$> exec p (fun _ => node_value k' p frz sigs) n [] c (bs_start ∅)
          | None, None => None
          end
      end
  end.

(** The signals reached through the tracked reads of [n]'s evaluation over [sigs]. A disposed memo
    reaches none. *)
Fixpoint node_deps (k : nat) (p : rprog) (frz : gmap nid outcome) (sigs : gmap nid val) (n : nid)
    : list nid :=
  match k with
  | O => []
  | S k' =>
      match frz !! n with
      | Some _ => []
      | None =>
          match rsignals p !! n, rmemos p !! n with
          | Some _, _ => [n]
          | None, Some c =>
              match exec p (fun _ => node_value k' p frz sigs) n [] c (bs_start ∅) with
              | Some (_, st) => flat_map (fun r => node_deps k' p frz sigs r.1) (breads st)
              | None => []
              end
          | None, None => []
          end
      end
  end.

(** The current value of a signal or memo. The fuel covers every memo chain of an acyclic
    program. *)
Definition memo_value (p : rprog) (frz : gmap nid outcome) (sigs : gmap nid val) (n : nid)
    : option outcome :=
  node_value (S (size (rmemos p))) p frz sigs n.

Definition memo_deps (p : rprog) (frz : gmap nid outcome) (sigs : gmap nid val) (n : nid)
    : list nid :=
  node_deps (S (size (rmemos p))) p frz sigs n.

(** Every read answered from the signals current at the read. *)
Definition live_read (p : rprog) (frz : gmap nid outcome) : reader :=
  fun sigs t => memo_value p frz sigs t.

(** Every read answered from the one snapshot [snap]. *)
Definition frozen_read (p : rprog) (frz : gmap nid outcome) (snap : gmap nid val) : reader :=
  fun _ t => memo_value p frz snap t.

(** Runs cleanups in list order. A failed cleanup ends only itself and emits its failure. Its reads
    are untracked. *)
Fixpoint run_cleanups (p : rprog) (rd : reader) (n : nid) (cs : list (cmd * env)) (st : bstate)
    : option bstate :=
  match cs with
  | [] => Some st
  | (b, g) :: cs' =>
      r ← exec p rd n g b (bs_emit (EvCleanup n) st);
      run_cleanups p rd n cs' (note_fail n r.1 r.2)
  end.

(** One run of effect [n]: the previous run's cleanups, then the body. A failed body emits its
    failure and keeps the reads and cleanups reached. *)
Definition run_effect (p : rprog) (rd : reader) (n : nid) (body : cmd) (cs : list (cmd * env))
    (sigs : gmap nid val) : option bstate :=
  st ← run_cleanups p rd n cs (bs_start sigs);
  r ← exec p rd n [] body (bs_emit (EvRun n) (bs_untracked st));
  Some (note_fail n r.1 r.2).

(** The disposal of node [n]: its cleanups on the retired scope, then [EvDispose]. *)
Definition dispose_node (p : rprog) (rd : reader) (n : nid) (cs : list (cmd * env))
    (sigs : gmap nid val) : option bstate :=
  st ← run_cleanups p rd n cs (bs_start_retired sigs);
  Some (bs_emit (EvDispose n) st).

(** The record of one effect run or one disposal: the signals at its start, the disposed memos' values,
    the cleanups it ran, its tracked reads, all its reads, its events and its signal writes.
    [run_all_reads] holds the tracked and untracked reads of its cleanups and body in order. *)
Record run := {
  run_node : nid;
  run_disposal : bool;
  run_snapshot : gmap nid val;
  run_frozen : gmap nid outcome;
  run_ran_cleanups : list (cmd * env);
  run_reads : list (nid * outcome);
  run_all_reads : list (nid * outcome);
  run_events : list event;
  run_writes : list (nid * val);
}.

(** Signals, each effect's last-run reads and pending cleanups, each disposed memo's answer, the
    disposed nodes, and the index of the next round. *)
Record rstate := {
  rs_sigs : gmap nid val;
  rs_effects : gmap nid (list (nid * outcome) * list (cmd * env));
  rs_frozen : gmap nid outcome;
  rs_disposed : gset nid;
  rs_round : nat;
}.

Definition effect_entry (st : rstate) (n : nid) : list (nid * outcome) * list (cmd * env) :=
  default ([], []) (rs_effects st !! n).

Definition record_of (n : nid) (disposal : bool) (st : rstate) (cs : list (cmd * env)) (bs : bstate)
    : run :=
  {| run_node := n; run_disposal := disposal; run_snapshot := rs_sigs st;
     run_frozen := rs_frozen st; run_ran_cleanups := cs; run_reads := breads bs;
     run_all_reads := breads_all bs; run_events := bevs bs; run_writes := bwrites bs |}.

Fixpoint run_round (p : rprog) (ns : list nid) (st : rstate) : option (rstate * list run) :=
  match ns with
  | [] => Some (st, [])
  | n :: ns' =>
      body ← reffects p !! n;
      let cs := (effect_entry st n).2 in
      bs ← run_effect p (live_read p (rs_frozen st)) n body cs (rs_sigs st);
      let st' := {| rs_sigs := bsigs bs;
                    rs_effects := <[n := (breads bs, bclean bs)]> (rs_effects st);
                    rs_frozen := rs_frozen st;
                    rs_disposed := rs_disposed st;
                    rs_round := rs_round st |} in
      '(st'', rs) ← run_round p ns' st';
      Some (st'', record_of n false st cs bs :: rs)
  end.

(** Whether some read differs from the current graph, evaluating reads oldest first and stopping at
    the first difference. *)
Fixpoint reads_moved (p : rprog) (frz : gmap nid outcome) (sigs : gmap nid val)
    (reads : list (nid * outcome)) : option bool :=
  match reads with
  | [] => Some false
  | (t, o) :: reads' =>
      cur ← memo_value p frz sigs t;
      if decide (cur = o) then reads_moved p frz sigs reads' else Some true
  end.

Definition effect_ids (p : rprog) : list nid := (map_to_list (reffects p)).*1.

(** The effects a round runs: those whose last-run reads differ from the graph at the round's
    start. A disposed effect has no entry, and so no reads to differ. *)
Fixpoint run_set (p : rprog) (st : rstate) (ns : list nid) : option (list nid) :=
  match ns with
  | [] => Some []
  | n :: ns' =>
      moved ← reads_moved p (rs_frozen st) (rs_sigs st) (effect_entry st n).1;
      rest ← run_set p st ns';
      Some (if (moved : bool) then n :: rest else rest)
  end.

Definition next_round (st : rstate) : rstate :=
  {| rs_sigs := rs_sigs st; rs_effects := rs_effects st; rs_frozen := rs_frozen st;
     rs_disposed := rs_disposed st; rs_round := S (rs_round st) |}.

(** Runs rounds until a round's run set is empty. [None] when [fuel] rounds do not reach that. An
    empty run set takes no round index. *)
Fixpoint settle (fuel : nat) (order : effect_order) (p : rprog) (st : rstate)
    : option (rstate * list run) :=
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
    An effect runs its cleanups over the current signals. A memo's last value is its value over
    [base], the signals at the start of the batch. *)
Definition apply_dispose (p : rprog) (base : gmap nid val) (n : nid) (st : rstate)
    : option (rstate * list run) :=
  if decide (n ∈ rs_disposed st) then Some (st, [])
  else
    match reffects p !! n, rmemos p !! n with
    | Some _, _ =>
        let cs := (effect_entry st n).2 in
        bs ← dispose_node p (live_read p (rs_frozen st)) n cs (rs_sigs st);
        Some ({| rs_sigs := bsigs bs;
                 rs_effects := delete n (rs_effects st);
                 rs_frozen := rs_frozen st;
                 rs_disposed := {[ n ]} ∪ rs_disposed st;
                 rs_round := rs_round st |},
              [record_of n true st cs bs])
    | None, Some _ =>
        o ← memo_value p (rs_frozen st) base n;
        bs ← dispose_node p (live_read p (rs_frozen st)) n [] (rs_sigs st);
        Some ({| rs_sigs := bsigs bs;
                 rs_effects := rs_effects st;
                 rs_frozen := <[n := o]> (rs_frozen st);
                 rs_disposed := {[ n ]} ∪ rs_disposed st;
                 rs_round := rs_round st |},
              [record_of n true st [] bs])
    | None, None => None
    end.

Definition apply_input (p : rprog) (base : gmap nid val) (i : rinput) (st : rstate)
    : option (rstate * list run) :=
  match i with
  | RWrite t v =>
      if decide (is_Some (rsignals p !! t))
      then Some ({| rs_sigs := <[t := v]> (rs_sigs st);
                    rs_effects := rs_effects st;
                    rs_frozen := rs_frozen st;
                    rs_disposed := rs_disposed st;
                    rs_round := rs_round st |}, [])
      else None
  | RDispose n => apply_dispose p base n st
  end.

Fixpoint apply_batch (p : rprog) (base : gmap nid val) (b : list rinput) (st : rstate)
    : option (rstate * list run) :=
  match b with
  | [] => Some (st, [])
  | i :: b' =>
      '(st1, r1) ← apply_input p base i st;
      '(st2, r2) ← apply_batch p base b' st1;
      Some (st2, r1 ++ r2)
  end.

Fixpoint run_history (fuel : nat) (order : effect_order) (p : rprog) (h : rhistory) (st : rstate)
    : option (rstate * list run) :=
  match h with
  | [] => Some (st, [])
  | b :: h' =>
      '(st0, r0) ← apply_batch p (rs_sigs st) b st;
      '(st1, r1) ← settle fuel order p st0;
      '(st2, r2) ← run_history fuel order p h' st1;
      Some (st2, r0 ++ r1 ++ r2)
  end.

Definition rstate_start (p : rprog) : rstate :=
  {| rs_sigs := rsignals p; rs_effects := ∅; rs_frozen := ∅; rs_disposed := ∅; rs_round := 0%nat |}.

(** Every effect run and disposal of the history: an initial round of all effects, then settling, then
    each batch followed by settling. [fuel] bounds the rounds of each settling. *)
Definition rruns (fuel : nat) (order : effect_order) (p : rprog) (h : rhistory)
    : option (list run) :=
  '(st1, r1) ← run_round p (order 0%nat (effect_ids p)) (rstate_start p);
  '(st2, r2) ← settle fuel order p (next_round st1);
  '(_, r3) ← run_history fuel order p h st2;
  Some (r1 ++ r2 ++ r3).

Definition rtrace (fuel : nat) (order : effect_order) (p : rprog) (h : rhistory)
    : option (list event) :=
  rs ← rruns fuel order p h;
  Some (concat (run_events <$> rs)).

Fixpoint well_behaved_effect (c : cmd) : bool :=
  match c with
  | CRet e => expr_no_prev e
  | CThrow _ => true
  | CLet _ e k | CLog e k => expr_no_prev e && well_behaved_effect k
  | CIf e a b => expr_no_prev e && well_behaved_effect a && well_behaved_effect b
  | CRead _ n k | CTryRead _ _ n k | CReadU _ n k => expr_no_prev n && well_behaved_effect k
  | CWrite n e k => expr_no_prev n && expr_no_prev e && well_behaved_effect k
  | CCleanup b k | CBatch b k => well_behaved_effect b && well_behaved_effect k
  | CSignal _ _ _ | CMemo _ _ _ _ _ | CEffect _ _ _ | CDispose _ _ => false
  end.

(** Whatever its reads answer, a memo body reads only memos of lower rank. *)
Definition memo_reads_below (p : rprog) (rank : nid -> nat) (m : nid) (c : cmd) : Prop :=
  forall (rd : nid -> option outcome) o st t v,
    exec p (fun _ => rd) m [] c (bs_start ∅) = Some (o, st) ->
    (t, v) ∈ breads st ->
    is_Some (rmemos p !! t) ->
    (rank t < rank m)%nat.

Definition memos_acyclic (p : rprog) : Prop :=
  exists rank, map_Forall (memo_reads_below p rank) (rmemos p).

Definition rwf (p : rprog) : Prop :=
  dom (rsignals p) ## dom (rmemos p) /\
  dom (rsignals p) ## dom (reffects p) /\
  dom (rmemos p) ## dom (reffects p) /\
  map_Forall (fun _ c => well_behaved_memo c = true) (rmemos p) /\
  map_Forall (fun _ c => well_behaved_effect c = true) (reffects p) /\
  memos_acyclic p.

(** The effect runs of [n] in [rs], without its disposal. *)
Definition runs_of (rs : list run) (n : nid) : list run :=
  filter (fun r => run_node r = n /\ run_disposal r = false) rs.

(** Run [S j] of effect [n] in [rs] is redundant when it and run [j] made the same reads, tracked and
    untracked, cleanups included, in the same order and with the same outcomes. The initial run has
    no previous run. *)
Definition redundant (p : rprog) (rs : list run) (n : nid) (k : nat) : Prop :=
  is_Some (reffects p !! n) /\
  exists j prev cur,
    k = S j /\
    runs_of rs n !! j = Some prev /\
    runs_of rs n !! k = Some cur /\
    run_all_reads cur = run_all_reads prev.

(** What must be preserved from a trace. *)
Record observation := { obs_ty : Type; obs_of : list event -> obs_ty }.

(** The whole trace, run counts included. *)
Definition obs_full : observation := {| obs_ty := list event; obs_of := fun t => t |}.

(** A node's settled view: the logs and failures since its last [EvRun], the distinct failures of its
    whole history in order of first occurrence, and whether it is disposed. *)
Record settled_view := {
  sv_last : list event;
  sv_failures : list val;
  sv_disposed : bool;
}.

Definition settled_step (s : gmap nid settled_view) (e : event) : gmap nid settled_view :=
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
  end.

(** Per node: the values of its last run, its failures and its disposal. *)
Definition obs_settled : observation :=
  {| obs_ty := gmap nid settled_view; obs_of := foldl settled_step ∅ |}.

(** Every trace of [engine] matches, under [O], a reference trace of a valid order chosen for the
    program and history. *)
Definition refines (O : observation)
    (engine : rprog -> rhistory -> option (list event)) : Prop :=
  forall p h t, rwf p -> engine p h = Some t ->
    exists ord fuel t', is_effect_order ord /\ rtrace fuel ord p h = Some t' /\
      obs_of O t = obs_of O t'.

(** [engine] gives a trace whenever the reference settles under every valid order. *)
Definition engine_answers (engine : rprog -> rhistory -> option (list event)) : Prop :=
  forall p h, rwf p ->
    (forall ord, is_effect_order ord -> exists fuel t, rtrace fuel ord p h = Some t) ->
    is_Some (engine p h).
