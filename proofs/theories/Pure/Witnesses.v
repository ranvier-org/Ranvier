From stdpp Require Import prelude.
From stdpp Require Import gmap.
From Ranvier.Base Require Import Prelude.
From Ranvier.Base Require Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList State Marking.

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
    running. Closed in [BUGS.md]. *)
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
