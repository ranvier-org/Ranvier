From stdpp Require Import prelude.
From stdpp Require Import gmap.
From Ranvier.Base Require Import Prelude.
From Ranvier.Base Require Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList State Marking Engine Check Runs WholeGraph.

(** ** Walks *)

Local Ltac walk_steps :=
  repeat first
    [ eapply walk_visit; [reflexivity|reflexivity|reflexivity|]
    | apply walk_end; reflexivity ].

(** Observer 1 holds two positions from the first visit on, and the walk misses it: removing position
    0 moves observer 3 there, and position 2 sits above the cursor. *)
Example walk_covers_duplicate_observer :
  let o := {| observer_items := [(1, 0); (2, 0)]; observer_latest := 1 |} in
  let muts := [[WAdd 1 1; WAdd 3 0; WRemove 1 0]; []] in
  (exists o', walk (length (observer_items o)) o muts [2; 3] o') /\
  Forall (fun p => 1 ∈ observer_view p) (mutation_points (concat muts) o) /\
  1 ∉ [2; 3].
Proof.
  simpl. split_and!.
  - eexists. walk_steps.
  - repeat apply Forall_cons_2; try apply Forall_nil_2; cbv beta; apply view_elem;
      first [exists 0; eexists; reflexivity | exists 1; eexists; reflexivity | exists 2; eexists; reflexivity].
  - set_solver.
Qed.

(** A removal below the cursor moves the visited last observer into the hole, and the walk visits it
    again. *)
Example walk_visits_moved_twice :
  let o := {| observer_items := [(1, 0); (2, 0); (3, 0)]; observer_latest := 2 |} in
  exists o', walk (length (observer_items o)) o [[WRemove 1 0]; []; []] [3; 2; 3] o'.
Proof. simpl. eexists. walk_steps. Qed.

(** An observer added during a walk is visited when a later removal moves it below the cursor. *)
Example walk_visits_added_observer :
  let o := {| observer_items := [(1, 0); (2, 0); (3, 0); (4, 0)]; observer_latest := 3 |} in
  exists o', walk (length (observer_items o)) o [[WAdd 5 0; WRemove 1 0]; []; []; []] [4; 3; 2; 5] o'.
Proof. simpl. eexists. walk_steps. Qed.

(** ** Marks *)

Local Definition clean_node (k : kind) (cleanup disposed : bool) : node :=
  {| n_kind := k; n_value := 0%Z; n_failure := None; n_status := StNone; n_freshness := Clean;
     n_unannounced := false; n_runs := 0; n_published := false; n_disposed := disposed; n_queued := false;
     n_in_cleanup := cleanup; n_scope := None; n_violated := false;
     n_body := Syntax.CRet (Syntax.EConst 0%Z); n_env := []; n_mode := Syntax.Pure;
     n_comparer := Syntax.CmpEq; n_link := None |}.

Local Definition mark_state (w : EdgeWorld) (nodes : gmap node_id node) (q : list node_id) (h : nat)
    (running : list node_id) : state :=
  {| g_flags := current_flags; g_edges := w; g_nodes := nodes; g_owners := ∅; g_queue := q;
     g_queue_head := h; g_flushing := false; g_flush_owed := false; g_batch_depth := 0; g_pull_depth := 0; g_current := None;
     g_untracked := false; g_current_owner := None; g_next_id := 3; g_trace := []; g_running := running;
     g_ghost := empty_ghost; g_registered := ∅ |}.

(** Computation 2, in a run, has read source 1. *)
Local Definition linked_world : EdgeWorld :=
  default empty_world (sources_add 2 1 (sources_begin_run 2 empty_world)).

Local Lemma linked_world_reachable : world_reachable linked_world.
Proof.
  apply (reachable_add 2 1 (sources_begin_run 2 empty_world)); [apply reachable_begin, reachable_empty|]. vm_compute. reflexivity.
Qed.

Local Lemma linked_world_edge s : g_edges s = linked_world -> edge s 1 2.
Proof.
  intros Hs. exists 0, 0. rewrite Hs. split; vm_compute; reflexivity.
Qed.

(** Memo 1 goes stale, and its observer, effect 2 in its cleanups, stays clean. [takes_marks] excepts
    this observer from [mark_forwards_on_clean]. Closed in [BUGS.md]. *)
Example in_cleanup_observer_stays_clean :
  let s := mark_state linked_world
             (<[1 := clean_node KMemo false false]> (<[2 := clean_node KEffect true false]> ∅)) [] 0 [2] in
  edge_match (g_edges s) /\ edge s 1 2 /\ 2 ∈ g_running s /\
  exists s', mark_check 3 1 s = Some s' /\ ends_stale s' 1 /\ ~ ends_stale s' 2.
Proof.
  simpl. split_and!.
  - exact (proj1 (reachable_well_formed _ linked_world_reachable)).
  - by apply linked_world_edge.
  - left.
  - eexists. split; [vm_compute; reflexivity|]. split.
    + eexists. split; vm_compute; reflexivity.
    + intros (d & Hd & Hst). vm_compute in Hd. simplify_eq.
Qed.

(** Effect 2 disposes itself mid-body and then reads memo 1: [Track] and [AddSource] link the edge with
    no disposed test, and the [finally] of [RunBody] clears it. The effect ignores the mark, and it is
    running. Awaiting ruling in [BUGS.md]. *)
Example disposed_running_observer_stays_clean :
  let s := mark_state linked_world
             (<[1 := clean_node KMemo false false]> (<[2 := clean_node KEffect false true]> ∅)) [] 0 [2] in
  world_reachable (g_edges s) /\ edge s 1 2 /\ 2 ∈ g_running s /\
  exists s', mark_check 3 1 s = Some s' /\ ends_stale s' 1 /\ ~ ends_stale s' 2.
Proof.
  simpl. split_and!.
  - exact linked_world_reachable.
  - by apply linked_world_edge.
  - left.
  - eexists. split; [vm_compute; reflexivity|]. split.
    + eexists. split; vm_compute; reflexivity.
    + intros (d & Hd & Hst). vm_compute in Hd. simplify_eq.
Qed.

(** An effect marked again after its entry was consumed has two entries in [g_queue] and one from
    [g_queue_head], so [effect_mark_queues] counts [pending]. F# releases the consumed slot. *)
Example requeue_holds_two_entries :
  let s := mark_state empty_world (<[2 := clean_node KEffect false false]> ∅) [2] 1 [] in
  queue_once s 2 /\
  exists s', mark_check 3 2 s = Some s' /\ entries_of 2 (g_queue s') = 2 /\ entries_of 2 (pending s') = 1.
Proof.
  simpl. split.
  - intros d _. vm_compute. split; [lia|]. intros _ Hin. inversion Hin.
  - eexists. split; [vm_compute; reflexivity|]. split; vm_compute; reflexivity.
Qed.

(** ** Runs of the engine *)

Import Syntax.
Open Scope nat_scope.

Local Notation V := EVar.
Local Notation K := EConst.
Local Notation Nop := (CRet (EConst 0%Z)).

(** The state a program ends in, under [current_flags]. *)
Local Definition final (p : cmd) : state :=
  default (init current_flags) (fst <$> run_top 100000 p (init current_flags)).

Local Definition node_view (s : state) (n : node_id) : option (freshness * bool * bool * status * val) :=
  d ← g_nodes s !! n; Some (n_freshness d, n_unannounced d, n_queued d, n_status d, n_value d).

(** Every stale node holds [n_unannounced], which is the last disjunct of [wake_ok]. *)
Local Definition stale_all_unannounced (s : state) : Prop :=
  forall n d, g_nodes s !! n = Some d -> stale (n_freshness d) = true -> n_unannounced d = true.

Local Lemma stale_all_unannounced_of_list s :
  forallb (fun p : node_id * node => negb (stale (n_freshness p.2)) || n_unannounced p.2) (map_to_list (g_nodes s)) = true ->
  stale_all_unannounced s.
Proof.
  intros H n d Hd Hst.
  assert (Hin : In (n, d) (map_to_list (g_nodes s))).
  { apply list_elem_of_In, elem_of_map_to_list, Hd. }
  pose proof (proj1 (forallb_forall _ _) H (n, d) Hin) as Hp.
  simpl in Hp. by rewrite Hst in Hp.
Qed.

(** The end state a lost wake-up leaves: [x] is stale and holds [n_unannounced], its observer [e] is clean
    and unqueued, nothing is live or running, and a flush changes nothing. Confirmed in [BUGS.md]. *)
Local Definition left_behind (s : state) (x e : node_id) : Prop :=
  edge s x e /\ stale_all_unannounced s /\
  (exists f v, node_view s x = Some (f, true, false, StNone, v) /\ stale f = true) /\
  (exists v, node_view s e = Some (Clean, false, false, StNone, v)) /\
  pending s = [] /\ g_running s = [] /\ run_flush 1000 s = Some (s, tt).

Local Ltac left_behind_proof i h :=
  split_and!;
    [ exists i, h; split; vm_compute; reflexivity
    | apply stale_all_unannounced_of_list; vm_compute; reflexivity
    | eexists _, _; split; vm_compute; reflexivity
    | eexists; vm_compute; reflexivity
    | vm_compute; reflexivity
    | vm_compute; reflexivity
    | vm_compute; reflexivity ].

(** An owning memo's cleanup writes a source its reader [x] has read in the same run. *)
Definition self_marking_cleanup (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (CSignal 2 (K 0%Z) (CSignal 3 (K 0%Z) (
  CMemo 4 Owning CmpEq
    (CRead 10 (V 2) (CCleanup (CWrite (V 3) (K 50%Z) Nop) (CRet (EMul (V 10) (K 0%Z))))) (
  CMemo 5 Pure CmpEq
    (CRead 10 (V 1) (CRead 11 (V 3) (CRead 12 (V 4) (CLog (V 11) (CRet (V 11)))))) (
  CEffect 6 (CRead 10 (V 5) (CLog (V 10) Nop)) (
  CBatch (CWrite (V 1) (K 1%Z) (CWrite (V 2) (K 1%Z) Nop)) k)))))).

Example self_marking_cleanup_left_behind :
  let s := final (self_marking_cleanup Nop) in
  left_behind s 5 6 /\ node_view s 5 = Some (Dirty, true, false, StNone, 0%Z) /\
  n_value <$> g_nodes s !! 3 = Some 50%Z.
Proof. intros s. split_and!; [left_behind_proof 0 0|vm_compute; reflexivity|vm_compute; reflexivity]. Qed.

Definition self_marking_memo_body : cmd :=
  CRead 10 (V 1) (CLog (V 10) (
  CIf (ELt (V 10) (K 3%Z))
    (CWrite (V 1) (EAdd (V 10) (K 1%Z)) (CIf (ELt (V 10) (K 2%Z)) Nop (CRet (V 10))))
    (CIf (ELt (V 10) (K 2%Z)) Nop (CRet (V 10))))).

(** A memo writes its own source. Its next run answers 2, and the effect saw 0. *)
Definition self_marking_body (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (
  CMemo 2 Pure CmpEq self_marking_memo_body (
  CEffect 3 (CRead 10 (V 2) (CLog (V 10) Nop)) k)).

Example self_marking_body_left_behind :
  let s := final (self_marking_body Nop) in
  left_behind s 2 3 /\ n_value <$> g_nodes s !! 1 = Some 2%Z /\
  node_view s 2 = Some (Dirty, true, false, StNone, 0%Z).
Proof. intros s. split_and!; [left_behind_proof 0 0|vm_compute; reflexivity|vm_compute; reflexivity]. Qed.

(** The read of the left-behind memo moves it, which queues the effect and owes no flush. The next flush
    runs the effect on the value it missed. Confirmed in [BUGS.md]. *)
Example read_move_leaves_effect_queued :
  let s := final (self_marking_cleanup (CRead 20 (V 5) Nop)) in
  node_view s 6 = Some (Dirty, false, true, StNone, 0%Z) /\
  pending s = [6] /\ g_flush_owed s = false /\ n_runs <$> g_nodes s !! 6 = Some 1 /\
  (r ← run_flush 1000 s; Some (g_trace r.1)) = Some (g_trace s ++ [EvRun 6; EvLog 6 50%Z]).
Proof. intros s. split_and!; vm_compute; reflexivity. Qed.

(** The same memo through a reading memo, whose [ResolveCheck] goes clean. *)
Definition self_marking_through_memo (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (
  CMemo 2 Pure CmpEq self_marking_memo_body (
  CMemo 3 Pure CmpEq (CRead 10 (V 2) (CRet (V 10))) (
  CEffect 4 (CRead 10 (V 3) (CLog (V 10) Nop)) k))).

Example self_marking_through_memo_left_behind :
  let s := final (self_marking_through_memo Nop) in
  left_behind s 2 3 /\ edge s 3 4 /\
  node_view s 2 = Some (Dirty, true, false, StNone, 0%Z) /\
  node_view s 3 = Some (Clean, false, false, StNone, 0%Z) /\
  node_view s 4 = Some (Clean, false, false, StNone, 0%Z).
Proof.
  intros s. split_and!;
    [left_behind_proof 0 0|exists 0, 0; split; vm_compute; reflexivity
    |vm_compute; reflexivity|vm_compute; reflexivity|vm_compute; reflexivity].
Qed.

(** A run reads [a], marks itself and reads itself. The nested run's branch trims [a]'s edge, and the
    outer run stores the value it computed from [a]. The memo ends clean, at 10, with no edge to [a],
    where its body answers 20. Confirmed in [BUGS.md]. *)
Definition nested_self_run (k : cmd) : cmd :=
  CSignal 1 (K 1%Z) (CSignal 2 (K 10%Z) (CSignal 3 (K 20%Z) (
  CMemo 4 Pure CmpEq
    (CRead 10 (V 1) (
     CIf (EEq (V 10) (K 1%Z)) (CRead 11 (V 2) (CRet (V 11))) (
     CIf (EEq (V 10) (K 2%Z))
       (CRead 11 (V 2) (CWrite (V 1) (K 3%Z) (CRead 12 (V 4) (CRet (V 11)))))
       (CRead 11 (V 3) (CRet (V 11)))))) (
  CEffect 5 (CRead 10 (V 4) (CLog (V 10) Nop)) (
  CWrite (V 1) (K 2%Z) (CWrite (V 2) (K 11%Z) k)))))).

Example nested_self_run_ends_clean_and_wrong :
  let s := final (nested_self_run Nop) in
  node_view s 4 = Some (Clean, false, false, StNone, 10%Z) /\
  node_view s 5 = Some (Clean, false, false, StNone, 0%Z) /\
  observer_items (observers_of (g_edges s) 4) = [(5, 0); (4, 2)] /\
  pending s = [] /\ g_running s = [] /\ run_flush 1000 s = Some (s, tt) /\
  observer_items (observers_of (g_edges s) 2) = [] /\ n_value <$> g_nodes s !! 1 = Some 3%Z /\
  n_value <$> g_nodes s !! 3 = Some 20%Z /\
  (2 ∉ fst <$> source_slots (sources_of (g_edges s) 4)) /\ 3 ∈ fst <$> source_slots (sources_of (g_edges s) 4).
Proof.
  intros s. split_and!; try (vm_compute; reflexivity).
  - vm_compute. intros Hin. repeat (apply elem_of_cons in Hin as [Hin|Hin]; [discriminate Hin|]). by apply not_elem_of_nil in Hin.
  - vm_compute. repeat (first [left | right]; try done).
Qed.

(** A memo reads itself, then a memo over a signal. The write leaves it in [Check] with a slot naming
    itself, so the next read's walk asks the memo to resolve itself. Each round of the walk spends
    fuel and changes nothing, so the read has no result at any fuel tried, and the F# overflows its stack.
    Confirmed in [BUGS.md]. *)
Definition self_read_check (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (
  CMemo 2 Pure CmpEq (CRead 10 (V 1) (CRet (V 10))) (
  CMemo 3 Pure CmpEq (CRead 10 (V 3) (CRead 11 (V 2) (CRet (V 11)))) (
  CRead 20 (V 3) (CRead 21 (V 3) (CWrite (V 1) (K 1%Z) k))))).

Example self_read_check_resolves_itself :
  let s := final (self_read_check Nop) in
  n_freshness <$> g_nodes s !! 3 = Some Check /\ source_slots (sources_of (g_edges s) 3) !! 0 = Some (3, Some 0) /\
  trace_of 100000 current_flags (self_read_check Nop) =
    Some [EvHostRead 3 (Ok 0%Z); EvHostRead 3 (Ok 0%Z)] /\
  trace_of 500 current_flags (self_read_check (CRead 22 (V 3) Nop)) = None /\
  trace_of 2000 current_flags (self_read_check (CRead 22 (V 3) Nop)) = None.
Proof. intros s. split_and!; vm_compute; reflexivity. Qed.

(** The walk reaches a later self-slot when the sources before it hold their values. The program returns
    without its final read, and with that read has no result at the fuel tried. *)
Definition self_read_second (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (
  CMemo 2 Pure CmpEq (CRead 10 (V 1) (CRet (ELt (V 10) (K 100%Z)))) (
  CMemo 3 Pure CmpEq (CRead 11 (V 2) (CRead 12 (V 3) (CRet (V 11)))) (
  CRead 20 (V 3) (CRead 21 (V 3) (CWrite (V 1) (K 1%Z) k))))).

Example self_read_second_resolves_itself :
  source_slots (sources_of (g_edges (final (self_read_second Nop))) 3) !! 1 = Some (3, Some 0) /\
  n_freshness <$> g_nodes (final (self_read_second Nop)) !! 3 = Some Check /\
  is_Some (trace_of 2000 current_flags (self_read_second Nop)) /\
  trace_of 2000 current_flags (self_read_second (CRead 22 (V 3) Nop)) = None.
Proof. split_and!; try (vm_compute; reflexivity). vm_compute. eexists. reflexivity. Qed.

(** Two memos read each other, by constant id, since a memo's body runs after the later memo exists. The
    write leaves both in [Check], and a read walks round the cycle, with no result at any fuel tried. *)
Definition mutual_read_check (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (
  CMemo 2 Pure CmpEq (CRead 10 (V 1) (CRet (V 10))) (
  CMemo 3 Pure CmpEq (CRead 11 (K 4%Z) (CRead 12 (V 2) (CRet (V 12)))) (
  CMemo 4 Pure CmpEq (CRead 13 (K 3%Z) (CRet (V 13))) (
  CTryRead 20 21 (K 3%Z) (CTryRead 22 23 (K 4%Z) (CTryRead 24 25 (K 3%Z) (CTryRead 26 27 (K 4%Z) (
  CWrite (V 1) (K 1%Z) k)))))))).

Example mutual_read_check_resolves_itself :
  n_freshness <$> g_nodes (final (mutual_read_check Nop)) !! 3 = Some Check /\
  n_freshness <$> g_nodes (final (mutual_read_check Nop)) !! 4 = Some Check /\
  is_Some (trace_of 2000 current_flags (mutual_read_check Nop)) /\
  trace_of 500 current_flags (mutual_read_check (CTryRead 28 29 (K 3%Z) Nop)) = None /\
  trace_of 2000 current_flags (mutual_read_check (CTryRead 28 29 (K 3%Z) Nop)) = None.
Proof. split_and!; try (vm_compute; reflexivity). vm_compute. eexists. reflexivity. Qed.

(** ** Check resolution counterexamples *)

Lemma check_invariant_final p : check_invariant (final p).
Proof.
  unfold final. destruct (run_top 100000 p (init current_flags)) as [[s r]|] eqn:E; simpl.
  - eapply check_invariant_run_top; [apply check_invariant_init|exact E].
  - apply check_invariant_init.
Qed.

Local Lemma settled_of s n :
  gh_unnoticed (g_ghost s) = ∅ -> g_running s = [] -> reads_settled s n.
Proof.
  intros Hu Hr. unfold reads_settled. rewrite Hu, Hr. split_and!.
  - set_solver.
  - apply not_elem_of_nil.
  - intros src _. apply not_elem_of_nil.
Qed.

(** [x] reads [a], writes [a]'s source, and reads [b], a memo over [a]. [b]'s walk runs [a] under [x]'s
    body, and the notice of that run skips [x], the current computation. *)
Definition check_over_moved (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (
  CMemo 2 Pure CmpEq (CRead 10 (V 1) (CRet (V 10))) (
  CMemo 3 Pure CmpEq (CRead 10 (V 2) (CRet (V 10))) (
  CMemo 4 Pure CmpEq
    (CRead 10 (V 2) (CWrite (V 1) (K 1%Z) (CRead 11 (V 3) (CLog (V 10) (CRet (V 10)))))) (
  CRead 20 (V 3) (CRead 21 (V 4) k))))).

Local Definition over_moved_start : state := final (check_over_moved Nop).

Local Definition over_moved_end : state :=
  default over_moved_start (fst <$> update_if_necessary 1000 4 over_moved_start).

(** [x] goes from [Check] to [Clean] with no run, holding the value of its read of 0, and [a] holds 1.
    Confirmed in [BUGS.md]. *)
Example check_resolves_over_moved_source :
  node_view over_moved_start 4 = Some (Check, true, false, StNone, 0%Z) /\
  n_kind <$> g_nodes over_moved_start !! 4 = Some KMemo /\
  update_if_necessary 1000 4 over_moved_start = Some (over_moved_end, tt) /\
  node_view over_moved_end 4 = Some (Clean, false, false, StNone, 0%Z) /\
  node_view over_moved_end 2 = Some (Clean, false, false, StNone, 1%Z) /\
  n_runs <$> g_nodes over_moved_end !! 4 = Some 1 /\
  moved_since_last_run over_moved_end 4 /\
  gh_unnoticed (g_ghost over_moved_end) = {[4]}.
Proof.
  split_and!; try (vm_compute; reflexivity).
  exists 2, 0, (Some 1), (default (signal_node 0%Z) (g_nodes over_moved_end !! 2)).
  split_and!; [vm_compute; reflexivity|vm_compute; reflexivity|].
  right. exists (0%Z, StNone, None). split.
  - vm_compute. left.
  - vm_compute. intros H. discriminate H.
Qed.

(**md
**Falsified on the present engine: a memo can resolve `Clean` over a moved source.**

This is a counterexample, not a guarantee. It proves `resolve_clean_no_move_statement` false, using the Confirmed bug [`check_resolves_over_moved_source`](#check_resolves_over_moved_source): a memo reads a memo, writes that memo's source, then reads another memo over it, and later resolves `Clean` holding the old value.
*)
Theorem resolve_clean_no_move_falsified : ~ resolve_clean_no_move_statement.
Proof.
  intros H.
  destruct check_resolves_over_moved_source as (_ & _ & Hu & _ & _ & _ & Hm & _).
  destruct (g_nodes over_moved_start !! 4) as [d|] eqn:Hd; [|vm_compute in Hd; discriminate Hd].
  destruct (g_nodes over_moved_end !! 4) as [d'|] eqn:Hd'; [|vm_compute in Hd'; discriminate Hd'].
  refine (H 1000 4 over_moved_start over_moved_end d d' (check_invariant_final _) Hd _ _ Hu Hd' _ Hm).
  all: vm_compute in Hd; vm_compute in Hd'; injection Hd as <-; injection Hd' as <-; reflexivity.
Qed.

(** The usual resolution: a memo reads a memo whose value holds. It goes from [Check] to [Clean] with no
    run, and [resolve_clean_unmoved]'s premise holds. *)
Definition check_cutoff (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (
  CMemo 2 Pure CmpEq (CRead 10 (V 1) (CRet (ELt (V 10) (K 100%Z)))) (
  CMemo 3 Pure CmpEq (CRead 11 (V 2) (CRet (V 11))) (
  CRead 20 (V 3) (CWrite (V 1) (K 1%Z) k)))).

Local Definition cutoff_start : state := final (check_cutoff Nop).

Local Definition cutoff_end : state :=
  default cutoff_start (fst <$> update_if_necessary 1000 3 cutoff_start).

Example check_cutoff_settled :
  n_freshness <$> g_nodes cutoff_start !! 3 = Some Check /\
  update_if_necessary 1000 3 cutoff_start = Some (cutoff_end, tt) /\
  n_freshness <$> g_nodes cutoff_end !! 3 = Some Clean /\
  n_runs <$> g_nodes cutoff_end !! 3 = n_runs <$> g_nodes cutoff_start !! 3 /\
  reads_settled cutoff_end 3 /\ ~ moved_since_last_run cutoff_end 3.
Proof.
  assert (Hset : reads_settled cutoff_end 3).
  { apply settled_of; vm_compute; reflexivity. }
  split_and!; try (vm_compute; reflexivity); [done|].
  destruct (g_nodes cutoff_start !! 3) as [d|] eqn:Hd; [|vm_compute in Hd; discriminate Hd].
  destruct (g_nodes cutoff_end !! 3) as [d'|] eqn:Hd'; [|vm_compute in Hd'; discriminate Hd'].
  assert (Hu : update_if_necessary 1000 3 cutoff_start = Some (cutoff_end, tt)) by (vm_compute; reflexivity).
  refine (resolve_clean_unmoved 1000 3 cutoff_start cutoff_end d d' (check_invariant_final _) Hd _ _ Hu Hd' _ Hset).
  all: vm_compute in Hd; vm_compute in Hd'; injection Hd as <-; injection Hd' as <-; reflexivity.
Qed.

(** [self_marking_wake_lost] does not reach [moved_since_last_run]: the reader goes clean over a [Dirty]
    source whose stored value it read. *)
Example self_marking_through_memo_unmoved :
  let s := final (self_marking_through_memo Nop) in
  n_freshness <$> g_nodes s !! 2 = Some Dirty /\ n_freshness <$> g_nodes s !! 3 = Some Clean /\
  reads_settled s 3 /\ ~ moved_since_last_run s 3.
Proof.
  intros s.
  assert (Hset : reads_settled s 3).
  { apply settled_of; vm_compute; reflexivity. }
  split_and!; try (vm_compute; reflexivity); [done|].
  destruct (g_nodes s !! 3) as [d|] eqn:Hd; [|vm_compute in Hd; discriminate Hd].
  refine (unmoved_short_of_dirty s 3 d (check_invariant_final _) Hd _ _ Hset).
  all: subst s; vm_compute in Hd; injection Hd as <-; first [reflexivity|discriminate].
Qed.

(** The same skip through a second read of the source: [x] reads [a], writes [a]'s source, reads [a] again,
    and answers the first read. Its latest read of [a] holds what [a] now stores, and its first does not. *)
Definition check_two_reads (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (
  CMemo 2 Pure CmpEq (CRead 10 (V 1) (CRet (V 10))) (
  CMemo 3 Pure CmpEq
    (CRead 10 (V 2) (CWrite (V 1) (K 1%Z) (CRead 11 (V 2) (CLog (V 10) (CRet (V 10)))))) (
  CRead 19 (V 2) (CRead 20 (V 3) k)))).

Local Definition two_reads_start : state := final (check_two_reads Nop).

Local Definition two_reads_end : state :=
  default two_reads_start (fst <$> update_if_necessary 1000 3 two_reads_start).

Example check_two_reads_over_moved_source :
  node_view two_reads_start 3 = Some (Check, true, false, StNone, 0%Z) /\
  update_if_necessary 1000 3 two_reads_start = Some (two_reads_end, tt) /\
  node_view two_reads_end 3 = Some (Clean, false, false, StNone, 0%Z) /\
  node_view two_reads_end 2 = Some (Clean, false, false, StNone, 1%Z) /\
  seen_of (g_ghost two_reads_end) 3 2 = [(1%Z, StNone, None); (0%Z, StNone, None)] /\
  moved_since_last_run two_reads_end 3 /\
  gh_unnoticed (g_ghost two_reads_end) = {[3]}.
Proof.
  split_and!; try (vm_compute; reflexivity).
  exists 2, 0, (Some 0), (default (signal_node 0%Z) (g_nodes two_reads_end !! 2)).
  split_and!; [vm_compute; reflexivity|vm_compute; reflexivity|].
  right. exists (0%Z, StNone, None). split.
  - vm_compute. right. left.
  - vm_compute. intros H. discriminate H.
Qed.

(** The memo of [self_marking_body] under a reading memo, with no effect. The reader's walk runs the memo,
    which ends [Dirty] with its value unchanged, and the reader goes [Clean] over it. *)
Definition stale_source_reader (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (
  CMemo 2 Pure CmpEq self_marking_memo_body (
  CMemo 3 Pure CmpEq (CRead 10 (V 2) (CRet (V 10))) (
  CRead 20 (V 3) k))).

Local Definition stale_start : state := final (stale_source_reader Nop).

Local Definition stale_end : state :=
  default stale_start (fst <$> update_if_necessary 1000 3 stale_start).

(**md
**Falsified on the present engine: a memo can resolve `Clean` over a live stale source.**

This is a counterexample, not a guarantee. It proves `resolve_clean_no_stale_source_statement` false, on the program [`stale_source_reader`](#stale_source_reader), an instance of the Confirmed bug `self_marking_wake_lost`: the source's own run leaves it `Dirty` with its value unchanged, and its reader goes `Clean` over it.
*)
Theorem resolve_clean_no_stale_source_falsified : ~ resolve_clean_no_stale_source_statement.
Proof.
  intros H.
  assert (Hu : update_if_necessary 1000 3 stale_start = Some (stale_end, tt)) by (vm_compute; reflexivity).
  destruct (g_nodes stale_start !! 3) as [d|] eqn:Hd; [|vm_compute in Hd; discriminate Hd].
  destruct (g_nodes stale_end !! 3) as [d'|] eqn:Hd'; [|vm_compute in Hd'; discriminate Hd'].
  destruct (g_nodes stale_end !! 2) as [e|] eqn:He; [|vm_compute in He; discriminate He].
  assert (Hin : 2 ∈ fst <$> source_slots (sources_of (g_edges stale_end) 3)) by (vm_compute; left).
  assert (Hst : stale (n_freshness e) = false).
  { assert (Hlive : n_disposed e = false) by (vm_compute in He; injection He as <-; reflexivity).
    refine (H 1000 3 stale_start stale_end d d' (check_invariant_final _) Hd _ _ Hu Hd' _ 2 e Hin He Hlive).
    all: vm_compute in Hd; vm_compute in Hd'; injection Hd as <-; injection Hd' as <-; reflexivity. }
  vm_compute in He. injection He as <-. discriminate Hst.
Qed.

(** A reader's source is disposed while [Dirty]. The walk leaves it stale, since a disposed memo has nothing to
    bring current, and the reader goes [Clean] over it. *)
Definition over_disposed_source (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (
  CMemo 2 Pure CmpEq (CRead 10 (V 1) (CRet (V 10))) (
  CMemo 3 Pure CmpEq (CRead 11 (V 2) (CRet (V 11))) (
  CRead 20 (V 3) (CWrite (V 1) (K 1%Z) (CDispose (V 2) k))))).

Local Definition disposed_source_start : state := final (over_disposed_source Nop).

Local Definition disposed_source_end : state :=
  default disposed_source_start (fst <$> update_if_necessary 1000 3 disposed_source_start).

Example resolve_over_disposed_dirty_source :
  node_view disposed_source_start 3 = Some (Check, false, false, StNone, 0%Z) /\
  update_if_necessary 1000 3 disposed_source_start = Some (disposed_source_end, tt) /\
  node_view disposed_source_end 3 = Some (Clean, false, false, StNone, 0%Z) /\
  n_freshness <$> g_nodes disposed_source_end !! 2 = Some Dirty /\
  n_disposed <$> g_nodes disposed_source_end !! 2 = Some true.
Proof. split_and!; vm_compute; reflexivity. Qed.

(** A cleanup of a memo's nested run disposes the memo in [RecomputeScoped]'s window, with the flag of that
    nested run. The memo goes from [Check] to [Clean] holding [n_unannounced]. Awaiting ruling in [BUGS.md]. *)
Definition disposed_window (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (CSignal 2 (K 0%Z) (
  CMemo 3 Pure CmpEq (CRead 10 (V 1) (CRet (V 10))) (
  CMemo 4 Owning CmpEq
    (CRead 10 (V 3) (CIf (EEq (V 10) (K 0%Z)) Nop (
     CRead 11 (V 2) (CIf (EEq (V 11) (K 0%Z))
       (CWrite (V 2) (K 1%Z) (CReadUntracked 12 (K 4%Z) (CReadUntracked 13 (K 4%Z) Nop)))
       (CCleanup (CDispose (K 4%Z) Nop) (CWrite (V 2) (K 2%Z) Nop)))))) (
  CRead 20 (V 4) (CWrite (V 1) (K 1%Z) k))))).

Local Definition window_start : state := final (disposed_window Nop).

Local Definition window_end : state :=
  default window_start (fst <$> update_if_necessary 2000 4 window_start).

Example disposed_window_keeps_unannounced :
  node_view window_start 4 = Some (Check, false, false, StNone, 0%Z) /\
  update_if_necessary 2000 4 window_start = Some (window_end, tt) /\
  node_view window_end 4 = Some (Clean, true, false, StNone, 0%Z) /\
  n_disposed <$> g_nodes window_end !! 4 = Some true.
Proof. split_and!; vm_compute; reflexivity. Qed.

(** ** Wake-ups *)

Local Definition final_with (fl : flags) (p : cmd) : state :=
  default (init fl) (fst <$> run_top 100000 p (init fl)).

Local Lemma in_of_bool (x : node_id) (X : gset node_id) : bool_decide (x ∈ X) = true -> x ∈ X.
Proof. apply bool_decide_eq_true_1. Qed.

Local Lemma bool_of_in (x : node_id) (X : gset node_id) : x ∈ X -> bool_decide (x ∈ X) = true.
Proof. apply bool_decide_eq_true_2. Qed.

Local Lemma not_covered s src c dc e :
  g_nodes s !! c = Some dc -> stale (n_freshness dc) = false -> n_queued dc = false -> g_running s = [] ->
  g_nodes s !! src = Some e -> n_unannounced e = false -> ~ observer_covered s src c.
Proof.
  intros Hc Hs Hq Hr He Hu [[(d&Hd&H)|(d&Hd&H)]|[H|H]].
  - rewrite Hc in Hd. injection Hd as <-. congruence.
  - rewrite Hc in Hd. injection Hd as <-. congruence.
  - by rewrite Hr in H.
  - unfold n_unannounced_of in H. rewrite He in H. simpl in H. congruence.
Qed.

(** A memo disposed while stale, then read by a new effect. The read links an edge with no mark, and the memo
    answers its last value, as Core.fs 4372-4376 documents. Closed in [BUGS.md] as spec corrected: [wake_ok] covers
    live sources. *)
Definition disposed_stale_read (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (
  CMemo 2 Pure CmpEq (CRead 10 (V 1) (CRet (V 10))) (
  CRead 20 (V 2) (CWrite (V 1) (K 1%Z) (CDispose (V 2) (
  CEffect 3 (CRead 10 (V 2) (CLog (V 10) Nop)) k))))).

Example disposed_stale_source_read :
  let s := final (disposed_stale_read Nop) in
  edge s 2 3 /\ stale_of s 2 /\ ~ live_of s 2 /\ ~ observer_covered s 2 3.
Proof.
  intros s.
  destruct (g_nodes s !! (2 : node_id)) as [e|] eqn:He; [|vm_compute in He; discriminate He].
  destruct (g_nodes s !! (3 : node_id)) as [c|] eqn:Hc; [|vm_compute in Hc; discriminate Hc].
  split_and!.
  - exists 0, 0. split; vm_compute; reflexivity.
  - exists e. split; [done|]. subst s. vm_compute in He. injection He as <-. reflexivity.
  - intros (e'&He'&Hl). rewrite He in He'. injection He' as <-. subst s. vm_compute in He. injection He as <-. discriminate Hl.
  - apply (not_covered s 2 3 c e); [done| | |subst s; vm_compute; reflexivity|done|];
      subst s; vm_compute in He; vm_compute in Hc; injection He as <-; injection Hc as <-; reflexivity.
Qed.

(** An effect reads [a], then [b]. [b]'s run writes [a]'s source. *)
Definition walk_passed (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (CSignal 2 (K 0%Z) (
  CMemo 3 Pure CmpEq (CRead 10 (V 1) (CRet (V 10))) (
  CMemo 4 Pure CmpEq (CRead 10 (V 2) (CWrite (V 1) (V 10) Nop)) (
  CEffect 5 (CRead 10 (V 3) (CRead 11 (V 4) (CLog (V 10) Nop))) (
  CWrite (V 2) (K 1%Z) k))))).

(** The same with [b] as an owning memo whose cleanup writes [a]'s source. *)
Definition walk_passed_by_cleanup (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (CSignal 2 (K 0%Z) (
  CMemo 3 Pure CmpEq (CRead 10 (V 1) (CRet (V 10))) (
  CMemo 4 Owning CmpEq (CRead 10 (V 2) (CCleanup (CWrite (V 1) (K 1%Z) Nop) (CRet (EMul (V 10) (K 0%Z))))) (
  CEffect 5 (CRead 10 (V 3) (CRead 11 (V 4) (CLog (V 10) Nop))) (
  CWrite (V 2) (K 1%Z) k))))).

(** What both programs end in: the effect is clean, unqueued and idle over a live, [Dirty] [a] with no
    announcement, the signal holds 1, [a] holds 0, and a flush changes nothing. *)
Local Definition passed_over (s : state) : Prop :=
  edge s 3 5 /\ stale_of s 3 /\ live_of s 3 /\ ~ observer_covered s 3 5 /\
  (5 : node_id) ∈ gh_passed (g_ghost s) /\ (5 : node_id) ∈ gh_over (g_ghost s) /\
  n_value <$> g_nodes s !! (1 : node_id) = Some 1%Z /\ n_value <$> g_nodes s !! (3 : node_id) = Some 0%Z /\
  pending s = [] /\ g_running s = [] /\ run_flush 1000 s = Some (s, tt).

Local Ltac passed_over_proof s :=
  unfold passed_over; set (e := default (signal_node 0%Z) (g_nodes s !! (3 : node_id)));
  set (c := default (signal_node 0%Z) (g_nodes s !! (5 : node_id)));
  assert (He : g_nodes s !! (3 : node_id) = Some e) by (vm_compute; reflexivity);
  assert (Hc : g_nodes s !! (5 : node_id) = Some c) by (vm_compute; reflexivity);
  split_and!;
    [ exists 0, 0; split; vm_compute; reflexivity
    | exists e; split; [done|vm_compute; reflexivity]
    | exists e; split; [done|vm_compute; reflexivity]
    | apply (not_covered s 3 5 c e); [done|vm_compute; reflexivity|vm_compute; reflexivity|vm_compute; reflexivity|done|vm_compute; reflexivity]
    | apply in_of_bool; vm_compute; reflexivity | apply in_of_bool; vm_compute; reflexivity
    | vm_compute; reflexivity | vm_compute; reflexivity | vm_compute; reflexivity | vm_compute; reflexivity | vm_compute; reflexivity ].

Example walk_passed_source_marked : passed_over (final (walk_passed Nop)).
Proof. set (s := final (walk_passed Nop)). passed_over_proof s. Qed.

Example walk_passed_source_cleanup : passed_over (final (walk_passed_by_cleanup Nop)).
Proof. set (s := final (walk_passed_by_cleanup Nop)). passed_over_proof s. Qed.

(** After the walk, another write of [a]'s source wakes nothing: [a] is [Dirty] with no announcement, so it tells no
    observer, and the effect stays at its one run. *)
Example walk_passed_stays_deaf :
  let s := final (walk_passed (CWrite (V 1) (K 2%Z) Nop)) in
  n_value <$> g_nodes s !! (1 : node_id) = Some 2%Z /\ n_runs <$> g_nodes s !! (5 : node_id) = Some 1 /\
  g_trace s = [EvRun 5; EvLog 5 0%Z] /\ pending s = [].
Proof. intros s. split_and!; vm_compute; reflexivity. Qed.

(**md
**Falsified on the present engine: a program can leave an observer of a live stale source uncovered.**

This is a counterexample, not a guarantee. It proves `no_lost_wakeups_statement` false, using the Confirmed bug [`walk_passed_source_marked`](#walk_passed_source_marked): an effect's check walk passes a memo, a later source's run writes that memo's source, and the walk ends with the effect `Clean` over the memo, now `Dirty` with no announcement.
*)
Theorem no_lost_wakeups_falsified : ~ no_lost_wakeups_statement.
Proof.
  intros H.
  destruct walk_passed_source_marked as (He & Hs & Hl & Hn & _).
  apply Hn. refine (H 100000 (walk_passed Nop) (init current_flags) (final (walk_passed Nop)) _ engine_inv_init _ 3 5 He Hs Hl).
  vm_compute. reflexivity.
Qed.

(**md
**Falsified on the present engine: `run_top` need not keep the invariant with `wake_ok`.**

This is a counterexample, not a guarantee. It proves `engine_inv_preserved_statement` false, on the same program as [`no_lost_wakeups_falsified`](#no_lost_wakeups_falsified).
*)
Theorem engine_inv_preserved_falsified : ~ engine_inv_preserved_statement.
Proof.
  intros H.
  destruct walk_passed_source_marked as (He & Hs & Hl & Hn & _).
  apply Hn.
  refine (ei_wake _ (Pexec _ _ (H 100000) [] (walk_passed Nop) (init current_flags) (final (walk_passed Nop)) _ engine_inv_init I _) 3 5 He Hs Hl).
  vm_compute. reflexivity.
Qed.

(** An effect reads [a], writes its source and reads [b], a memo over [a]. [b]'s walk runs [a] with the effect
    current, and the notice of that run skips the effect. The effect's next execution walks two clean sources and
    goes [Clean], with its body's first read at 0 and [a] at 1. Confirmed in [BUGS.md]. *)
Definition effect_over_moved (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (
  CMemo 2 Pure CmpEq (CRead 10 (V 1) (CRet (V 10))) (
  CMemo 3 Pure CmpEq (CRead 10 (V 2) (CRet (V 10))) (
  CRead 20 (V 3) (
  CEffect 4 (CRead 10 (V 2) (CWrite (V 1) (K 1%Z) (CRead 11 (V 3) (CLog (V 10) Nop)))) k)))).

Example effect_resolves_over_moved_source :
  let s := final (effect_over_moved Nop) in
  g_trace s = [EvHostRead 3 (Ok 0%Z); EvRun 4; EvLog 4 0%Z] /\
  node_view s 2 = Some (Clean, false, false, StNone, 1%Z) /\
  n_freshness <$> g_nodes s !! (4 : node_id) = Some Clean /\ n_runs <$> g_nodes s !! (4 : node_id) = Some 1 /\
  pending s = [] /\ g_running s = [] /\ run_flush 1000 s = Some (s, tt).
Proof. intros s. split_and!; vm_compute; reflexivity. Qed.

(** The notice with its skip removed is [notify_dirty]: [mark_except] with no test is [mark_dirty]. *)
Local Definition skip_state : state :=
  with_current (Some 2) (mark_state linked_world
    (<[1 := clean_node KMemo false false]> (<[2 := clean_node KEffect false false]> ∅)) [] 0 [2]).

(**md
**Removing the skip of `NotifyDirtyExcept` breaks `notice_skips_current`.**

*Check, not a guarantee.*

Source 1 is observed by effect 2, the current computation, which is `Clean`. The notice leaves the effect short of `Dirty`. The same notice with the skip removed, which is `notify_dirty`, leaves it `Dirty`.
*)
Theorem skip_removed_fires :
  (d ← (s ← notify_dirty_except 3 1 (Some 2) skip_state; g_nodes s !! (2 : node_id)); Some (n_freshness d)) = Some Clean /\
  (d ← (s ← notify_dirty 3 1 skip_state; g_nodes s !! (2 : node_id)); Some (n_freshness d)) = Some Dirty.
Proof. split; vm_compute; reflexivity. Qed.

(** ** Runs and nested runs *)

(** The memo of [nested_self_run] over its host read, with no effect. The write leaves it [Dirty]. *)
Definition nested_host_body : cmd :=
  CRead 10 (V 1) (
  CIf (EEq (V 10) (K 1%Z)) (CRead 11 (V 2) (CRet (V 11))) (
  CIf (EEq (V 10) (K 2%Z))
    (CRead 11 (V 2) (CWrite (V 1) (K 3%Z) (CRead 12 (V 4) (CRet (V 11)))))
    (CRead 11 (V 3) (CRet (V 11))))).

Definition nested_host : cmd :=
  CSignal 1 (K 1%Z) (CSignal 2 (K 10%Z) (CSignal 3 (K 20%Z) (
  CMemo 4 Pure CmpEq nested_host_body (CRead 20 (V 4) (CWrite (V 1) (K 2%Z) Nop))))).

(** The body up to its write of its own source. *)
Definition nested_host_point : cmd :=
  CRead 10 (V 1) (CIf (EEq (V 10) (K 1%Z)) Nop (CIf (EEq (V 10) (K 2%Z)) (CRead 11 (V 2) (CWrite (V 1) (K 3%Z) Nop)) Nop)).

Local Lemma nested_host_prefix : body_prefix nested_host_point nested_host_body.
Proof. repeat constructor. Qed.

Local Definition nested_start : state := final nested_host.
Local Definition nested_node : node := default (signal_node 0%Z) (g_nodes nested_start !! (4 : node_id)).
Local Definition nested_end : state := default nested_start (fst <$> run_memo 1000 4 nested_start).
Local Definition nested_point : state :=
  default nested_start (fst <$> hosted 4 (exec 999 (run_env nested_node) nested_host_point) (run_start 4 nested_node nested_start)).

Local Lemma nested_settled : engine_settled nested_start.
Proof.
  refine (Pexec _ _ (engine_settled_preserved 100000) [] nested_host (init current_flags) nested_start _ engine_settled_init I _).
  vm_compute. reflexivity.
Qed.

Local Lemma nested_lookup : g_nodes nested_start !! (4 : node_id) = Some nested_node.
Proof. vm_compute. reflexivity. Qed.

(**md
**Falsified on the present engine: a mark received during a run can be gone at the run's end.**

This is a counterexample, not a guarantee. It proves `marks_survive_run_statement` false, using the Confirmed bug `nested_self_recompute_resets_cursor`: a memo writes its own source, which leaves it `Dirty`, then reads itself. The nested run ends `Clean`, and so does the outer run.
*)
Theorem marks_survive_run_falsified : ~ marks_survive_run_statement.
Proof.
  intros H.
  assert (Hb : n_body nested_node = nested_host_body) by (vm_compute; reflexivity).
  destruct (H 999 4 nested_start nested_end nested_point (Ok 0%Z) nested_node nested_host_point
             nested_settled nested_lookup) as [(e&He&Hs) _].
  - vm_compute. reflexivity.
  - rewrite Hb. apply nested_host_prefix.
  - vm_compute. reflexivity.
  - eexists. split; vm_compute; reflexivity.
  - vm_compute. reflexivity.
  - vm_compute in He. injection He as <-. vm_compute in Hs. discriminate Hs.
Qed.

(**md
**Falsified on the present engine: a live memo's source list can miss a read of its run.**

This is a counterexample, not a guarantee. It proves `run_end_edges_are_reads_statement` false, on the same run as [`marks_survive_run_falsified`](#marks_survive_run_falsified): the outer run's read of signal 2 has no slot, because the nested run trimmed it. A live memo with no nested run has the list `run_end_edges_no_nested_run` states.
*)
Theorem run_end_edges_are_reads_falsified : ~ run_end_edges_are_reads_statement.
Proof.
  intros H.
  assert (Hnr : (4 : node_id) ∉ g_running nested_start) by (vm_compute; apply not_elem_of_nil).
  assert (Hk : has_kind 4 KMemo nested_start) by (exists nested_node; split; [exact nested_lookup|vm_compute; reflexivity]).
  assert (Hm : run_memo 1000 4 nested_start = Some (nested_end, tt)) by (vm_compute; reflexivity).
  assert (He : g_nodes nested_end !! (4 : node_id) = Some (default (signal_node 0%Z) (g_nodes nested_end !! (4 : node_id)))) by (vm_compute; reflexivity).
  pose proof (H 1000 4 nested_start nested_end _ nested_settled Hnr (or_introl (conj Hk Hm)) He ltac:(vm_compute; reflexivity)) as Heq.
  vm_compute in Heq. discriminate Heq.
Qed.

(** A diamond with no write in any body: [x] reads [h] and [y], and [m] reads [h], [x] and [y]. [m]'s run reaches [y]'s run
    through [x], whose notice skips [x] and marks [m], so [m] ends [Dirty] holding its flag. Model only: scenario [S15] runs the
    same shape on an effect. *)
Definition diamond_stale (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (CSignal 2 (K 0%Z) (
  CMemo 3 Pure CmpEq (CRead 10 (V 2) (CRet (V 10))) (
  CMemo 4 Pure CmpEq (CRead 10 (V 1) (CRead 11 (V 3) Nop)) (
  CMemo 5 Pure CmpEq (CRead 10 (V 1) (CRead 11 (V 4) (CRead 12 (V 3) Nop))) (
  CRead 20 (V 5) (
  CBatch (CWrite (V 1) (K 1%Z) (CWrite (V 2) (K 1%Z) Nop)) (
  CRead 21 (V 5) k))))))).

Example diamond_ends_memo_stale :
  let s := final (diamond_stale Nop) in
  n_freshness <$> g_nodes s !! (5 : node_id) = Some Dirty /\ n_unannounced <$> g_nodes s !! (5 : node_id) = Some true /\
  n_runs <$> g_nodes s !! (5 : node_id) = Some 2 /\ gh_passed (g_ghost s) = ∅.
Proof. intros s. split_and!; vm_compute; reflexivity. Qed.

(** ** The historic fixes, turned off *)

Local Definition no_reader_mark : flags := {| f_unannounced := true; f_reader_mark := false |}.
Local Definition no_unannounced : flags := {| f_unannounced := false; f_reader_mark := true |}.

Local Lemma wake_ok_of_all s : stale_all_unannounced s -> wake_ok s.
Proof.
  intros H src c _ (d&Hd&Hs) _. right. right. unfold n_unannounced_of. rewrite Hd. simpl. by rewrite (H src d Hd Hs).
Qed.

(** A memo writes its own source, so its first run for the effect ends stale. *)
Definition stale_late_reader : cmd :=
  CSignal 1 (K 0%Z) (CSignal 2 (K 0%Z) (
  CMemo 3 Pure CmpEq
    (CRead 10 (V 1) (CIf (ELt (V 10) (K 3%Z)) (CWrite (V 1) (EAdd (V 10) (K 1%Z)) (CRet (V 10))) (CRet (V 10)))) (
  CEffect 4 (CRead 10 (V 2) (CIf (V 10) (CRead 11 (V 3) (CLog (V 11) Nop)) (CLog (K (-1)%Z) Nop))) (
  CWrite (V 2) (K 1%Z) Nop)))).

(**md
**Removing `MarkReaderCheck` breaks `wake_told`, and `wake_ok` cannot see it.**

*Check, not a guarantee.*

With `f_reader_mark` cleared, the program's final state has an effect clean and idle over a live stale memo it read, with `gh_over` empty, so `wake_told` fails. `no_wake_lost_unless_resolved` has it holding with the flag on. The memo holds its announcement, so `wake_ok` holds on the broken state too: `wake_told` is the statement that detects this mutant.
*)
Theorem reader_mark_off_fires :
  let s_bad := final_with no_reader_mark stale_late_reader in
  let s_good := final_with current_flags stale_late_reader in
  ~ wake_told s_bad /\ wake_told s_good /\ wake_ok s_bad.
Proof.
  intros s_bad s_good. split_and!.
  - set (e := default (signal_node 0%Z) (g_nodes s_bad !! (3 : node_id))).
    set (c := default (signal_node 0%Z) (g_nodes s_bad !! (4 : node_id))).
    assert (He : g_nodes s_bad !! (3 : node_id) = Some e) by (vm_compute; reflexivity).
    assert (Hc : g_nodes s_bad !! (4 : node_id) = Some c) by (vm_compute; reflexivity).
    assert (Hcs : stale (n_freshness c) = false) by (vm_compute; reflexivity).
    assert (Hcq : n_queued c = false) by (vm_compute; reflexivity).
    clearbody c.
    intros H. destruct (H 3 4) as [[(d&Hd&Hs)|(d&Hd&Hq)]|[Hr|Ho]].
    + exists 1, 0. split; vm_compute; reflexivity.
    + exists e. split; [exact He|vm_compute; reflexivity].
    + exists e. split; [exact He|vm_compute; reflexivity].
    + rewrite Hc in Hd. injection Hd as <-. congruence.
    + rewrite Hc in Hd. injection Hd as <-. congruence.
    + vm_compute in Hr. exact Hr.
    + apply bool_of_in in Ho. vm_compute in Ho. discriminate Ho.
  - refine (no_wake_lost_unless_resolved 100000 stale_late_reader (init current_flags) s_good _ engine_settled_init _).
    vm_compute. reflexivity.
  - apply wake_ok_of_all, stale_all_unannounced_of_list. vm_compute. reflexivity.
Qed.

(**md
**Never setting `n_unannounced` breaks `wake_ok`.**

*Check, not a guarantee.*

With `f_unannounced` cleared, a run ending stale sets no flag and a mark forwards no announcement. On [`self_marking_cleanup`](#self_marking_cleanup) the final state has an effect clean and idle over a live stale memo with no flag, so `wake_ok` fails. With the flag on, `wake_ok` holds on that program.
*)
Theorem unannounced_off_fires :
  let s_bad := final_with no_unannounced (self_marking_cleanup Nop) in
  let s_good := final_with current_flags (self_marking_cleanup Nop) in
  ~ wake_ok s_bad /\ wake_ok s_good.
Proof.
  intros s_bad s_good. split.
  - set (e := default (signal_node 0%Z) (g_nodes s_bad !! (5 : node_id))).
    set (c := default (signal_node 0%Z) (g_nodes s_bad !! (6 : node_id))).
    assert (He : g_nodes s_bad !! (5 : node_id) = Some e) by (vm_compute; reflexivity).
    assert (Hc : g_nodes s_bad !! (6 : node_id) = Some c) by (vm_compute; reflexivity).
    intros H. refine (not_covered s_bad 5 6 c e Hc _ _ _ He _ (H 5 6 _ _ _)).
    + vm_compute. reflexivity.
    + vm_compute. reflexivity.
    + vm_compute. reflexivity.
    + vm_compute. reflexivity.
    + exists 0, 0. split; vm_compute; reflexivity.
    + exists e. split; [exact He|vm_compute; reflexivity].
    + exists e. split; [exact He|vm_compute; reflexivity].
  - apply wake_ok_of_all, stale_all_unannounced_of_list. vm_compute. reflexivity.
Qed.

(** ** Flushes *)

(** A batch whose body fails applies its write and skips [RequestFlush], as [RunBatch] does. The effect stays queued
    and [Dirty] with no flush owed, and runs at the next flush. Awaiting ruling in [BUGS.md]. *)
Definition failed_batch : cmd :=
  CSignal 1 (K 0%Z) (
  CEffect 2 (CRead 10 (V 1) (CLog (V 10) Nop)) (
  CBatch (CWrite (V 1) (K 1%Z) (CThrow 5%Z)) Nop)).

Example failed_batch_leaves_queue :
  let s := final failed_batch in
  node_view s 2 = Some (Dirty, false, true, StNone, 0%Z) /\
  pending s = [2] /\ g_flush_owed s = false /\ n_runs <$> g_nodes s !! 2 = Some 1 /\
  (r ← run_flush 1000 s; Some (g_trace r.1)) = Some (g_trace s ++ [EvRun 2; EvLog 2 1%Z]).
Proof. intros s. split_and!; vm_compute; reflexivity. Qed.

(** ** The whole graph *)

Local Lemma pure_b s n : pure_at (shapes s) (Datatypes.S n) n = true -> ~ self_dirtying s n.
Proof. intros H K. exact (K H). Qed.

Local Lemma dirtying_b s n : pure_at (shapes s) (Datatypes.S n) n = false -> self_dirtying s n.
Proof. intros H K. unfold pure_in in K. congruence. Qed.

Local Lemma not_well_behaved s n d :
  g_nodes s !! n = Some d -> n_kind d = KMemo -> self_dirtying s n -> ~ well_behaved_program s.
Proof. intros Hd Hk H K. exact (H (K n d Hd Hk)). Qed.

(** Every effect of the state has id [e]. *)
Local Definition sole_effect (s : state) (e : node_id) : bool :=
  forallb (fun x : node_id * node =>
    match n_kind x.2 with KEffect => bool_decide (x.1 = e) | _ => true end) (map_to_list (g_nodes s)).

Local Lemma sole_effect_is s e c d :
  sole_effect s e = true -> g_nodes s !! c = Some d -> n_kind d = KEffect -> c = e.
Proof.
  intros H Hd Hk. unfold sole_effect in H. rewrite forallb_forall in H.
  assert (Hin : In (c, d) (map_to_list (g_nodes s))) by (apply list_elem_of_In; apply elem_of_map_to_list; exact Hd).
  specialize (H _ Hin). simpl in H. rewrite Hk in H. by apply bool_decide_eq_true in H.
Qed.

Local Lemma effect_missed_write s (e t : node_id) (l : val) (o : outcome) :
  sole_effect s e = true -> (slot_view (g_edges s) e).1 = [t] -> last_log e (g_trace s) = Some l ->
  denotes s t o -> o <> Ok l -> ~ effect_ran_for_last_write s.
Proof.
  intros Hs Hv Hl Ho Hne (c&d&t'&v&Hd&Hk&Ht&Hden&Hlog).
  pose proof (sole_effect_is s e c d Hs Hd Hk) as ->.
  rewrite Hv in Ht. apply list_elem_of_singleton in Ht as ->.
  rewrite Hl in Hlog. injection Hlog as <-.
  apply Hne. exact (denotes_unique s t o (Ok l) Ho Hden).
Qed.

(** Issue 302's program, from conformance scenario S03: three memos in a diamond with a logging memo, written in
    four batches. *)
Definition s03_batch (i : val) : cmd :=
  CWrite (V 2) (K i) (CWrite (V 3) (K i) (CWrite (V 1) (K i) Nop)).

Definition prog_S03 : cmd :=
  CSignal 1 (K 0%Z) (CSignal 2 (K 0%Z) (CSignal 3 (K 0%Z) (
  CMemo 4 Pure CmpEq (CRead 10 (V 1) (CRet (V 10))) (
  CMemo 5 Pure CmpEq (CRead 10 (V 3) (CRead 11 (V 4) (CRet (V 11)))) (
  CMemo 6 Pure CmpEq
    (CRead 10 (V 2) (CRead 11 (V 5) (CRead 12 (V 4) (CLog (V 12) (CRet (EDiv (V 12) (K 2%Z))))))) (
  CEffect 7 (CRead 10 (V 6) (CLog (V 10) Nop)) (
  CBatch (s03_batch 1%Z) (CBatch (s03_batch 2%Z) (CBatch (s03_batch 3%Z) (CBatch (s03_batch 4%Z) Nop)))))))))).

(** Issue 290's program, from conformance scenario S04: a memo writes its own source, and a later write brings an
    effect to read it. *)
Definition prog_S04 : cmd :=
  CSignal 1 (K 0%Z) (CSignal 2 (K 0%Z) (
  CMemo 3 Pure CmpEq
    (CRead 10 (V 1) (CIf (ELt (V 10) (K 3%Z)) (CWrite (V 1) (EAdd (V 10) (K 1%Z)) (CRet (V 10))) (CRet (V 10)))) (
  CEffect 4 (CRead 10 (V 2) (CIf (V 10) (CRead 11 (V 3) (CLog (V 11) Nop)) (CLog (K (-1)%Z) Nop))) (
  CWrite (V 2) (K 1%Z) (CRead 20 (V 1) Nop))))).

(**md
**Regression witness for #302: without the announcement flag, the effect is left behind.**

*Check, not a guarantee.*

`prog_S03` ends in a state where `wake_ok` fails with `f_unannounced` cleared and holds on the present engine, and only the present engine's effect logged what its source now denotes. Without the flag the effect logged 0 and is `Clean` over a `Dirty` memo, whose body answers 2. Every premise of [`well_behaved_nothing_passed`](Ranvier.Pure.WholeGraph.html#well_behaved_nothing_passed) holds on the state, and its conclusion fails: that theorem needs the flag.
*)
Theorem witness_302 :
  exists s_bad s_good r_bad r_good,
    run_top 100000 prog_S03 (init no_unannounced) = Some (s_bad, r_bad) /\
    run_top 100000 prog_S03 (init current_flags) = Some (s_good, r_good) /\
    ~ wake_ok s_bad /\ wake_ok s_good /\ effect_ran_for_last_write s_good /\ ~ effect_ran_for_last_write s_bad /\
    well_behaved_program s_bad /\ effects_only_read s_bad /\ ~ nothing_passed s_bad.
Proof.
  exists (final_with no_unannounced prog_S03), (final_with current_flags prog_S03), (Ok 0%Z), (Ok 0%Z).
  set (s_bad := final_with no_unannounced prog_S03). set (s_good := final_with current_flags prog_S03).
  split_and!.
  - vm_compute. reflexivity.
  - vm_compute. reflexivity.
  - set (e := default (signal_node 0%Z) (g_nodes s_bad !! (6 : node_id))).
    set (c := default (signal_node 0%Z) (g_nodes s_bad !! (7 : node_id))).
    assert (He : g_nodes s_bad !! (6 : node_id) = Some e) by (vm_compute; reflexivity).
    assert (Hc : g_nodes s_bad !! (7 : node_id) = Some c) by (vm_compute; reflexivity).
    intros H. refine (not_covered s_bad 6 7 c e Hc _ _ _ He _ (H 6 7 _ _ _)).
    + vm_compute. reflexivity.
    + vm_compute. reflexivity.
    + vm_compute. reflexivity.
    + vm_compute. reflexivity.
    + exists 0, 0. split; vm_compute; reflexivity.
    + exists e. split; [exact He|vm_compute; reflexivity].
    + exists e. split; [exact He|vm_compute; reflexivity].
  - apply wake_ok_of_all, stale_all_unannounced_of_list. vm_compute. reflexivity.
  - assert (Hd : g_nodes s_good !! (7 : node_id) = Some (default (signal_node 0%Z) (g_nodes s_good !! (7 : node_id)))) by (vm_compute; reflexivity).
    exists 7, (default (signal_node 0%Z) (g_nodes s_good !! (7 : node_id))), 6, 2%Z. split_and!; [exact Hd|vm_compute; reflexivity| |exists 7; vm_compute; reflexivity|vm_compute; reflexivity].
    apply list_elem_of_In. vm_compute. left. reflexivity.
  - apply (effect_missed_write s_bad 7 6 0%Z (Ok 2%Z)).
    + vm_compute. reflexivity.
    + vm_compute. reflexivity.
    + vm_compute. reflexivity.
    + exists 7. vm_compute. reflexivity.
    + discriminate.
  - apply well_behaved_of_b. vm_compute. reflexivity.
  - apply effects_only_read_of_b. vm_compute. reflexivity.
  - intros H. assert (Hin : bool_decide ((7 : node_id) ∈ gh_passed (g_ghost s_bad)) = true) by (vm_compute; reflexivity).
    apply in_of_bool in Hin. unfold nothing_passed in H. rewrite H in Hin. exact (not_elem_of_empty _ Hin).
Qed.

(**md
**Regression witness for #290: without `MarkReaderCheck`, a reader of a stale memo goes untold.**

*Check, not a guarantee.*

`prog_S04` ends in a state where `wake_told` fails with `f_reader_mark` cleared and holds on the present engine. `wake_ok` holds on both: the stale memo holds its flag. This witness has no `effect_ran_for_last_write` clause: a memo that writes its own source denotes nothing.
*)
Theorem witness_290 :
  exists s_bad s_good r_bad r_good,
    run_top 100000 prog_S04 (init no_reader_mark) = Some (s_bad, r_bad) /\
    run_top 100000 prog_S04 (init current_flags) = Some (s_good, r_good) /\
    ~ wake_told s_bad /\ wake_told s_good /\ wake_ok s_bad /\ wake_ok s_good.
Proof.
  exists (final_with no_reader_mark prog_S04), (final_with current_flags prog_S04), (Ok 0%Z), (Ok 0%Z).
  set (s_bad := final_with no_reader_mark prog_S04). set (s_good := final_with current_flags prog_S04).
  split_and!.
  - vm_compute. reflexivity.
  - vm_compute. reflexivity.
  - set (e := default (signal_node 0%Z) (g_nodes s_bad !! (3 : node_id))).
    set (c := default (signal_node 0%Z) (g_nodes s_bad !! (4 : node_id))).
    assert (He : g_nodes s_bad !! (3 : node_id) = Some e) by (vm_compute; reflexivity).
    assert (Hc : g_nodes s_bad !! (4 : node_id) = Some c) by (vm_compute; reflexivity).
    assert (Hcs : stale (n_freshness c) = false) by (vm_compute; reflexivity).
    assert (Hcq : n_queued c = false) by (vm_compute; reflexivity).
    clearbody c.
    intros H. destruct (H 3 4) as [[(d&Hd&Hs)|(d&Hd&Hq)]|[Hr|Ho]].
    + exists 1, 0. split; vm_compute; reflexivity.
    + exists e. split; [exact He|vm_compute; reflexivity].
    + exists e. split; [exact He|vm_compute; reflexivity].
    + rewrite Hc in Hd. injection Hd as <-. congruence.
    + rewrite Hc in Hd. injection Hd as <-. congruence.
    + vm_compute in Hr. exact Hr.
    + apply bool_of_in in Ho. vm_compute in Ho. discriminate Ho.
  - refine (no_wake_lost_unless_resolved 100000 prog_S04 (init current_flags) s_good _ engine_settled_init _).
    vm_compute. reflexivity.
  - apply wake_ok_of_all, stale_all_unannounced_of_list. vm_compute. reflexivity.
  - apply wake_ok_of_all, stale_all_unannounced_of_list. vm_compute. reflexivity.
Qed.

(**md
**`wake_ok` and `wake_told` both hold on the states `self_marking_wake_lost` leaves behind.**

*Check, not a guarantee.*

On [`self_marking_cleanup`](#self_marking_cleanup) and [`self_marking_through_memo`](#self_marking_through_memo) a reader is `Clean` and left behind a source whose next run moves, and both predicates hold. Neither detects this Confirmed bug. Both programs fall outside `well_behaved_program`, so [`convergence`](Ranvier.Pure.WholeGraph.html#convergence) does not reach them.
*)
Theorem wake_predicates_miss_self_marking :
  let s1 := final (self_marking_cleanup Nop) in
  let s2 := final (self_marking_through_memo Nop) in
  left_behind s1 5 6 /\ wake_ok s1 /\ wake_told s1 /\ ~ well_behaved_program s1 /\
  left_behind s2 2 3 /\ wake_ok s2 /\ wake_told s2 /\ ~ well_behaved_program s2.
Proof.
  intros s1 s2. destruct self_marking_cleanup_left_behind as [H1 _]. destruct self_marking_through_memo_left_behind as [H2 _].
  split_and!.
  - exact H1.
  - apply wake_ok_of_all, stale_all_unannounced_of_list. vm_compute. reflexivity.
  - refine (no_wake_lost_unless_resolved 100000 (self_marking_cleanup Nop) (init current_flags) s1 _ engine_settled_init _).
    vm_compute. reflexivity.
  - apply (not_well_behaved s1 4 (default (signal_node 0%Z) (g_nodes s1 !! (4 : node_id)))); [vm_compute; reflexivity|vm_compute; reflexivity|].
    apply dirtying_b. vm_compute. reflexivity.
  - exact H2.
  - apply wake_ok_of_all, stale_all_unannounced_of_list. vm_compute. reflexivity.
  - refine (no_wake_lost_unless_resolved 100000 (self_marking_through_memo Nop) (init current_flags) s2 _ engine_settled_init _).
    vm_compute. reflexivity.
  - apply (not_well_behaved s2 2 (default (signal_node 0%Z) (g_nodes s2 !! (2 : node_id)))); [vm_compute; reflexivity|vm_compute; reflexivity|].
    apply dirtying_b. vm_compute. reflexivity.
Qed.

(** [self_dirtying] holds on each memo the Confirmed self-marking and self-reading bugs leave wrong: the memo that
    writes its source, its reader, and each memo of a cycle. *)
Example self_dirtying_covers_known_cases :
  self_dirtying (final (nested_self_run Nop)) 4 /\
  n_freshness <$> g_nodes (final (nested_self_run Nop)) !! (4 : node_id) = Some Clean /\
  self_dirtying (final (self_marking_through_memo Nop)) 2 /\
  self_dirtying (final (self_marking_through_memo Nop)) 3 /\
  n_freshness <$> g_nodes (final (self_marking_through_memo Nop)) !! (3 : node_id) = Some Clean /\
  self_dirtying (final (self_read_check Nop)) 3 /\
  self_dirtying (final (mutual_read_check Nop)) 3 /\ self_dirtying (final (mutual_read_check Nop)) 4 /\
  ~ self_dirtying (final (self_read_check Nop)) 2.
Proof.
  split_and!; try (apply dirtying_b; vm_compute; reflexivity); try (vm_compute; reflexivity).
  apply pure_b. vm_compute. reflexivity.
Qed.

(** A live effect over a diamond of live memos. *)
Definition live_diamond (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (
  CMemo 2 Pure CmpEq (CRead 10 (V 1) (CRet (EAdd (V 10) (K 1%Z)))) (
  CMemo 3 Pure CmpEq (CRead 10 (V 1) (CRet (EMul (V 10) (K 2%Z)))) (
  CMemo 4 Pure CmpEq (CRead 10 (V 2) (CRead 11 (V 3) (CRet (EAdd (V 10) (V 11))))) (
  CEffect 5 (CRead 10 (V 4) (CLog (V 10) Nop)) (
  CWrite (V 1) (K 1%Z) (CBatch (CWrite (V 1) (K 2%Z) (CWrite (V 1) (K 3%Z) Nop)) k)))))).

(**md
**The premises of `convergence` hold on a live effect over live memos, and the theorem answers the latest value.**

*Check, not a guarantee.*

[`live_diamond`](#live_diamond) reaches every premise of [`convergence`](Ranvier.Pure.WholeGraph.html#convergence) and [`well_behaved_nothing_passed`](Ranvier.Pure.WholeGraph.html#well_behaved_nothing_passed). The effect read the memo at 10, the value its body answers over the signal at 3.
*)
Theorem convergence_reached :
  let s := final (live_diamond Nop) in
  run_top 100000 (live_diamond Nop) (init current_flags) = Some (s, Ok 0%Z) /\
  well_behaved_program s /\ effects_only_read s /\ quiescent s /\
  (exists d, g_nodes s !! (5 : node_id) = Some d /\ n_kind d = KEffect /\ n_disposed d = false) /\
  (exists d, g_nodes s !! (4 : node_id) = Some d /\ n_kind d = KMemo /\ n_disposed d = false) /\
  read_prefix (g_edges s) 5 = [4] /\ denotes s 4 (Ok 10%Z) /\
  forall v, v ∈ seen_of (g_ghost s) 5 4 -> view_answer v = Ok 10%Z.
Proof.
  intros s.
  assert (Hm : run_top 100000 (live_diamond Nop) (init current_flags) = Some (s, Ok 0%Z)) by (vm_compute; reflexivity).
  assert (Ha : well_behaved_program s) by (apply well_behaved_of_b; vm_compute; reflexivity).
  assert (He : effects_only_read s) by (apply effects_only_read_of_b; vm_compute; reflexivity).
  assert (Hq : quiescent s) by (split; vm_compute; reflexivity).
  set (e := default (signal_node 0%Z) (g_nodes s !! (5 : node_id))).
  assert (Hd5 : g_nodes s !! (5 : node_id) = Some e) by (vm_compute; reflexivity).
  assert (Hk5 : n_kind e = KEffect) by (vm_compute; reflexivity).
  assert (Hl5 : n_disposed e = false) by (vm_compute; reflexivity).
  assert (Hpre : read_prefix (g_edges s) 5 = [4]) by (vm_compute; reflexivity).
  assert (Hin : (4 : node_id) ∈ read_prefix (g_edges s) 5) by (rewrite Hpre; apply list_elem_of_singleton; reflexivity).
  assert (Hden : denotes s 4 (Ok 10%Z)) by (exists 5; vm_compute; reflexivity).
  split_and!; try assumption.
  - exists e. split_and!; assumption.
  - exists (default (signal_node 0%Z) (g_nodes s !! (4 : node_id))). split_and!; vm_compute; reflexivity.
  - intros v Hv. destruct (convergence 100000 _ s _ Hm Ha He Hq 5 e 4 Hd5 Hk5 Hl5 Hin) as (_&_&Hall).
    exact (denotes_unique s 4 _ _ (Hall v Hv) Hden).
Qed.

(**md
**`effect_resolves_over_moved_source` breaks the conclusion of `convergence`, and `effects_only_read` is what excludes it.**

*Check, not a guarantee.*

On [`effect_over_moved`](#effect_over_moved) the program is well behaved and quiescent, and the effect's last run read 0 from a memo that denotes 1. `effects_only_read` fails on the effect that writes. Dropping the premise from `convergence` makes it false. The premise is provisional, pending the owner's ruling on the bug.
*)
Theorem effects_only_read_needed :
  let s := final (effect_over_moved Nop) in
  well_behaved_program s /\ quiescent s /\ ~ effects_only_read s /\
  (2 : node_id) ∈ read_prefix (g_edges s) 4 /\
  exists v, v ∈ seen_of (g_ghost s) 4 2 /\ ~ denotes s 2 (view_answer v).
Proof.
  intros s.
  set (e := default (signal_node 0%Z) (g_nodes s !! (4 : node_id))).
  assert (Hd : g_nodes s !! (4 : node_id) = Some e) by (vm_compute; reflexivity).
  assert (Hk : n_kind e = KEffect) by (vm_compute; reflexivity).
  split_and!.
  - apply well_behaved_of_b. vm_compute. reflexivity.
  - split; vm_compute; reflexivity.
  - intros H. destruct (H 4 e Hd Hk) as [_ Hb]. vm_compute in Hb. discriminate Hb.
  - apply list_elem_of_In. vm_compute. left. reflexivity.
  - exists (0%Z, StNone, None). split.
    + apply list_elem_of_In. vm_compute. left. reflexivity.
    + intros Hx. assert (Hy : denotes s 2 (Ok 1%Z)) by (exists 3; vm_compute; reflexivity).
      pose proof (denotes_unique s 2 _ _ Hx Hy) as Hz. vm_compute in Hz. discriminate Hz.
Qed.

(**md
**Falsified on the present engine: a read of a disposed memo answers what it stored, not what its body computes.**

This is a counterexample, not a guarantee. It proves `glitch_free_read_statement` false, on [`disposed_stale_source_read`](#disposed_stale_source_read): the memo is disposed at 0 over a signal at 1, and its body answers 1. Core.fs 4372-4376 documents this answer.
*)
Theorem glitch_free_read_falsified : ~ glitch_free_read_statement.
Proof.
  intros H.
  set (s := final (disposed_stale_read Nop)).
  assert (Hm : run_top 100000 (disposed_stale_read Nop) (init current_flags) = Some (s, Ok 0%Z)) by (vm_compute; reflexivity).
  assert (Hr : read 1000 2 true s = Some (s, Ok 0%Z)) by (vm_compute; reflexivity).
  destruct (H 100000 1000 _ s _ 2 s _ Hm (pure_b s 2 ltac:(vm_compute; reflexivity)) Hr) as [k Hk].
  destruct k as [|[|k]]; vm_compute in Hk; discriminate Hk.
Qed.

(** A write queues effect [9]. Its walk runs memo [5], and memo [4]'s run inside it reads memo [3], whose move leaves
    memo [4] stale. The walk reaches memo [8], whose chain runs memo [4] again. Under [CmpNever] that run counts as a move,
    and marks memo [5] [Dirty] with no announcement. The effect ends [Clean]. Confirmed in [BUGS.md]. *)
Definition cmp_never_source (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (
  CMemo 2 Pure CmpEq (CRead 10 (V 1) (CRet (V 10))) (
  CMemo 3 Pure CmpNever (CRead 10 (V 2) Nop) (
  CMemo 4 Pure CmpNever (CRead 10 (V 1) (CIf (V 10) (CRead 11 (V 3) Nop) (CRead 11 (V 2) Nop))) (
  CMemo 5 Pure CmpEq (CRead 10 (V 1) (CRead 11 (V 4) (CRet (ELt (V 10) (K 100%Z))))) (
  CMemo 6 Pure CmpEq (CRead 10 (V 4) Nop) (
  CMemo 7 Pure CmpEq (CRead 10 (V 6) Nop) (
  CMemo 8 Pure CmpEq (CRead 10 (V 7) Nop) (
  CEffect 9 (CRead 10 (V 5) (CRead 11 (V 8) (CLog (V 10) Nop))) (
  CWrite (V 1) (K 2%Z) k))))))))).

(**md
**A `CmpNever` comparer takes a program with no self-marking to a walk that passes a source.**

*Check, not a guarantee.*

No memo body writes, and every comparer other than `CmpNever` is `CmpEq`, yet the effect ends in `gh_passed`, `Clean` over a live `Dirty` memo with no announcement, and `wake_ok` fails. So [`well_behaved_nothing_passed`](Ranvier.Pure.WholeGraph.html#well_behaved_nothing_passed) needs its exclusion of `CmpNever`: `well_behaved_program` fails here on memo 3.
*)
Theorem cmp_never_passes_source :
  let s := final (cmp_never_source Nop) in
  edge s 5 9 /\ stale_of s 5 /\ live_of s 5 /\ ~ observer_covered s 5 9 /\
  (9 : node_id) ∈ gh_passed (g_ghost s) /\ ~ nothing_passed s /\ ~ wake_ok s /\ ~ well_behaved_program s /\
  pending s = [] /\ g_running s = [] /\ run_flush 1000 s = Some (s, tt) /\
  g_trace s = [EvRun 9; EvLog 9 1%Z].
Proof.
  intros s.
  set (e := default (signal_node 0%Z) (g_nodes s !! (5 : node_id))).
  set (c := default (signal_node 0%Z) (g_nodes s !! (9 : node_id))).
  assert (He : g_nodes s !! (5 : node_id) = Some e) by (vm_compute; reflexivity).
  assert (Hc : g_nodes s !! (9 : node_id) = Some c) by (vm_compute; reflexivity).
  assert (Hedge : edge s 5 9) by (exists 0, 0; split; vm_compute; reflexivity).
  assert (Hst : stale_of s 5) by (exists e; split; [exact He|vm_compute; reflexivity]).
  assert (Hl : live_of s 5) by (exists e; split; [exact He|vm_compute; reflexivity]).
  assert (Hn : ~ observer_covered s 5 9).
  { apply (not_covered s 5 9 c e); [exact Hc|vm_compute; reflexivity|vm_compute; reflexivity|vm_compute; reflexivity|exact He|vm_compute; reflexivity]. }
  assert (Hp : (9 : node_id) ∈ gh_passed (g_ghost s)) by (apply in_of_bool; vm_compute; reflexivity).
  split_and!; try assumption.
  - intros H. unfold nothing_passed in H. rewrite H in Hp. exact (not_elem_of_empty _ Hp).
  - intros H. exact (Hn (H 5 9 Hedge Hst Hl)).
  - apply (not_well_behaved s 3 (default (signal_node 0%Z) (g_nodes s !! (3 : node_id)))); [vm_compute; reflexivity|vm_compute; reflexivity|].
    apply dirtying_b. vm_compute. reflexivity.
  - vm_compute. reflexivity.
  - vm_compute. reflexivity.
  - vm_compute. reflexivity.
  - vm_compute. reflexivity.
Qed.

(** The effect reads memo [6] and memo [9]. As in [cmp_never_source], the walk passes memo [6], and a later run of [CmpNever]
    memo [5] leaves it [Dirty] with no announcement. A write then reaches memo [6] through signal [2], which only memo [6]
    reads, and stops there. A memo returning a fresh record under the default policy maps to [CmpNever]. Confirmed in
    [BUGS.md]. *)
Definition never_lost (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (
  CSignal 2 (K 0%Z) (
  CMemo 3 Pure CmpEq (CRead 10 (V 1) (CRet (V 10))) (
  CMemo 4 Pure CmpNever (CRead 10 (V 3) Nop) (
  CMemo 5 Pure CmpNever (CRead 10 (V 1) (CIf (V 10) (CRead 11 (V 4) Nop) (CRead 11 (V 3) Nop))) (
  CMemo 6 Pure CmpEq (CRead 10 (V 1) (CRead 11 (V 5) (CRead 12 (V 2) (CRet (V 12))))) (
  CMemo 7 Pure CmpEq (CRead 10 (V 5) Nop) (
  CMemo 8 Pure CmpEq (CRead 10 (V 7) Nop) (
  CMemo 9 Pure CmpEq (CRead 10 (V 8) Nop) (
  CEffect 10 (CRead 10 (V 6) (CRead 11 (V 9) (CLog (V 10) Nop))) (
  CWrite (V 1) (K 2%Z) (CWrite (V 2) (K 7%Z) k))))))))))).

(**md
**A memo that counts every run as a move can leave an effect `Clean` on a stale value, for good.**

*Check, not a guarantee.*

No body writes. After [`never_lost`](#never_lost) the effect's only log is 0, its source `6` denotes 7, the queue is empty, nothing is running, and a flush changes nothing. A second write of the signal leaves all of this in place, with the source denoting 8.
*)
Theorem never_lost_update :
  let s := final (never_lost Nop) in
  let s' := final (never_lost (CWrite (V 2) (K 8%Z) Nop)) in
  g_trace s = [EvRun 10; EvLog 10 0%Z] /\ (6 : node_id) ∈ read_prefix (g_edges s) 10 /\
  n_freshness <$> g_nodes s !! (10 : node_id) = Some Clean /\
  seen_of (g_ghost s) 10 6 = [(0%Z, StNone, None)] /\ denotes s 6 (Ok 7%Z) /\
  quiescent s /\ run_flush 1000 s = Some (s, tt) /\
  g_trace s' = [EvRun 10; EvLog 10 0%Z] /\
  n_freshness <$> g_nodes s' !! (10 : node_id) = Some Clean /\ denotes s' 6 (Ok 8%Z) /\
  quiescent s' /\ run_flush 1000 s' = Some (s', tt).
Proof.
  intros s s'. split_and!; try (vm_compute; reflexivity).
  - apply list_elem_of_In. vm_compute. left. reflexivity.
  - exists 7. vm_compute. reflexivity.
  - split; vm_compute; reflexivity.
  - exists 7. vm_compute. reflexivity.
  - split; vm_compute; reflexivity.
Qed.
