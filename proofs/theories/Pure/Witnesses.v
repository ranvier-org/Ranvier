From stdpp Require Import prelude.
From stdpp Require Import gmap.
From Ranvier.Base Require Import Prelude.
From Ranvier.Base Require Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList State Marking Engine Check.

(** ** Walks *)

Local Ltac walk_steps :=
  repeat first
    [ eapply walk_visit; [reflexivity|reflexivity|reflexivity|]
    | apply walk_end; reflexivity ].

(** Observer 1 holds two positions from the first visit on, and the walk misses it: removing position
    0 moves observer 3 there, and position 2 sits above the cursor. *)
Example walk_covers_duplicate_observer :
  let o := {| oitems := [(1, 0); (2, 0)]; olatest := 1 |} in
  let muts := [[WAdd 1 1; WAdd 3 0; WRemove 1 0]; []] in
  (exists o', walk (length (oitems o)) o muts [2; 3] o') /\
  Forall (fun p => 1 ∈ obs_view p) (mut_points (concat muts) o) /\
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
  let o := {| oitems := [(1, 0); (2, 0); (3, 0)]; olatest := 2 |} in
  exists o', walk (length (oitems o)) o [[WRemove 1 0]; []; []] [3; 2; 3] o'.
Proof. simpl. eexists. walk_steps. Qed.

(** An observer added during a walk is visited when a later removal moves it below the cursor. *)
Example walk_visits_added_observer :
  let o := {| oitems := [(1, 0); (2, 0); (3, 0); (4, 0)]; olatest := 3 |} in
  exists o', walk (length (oitems o)) o [[WAdd 5 0; WRemove 1 0]; []; []; []] [4; 3; 2; 5] o'.
Proof. simpl. eexists. walk_steps. Qed.

(** ** Marks *)

Local Definition clean_node (k : kind) (cleanup disposed : bool) : node :=
  {| n_kind := k; n_value := 0%Z; n_failure := None; n_status := StNone; n_fresh := Clean;
     n_unann := false; n_runs := 0; n_published := false; n_disposed := disposed; n_queued := false;
     n_in_cleanup := cleanup; n_scope := None; n_violated := false;
     n_body := Syntax.CRet (Syntax.EConst 0%Z); n_env := []; n_mode := Syntax.Pure;
     n_cmp := Syntax.CmpEq; n_link := None |}.

Local Definition mark_state (w : EdgeWorld) (nodes : gmap nid node) (q : list nid) (h : nat)
    (running : list nid) : state :=
  {| g_flags := current_flags; g_edges := w; g_nodes := nodes; g_owners := ∅; g_queue := q;
     g_head := h; g_flushing := false; g_owed := false; g_batch := 0; g_pull := 0; g_current := None;
     g_untracked := false; g_owner := None; g_next := 3; g_trace := []; g_running := running;
     g_ghost := empty_ghost |}.

(** Computation 2, in a run, has read source 1. *)
Local Definition linked_world : EdgeWorld :=
  default empty_world (sl_add 2 1 (sl_begin 2 empty_world)).

Local Lemma linked_world_reachable : ew_reachable linked_world.
Proof.
  apply (er_add 2 1 (sl_begin 2 empty_world)); [apply er_begin, er_empty|]. vm_compute. reflexivity.
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
  - exact (proj1 (reachable_wf _ linked_world_reachable)).
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
  ew_reachable (g_edges s) /\ edge s 1 2 /\ 2 ∈ g_running s /\
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
    [g_head], so [effect_mark_queues] counts [pending]. F# releases the consumed slot. *)
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

Local Definition node_view (s : state) (n : nid) : option (fresh * bool * bool * status * val) :=
  d ← g_nodes s !! n; Some (n_fresh d, n_unann d, n_queued d, n_status d, n_value d).

(** Every stale node holds [n_unann], which is the last disjunct of [wake_ok]. *)
Local Definition stale_all_unann (s : state) : Prop :=
  forall n d, g_nodes s !! n = Some d -> stale (n_fresh d) = true -> n_unann d = true.

Local Lemma stale_all_unann_of_list s :
  forallb (fun p : nid * node => negb (stale (n_fresh p.2)) || n_unann p.2) (map_to_list (g_nodes s)) = true ->
  stale_all_unann s.
Proof.
  intros H n d Hd Hst.
  assert (Hin : In (n, d) (map_to_list (g_nodes s))).
  { apply list_elem_of_In, elem_of_map_to_list, Hd. }
  pose proof (proj1 (forallb_forall _ _) H (n, d) Hin) as Hp.
  simpl in Hp. by rewrite Hst in Hp.
Qed.

(** The end state a lost wake-up leaves: [x] is stale and holds [n_unann], its observer [e] is clean
    and unqueued, nothing is live or running, and a flush changes nothing. Confirmed in [BUGS.md]. *)
Local Definition left_behind (s : state) (x e : nid) : Prop :=
  edge s x e /\ stale_all_unann s /\
  (exists f v, node_view s x = Some (f, true, false, StNone, v) /\ stale f = true) /\
  (exists v, node_view s e = Some (Clean, false, false, StNone, v)) /\
  pending s = [] /\ g_running s = [] /\ run_flush 1000 s = Some (s, tt).

Local Ltac left_behind_proof i h :=
  split_and!;
    [ exists i, h; split; vm_compute; reflexivity
    | apply stale_all_unann_of_list; vm_compute; reflexivity
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
  pending s = [6] /\ g_owed s = false /\ n_runs <$> g_nodes s !! 6 = Some 1 /\
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
  oitems (obs_of (g_edges s) 4) = [(5, 0); (4, 2)] /\
  pending s = [] /\ g_running s = [] /\ run_flush 1000 s = Some (s, tt) /\
  oitems (obs_of (g_edges s) 2) = [] /\ n_value <$> g_nodes s !! 1 = Some 3%Z /\
  n_value <$> g_nodes s !! 3 = Some 20%Z /\
  (2 ∉ fst <$> sslots (src_of (g_edges s) 4)) /\ 3 ∈ fst <$> sslots (src_of (g_edges s) 4).
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
  n_fresh <$> g_nodes s !! 3 = Some Check /\ sslots (src_of (g_edges s) 3) !! 0 = Some (3, Some 0) /\
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
  sslots (src_of (g_edges (final (self_read_second Nop))) 3) !! 1 = Some (3, Some 0) /\
  n_fresh <$> g_nodes (final (self_read_second Nop)) !! 3 = Some Check /\
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
  n_fresh <$> g_nodes (final (mutual_read_check Nop)) !! 3 = Some Check /\
  n_fresh <$> g_nodes (final (mutual_read_check Nop)) !! 4 = Some Check /\
  is_Some (trace_of 2000 current_flags (mutual_read_check Nop)) /\
  trace_of 500 current_flags (mutual_read_check (CTryRead 28 29 (K 3%Z) Nop)) = None /\
  trace_of 2000 current_flags (mutual_read_check (CTryRead 28 29 (K 3%Z) Nop)) = None.
Proof. split_and!; try (vm_compute; reflexivity). vm_compute. eexists. reflexivity. Qed.

(** ** Check resolution *)

Lemma check_inv_final p : check_inv (final p).
Proof.
  unfold final. destruct (run_top 100000 p (init current_flags)) as [[s r]|] eqn:E; simpl.
  - eapply check_inv_run_top; [apply check_inv_init|exact E].
  - apply check_inv_init.
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

Theorem resolve_clean_no_move_falsified : ~ resolve_clean_no_move_statement.
Proof.
  intros H.
  destruct check_resolves_over_moved_source as (_ & _ & Hu & _ & _ & _ & Hm & _).
  destruct (g_nodes over_moved_start !! 4) as [d|] eqn:Hd; [|vm_compute in Hd; discriminate Hd].
  destruct (g_nodes over_moved_end !! 4) as [d'|] eqn:Hd'; [|vm_compute in Hd'; discriminate Hd'].
  refine (H 1000 4 over_moved_start over_moved_end d d' (check_inv_final _) Hd _ _ Hu Hd' _ Hm).
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
  n_fresh <$> g_nodes cutoff_start !! 3 = Some Check /\
  update_if_necessary 1000 3 cutoff_start = Some (cutoff_end, tt) /\
  n_fresh <$> g_nodes cutoff_end !! 3 = Some Clean /\
  n_runs <$> g_nodes cutoff_end !! 3 = n_runs <$> g_nodes cutoff_start !! 3 /\
  reads_settled cutoff_end 3 /\ ~ moved_since_last_run cutoff_end 3.
Proof.
  assert (Hset : reads_settled cutoff_end 3).
  { apply settled_of; vm_compute; reflexivity. }
  split_and!; try (vm_compute; reflexivity); [done|].
  destruct (g_nodes cutoff_start !! 3) as [d|] eqn:Hd; [|vm_compute in Hd; discriminate Hd].
  destruct (g_nodes cutoff_end !! 3) as [d'|] eqn:Hd'; [|vm_compute in Hd'; discriminate Hd'].
  assert (Hu : update_if_necessary 1000 3 cutoff_start = Some (cutoff_end, tt)) by (vm_compute; reflexivity).
  refine (resolve_clean_unmoved 1000 3 cutoff_start cutoff_end d d' (check_inv_final _) Hd _ _ Hu Hd' _ Hset).
  all: vm_compute in Hd; vm_compute in Hd'; injection Hd as <-; injection Hd' as <-; reflexivity.
Qed.

(** [self_marking_wake_lost] does not reach [moved_since_last_run]: the reader goes clean over a [Dirty]
    source whose stored value it read. *)
Example self_marking_through_memo_unmoved :
  let s := final (self_marking_through_memo Nop) in
  n_fresh <$> g_nodes s !! 2 = Some Dirty /\ n_fresh <$> g_nodes s !! 3 = Some Clean /\
  reads_settled s 3 /\ ~ moved_since_last_run s 3.
Proof.
  intros s.
  assert (Hset : reads_settled s 3).
  { apply settled_of; vm_compute; reflexivity. }
  split_and!; try (vm_compute; reflexivity); [done|].
  destruct (g_nodes s !! 3) as [d|] eqn:Hd; [|vm_compute in Hd; discriminate Hd].
  refine (unmoved_short_of_dirty s 3 d (check_inv_final _) Hd _ _ Hset).
  all: subst s; vm_compute in Hd; injection Hd as <-; first [reflexivity|discriminate].
Qed.

(** The same skip through a second read of the source: [x] reads [a], writes [a]'s source, reads [a] again,
    and answers the first read. Its latest read of [a] holds the current view, and its first does not. *)
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

Theorem resolve_clean_no_stale_source_falsified : ~ resolve_clean_no_stale_source_statement.
Proof.
  intros H.
  assert (Hu : update_if_necessary 1000 3 stale_start = Some (stale_end, tt)) by (vm_compute; reflexivity).
  destruct (g_nodes stale_start !! 3) as [d|] eqn:Hd; [|vm_compute in Hd; discriminate Hd].
  destruct (g_nodes stale_end !! 3) as [d'|] eqn:Hd'; [|vm_compute in Hd'; discriminate Hd'].
  destruct (g_nodes stale_end !! 2) as [e|] eqn:He; [|vm_compute in He; discriminate He].
  assert (Hin : 2 ∈ fst <$> sslots (src_of (g_edges stale_end) 3)) by (vm_compute; left).
  assert (Hst : stale (n_fresh e) = false).
  { assert (Hlive : n_disposed e = false) by (vm_compute in He; injection He as <-; reflexivity).
    refine (H 1000 3 stale_start stale_end d d' (check_inv_final _) Hd _ _ Hu Hd' _ 2 e Hin He Hlive).
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
  n_fresh <$> g_nodes disposed_source_end !! 2 = Some Dirty /\
  n_disposed <$> g_nodes disposed_source_end !! 2 = Some true.
Proof. split_and!; vm_compute; reflexivity. Qed.

(** A cleanup of a memo's nested run disposes the memo in [RecomputeScoped]'s window, with the flag of that
    nested run. The memo goes from [Check] to [Clean] holding [n_unann]. Awaiting ruling in [BUGS.md]. *)
Definition disposed_window (k : cmd) : cmd :=
  CSignal 1 (K 0%Z) (CSignal 2 (K 0%Z) (
  CMemo 3 Pure CmpEq (CRead 10 (V 1) (CRet (V 10))) (
  CMemo 4 Owning CmpEq
    (CRead 10 (V 3) (CIf (EEq (V 10) (K 0%Z)) Nop (
     CRead 11 (V 2) (CIf (EEq (V 11) (K 0%Z))
       (CWrite (V 2) (K 1%Z) (CReadU 12 (K 4%Z) (CReadU 13 (K 4%Z) Nop)))
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
