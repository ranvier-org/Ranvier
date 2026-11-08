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

(** Each inner list is one batch of signal writes, applied in order. *)
Definition rhistory := list (list (nid * val)).

(** The order of the effects in a round. Instances permute their argument. *)
Definition effect_order := list nid -> list nid.

Definition is_effect_order (order : effect_order) : Prop := forall l, order l ≡ₚ l.

(** The state of one body's evaluation. *)
Record bstate := {
  bsigs : gmap nid val;
  breads : list (nid * outcome);  (* tracked reads, oldest first *)
  bclean : list (cmd * env);      (* registered cleanups and their environments, newest first *)
  bevs : list event;
  bwrites : list (nid * val);
}.

Definition bs_start (sigs : gmap nid val) : bstate :=
  {| bsigs := sigs; breads := []; bclean := []; bevs := []; bwrites := [] |}.

Definition bs_track (t : nid) (o : outcome) (st : bstate) : bstate :=
  {| bsigs := bsigs st; breads := breads st ++ [(t, o)]; bclean := bclean st; bevs := bevs st;
     bwrites := bwrites st |}.

Definition bs_write (t : nid) (v : val) (st : bstate) : bstate :=
  {| bsigs := <[t := v]> (bsigs st); breads := breads st; bclean := bclean st; bevs := bevs st;
     bwrites := bwrites st ++ [(t, v)] |}.

Definition bs_cleanup (b : cmd) (g : env) (st : bstate) : bstate :=
  {| bsigs := bsigs st; breads := breads st; bclean := (b, g) :: bclean st; bevs := bevs st;
     bwrites := bwrites st |}.

Definition bs_emit (e : event) (st : bstate) : bstate :=
  {| bsigs := bsigs st; breads := breads st; bclean := bclean st; bevs := bevs st ++ [e];
     bwrites := bwrites st |}.

Definition bs_untracked (st : bstate) : bstate :=
  {| bsigs := bsigs st; breads := []; bclean := bclean st; bevs := bevs st;
     bwrites := bwrites st |}.

(** The answer to a read of a node, given the signals current at the read. *)
Definition reader := gmap nid val -> nid -> option outcome.

Definition node_of (g : env) (e : expr) : option nid :=
  z ← eval_expr g None e;
  if decide (0 <= z)%Z then Some (Z.to_nat z) else None.

(** Evaluates a body on behalf of node [n]. [None] marks a stuck body: an undefined expression, a
    read [rd] leaves unanswered, a write to a non-signal, or a command outside the fragment.
    A zero condition selects the else branch. A failed read binds the failure to [x] for
    [CTryRead], and ends the body for [CRead] and [CReadU]. [CBatch] sequences its parts, and a
    failure ends the body and keeps the state reached. *)
Fixpoint exec (p : rprog) (rd : reader) (n : nid) (g : env) (c : cmd) (st : bstate)
    : option (outcome * bstate) :=
  match c with
  | CRet e => v ← eval_expr g None e; Some (Ok v, st)
  | CThrow f => Some (Fail f, st)
  | CLet x e k => v ← eval_expr g None e; exec p rd n ((x, v) :: g) k st
  | CIf e a b =>
      v ← eval_expr g None e;
      if decide (v = 0%Z) then exec p rd n g b st else exec p rd n g a st
  | CRead x m k =>
      t ← node_of g m;
      o ← rd (bsigs st) t;
      match o with
      | Ok v => exec p rd n ((x, v) :: g) k (bs_track t o st)
      | Fail f => Some (Fail f, bs_track t o st)
      end
  | CTryRead x ok m k =>
      t ← node_of g m;
      o ← rd (bsigs st) t;
      match o with
      | Ok v => exec p rd n ((ok, 1%Z) :: (x, v) :: g) k (bs_track t o st)
      | Fail f => exec p rd n ((ok, 0%Z) :: (x, f) :: g) k (bs_track t o st)
      end
  | CReadU x m k =>
      t ← node_of g m;
      o ← rd (bsigs st) t;
      match o with
      | Ok v => exec p rd n ((x, v) :: g) k st
      | Fail f => Some (Fail f, st)
      end
  | CWrite m e k =>
      t ← node_of g m;
      v ← eval_expr g None e;
      if decide (is_Some (rsignals p !! t))
      then exec p rd n g k (bs_write t v st)
      else None
  | CCleanup b k => exec p rd n g k (bs_cleanup b g st)
  | CBatch b k =>
      r ← exec p rd n g b st;
      match r.1 with
      | Ok _ => exec p rd n g k r.2
      | Fail _ => Some r
      end
  | CLog e k => v ← eval_expr g None e; exec p rd n g k (bs_emit (EvLog n v) st)
  | CSignal _ _ _ | CMemo _ _ _ _ _ | CEffect _ _ _ | CDispose _ _ => None
  end.

(** A node's value over [sigs], with at most [k] nested memo evaluations. A memo's logs are
    discarded. *)
Fixpoint node_value (k : nat) (p : rprog) (sigs : gmap nid val) (n : nid) : option outcome :=
  match k with
  | O => None
  | S k' =>
      match rsignals p !! n, rmemos p !! n with
      | Some _, _ => Ok <$> sigs !! n
      | None, Some c => fst <$> exec p (fun _ => node_value k' p sigs) n [] c (bs_start ∅)
      | None, None => None
      end
  end.

(** The signals reached through the tracked reads of [n]'s evaluation over [sigs]. *)
Fixpoint node_deps (k : nat) (p : rprog) (sigs : gmap nid val) (n : nid) : list nid :=
  match k with
  | O => []
  | S k' =>
      match rsignals p !! n, rmemos p !! n with
      | Some _, _ => [n]
      | None, Some c =>
          match exec p (fun _ => node_value k' p sigs) n [] c (bs_start ∅) with
          | Some (_, st) => flat_map (fun r => node_deps k' p sigs r.1) (breads st)
          | None => []
          end
      | None, None => []
      end
  end.

(** The current value of a signal or memo. The fuel covers every memo chain of an acyclic
    program. *)
Definition memo_value (p : rprog) (sigs : gmap nid val) (n : nid) : option outcome :=
  node_value (S (size (rmemos p))) p sigs n.

Definition memo_deps (p : rprog) (sigs : gmap nid val) (n : nid) : list nid :=
  node_deps (S (size (rmemos p))) p sigs n.

(** Every read answered from the signals current at the read. *)
Definition live_read (p : rprog) : reader := fun sigs t => memo_value p sigs t.

(** Every read answered from the one snapshot [snap]. *)
Definition frozen_read (p : rprog) (snap : gmap nid val) : reader := fun _ t => memo_value p snap t.

(** Runs cleanups in list order. A failed cleanup ends only itself. Its reads are untracked. *)
Fixpoint run_cleanups (p : rprog) (rd : reader) (n : nid) (cs : list (cmd * env)) (st : bstate)
    : option bstate :=
  match cs with
  | [] => Some st
  | (b, g) :: cs' =>
      r ← exec p rd n g b (bs_emit (EvCleanup n) st);
      run_cleanups p rd n cs' r.2
  end.

(** One run of effect [n]: the previous run's cleanups, then the body. A failed body keeps the
    reads and cleanups reached. *)
Definition run_effect (p : rprog) (rd : reader) (n : nid) (body : cmd) (cs : list (cmd * env))
    (sigs : gmap nid val) : option bstate :=
  st ← run_cleanups p rd n cs (bs_start sigs);
  r ← exec p rd n [] body (bs_emit (EvRun n) (bs_untracked st));
  Some r.2.

(** The record of one effect run: the signals at its start, the cleanups it ran, its tracked reads,
    its events and its signal writes. *)
Record run := {
  run_node : nid;
  run_snapshot : gmap nid val;
  run_ran_cleanups : list (cmd * env);
  run_reads : list (nid * outcome);
  run_events : list event;
  run_writes : list (nid * val);
}.

(** Signals, and each effect's last-run reads and pending cleanups. *)
Record rstate := {
  rs_sigs : gmap nid val;
  rs_effects : gmap nid (list (nid * outcome) * list (cmd * env));
}.

Definition effect_entry (st : rstate) (n : nid) : list (nid * outcome) * list (cmd * env) :=
  default ([], []) (rs_effects st !! n).

Fixpoint run_round (p : rprog) (ns : list nid) (st : rstate) : option (rstate * list run) :=
  match ns with
  | [] => Some (st, [])
  | n :: ns' =>
      body ← reffects p !! n;
      let cs := (effect_entry st n).2 in
      bs ← run_effect p (live_read p) n body cs (rs_sigs st);
      let st' := {| rs_sigs := bsigs bs;
                    rs_effects := <[n := (breads bs, bclean bs)]> (rs_effects st) |} in
      '(st'', rs) ← run_round p ns' st';
      Some (st'', {| run_node := n; run_snapshot := rs_sigs st; run_ran_cleanups := cs;
                     run_reads := breads bs; run_events := bevs bs;
                     run_writes := bwrites bs |} :: rs)
  end.

(** Whether some read differs from the current graph, evaluating reads oldest first and stopping at
    the first difference. *)
Fixpoint reads_moved (p : rprog) (sigs : gmap nid val) (reads : list (nid * outcome))
    : option bool :=
  match reads with
  | [] => Some false
  | (t, o) :: reads' =>
      cur ← memo_value p sigs t;
      if decide (cur = o) then reads_moved p sigs reads' else Some true
  end.

Definition effect_ids (p : rprog) : list nid := (map_to_list (reffects p)).*1.

(** The effects a round runs: those whose last-run reads differ from the graph at the round's
    start. *)
Fixpoint run_set (p : rprog) (st : rstate) (ns : list nid) : option (list nid) :=
  match ns with
  | [] => Some []
  | n :: ns' =>
      moved ← reads_moved p (rs_sigs st) (effect_entry st n).1;
      rest ← run_set p st ns';
      Some (if (moved : bool) then n :: rest else rest)
  end.

(** Runs rounds until a round's run set is empty. [None] when [fuel] rounds do not reach that. *)
Fixpoint settle (fuel : nat) (order : effect_order) (p : rprog) (st : rstate)
    : option (rstate * list run) :=
  ns ← run_set p st (effect_ids p);
  match ns with
  | [] => Some (st, [])
  | _ :: _ =>
      match fuel with
      | O => None
      | S fuel' =>
          '(st1, r1) ← run_round p (order ns) st;
          '(st2, r2) ← settle fuel' order p st1;
          Some (st2, r1 ++ r2)
      end
  end.

Fixpoint apply_batch (p : rprog) (b : list (nid * val)) (sigs : gmap nid val)
    : option (gmap nid val) :=
  match b with
  | [] => Some sigs
  | (t, v) :: b' =>
      if decide (is_Some (rsignals p !! t)) then apply_batch p b' (<[t := v]> sigs) else None
  end.

Fixpoint run_history (fuel : nat) (order : effect_order) (p : rprog) (h : rhistory) (st : rstate)
    : option (rstate * list run) :=
  match h with
  | [] => Some (st, [])
  | b :: h' =>
      sigs ← apply_batch p b (rs_sigs st);
      '(st1, r1) ← settle fuel order p {| rs_sigs := sigs; rs_effects := rs_effects st |};
      '(st2, r2) ← run_history fuel order p h' st1;
      Some (st2, r1 ++ r2)
  end.

Definition rstate_start (p : rprog) : rstate :=
  {| rs_sigs := rsignals p; rs_effects := ∅ |}.

(** Every effect run of the history: an initial round of all effects, then settling, then each
    batch followed by settling. [fuel] bounds the rounds of each settling. *)
Definition rruns (fuel : nat) (order : effect_order) (p : rprog) (h : rhistory)
    : option (list run) :=
  '(st1, r1) ← run_round p (order (effect_ids p)) (rstate_start p);
  '(st2, r2) ← settle fuel order p st1;
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

Definition runs_of (rs : list run) (n : nid) : list run :=
  filter (fun r => run_node r = n) rs.

(** Run [S j] of effect [n] in [rs] is redundant when each value it read is the value of the same
    node in run [j]. The initial run has no previous run. *)
Definition redundant (p : rprog) (rs : list run) (n : nid) (k : nat) : Prop :=
  is_Some (reffects p !! n) /\
  exists j prev cur,
    k = S j /\
    runs_of rs n !! j = Some prev /\
    runs_of rs n !! k = Some cur /\
    forall t o, (t, o) ∈ run_reads cur -> (t, o) ∈ run_reads prev.
