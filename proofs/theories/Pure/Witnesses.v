From stdpp Require Import prelude.
From stdpp Require Import gmap.
From Ranvier.Base Require Import Prelude.
From Ranvier.Base Require Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList State Marking Engine.

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
     g_untracked := false; g_owner := None; g_next := 3; g_trace := []; g_running := running |}.

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
