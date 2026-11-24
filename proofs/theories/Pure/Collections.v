From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith Sorted PArith NArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList State Edges Marking Engine Check Runs Scheduler Ownership.

Open Scope nat_scope.

(**md # Collection-boundary primitives (pure tier)

This module proves the exact minimum specifications required by Ranvier's collection primitives
(`Projections.fs`, `Combinators.fs`, `ValueReaders.fs`, `Bindings.fs`) at the boundary with `Core.fs`:

1. **Direct-observer edge boundary** (`direct_link`, `direct_link_preserves_well_formed`,
   `direct_link_is_sources_add`, `direct_unlink_preserves_well_formed`,
   `direct_slot_remove_preserves_match`, `direct_observer_walk_covered`): proves the simulation
   contract for `Binding`, `FoldReads`, `FoldRow`, `ValueRow`, and `ForwardingEdge`, which call
   `AddObserver` and `RemoveObserverAt` directly without a `SourceList` run cursor.
2. **Active mid-list `OwnerLink` unlinking and per-key child scopes** (`unlink_active_removes_child`,
   `retire_unlinked_child_idempotent`): proves that dynamically retiring a per-key projection or
   keyed-collection scope removes its `OwnerLink` from a live parent owner while preserving strict
   acquisition order (`acq_ok`) for remaining sibling keys and cleanups.
3. **Two-queue pass scheduling and `QueueDrain` equivalence** (`pass_queue_once`,
   `pass_runs_before_effect`, `run_passes_returns_clean`, `core_queue_drain_refines_priority`):
   captures the minimal pass-before-effect priority contract for `SchedulePass`/`RunPasses` and
   proves that `Core.fs`'s `queueHead - 1` `PassDrain` slot preemption refines the direct two-queue
   priority loop.
4. **Collection signal and row hooks** (`write_signal_except_wakes_others`,
   `notify_removed_wakes_observers`, `adopt_companion_disposed_once`,
   `fold_sync_recomputes_on_reorder`): proves `Signal.WriteExcept` /
   `ProjectionBeacon.NotifyFailure`, `Memo.NotifyRemoved`, `Memo.Adopt`, and non-invertible
   `Projection.fold` recomputation on pure key reorders.
5. **Pass diff zero-removal cardinality skip and key-direct derived rows** (`apply_diff_zero_removals_iff`,
   `key_direct_row_retire_wakes_without_item_signal`): certifies skipping `entries.Iterate` in
   `Projection.ApplyDiff` whenever `entries.Count + AddedCount = seen.Count`, and certifies `RowsByKey`
   derived rows that link directly to upstream rows without a per-row `Signal<'K>`.
6. **Binary tournament winner tree and sticky-holder extremes** (`tournament_winner_optimal_and_first`,
   `tournament_point_update_refines_eval`, `sticky_holder_refines_extreme_scan`): certifies left-biased
   tournament winner selection, O(log N) `SiftUp` with early stop and zero-comparison capacity doubling,
   and sticky tie retention for `Projection.tryMinBy` and `Projection.tryMaxBy`. *)

(** ** Direct-observer edge boundary *)

(**md
Direct observer attachment (`AddObserver(self, slot)`) at the end of `self`'s slots, used by `Binding`,
`FoldRow`, `ValueRow`, `ForwardingEdge` (at `slot = 0`), and `FoldReads` (at `slot = order.Count - 1`,
guarded by a `HashSet` of sources).

- [self:] the direct observer attaching to `s`.
- [s:] the source observed.
- [w:] the edge world before the attachment.
*)
Definition direct_link (self s : node_id) (w : EdgeWorld) : EdgeWorld :=
  let slot := length (source_slots (sources_of w self)) in
  let p := observers_add self slot (observers_of w s) in
  sources_append self s (Some p.2) p.1 w.

Lemma direct_link_eq_sources_link self s w :
  world_well_formed w ->
  s ∉ fst <$> source_slots (sources_of w self) ->
  direct_link self s w = sources_link self s w.
Proof.
  intros Hw Hnot.
  destruct (sources_link_cases self s w) as [[_ [Hobs|Hin]]|[Heq _]].
  - exfalso. apply Hnot. by apply (held_iff_slot w self s Hw).
  - contradiction.
  - unfold direct_link. by rewrite Heq.
Qed.

(**md
**Direct observer attachment preserves `world_well_formed` and gives the observer a single entry in the source's observer set.**

Covers `Binding`, `FoldRow`, `ValueRow`, `ForwardingEdge` (where `source_slots` is empty before linking)
and `FoldReads.AddSource` (where a `HashSet` ensures `s ∉ fst <$> source_slots (sources_of w self)`).

- [self:] the direct observer attaching to `s`.
- [s:] the source observed.
- [w:] the well-formed edge world before the attachment.
*)
Theorem direct_link_preserves_well_formed self s w :
  world_well_formed w ->
  s ∉ fst <$> source_slots (sources_of w self) ->
  world_well_formed (direct_link self s w) /\
  held_once self (observers_of (direct_link self s w) s) /\
  source_cursor (sources_of (direct_link self s w) self) =
    length (source_slots (sources_of (direct_link self s w) self)).
Proof.
  intros Hw Hnot.
  rewrite (direct_link_eq_sources_link self s w Hw Hnot).
  pose proof (well_formed_link self s w Hw) as Hw'.
  rewrite <- (direct_link_eq_sources_link self s w Hw Hnot) in *.
  split_and!; [done| |].
  - apply well_formed_held_once; [done|].
    unfold direct_link, sources_append.
    rewrite sources_of_set_observers, sources_of_set_sources, decide_True by done.
    simpl. rewrite fmap_app. apply elem_of_app. right. simpl. by left.
  - unfold direct_link, sources_append.
    rewrite sources_of_set_observers, sources_of_set_sources, decide_True by done.
    simpl. rewrite length_app. simpl. lia.
Qed.

(**md
**A direct observer link with its cursor at the end of its slots is an exact `sources_add` step and stays in `world_reachable`.**

Formalises the boundary simulation in `CORRESPONDENCE.md`: each direct `AddObserver` call on a node whose
cursor sits at the end of its deduplicated slots equals `sources_add`, so arbitrary interleavings of
`SourceList` computations and direct collection observers remain within `world_reachable` and `world_point`.

- [self:] the direct observer attaching to `s`.
- [s:] the source observed, not yet present in `self`'s slots.
- [w:] the well-formed edge world before the attachment.
*)
Theorem direct_link_is_sources_add self s w :
  world_well_formed w ->
  source_cursor (sources_of w self) = length (source_slots (sources_of w self)) ->
  s ∉ fst <$> source_slots (sources_of w self) ->
  sources_add self s w = Some (direct_link self s w) /\
  (world_reachable w -> world_reachable (direct_link self s w)).
Proof.
  intros Hw Hcur Hnot.
  assert (Hadd : sources_add self s w = Some (direct_link self s w)).
  { unfold sources_add. rewrite Hcur.
    rewrite decide_False.
    - rewrite decide_False.
      + unfold sources_end_run. rewrite Hcur, decide_False by lia. simpl.
        by rewrite (direct_link_eq_sources_link self s w Hw Hnot).
      + intros [Hpos Hpred]. apply Hnot.
        destruct (source_slots (sources_of w self) !! pred (length (source_slots (sources_of w self))))
          as [[s0 mh]|] eqn:E; simplify_eq/=.
        apply list_elem_of_fmap. exists (s, mh). split; [done|].
        by eapply list_elem_of_lookup_2.
    - rewrite lookup_ge_None_2 by lia. done. }
  split; [done|].
  intros Hr. by eapply reachable_add.
Qed.

(**md
**Detaching all edges of a direct observer preserves `world_well_formed` and removes the observer from every source.**

Covers `Binding.Dispose`, `FoldRow.Dispose`, `ValueRow.Detach`, `ForwardingEdge.Clear`, and `FoldReads.Clear`.

- [self:] the direct observer detaching from its sources.
- [w:] the well-formed edge world before the detachment.
*)
Theorem direct_unlink_preserves_well_formed self w :
  world_well_formed w ->
  exists w',
    sources_clear self w = Some w' /\
    world_well_formed w' /\
    source_slots (sources_of w' self) = [] /\
    (forall s, self ∉ observer_view (observers_of w' s)) /\
    (world_reachable w -> world_reachable w').
Proof.
  intros Hw.
  destruct (well_formed_clear self w Hw) as (w' & Hcl & Hw').
  destruct (sources_clear_ok self w (proj1 Hw)) as (w'' & Hcl' & _ & Hsl & _ & _ & Hgone).
  rewrite Hcl in Hcl'. injection Hcl' as <-.
  exists w'. split_and!; [done|done|done|done|].
  intros Hr. by eapply reachable_clear.
Qed.

(**md
**Removing a single slot of a direct observer preserves `edge_match`, `slots_unique`, and `observers_once`.**

When `RemoveObserverAt` swap-removes the last observer in the source's `ObserverSet` into the vacated index and
invokes `MovedIn`, both ends of every remaining edge stay matched and no observer appears twice.

- [self:] the observer whose slot `i` is removed.
- [i:] the slot index removed.
- [w:] the edge world before the removal.
- [w':] the edge world after `sources_remove_slot`.
*)
Theorem direct_slot_remove_preserves_match self i w w' :
  edge_match w ->
  slots_unique w ->
  sources_remove_slot self i w = Some w' ->
  edge_match w' /\ slots_unique w' /\ observers_once w'.
Proof.
  intros Hm Hu Hrem.
  assert (Ht : trim_loop self i 1 w = Some w').
  { simpl. by rewrite Hrem. }
  destruct (trim_every_removal self i 1 w w' Hm Hu Ht) as (Hm' & Hu' & Ho' & _).
  done.
Qed.

(**md
**Every attached direct observer is visited by `ObserverSet`'s backward walk despite concurrent swap-removals.**

In any `world_point` (including arbitrary interleavings of `SourceList` computations and `direct_link` /
`direct_unlink` observers), every attached observer `c` of `s` occupies a unique slot in `observers_of w s`
(`held_once`) and is therefore visited by `walk` (`NotifyDirty`, `NotifyDirtyExcept`, `NotifyCheck`) whenever
each intermediate mutation point comes from a `world_point` in which `c` remains attached.

- [w:] a `world_point` edge world.
- [s:] the source whose observers are walked.
- [c:] an attached observer of `s`.
- [muts:] the per-visit mutation lists applied during the walk.
- [vis:] the list of observers visited by the walk.
- [o':] the observer set at the end of the walk.
*)
Theorem direct_observer_walk_covered w s c (muts : list (list walk_mutation))
    (vis : list node_id) (o' : observer_set) :
  walk (length (observer_items (observers_of w s))) (observers_of w s) muts vis o' ->
  Forall (fun ok => exists wk, world_point wk /\ ok = observers_of wk s /\ c ∈ observer_view ok)
    (mutation_points (concat muts) (observers_of w s)) ->
  c ∈ vis.
Proof.
  intros Hw Hpts.
  eapply walk_covers; [exact Hw|].
  eapply Forall_impl; [exact Hpts|].
  intros ok (wk & Hwp & -> & Hin).
  by apply observer_held_once.
Qed.

(** ** Active mid-list OwnerLink unlinking and per-key child scopes *)

(**md
**Unlinking an active child link from a live parent owner removes the child and preserves strict acquisition order (`acq_ok`).**

When a projection key or keyed-collection row is retired while its parent scope is still live
(`l_generation l = Some (o_generation y)`), `unlink` removes `l` from `o_children`, preserves `acq_ok`,
and keeps all remaining sibling children in their exact acquisition order.

- [l:] the link being detached (`parentLink.Detach()`).
- [o:] the parent owner id (`l_owner l`).
- [y:] the parent owner's state in `s`.
- [s:] the graph state before `unlink`.
*)
Theorem unlink_active_removes_child (l : link) (o : owner_id) (y : owner_state) (s : state) :
  l_owner l = o ->
  g_owners s !! o = Some y ->
  l_generation l = Some (o_generation y) ->
  acq_ok y ->
  unlink l s = Some (with_owners (<[o := owner_without l y]> (g_owners s)) s, tt) /\
  acq_ok (owner_without l y) /\
  l ∉ o_children (owner_without l y) /\
  (forall m, m ∈ o_children (owner_without l y) <->
     m ∈ o_children y /\ ~ (l_child m = l_child l /\ l_acquired m = l_acquired l)).
Proof.
  intros <- Hy Hgen Hacq.
  split_and!.
  - unfold unlink, m_bind, get_owner. rewrite Hy, decide_True by done. reflexivity.
  - by apply acq_without.
  - simpl. rewrite list_elem_of_filter. intros [Hneg _]. by apply Hneg.
  - intros m. simpl. rewrite list_elem_of_filter. tauto.
Qed.

(**md
**Retiring an already-disposed per-key child scope is a no-op on owners and trace.**

Once a removed key's scope has been retired during a projection pass (`entry.Scope.Dispose()`), any later
`retire` call on that scope id leaves `g_owners` and `g_trace` unchanged.

- [fuel:] the fuel, which `retire` runs at `S fuel`.
- [o:] the retired child owner id.
- [y:] the owner state of `o` in `s`, with `o_disposed y = true`.
- [s:] the state before `retire`.
- [s':] the state after `retire`.
*)
Theorem retire_unlinked_child_idempotent (fuel : nat) (o : owner_id) (y : owner_state) (s s' : state) :
  g_owners s !! o = Some y ->
  o_disposed y = true ->
  live_queue s = false ->
  retire (S fuel) o s = Some (s', tt) ->
  g_owners s' = g_owners s /\ g_trace s' = g_trace s.
Proof.
  intros Hy Hdisp Hlq Hret.
  cbn [retire] in Hret.
  unfold detached, m_bind, get_owner, m_modify, m_return, m_get, live_queue in Hret, Hlq.
  simpl in Hret. rewrite Hy, Hdisp in Hret. simpl in Hret.
  rewrite Hlq in Hret. simpl in Hret.
  injection Hret as <-. done.
Qed.

(** ** Two-queue pass scheduling and QueueDrain equivalence *)

(**md State of the two-queue scheduler (`passes` and `queue` in `Core.fs` lines 2155–2167, 3090–3215). *)
Record two_queue_state := {
  tq_passes : list node_id;
  tq_pass_head : nat;
  tq_queue : list node_id;
  tq_queue_head : nat;
  tq_pass_set : gset node_id;
  tq_effect_set : gset node_id
}.

Definition pending_passes (st : two_queue_state) : list node_id :=
  drop (tq_pass_head st) (tq_passes st).

Definition pending_effects (st : two_queue_state) : list node_id :=
  drop (tq_queue_head st) (tq_queue st).

(**md `Graph.SchedulePass`: enqueues a pass-publishing node at most once. *)
Definition schedule_pass_tq (n : node_id) (st : two_queue_state) : two_queue_state :=
  if decide (n ∈ tq_pass_set st) then st
  else {| tq_passes := tq_passes st ++ [n];
          tq_pass_head := tq_pass_head st;
          tq_queue := tq_queue st;
          tq_queue_head := tq_queue_head st;
          tq_pass_set := {[n]} ∪ tq_pass_set st;
          tq_effect_set := tq_effect_set st |}.

(**md `Graph.Schedule`: enqueues a normal effect at most once. *)
Definition schedule_effect_tq (n : node_id) (st : two_queue_state) : two_queue_state :=
  if decide (n ∈ tq_effect_set st) then st
  else {| tq_passes := tq_passes st;
          tq_pass_head := tq_pass_head st;
          tq_queue := tq_queue st ++ [n];
          tq_queue_head := tq_queue_head st;
          tq_pass_set := tq_pass_set st;
          tq_effect_set := {[n]} ∪ tq_effect_set st |}.

(** Invariant of the two-queue scheduler: heads within bounds, no duplicate pending entries, and queued sets
    contain all pending entries. *)
Record tq_inv (st : two_queue_state) : Prop := {
  tqi_pass_head : tq_pass_head st <= length (tq_passes st);
  tqi_queue_head : tq_queue_head st <= length (tq_queue st);
  tqi_pass_nodup : NoDup (pending_passes st);
  tqi_effect_nodup : NoDup (pending_effects st);
  tqi_pass_set : forall x, x ∈ pending_passes st -> x ∈ tq_pass_set st;
  tqi_effect_set : forall x, x ∈ pending_effects st -> x ∈ tq_effect_set st
}.

(**md
**`SchedulePass` and `Schedule` preserve the two-queue single-entry invariant `tq_inv`.**

Each projection pass or lookup refresh is pending at most once in `tq_passes`, and each normal effect is
pending at most once in `tq_queue`.

- [n:] the node scheduled.
- [st:] the two-queue state before scheduling.
*)
Theorem pass_queue_once (n : node_id) (st : two_queue_state) :
  tq_inv st ->
  tq_inv (schedule_pass_tq n st) /\ tq_inv (schedule_effect_tq n st).
Proof.
  intros [Hph Hqh Hpn Hen Hps Hes]. split.
  - unfold schedule_pass_tq. destruct (decide (n ∈ tq_pass_set st)) as [Hin|Hnin]; [by split|].
    split; simpl; try done.
    + rewrite length_app. simpl. lia.
    + unfold pending_passes in *. simpl. rewrite drop_app_le by done.
      apply NoDup_app. split_and!; [done| |apply NoDup_singleton].
      intros x Hx Hxn. apply list_elem_of_singleton in Hxn as ->.
      apply Hnin, Hps, Hx.
    + unfold pending_passes in *. simpl. rewrite drop_app_le by done.
      intros x Hx. apply elem_of_app in Hx as [Hx|Hx].
      * apply elem_of_union_r, Hps, Hx.
      * apply list_elem_of_singleton in Hx as ->. set_solver.
  - unfold schedule_effect_tq. destruct (decide (n ∈ tq_effect_set st)) as [Hin|Hnin]; [by split|].
    split; simpl; try done.
    + rewrite length_app. simpl. lia.
    + unfold pending_effects in *. simpl. rewrite drop_app_le by done.
      apply NoDup_app. split_and!; [done| |apply NoDup_singleton].
      intros x Hx Hxn. apply list_elem_of_singleton in Hxn as ->.
      apply Hnin, Hes, Hx.
    + unfold pending_effects in *. simpl. rewrite drop_app_le by done.
      intros x Hx. apply elem_of_app in Hx as [Hx|Hx].
      * apply elem_of_union_r, Hes, Hx.
      * apply list_elem_of_singleton in Hx as ->. set_solver.
Qed.

(**md Next action chosen by the minimal two-queue priority scheduler: always a pass when `pending_passes` is
    non-empty, and an effect only when `pending_passes` is empty. *)
Inductive tq_action :=
  | ActPass (p : node_id)
  | ActEffect (e : node_id)
  | ActIdle.

Definition next_tq_action (st : two_queue_state) : tq_action :=
  match tq_passes st !! tq_pass_head st with
  | Some p => ActPass p
  | None =>
      match tq_queue st !! tq_queue_head st with
      | Some e => ActEffect e
      | None => ActIdle
      end
  end.

(**md
**No normal effect is selected while any projection or lookup pass is pending (`pass_runs_before_effect`).**

This is the minimum scheduling specification required by collections: whenever `pending_passes st` is
non-empty, the next scheduled action is `ActPass p` (the head of `pending_passes st`) and never `ActEffect e`.

- [st:] the two-queue scheduler state.
- [p:] the first pending pass.
- [ps:] the remaining pending passes.
*)
Theorem pass_runs_before_effect (st : two_queue_state) (p : node_id) (ps : list node_id) :
  pending_passes st = p :: ps ->
  next_tq_action st = ActPass p /\ (forall e, next_tq_action st <> ActEffect e).
Proof.
  intros Hpend. unfold pending_passes in Hpend.
  assert (Hlook : tq_passes st !! tq_pass_head st = Some p).
  { rewrite <- (Nat.add_0_r (tq_pass_head st)).
    rewrite <- (lookup_drop (tq_passes st) (tq_pass_head st) 0).
    by rewrite Hpend. }
  unfold next_tq_action. rewrite Hlook. split; [done|by intros].
Qed.

(**md `Graph.RunPasses`: drains `tq_passes` using `run_one_pass` until no pass remains, then resets `tq_passes`
    and `tq_pass_head`. *)
Fixpoint run_passes_loop (fuel : nat) (run_one_pass : node_id -> two_queue_state -> two_queue_state)
    (st : two_queue_state) : option (two_queue_state * list node_id) :=
  match fuel with
  | 0 => None
  | S f =>
      match tq_passes st !! tq_pass_head st with
      | Some p =>
          let st1 := {| tq_passes := tq_passes st;
                        tq_pass_head := S (tq_pass_head st);
                        tq_queue := tq_queue st;
                        tq_queue_head := tq_queue_head st;
                        tq_pass_set := tq_pass_set st ∖ {[p]};
                        tq_effect_set := tq_effect_set st |} in
          let st2 := run_one_pass p st1 in
          match run_passes_loop f run_one_pass st2 with
          | Some (st3, trace) => Some (st3, p :: trace)
          | None => None
          end
      | None =>
          Some ({| tq_passes := [];
                   tq_pass_head := 0;
                   tq_queue := tq_queue st;
                   tq_queue_head := tq_queue_head st;
                   tq_pass_set := tq_pass_set st;
                   tq_effect_set := tq_effect_set st |}, [])
      end
  end.

(**md
**`run_passes_loop` returns only when the pass queue is completely empty and reset to head `0`.**

Even when executing a pass schedules downstream passes (for example, a projection over another projection's
`Keys`), `RunPasses` loops until `pending_passes` is empty before yielding control back to the effect queue.

- [fuel:] the step budget.
- [run_one_pass:] the transition of a single pass execution.
- [st:] the initial two-queue state.
- [st':] the two-queue state after `run_passes_loop`.
- [trace:] the sequence of passes executed.
*)
Theorem run_passes_returns_clean (fuel : nat)
    (run_one_pass : node_id -> two_queue_state -> two_queue_state)
    (st st' : two_queue_state) (trace : list node_id) :
  run_passes_loop fuel run_one_pass st = Some (st', trace) ->
  pending_passes st' = [] /\ tq_pass_head st' = 0 /\ tq_passes st' = [].
Proof.
  revert st st' trace.
  induction fuel as [|f IH]; intros st st' trace Hrun; simpl in Hrun; [done|].
  destruct (tq_passes st !! tq_pass_head st) as [p|].
  - destruct (run_passes_loop f run_one_pass _) as [[st3 tr]|] eqn:E; [|done].
    injection Hrun as <- <-. by eapply IH.
  - injection Hrun as <- <-. done.
Qed.

(**md One iteration of the direct two-queue priority loop after `RunPasses` has drained initial passes:
    dequeue effect `e`, run `run_one_effect e`, and then immediately drain any passes scheduled by `e` via
    `run_passes_loop`. *)
Definition priority_effect_step (fuel : nat)
    (run_one_pass : node_id -> two_queue_state -> two_queue_state)
    (run_one_effect : node_id -> two_queue_state -> two_queue_state)
    (st : two_queue_state) : option (two_queue_state * list node_id) :=
  match tq_queue st !! tq_queue_head st with
  | None => Some (st, [])
  | Some e =>
      let st1 := {| tq_passes := tq_passes st;
                    tq_pass_head := tq_pass_head st;
                    tq_queue := tq_queue st;
                    tq_queue_head := S (tq_queue_head st);
                    tq_pass_set := tq_pass_set st;
                    tq_effect_set := tq_effect_set st ∖ {[e]} |} in
      let st2 := run_one_effect e st1 in
      match run_passes_loop fuel run_one_pass st2 with
      | Some (st3, ptr) => Some (st3, e :: ptr)
      | None => None
      end
  end.

(**md `Core.fs`'s `QueueDrain` mechanism (`Core.fs` lines 3090–3106, 3190–3204): when a pass is scheduled
    while `flushing` after dequeueing at `queueHead` (so `tq_queue_head = S h`), `QueueDrain` decrements
    `queueHead` back to `h` and places `passDrain` in slot `h`; the next dequeue pops `passDrain` (advancing
    `queueHead` back to `S h`) and invokes `RunPasses`. *)
Definition core_queue_drain_step (fuel : nat) (pass_drain_id : node_id)
    (run_one_pass : node_id -> two_queue_state -> two_queue_state)
    (run_one_effect : node_id -> two_queue_state -> two_queue_state)
    (st : two_queue_state) : option (two_queue_state * list node_id) :=
  match tq_queue st !! tq_queue_head st with
  | None => Some (st, [])
  | Some e =>
      let h := tq_queue_head st in
      let st1 := {| tq_passes := tq_passes st;
                    tq_pass_head := tq_pass_head st;
                    tq_queue := tq_queue st;
                    tq_queue_head := S h;
                    tq_pass_set := tq_pass_set st;
                    tq_effect_set := tq_effect_set st ∖ {[e]} |} in
      let st2 := run_one_effect e st1 in
      (* QueueDrain preemption: decrement queue_head to h, write pass_drain_id at slot h,
         then dequeue pass_drain_id on the next iteration (advancing queue_head to S h) and run_passes *)
      let st_preempt := {| tq_passes := tq_passes st2;
                           tq_pass_head := tq_pass_head st2;
                           tq_queue := <[h := pass_drain_id]> (tq_queue st2);
                           tq_queue_head := h;
                           tq_pass_set := tq_pass_set st2;
                           tq_effect_set := tq_effect_set st2 |} in
      let st_after_pop := {| tq_passes := tq_passes st_preempt;
                             tq_pass_head := tq_pass_head st_preempt;
                             tq_queue := tq_queue st2;
                             tq_queue_head := tq_queue_head st2;
                             tq_pass_set := tq_pass_set st_preempt;
                             tq_effect_set := tq_effect_set st_preempt |} in
      match run_passes_loop fuel run_one_pass st_after_pop with
      | Some (st3, ptr) => Some (st3, e :: ptr)
      | None => None
      end
  end.

(**md
**`Core.fs`'s `QueueDrain` (`queueHead - 1` preemption) refines the direct two-queue priority step.**

Certifies the optimisation space in `Core.fs`: mutating `queue[queueHead - 1] <- passDrain` during effect
execution (leaving `pending_effects` after `S h` untouched) and popping `passDrain` on the next iteration
produces the exact same state and execution trace as a direct two-queue loop that checks `passHead < passCount`
before dequeueing the next effect.

- [fuel:] the step budget for `run_passes_loop`.
- [pass_drain_id:] the sentinel id for `passDrain`.
- [run_one_pass:] the transition of a single pass execution.
- [run_one_effect:] the transition of a single effect execution.
- [st:] the two-queue state before the step.
*)
Theorem core_queue_drain_refines_priority (fuel : nat) (pass_drain_id : node_id)
    (run_one_pass : node_id -> two_queue_state -> two_queue_state)
    (run_one_effect : node_id -> two_queue_state -> two_queue_state)
    (st : two_queue_state) :
  core_queue_drain_step fuel pass_drain_id run_one_pass run_one_effect st =
  priority_effect_step fuel run_one_pass run_one_effect st /\
  (forall q h, drop (S h) (<[h := pass_drain_id]> q) = drop (S h) q).
Proof.
  split.
  - unfold core_queue_drain_step, priority_effect_step.
    destruct (tq_queue st !! tq_queue_head st) as [e|]; [|done].
    simpl.
    destruct (run_one_effect e _) as [passes phead queue qhead pset eset].
    simpl. reflexivity.
  - intros q h. revert q. induction h as [|h IH]; intros [|x q]; simpl; [done|done|done|by apply IH].
Qed.

(** ** Collection signal and row hooks *)

(**md
`Signal.WriteExcept` (`Core.fs` line 3992) and `ProjectionBeacon.NotifyFailure` (`Projections.fs` line 90):
writes `v` to signal `n` and marks every observer dirty except the computation `r` currently pulling the
projection (unless `r` has already read `n` in its current run).

- [fuel:] the step budget.
- [n:] the signal written.
- [v:] the new value.
- [r:] the computation to skip (`Some running` or `None`).
*)
Definition write_signal_except (fuel : nat) (n : node_id) (v : val) (r : option node_id) : M unit :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    match n_kind d with
    | KSignal =>
      if decide (n_value d = v) then m_return tt
      else
        let* _ := modify_node n (node_with_value v) in
        let* _ := m_modify (ghost_publish n) in
        let* _ := m_step (notify_dirty_except f n r) in
        request_flush f
    | _ => stuck
    end
  end.

(**md
**`write_signal_except` marks every observer other than `r` dirty while skipping `r` when `n` is not in `r`'s read prefix.**

Matches the minimum specification for `anyPending.WriteExcept(v, running)` and
`inFlightVersion.WriteExcept(v, running)` in `Projections.fs`: all other readers of the summary signal are
woken (`ends_dirty`), while the currently pulling computation `running` is not self-marked if it has not yet
recorded `n` in `read_prefix`.

- [fuel:] the marking fuel.
- [n:] the signal written.
- [v:] the new value written.
- [r:] the excluded computation (`Some running`).
- [d:] the signal node in `s`.
- [s:] the state before the write.
- [s1:] the state after `notify_dirty_except`.
- [m:] an observer of `n` that takes marks.
*)
Theorem write_signal_except_wakes_others (fuel : nat) (n : node_id) (v : val)
    (r : option node_id) (d : node) (s s1 : state) (m : node_id) :
  g_nodes s !! n = Some d ->
  n_kind d = KSignal ->
  m ∈ observer_view (observers_of (g_edges s) n) ->
  takes_marks s m ->
  (r <> Some m \/ n ∈ read_prefix (g_edges s) m) ->
  notify_dirty_except fuel n r (ghost_publish n (set_node n (node_with_value v d) s)) = Some s1 ->
  ends_dirty s1 m /\
  (forall running s0,
     r = Some running ->
     n ∉ read_prefix (g_edges s0) running ->
     mark_except fuel n r running s0 = Some s0).
Proof.
  intros Hd Hk Hin Ht Hcond Hnot.
  set (s_pub := ghost_publish n (set_node n (node_with_value v d) s)) in *.
  assert (Hedges : g_edges s_pub = g_edges s) by (subst s_pub; unfold ghost_publish; simpl; rewrite lookup_insert_eq; done).
  assert (Ht_pub : takes_marks s_pub m).
  { destruct Ht as (dm & Hdm & Hkm).
    assert (Hne : m <> n).
    { intros ->. rewrite Hd in Hdm. injection Hdm as <-.
      destruct Hkm as [Hkm|[Hkm _]]; congruence. }
    exists dm. subst s_pub. unfold ghost_publish. simpl.
    rewrite lookup_insert_eq. simpl. rewrite lookup_insert_ne by done. done. }
  split.
  - eapply (notify_dirty_ends_dirty fuel n r s_pub s1 m).
    + by rewrite Hedges.
    + exact Ht_pub.
    + right. rewrite Hedges. done.
  - intros running s0 -> Hnp. unfold mark_except.
    rewrite decide_True by done. reflexivity.
Qed.

(**md
`Memo.NotifyRemoved` (`Core.fs` line 4660, called in `Projections.fs` line 772 before `entry.Row.Dispose()`):
walks the row memo's observers with `notify_dirty` so any reader of a removed key wakes to observe the key's
departure before `dispose_node` clears the row's upstream sources.

- [fuel:] the marking fuel.
- [n:] the retiring row memo id.
*)
Definition notify_removed (fuel : nat) (n : node_id) : M unit :=
  m_step (notify_dirty fuel n).

(**md
**`notify_removed` marks every live observer of a retiring row memo `Dirty`.**

When `Projections.fs` retires a removed key's row, calling `notify_removed` before `dispose_node` ensures
every live observer `m` of row `n` ends `Dirty` (and is queued if `m` is an effect), even though `dispose_node`
subsequently marks `n` disposed so `n` itself never recomputes again.

- [fuel:] the marking fuel.
- [n:] the retiring row memo id.
- [s:] the state before `notify_removed`.
- [s1:] the state after `notify_removed`.
- [m:] an observer of `n` that takes marks.
*)
Theorem notify_removed_wakes_observers (fuel : nat) (n : node_id) (s s1 : state) (m : node_id) :
  m ∈ observer_view (observers_of (g_edges s) n) ->
  takes_marks s m ->
  notify_removed fuel n s = Some (s1, tt) ->
  ends_dirty s1 m.
Proof.
  intros Hin Ht Hnr.
  unfold notify_removed, m_step in Hnr.
  destruct (notify_dirty fuel n s) as [s'|] eqn:E; [|done].
  injection Hnr as <-.
  eapply (notify_dirty_ends_dirty fuel n None s s' m); [done|done|by left].
Qed.

(** ** Memo.Adopt companion resource lifecycle *)

(**md State of a memo's adopted companion (`adopted : IDisposable` in `Core.fs` lines 4094, 4647–4651,
    4683–4696, used by `Combinators.fs` and `Projections.fs` to tie auxiliary views/readers to a memo's
    lifetime). *)
Record adopt_state := {
  ad_disposed : bool;
  ad_current : option nat;
  ad_disposed_log : list nat
}.

Definition empty_adopt_state : adopt_state :=
  {| ad_disposed := false; ad_current := None; ad_disposed_log := [] |}.

(**md `Memo.Adopt(companion)` (`Core.fs` lines 4683–4696):

- if the memo is already disposed, disposes `c` immediately;
- if the memo is live and holds a distinct companion `prev`, disposes `prev` and stores `Some c`;
- if the memo holds `None` or already holds `Some c`, stores `Some c` without disposing.
*)
Definition memo_adopt_step (c : nat) (st : adopt_state) : adopt_state :=
  if ad_disposed st then
    {| ad_disposed := true;
       ad_current := ad_current st;
       ad_disposed_log := ad_disposed_log st ++ [c] |}
  else
    match ad_current st with
    | None =>
        {| ad_disposed := false;
           ad_current := Some c;
           ad_disposed_log := ad_disposed_log st |}
    | Some prev =>
        if decide (prev = c) then st
        else
          {| ad_disposed := false;
             ad_current := Some c;
             ad_disposed_log := ad_disposed_log st ++ [prev] |}
    end.

(**md `Memo.Dispose` companion release (`Core.fs` lines 4647–4651): clears `adopted` and disposes it once. *)
Definition memo_dispose_adopted (st : adopt_state) : adopt_state :=
  if ad_disposed st then st
  else
    match ad_current st with
    | None =>
        {| ad_disposed := true;
           ad_current := None;
           ad_disposed_log := ad_disposed_log st |}
    | Some c =>
        {| ad_disposed := true;
           ad_current := None;
           ad_disposed_log := ad_disposed_log st ++ [c] |}
    end.

Fixpoint adopt_many (cs : list nat) (st : adopt_state) : adopt_state :=
  match cs with
  | [] => st
  | c :: cs' => adopt_many cs' (memo_adopt_step c st)
  end.

Lemma adopt_many_live_distinct (cs : list nat) (cur : nat) (log : list nat) :
  NoDup (cur :: cs) ->
  memo_dispose_adopted
    (adopt_many cs {| ad_disposed := false; ad_current := Some cur; ad_disposed_log := log |}) =
  {| ad_disposed := true; ad_current := None; ad_disposed_log := log ++ cur :: cs |}.
Proof.
  revert cur log.
  induction cs as [|c cs IH]; intros cur log Hnd; simpl.
  - reflexivity.
  - apply NoDup_cons in Hnd as [Hne Hnd'].
    assert (Hcur_ne : cur <> c) by (intros ->; apply Hne; left).
    unfold memo_adopt_step; simpl.
    rewrite decide_False by done.
    rewrite (IH c (log ++ [cur]) Hnd').
    rewrite <- app_assoc. reflexivity.
Qed.

(**md
**Every distinct companion passed to `Memo.Adopt` is disposed in order and exactly once when the memo is disposed.**

Whether a memo adopts one companion (as in `Projection.foldGroupBy` / `Projection.map`), replaces its
companion across multiple `Adopt` calls, or adopts a companion after the memo is already disposed, every
distinct companion is disposed once and `ad_current` ends `None`.

- [cs:] a list of distinct companion ids passed to `Memo.Adopt`.
*)
Theorem adopt_companion_disposed_once (cs : list nat) :
  NoDup cs ->
  let st' := memo_dispose_adopted (adopt_many cs empty_adopt_state) in
  ad_disposed st' = true /\
  ad_current st' = None /\
  ad_disposed_log st' = cs /\
  NoDup (ad_disposed_log st') /\
  (forall c, ad_disposed_log (memo_adopt_step c st') = cs ++ [c]).
Proof.
  intros Hnd st'.
  assert (Hst : st' = {| ad_disposed := true; ad_current := None; ad_disposed_log := cs |}).
  { subst st'. destruct cs as [|c0 cs'].
    - reflexivity.
    - cbn [adopt_many]. unfold empty_adopt_state, memo_adopt_step at 1; simpl.
      by rewrite (adopt_many_live_distinct cs' c0 [] Hnd). }
  rewrite Hst. simpl.
  split_and!; [done|done|done|exact Hnd|intros c; unfold memo_adopt_step; simpl; done].
Qed.

(** ** Non-invertible Projection.fold order sensitivity *)

Definition fold_cached_keys {K V S : Type} `{Countable K}
    (folder : S -> V -> S) (zero : S) (vals : gmap K V) (keys : list K) : S :=
  foldl (fun acc k => match vals !! k with Some v => folder acc v | None => acc end) zero keys.

Definition fold_sync_step {K V S : Type} `{Countable K}
    (invertible had_keys order_changed full : bool)
    (folder : S -> V -> S) (zero state : S) (vals : gmap K V) (next_keys : list K) : S * bool :=
  let full' := if negb invertible && had_keys && order_changed then true else full in
  if full' then (fold_cached_keys folder zero vals next_keys, false) else (state, false).

(**md
**When `invertible = false` (`Projection.fold`), any non-empty key-order change (`had_keys = true`, `order_changed = true`) triggers `Recompute` so the state equals `fold_cached_keys folder zero vals next_keys` even when zero keys were added, removed, or edited (`Combinators.fs` lines 1030–1060).**

Conversely, when `invertible = true` (`Projection.foldGroup`), a pure reorder with `full = false` preserves `state` without invoking `Recompute`.

- [folder:] the fold accumulator function.
- [zero:] the initial fold seed.
- [state:] the fold state prior to `Sync`.
- [vals:] the settled cached row values.
- [next_keys:] the new upstream `Keys` sequence.
- [full:] the `full` recompute flag entering `Sync`.
*)
Theorem fold_sync_recomputes_on_reorder {K V S : Type} `{Countable K}
    (folder : S -> V -> S) (zero state : S) (vals : gmap K V) (next_keys : list K) (full : bool) :
  fold_sync_step false true true full folder zero state vals next_keys =
    (fold_cached_keys folder zero vals next_keys, false) /\
  fold_sync_step true true true false folder zero state vals next_keys =
    (state, false).
Proof.
  unfold fold_sync_step; simpl.
  split; reflexivity.
Qed.

(** ** Pass diff zero-removal cardinality skip and key-direct derived rows *)

Lemma gset_size_list_to_set_nodup `{Countable K} (l : list K) :
  NoDup l ->
  size (list_to_set l : gset K) = length l.
Proof.
  induction 1 as [|x l Hnin Hnd IH].
  - cbn [list_to_set length]. apply (fin_sets.size_empty (C := gset K)).
  - cbn [list_to_set length]. rewrite fin_sets.size_union.
    + rewrite fin_sets.size_singleton, IH. lia.
    + apply disjoint_singleton_l. by rewrite elem_of_list_to_set.
Qed.

(**md
**In `Projection.ApplyDiff`, `size entries + length adds = size seen` holds if and only if zero live keys were removed (`dom entries ∖ seen = ∅`).**

During `Enumerate`, every visited key is added to `seen` without duplicates, and `adds` collects every
visited key absent from `entries` (`entries !! k = None`). Because `dom entries` partitions into
`(dom entries ∩ seen) ⊎ (dom entries ∖ seen)` while `seen` partitions into
`(dom entries ∩ seen) ⊎ list_to_set adds`, we obtain the exact removal-count identity
`size entries + length adds = size seen + size (dom entries ∖ seen)`. Thus `ApplyDiff` (`Projections.fs`
lines 987–993) can skip iterating `entries` altogether whenever `entries.Count + Pass.AddedCount = seen.Count`,
including when failed rows are hidden via `HideLast` (which leaves `entries`, `seen`, and `adds` intact).

- [entries:] the live row map at the start of `ApplyDiff`.
- [seen:] the set of keys visited by `Enumerate`.
- [adds:] the list of distinct new keys staged during `Enumerate`.
*)
Theorem apply_diff_zero_removals_iff `{Countable K} {V : Type}
    (entries : gmap K V) (seen : gset K) (adds : list K) :
  NoDup adds ->
  (forall k, k ∈ adds -> k ∈ seen /\ entries !! k = None) ->
  (forall k, k ∈ seen -> is_Some (entries !! k) \/ k ∈ adds) ->
  size entries + length adds = size seen + size (dom entries ∖ seen) /\
  size seen <= size entries + length adds /\
  (size entries + length adds = size seen <->
   dom entries ∖ seen = ∅ /\ (forall k, is_Some (entries !! k) -> k ∈ seen)).
Proof.
  intros Hnd Hadds Hseen.
  set (I := dom entries ∩ seen).
  set (R := dom entries ∖ seen).
  set (A := (list_to_set adds : gset K)).
  assert (Hdom_eq : dom entries = I ∪ R).
  { apply leibniz_equiv. intros x. subst I R.
    destruct (decide (x ∈ seen)); set_solver. }
  assert (Hdom_disj : I ## R) by (subst I R; set_solver).
  assert (Hseen_eq : seen = I ∪ A).
  { apply leibniz_equiv. intros x. subst I A. split.
    - intros Hx. destruct (Hseen x Hx) as [Hsome|Hadd].
      + apply elem_of_union_l, elem_of_intersection. split; [by apply elem_of_dom|exact Hx].
      + apply elem_of_union_r. by apply elem_of_list_to_set.
    - intros [Hx|Hx]%elem_of_union.
      + by apply elem_of_intersection in Hx as [_ Hx].
      + apply elem_of_list_to_set in Hx. by destruct (Hadds x Hx) as [Hx' _]. }
  assert (Hseen_disj : I ## A).
  { subst I A. intros x HxI HxA.
    apply elem_of_intersection in HxI as [Hxdom _].
    apply elem_of_dom in Hxdom as [v Hv].
    apply elem_of_list_to_set in HxA.
    destruct (Hadds x HxA) as [_ Hnone]. congruence. }
  assert (Hsz_dom : size entries = size I + size R).
  { rewrite <- size_dom, Hdom_eq. by apply fin_sets.size_union. }
  assert (Hsz_seen : size seen = size I + length adds).
  { rewrite Hseen_eq, (fin_sets.size_union I A Hseen_disj).
    subst A. rewrite (gset_size_list_to_set_nodup adds Hnd). lia. }
  assert (Hid : size entries + length adds = size seen + size R) by lia.
  split_and!; [exact Hid | lia |].
  split.
  - intros Heq.
    assert (HszR : size R = 0) by lia.
    apply fin_sets.size_empty_iff in HszR. apply leibniz_equiv in HszR.
    split; [exact HszR|].
    intros k Hk. apply elem_of_dom in Hk.
    destruct (decide (k ∈ seen)) as [Hin|Hnin]; [exact Hin|].
    exfalso. assert (HkR : k ∈ R) by (subst R; set_solver).
    rewrite HszR in HkR. set_solver.
  - intros [Hemp _]. subst R. rewrite Hemp, fin_sets.size_empty in Hid. lia.
Qed.

(**md
**A key-direct derived row (`RowsByKey`) occupies one inline source slot for its upstream row and still wakes `Dirty` when the upstream row retires.**

Certifies the `RowsByKey` optimisation in `Projections.fs` and `Combinators.fs`: derived view rows (`MapView`,
`SliceView`, `FilterView`, `DistinctView`, `SortView`, `GroupView`, `perKey`) read `upstream` by a fixed key
`'K` rather than through an intermediate per-row `Signal<'K>`. Linking directly to `up_row` keeps the row memo
at a single source slot (`length = 1`) when `up_row` is its sole source, and retiring `up_row` via
`notify_removed` marks `row_m` `Dirty` without any item-signal write.

- [fuel:] the marking fuel.
- [row_m:] the derived view's row memo id.
- [up_row:] the upstream projection's row memo id.
- [s:] the graph state before `up_row` retires.
- [s1:] the graph state after `notify_removed fuel up_row`.
*)
Theorem key_direct_row_retire_wakes_without_item_signal
    (fuel : nat) (row_m up_row : node_id) (s s1 : state) :
  world_well_formed (g_edges s) ->
  source_slots (sources_of (g_edges s) row_m) = [] ->
  takes_marks s row_m ->
  let w' := direct_link row_m up_row (g_edges s) in
  let s_linked := with_edges w' s in
  length (source_slots (sources_of w' row_m)) = 1 /\
  held_once row_m (observers_of w' up_row) /\
  (notify_removed fuel up_row s_linked = Some (s1, tt) -> ends_dirty s1 row_m).
Proof.
  intros Hwf Hempty Ht w' s_linked.
  assert (Hnot : up_row ∉ fst <$> source_slots (sources_of (g_edges s) row_m)).
  { rewrite Hempty. simpl. set_solver. }
  destruct (direct_link_preserves_well_formed row_m up_row (g_edges s) Hwf Hnot)
    as (Hwf' & Hheld & _).
  assert (Hlen : length (source_slots (sources_of w' row_m)) = 1).
  { subst w'. unfold direct_link, sources_append.
    rewrite sources_of_set_observers, sources_of_set_sources, decide_True by done.
    simpl. rewrite Hempty. reflexivity. }
  split_and!; [exact Hlen | exact Hheld |].
  intros Hnr.
  eapply (notify_removed_wakes_observers fuel up_row s_linked s1 row_m).
  - simpl. apply (held_iff_slot w' row_m up_row Hwf').
    subst w'. unfold direct_link, sources_append.
    rewrite sources_of_set_observers, sources_of_set_sources, decide_True by done.
    simpl. rewrite Hempty. simpl. by left.
  - destruct Ht as (dm & Hdm & Hkm). exists dm. simpl. done.
  - exact Hnr.
Qed.

(** ** Single-projection ChooseView boolean gate cutoff and classification *)

Inductive gate_reading (T : Type) :=
  | GR_Ready : T -> gate_reading T
  | GR_Pending : gate_reading T
  | GR_Failed : nat -> gate_reading T.
Arguments GR_Ready {T} _.
Arguments GR_Pending {T}.
Arguments GR_Failed {T} _.

Inductive choose_outcome :=
  | CO_Visit
  | CO_Exclude
  | CO_HeldOut
  | CO_HideFailed.

Record choice_gate_state := mk_choice_gate {
  cg_published : bool;
  cg_peek : bool;
  cg_last : option (gate_reading bool);
}.

Definition choice_gate_init : choice_gate_state :=
  {| cg_published := false; cg_peek := false; cg_last := None |}.

Definition gate_reading_bool_eqb (a b : gate_reading bool) : bool :=
  match a, b with
  | GR_Ready x, GR_Ready y => Bool.eqb x y
  | GR_Pending, GR_Pending => true
  | GR_Failed e1, GR_Failed e2 => Nat.eqb e1 e2
  | _, _ => false
  end.

Definition step_choice_gate {U : Type}
    (gs : choice_gate_state) (r : gate_reading (option U))
    : choice_gate_state * gate_reading bool * bool :=
  let gr :=
    match r with
    | GR_Ready (Some _) => GR_Ready true
    | GR_Ready None => GR_Ready false
    | GR_Pending => GR_Pending
    | GR_Failed eid => GR_Failed eid
    end in
  let '(pub', peek') :=
    match gr with
    | GR_Ready b => (true, b)
    | _ => (cg_published gs, cg_peek gs)
    end in
  let moved :=
    match cg_last gs with
    | None => true
    | Some prev => negb (gate_reading_bool_eqb prev gr)
    end in
  ({| cg_published := pub'; cg_peek := peek'; cg_last := Some gr |}, gr, moved).

Definition classify_choice_gate (gs : choice_gate_state) (gr : gate_reading bool) : choose_outcome :=
  match gr with
  | GR_Ready true => CO_Visit
  | GR_Ready false => CO_Exclude
  | GR_Pending =>
      if negb (cg_published gs) then CO_HeldOut
      else if cg_peek gs then CO_Visit else CO_Exclude
  | GR_Failed _ => CO_HideFailed
  end.

(**md
**`ChooseView`'s per-key boolean gate cuts off every `Some u1 -> Some u2` edit (`moved = false`) while classifying Ready, Pending, and Failed chooser outcomes identically to a two-projection `Option.isSome` `FilterView`.**

Certifies the `ChooseView` single-projection fusion in `Combinators.fs`:
1. After settling `GR_Ready (Some u1)`, a subsequent `GR_Ready (Some u2)` transition produces `moved = false`
   on the boolean gate memo, resolving `ChooseView`'s `Check` walk to `Clean` with zero view passes while
   `choices.Get` yields `Some u2`.
2. Across every `Ready`, `Pending` (both unsettled and previously settled), and `Failed` transition,
   `classify_choice_gate` matches `FilterView`'s `predicate = true` classification over `Option.isSome`.

- [u1:] the previously published `Some` payload.
- [u2:] the updated `Some` payload.
- [gs:] the gate state prior to the step.
- [r:] the chooser reading for the current step.
*)
Theorem choose_gate_cutoff_and_classification {U : Type}
    (u1 u2 : U) (gs : choice_gate_state) (r : gate_reading (option U)) :
  (let '(gs1, gr1, _) := step_choice_gate gs (GR_Ready (Some u1)) in
   let '(gs2, gr2, moved2) := step_choice_gate gs1 (GR_Ready (Some u2)) in
   moved2 = false /\
   classify_choice_gate gs1 gr1 = CO_Visit /\
   classify_choice_gate gs2 gr2 = CO_Visit) /\
  (let '(gs', gr, _) := step_choice_gate gs r in
   classify_choice_gate gs' gr =
     match r with
     | GR_Ready (Some _) => CO_Visit
     | GR_Ready None => CO_Exclude
     | GR_Pending =>
         if negb (cg_published gs) then CO_HeldOut
         else if cg_peek gs then CO_Visit else CO_Exclude
     | GR_Failed _ => CO_HideFailed
     end).
Proof.
  split.
  - simpl. split_and!; reflexivity.
  - destruct r as [[u|]| |eid]; simpl; reflexivity.
Qed.

(** ** Lazy HeldOut cell and staging buffer refinement *)

Record eager_held_out (K : Type) := mk_eager_ho {
  eho_cell : list K;
  eho_pass : list K;
  eho_observed : bool;
}.
Arguments mk_eager_ho {K} _ _ _.
Arguments eho_cell {K} _.
Arguments eho_pass {K} _.
Arguments eho_observed {K} _.

Record lazy_held_out (K : Type) := mk_lazy_ho {
  lho_cell : option (list K);
  lho_pass : option (list K);
  lho_observed : bool;
}.
Arguments mk_lazy_ho {K} _ _ _.
Arguments lho_cell {K} _.
Arguments lho_pass {K} _.
Arguments lho_observed {K} _.

Definition held_out_rel {K : Type} (e : eager_held_out K) (l : lazy_held_out K) : Prop :=
  eho_cell e = default [] (lho_cell l) /\
  eho_pass e = default [] (lho_pass l) /\
  eho_observed e = (match lho_cell l with Some _ => lho_observed l | None => false end) /\
  (lho_cell l = None -> lho_observed l = false).

Definition eager_ho_read {K : Type} (e : eager_held_out K) : eager_held_out K * list K :=
  ({| eho_cell := eho_cell e; eho_pass := eho_pass e; eho_observed := true |}, eho_cell e).

Definition lazy_ho_read {K : Type} (l : lazy_held_out K) : lazy_held_out K * list K :=
  let c := default [] (lho_cell l) in
  ({| lho_cell := Some c; lho_pass := lho_pass l; lho_observed := true |}, c).

Definition eager_ho_publish `{EqDecision K}
    (e : eager_held_out K) (extra : list K) : eager_held_out K * bool :=
  let staged := eho_pass e ++ extra in
  let moved := negb (bool_decide (eho_cell e = staged)) in
  let cell' := if moved then staged else eho_cell e in
  ({| eho_cell := cell'; eho_pass := []; eho_observed := eho_observed e |},
   moved && eho_observed e).

Definition lazy_ho_publish `{EqDecision K}
    (l : lazy_held_out K) (extra : list K) : lazy_held_out K * bool :=
  let pass1 :=
    match extra with
    | [] => lho_pass l
    | _ => Some (default [] (lho_pass l) ++ extra)
    end in
  let staged := default [] pass1 in
  match staged with
  | [] =>
      match lho_cell l with
      | None => ({| lho_cell := None; lho_pass := None; lho_observed := lho_observed l |}, false)
      | Some [] => ({| lho_cell := Some []; lho_pass := None; lho_observed := lho_observed l |}, false)
      | Some (_ :: _) =>
          ({| lho_cell := Some []; lho_pass := None; lho_observed := lho_observed l |}, lho_observed l)
      end
  | _ :: _ =>
      let cur := default [] (lho_cell l) in
      let moved := negb (bool_decide (cur = staged)) in
      let cell' := if moved then staged else cur in
      ({| lho_cell := Some cell'; lho_pass := None; lho_observed := lho_observed l |},
       moved && lho_observed l)
  end.

(**md
**Lazily allocating `HeldOut`'s signal cell and staging list refines an eager `Signal<'K[]>` initialized to `Array.empty`, preserving read values, observer status, and wake notifications while allocating nothing on empty passes.**

Certifies the `HeldOut<'K>` lazy cell and pass-buffer optimisation in `Combinators.fs`:
1. Initially `(eho = ([], [], false), lho = (None, None, false))` satisfies `held_out_rel` with zero allocated signal or buffer.
2. Reading `Keys`/`Cell` allocates `Some []` on demand, marks it observed, returns the exact same slice as the eager cell, and preserves `held_out_rel`.
3. Publishing any combination of staged keys and inherited `PendingExtra` keys preserves `held_out_rel`, leaves `lho_cell = None` whenever no key has ever been held out or read, and wakes observers iff the eager cell wakes observers.

- [e:] the reference eager `HeldOut` state.
- [l:] the lazy `HeldOut` state.
- [extra:] the upstream `PendingExtra` slice for the current pass.
*)
Theorem lazy_held_out_refines_eager `{EqDecision K}
    (e : eager_held_out K) (l : lazy_held_out K) (extra : list K) :
  @held_out_rel K {| eho_cell := []; eho_pass := []; eho_observed := false |}
                  {| lho_cell := None; lho_pass := None; lho_observed := false |} /\
  (held_out_rel e l ->
   let '(e_r, kr_e) := eager_ho_read e in
   let '(l_r, kr_l) := lazy_ho_read l in
   kr_l = kr_e /\ held_out_rel e_r l_r) /\
  (held_out_rel e l ->
   let '(e_p, wake_e) := eager_ho_publish e extra in
   let '(l_p, wake_l) := lazy_ho_publish l extra in
   wake_l = wake_e /\ held_out_rel e_p l_p).
Proof.
  destruct e as [ec ep eo], l as [lc lp lo].
  split_and!.
  - unfold held_out_rel. simpl. done.
  - intros (Hc & Hp & _ & _). unfold eager_ho_read, lazy_ho_read, held_out_rel in *.
    simpl in *. split; [symmetry; exact Hc |].
    split_and!; [exact Hc | exact Hp | reflexivity | discriminate].
  - intros (Hc & Hp & Hobs & Hnone).
    unfold held_out_rel, eager_ho_publish, lazy_ho_publish in *. simpl in *.
    assert (Hstaged :
      default [] (match extra with [] => lp | _ => Some (default [] lp ++ extra) end) =
      ep ++ extra).
    { destruct extra; simpl; [rewrite Hp, app_nil_r; reflexivity | rewrite Hp; reflexivity]. }
    rewrite Hstaged.
    destruct (ep ++ extra) as [|x xs] eqn:Hst.
    + destruct lc as [[|y ys]|]; simpl in Hc, Hobs.
      * subst. rewrite bool_decide_eq_true_2 by reflexivity. simpl.
        split; [reflexivity|].
        split_and!; [reflexivity | reflexivity | reflexivity | discriminate].
      * subst. rewrite bool_decide_eq_false_2 by discriminate. simpl.
        split; [reflexivity|].
        split_and!; [reflexivity | reflexivity | reflexivity | discriminate].
      * subst. rewrite Hnone by reflexivity.
        rewrite bool_decide_eq_true_2 by reflexivity. simpl.
        split; [reflexivity|].
        split_and!; [reflexivity | reflexivity | reflexivity | reflexivity].
    + rewrite <- Hc.
      destruct lc as [c|]; simpl in Hobs.
      * subst. destruct (negb (bool_decide (c = x :: xs))); simpl;
          (split; [reflexivity | split_and!; [reflexivity | reflexivity | reflexivity | discriminate]]).
      * rewrite Hc, Hnone by reflexivity. rewrite Hobs.
        rewrite !andb_false_r.
        destruct (negb (bool_decide ([] = x :: xs))); simpl;
          (split; [reflexivity | split_and!; [reflexivity | reflexivity | reflexivity | discriminate]]).
Qed.

(** ** Pooled Lookup.Invalidate failed-and-affected union refinement *)

Record lookup_pool_state (K : Type) `{Countable K} := mk_lookup_pool {
  lps_busy : bool;
  lps_union : gset K;
  lps_keys : list K;
}.
Arguments mk_lookup_pool {K _ _} _ _ _.
Arguments lps_busy {K _ _} _.
Arguments lps_union {K _ _} _.
Arguments lps_keys {K _ _} _.

Definition lookup_pool_inv `{Countable K} (ps : lookup_pool_state K) : Prop :=
  lps_busy ps = false -> lps_union ps = ∅ /\ lps_keys ps = [].

Definition lookup_invalidate_step `{Countable K}
    (ps : lookup_pool_state K) (failed : gset K) (affected : list K)
    : lookup_pool_state K * list K :=
  if decide (failed = ∅) then
    (ps, affected)
  else if lps_busy ps then
    let u := failed ∪ list_to_set affected in
    (ps, elements u)
  else
    let u := (lps_union ps ∪ failed) ∪ list_to_set affected in
    let ks := lps_keys ps ++ elements u in
    ({| lps_busy := false; lps_union := ∅; lps_keys := [] |}, ks).

(**md
**Pooling `unionBuf` and `revisitBuf` across `Lookup.Invalidate` error-recovery passes (with a fresh-buffer fallback on reentrant entry) revisits the exact duplicate-free union `failed ∪ affected` and restores the empty-buffer invariant.**

Certifies the `Lookup.Invalidate` pooled buffer optimisation in `Projections.fs`:
1. Whenever `failed ≠ ∅` and `lookup_pool_inv ps` holds, both the non-reentrant pooled branch (`lps_busy = false`)
   and the reentrant fallback branch (`lps_busy = true`) produce a duplicate-free revisit list whose elements are
   precisely `failed ∪ list_to_set affected` — identical to allocating a fresh `KeySet` and `ResizeArray` per call.
2. Clearing `unionBuf`, `revisitBuf`, and `invalidating` in `finally` preserves `lookup_pool_inv` for subsequent transitions.

- [ps:] the pool state upon entering `Lookup.Invalidate`.
- [failed:] the non-empty set of currently failed or suspended lookup cells.
- [affected:] the sequence of keys affected by the upstream transition.
*)
Theorem lookup_invalidate_pooled_refines_union `{Countable K}
    (ps : lookup_pool_state K) (failed : gset K) (affected : list K) :
  lookup_pool_inv ps ->
  failed ≠ ∅ ->
  let '(ps', revisited) := lookup_invalidate_step ps failed affected in
  lookup_pool_inv ps' /\
  NoDup revisited /\
  (list_to_set revisited : gset K) = failed ∪ list_to_set affected.
Proof.
  intros Hinv Hne.
  unfold lookup_invalidate_step.
  rewrite decide_False by exact Hne.
  destruct (lps_busy ps) eqn:Hbusy.
  - cbv zeta. simpl.
    split; [exact Hinv | split; [apply NoDup_elements | set_solver]].
  - destruct (Hinv Hbusy) as [Hu Hk].
    cbv zeta. simpl. rewrite Hu, Hk. simpl.
    split; [unfold lookup_pool_inv; simpl; intro; split; reflexivity | split; [apply NoDup_elements | set_solver]].
Qed.

(** ** Single-key SortView binary-search reinsertion refinement *)

Fixpoint strictly_sorted {A : Type} (R : A -> A -> Prop) (l : list A) : Prop :=
  match l with
  | [] => True
  | x :: xs => Forall (R x) xs /\ strictly_sorted R xs
  end.

Lemma perm_middle_cons {A : Type} (x : A) (u v : list A) :
  u ++ x :: v ≡ₚ x :: (u ++ v).
Proof.
  induction u as [| y u' IH]; simpl.
  - reflexivity.
  - rewrite IH. apply Permutation_swap.
Qed.

Lemma strictly_sorted_perm_eq {A : Type} (R : A -> A -> Prop) :
  (forall x, ~ R x x) ->
  (forall x y z, R x y -> R y z -> R x z) ->
  forall l1 l2,
    strictly_sorted R l1 ->
    strictly_sorted R l2 ->
    l1 ≡ₚ l2 ->
    l1 = l2.
Proof.
  intros Hirr Htrans l1.
  induction l1 as [| x1 xs1 IH]; intros [| x2 xs2] Hss1 Hss2 Hp.
  - reflexivity.
  - pose proof (Permutation_length Hp) as Hlen. simpl in Hlen. lia.
  - pose proof (Permutation_length Hp) as Hlen. simpl in Hlen. lia.
  - destruct Hss1 as [Hall1 Hss1], Hss2 as [Hall2 Hss2].
    assert (Hx1 : x1 ∈ x2 :: xs2).
    { rewrite <- Hp. apply elem_of_cons. left. reflexivity. }
    apply elem_of_cons in Hx1 as [-> | Hx1_in].
    + f_equal. apply (IH xs2 Hss1 Hss2).
      exact (Permutation_cons_inv Hp).
    + exfalso.
      assert (Hx2 : x2 ∈ x1 :: xs1).
      { rewrite Hp. apply elem_of_cons. left. reflexivity. }
      apply elem_of_cons in Hx2 as [-> | Hx2_in].
      * rewrite Forall_forall in Hall2.
        exact (Hirr x1 (Hall2 x1 Hx1_in)).
      * rewrite Forall_forall in Hall1, Hall2.
        exact (Hirr x1 (Htrans x1 x2 x1 (Hall1 x2 Hx2_in) (Hall2 x1 Hx1_in))).
Qed.

Lemma strictly_sorted_reinsert {A : Type} (R : A -> A -> Prop)
    (x : A) (pre suf : list A) :
  strictly_sorted R (pre ++ suf) ->
  Forall (fun y => R y x) pre ->
  Forall (R x) suf ->
  strictly_sorted R (pre ++ x :: suf).
Proof.
  induction pre as [| y pre' IH]; intros Hrest Hpre Hsuf; simpl in *.
  - split; [exact Hsuf | exact Hrest].
  - destruct Hrest as [Hy_rest Hss_rest].
    apply Forall_cons_1 in Hpre as [Hy_x Hpre'].
    apply Forall_app in Hy_rest as [Hy_pre Hy_suf].
    split.
    + apply Forall_app. split; [exact Hy_pre | constructor; [exact Hy_x | exact Hy_suf]].
    + exact (IH Hss_rest Hpre' Hsuf).
Qed.

(**md
**Reinserting a single rank-changed key at its binary-search partition point produces the exact output array of a full lexicographic `(rank, upstreamIndex)` sort.**

Certifies the `SortView.updateOrder` single-key fast path in `Combinators.fs`:
1. When placed keys are unchanged and only `changedIdx` changed rank, removing `changedIdx` from `oldPos` leaves the
   `n - 1` unchanged indices `before ++ after` strictly ordered under `R(i, j) := compareRanks ranks[i] ranks[j] < 0 ∨ (compareRanks = 0 ∧ i < j)`.
2. Binary search partitions `before ++ after` into `pre ++ suf` where every index in `pre` precedes `changedIdx` under `R`
   and `changedIdx` precedes every index in `suf` under `R`.
3. Shifting `order` in-place to `pre ++ changedIdx :: suf` yields a strictly `R`-sorted permutation of all `n` indices,
   which by strict-order permutation uniqueness equals (`=`) the array produced by any full sort (`Array.sortInPlaceWith`).

- [R:] the strict irreflexive, transitive `(rank, upstreamIndex)` order on placed indices.
- [changedIdx:] the unique upstream position whose sort key changed on this pass.
- [before:] the slice of the previous `order` before `oldPos`.
- [after:] the slice of the previous `order` after `oldPos`.
- [pre:] the binary-search prefix of `before ++ after` preceding `(ranks[changedIdx], changedIdx)`.
- [suf:] the binary-search suffix of `before ++ after` following `(ranks[changedIdx], changedIdx)`.
- [full_sorted:] any full sort of `before ++ changedIdx :: after` under `R`.
*)
Theorem sortview_reinsert_refines_sort {A : Type} (R : A -> A -> Prop)
    (changedIdx : A) (before after pre suf full_sorted : list A) :
  (forall x, ~ R x x) ->
  (forall x y z, R x y -> R y z -> R x z) ->
  strictly_sorted R (before ++ after) ->
  pre ++ suf = before ++ after ->
  Forall (fun y => R y changedIdx) pre ->
  Forall (R changedIdx) suf ->
  strictly_sorted R full_sorted ->
  full_sorted ≡ₚ before ++ changedIdx :: after ->
  strictly_sorted R (pre ++ changedIdx :: suf) /\
  pre ++ changedIdx :: suf = full_sorted.
Proof.
  intros Hirr Htrans Hunchanged Hpart Hpre Hsuf Hfull_ss Hfull_perm.
  assert (Hre_ss : strictly_sorted R (pre ++ changedIdx :: suf)).
  { rewrite <- Hpart in Hunchanged.
    exact (strictly_sorted_reinsert R changedIdx pre suf Hunchanged Hpre Hsuf). }
  split; [exact Hre_ss |].
  apply (strictly_sorted_perm_eq R Hirr Htrans _ _ Hre_ss Hfull_ss).
  rewrite (perm_middle_cons changedIdx pre suf).
  rewrite Hpart.
  rewrite <- (perm_middle_cons changedIdx before after).
  symmetry. exact Hfull_perm.
Qed.

(** * Incremental Filter, Choose, and 1-Based Fenwick Primitives *)

Inductive col_view_freshness : Type := CVClean | CVCheck | CVDirty.

Record inclusion_view_state : Type := {
  ivs_fresh : col_view_freshness;
  ivs_runs : nat;
  ivs_values : list bool;
}.

Definition resolve_check_slots (old_vals new_vals : list bool) : bool :=
  bool_decide (new_vals = old_vals).

Definition ensure_current_step (st : inclusion_view_state) (new_vals : list bool) : inclusion_view_state :=
  match st.(ivs_fresh) with
  | CVClean => st
  | CVCheck =>
      if resolve_check_slots st.(ivs_values) new_vals then
        {| ivs_fresh := CVClean; ivs_runs := st.(ivs_runs); ivs_values := st.(ivs_values) |}
      else
        {| ivs_fresh := CVClean; ivs_runs := S st.(ivs_runs); ivs_values := new_vals |}
  | CVDirty =>
      {| ivs_fresh := CVClean; ivs_runs := S st.(ivs_runs); ivs_values := new_vals |}
  end.

(**md
**An unchanged `Check` pass resolves clean without advancing view runs.**

When every per-row `InclusionSlot` predicate or chooser re-evaluates to its
previously cached boolean inclusion flag, `ensure_current_step` resolves
`CVCheck` directly back to `CVClean` while leaving `ivs_runs` and
`ivs_values` unchanged.

- [st:] current view freshness state, run counter, and cached boolean flags.
- [new_vals:] re-evaluated per-row boolean flags on the current check pass.
*)
Theorem row_slot_check_cutoff_skips_pass
  (st : inclusion_view_state) (new_vals : list bool) :
  st.(ivs_fresh) = CVCheck ->
  new_vals = st.(ivs_values) ->
  (ensure_current_step st new_vals).(ivs_fresh) = CVClean /\
  (ensure_current_step st new_vals).(ivs_runs) = st.(ivs_runs) /\
  (ensure_current_step st new_vals).(ivs_values) = st.(ivs_values).
Proof.
  intros Hchk Heq. unfold ensure_current_step, resolve_check_slots.
  rewrite Hchk, Heq, bool_decide_eq_true_2; [|reflexivity].
  repeat split; reflexivity.
Qed.

Fixpoint count_true (bits : list bool) : nat :=
  match bits with
  | [] => 0
  | b :: rest => (if b then 1 else 0) + count_true rest
  end.

Definition prefix_count (bits : list bool) (k : nat) : nat :=
  count_true (take k bits).

Lemma count_true_app (l1 l2 : list bool) :
  count_true (l1 ++ l2) = count_true l1 + count_true l2.
Proof.
  induction l1 as [|b l1' IH]; cbn; [reflexivity|].
  rewrite IH. lia.
Qed.

Lemma prefix_count_lookup_true (bits : list bool) (idx k : nat) :
  bits !! idx = Some true ->
  idx < k ->
  1 <= prefix_count bits k.
Proof.
  revert idx k.
  induction bits as [|b rest IH]; intros idx k Hlu Hlt; [discriminate|].
  destruct k as [|k']; [lia|].
  unfold prefix_count in *. cbn [take].
  destruct idx as [|idx'].
  - cbn in Hlu. injection Hlu as ->. cbn [count_true]. lia.
  - cbn [count_true].
    pose proof (IH idx' k' Hlu ltac:(lia)). lia.
Qed.

(**md
**Point flipping a boolean slot updates every prefix count by exact +/-1.**

Flipping entry `idx` from `b_old` to `b_new` alters `prefix_count bits k`
only when `idx < k`, adding `1` if `b_new` is `true` and subtracting `1` if
`b_old` is `true`.

- [bits:] boolean inclusion mask indexed by upstream slot position.
- [idx:] slot index being updated.
- [k:] prefix bound at which the count is queried.
- [b_old:] prior inclusion flag at slot `idx`.
- [b_new:] new inclusion flag written at slot `idx`.
*)
Theorem prefix_count_point_update
  (bits : list bool) (idx k : nat) (b_old b_new : bool) :
  bits !! idx = Some b_old ->
  prefix_count (<[idx := b_new]> bits) k =
    (if decide (idx < k) then
       prefix_count bits k + (if b_new then 1 else 0) - (if b_old then 1 else 0)
     else prefix_count bits k).
Proof.
  revert idx k.
  induction bits as [|x rest IH]; intros idx k Hlu; [discriminate|].
  destruct k as [|k'].
  - destruct (decide (idx < 0)); [lia|]. reflexivity.
  - destruct idx as [|idx'].
    + cbn in Hlu. injection Hlu as ->.
      destruct (decide (0 < S k')); [|lia].
      unfold prefix_count. cbn.
      destruct b_old, b_new; lia.
    + cbn [lookup] in Hlu.
      unfold prefix_count.
      change (<[S idx' := b_new]> (x :: rest)) with (x :: <[idx' := b_new]> rest).
      cbn [take count_true].
      destruct (decide (S idx' < S k')) as [Hlt|Hge].
      * specialize (IH idx' k' Hlu).
        unfold prefix_count in IH.
        rewrite IH.
        destruct (decide (idx' < k')); [|lia].
        destruct b_old.
        -- pose proof (prefix_count_lookup_true rest idx' k' Hlu ltac:(lia)) as Hpos.
           unfold prefix_count in Hpos. lia.
        -- lia.
      * specialize (IH idx' k' Hlu).
        unfold prefix_count in IH.
        rewrite IH.
        destruct (decide (idx' < k')); [lia|].
        reflexivity.
Qed.

Fixpoint filter_scan {K : Type} (rows : list (K * bool)) : list K :=
  match rows with
  | [] => []
  | (k, true) :: rest => k :: filter_scan rest
  | (_, false) :: rest => filter_scan rest
  end.

Definition splice_insert_at {K : Type} (pos : nat) (k : K) (out : list K) : list K :=
  take pos out ++ k :: drop pos out.

Definition splice_remove_at {K : Type} (pos : nat) (out : list K) : list K :=
  take pos out ++ drop (S pos) out.

Definition filter_flip_at {K : Type} (rows : list (K * bool)) (idx : nat) (b_new : bool) (out : list K) : list K :=
  match rows !! idx with
  | None => out
  | Some (k, b_old) =>
      if Bool.eqb b_old b_new then out
      else
        let pos := prefix_count (snd <$> rows) idx in
        if b_new then splice_insert_at pos k out else splice_remove_at pos out
  end.

Lemma prefix_count_snd_length {K : Type} (rows : list (K * bool)) (idx : nat) :
  prefix_count (snd <$> rows) idx = length (filter_scan (take idx rows)).
Proof.
  revert idx.
  induction rows as [|[k b] rest IH]; intros [|idx']; unfold prefix_count in *;
    [reflexivity|reflexivity|reflexivity|].
  change (snd <$> (k, b) :: rest) with (b :: (snd <$> rest)).
  cbn [take filter_scan length count_true].
  destruct b; cbn [count_true filter_scan length]; specialize (IH idx'); rewrite IH; lia.
Qed.

Lemma filter_scan_app {K : Type} (l1 l2 : list (K * bool)) :
  filter_scan (l1 ++ l2) = filter_scan l1 ++ filter_scan l2.
Proof.
  induction l1 as [|[k [|]] rest IH]; cbn; [reflexivity| | ]; rewrite IH; reflexivity.
Qed.

Lemma take_prefix_filter {K : Type} (n : nat) (l : list (K * bool)) :
  take (length (filter_scan (take n l))) (filter_scan l) = filter_scan (take n l).
Proof.
  rewrite <- (take_drop n l) at 2.
  rewrite filter_scan_app, take_app_length. reflexivity.
Qed.

Lemma drop_prefix_filter {K : Type} (n : nat) (l : list (K * bool)) :
  drop (length (filter_scan (take n l))) (filter_scan l) = filter_scan (drop n l).
Proof.
  rewrite <- (take_drop n l) at 2.
  rewrite filter_scan_app, drop_app_length. reflexivity.
Qed.

Lemma drop_split_lookup {A : Type} (l : list A) (i : nat) (x : A) :
  l !! i = Some x -> drop i l = x :: drop (S i) l.
Proof.
  revert i. induction l as [|y rest IH]; intros [|i'] Hlu; cbn in *; [discriminate|discriminate| |].
  - injection Hlu as ->. reflexivity.
  - apply IH, Hlu.
Qed.

Lemma filter_flip_at_correct {K : Type} (rows : list (K * bool)) (idx : nat) (b_new : bool) :
  filter_flip_at rows idx b_new (filter_scan rows) =
  filter_scan
    (match rows !! idx with
     | None => rows
     | Some (k, _) => <[idx := (k, b_new)]> rows
     end).
Proof.
  unfold filter_flip_at.
  destruct (rows !! idx) as [[k b_old]|] eqn:Hlu; [|reflexivity].
  pose proof (drop_split_lookup rows idx (k, b_old) Hlu) as Hdrop.
  replace (<[idx := (k, b_new)]> rows) with (take idx rows ++ (k, b_new) :: drop (S idx) rows); last first.
  { clear Hdrop. revert rows Hlu. induction idx as [|i IH]; intros [|x xs] Hlu; cbn in *; try discriminate.
    - injection Hlu as _. reflexivity.
    - f_equal. apply IH, Hlu. }
  destruct (Bool.eqb b_old b_new) eqn:Heq.
  - destruct b_old, b_new; cbn in Heq; try discriminate;
      rewrite <- (take_drop idx rows) at 1;
      rewrite !filter_scan_app, Hdrop; cbn; reflexivity.
  - rewrite prefix_count_snd_length.
    destruct b_old, b_new; cbn in Heq; try discriminate; cbv zeta.
    + unfold splice_remove_at.
      rewrite take_prefix_filter.
      pose proof (drop_prefix_filter idx rows) as Hdr.
      rewrite Hdrop in Hdr.
      assert (Htail : drop (S (length (filter_scan (take idx rows)))) (filter_scan rows) =
                      filter_scan (drop (S idx) rows)).
      { replace (S (length (filter_scan (take idx rows))))
          with (length (filter_scan (take idx rows)) + 1) by lia.
        rewrite <- drop_drop. rewrite Hdr. reflexivity. }
      rewrite Htail, filter_scan_app. cbn. reflexivity.
    + unfold splice_insert_at.
      rewrite take_prefix_filter, drop_prefix_filter, Hdrop, filter_scan_app.
      cbn. reflexivity.
Qed.

Fixpoint apply_row_updates {K : Type} (rows : list (K * bool)) (flips : list (nat * bool)) : list (K * bool) :=
  match flips with
  | [] => rows
  | (idx, b_new) :: rest =>
      let rows' :=
        match rows !! idx with
        | None => rows
        | Some (k, _) => <[idx := (k, b_new)]> rows
        end in
      apply_row_updates rows' rest
  end.

Fixpoint apply_filter_flips {K : Type} (rows : list (K * bool)) (flips : list (nat * bool)) (out : list K) : list K :=
  match flips with
  | [] => out
  | (idx, b_new) :: rest =>
      let out' := filter_flip_at rows idx b_new out in
      let rows' :=
        match rows !! idx with
        | None => rows
        | Some (k, _) => <[idx := (k, b_new)]> rows
        end in
      apply_filter_flips rows' rest out'
  end.

(**md
**Splicing a batch of inclusion flips refines full `filter_scan`.**

Starting from `filter_scan rows`, applying each flip `(idx, b_new)` via
prefix-count splice insertion or removal produces the exact key list computed
by scanning the updated row table `apply_row_updates rows flips`.

- [K:] key type carried by upstream rows.
- [rows:] initial sequence of keyed inclusion flags in upstream order.
- [flips:] ordered batch of `(slot_index, new_flag)` dirty transitions.
*)
Theorem filter_incremental_step_refines_scan
  {K : Type} (rows : list (K * bool)) (flips : list (nat * bool)) :
  apply_filter_flips rows flips (filter_scan rows) =
  filter_scan (apply_row_updates rows flips).
Proof.
  revert rows.
  induction flips as [|[idx b_new] rest IH]; intros rows; [reflexivity|].
  cbn [apply_filter_flips apply_row_updates].
  rewrite filter_flip_at_correct.
  apply IH.
Qed.

Inductive incl_state : Type := ISExcluded | ISIncluded | ISFailed.
Inductive incl_kc : Type := IKCAdded | IKCRemoved | IKCChanged.

Definition incl_kc_merge_opt (prev : option incl_kc) (next : incl_kc) : option incl_kc :=
  match prev, next with
  | None, _ => Some next
  | Some IKCAdded, IKCRemoved => None
  | Some IKCAdded, _ => Some IKCAdded
  | Some IKCRemoved, IKCAdded => Some IKCChanged
  | Some IKCRemoved, _ => Some IKCRemoved
  | Some IKCChanged, IKCRemoved => Some IKCRemoved
  | Some IKCChanged, _ => Some IKCChanged
  end.

Definition incl_transition_delta (s1 s2 : incl_state) (val_changed : bool) : option incl_kc :=
  match s1, s2 with
  | ISExcluded, ISExcluded => None
  | ISExcluded, ISFailed => None
  | ISFailed, ISExcluded => None
  | ISFailed, ISFailed => None
  | ISExcluded, ISIncluded => Some IKCAdded
  | ISFailed, ISIncluded => Some IKCAdded
  | ISIncluded, ISExcluded => Some IKCRemoved
  | ISIncluded, ISFailed => Some IKCRemoved
  | ISIncluded, ISIncluded => if val_changed then Some IKCChanged else None
  end.

Definition incl_step_acc (acc : option incl_kc) (s1 s2 : incl_state) (vc : bool) : option incl_kc :=
  match incl_transition_delta s1 s2 vc with
  | None => acc
  | Some kc => incl_kc_merge_opt acc kc
  end.

(**md
**Hidden `Excluded` / `Failed` / `Included` transitions cancel or coalesce exactly.**

Over two sequential slot transitions `s0 -> s1 -> s2`, merging per-step
membership changes keeps `ISExcluded <-> ISFailed` silent, cancels
`ISExcluded -> ISIncluded -> ISFailed` back to `None`, and coalesces
`ISIncluded -> ISFailed -> ISIncluded` to `Some IKCChanged`.

- [s0:] initial inclusion state at pass start.
- [s1:] intermediate inclusion state after the first transition.
- [s2:] final inclusion state after the second transition.
*)
Theorem filter_hidden_transition_cancels_or_emits
  (s0 s1 s2 : incl_state) :
  incl_step_acc (incl_step_acc None s0 s1 false) s1 s2 false =
  incl_transition_delta s0 s2
    (match s0, s1, s2 with
     | ISIncluded, ISExcluded, ISIncluded
     | ISIncluded, ISFailed, ISIncluded => true
     | _, _, _ => false
     end).
Proof.
  destruct s0, s1, s2; reflexivity.
Qed.

Fixpoint pos_lsb (p : positive) : positive :=
  match p with | xH => xH | xI _ => xH | xO q => xO (pos_lsb q) end.

Lemma pos_lsb_le (p : positive) : (Pos.to_nat (pos_lsb p) <= Pos.to_nat p)%nat.
Proof. induction p as [q _|q IH|]; cbn; lia. Qed.

Definition nat_lsb (i : nat) : nat :=
  match i with | 0 => 0 | S _ => Pos.to_nat (pos_lsb (Pos.of_nat i)) end.

Lemma nat_lsb_bounds (i : nat) : (1 <= i -> 1 <= nat_lsb i /\ nat_lsb i <= i)%nat.
Proof.
  intro Hi. destruct i as [|i']; [lia|]. unfold nat_lsb. split.
  - pose proof (Pos2Nat.is_pos (pos_lsb (Pos.of_nat (S i')))). lia.
  - pose proof (pos_lsb_le (Pos.of_nat (S i'))) as Hle.
    rewrite Nat2Pos.id in Hle; [exact Hle | lia].
Qed.

Lemma z_land_opp_pos (p : positive) : Z.land (Z.pos p) (- Z.pos p) = Z.pos (pos_lsb p).
Proof.
  change (- Z.pos p)%Z with (Z.neg p).
  induction p as [q _|q IH|]; cbn [pos_lsb].
  - change (Z.land (Z.pos (q~1)) (Z.neg (q~1))) with (Z.of_N (Pos.ldiff (q~1) (q~0))).
    assert (Hdiag : Pos.ldiff q q = 0%N)
      by (apply N.bits_inj => n; rewrite N.pos_ldiff_spec; rewrite N.bits_0; by rewrite andb_negb_r).
    cbn. by rewrite Hdiag.
  - change (Z.land (Z.pos (q~0)) (Z.neg (q~0)))
      with (Z.of_N (N.ldiff (N.pos (q~0)) (Pos.pred_N (q~0)))).
    replace (Z.pos (pos_lsb q)~0) with (Z.of_N (N.double (N.pos (pos_lsb q)))) by reflexivity.
    assert (IH' : N.ldiff (N.pos q) (Pos.pred_N q) = N.pos (pos_lsb q))
      by (apply N2Z.inj; exact IH).
    rewrite <- IH'. f_equal. apply N.bits_inj => n.
    destruct n as [|p0] using N.peano_ind.
    + rewrite N.ldiff_spec; rewrite N.double_spec; rewrite N.testbit_even_0. reflexivity.
    + clear IHp0.
      rewrite N.ldiff_spec; rewrite N.double_spec; rewrite N.double_bits_succ; rewrite N.ldiff_spec.
      rewrite <- N.div2_bits. rewrite <- N.div2_bits.
      rewrite <- N.div2_div. rewrite <- N.div2_div.
      cbn; destruct q; reflexivity.
  - reflexivity.
Qed.

Lemma z_land_opp_nat (i : nat) :
  (1 <= i)%nat -> Z.land (Z.of_nat i) (- Z.of_nat i)%Z = Z.of_nat (nat_lsb i).
Proof.
  intro Hi. destruct i as [|i']; [lia|]. unfold nat_lsb.
  replace (Z.of_nat (S i')) with (Z.pos (Pos.of_nat (S i'))) by lia.
  rewrite z_land_opp_pos. lia.
Qed.

Fixpoint fenwick_sum_go (fuel : nat) (tree : list Z) (i : nat) (acc : Z) : Z :=
  match fuel with
  | 0 => acc
  | S fuel' =>
      if decide (1 <= i)%nat then
        fenwick_sum_go fuel' tree (i - nat_lsb i) (acc + default 0%Z (tree !! i))%Z
      else acc
  end.

Definition fenwick_sum_pure (tree : list Z) (i : nat) : Z :=
  fenwick_sum_go i tree i 0%Z.

Lemma fenwick_sum_go_fuel_irrel (f1 f2 : nat) (tree : list Z) (i : nat) (acc : Z) :
  (i <= f1)%nat -> (i <= f2)%nat ->
  fenwick_sum_go f1 tree i acc = fenwick_sum_go f2 tree i acc.
Proof.
  revert f2 i acc. induction f1 as [|f1' IH]; intros [|f2'] i acc Hf1 Hf2.
  - reflexivity.
  - assert (i = 0)%nat as -> by lia. reflexivity.
  - assert (i = 0)%nat as -> by lia. reflexivity.
  - cbn [fenwick_sum_go]. destruct (decide (1 <= i)%nat) as [Hi|_]; [|reflexivity].
    destruct (nat_lsb_bounds i Hi). apply IH; lia.
Qed.

Fixpoint fenwick_add_go (fuel : nat) (tree : list Z) (n i : nat) (delta : Z) : list Z :=
  match fuel with
  | 0 => tree
  | S fuel' =>
      if decide (1 <= i /\ i <= n)%nat then
        fenwick_add_go fuel' (<[i := (default 0%Z (tree !! i) + delta)%Z]> tree) n (i + nat_lsb i) delta
      else tree
  end.

Definition fenwick_add_pure (tree : list Z) (n i : nat) (delta : Z) : list Z :=
  fenwick_add_go (S n - i) tree n i delta.

Lemma fenwick_add_go_fuel_irrel (f1 f2 : nat) (tree : list Z) (n i : nat) (delta : Z) :
  (1 <= i)%nat -> (S n - i <= f1)%nat -> (S n - i <= f2)%nat ->
  fenwick_add_go f1 tree n i delta = fenwick_add_go f2 tree n i delta.
Proof.
  revert f2 tree i. induction f1 as [|f1' IH]; intros [|f2'] tree i Hi Hf1 Hf2.
  - reflexivity.
  - cbn [fenwick_add_go]. destruct (decide (1 <= i /\ i <= n)%nat); [lia|reflexivity].
  - cbn [fenwick_add_go]. destruct (decide (1 <= i /\ i <= n)%nat); [lia|reflexivity].
  - cbn [fenwick_add_go]. destruct (decide (1 <= i /\ i <= n)%nat) as [[_ Hle]|_]; [|reflexivity].
    destruct (nat_lsb_bounds i Hi). apply IH; lia.
Qed.

(** ** 7. Incremental `SortView` inverse-order permutation and splice refinement *)

(**md
Inverse-permutation coherence between `SortView`'s `order` array (output position
-> placed index) and `orderPos` array (placed index -> output position).
*)
Definition inv_order (order order_pos : list nat) : Prop :=
  length order = length order_pos /\
  (forall pos p, order !! pos = Some p -> order_pos !! p = Some pos) /\
  (forall p pos, order_pos !! p = Some pos -> order !! pos = Some p).

Definition inv_order_hole (hole free_p : nat) (order order_pos : list nat) : Prop :=
  length order = length order_pos /\
  (hole < length order)%nat /\
  (free_p < length order_pos)%nat /\
  (forall pos p, pos <> hole -> order !! pos = Some p -> p <> free_p /\ order_pos !! p = Some pos) /\
  (forall p pos, p <> free_p -> order_pos !! p = Some pos -> pos <> hole /\ order !! pos = Some p).

Fixpoint shift_right_steps (steps k : nat) (order order_pos : list nat) : list nat * list nat :=
  match steps with
  | 0 => (order, order_pos)
  | S steps' =>
      match order !! (k - 1)%nat with
      | Some moved =>
          shift_right_steps steps' (k - 1)%nat (<[k := moved]> order) (<[moved := k]> order_pos)
      | None => (order, order_pos)
      end
  end.

Definition reinsert_left_pure (old_pos new_pos p_idx : nat) (order order_pos : list nat) : list nat * list nat :=
  let '(order1, order_pos1) := shift_right_steps (old_pos - new_pos)%nat old_pos order order_pos in
  (<[new_pos := p_idx]> order1, <[p_idx := new_pos]> order_pos1).

Lemma inv_order_to_hole (order order_pos : list nat) (old_pos p_idx : nat) :
  inv_order order order_pos ->
  order !! old_pos = Some p_idx ->
  inv_order_hole old_pos p_idx order order_pos.
Proof.
  intros [Hlen [Hfw Hbw]] Hold.
  assert (Hold_lt : (old_pos < length order)%nat) by (eapply lookup_lt_Some; exact Hold).
  assert (Hp_ok : order_pos !! p_idx = Some old_pos) by (apply Hfw; exact Hold).
  assert (Hp_lt : (p_idx < length order_pos)%nat) by (eapply lookup_lt_Some; exact Hp_ok).
  split_and!; [exact Hlen | exact Hold_lt | exact Hp_lt | |].
  - intros pos p Hne Hpos. pose proof (Hfw pos p Hpos) as Hpos'. split; [|exact Hpos'].
    intro Heq. subst p. rewrite Hp_ok in Hpos'. congruence.
  - intros p pos Hne Hp. pose proof (Hbw p pos Hp) as Hpos'. split; [|exact Hpos'].
    intro Heq. subst pos. rewrite Hold in Hpos'. congruence.
Qed.

Lemma inv_order_hole_step (k free_p moved : nat) (order order_pos : list nat) :
  (1 <= k)%nat ->
  inv_order_hole k free_p order order_pos ->
  order !! (k - 1)%nat = Some moved ->
  inv_order_hole (k - 1)%nat free_p (<[k := moved]> order) (<[moved := k]> order_pos).
Proof.
  intros Hk (Hlen & Hk_lt & Hfree_lt & Hfw & Hbw) Hprev.
  assert (Hmoved : moved <> free_p /\ order_pos !! moved = Some (k - 1)%nat).
  { apply Hfw; [lia | exact Hprev]. }
  destruct Hmoved as [Hmoved_ne Hmoved_pos].
  assert (Hmoved_lt : (moved < length order_pos)%nat) by (eapply lookup_lt_Some; exact Hmoved_pos).
  split_and!.
  - rewrite !length_insert. exact Hlen.
  - rewrite length_insert. lia.
  - rewrite length_insert. exact Hfree_lt.
  - intros pos p Hne Hpos.
    destruct (decide (pos = k)) as [->|Hnek].
    + rewrite list_lookup_insert_eq in Hpos; [|exact Hk_lt].
      injection Hpos as <-. split; [exact Hmoved_ne|].
      rewrite list_lookup_insert_eq; [reflexivity | exact Hmoved_lt].
    + rewrite list_lookup_insert_ne in Hpos; [|lia].
      destruct (Hfw pos p Hnek Hpos) as [Hp_ne Hpos_old].
      split; [exact Hp_ne|].
      assert (Hp_ne_moved : p <> moved).
      { intro Heq. subst p. rewrite Hmoved_pos in Hpos_old. apply Hne. congruence. }
      rewrite list_lookup_insert_ne; [exact Hpos_old | lia].
  - intros p pos Hne Hp.
    destruct (decide (p = moved)) as [->|Hnem].
    + rewrite list_lookup_insert_eq in Hp; [|exact Hmoved_lt].
      injection Hp as <-. split; [lia|].
      rewrite list_lookup_insert_eq; [reflexivity | exact Hk_lt].
    + rewrite list_lookup_insert_ne in Hp; [|lia].
      destruct (Hbw p pos Hne Hp) as [Hpos_nek Hpos_old].
      split.
      * intro Heq. subst pos. rewrite Hprev in Hpos_old. apply Hnem. congruence.
      * rewrite list_lookup_insert_ne; [exact Hpos_old | lia].
Qed.

Lemma shift_right_steps_hole (steps k free_p : nat) (order order_pos : list nat) :
  (steps <= k)%nat ->
  inv_order_hole k free_p order order_pos ->
  let '(order', order_pos') := shift_right_steps steps k order order_pos in
  inv_order_hole (k - steps)%nat free_p order' order_pos'.
Proof.
  revert k order order_pos.
  induction steps as [|steps' IH]; intros k order order_pos Hle Hinv.
  - cbn [shift_right_steps]. replace (k - 0)%nat with k by lia. exact Hinv.
  - cbn [shift_right_steps].
    destruct Hinv as (Hlen & Hk_lt & Hfree_lt & Hfw & Hbw).
    assert (Hkm1_lt : (k - 1 < length order)%nat) by lia.
    destruct (lookup_lt_is_Some_2 order (k - 1)%nat Hkm1_lt) as [moved Hprev].
    rewrite Hprev.
    pose proof (inv_order_hole_step k free_p moved order order_pos ltac:(lia) (conj Hlen (conj Hk_lt (conj Hfree_lt (conj Hfw Hbw)))) Hprev) as Hstep.
    replace (k - S steps')%nat with ((k - 1) - steps')%nat by lia.
    apply IH; [lia | exact Hstep].
Qed.

Lemma inv_order_close_hole (hole free_p : nat) (order order_pos : list nat) :
  inv_order_hole hole free_p order order_pos ->
  inv_order (<[hole := free_p]> order) (<[free_p := hole]> order_pos) /\
  (<[hole := free_p]> order) !! hole = Some free_p.
Proof.
  intros (Hlen & Hhole_lt & Hfree_lt & Hfw & Hbw).
  split.
  - split_and!.
    + rewrite !length_insert. exact Hlen.
    + intros pos p Hpos.
      destruct (decide (pos = hole)) as [->|Hne].
      * rewrite list_lookup_insert_eq in Hpos; [|exact Hhole_lt].
        injection Hpos as <-.
        rewrite list_lookup_insert_eq; [reflexivity | exact Hfree_lt].
      * rewrite list_lookup_insert_ne in Hpos; [|lia].
        destruct (Hfw pos p Hne Hpos) as [Hp_ne Hpos_old].
        rewrite list_lookup_insert_ne; [exact Hpos_old | lia].
    + intros p pos Hpos.
      destruct (decide (p = free_p)) as [->|Hne].
      * rewrite list_lookup_insert_eq in Hpos; [|exact Hfree_lt].
        injection Hpos as <-.
        rewrite list_lookup_insert_eq; [reflexivity | exact Hhole_lt].
      * rewrite list_lookup_insert_ne in Hpos; [|lia].
        destruct (Hbw p pos Hne Hpos) as [Hpos_ne Hpos_old].
        rewrite list_lookup_insert_ne; [exact Hpos_old | lia].
  - rewrite list_lookup_insert_eq; [reflexivity | exact Hhole_lt].
Qed.

(**md
**Leftward reinsertion preserves inverse-order array coherence (`inv_order`).**

Mirrors `SortView.reinsertPlacedSlot`'s `newPos < oldPos` loop: shifting
`order[old_pos - 1 .. new_pos]` right by one slot while updating `orderPos`
and placing `p_idx` at `new_pos` preserves the bijection `order <-> orderPos`
and installs `order' !! new_pos = Some p_idx`.

- [order:] output-position to placed-index permutation before reinsertion.
- [order_pos:] inverse placed-index to output-position map before reinsertion.
- [old_pos:] prior output position holding `p_idx`.
- [new_pos:] target output position (`new_pos <= old_pos`).
- [p_idx:] placed-slot index moved from `old_pos` to `new_pos`.
*)
Theorem inv_order_reinsert_coherent
  (order order_pos : list nat) (old_pos new_pos p_idx : nat) :
  inv_order order order_pos ->
  (new_pos <= old_pos)%nat ->
  order !! old_pos = Some p_idx ->
  let '(order', order_pos') := reinsert_left_pure old_pos new_pos p_idx order order_pos in
  inv_order order' order_pos' /\ order' !! new_pos = Some p_idx.
Proof.
  intros Hinv Hle Hold.
  unfold reinsert_left_pure.
  pose proof (shift_right_steps_hole (old_pos - new_pos)%nat old_pos p_idx order order_pos ltac:(lia) (inv_order_to_hole order order_pos old_pos p_idx Hinv Hold)) as Hshift.
  destruct (shift_right_steps (old_pos - new_pos)%nat old_pos order order_pos) as [order1 order_pos1].
  replace (old_pos - (old_pos - new_pos))%nat with new_pos in Hshift by lia.
  exact (inv_order_close_hole new_pos p_idx order1 order_pos1 Hshift).
Qed.

Definition rank_up_le (a b : nat * Z * nat) : Prop :=
  (a.1.2 < b.1.2)%Z \/ (a.1.2 = b.1.2 /\ (a.2 <= b.2)%nat).

Definition rank_up_le_b (a b : nat * Z * nat) : bool :=
  bool_decide ((a.1.2 < b.1.2)%Z \/ (a.1.2 = b.1.2 /\ (a.2 <= b.2)%nat)).

Lemma rank_up_le_b_spec (a b : nat * Z * nat) :
  if rank_up_le_b a b then rank_up_le a b else rank_up_le b a.
Proof.
  unfold rank_up_le_b, rank_up_le.
  destruct (bool_decide_reflect ((a.1.2 < b.1.2)%Z \/ (a.1.2 = b.1.2 /\ (a.2 <= b.2)%nat))); lia.
Qed.

Lemma rank_up_le_refl (a : nat * Z * nat) : rank_up_le a a.
Proof. unfold rank_up_le. lia. Qed.

Lemma rank_up_le_trans (a b c : nat * Z * nat) :
  rank_up_le a b -> rank_up_le b c -> rank_up_le a c.
Proof. unfold rank_up_le. lia. Qed.

Fixpoint sorted_insert (x : nat * Z * nat) (l : list (nat * Z * nat)) : list (nat * Z * nat) :=
  match l with
  | [] => [x]
  | y :: ys => if rank_up_le_b x y then x :: y :: ys else y :: sorted_insert x ys
  end.

Definition full_insertion_sort (xs : list (nat * Z * nat)) : list (nat * Z * nat) :=
  foldr sorted_insert [] xs.

Fixpoint sorted_remove_key (k : nat) (l : list (nat * Z * nat)) : list (nat * Z * nat) :=
  match l with
  | [] => []
  | y :: ys =>
      if decide (y.1.1 <> k) then y :: sorted_remove_key k ys
      else sorted_remove_key k ys
  end.

Lemma sorted_insert_perm (x : nat * Z * nat) (l : list (nat * Z * nat)) :
  sorted_insert x l ≡ₚ x :: l.
Proof.
  induction l as [|y ys IH]; cbn [sorted_insert].
  - reflexivity.
  - destruct (rank_up_le_b x y).
    + reflexivity.
    + rewrite IH. apply perm_swap.
Qed.

Lemma full_insertion_sort_perm (xs : list (nat * Z * nat)) :
  full_insertion_sort xs ≡ₚ xs.
Proof.
  induction xs as [|x xs IH]; cbn [full_insertion_sort foldr].
  - reflexivity.
  - rewrite sorted_insert_perm, IH. reflexivity.
Qed.

Lemma sorted_insert_sorts (x : nat * Z * nat) (l : list (nat * Z * nat)) :
  StronglySorted rank_up_le l ->
  StronglySorted rank_up_le (sorted_insert x l).
Proof.
  induction l as [|y ys IH]; intro Hsort; cbn [sorted_insert].
  - constructor; [constructor | constructor].
  - inversion Hsort as [|? ? Hsys Hfy]; subst.
    pose proof (rank_up_le_b_spec x y) as Hcmp.
    destruct (rank_up_le_b x y).
    + constructor; [exact Hsort|].
      constructor; [exact Hcmp|].
      eapply Forall_impl; [exact Hfy|].
      intros z Hyz. eapply rank_up_le_trans; [exact Hcmp | exact Hyz].
    + constructor; [apply IH; exact Hsys|].
      rewrite Forall_forall. intros z Hz.
      assert (Hz_in : z ∈ x :: ys).
      { rewrite <- (sorted_insert_perm x ys). exact Hz. }
      apply elem_of_cons in Hz_in as [-> | Hys].
      * exact Hcmp.
      * rewrite Forall_forall in Hfy. apply Hfy; exact Hys.
Qed.

Lemma full_insertion_sort_sorts (xs : list (nat * Z * nat)) :
  StronglySorted rank_up_le (full_insertion_sort xs).
Proof.
  induction xs as [|x xs IH]; cbn [full_insertion_sort foldr].
  - constructor.
  - apply sorted_insert_sorts; exact IH.
Qed.

Lemma sorted_remove_key_eq_filter (k : nat) (l : list (nat * Z * nat)) :
  sorted_remove_key k l = filter (fun y : nat * Z * nat => y.1.1 <> k) l.
Proof.
  induction l as [|y ys IH]; cbn [sorted_remove_key].
  - rewrite filter_nil. reflexivity.
  - destruct (decide (y.1.1 <> k)) as [Hy|Hy].
    + rewrite filter_cons_True by exact Hy. rewrite IH. reflexivity.
    + rewrite filter_cons_False by exact Hy. exact IH.
Qed.

Lemma filter_elem_of {A : Type} (P : A -> Prop) `{!forall x, Decision (P x)}
  (l : list A) (z : A) :
  z ∈ filter P l -> P z /\ z ∈ l.
Proof.
  induction l as [|y ys IH]; intro Hz.
  - rewrite filter_nil in Hz. inversion Hz.
  - destruct (decide (P y)) as [HPy|HPy].
    + rewrite filter_cons_True in Hz by exact HPy.
      apply elem_of_cons in Hz as [-> | Hz].
      * split; [exact HPy | left].
      * destruct (IH Hz) as [HPz Hz']. split; [exact HPz | right; exact Hz'].
    + rewrite filter_cons_False in Hz by exact HPy.
      destruct (IH Hz) as [HPz Hz']. split; [exact HPz | right; exact Hz'].
Qed.

Lemma strongly_sorted_filter (P : nat * Z * nat -> Prop) `{!forall y, Decision (P y)}
  (l : list (nat * Z * nat)) :
  StronglySorted rank_up_le l ->
  StronglySorted rank_up_le (filter P l).
Proof.
  induction l as [|y ys IH]; intro Hsort.
  - rewrite filter_nil. constructor.
  - inversion Hsort as [|? ? Hsys Hfy]; subst.
    destruct (decide (P y)) as [HPy|HPy].
    + rewrite filter_cons_True by exact HPy.
      constructor; [apply IH; exact Hsys|].
      rewrite Forall_forall in Hfy |- *.
      intros z Hz. apply filter_elem_of in Hz as [_ Hz].
      apply Hfy; exact Hz.
    + rewrite filter_cons_False by exact HPy.
      apply IH; exact Hsys.
Qed.

Lemma list_filter_perm {A : Type} (P : A -> Prop) `{!forall x, Decision (P x)}
  (l1 l2 : list A) :
  l1 ≡ₚ l2 -> filter P l1 ≡ₚ filter P l2.
Proof.
  intro Hp. induction Hp as [|x l1 l2 Hsub IH|x y l|l1 l2 l3 Hsub1 IH1 Hsub2 IH2].
  - rewrite filter_nil. reflexivity.
  - destruct (decide (P x)) as [HPx|HPx].
    + rewrite !filter_cons_True by exact HPx. constructor. exact IH.
    + rewrite !filter_cons_False by exact HPx. exact IH.
  - destruct (decide (P x)) as [HPx|HPx], (decide (P y)) as [HPy|HPy];
      repeat first [ rewrite filter_cons_True by done | rewrite filter_cons_False by done ];
      try apply perm_swap; reflexivity.
  - etrans; [exact IH1 | exact IH2].
Qed.

Lemma list_fmap_perm {A B : Type} (f : A -> B) (l1 l2 : list A) :
  l1 ≡ₚ l2 -> (f <$> l1) ≡ₚ (f <$> l2).
Proof.
  intro Hp. induction Hp as [|x l1 l2 Hsub IH|x y l|l1 l2 l3 Hsub1 IH1 Hsub2 IH2];
    cbn [fmap list_fmap].
  - reflexivity.
  - constructor. exact IH.
  - apply perm_swap.
  - etrans; [exact IH1 | exact IH2].
Qed.

Lemma nodup_snd_filter (P : nat * Z * nat -> Prop) `{!forall y, Decision (P y)}
  (xs : list (nat * Z * nat)) :
  NoDup (snd <$> xs) ->
  NoDup (snd <$> filter P xs).
Proof.
  induction xs as [|y ys IH]; intro Hnd.
  - rewrite filter_nil. constructor.
  - cbn [fmap list_fmap] in Hnd. inversion Hnd as [|? ? Hnot Hnd']; subst.
    destruct (decide (P y)) as [HPy|HPy].
    + rewrite filter_cons_True by exact HPy.
      cbn [fmap list_fmap]. constructor.
      * intro Hin. apply Hnot.
        apply list_elem_of_fmap in Hin as [z [Heq Hz]].
        apply filter_elem_of in Hz as [_ Hz].
        subst. apply list_elem_of_fmap. eauto.
      * apply IH; exact Hnd'.
    + rewrite filter_cons_False by exact HPy.
      apply IH; exact Hnd'.
Qed.

Lemma strongly_sorted_nodup_unique (l1 l2 : list (nat * Z * nat)) :
  StronglySorted rank_up_le l1 ->
  StronglySorted rank_up_le l2 ->
  l1 ≡ₚ l2 ->
  NoDup (snd <$> l1) ->
  l1 = l2.
Proof.
  revert l2. induction l1 as [|a l1' IH]; intros l2 Hs1 Hs2 Hperm Hnd1.
  - symmetry. apply Permutation_nil. exact Hperm.
  - destruct l2 as [|b l2']; [exfalso; eapply Permutation_nil_cons; symmetry; exact Hperm|].
    inversion Hs1 as [|? ? Hs1' Hfa]; subst.
    inversion Hs2 as [|? ? Hs2' Hfb]; subst.
    cbn [fmap list_fmap] in Hnd1. inversion Hnd1 as [|? ? Ha_not Hnd1']; subst.
    assert (Hb_in : b ∈ a :: l1') by (rewrite Hperm; constructor).
    assert (Ha_in : a ∈ b :: l2') by (rewrite <- Hperm; constructor).
    assert (Hab : rank_up_le a b).
    { apply elem_of_cons in Hb_in as [-> | Hb_in].
      - apply rank_up_le_refl.
      - rewrite Forall_forall in Hfa. apply Hfa; exact Hb_in. }
    assert (Hba : rank_up_le b a).
    { apply elem_of_cons in Ha_in as [-> | Ha_in].
      - apply rank_up_le_refl.
      - rewrite Forall_forall in Hfb. apply Hfb; exact Ha_in. }
    assert (Heq_rk : a.1.2 = b.1.2 /\ a.2 = b.2) by (unfold rank_up_le in Hab, Hba; lia).
    destruct Heq_rk as [_ Heq_snd].
    apply elem_of_cons in Hb_in as [Heq | Hb_l1'].
    + subst b. f_equal. apply IH; [exact Hs1' | exact Hs2' | | exact Hnd1'].
      eapply Permutation_cons_inv; exact Hperm.
    + exfalso. apply Ha_not. rewrite Heq_snd.
      apply list_elem_of_fmap. exists b. split; [reflexivity | exact Hb_l1'].
Qed.

(**md
**Incremental single-slot insertion and key removal refine full upstream-tie-broken sorting.**

Under distinct upstream indices (`NoDup (snd <$> ...)`), `sorted_insert` on
the already-sorted list equals `full_insertion_sort` of `(x :: xs)`, and
`sorted_remove_key k` equals `full_insertion_sort` over the filtered list
with key `k` removed.

- [x:] newly placed or reinserted `(key, rank, up_idx)` triple.
- [k:] removed upstream key.
- [xs:] sorted upstream triple collection prior to the incremental edit.
*)
Theorem sortview_insert_remove_refines_sort
  (x : nat * Z * nat) (k : nat) (xs : list (nat * Z * nat)) :
  (NoDup (snd <$> (x :: xs)) ->
   sorted_insert x (full_insertion_sort xs) = full_insertion_sort (x :: xs)) /\
  (NoDup (snd <$> xs) ->
   sorted_remove_key k (full_insertion_sort xs) =
     full_insertion_sort (filter (fun y : nat * Z * nat => y.1.1 <> k) xs)).
Proof.
  split.
  - intros _. reflexivity.
  - intro Hnd. rewrite sorted_remove_key_eq_filter.
    set (P := fun y : nat * Z * nat => y.1.1 <> k).
    apply strongly_sorted_nodup_unique.
    + apply strongly_sorted_filter. apply full_insertion_sort_sorts.
    + apply full_insertion_sort_sorts.
    + rewrite (list_filter_perm P _ _ (full_insertion_sort_perm xs)).
      symmetry. apply full_insertion_sort_perm.
    + rewrite (list_fmap_perm snd _ _ (list_filter_perm P _ _ (full_insertion_sort_perm xs))).
      exact (nodup_snd_filter P xs Hnd).
Qed.

(** ** Incremental `DistinctView` and `Grouping` bucket refinement *)

Definition row_up (r : nat * nat * option Z) : nat := r.1.1.
Definition row_key (r : nat * nat * option Z) : nat := r.1.2.
Definition row_grp (r : nat * nat * option Z) : option Z := r.2.

Fixpoint bucket_members (g : Z) (xs : list (nat * nat * option Z)) : list (nat * nat) :=
  match xs with
  | [] => []
  | r :: xs' =>
      if decide (row_grp r = Some g) then
        (row_up r, row_key r) :: bucket_members g xs'
      else
        bucket_members g xs'
  end.

Fixpoint bucket_remove (u0 : nat) (ms : list (nat * nat)) : list (nat * nat) :=
  match ms with
  | [] => []
  | m :: ms' =>
      if decide (m.1 = u0) then bucket_remove u0 ms'
      else m :: bucket_remove u0 ms'
  end.

Fixpoint bucket_insert (m : nat * nat) (ms : list (nat * nat)) : list (nat * nat) :=
  match ms with
  | [] => [m]
  | m' :: ms' =>
      if decide (m.1 < m'.1)%nat then m :: m' :: ms'
      else m' :: bucket_insert m ms'
  end.

Fixpoint update_row_group (u0 : nat) (mg' : option Z) (xs : list (nat * nat * option Z))
  : list (nat * nat * option Z) :=
  match xs with
  | [] => []
  | r :: xs' =>
      if decide (row_up r = u0) then
        (row_up r, row_key r, mg') :: update_row_group u0 mg' xs'
      else
        r :: update_row_group u0 mg' xs'
  end.

Definition bucket_map_apply_old
  (u0 : nat) (mg_old : option Z) (bmap : Z -> list (nat * nat)) (g : Z) : list (nat * nat) :=
  match mg_old with
  | Some g_old => if decide (g = g_old) then bucket_remove u0 (bmap g) else bmap g
  | None => bmap g
  end.

Definition bucket_map_update
  (u0 k0 : nat) (mg_old mg_new : option Z) (bmap : Z -> list (nat * nat)) (g : Z) : list (nat * nat) :=
  let bmap1 := bucket_map_apply_old u0 mg_old bmap in
  match mg_new with
  | Some g_new => if decide (g = g_new) then bucket_insert (u0, k0) (bmap1 g) else bmap1 g
  | None => bmap1 g
  end.

Fixpoint distinct_scan_aux (seen : list Z) (xs : list (nat * nat * option Z)) : list nat :=
  match xs with
  | [] => []
  | r :: xs' =>
      match row_grp r with
      | None => distinct_scan_aux seen xs'
      | Some g =>
          if decide (g ∈ seen) then distinct_scan_aux seen xs'
          else row_key r :: distinct_scan_aux (g :: seen) xs'
      end
  end.

Definition distinct_scan (xs : list (nat * nat * option Z)) : list nat :=
  distinct_scan_aux [] xs.

Definition is_bucket_winner (bmap : Z -> list (nat * nat)) (r : nat * nat * option Z) : bool :=
  match row_grp r with
  | None => false
  | Some g =>
      match bmap g with
      | [] => false
      | m :: _ => bool_decide (m.1 = row_up r /\ m.2 = row_key r)
      end
  end.

Fixpoint distinct_by_buckets (bmap : Z -> list (nat * nat)) (xs : list (nat * nat * option Z)) : list nat :=
  match xs with
  | [] => []
  | r :: xs' =>
      if is_bucket_winner bmap r then row_key r :: distinct_by_buckets bmap xs'
      else distinct_by_buckets bmap xs'
  end.

Fixpoint group_scan_order_aux (seen : list Z) (xs : list (nat * nat * option Z)) : list Z :=
  match xs with
  | [] => []
  | r :: xs' =>
      match row_grp r with
      | None => group_scan_order_aux seen xs'
      | Some g =>
          if decide (g ∈ seen) then group_scan_order_aux seen xs'
          else g :: group_scan_order_aux (g :: seen) xs'
      end
  end.

Definition group_scan_order (xs : list (nat * nat * option Z)) : list Z :=
  group_scan_order_aux [] xs.

Definition group_scan_members (g : Z) (xs : list (nat * nat * option Z)) : list nat :=
  snd <$> bucket_members g xs.

Fixpoint insert_first_idx (p : nat * Z) (l : list (nat * Z)) : list (nat * Z) :=
  match l with
  | [] => [p]
  | q :: l' =>
      if decide (p.1 < q.1)%nat then p :: q :: l'
      else q :: insert_first_idx p l'
  end.

Definition active_group_pairs (bmap : Z -> list (nat * nat)) (gs : list Z) : list (nat * Z) :=
  foldr (fun g acc =>
    match bmap g with
    | [] => acc
    | m :: _ => insert_first_idx (m.1, g) acc
    end) [] gs.

Definition ordered_groups_by_first_idx (bmap : Z -> list (nat * nat)) (gs : list Z) : list Z :=
  snd <$> active_group_pairs bmap gs.

Lemma row_up_update_row_group (u0 : nat) (mg' : option Z) (xs : list (nat * nat * option Z)) :
  row_up <$> update_row_group u0 mg' xs = row_up <$> xs.
Proof.
  induction xs as [|r xs' IH]; cbn [update_row_group fmap list_fmap].
  - reflexivity.
  - destruct (decide (row_up r = u0)); cbn [fmap list_fmap row_up].
    + destruct r as [[u k] mg]; cbn in *. rewrite IH. reflexivity.
    + rewrite IH. reflexivity.
Qed.

Lemma strongly_sorted_row_up_update (u0 : nat) (mg' : option Z) (xs : list (nat * nat * option Z)) :
  StronglySorted Nat.lt (row_up <$> xs) ->
  StronglySorted Nat.lt (row_up <$> update_row_group u0 mg' xs).
Proof.
  rewrite row_up_update_row_group. trivial.
Qed.

Lemma strongly_sorted_tail_lt (r : nat * nat * option Z) (xs : list (nat * nat * option Z)) :
  StronglySorted Nat.lt (row_up <$> (r :: xs)) ->
  StronglySorted Nat.lt (row_up <$> xs) /\ Forall (fun q => (row_up r < row_up q)%nat) xs.
Proof.
  intro Hsort. cbn [fmap list_fmap] in Hsort.
  inversion Hsort as [|? ? Hxs Hfa]; subst.
  split; [exact Hxs|].
  rewrite Forall_forall. intros q Hq.
  rewrite Forall_forall in Hfa.
  apply Hfa. apply list_elem_of_fmap. exists q. split; [reflexivity | exact Hq].
Qed.

Lemma bucket_remove_members (g : Z) (u0 : nat) (xs : list (nat * nat * option Z)) :
  bucket_remove u0 (bucket_members g xs) = bucket_members g (update_row_group u0 None xs).
Proof.
  induction xs as [|r xs' IH]; cbn [bucket_members update_row_group].
  - reflexivity.
  - destruct (decide (row_grp r = Some g)) as [Hg|Hg], (decide (row_up r = u0)) as [Hu|Hu];
      cbn [bucket_members bucket_remove row_grp row_up] in *.
    + rewrite decide_True by exact Hu. exact IH.
    + rewrite decide_False by exact Hu. rewrite decide_True by exact Hg.
      cbn [fst]. rewrite IH. reflexivity.
    + rewrite decide_False by discriminate. exact IH.
    + rewrite decide_False by exact Hg. exact IH.
Qed.

Lemma update_row_group_gt (u0 : nat) (mg : option Z) (xs : list (nat * nat * option Z)) :
  Forall (fun q => (u0 < row_up q)%nat) xs ->
  update_row_group u0 mg xs = xs.
Proof.
  induction xs as [|q xs' IH]; intro Hfa; cbn [update_row_group].
  - reflexivity.
  - inversion Hfa as [|? ? Hq Hfa']; subst.
    rewrite decide_False by lia. f_equal. exact (IH Hfa').
Qed.

Lemma bucket_members_update_none_ne (g : Z) (u0 k0 : nat) (mg_old : option Z) (xs : list (nat * nat * option Z)) :
  StronglySorted Nat.lt (row_up <$> xs) ->
  (u0, k0, mg_old) ∈ xs ->
  Some g <> mg_old ->
  bucket_members g xs = bucket_members g (update_row_group u0 None xs).
Proof.
  induction xs as [|r xs' IH]; intros Hsort Hin Hne.
  - inversion Hin.
  - destruct (strongly_sorted_tail_lt r xs' Hsort) as [Hsort' Hlt'].
    apply elem_of_cons in Hin as [<- | Hin'].
    + cbn [update_row_group].
      rewrite decide_True by reflexivity.
      change (row_up (u0, k0, mg_old)) with u0 in Hlt'.
      rewrite (update_row_group_gt u0 None xs' Hlt').
      cbn [bucket_members].
      rewrite decide_False by (cbn; congruence).
      rewrite decide_False by (cbn; discriminate).
      reflexivity.
    + assert (Hru : row_up r <> u0).
      { intro Heq. rewrite Forall_forall in Hlt'.
        pose proof (Hlt' (u0, k0, mg_old) Hin') as Hlt_u.
        cbn [row_up fst] in Hlt_u. rewrite Heq in Hlt_u. lia. }
      cbn [update_row_group].
      rewrite decide_False by exact Hru.
      cbn [bucket_members].
      destruct (decide (row_grp r = Some g)); [f_equal|]; exact (IH Hsort' Hin' Hne).
Qed.

Lemma bucket_map_apply_old_correct (g : Z) (u0 k0 : nat) (mg_old : option Z) (xs : list (nat * nat * option Z)) :
  StronglySorted Nat.lt (row_up <$> xs) ->
  (u0, k0, mg_old) ∈ xs ->
  bucket_map_apply_old u0 mg_old (fun g' => bucket_members g' xs) g =
  bucket_members g (update_row_group u0 None xs).
Proof.
  intros Hsort Hin.
  destruct mg_old as [g_old|]; cbn [bucket_map_apply_old].
  - destruct (decide (g = g_old)) as [-> | Hne].
    + apply bucket_remove_members.
    + apply (bucket_members_update_none_ne g u0 k0 (Some g_old) xs Hsort Hin).
      intro Heq. injection Heq. contradiction.
  - apply (bucket_members_update_none_ne g u0 k0 None xs Hsort Hin).
    discriminate.
Qed.

Lemma bucket_members_all_gt (u0 : nat) (g : Z) (xs : list (nat * nat * option Z)) :
  Forall (fun q => (u0 < row_up q)%nat) xs ->
  Forall (fun m : nat * nat => (u0 < m.1)%nat) (bucket_members g xs).
Proof.
  induction xs as [|r xs' IH]; intro Hfa; cbn [bucket_members].
  - constructor.
  - inversion Hfa as [|? ? Hr Hfa']; subst.
    destruct (decide (row_grp r = Some g)); [constructor; [exact Hr | apply IH; exact Hfa'] | apply IH; exact Hfa'].
Qed.

Lemma update_row_group_absent (u0 : nat) (mg1 mg2 : option Z) (xs : list (nat * nat * option Z)) :
  Forall (fun q => (u0 < row_up q)%nat) xs ->
  update_row_group u0 mg1 xs = update_row_group u0 mg2 xs.
Proof.
  induction xs as [|r xs' IH]; intro Hfa; cbn [update_row_group].
  - reflexivity.
  - inversion Hfa as [|? ? Hr Hfa']; subst.
    rewrite !decide_False by lia. f_equal. apply IH. exact Hfa'.
Qed.

Lemma bucket_members_update_ne (g : Z) (mg' : option Z) (u_idx : nat) (xs : list (nat * nat * option Z)) :
  Some g <> mg' ->
  bucket_members g (update_row_group u_idx mg' xs) =
  bucket_members g (update_row_group u_idx None xs).
Proof.
  intros Hne. induction xs as [|r xs' IH]; cbn [update_row_group].
  - reflexivity.
  - destruct (decide (row_up r = u_idx)) as [Hu|Hu]; cbn [bucket_members].
    + rewrite decide_False by (cbn; congruence).
      rewrite decide_False by (cbn; discriminate).
      exact IH.
    + destruct (decide (row_grp r = Some g)); [f_equal|]; exact IH.
Qed.

Lemma bucket_insert_members_eq (g_new : Z) (u0 k0 : nat) (mg_old : option Z) (xs : list (nat * nat * option Z)) :
  StronglySorted Nat.lt (row_up <$> xs) ->
  (u0, k0, mg_old) ∈ xs ->
  bucket_insert (u0, k0) (bucket_members g_new (update_row_group u0 None xs)) =
  bucket_members g_new (update_row_group u0 (Some g_new) xs).
Proof.
  induction xs as [|r xs' IH]; intros Hsort Hin.
  - inversion Hin.
  - destruct (strongly_sorted_tail_lt r xs' Hsort) as [Hsort' Hlt'].
    apply elem_of_cons in Hin as [<- | Hin'].
    + change (row_up (u0, k0, mg_old)) with u0 in Hlt'.
      cbn [update_row_group]. rewrite !decide_True by reflexivity.
      rewrite (update_row_group_gt u0 None xs' Hlt').
      rewrite (update_row_group_gt u0 (Some g_new) xs' Hlt').
      cbn [bucket_members].
      rewrite decide_False by (cbn; discriminate).
      rewrite decide_True by reflexivity.
      cbn [row_up row_key fst].
      set (tail := bucket_members g_new xs').
      assert (Htail : Forall (fun m : nat * nat => (u0 < m.1)%nat) tail).
      { subst tail. apply bucket_members_all_gt. exact Hlt'. }
      destruct tail as [|m ms'].
      * reflexivity.
      * inversion Htail as [|? ? Hm _]; subst.
        cbn [bucket_insert fst]. rewrite decide_True by exact Hm. reflexivity.
    + assert (Hru_lt : (row_up r < u0)%nat).
      { rewrite Forall_forall in Hlt'. exact (Hlt' (u0, k0, mg_old) Hin'). }
      cbn [update_row_group]. rewrite !decide_False by lia.
      cbn [bucket_members].
      destruct (decide (row_grp r = Some g_new)) as [Hgr|Hgr].
      * cbn [bucket_insert]. rewrite decide_False by (cbn [fst]; lia).
        f_equal. exact (IH Hsort' Hin').
      * exact (IH Hsort' Hin').
Qed.

Lemma bucket_map_update_members_eq
  (u0 k0 : nat) (mg_old mg_new : option Z) (xs : list (nat * nat * option Z)) (g : Z) :
  StronglySorted Nat.lt (row_up <$> xs) ->
  (u0, k0, mg_old) ∈ xs ->
  bucket_map_update u0 k0 mg_old mg_new (fun g' => bucket_members g' xs) g =
  bucket_members g (update_row_group u0 mg_new xs).
Proof.
  intros Hsort Hin.
  unfold bucket_map_update.
  rewrite (bucket_map_apply_old_correct g u0 k0 mg_old xs Hsort Hin).
  destruct mg_new as [g_new|].
  - destruct (decide (g = g_new)) as [->|Hne].
    + apply (bucket_insert_members_eq g_new u0 k0 mg_old xs Hsort Hin).
    + symmetry. apply bucket_members_update_ne. congruence.
  - reflexivity.
Qed.

Lemma bucket_map_update_untouched
  (u0 k0 : nat) (mg_old mg_new : option Z) (bmap : Z -> list (nat * nat)) (g : Z) :
  Some g <> mg_old ->
  Some g <> mg_new ->
  bucket_map_update u0 k0 mg_old mg_new bmap g = bmap g.
Proof.
  intros Hold Hnew. unfold bucket_map_update, bucket_map_apply_old.
  destruct mg_old as [g1|], mg_new as [g2|];
    repeat rewrite decide_False by congruence; reflexivity.
Qed.

Definition distinct_seen_ok
  (seen : list Z) (bmap : Z -> list (nat * nat)) (xs : list (nat * nat * option Z)) : Prop :=
  (forall g, g ∈ seen ->
    exists m ms, bmap g = m :: ms /\ Forall (fun r => (m.1 < row_up r)%nat) xs) /\
  (forall g, g ∉ seen -> bmap g = bucket_members g xs).

Lemma distinct_by_buckets_seen
  (seen : list Z) (bmap : Z -> list (nat * nat)) (xs : list (nat * nat * option Z)) :
  StronglySorted Nat.lt (row_up <$> xs) ->
  distinct_seen_ok seen bmap xs ->
  distinct_by_buckets bmap xs = distinct_scan_aux seen xs.
Proof.
  revert seen. induction xs as [|r xs' IH]; intros seen Hsort [Hseen Hunseen].
  - reflexivity.
  - destruct (strongly_sorted_tail_lt r xs' Hsort) as [Hsort' Hlt'].
    cbn [distinct_by_buckets distinct_scan_aux].
    destruct (row_grp r) as [g|] eqn:Hgrp.
    + destruct (decide (g ∈ seen)) as [Hin|Hnot].
      * destruct (Hseen g Hin) as [m [ms [Hbmap Hfa]]].
        inversion Hfa as [|? ? Hm_lt _]; subst.
        unfold is_bucket_winner. rewrite Hgrp, Hbmap.
        rewrite bool_decide_false by lia.
        apply IH; [exact Hsort'|]. split.
        -- intros g' Hg'. destruct (Hseen g' Hg') as [m' [ms' [Hb' Hfa']]].
           inversion Hfa'; subst. exists m', ms'. split; assumption.
        -- intros g' Hg'. assert (Hne : g' <> g) by (intro; subst; apply Hg'; exact Hin).
           rewrite (Hunseen g' Hg'). cbn [bucket_members].
           rewrite Hgrp, decide_False by congruence. reflexivity.
      * pose proof (Hunseen g Hnot) as Hbmap.
        cbn [bucket_members] in Hbmap. rewrite Hgrp, decide_True in Hbmap by reflexivity.
        unfold is_bucket_winner. rewrite Hgrp, Hbmap.
        rewrite bool_decide_true by (split; reflexivity).
        f_equal. apply IH; [exact Hsort'|]. split.
        -- intros g' Hg'. apply elem_of_cons in Hg' as [-> | Hg'].
           ++ exists (row_up r, row_key r), (bucket_members g xs').
              split; [exact Hbmap | exact Hlt'].
           ++ destruct (Hseen g' Hg') as [m' [ms' [Hb' Hfa']]].
              inversion Hfa'; subst. exists m', ms'. split; assumption.
        -- intros g' Hg'.
           assert (Hg'_not : g' ∉ seen) by (intro; apply Hg'; right; assumption).
           assert (Hne : g' <> g) by (intro; subst; apply Hg'; left).
           rewrite (Hunseen g' Hg'_not). cbn [bucket_members].
           rewrite Hgrp, decide_False by congruence. reflexivity.
    + unfold is_bucket_winner. rewrite Hgrp.
      apply IH; [exact Hsort'|]. split.
      * intros g' Hg'. destruct (Hseen g' Hg') as [m' [ms' [Hb' Hfa']]].
        inversion Hfa'; subst. exists m', ms'. split; assumption.
      * intros g' Hg'. rewrite (Hunseen g' Hg'). cbn [bucket_members].
        rewrite Hgrp, decide_False by discriminate. reflexivity.
Qed.

Lemma distinct_by_buckets_refines_scan (xs : list (nat * nat * option Z)) :
  StronglySorted Nat.lt (row_up <$> xs) ->
  distinct_by_buckets (fun g => bucket_members g xs) xs = distinct_scan xs.
Proof.
  intro Hsort. unfold distinct_scan.
  apply distinct_by_buckets_seen; [exact Hsort|].
  split; [intros g Hin; inversion Hin | intros g _; reflexivity].
Qed.

(**md
**Incremental 2-bucket updates and `Members[0]` winner selection in `DistinctView` refine `distinct_scan`.**

Updating row `u0` from optional settled value `mg_old` to `mg_new` removes `u0`
from bucket `mg_old` (if `Some`), inserts `(u0, k0)` by ascending upstream index
into bucket `mg_new` (if `Some`), leaves every other bucket untouched, and
preserves the exact `Members[0]` winner sequence computed by sequential
`distinct_scan` over the updated rows.

- [u0:] upstream slot index of the updated row.
- [k0:] upstream key held at slot `u0`.
- [mg_old:] prior settled distinct value (or `None` when unsettled/failed).
- [mg_new:] next settled distinct value (or `None` when unsettled/failed).
- [xs:] upstream rows in strictly ascending `UpIdx` order prior to the update.
*)
Theorem distinct_bucket_update_refines_scan
  (u0 k0 : nat) (mg_old mg_new : option Z) (xs : list (nat * nat * option Z)) :
  StronglySorted Nat.lt (row_up <$> xs) ->
  (u0, k0, mg_old) ∈ xs ->
  let bmap' := bucket_map_update u0 k0 mg_old mg_new (fun g => bucket_members g xs) in
  let xs' := update_row_group u0 mg_new xs in
  (forall g, Some g <> mg_old -> Some g <> mg_new -> bmap' g = bucket_members g xs) /\
  (forall g, bmap' g = bucket_members g xs') /\
  distinct_by_buckets bmap' xs' = distinct_scan xs'.
Proof.
  intros Hsort Hin bmap' xs'.
  split_and!.
  - intros g Hold Hnew. unfold bmap'.
    apply (bucket_map_update_untouched u0 k0 mg_old mg_new (fun g0 => bucket_members g0 xs) g Hold Hnew).
  - intro g. unfold bmap', xs'.
    apply (bucket_map_update_members_eq u0 k0 mg_old mg_new xs g Hsort Hin).
  - rewrite (distinct_by_buckets_seen [] bmap' xs').
    + reflexivity.
    + unfold xs'. apply strongly_sorted_row_up_update; exact Hsort.
    + split.
      * intros g Hnil. inversion Hnil.
      * intros g _. unfold bmap', xs'.
        apply (bucket_map_update_members_eq u0 k0 mg_old mg_new xs g Hsort Hin).
Qed.

Fixpoint group_scan_pairs_aux (seen : list Z) (xs : list (nat * nat * option Z)) : list (nat * Z) :=
  match xs with
  | [] => []
  | r :: xs' =>
      match row_grp r with
      | None => group_scan_pairs_aux seen xs'
      | Some g =>
          if decide (g ∈ seen) then group_scan_pairs_aux seen xs'
          else (row_up r, g) :: group_scan_pairs_aux (g :: seen) xs'
      end
  end.

Lemma group_scan_pairs_snd (seen : list Z) (xs : list (nat * nat * option Z)) :
  snd <$> group_scan_pairs_aux seen xs = group_scan_order_aux seen xs.
Proof.
  revert seen. induction xs as [|r xs' IH]; intro seen; cbn [group_scan_pairs_aux group_scan_order_aux].
  - reflexivity.
  - destruct (row_grp r) as [g0|]; [|apply IH].
    destruct (decide (g0 ∈ seen)); [apply IH|].
    cbn [fmap list_fmap snd]. f_equal. apply IH.
Qed.

Lemma group_scan_pairs_in_lt
  (seen : list Z) (xs : list (nat * nat * option Z)) (u : nat) (g : Z) (u_bound : nat) :
  Forall (fun r => (u_bound < row_up r)%nat) xs ->
  (u, g) ∈ group_scan_pairs_aux seen xs ->
  (u_bound < u)%nat.
Proof.
  revert seen. induction xs as [|r xs' IH]; intros seen Hfa Hin; cbn [group_scan_pairs_aux] in Hin.
  - inversion Hin.
  - inversion Hfa as [|? ? Hr Hfa']; subst.
    destruct (row_grp r) as [g0|]; [|eapply IH; eassumption].
    destruct (decide (g0 ∈ seen)); [eapply IH; eassumption|].
    apply elem_of_cons in Hin as [Heq | Hin'].
    + injection Heq as -> ->. exact Hr.
    + eapply IH; eassumption.
Qed.

Lemma group_scan_pairs_sorted (seen : list Z) (xs : list (nat * nat * option Z)) :
  StronglySorted Nat.lt (row_up <$> xs) ->
  StronglySorted (fun a b : nat * Z => (a.1 < b.1)%nat) (group_scan_pairs_aux seen xs).
Proof.
  revert seen. induction xs as [|r xs' IH]; intros seen Hsort; cbn [group_scan_pairs_aux].
  - constructor.
  - destruct (strongly_sorted_tail_lt r xs' Hsort) as [Hsort' Hlt'].
    destruct (row_grp r) as [g0|]; [|apply IH; exact Hsort'].
    destruct (decide (g0 ∈ seen)); [apply IH; exact Hsort'|].
    constructor; [apply IH; exact Hsort'|].
    rewrite Forall_forall. intros [u g] Hmem. cbn [fst].
    eapply group_scan_pairs_in_lt; eassumption.
Qed.

Lemma group_scan_pairs_not_seen (seen : list Z) (xs : list (nat * nat * option Z)) (u : nat) (g : Z) :
  (u, g) ∈ group_scan_pairs_aux seen xs ->
  g ∉ seen.
Proof.
  revert seen. induction xs as [|r xs' IH]; intros seen Hin Hg_in; cbn [group_scan_pairs_aux] in Hin.
  - inversion Hin.
  - destruct (row_grp r) as [g0|]; [|eapply IH; eassumption].
    destruct (decide (g0 ∈ seen)).
    + eapply IH; eassumption.
    + apply elem_of_cons in Hin as [Heq | Hin'].
      * injection Heq as -> ->. contradiction.
      * apply (IH _ Hin'). right. exact Hg_in.
Qed.

Lemma group_scan_pairs_elem_iff (seen : list Z) (xs : list (nat * nat * option Z)) (u : nat) (g : Z) :
  (u, g) ∈ group_scan_pairs_aux seen xs <->
  g ∉ seen /\ exists k ms, bucket_members g xs = (u, k) :: ms.
Proof.
  revert seen. induction xs as [|r xs' IH]; intro seen; cbn [group_scan_pairs_aux bucket_members].
  - split; [intro H; inversion H | intros [_ [k [ms H]]]; discriminate H].
  - destruct (row_grp r) as [g0|].
    + destruct (decide (g0 ∈ seen)) as [Hg0_seen|Hg0_not].
      * rewrite IH. split; intros [Hg_not [k [ms Hmem]]]; split; try exact Hg_not; exists k, ms.
        -- assert (Hne : g <> g0) by (intro; subst; contradiction).
           rewrite decide_False by congruence. exact Hmem.
        -- assert (Hne : g <> g0) by (intro; subst; contradiction).
           rewrite decide_False in Hmem by congruence. exact Hmem.
      * split.
        -- intro Hin. apply elem_of_cons in Hin as [Heq | Hin'].
           ++ injection Heq as -> ->. split; [exact Hg0_not|].
              exists (row_key r), (bucket_members g0 xs').
              rewrite decide_True by reflexivity. reflexivity.
           ++ pose proof (group_scan_pairs_not_seen _ _ _ _ Hin') as Hg_ns.
              apply IH in Hin' as [_ Hmem].
              assert (Hg_not : g ∉ seen) by (intro; apply Hg_ns; right; assumption).
              assert (Hne : g <> g0) by (intro; subst; apply Hg_ns; left).
              split; [exact Hg_not|].
              destruct Hmem as [k [ms Hmem']].
              exists k, ms. rewrite decide_False by congruence. exact Hmem'.
        -- intros [Hg_not [k [ms Hmem]]].
           destruct (decide (g = g0)) as [-> | Hne].
           ++ rewrite decide_True in Hmem by reflexivity.
              injection Hmem as -> _. left.
           ++ right. apply IH. split; [intro Hc; apply elem_of_cons in Hc as [?|?]; contradiction|].
              exists k, ms. rewrite decide_False in Hmem by congruence. exact Hmem.
    + rewrite IH.
      split; intros [Hg_not [k [ms Hmem]]]; split; try exact Hg_not; exists k, ms.
      * rewrite decide_False by discriminate. exact Hmem.
      * rewrite decide_False in Hmem by discriminate. exact Hmem.
Qed.

Lemma insert_first_idx_perm (p : nat * Z) (l : list (nat * Z)) :
  insert_first_idx p l ≡ₚ p :: l.
Proof.
  induction l as [|q l' IH]; cbn [insert_first_idx].
  - reflexivity.
  - destruct (decide (p.1 < q.1)%nat); [reflexivity | rewrite IH; apply perm_swap].
Qed.

Lemma insert_first_idx_sorts (p : nat * Z) (l : list (nat * Z)) :
  (forall q, q ∈ l -> q.1 <> p.1) ->
  StronglySorted (fun a b : nat * Z => (a.1 < b.1)%nat) l ->
  StronglySorted (fun a b : nat * Z => (a.1 < b.1)%nat) (insert_first_idx p l).
Proof.
  induction l as [|q l' IH]; intros Hne Hsort; cbn [insert_first_idx].
  - constructor; [constructor | constructor].
  - inversion Hsort as [|? ? Hsort' Hfa]; subst.
    assert (Hqne : q.1 <> p.1) by (apply Hne; left).
    destruct (decide (p.1 < q.1)%nat) as [Hlt|Hge].
    + constructor; [exact Hsort|].
      constructor; [exact Hlt|].
      eapply Forall_impl; [exact Hfa|]. intros z Hz. destruct p, q, z; cbn in *; lia.
    + constructor; [apply IH; [intros z Hz; apply Hne; right; exact Hz | exact Hsort']|].
      rewrite Forall_forall. intros z Hz.
      assert (Hz_in : z ∈ p :: l') by (rewrite <- (insert_first_idx_perm p l'); exact Hz).
      apply elem_of_cons in Hz_in as [-> | Hz_in'].
      * destruct p, q; cbn in *; lia.
      * rewrite Forall_forall in Hfa. apply Hfa; exact Hz_in'.
Qed.

Lemma active_group_pairs_in_iff (bmap : Z -> list (nat * nat)) (gs : list Z) (p : nat * Z) :
  p ∈ active_group_pairs bmap gs <->
  p.2 ∈ gs /\ exists m ms, bmap p.2 = m :: ms /\ p.1 = m.1.
Proof.
  induction gs as [|g gs' IH]; cbn [active_group_pairs foldr].
  - split; [intro H; inversion H | intros [H _]; inversion H].
  - destruct (bmap g) as [|m ms] eqn:Hbm.
    + rewrite IH. split; intros [Hin Hrest]; split; try assumption.
      * right. exact Hin.
      * apply elem_of_cons in Hin as [Heq | Hin']; [|exact Hin'].
        subst g. destruct Hrest as [m' [ms' [Hbm' _]]]. rewrite Hbm in Hbm'. discriminate Hbm'.
    + rewrite insert_first_idx_perm. split.
      * intro Hin. apply elem_of_cons in Hin as [-> | Hin'].
        -- split; [cbn; left | exists m, ms; split; [exact Hbm | reflexivity]].
        -- apply IH in Hin' as [Hin_gs Hrest]. split; [right; exact Hin_gs | exact Hrest].
      * intros [Hin Hrest]. apply elem_of_cons in Hin as [Heq | Hin'].
        -- destruct Hrest as [m' [ms' [Hbm' Hp1]]].
           rewrite Heq, Hbm in Hbm'. injection Hbm' as <- _.
           destruct p as [p1 p2]; cbn [fst snd] in *; subst; left.
        -- right. apply IH. split; [exact Hin' | exact Hrest].
Qed.

Lemma bucket_members_first_lt_unique
  (g1 g2 : Z) (m1 m2 : nat * nat) (ms1 ms2 : list (nat * nat)) (xs : list (nat * nat * option Z)) :
  bucket_members g1 xs = m1 :: ms1 ->
  bucket_members g2 xs = m2 :: ms2 ->
  g1 <> g2 ->
  m1.1 = m2.1 ->
  StronglySorted Nat.lt (row_up <$> xs) ->
  False.
Proof.
  induction xs as [|r xs' IH]; intros Hm1 Hm2 Hne Heq1 Hsort.
  - discriminate Hm1.
  - destruct (strongly_sorted_tail_lt r xs' Hsort) as [Hsort' Hlt'].
    cbn [bucket_members] in Hm1, Hm2.
    destruct (decide (row_grp r = Some g1)) as [Hg1|Hg1],
             (decide (row_grp r = Some g2)) as [Hg2|Hg2].
    + rewrite Hg1 in Hg2. injection Hg2 as <-. contradiction.
    + injection Hm1 as <- <-.
      assert (Hm2_in : m2 ∈ bucket_members g2 xs') by (rewrite Hm2; left).
      assert (Hfa2 := bucket_members_all_gt (row_up r) g2 xs' Hlt').
      rewrite Forall_forall in Hfa2. pose proof (Hfa2 m2 Hm2_in) as Hlt. cbn [fst] in *. lia.
    + injection Hm2 as <- <-.
      assert (Hm1_in : m1 ∈ bucket_members g1 xs') by (rewrite Hm1; left).
      assert (Hfa1 := bucket_members_all_gt (row_up r) g1 xs' Hlt').
      rewrite Forall_forall in Hfa1. pose proof (Hfa1 m1 Hm1_in) as Hlt. cbn [fst] in *. lia.
    + exact (IH Hm1 Hm2 Hne Heq1 Hsort').
Qed.

Lemma active_group_pairs_sorted (xs : list (nat * nat * option Z)) (gs : list Z) :
  StronglySorted Nat.lt (row_up <$> xs) ->
  NoDup gs ->
  StronglySorted (fun a b : nat * Z => (a.1 < b.1)%nat) (active_group_pairs (fun g => bucket_members g xs) gs).
Proof.
  intros Hsort Hnd. induction gs as [|g gs' IH]; cbn [active_group_pairs foldr].
  - constructor.
  - inversion Hnd as [|? ? Hg_not Hnd']; subst.
    destruct (bucket_members g xs) as [|m ms] eqn:Hbm.
    + apply IH. exact Hnd'.
    + apply insert_first_idx_sorts.
      * intros [u g'] Hmem. cbn [fst].
        apply active_group_pairs_in_iff in Hmem as [Hg'_in [m' [ms' [Hbm' Hu_eq]]]].
        cbn [fst snd] in *.
        assert (Hne : g <> g') by (intro; subst; contradiction).
        intro Heq. eapply (bucket_members_first_lt_unique g g' m m' ms ms' xs Hbm Hbm' Hne); [congruence | exact Hsort].
      * apply IH. exact Hnd'.
Qed.

Lemma strongly_sorted_pair_lt_unique (l1 l2 : list (nat * Z)) :
  StronglySorted (fun a b : nat * Z => (a.1 < b.1)%nat) l1 ->
  StronglySorted (fun a b : nat * Z => (a.1 < b.1)%nat) l2 ->
  (forall x, x ∈ l1 <-> x ∈ l2) ->
  l1 = l2.
Proof.
  revert l2. induction l1 as [|a l1' IH]; intros l2 Hs1 Hs2 Hiff.
  - destruct l2 as [|b l2']; [reflexivity|].
    exfalso. pose proof (proj2 (Hiff b) ltac:(left)) as Hnil. inversion Hnil.
  - destruct l2 as [|b l2'].
    + exfalso. pose proof (proj1 (Hiff a) ltac:(left)) as Hnil. inversion Hnil.
    + inversion Hs1 as [|? ? Hs1' Hfa]; subst.
      inversion Hs2 as [|? ? Hs2' Hfb]; subst.
      assert (Hb_in : b ∈ a :: l1') by (apply Hiff; left).
      assert (Ha_in : a ∈ b :: l2') by (apply Hiff; left).
      apply elem_of_cons in Hb_in as [Heq_ba | Hb_l1'].
      * subst b. f_equal. apply IH; [exact Hs1' | exact Hs2'|].
        assert (Ha1 : a ∉ l1').
        { intro Hc. rewrite Forall_forall in Hfa. pose proof (Hfa a Hc) as Hlt. lia. }
        assert (Ha2 : a ∉ l2').
        { intro Hc. rewrite Forall_forall in Hfb. pose proof (Hfb a Hc) as Hlt. lia. }
        intro x. split; intro Hx.
        -- assert (Hne : x <> a) by (intro; subst; contradiction).
           pose proof (proj1 (Hiff x) ltac:(right; exact Hx)) as Hx'.
           apply elem_of_cons in Hx' as [?|?]; [contradiction | assumption].
        -- assert (Hne : x <> a) by (intro; subst; contradiction).
           pose proof (proj2 (Hiff x) ltac:(right; exact Hx)) as Hx'.
           apply elem_of_cons in Hx' as [?|?]; [contradiction | assumption].
      * rewrite Forall_forall in Hfa. pose proof (Hfa b Hb_l1') as Hab.
        apply elem_of_cons in Ha_in as [Heq_ab | Ha_l2']; [subst; lia|].
        rewrite Forall_forall in Hfb. pose proof (Hfb a Ha_l2') as Hba.
        lia.
Qed.

Lemma active_group_pairs_ext (b1 b2 : Z -> list (nat * nat)) (gs : list Z) :
  (forall g, g ∈ gs -> b1 g = b2 g) ->
  active_group_pairs b1 gs = active_group_pairs b2 gs.
Proof.
  induction gs as [|g gs' IH]; intro Hext; cbn [active_group_pairs foldr].
  - reflexivity.
  - unfold active_group_pairs in IH.
    rewrite (Hext g ltac:(left)).
    rewrite (IH (fun g' Hg' => Hext g' ltac:(right; exact Hg'))).
    reflexivity.
Qed.

Lemma bucket_members_nonempty_in_xs (g : Z) (m : nat * nat) (ms : list (nat * nat)) (xs : list (nat * nat * option Z)) :
  bucket_members g xs = m :: ms ->
  exists r, r ∈ xs /\ row_grp r = Some g.
Proof.
  induction xs as [|r xs' IH]; intro Hbm; cbn [bucket_members] in Hbm.
  - discriminate Hbm.
  - destruct (decide (row_grp r = Some g)) as [Hg|Hg].
    + exists r. split; [left | exact Hg].
    + destruct (IH Hbm) as [r' [Hr' Hgr']]. exists r'. split; [right; exact Hr' | exact Hgr'].
Qed.

(**md
**Incremental `Grouping` move isolates untouched inner views and refines member and outer `FirstIdx` scans.**

Moving row `u0` from optional group `mg_old` to `mg_new` removes `u0` from group
`mg_old` (if `Some`), inserts `(u0, k0)` by ascending upstream index into group
`mg_new` (if `Some`), leaves every other group's member cell completely untouched,
yields exact `group_scan_members` for every group (so untouched inner `GroupView`
readers stay asleep), and sorting active non-empty groups by `FirstIdx`
(`Members[0].UpIdx`) equals `group_scan_order` over the updated rows.

- [u0:] upstream slot index of the moved row.
- [k0:] upstream key held at slot `u0`.
- [mg_old:] prior group key (or `None` when ungrouped).
- [mg_new:] next group key (or `None` when ungrouped).
- [xs:] upstream rows in strictly ascending `UpIdx` order prior to the move.
- [gs:] duplicate-free candidate group list covering all active groups in the updated rows.
*)
Theorem grouping_move_refines_scan
  (u0 k0 : nat) (mg_old mg_new : option Z)
  (xs : list (nat * nat * option Z)) (gs : list Z) :
  StronglySorted Nat.lt (row_up <$> xs) ->
  (u0, k0, mg_old) ∈ xs ->
  let bmap' := bucket_map_update u0 k0 mg_old mg_new (fun g => bucket_members g xs) in
  let xs' := update_row_group u0 mg_new xs in
  NoDup gs ->
  (forall r g, r ∈ xs' -> row_grp r = Some g -> g ∈ gs) ->
  (forall g, Some g <> mg_old -> Some g <> mg_new -> bmap' g = bucket_members g xs) /\
  (forall g, snd <$> bmap' g = group_scan_members g xs') /\
  ordered_groups_by_first_idx bmap' gs = group_scan_order xs'.
Proof.
  intros Hsort Hin bmap' xs' Hnd Hcov.
  assert (Hbmap' : forall g, bmap' g = bucket_members g xs').
  { intro g. unfold bmap', xs'.
    apply (bucket_map_update_members_eq u0 k0 mg_old mg_new xs g Hsort Hin). }
  assert (Hsort' : StronglySorted Nat.lt (row_up <$> xs')).
  { unfold xs'. apply strongly_sorted_row_up_update; exact Hsort. }
  split_and!.
  - intros g Hold Hnew. unfold bmap'.
    apply (bucket_map_update_untouched u0 k0 mg_old mg_new (fun g0 => bucket_members g0 xs) g Hold Hnew).
  - intro g. unfold group_scan_members. rewrite Hbmap'. reflexivity.
  - unfold ordered_groups_by_first_idx, group_scan_order.
    rewrite (active_group_pairs_ext bmap' (fun g => bucket_members g xs') gs (fun g _ => Hbmap' g)).
    rewrite <- (group_scan_pairs_snd [] xs').
    f_equal. apply strongly_sorted_pair_lt_unique.
    + apply active_group_pairs_sorted; assumption.
    + apply group_scan_pairs_sorted; exact Hsort'.
    + intros [u g]. rewrite active_group_pairs_in_iff, group_scan_pairs_elem_iff.
      cbn [fst snd]. split.
      * intros [_ [m [ms [Hbm ->]]]].
        split; [intro Hnil; inversion Hnil | destruct m as [m1 m2]; exists m2, ms; exact Hbm].
      * intros [_ [k [ms Hbm]]].
        destruct (bucket_members_nonempty_in_xs g (u, k) ms xs' Hbm) as [r [Hr Hgrp]].
        split; [exact (Hcov r g Hr Hgrp) | exists (u, k), ms; split; [exact Hbm | reflexivity]].
Qed.

(** ** Binary tournament winner tree and sticky-holder extremes *)

Definition tour_entry : Type := option (nat * Z).

Definition pick_winner (a b : tour_entry) : tour_entry :=
  match a, b with
  | None, _ => b
  | Some _, None => a
  | Some (ia, ra), Some (ib, rb) =>
      if Z.leb ra rb then Some (ia, ra) else Some (ib, rb)
  end.

Lemma pick_winner_none_r (a : tour_entry) :
  pick_winner a None = a.
Proof.
  destruct a as [[ia ra]|]; reflexivity.
Qed.

Lemma pick_winner_assoc (a b c : tour_entry) :
  pick_winner (pick_winner a b) c = pick_winner a (pick_winner b c).
Proof.
  destruct a as [[ia ra]|], b as [[ib rb]|], c as [[ic rc]|]; cbn [pick_winner];
  repeat match goal with
  | |- context [Z.leb ?x ?y] => destruct (Z.leb_spec x y); cbn [pick_winner]
  end; try reflexivity; lia.
Qed.

Inductive tour_tree :=
| TLeaf (val : tour_entry)
| TNode (win : tour_entry) (left right : tour_tree).

Definition tour_winner (t : tour_tree) : tour_entry :=
  match t with
  | TLeaf w => w
  | TNode w _ _ => w
  end.

Fixpoint build_tour (h base : nat) (leaves : nat -> option Z) : tour_tree :=
  match h with
  | 0 =>
      TLeaf (match leaves base with
             | Some r => Some (base, r)
             | None => None
             end)
  | S h' =>
      let l := build_tour h' base leaves in
      let r := build_tour h' (base + Nat.pow 2 h') leaves in
      TNode (pick_winner (tour_winner l) (tour_winner r)) l r
  end.

Fixpoint extreme_scan (base len : nat) (leaves : nat -> option Z) : tour_entry :=
  match len with
  | 0 => None
  | S len' =>
      let here := match leaves base with
                  | Some r => Some (base, r)
                  | None => None
                  end in
      pick_winner here (extreme_scan (S base) len' leaves)
  end.

Lemma extreme_scan_app (len1 len2 base : nat) (leaves : nat -> option Z) :
  extreme_scan base (len1 + len2) leaves =
  pick_winner (extreme_scan base len1 leaves) (extreme_scan (base + len1) len2 leaves).
Proof.
  revert base. induction len1 as [|len1' IH]; intro base.
  - cbn [Nat.add extreme_scan pick_winner]. rewrite Nat.add_0_r. reflexivity.
  - cbn [Nat.add extreme_scan]. rewrite IH.
    rewrite <- pick_winner_assoc, Nat.add_succ_r. reflexivity.
Qed.

Lemma tour_winner_build_eq_scan (h base : nat) (leaves : nat -> option Z) :
  tour_winner (build_tour h base leaves) = extreme_scan base (Nat.pow 2 h) leaves.
Proof.
  revert base. induction h as [|h' IH]; intro base.
  - cbn [build_tour Nat.pow extreme_scan tour_winner]. rewrite pick_winner_none_r. reflexivity.
  - cbn [build_tour Nat.pow Nat.mul tour_winner].
    rewrite Nat.add_0_r, !IH, extreme_scan_app. reflexivity.
Qed.

Lemma extreme_scan_none_spec (len base : nat) (leaves : nat -> option Z) :
  extreme_scan base len leaves = None ->
  forall j, base <= j < base + len -> leaves j = None.
Proof.
  revert base. induction len as [|len' IH]; intros base Hnone j Hj.
  - lia.
  - cbn [extreme_scan] in Hnone.
    destruct (leaves base) as [r|] eqn:Hbase.
    + destruct (extreme_scan (S base) len' leaves) as [[ib rb]|]; cbn [pick_winner] in Hnone;
        [destruct (Z.leb r rb) |]; discriminate.
    + cbn [pick_winner] in Hnone.
      destruct (decide (j = base)) as [->|Hne].
      * exact Hbase.
      * apply (IH (S base) Hnone j). lia.
Qed.

Lemma extreme_scan_some_spec (len base iw : nat) (rw : Z) (leaves : nat -> option Z) :
  extreme_scan base len leaves = Some (iw, rw) ->
  base <= iw < base + len /\
  leaves iw = Some rw /\
  (forall j r, base <= j < base + len -> leaves j = Some r -> (rw <= r)%Z) /\
  (forall j r, base <= j < iw -> leaves j = Some r -> (rw < r)%Z).
Proof.
  revert base iw rw. induction len as [|len' IH]; intros base iw rw Hsome.
  - cbn [extreme_scan] in Hsome. discriminate.
  - cbn [extreme_scan] in Hsome.
    destruct (leaves base) as [rb|] eqn:Hbase.
    + destruct (extreme_scan (S base) len' leaves) as [[it rt]|] eqn:Hrest.
      * cbn [pick_winner] in Hsome. destruct (Z.leb_spec rb rt) as [Hle|Hgt].
        -- inversion Hsome; subst iw rw; clear Hsome.
           destruct (IH (S base) it rt Hrest) as (_ & _ & Hmin & _).
           repeat split; try lia; try exact Hbase.
           intros j r Hj Hlj. destruct (decide (j = base)) as [->|Hne].
           ++ rewrite Hbase in Hlj. inversion Hlj; lia.
           ++ pose proof (Hmin j r ltac:(lia) Hlj). lia.
        -- inversion Hsome; subst iw rw; clear Hsome.
           destruct (IH (S base) it rt Hrest) as (Hbounds & Hlt & Hmin & Hstrict).
           repeat split; try lia; try exact Hlt.
           ++ intros j r Hj Hlj. destruct (decide (j = base)) as [->|Hne].
              ** rewrite Hbase in Hlj. inversion Hlj; lia.
              ** apply (Hmin j r ltac:(lia) Hlj).
           ++ intros j r Hj Hlj. destruct (decide (j = base)) as [->|Hne].
              ** rewrite Hbase in Hlj. inversion Hlj; lia.
              ** apply (Hstrict j r ltac:(lia) Hlj).
      * cbn [pick_winner] in Hsome. inversion Hsome; subst iw rw; clear Hsome.
        pose proof (extreme_scan_none_spec len' (S base) leaves Hrest) as Hallnone.
        repeat split; try lia; try exact Hbase.
        intros j r Hj Hlj. destruct (decide (j = base)) as [->|Hne].
        -- rewrite Hbase in Hlj. inversion Hlj; lia.
        -- rewrite (Hallnone j ltac:(lia)) in Hlj. discriminate.
    + cbn [pick_winner] in Hsome.
      destruct (IH (S base) iw rw Hsome) as (Hbounds & Hlw & Hmin & Hstrict).
      repeat split; try lia; try exact Hlw.
      * intros j r Hj Hlj. destruct (decide (j = base)) as [->|Hne].
        -- rewrite Hbase in Hlj. discriminate.
        -- apply (Hmin j r ltac:(lia) Hlj).
      * intros j r Hj Hlj. destruct (decide (j = base)) as [->|Hne].
        -- rewrite Hbase in Hlj. discriminate.
        -- apply (Hstrict j r ltac:(lia) Hlj).
Qed.

(**md
**Bottom-up tournament build finds an optimal rank at the earliest `Keys`-order slot.**

For any tree height `h`, base slot index `base`, and optional slot ranks `leaves`:

- `tour_winner (build_tour h base leaves)` equals the left-to-right `extreme_scan` over
  `[base, base + 2^h)`, and
- if it returns `Some (i_win, r_win)`, then `i_win` is a settled slot in `[base, base + 2^h)`
  with rank `r_win`, `(r_win <= r)%Z` for every settled slot in `[base, base + 2^h)`, and
  strictly `(r_win < r)%Z` for every settled slot strictly before `i_win`.
*)
Theorem tournament_winner_optimal_and_first (h base : nat) (leaves : nat -> option Z) :
  tour_winner (build_tour h base leaves) = extreme_scan base (Nat.pow 2 h) leaves /\
  forall i_win r_win,
    tour_winner (build_tour h base leaves) = Some (i_win, r_win) ->
    base <= i_win < base + Nat.pow 2 h /\
    leaves i_win = Some r_win /\
    (forall j r, base <= j < base + Nat.pow 2 h -> leaves j = Some r -> (r_win <= r)%Z) /\
    (forall j r, base <= j < i_win -> leaves j = Some r -> (r_win < r)%Z).
Proof.
  rewrite tour_winner_build_eq_scan. split.
  - reflexivity.
  - intros iw rw Hsome. apply extreme_scan_some_spec; exact Hsome.
Qed.

Inductive id_tree :=
| ILeaf (wid : option nat)
| INode (wid : option nat) (left right : id_tree).

Definition id_winner (t : id_tree) : option nat :=
  match t with
  | ILeaf w => w
  | INode w _ _ => w
  end.

Definition eval_id (leaves : nat -> option Z) (w : option nat) : tour_entry :=
  match w with
  | None => None
  | Some i =>
      match leaves i with
      | Some r => Some (i, r)
      | None => None
      end
  end.

Definition pick_id (leaves : nat -> option Z) (a b : option nat) : option nat :=
  fst <$> pick_winner (eval_id leaves a) (eval_id leaves b).

Lemma pick_id_source (leaves : nat -> option Z) (a b : option nat) (nw : nat) :
  pick_id leaves a b = Some nw -> a = Some nw \/ b = Some nw.
Proof.
  unfold pick_id, eval_id.
  destruct a as [ia|], b as [ib|];
    try destruct (leaves ia) as [ra|]; try destruct (leaves ib) as [rb|];
    cbn [pick_winner]; try destruct (Z.leb ra rb);
    cbn [fmap option_fmap option_map fst];
    intro H; inversion H; auto.
Qed.

Fixpoint build_id_tree (h base : nat) (leaves : nat -> option Z) : id_tree :=
  match h with
  | 0 => ILeaf (match leaves base with Some _ => Some base | None => None end)
  | S h' =>
      let l := build_id_tree h' base leaves in
      let r := build_id_tree h' (base + Nat.pow 2 h') leaves in
      INode (pick_id leaves (id_winner l) (id_winner r)) l r
  end.

Lemma eval_build_id_tree (h base : nat) (leaves : nat -> option Z) :
  eval_id leaves (id_winner (build_id_tree h base leaves)) =
  tour_winner (build_tour h base leaves) /\
  forall iw, id_winner (build_id_tree h base leaves) = Some iw ->
    base <= iw < base + Nat.pow 2 h /\ exists r, leaves iw = Some r.
Proof.
  revert base. induction h as [|h' IH]; intro base.
  - cbn [build_id_tree build_tour id_winner tour_winner Nat.pow].
    destruct (leaves base) as [r|] eqn:Hb; cbn [eval_id]; split.
    + rewrite Hb. reflexivity.
    + intros iw Hw. inversion Hw; subst iw. split; [lia | exists r; exact Hb].
    + reflexivity.
    + intros iw Hw. discriminate.
  - cbn [build_id_tree build_tour id_winner tour_winner Nat.pow Nat.mul].
    rewrite Nat.add_0_r.
    destruct (IH base) as [IHl Hlbound].
    destruct (IH (base + Nat.pow 2 h')) as [IHr Hrbound].
    set (wl := id_winner (build_id_tree h' base leaves)) in *.
    set (wr := id_winner (build_id_tree h' (base + Nat.pow 2 h') leaves)) in *.
    unfold pick_id.
    destruct wl as [il|] eqn:Hwl; destruct wr as [ir|] eqn:Hwr.
    + destruct (Hlbound il eq_refl) as (Hlb & rl & Hrl).
      destruct (Hrbound ir eq_refl) as (Hrb & rr & Hrr).
      cbn [eval_id] in *. rewrite Hrl in *. rewrite Hrr in *.
      rewrite <- IHl, <- IHr. cbn [pick_winner].
      destruct (Z.leb rl rr); cbn [fmap option_fmap option_map fst eval_id]; split.
      * rewrite Hrl. reflexivity.
      * intros iw Hw. inversion Hw; subst iw. split; [lia | exists rl; exact Hrl].
      * rewrite Hrr. reflexivity.
      * intros iw Hw. inversion Hw; subst iw. split; [lia | exists rr; exact Hrr].
    + destruct (Hlbound il eq_refl) as (Hlb & rl & Hrl).
      cbn [eval_id] in *. rewrite Hrl in *.
      rewrite <- IHl, <- IHr.
      cbn [pick_winner fmap option_fmap option_map fst eval_id].
      split; [rewrite Hrl; reflexivity | intros iw Hw; inversion Hw; subst iw; split; [lia | exists rl; exact Hrl]].
    + destruct (Hrbound ir eq_refl) as (Hrb & rr & Hrr).
      cbn [eval_id] in *. rewrite Hrr in *.
      rewrite <- IHl, <- IHr.
      cbn [pick_winner fmap option_fmap option_map fst eval_id].
      split; [rewrite Hrr; reflexivity | intros iw Hw; inversion Hw; subst iw; split; [lia | exists rr; exact Hrr]].
    + rewrite <- IHl, <- IHr. cbn [eval_id pick_winner fmap option_fmap option_map fst].
      split; [reflexivity | intros iw Hw; discriminate].
Qed.

Lemma build_id_tree_ext (h base : nat) (l1 l2 : nat -> option Z) :
  (forall j, base <= j < base + Nat.pow 2 h -> l1 j = l2 j) ->
  build_id_tree h base l1 = build_id_tree h base l2.
Proof.
  revert base. induction h as [|h' IH]; intros base Hext.
  - cbn [build_id_tree]. rewrite (Hext base).
    + reflexivity.
    + cbn [Nat.pow]. lia.
  - cbn [build_id_tree Nat.pow Nat.mul] in *. rewrite Nat.add_0_r in *.
    rewrite (IH base (fun j Hj => Hext j ltac:(lia))).
    rewrite (IH (base + Nat.pow 2 h') (fun j Hj => Hext j ltac:(lia))).
    set (l := build_id_tree h' base l2).
    set (r := build_id_tree h' (base + Nat.pow 2 h') l2).
    f_equal.
    unfold pick_id, eval_id.
    (destruct (id_winner l) as [il|] eqn:Hil;
      [ destruct (proj2 (eval_build_id_tree h' base l2) il Hil) as [Hbl _];
        rewrite (Hext il ltac:(lia)) | ]);
    (destruct (id_winner r) as [ir|] eqn:Hir;
      [ destruct (proj2 (eval_build_id_tree h' (base + Nat.pow 2 h') l2) ir Hir) as [Hbr _];
        rewrite (Hext ir ltac:(lia)) | ]);
    reflexivity.
Qed.

Lemma build_id_tree_upper_none (h base : nat) (leaves : nat -> option Z) :
  (forall j, base <= j < base + Nat.pow 2 h -> leaves j = None) ->
  id_winner (build_id_tree h base leaves) = None.
Proof.
  intro Hall.
  destruct (id_winner (build_id_tree h base leaves)) as [iw|] eqn:Hw; [|reflexivity].
  destruct (proj2 (eval_build_id_tree h base leaves) iw Hw) as (Hb & r & Hrw).
  rewrite (Hall iw Hb) in Hrw. discriminate.
Qed.

Lemma build_id_tree_double_cap (h base : nat) (leaves : nat -> option Z) :
  (forall j, base + Nat.pow 2 h <= j < base + Nat.pow 2 (S h) -> leaves j = None) ->
  id_winner (build_id_tree (S h) base leaves) = id_winner (build_id_tree h base leaves).
Proof.
  intro Hupper.
  simpl build_id_tree. cbn [id_winner].
  rewrite (build_id_tree_upper_none h (base + Nat.pow 2 h) leaves).
  - unfold pick_id. cbn [eval_id]. rewrite pick_winner_none_r.
    destruct (id_winner (build_id_tree h base leaves)) as [iw|] eqn:Hw.
    + destruct (proj2 (eval_build_id_tree h base leaves) iw Hw) as (_ & r & Hrw).
      unfold eval_id. rewrite Hrw. reflexivity.
    + reflexivity.
  - intros j Hj. apply Hupper. cbn [Nat.pow Nat.mul] in *. lia.
Qed.

Definition option_nat_eqb (a b : option nat) : bool :=
  match a, b with
  | None, None => true
  | Some x, Some y => Nat.eqb x y
  | _, _ => false
  end.

Fixpoint sift_id_step (h base target : nat) (leaves' : nat -> option Z) (t : id_tree) : id_tree * bool :=
  match h, t with
  | 0, ILeaf _ =>
      (ILeaf (match leaves' target with Some _ => Some target | None => None end), true)
  | S h', INode oldW l r =>
      let mid := base + Nat.pow 2 h' in
      if Nat.ltb target mid then
        let '(l', cont) := sift_id_step h' base target leaves' l in
        if negb cont then
          (INode oldW l' r, false)
        else
          let nextW := pick_id leaves' (id_winner l') (id_winner r) in
          if option_nat_eqb oldW nextW then
            match nextW, id_winner l' with
            | Some nw, Some cw => (INode oldW l' r, Nat.eqb nw cw)
            | _, _ => (INode oldW l' r, false)
            end
          else
            (INode nextW l' r, true)
      else
        let '(r', cont) := sift_id_step h' mid target leaves' r in
        if negb cont then
          (INode oldW l r', false)
        else
          let nextW := pick_id leaves' (id_winner l) (id_winner r') in
          if option_nat_eqb oldW nextW then
            match nextW, id_winner r' with
            | Some nw, Some cw => (INode oldW l r', Nat.eqb nw cw)
            | _, _ => (INode oldW l r', false)
            end
          else
            (INode nextW l r', true)
  | _, _ => (t, false)
  end.

(**md
**Incremental `SiftUp` with early stop agrees bit-for-bit with a full tournament rebuild.**

Updating slot `target` in `[base, base + 2^h)` from `leaves` to `leaves'` (`v_new` at
`target`, `leaves` everywhere else) and running `sift_id_step` along the ancestor path of
`target` yields `build_id_tree h base leaves'`, preserves exact equality whenever the
early-stop condition fires (`cont = false` implies `nextW = oldW` and `nextW <> Some target`),
and shifts left-justified leaves under capacity doubling (`build_id_tree_double_cap`) with
zero comparisons.
*)
Theorem tournament_point_update_refines_eval
  (h base target : nat) (v_new : option Z) (leaves : nat -> option Z) :
  base <= target < base + Nat.pow 2 h ->
  let leaves' := fun j => if decide (j = target) then v_new else leaves j in
  let '(t', cont) := sift_id_step h base target leaves' (build_id_tree h base leaves) in
  t' = build_id_tree h base leaves' /\
  eval_id leaves' (id_winner t') = extreme_scan base (Nat.pow 2 h) leaves' /\
  (cont = false ->
     id_winner t' = id_winner (build_id_tree h base leaves) /\
     id_winner t' <> Some target).
Proof.
  intros Htarget leaves'.
  assert (Hmain :
    let '(t', cont) := sift_id_step h base target leaves' (build_id_tree h base leaves) in
    t' = build_id_tree h base leaves' /\
    (cont = false ->
       id_winner t' = id_winner (build_id_tree h base leaves) /\
       id_winner t' <> Some target)).
  { subst leaves'. revert base target Htarget.
    induction h as [|h' IH]; intros base target Htarget;
    set (leaves' := fun j => if decide (j = target) then v_new else leaves j).
    - cbn [Nat.pow] in Htarget.
      assert (Heq : target = base) by lia. subst target.
      cbn [build_id_tree sift_id_step].
      split; [reflexivity | discriminate].
    - cbn [Nat.pow Nat.mul] in Htarget. rewrite Nat.add_0_r in Htarget.
      remember (build_id_tree (S h') base leaves') as tree_new eqn:Htree_new.
      cbn [build_id_tree sift_id_step].
      subst tree_new.
      set (mid := base + Nat.pow 2 h') in *.
      set (l_old := build_id_tree h' base leaves) in *.
      set (r_old := build_id_tree h' mid leaves) in *.
      set (oldW := pick_id leaves (id_winner l_old) (id_winner r_old)) in *.
      destruct (Nat.ltb_spec target mid) as [Hleft|Hright].
      + pose proof (IH base target ltac:(lia)) as IHl.
        fold leaves' in IHl. fold l_old in IHl.
        destruct (sift_id_step h' base target leaves' l_old) as [l' contl] eqn:Hstep.
        destruct IHl as [Hl' Hcontl].
        assert (Hr_eq : r_old = build_id_tree h' mid leaves').
        { unfold r_old. apply build_id_tree_ext.
          intros j Hj. unfold leaves'. rewrite decide_False by lia. reflexivity. }
        destruct contl; cbn [negb].
        * remember (pick_id leaves' (id_winner l') (id_winner r_old)) as nextW eqn:HnextW.
          assert (Hbuild' : build_id_tree (S h') base leaves' =
                            INode nextW l' r_old).
          { simpl build_id_tree. fold mid. rewrite <- Hl', <- Hr_eq, <- HnextW. reflexivity. }
          destruct (option_nat_eqb oldW nextW) eqn:Heqw.
          -- assert (oldW = nextW) as Hweq.
             { destruct oldW as [x|], nextW as [y|]; cbn [option_nat_eqb] in Heqw;
                 try discriminate; [f_equal; apply Nat.eqb_eq; exact Heqw | reflexivity]. }
             destruct nextW as [nw|]; destruct (id_winner l') as [cw|] eqn:Hcw.
             ++ destruct (Nat.eqb_spec nw cw) as [->|Hne].
                ** split; [rewrite Hbuild', Hweq; reflexivity | discriminate].
                ** split; [rewrite Hbuild', Hweq; reflexivity |].
                   intro. cbn [id_winner]. split; [reflexivity |].
                   rewrite Hweq. symmetry in HnextW.
                   destruct (pick_id_source _ _ _ _ HnextW) as [Heql|Heqr].
                   --- inversion Heql; subst; contradiction.
                   --- destruct (proj2 (eval_build_id_tree h' mid leaves) nw Heqr) as [Hbr _].
                       intro Heqt; inversion Heqt; lia.
             ++ split; [rewrite Hbuild', Hweq; reflexivity |].
                intro. cbn [id_winner]. split; [reflexivity |].
                rewrite Hweq. symmetry in HnextW.
                destruct (pick_id_source _ _ _ _ HnextW) as [Heql|Heqr]; [discriminate |].
                destruct (proj2 (eval_build_id_tree h' mid leaves) nw Heqr) as [Hbr _].
                intro Heqt; inversion Heqt; lia.
             ++ split; [rewrite Hbuild', Hweq; reflexivity |].
                intro. cbn [id_winner]. split; [reflexivity | rewrite Hweq; discriminate].
             ++ split; [rewrite Hbuild', Hweq; reflexivity |].
                intro. cbn [id_winner]. split; [reflexivity | rewrite Hweq; discriminate].
          -- split; [rewrite Hbuild'; reflexivity | discriminate].
        * destruct (Hcontl eq_refl) as [Hwl_eq Hwl_nt].
          assert (Hpick_eq : pick_id leaves' (id_winner l') (id_winner r_old) = oldW).
          { unfold oldW, pick_id. rewrite Hwl_eq.
            assert (Heval_l : eval_id leaves' (id_winner l_old) = eval_id leaves (id_winner l_old)).
            { destruct (id_winner l_old) as [il|] eqn:Hil; [|reflexivity].
              assert (il <> target) by (intro Heq; apply Hwl_nt; rewrite Hwl_eq, Heq; reflexivity).
              unfold eval_id, leaves'. rewrite decide_False by assumption. reflexivity. }
            assert (Heval_r : eval_id leaves' (id_winner r_old) = eval_id leaves (id_winner r_old)).
            { destruct (id_winner r_old) as [ir|] eqn:Hir; [|reflexivity].
              destruct (proj2 (eval_build_id_tree h' mid leaves) ir Hir) as [Hbr _].
              unfold eval_id, leaves'. rewrite decide_False by lia. reflexivity. }
            rewrite Heval_l, Heval_r. reflexivity. }
          assert (Hold_nt : oldW <> Some target).
          { intro Heqt. unfold oldW in Heqt.
            destruct (pick_id_source _ _ _ _ Heqt) as [Heql|Heqr].
            - apply Hwl_nt. rewrite Hwl_eq. exact Heql.
            - destruct (proj2 (eval_build_id_tree h' mid leaves) target Heqr) as [Hbr _]. lia. }
          split.
          -- simpl build_id_tree. fold mid. rewrite <- Hl', <- Hr_eq, Hpick_eq. reflexivity.
          -- intro. cbn [id_winner]. split; [reflexivity | exact Hold_nt].
      + pose proof (IH mid target ltac:(lia)) as IHr.
        fold leaves' in IHr. fold r_old in IHr.
        destruct (sift_id_step h' mid target leaves' r_old) as [r' contr] eqn:Hstep.
        destruct IHr as [Hr' Hcontr].
        assert (Hl_ext : l_old = build_id_tree h' base leaves').
        { unfold l_old. apply build_id_tree_ext.
          intros j Hj. unfold leaves'. rewrite decide_False by lia. reflexivity. }
        destruct contr; cbn [negb].
        * remember (pick_id leaves' (id_winner l_old) (id_winner r')) as nextW eqn:HnextW.
          assert (Hbuild' : build_id_tree (S h') base leaves' =
                            INode nextW l_old r').
          { simpl build_id_tree. fold mid. rewrite <- Hl_ext, <- Hr', <- HnextW. reflexivity. }
          destruct (option_nat_eqb oldW nextW) eqn:Heqw.
          -- assert (oldW = nextW) as Hweq.
             { destruct oldW as [x|], nextW as [y|]; cbn [option_nat_eqb] in Heqw;
                 try discriminate; [f_equal; apply Nat.eqb_eq; exact Heqw | reflexivity]. }
             destruct nextW as [nw|]; destruct (id_winner r') as [cw|] eqn:Hcw.
             ++ destruct (Nat.eqb_spec nw cw) as [->|Hne].
                ** split; [rewrite Hbuild', Hweq; reflexivity | discriminate].
                ** split; [rewrite Hbuild', Hweq; reflexivity |].
                   intro. cbn [id_winner]. split; [reflexivity |].
                   rewrite Hweq. symmetry in HnextW.
                   destruct (pick_id_source _ _ _ _ HnextW) as [Heql|Heqr].
                   --- destruct (proj2 (eval_build_id_tree h' base leaves) nw Heql) as [Hbl _].
                       intro Heqt; inversion Heqt; lia.
                   --- inversion Heqr; subst; contradiction.
             ++ split; [rewrite Hbuild', Hweq; reflexivity |].
                intro. cbn [id_winner]. split; [reflexivity |].
                rewrite Hweq. symmetry in HnextW.
                destruct (pick_id_source _ _ _ _ HnextW) as [Heql|Heqr]; [|discriminate].
                destruct (proj2 (eval_build_id_tree h' base leaves) nw Heql) as [Hbl _].
                intro Heqt; inversion Heqt; lia.
             ++ split; [rewrite Hbuild', Hweq; reflexivity |].
                intro. cbn [id_winner]. split; [reflexivity | rewrite Hweq; discriminate].
             ++ split; [rewrite Hbuild', Hweq; reflexivity |].
                intro. cbn [id_winner]. split; [reflexivity | rewrite Hweq; discriminate].
          -- split; [rewrite Hbuild'; reflexivity | discriminate].
        * destruct (Hcontr eq_refl) as [Hwr_eq Hwr_nt].
          assert (Hpick_eq : pick_id leaves' (id_winner l_old) (id_winner r') = oldW).
          { unfold oldW, pick_id. rewrite Hwr_eq.
            assert (Heval_l : eval_id leaves' (id_winner l_old) = eval_id leaves (id_winner l_old)).
            { destruct (id_winner l_old) as [il|] eqn:Hil; [|reflexivity].
              destruct (proj2 (eval_build_id_tree h' base leaves) il Hil) as [Hbl _].
              unfold eval_id, leaves'. rewrite decide_False by lia. reflexivity. }
            assert (Heval_r : eval_id leaves' (id_winner r_old) = eval_id leaves (id_winner r_old)).
            { destruct (id_winner r_old) as [ir|] eqn:Hir; [|reflexivity].
              assert (ir <> target) by (intro Heq; apply Hwr_nt; rewrite Hwr_eq, Heq; reflexivity).
              unfold eval_id, leaves'. rewrite decide_False by assumption. reflexivity. }
            rewrite Heval_l, Heval_r. reflexivity. }
          assert (Hold_nt : oldW <> Some target).
          { intro Heqt. unfold oldW in Heqt.
            destruct (pick_id_source _ _ _ _ Heqt) as [Heql|Heqr].
            - destruct (proj2 (eval_build_id_tree h' base leaves) target Heql) as [Hbl _]. lia.
            - apply Hwr_nt. rewrite Hwr_eq. exact Heqr. }
          split.
          -- simpl build_id_tree. fold mid. rewrite <- Hl_ext, <- Hr', Hpick_eq. reflexivity.
          -- intro. cbn [id_winner]. split; [reflexivity | exact Hold_nt]. }
  destruct (sift_id_step h base target leaves' (build_id_tree h base leaves)) as [t' cont] eqn:Hsift.
  destruct Hmain as [Ht' Hcont].
  split; [exact Ht' | split; [| exact Hcont]].
  rewrite Ht'.
  rewrite (proj1 (eval_build_id_tree h base leaves')).
  rewrite tour_winner_build_eq_scan. reflexivity.
Qed.

Definition sticky_select (sticky_prev : option nat) (leaves : nat -> option Z) (top : tour_entry) : tour_entry :=
  match top with
  | None => None
  | Some (iw, rw) =>
      match sticky_prev with
      | Some ks =>
          match leaves ks with
          | Some rs => if Z.leb rs rw then Some (ks, rs) else Some (iw, rw)
          | None => Some (iw, rw)
          end
      | None => Some (iw, rw)
      end
  end.

Definition pick_sticky (sticky_prev : option nat) (a b : tour_entry) : tour_entry :=
  match a, b with
  | None, _ => b
  | Some _, None => a
  | Some (ia, ra), Some (ib, rb) =>
      if Z.ltb rb ra || match sticky_prev with Some ks => Nat.eqb ib ks && Z.leb rb ra | None => false end
      then Some (ib, rb)
      else Some (ia, ra)
  end.

Lemma pick_sticky_none_r (sticky_prev : option nat) (a : tour_entry) :
  pick_sticky sticky_prev a None = a.
Proof.
  destruct a as [[ia ra]|]; reflexivity.
Qed.

Lemma pick_sticky_assoc (sticky_prev : option nat) (a b c : tour_entry) :
  pick_sticky sticky_prev (pick_sticky sticky_prev a b) c =
  pick_sticky sticky_prev a (pick_sticky sticky_prev b c).
Proof.
  destruct a as [[ia ra]|], b as [[ib rb]|], c as [[ic rc]|];
    destruct sticky_prev as [ks|]; cbn [pick_sticky];
    repeat match goal with
    | |- context [Nat.eqb ?x ?y] => destruct (Nat.eqb_spec x y); subst; cbn [andb orb pick_sticky]
    | |- context [Z.ltb ?x ?y] => destruct (Z.ltb_spec x y); cbn [andb orb pick_sticky]
    | |- context [Z.leb ?x ?y] => destruct (Z.leb_spec x y); cbn [andb orb pick_sticky]
    end; try reflexivity; lia.
Qed.

Fixpoint sticky_extreme_scan
  (sticky_prev : option nat) (base len : nat) (leaves : nat -> option Z) (acc : tour_entry) : tour_entry :=
  match len with
  | 0 => acc
  | S len' =>
      let here := match leaves base with Some r => Some (base, r) | None => None end in
      sticky_extreme_scan sticky_prev (S base) len' leaves (pick_sticky sticky_prev acc here)
  end.

Fixpoint sticky_scan_right
  (sticky_prev : option nat) (base len : nat) (leaves : nat -> option Z) : tour_entry :=
  match len with
  | 0 => None
  | S len' =>
      let here := match leaves base with Some r => Some (base, r) | None => None end in
      pick_sticky sticky_prev here (sticky_scan_right sticky_prev (S base) len' leaves)
  end.

Lemma sticky_extreme_scan_eq_right
  (sticky_prev : option nat) (len base : nat) (leaves : nat -> option Z) (acc : tour_entry) :
  sticky_extreme_scan sticky_prev base len leaves acc =
  pick_sticky sticky_prev acc (sticky_scan_right sticky_prev base len leaves).
Proof.
  revert base acc. induction len as [|len' IH]; intros base acc.
  - cbn [sticky_extreme_scan sticky_scan_right]. rewrite pick_sticky_none_r. reflexivity.
  - cbn [sticky_extreme_scan sticky_scan_right].
    rewrite IH, pick_sticky_assoc. reflexivity.
Qed.

Lemma sticky_scan_right_rel
  (sticky_prev : option nat) (len base : nat) (leaves : nat -> option Z) :
  sticky_scan_right sticky_prev base len leaves =
  match extreme_scan base len leaves with
  | None => None
  | Some (iw, rw) =>
      match sticky_prev with
      | Some ks =>
          if decide (base <= ks < base + len) then
            match leaves ks with
            | Some rs => if Z.leb rs rw then Some (ks, rs) else Some (iw, rw)
            | None => Some (iw, rw)
            end
          else Some (iw, rw)
      | None => Some (iw, rw)
      end
  end.
Proof.
  revert base. induction len as [|len' IH]; intro base.
  - cbn [sticky_scan_right extreme_scan]. reflexivity.
  - cbn [sticky_scan_right extreme_scan].
    rewrite IH.
    destruct (leaves base) as [rb|] eqn:Hbase.
    + destruct (extreme_scan (S base) len' leaves) as [[it rt]|] eqn:Hrest.
      * destruct (extreme_scan_some_spec len' (S base) it rt leaves Hrest) as (Hitb & Hlit & Hmin & _).
        cbn [pick_winner].
        destruct sticky_prev as [ks|].
        -- destruct (decide (ks = base)) as [->|Hneb].
           ++ rewrite decide_False by lia.
              destruct (Z.leb_spec rb rt); rewrite decide_True by lia; rewrite Hbase;
                cbn [pick_sticky];
                rewrite (proj2 (Nat.eqb_neq it base) ltac:(lia)); cbn [andb orb];
                destruct (Z.ltb_spec rt rb), (Z.leb_spec rb rt); try rewrite Z.leb_refl; try reflexivity; lia.
           ++ destruct (decide (S base <= ks < S base + len')) as [Hin|Hout].
              ** destruct (Z.leb_spec rb rt); rewrite decide_True by lia;
                   destruct (leaves ks) as [rs|] eqn:Hks.
                 --- pose proof (Hmin ks rs Hin Hks) as Hrt_le_rs.
                     destruct (Z.leb_spec rs rt); cbn [pick_sticky].
                     +++ rewrite Nat.eqb_refl; cbn [andb].
                         destruct (Z.ltb_spec rs rb), (Z.leb_spec rs rb); cbn [orb]; try reflexivity; lia.
                     +++ destruct (Nat.eqb_spec it ks) as [->|]; [rewrite Hks in Hlit; inversion Hlit; lia |].
                         cbn [andb orb].
                         destruct (Z.ltb_spec rt rb), (Z.leb_spec rs rb); try reflexivity; lia.
                 --- cbn [pick_sticky].
                     destruct (Nat.eqb_spec it ks) as [->|]; [rewrite Hks in Hlit; discriminate |].
                     cbn [andb orb].
                     destruct (Z.ltb_spec rt rb); [lia | reflexivity].
                 --- pose proof (Hmin ks rs Hin Hks) as Hrt_le_rs.
                     destruct (Z.leb_spec rs rt); cbn [pick_sticky].
                     +++ rewrite Nat.eqb_refl; cbn [andb].
                         destruct (Z.ltb_spec rs rb), (Z.leb_spec rs rb); cbn [orb]; try reflexivity; lia.
                     +++ destruct (Nat.eqb_spec it ks) as [->|]; [rewrite Hks in Hlit; inversion Hlit; lia |].
                         cbn [andb orb].
                         destruct (Z.ltb_spec rt rb); [reflexivity | lia].
                 --- cbn [pick_sticky].
                     destruct (Nat.eqb_spec it ks) as [->|]; [rewrite Hks in Hlit; discriminate |].
                     cbn [andb orb].
                     destruct (Z.ltb_spec rt rb); [reflexivity | lia].
              ** destruct (Z.leb_spec rb rt); rewrite decide_False by lia;
                   cbn [pick_sticky];
                   rewrite (proj2 (Nat.eqb_neq it ks) ltac:(lia)); cbn [andb orb];
                   destruct (Z.ltb_spec rt rb); try reflexivity; lia.
        -- destruct (Z.leb_spec rb rt); cbn [pick_sticky];
             destruct (Z.ltb_spec rt rb); try reflexivity; lia.
      * cbn [pick_sticky pick_winner].
        destruct sticky_prev as [ks|]; [|reflexivity].
        destruct (decide (ks = base)) as [->|Hne].
        -- rewrite decide_True by lia. rewrite Hbase, Z.leb_refl. reflexivity.
        -- destruct (decide (base <= ks < base + S len')) as [Hin|]; [|reflexivity].
           pose proof (extreme_scan_none_spec len' (S base) leaves Hrest ks ltac:(lia)) as Hks_none.
           rewrite Hks_none. reflexivity.
    + cbn [pick_sticky pick_winner].
      destruct (extreme_scan (S base) len' leaves) as [[it rt]|] eqn:Hrest; [|reflexivity].
      destruct sticky_prev as [ks|]; [|reflexivity].
      destruct (decide (ks = base)) as [->|Hne].
      * rewrite decide_False by lia. rewrite decide_True by lia. rewrite Hbase. reflexivity.
      * destruct (decide (S base <= ks < S base + len'));
          [rewrite decide_True by lia | rewrite decide_False by lia]; reflexivity.
Qed.

(**md
**Winner-tree selection with `sticky_select` refines sequential `sticky_extreme_scan`.**

For any tree height `h` with valid slot bounds (`None` or `< 2^h`) for the previous sticky
holder `sticky_prev`: resolving `sticky_select` against `tour_winner (build_tour h 0 leaves)`
yields the exact same key and rank (including sticky tie preservation when the previous holder
remains tied for best rank) as a full `Keys`-order scan `sticky_extreme_scan`.
*)
Theorem sticky_holder_refines_extreme_scan
  (h : nat) (sticky_prev : option nat) (leaves : nat -> option Z) :
  (forall ks, sticky_prev = Some ks -> ks < Nat.pow 2 h) ->
  sticky_select sticky_prev leaves (tour_winner (build_tour h 0 leaves)) =
  sticky_extreme_scan sticky_prev 0 (Nat.pow 2 h) leaves None.
Proof.
  intro Hbound.
  rewrite sticky_extreme_scan_eq_right. cbn [pick_sticky].
  rewrite tour_winner_build_eq_scan, sticky_scan_right_rel.
  destruct (extreme_scan 0 (Nat.pow 2 h) leaves) as [[iw rw]|]; [|reflexivity].
  destruct sticky_prev as [ks|]; [|reflexivity].
  rewrite decide_True by (split; [lia | apply Hbound; reflexivity]).
  reflexivity.
Qed.

(** ** Lazy partitioned KeyMap, aligned Trim, dense IndexProjection, and Lookup sync *)

Record lazy_part_map (K V : Type) `{Countable K} := mk_lpm {
  lpm_prims : gmap K V;
  lpm_objs : option (gmap K V);
}.
Arguments mk_lpm {K V _ _} _ _.
Arguments lpm_prims {K V _ _} _.
Arguments lpm_objs {K V _ _} _.

Definition lpm_empty {K V : Type} `{Countable K} : lazy_part_map K V :=
  mk_lpm ∅ None.

Definition lpm_abs {K V : Type} `{Countable K} (m : lazy_part_map K V) : gmap K V :=
  lpm_prims m ∪ default ∅ (lpm_objs m).

Definition lpm_wf {K V : Type} `{Countable K} (is_prim : K -> bool) (m : lazy_part_map K V) : Prop :=
  (forall k, is_Some (lpm_prims m !! k) -> is_prim k = true) /\
  match lpm_objs m with
  | None => True
  | Some om => forall k, is_Some (om !! k) -> is_prim k = false
  end.

Definition lpm_find {K V : Type} `{Countable K} (is_prim : K -> bool) (k : K) (m : lazy_part_map K V) : option V :=
  if is_prim k then lpm_prims m !! k
  else match lpm_objs m with None => None | Some om => om !! k end.

Definition lpm_set {K V : Type} `{Countable K} (is_prim : K -> bool) (k : K) (v : V) (m : lazy_part_map K V)
    : lazy_part_map K V :=
  if is_prim k then
    mk_lpm (<[k := v]> (lpm_prims m)) (lpm_objs m)
  else
    let om := match lpm_objs m with None => ∅ | Some om => om end in
    mk_lpm (lpm_prims m) (Some (<[k := v]> om)).

Definition lpm_remove {K V : Type} `{Countable K} (is_prim : K -> bool) (k : K) (m : lazy_part_map K V)
    : lazy_part_map K V :=
  if is_prim k then
    mk_lpm (delete k (lpm_prims m)) (lpm_objs m)
  else
    match lpm_objs m with
    | None => m
    | Some om => mk_lpm (lpm_prims m) (Some (delete k om))
    end.

Definition lpm_count {K V : Type} `{Countable K} (m : lazy_part_map K V) : nat :=
  size (lpm_prims m) + match lpm_objs m with None => 0 | Some om => size om end.

Lemma lpm_prims_none_on_obj {K V : Type} `{Countable K} (is_prim : K -> bool) (m : lazy_part_map K V) (k : K) :
  lpm_wf is_prim m -> is_prim k = false -> lpm_prims m !! k = None.
Proof.
  intros [Hp _] Hfalse.
  destruct (lpm_prims m !! k) as [v|] eqn:E; [|reflexivity].
  pose proof (Hp k (ex_intro _ v E)) as Htrue. congruence.
Qed.

Lemma lpm_objs_none_on_prim {K V : Type} `{Countable K} (is_prim : K -> bool) (m : lazy_part_map K V) (k : K) :
  lpm_wf is_prim m -> is_prim k = true -> default (∅ : gmap K V) (lpm_objs m) !! k = None.
Proof.
  intros [_ Ho] Htrue.
  destruct (lpm_objs m) as [om|]; simpl; [|apply lookup_empty].
  destruct (om !! k) as [v|] eqn:E; [|reflexivity].
  pose proof (Ho k (ex_intro _ v E)) as Hfalse. congruence.
Qed.

(**md
**A lazily allocated partitioned map `(prims, option objs)` starting at `(∅, None)` and allocating `objs` only on the first non-primitive insert refines a total `gmap K V` across `Find`, `Set`, `Remove`, and `Count` (`Platform.fs`).**

- [is_prim:] predicate distinguishing primitive keys stored in `prims` from non-primitive keys stored in `objs`.
- [m:] the partitioned map state.
*)
Theorem lazy_partitioned_map_refines_total_map
    {K V : Type} `{Countable K} (is_prim : K -> bool) (m : lazy_part_map K V) :
  lpm_wf is_prim (lpm_empty (K := K) (V := V)) /\
  lpm_abs (lpm_empty (K := K) (V := V)) = ∅ /\
  lpm_objs (lpm_empty (K := K) (V := V)) = None /\
  (lpm_wf is_prim m ->
   (forall k v, is_prim k = true -> lpm_objs m = None -> lpm_objs (lpm_set is_prim k v m) = None) /\
   (forall k, lpm_find is_prim k m = lpm_abs m !! k) /\
   (forall k v, lpm_wf is_prim (lpm_set is_prim k v m) /\ lpm_abs (lpm_set is_prim k v m) = <[k := v]> (lpm_abs m)) /\
   (forall k, lpm_wf is_prim (lpm_remove is_prim k m) /\ lpm_abs (lpm_remove is_prim k m) = delete k (lpm_abs m)) /\
   lpm_count m = size (lpm_abs m)).
Proof.
  split_and!.
  - unfold lpm_wf; simpl. split; [|done].
    intros k [v Hv]. rewrite lookup_empty in Hv. discriminate.
  - unfold lpm_abs, lpm_empty; simpl.
    apply map_eq. intros q. rewrite lookup_union, !lookup_empty. reflexivity.
  - reflexivity.
  - intros Hwf. split_and!.
    + intros k v Htrue Hnone. unfold lpm_set. rewrite Htrue. simpl. exact Hnone.
    + intros k. unfold lpm_find, lpm_abs. rewrite lookup_union.
      destruct (is_prim k) eqn:Hp.
      * rewrite (lpm_objs_none_on_prim is_prim m k Hwf Hp).
        destruct (lpm_prims m !! k); reflexivity.
      * rewrite (lpm_prims_none_on_obj is_prim m k Hwf Hp).
        destruct (lpm_objs m) as [om|]; simpl; [destruct (om !! k); reflexivity | rewrite lookup_empty; reflexivity].
    + intros k v. unfold lpm_set, lpm_wf, lpm_abs.
      destruct (is_prim k) eqn:Hp; simpl.
      * destruct Hwf as [Hprims Hobjs].
        assert (Hok : default (∅ : gmap K V) (lpm_objs m) !! k = None)
          by (apply (lpm_objs_none_on_prim is_prim m k (conj Hprims Hobjs) Hp)).
        split.
        -- split; [|exact Hobjs].
           intros q [vq Hvq]. destruct (decide (q = k)) as [->|Hne]; [exact Hp|].
           rewrite lookup_insert_ne in Hvq; [|symmetry; exact Hne].
           apply Hprims. eexists; exact Hvq.
        -- apply map_eq. intros q. rewrite !lookup_union.
           destruct (decide (q = k)) as [->|Hne].
           ++ rewrite !lookup_insert_eq, Hok. reflexivity.
           ++ rewrite !lookup_insert_ne; [|symmetry; exact Hne | symmetry; exact Hne].
              rewrite lookup_union. reflexivity.
      * destruct Hwf as [Hprims Hobjs].
        assert (Hpk : lpm_prims m !! k = None) by (apply (lpm_prims_none_on_obj is_prim m k (conj Hprims Hobjs) Hp)).
        split.
        -- split; [exact Hprims|].
           intros q [vq Hvq]. destruct (decide (q = k)) as [->|Hne]; [exact Hp|].
           rewrite lookup_insert_ne in Hvq; [|symmetry; exact Hne].
           destruct (lpm_objs m) as [om|]; simpl in *.
           ++ apply Hobjs. eexists; exact Hvq.
           ++ rewrite lookup_empty in Hvq. discriminate.
        -- destruct (lpm_objs m) as [om|] eqn:Eo; simpl; apply map_eq; intros q; rewrite !lookup_union;
             destruct (decide (q = k)) as [->|Hne].
           ++ rewrite Hpk, !lookup_insert_eq. reflexivity.
           ++ rewrite !lookup_insert_ne; [|symmetry; exact Hne | symmetry; exact Hne].
              rewrite lookup_union. reflexivity.
           ++ rewrite Hpk, !lookup_insert_eq. reflexivity.
           ++ rewrite !lookup_insert_ne; [|symmetry; exact Hne | symmetry; exact Hne].
              rewrite lookup_union, !lookup_empty. reflexivity.
    + intros k. unfold lpm_remove, lpm_wf, lpm_abs.
      destruct (is_prim k) eqn:Hp; simpl.
      * destruct Hwf as [Hprims Hobjs].
        assert (Hok : default (∅ : gmap K V) (lpm_objs m) !! k = None)
          by (apply (lpm_objs_none_on_prim is_prim m k (conj Hprims Hobjs) Hp)).
        split.
        -- split; [|exact Hobjs].
           intros q [vq Hvq]. destruct (decide (q = k)) as [->|Hne].
           ++ rewrite lookup_delete_eq in Hvq. discriminate.
           ++ rewrite lookup_delete_ne in Hvq; [|symmetry; exact Hne].
              apply Hprims. eexists; exact Hvq.
        -- apply map_eq. intros q. rewrite !lookup_union.
           destruct (decide (q = k)) as [->|Hne].
           ++ rewrite !lookup_delete_eq, Hok. reflexivity.
           ++ rewrite !lookup_delete_ne; [|symmetry; exact Hne | symmetry; exact Hne].
              rewrite lookup_union. reflexivity.
      * destruct Hwf as [Hprims Hobjs].
        assert (Hpk : lpm_prims m !! k = None) by (apply (lpm_prims_none_on_obj is_prim m k (conj Hprims Hobjs) Hp)).
        destruct (lpm_objs m) as [om|] eqn:Eo; simpl.
        -- split.
           ++ split; [exact Hprims|].
              intros q [vq Hvq]. destruct (decide (q = k)) as [->|Hne].
              ** rewrite lookup_delete_eq in Hvq. discriminate.
              ** rewrite lookup_delete_ne in Hvq; [|symmetry; exact Hne].
                 apply Hobjs. eexists; exact Hvq.
           ++ apply map_eq. intros q. rewrite !lookup_union.
              destruct (decide (q = k)) as [->|Hne].
              ** rewrite !lookup_delete_eq, Hpk. reflexivity.
              ** rewrite !lookup_delete_ne; [|symmetry; exact Hne | symmetry; exact Hne].
                 rewrite lookup_union. reflexivity.
        -- split; [split; [exact Hprims | rewrite Eo; exact I] |].
           rewrite Eo. simpl.
           apply map_eq. intros q.
           destruct (decide (q = k)) as [->|Hne].
           ++ rewrite lookup_delete_eq, lookup_union, Hpk, lookup_empty. reflexivity.
           ++ rewrite lookup_delete_ne; [reflexivity | symmetry; exact Hne].
    + unfold lpm_count, lpm_abs.
      destruct (lpm_objs m) as [om|] eqn:Eo; simpl.
      * rewrite <- !size_dom, dom_union_L, fin_sets.size_union; [reflexivity|].
        intros q Hqp%elem_of_dom Hqo%elem_of_dom.
        destruct Hwf as [Hprims Hobjs]. rewrite Eo in Hobjs.
        pose proof (Hprims q Hqp) as Ht. pose proof (Hobjs q Hqo) as Hf. congruence.
      * assert (Hum : lpm_prims m ∪ (∅ : gmap K V) = lpm_prims m)
          by (apply map_eq; intros q; rewrite lookup_union, lookup_empty; destruct (lpm_prims m !! q); reflexivity).
        rewrite Hum. lia.
Qed.

Definition trim_single_removed {K : Type} `{EqDecision K} (pk k : K) : list K :=
  if decide (k = pk) then [] else [pk].

(**md
**In a `NoDup` key array `prevKeys` where slot `idx` holds `pk`, single-index `Trim` replacement preserves `prevKeys` with `removed = []` when `k = pk`, preserves `NoDup (<[idx := k]> prevKeys)` if and only if `(k <> pk -> k ∉ prevKeys)`, and drops precisely `trim_single_removed pk k` (`Projections.fs`).**

- [prevKeys:] the previous `NoDup` published key sequence.
- [idx:] the unique changed item index (`changedAt.Count = 1`).
- [pk:] the previous key at index `idx`.
- [k:] the recomputed key at index `idx`.
*)
Theorem trim_single_inplace_key_preserves_uniqueness
    {K : Type} `{EqDecision K} (prevKeys : list K) (idx : nat) (pk k : K) :
  NoDup prevKeys ->
  prevKeys !! idx = Some pk ->
  (k = pk -> <[idx := k]> prevKeys = prevKeys /\ trim_single_removed pk k = []) /\
  (NoDup (<[idx := k]> prevKeys) <-> (k <> pk -> k ∉ prevKeys)) /\
  (forall x, (x ∈ prevKeys /\ x ∉ <[idx := k]> prevKeys) <-> x ∈ trim_single_removed pk k).
Proof.
  intros Hnd Hidx.
  pose proof (lookup_lt_Some _ _ _ Hidx) as Hlt.
  pose proof (take_drop_middle prevKeys idx pk Hidx) as Hmid.
  set (L := take idx prevKeys) in *.
  set (R := drop (S idx) prevKeys) in *.
  assert (HlenL : length L = idx) by (subst L; apply length_take_le; lia).
  assert (Hins : <[idx := k]> prevKeys = L ++ k :: R).
  { rewrite <- Hmid at 1. rewrite insert_app_r_alt; [|lia].
    rewrite HlenL, Nat.sub_diag. simpl. reflexivity. }
  assert (Hnd_decomp : NoDup L /\ (forall x, x ∈ L -> x ∉ pk :: R) /\ pk ∉ R /\ NoDup R).
  { rewrite <- Hmid in Hnd.
    apply NoDup_app in Hnd as (HndL & HdisjL & Hnd_rest).
    apply NoDup_cons in Hnd_rest as (HpkR & HndR).
    repeat split; assumption. }
  destruct Hnd_decomp as (HndL & HdisjL & HpkR & HndR).
  split_and!.
  - intros ->. rewrite Hins, <- Hmid.
    unfold trim_single_removed. rewrite decide_True by reflexivity. split; reflexivity.
  - rewrite Hins. split.
    + intros Hnd' Hne Hkin.
      apply NoDup_app in Hnd' as (_ & Hdisj' & Hnd_rest').
      apply NoDup_cons in Hnd_rest' as (HkR & _).
      rewrite <- Hmid in Hkin.
      apply elem_of_app in Hkin as [HkL | Hk_rest].
      * apply (Hdisj' k HkL). by left.
      * apply elem_of_cons in Hk_rest as [Heq | HkR']; [exact (Hne Heq) | exact (HkR HkR')].
    + intros Hnot.
      destruct (decide (k = pk)) as [-> | Hne].
      * rewrite Hmid. exact Hnd.
      * pose proof (Hnot Hne) as Hnin.
        rewrite <- Hmid in Hnin.
        apply NoDup_app. split_and!; [exact HndL | |].
        -- intros x HxL Hxin. apply elem_of_cons in Hxin as [-> | HxR].
           ++ apply Hnin. apply elem_of_app. by left.
           ++ apply (HdisjL x HxL). apply elem_of_cons. by right.
        -- apply NoDup_cons. split; [|exact HndR].
           intros HkR. apply Hnin. apply elem_of_app. right. apply elem_of_cons. by right.
  - intros x. rewrite Hins. rewrite <- Hmid at 1.
    unfold trim_single_removed.
    destruct (decide (k = pk)) as [-> | Hne].
    + rewrite elem_of_nil. split; [intros [Hin Hnin]; contradiction | intros []].
    + split.
      * intros [Hin Hnin].
        apply elem_of_app in Hin as [HxL | Hx_rest].
        -- exfalso. apply Hnin. apply elem_of_app. by left.
        -- apply elem_of_cons in Hx_rest as [-> | HxR]; [by left |].
           exfalso. apply Hnin. apply elem_of_app. right. apply elem_of_cons. by right.
      * intros Hmem. apply elem_of_cons in Hmem as [-> | Hnil]; [|inversion Hnil]. split.
        -- apply elem_of_app. right. by left.
        -- intros Hinx. apply elem_of_app in Hinx as [HpkL | Hpk_rest].
           ++ apply (HdisjL pk HpkL). by left.
           ++ apply elem_of_cons in Hpk_rest as [Heq | HpkR']; [exact (Hne (eq_sym Heq)) | exact (HpkR HpkR')].
Qed.

Definition trim_two_removed {K : Type} `{EqDecision K} (pk0 pk1 k0 k1 : K) : list K :=
  (if decide (pk0 = k0 \/ pk0 = k1) then [] else [pk0]) ++
  (if decide (pk1 = k0 \/ pk1 = k1) then [] else [pk1]).

(**md
**In a `NoDup` key array `prevKeys` where distinct slots `i0 < i1` hold `pk0` and `pk1`, updating those slots to `(k0, k1)` preserves `NoDup` iff `k1 <> k0` and neither `k0` nor `k1` collides with an outside key, while emitting duplicate-free removals matching set difference (`Projections.fs`).**

- [prevKeys:] the previous `NoDup` published key sequence.
- [i0:] the first changed index (`i0 < i1`).
- [i1:] the second changed index.
- [pk0:] the previous key at index `i0`.
- [pk1:] the previous key at index `i1`.
- [k0:] the recomputed key at index `i0`.
- [k1:] the recomputed key at index `i1`.
*)
Theorem trim_two_index_key_swap_preserves_uniqueness
    {K : Type} `{EqDecision K} (prevKeys : list K) (i0 i1 : nat) (pk0 pk1 k0 k1 : K) :
  NoDup prevKeys ->
  i0 < i1 ->
  prevKeys !! i0 = Some pk0 ->
  prevKeys !! i1 = Some pk1 ->
  (NoDup (<[i1 := k1]> (<[i0 := k0]> prevKeys)) <->
   (k1 <> k0 /\
    (k0 <> pk0 /\ k0 <> pk1 -> k0 ∉ prevKeys) /\
    (k1 <> pk0 /\ k1 <> pk1 -> k1 ∉ prevKeys))) /\
  NoDup (trim_two_removed pk0 pk1 k0 k1) /\
  (forall x, (x ∈ prevKeys /\ x ∉ <[i1 := k1]> (<[i0 := k0]> prevKeys)) <->
             x ∈ trim_two_removed pk0 pk1 k0 k1).
Proof.
  intros Hnd Hlt H0 H1.
  pose proof (lookup_lt_Some _ _ _ H0) as Hlt0.
  pose proof (lookup_lt_Some _ _ _ H1) as Hlt1.
  pose proof (take_drop_middle prevKeys i0 pk0 H0) as Hmid0.
  set (L := take i0 prevKeys) in *.
  set (Rest := drop (S i0) prevKeys) in *.
  assert (HlenL : length L = i0) by (subst L; apply length_take_le; lia).
  set (j := i1 - S i0) in *.
  assert (Hj : Rest !! j = Some pk1).
  { subst Rest j. rewrite lookup_drop. replace (S i0 + (i1 - S i0)) with i1 by lia. exact H1. }
  pose proof (take_drop_middle Rest j pk1 Hj) as Hmid1.
  set (M := take j Rest) in *.
  set (R := drop (S j) Rest) in *.
  assert (HlenM : length M = j) by (subst M; apply length_take_le; pose proof (lookup_lt_Some _ _ _ Hj); lia).
  assert (Hdecomp : prevKeys = L ++ pk0 :: M ++ pk1 :: R)
    by exact (eq_trans (eq_sym Hmid0) (f_equal (fun s => L ++ pk0 :: s) (eq_sym Hmid1))).
  assert (Hins0 : <[i0 := k0]> prevKeys = L ++ k0 :: M ++ pk1 :: R).
  { rewrite Hdecomp at 1. rewrite insert_app_r_alt; [|lia].
    rewrite HlenL, Nat.sub_diag. simpl. reflexivity. }
  assert (Hins1 : <[i1 := k1]> (<[i0 := k0]> prevKeys) = L ++ k0 :: M ++ k1 :: R).
  { rewrite Hins0. rewrite insert_app_r_alt; [|lia].
    rewrite HlenL. replace (i1 - i0) with (S j) by lia. simpl.
    f_equal. f_equal. rewrite insert_app_r_alt; [|lia].
    rewrite HlenM, Nat.sub_diag. simpl. reflexivity. }
  rewrite Hdecomp in Hnd.
  apply NoDup_app in Hnd as (HndL & HdisjL & Hnd_rest0).
  apply NoDup_cons in Hnd_rest0 as (Hpk0_nin & Hnd_rest1).
  apply NoDup_app in Hnd_rest1 as (HndM & HdisjM & Hnd_rest2).
  apply NoDup_cons in Hnd_rest2 as (Hpk1R & HndR).
  assert (Hpk_ne : pk0 <> pk1).
  { intros Heq. apply Hpk0_nin. apply elem_of_app. right. rewrite Heq. by left. }
  assert (Hpk0L : pk0 ∉ L) by (intros Hin; apply (HdisjL pk0 Hin); by left).
  assert (Hpk1L : pk1 ∉ L) by (intros Hin; apply (HdisjL pk1 Hin); apply elem_of_cons; right; apply elem_of_app; right; by left).
  assert (Hpk0M : pk0 ∉ M) by (intros Hin; apply Hpk0_nin; apply elem_of_app; by left).
  assert (Hpk1M : pk1 ∉ M) by (intros Hin; apply (HdisjM pk1 Hin); by left).
  assert (Hpk0R : pk0 ∉ R) by (intros Hin; apply Hpk0_nin; apply elem_of_app; right; apply elem_of_cons; by right).
  split_and!.
  - rewrite Hins1. split.
    + intros Hnd'.
      apply NoDup_app in Hnd' as (_ & HdisjL' & Hnd_r0).
      apply NoDup_cons in Hnd_r0 as (Hk0_nin & Hnd_r1).
      apply NoDup_app in Hnd_r1 as (_ & HdisjM' & Hnd_r2).
      apply NoDup_cons in Hnd_r2 as (Hk1R & _).
      assert (Hk_ne : k1 <> k0).
      { intros Heq. apply Hk0_nin. apply elem_of_app. right. rewrite Heq. by left. }
      repeat split; try exact Hk_ne; rewrite Hdecomp; intros [Hn0 Hn1] Hin.
      * apply elem_of_app in Hin as [HinL | Hin].
        { apply (HdisjL' k0 HinL). by left. }
        apply elem_of_cons in Hin as [Heq | Hin]; [exact (Hn0 Heq)|].
        apply elem_of_app in Hin as [HinM | Hin].
        { apply Hk0_nin. apply elem_of_app. by left. }
        apply elem_of_cons in Hin as [Heq | HinR]; [exact (Hn1 Heq)|].
        apply Hk0_nin. apply elem_of_app. right. apply elem_of_cons. by right.
      * apply elem_of_app in Hin as [HinL | Hin].
        { apply (HdisjL' k1 HinL). apply elem_of_cons. right. apply elem_of_app. right. by left. }
        apply elem_of_cons in Hin as [Heq | Hin]; [exact (Hn0 Heq)|].
        apply elem_of_app in Hin as [HinM | Hin].
        { apply (HdisjM' k1 HinM). by left. }
        apply elem_of_cons in Hin as [Heq | HinR]; [exact (Hn1 Heq)|].
        exact (Hk1R HinR).
    + intros (Hk_ne & Hk0_out & Hk1_out).
      assert (Hk0_L : k0 ∉ L).
      { intros Hin. apply Hk0_out; [split; [intros ->; contradiction | intros ->; contradiction]|].
        rewrite Hdecomp. apply elem_of_app. by left. }
      assert (Hk0_M : k0 ∉ M).
      { intros Hin. apply Hk0_out; [split; [intros ->; contradiction | intros ->; contradiction]|].
        rewrite Hdecomp. apply elem_of_app. right. apply elem_of_cons. right. apply elem_of_app. by left. }
      assert (Hk0_R : k0 ∉ R).
      { intros Hin. apply Hk0_out; [split; [intros ->; contradiction | intros ->; contradiction]|].
        rewrite Hdecomp. apply elem_of_app. right. apply elem_of_cons. right. apply elem_of_app. right. apply elem_of_cons. by right. }
      assert (Hk1_L : k1 ∉ L).
      { intros Hin. apply Hk1_out; [split; [intros ->; contradiction | intros ->; contradiction]|].
        rewrite Hdecomp. apply elem_of_app. by left. }
      assert (Hk1_M : k1 ∉ M).
      { intros Hin. apply Hk1_out; [split; [intros ->; contradiction | intros ->; contradiction]|].
        rewrite Hdecomp. apply elem_of_app. right. apply elem_of_cons. right. apply elem_of_app. by left. }
      assert (Hk1_R : k1 ∉ R).
      { intros Hin. apply Hk1_out; [split; [intros ->; contradiction | intros ->; contradiction]|].
        rewrite Hdecomp. apply elem_of_app. right. apply elem_of_cons. right. apply elem_of_app. right. apply elem_of_cons. by right. }
      apply NoDup_app. split_and!; [exact HndL| |].
      * intros x HxL Hxin.
        apply elem_of_cons in Hxin as [-> | Hxin]; [exact (Hk0_L HxL)|].
        apply elem_of_app in Hxin as [HxM | Hxin].
        { apply (HdisjL x HxL). apply elem_of_cons. right. apply elem_of_app. by left. }
        apply elem_of_cons in Hxin as [-> | HxR]; [exact (Hk1_L HxL)|].
        apply (HdisjL x HxL). apply elem_of_cons. right. apply elem_of_app. right. apply elem_of_cons. by right.
      * apply NoDup_cons. split.
        -- intros Hxin. apply elem_of_app in Hxin as [Hk0M | Hxin]; [exact (Hk0_M Hk0M)|].
           apply elem_of_cons in Hxin as [Heq | Hk0R]; [exact (Hk_ne (eq_sym Heq)) | exact (Hk0_R Hk0R)].
        -- apply NoDup_app. split_and!; [exact HndM| |].
           ++ intros x HxM Hxin. apply elem_of_cons in Hxin as [-> | HxR]; [exact (Hk1_M HxM)|].
              apply (HdisjM x HxM). apply elem_of_cons. by right.
           ++ apply NoDup_cons. split; [exact Hk1_R | exact HndR].
  - unfold trim_two_removed.
    destruct (decide (pk0 = k0 \/ pk0 = k1)), (decide (pk1 = k0 \/ pk1 = k1)); simpl.
    + constructor.
    + apply NoDup_singleton.
    + apply NoDup_singleton.
    + apply NoDup_cons. split; [rewrite elem_of_cons, elem_of_nil; intros [Heq|Hnil]; [exact (Hpk_ne Heq) | inversion Hnil] | apply NoDup_singleton].
  - intros x. rewrite Hins1, Hdecomp. unfold trim_two_removed.
    assert (Hout : x ∈ (L ++ k0 :: M ++ k1 :: R) <-> (x ∈ L \/ x = k0 \/ x ∈ M \/ x = k1 \/ x ∈ R)).
    { repeat (rewrite elem_of_app || rewrite elem_of_cons); tauto. }
    assert (Hin : x ∈ (L ++ pk0 :: M ++ pk1 :: R) <-> (x ∈ L \/ x = pk0 \/ x ∈ M \/ x = pk1 \/ x ∈ R)).
    { repeat (rewrite elem_of_app || rewrite elem_of_cons); tauto. }
    rewrite Hin, Hout.
    split.
    + intros [[HxL | [-> | [HxM | [-> | HxR]]]] Hnin].
      * exfalso; apply Hnin; by left.
      * apply elem_of_app. left. rewrite decide_False; [by left |].
        intros [-> | ->]; apply Hnin; tauto.
      * exfalso; apply Hnin; tauto.
      * apply elem_of_app. right. rewrite decide_False; [by left |].
        intros [-> | ->]; apply Hnin; tauto.
      * exfalso; apply Hnin; tauto.
    + intros Hmem.
      apply elem_of_app in Hmem as [Hm0 | Hm1].
      * destruct (decide (pk0 = k0 \/ pk0 = k1)) as [_|Hn0]; [inversion Hm0|].
        apply elem_of_cons in Hm0 as [-> | Hm0]; [|inversion Hm0].
        split; [tauto|].
        intros [HpL | [Heq | [HpM | [Heq | HpR]]]];
          [exact (Hpk0L HpL) | exact (Hn0 (or_introl Heq)) | exact (Hpk0M HpM) | exact (Hn0 (or_intror Heq)) | exact (Hpk0R HpR)].
      * destruct (decide (pk1 = k0 \/ pk1 = k1)) as [_|Hn1]; [inversion Hm1|].
        apply elem_of_cons in Hm1 as [-> | Hm1]; [|inversion Hm1].
        split; [tauto|].
        intros [HpL | [Heq | [HpM | [Heq | HpR]]]];
          [exact (Hpk1L HpL) | exact (Hn1 (or_introl Heq)) | exact (Hpk1M HpM) | exact (Hn1 (or_intror Heq)) | exact (Hpk1R HpR)].
Qed.

Fixpoint dense_entries {V : Type} (start : nat) (xs : list V) : gmap nat V :=
  match xs with
  | [] => ∅
  | v :: xs' => <[start := v]> (dense_entries (S start) xs')
  end.

Lemma lookup_dense_entries {V : Type} (start i : nat) (xs : list V) :
  dense_entries start xs !! i =
  if decide (start <= i) then xs !! (i - start) else None.
Proof.
  revert start.
  induction xs as [|v xs' IH]; intros start; simpl.
  - rewrite lookup_empty. destruct (decide (start <= i)); reflexivity.
  - destruct (decide (start <= i)) as [Hle|Hgt].
    + destruct (decide (i = start)) as [->|Hne].
      * rewrite lookup_insert_eq, Nat.sub_diag. reflexivity.
      * rewrite lookup_insert_ne; [|symmetry; exact Hne].
        rewrite IH, decide_True; [|lia].
        replace (i - start) with (S (i - S start)) by lia.
        reflexivity.
    + rewrite lookup_insert_ne; [|lia].
      rewrite IH, decide_False; [reflexivity | lia].
Qed.

Definition dense_step {V : Type} `{EqDecision V}
    (prev cur : list V) (i : nat) (m : gmap nat V) : gmap nat V :=
  if decide (i < Nat.min (length prev) (length cur)) then
    if decide (prev !! i = cur !! i) then m
    else match cur !! i with Some v => <[i := v]> m | None => m end
  else if decide (i < length cur) then
    match cur !! i with Some v => <[i := v]> m | None => m end
  else
    delete i m.

Fixpoint dense_stage_upto {V : Type} `{EqDecision V}
    (prev cur : list V) (n : nat) (m : gmap nat V) : gmap nat V :=
  match n with
  | 0 => m
  | S n' => dense_step prev cur n' (dense_stage_upto prev cur n' m)
  end.

Lemma dense_step_lookup_ne {V : Type} `{EqDecision V}
    (prev cur : list V) (i q : nat) (m : gmap nat V) :
  q <> i -> dense_step prev cur i m !! q = m !! q.
Proof.
  intros Hne. unfold dense_step.
  destruct (decide (i < Nat.min (length prev) (length cur))).
  - destruct (decide (prev !! i = cur !! i)); [reflexivity|].
    destruct (cur !! i); [apply lookup_insert_ne; symmetry; exact Hne | reflexivity].
  - destruct (decide (i < length cur)).
    + destruct (cur !! i); [apply lookup_insert_ne; symmetry; exact Hne | reflexivity].
    + apply lookup_delete_ne; symmetry; exact Hne.
Qed.

Lemma dense_stage_upto_hi {V : Type} `{EqDecision V}
    (prev cur : list V) (n q : nat) (m : gmap nat V) :
  n <= q ->
  dense_stage_upto prev cur n m !! q = m !! q.
Proof.
  induction n as [|n' IH]; intros Hle; simpl; [reflexivity|].
  rewrite dense_step_lookup_ne by lia. apply IH. lia.
Qed.

Lemma dense_stage_upto_spec {V : Type} `{EqDecision V}
    (prev cur : list V) (n : nat) (m : gmap nat V) (q : nat) :
  m !! q = prev !! q ->
  dense_stage_upto prev cur n m !! q = if decide (q < n) then cur !! q else m !! q.
Proof.
  intros Hinit.
  induction n as [|n' IH]; simpl.
  - destruct (decide (q < 0)); [lia | reflexivity].
  - destruct (decide (q < S n')) as [Hlt|Hge].
    + destruct (decide (q = n')) as [->|Hne].
      * pose proof (dense_stage_upto_hi prev cur n' n' m ltac:(lia)) as Hhi.
        unfold dense_step.
        destruct (decide (n' < Nat.min (length prev) (length cur))) as [Hmin|Hnmin].
        -- destruct (decide (prev !! n' = cur !! n')) as [Heq|Hdiff].
           ++ rewrite Hhi, Hinit. exact Heq.
           ++ destruct (lookup_lt_is_Some_2 cur n' ltac:(lia)) as [v Hv].
              rewrite Hv, lookup_insert_eq. reflexivity.
        -- destruct (decide (n' < length cur)) as [Hcur|Hncur].
           ++ destruct (lookup_lt_is_Some_2 cur n' Hcur) as [v Hv].
              rewrite Hv, lookup_insert_eq. reflexivity.
           ++ rewrite lookup_delete_eq. symmetry. apply lookup_ge_None_2. lia.
      * rewrite dense_step_lookup_ne by lia.
        rewrite IH, decide_True by lia. reflexivity.
    + rewrite dense_step_lookup_ne by lia.
      rewrite IH, decide_False by lia. reflexivity.
Qed.

Lemma lookup_dense_entries_0 {V : Type} (i : nat) (xs : list V) :
  dense_entries 0 xs !! i = xs !! i.
Proof.
  rewrite lookup_dense_entries, decide_True; [|lia].
  rewrite Nat.sub_0_r. reflexivity.
Qed.

(**md
**`IndexProjection`'s dense positional pass transforms `dense_entries 0 prev` directly into `dense_entries 0 cur` while skipping unchanged overlap slots (`prev !! i = cur !! i`), skipping `Keys` publication iff `length cur = length prev`, and preserving unchanged reference-equal row values (`Projections.fs`).**

- [prev:] previous materialized item slice.
- [cur:] current materialized item slice.
*)
Theorem index_projection_dense_diff_refines_visit
    {V : Type} `{EqDecision V} (prev cur : list V) :
  dense_stage_upto prev cur (Nat.max (length prev) (length cur)) (dense_entries 0 prev) =
    dense_entries 0 cur /\
  (seq 0 (length cur) = seq 0 (length prev) <-> length cur = length prev) /\
  (forall i v, i < Nat.min (length prev) (length cur) ->
    prev !! i = Some v -> cur !! i = Some v ->
    dense_entries 0 prev !! i = dense_entries 0 cur !! i).
Proof.
  split_and!.
  - apply map_eq. intros q.
    rewrite dense_stage_upto_spec; [|apply lookup_dense_entries_0].
    rewrite !lookup_dense_entries_0.
    destruct (decide (q < Nat.max (length prev) (length cur))) as [Hlt|Hge]; [reflexivity|].
    rewrite (lookup_ge_None_2 prev q ltac:(lia)), (lookup_ge_None_2 cur q ltac:(lia)). reflexivity.
  - split; [intro Heq; apply (f_equal (@length nat)) in Heq; rewrite !length_seq in Heq; exact Heq | intros ->; reflexivity].
  - intros i v Hmin Hprev Hcur.
    rewrite !lookup_dense_entries_0. congruence.
Qed.

Inductive ls_upstream (S : Type) :=
  | LsPending
  | LsFailed
  | LsReady (v : S).
Arguments LsPending {S}.
Arguments LsFailed {S}.
Arguments LsReady {S} v.

Record lookup_sync_state (S T : Type) := mk_ls {
  ls_synced_run : Z;
  ls_primed : bool;
  ls_pending : bool;
  ls_failed : bool;
  ls_prev_state : S;
  ls_table : T;
}.
Arguments mk_ls {S T} _ _ _ _ _ _.
Arguments ls_synced_run {S T} _.
Arguments ls_primed {S T} _.
Arguments ls_pending {S T} _.
Arguments ls_failed {S T} _.
Arguments ls_prev_state {S T} _.
Arguments ls_table {S T} _.

Definition ls_refresh {S T : Type} `{EqDecision S}
    (up_runs : Z) (u : ls_upstream S) (recompute : S -> T) (st : lookup_sync_state S T)
    : lookup_sync_state S T :=
  match u with
  | LsPending =>
      {| ls_synced_run := (-1)%Z;
         ls_primed := ls_primed st;
         ls_pending := true;
         ls_failed := false;
         ls_prev_state := ls_prev_state st;
         ls_table := ls_table st |}
  | LsFailed =>
      {| ls_synced_run := (-1)%Z;
         ls_primed := ls_primed st;
         ls_pending := false;
         ls_failed := true;
         ls_prev_state := ls_prev_state st;
         ls_table := ls_table st |}
  | LsReady v =>
      if negb (ls_primed st) then
        {| ls_synced_run := up_runs;
           ls_primed := true;
           ls_pending := false;
           ls_failed := false;
           ls_prev_state := v;
           ls_table := ls_table st |}
      else if ls_pending st || ls_failed st || negb (bool_decide (ls_prev_state st = v)) then
        {| ls_synced_run := up_runs;
           ls_primed := true;
           ls_pending := false;
           ls_failed := false;
           ls_prev_state := v;
           ls_table := recompute v |}
      else
        {| ls_synced_run := up_runs;
           ls_primed := true;
           ls_pending := false;
           ls_failed := false;
           ls_prev_state := ls_prev_state st;
           ls_table := ls_table st |}
  end.

Definition ls_sync_fast {S T : Type} `{EqDecision S}
    (up_runs : Z) (up_clean_ready : bool) (u : ls_upstream S)
    (recompute : S -> T) (st : lookup_sync_state S T)
    : lookup_sync_state S T :=
  if bool_decide (ls_synced_run st = up_runs) && up_clean_ready then st
  else ls_refresh up_runs u recompute st.

Definition ls_sync_inv {S T : Type}
    (up_runs : Z) (u : ls_upstream S) (st : lookup_sync_state S T) : Prop :=
  ls_synced_run st = up_runs ->
  exists v,
    u = LsReady v /\
    ls_primed st = true /\
    ls_pending st = false /\
    ls_failed st = false /\
    ls_prev_state st = v.

Record affected_buf_state (K : Type) := mk_ab {
  ab_collecting : bool;
  ab_buf : list K;
}.
Arguments mk_ab {K} _ _.
Arguments ab_collecting {K} _.
Arguments ab_buf {K} _.

Definition ab_inv {K : Type} (st : affected_buf_state K) : Prop :=
  ab_collecting st = false -> ab_buf st = [].

Definition ab_collect_step {K : Type}
    (st : affected_buf_state K) (xs : list K) : list K * affected_buf_state K :=
  if negb (ab_collecting st) then
    let st_busy := {| ab_collecting := true; ab_buf := xs |} in
    let delivered := ab_buf st_busy in
    (delivered, {| ab_collecting := false; ab_buf := [] |})
  else
    (xs, st).

(**md
**Skipping `Lookup.Refresh` when `syncedRun = state.Runs && state.IsCleanAndReady` is identical to running `Refresh`, `Refresh` preserves `ls_sync_inv` on non-negative runs, and pooling `affectedBuf` under `collectingAffected` isolates reentrant notifications and restores the empty buffer (`Projections.fs`).**

- [up_runs:] upstream `state.Runs` generation counter.
- [up_clean_ready:] whether upstream `state.IsCleanAndReady` holds.
- [u:] upstream `TryValueUntracked` outcome.
- [recompute:] table invalidation/recomputation function when upstream changes.
- [st:] `Lookup` sync state.
- [ab:] pooled `affectedBuf` state.
- [xs:] top-level affected key batch.
- [nested:] reentrant nested affected key batch.
*)
Theorem lookup_sync_clean_skip_and_affected_buf_eq
    {S T K : Type} `{EqDecision S}
    (up_runs : Z) (up_clean_ready : bool) (u : ls_upstream S)
    (recompute : S -> T) (st : lookup_sync_state S T)
    (ab : affected_buf_state K) (xs nested : list K) :
  (up_clean_ready = true -> exists v, u = LsReady v) ->
  (ls_sync_inv up_runs u st ->
   ls_sync_fast up_runs up_clean_ready u recompute st = ls_refresh up_runs u recompute st) /\
  ((0 <= up_runs)%Z ->
   ls_sync_inv up_runs u (ls_refresh up_runs u recompute st)) /\
  (ab_inv ab -> ab_collecting ab = false ->
   let st_during := {| ab_collecting := true; ab_buf := xs |} in
   ab_collect_step st_during nested = (nested, st_during) /\
   ab_collect_step ab xs = (xs, {| ab_collecting := false; ab_buf := ([] : list K) |}) /\
   ab_inv {| ab_collecting := false; ab_buf := ([] : list K) |}).
Proof.
  intro Hclean.
  split_and!.
  - intro Hinv. unfold ls_sync_fast.
    destruct (bool_decide (ls_synced_run st = up_runs)) eqn:Hrun;
      destruct up_clean_ready; simpl; try reflexivity.
    apply bool_decide_eq_true_1 in Hrun.
    destruct (Hinv Hrun) as (v & -> & Hp & Hpend & Hfail & <-).
    unfold ls_refresh. rewrite Hp, Hpend, Hfail. simpl.
    rewrite bool_decide_eq_true_2 by reflexivity. simpl.
    destruct st; simpl in *; subst; reflexivity.
  - intros Hpos Heq.
    destruct u as [| |v]; unfold ls_refresh in Heq; simpl in Heq; try lia.
    exists v. unfold ls_refresh.
    destruct (ls_primed st); simpl.
    + destruct (ls_pending st || ls_failed st || negb (bool_decide (ls_prev_state st = v))) eqn:Econd; simpl;
        [split_and!; reflexivity|].
      destruct (ls_pending st), (ls_failed st), (bool_decide (ls_prev_state st = v)) eqn:Ebd;
        simpl in Econd; try discriminate.
      apply bool_decide_eq_true_1 in Ebd.
      split_and!; try reflexivity; exact Ebd.
    + split_and!; reflexivity.
  - intros _ Hfalse. simpl.
    unfold ab_collect_step, ab_inv. rewrite Hfalse. simpl.
    split_and!; [reflexivity | reflexivity | intro; reflexivity].
Qed.

(** ** Cached row-entry reads, slice/group-view window diffs, and non-invertible fold row cache *)

Record cached_entry (V : Type) := {
  ce_live : bool;
  ce_val : V;
}.
Arguments ce_live {V} _.
Arguments ce_val {V} _.

Definition read_entry_cached {K V : Type} `{Countable K}
    (entries : gmap K (cached_entry V)) (key : K) (cached : option (cached_entry V))
    : option (cached_entry V) :=
  match cached with
  | Some e => if ce_live e then Some e else entries !! key
  | None => entries !! key
  end.

(**md
**Reading a cached `RowEntry` directly whenever `ce_live e = true`, and falling back to `entries !! key` on `None` or retirement (`ce_live e = false`), agrees with `entries !! key` under the live-entry invariant (`Projections.fs` `ReadEntry`, `Combinators.fs` `EntryCache`).**

- [entries:] live key-to-entry map at the read.
- [entries':] updated key-to-entry map after entry retirement.
- [key:] row key.
- [cached:] cached entry reference (`Tag` or `DualEntry`).
- [v0:] payload retained on a retired entry.
*)
Theorem live_row_entry_agrees_with_find
    {K V : Type} `{Countable K}
    (entries entries' : gmap K (cached_entry V))
    (key : K) (cached : option (cached_entry V)) (v0 : V) :
  (forall e, cached = Some e -> ce_live e = true -> entries !! key = Some e) ->
  read_entry_cached entries key cached = entries !! key /\
  read_entry_cached entries' key (Some {| ce_live := false; ce_val := v0 |}) = entries' !! key.
Proof.
  intro Hlive. split.
  - destruct cached as [e|]; simpl; [|reflexivity].
    destruct (ce_live e) eqn:Hl; [symmetry; apply Hlive; [reflexivity | exact Hl] | reflexivity].
  - simpl. reflexivity.
Qed.

Definition slice_win {K : Type} (xs : list K) (start win_len : nat) : list K :=
  take win_len (drop start xs).

(**md
**When upstream changes only `[chg_start .. chg_prev_end]` into `[chg_start .. chg_end]` (`pref ++ mid_old ++ suf` to `pref ++ mid_new ++ suf`), a fixed-length `SliceView` window strictly before `chg_start` or strictly after the edited range with constant shift relative to `chg_end`/`chg_prev_end` yields the identical slice in $O(1)$ (`Combinators.fs` `SliceView.Enumerate`).**

- [pref:] shared prefix before `chg_start`.
- [mid_old:] old edited segment in `prev`.
- [mid_new:] replacement edited segment in `cur`.
- [suf:] shared suffix after the edited segment.
- [start:] current `SliceView` window start index.
- [last_start:] prior `SliceView` window start index.
- [win_len:] preserved window length.
*)
Theorem slice_outside_changed_range_noop
    {K : Type} (pref mid_old mid_new suf : list K) (start last_start win_len : nat) :
  let prev := pref ++ mid_old ++ suf in
  let cur := pref ++ mid_new ++ suf in
  let chg_start := length pref in
  let chg_prev_end := (Z.of_nat (length pref + length mid_old) - 1)%Z in
  let chg_end := (Z.of_nat (length pref + length mid_new) - 1)%Z in
  ((start = last_start /\ start + win_len <= chg_start) \/
   ((Z.of_nat start > chg_end)%Z /\
    (Z.of_nat last_start > chg_prev_end)%Z /\
    (Z.of_nat start - chg_end = Z.of_nat last_start - chg_prev_end)%Z)) ->
  slice_win cur start win_len = slice_win prev last_start win_len.
Proof.
  intros prev cur chg_start chg_prev_end chg_end Hcase.
  subst prev cur chg_start chg_prev_end chg_end.
  unfold slice_win.
  destruct Hcase as [[-> Hpre] | [Hs [Hls Hdiff]]].
  - rewrite !drop_app_le by lia.
    rewrite !take_app_le by (rewrite length_drop; lia).
    reflexivity.
  - rewrite !app_assoc.
    rewrite !drop_app_ge by (rewrite length_app; lia).
    rewrite !length_app.
    replace (start - (length pref + length mid_new))
       with (last_start - (length pref + length mid_old)) by lia.
    reflexivity.
Qed.

Fixpoint filter_absent {K : Type} `{EqDecision K} (known xs : list K) : list K :=
  match xs with
  | [] => []
  | k :: rest =>
      if decide (k ∈ known) then filter_absent known rest
      else k :: filter_absent known rest
  end.

Lemma filter_absent_app {K : Type} `{EqDecision K} (known xs ys : list K) :
  filter_absent known (xs ++ ys) = filter_absent known xs ++ filter_absent known ys.
Proof.
  induction xs as [|k xs IH]; simpl; [reflexivity|].
  destruct (decide (k ∈ known)); simpl; [exact IH | rewrite IH; reflexivity].
Qed.

Lemma filter_absent_sub {K : Type} `{EqDecision K} (known xs : list K) :
  (forall x, x ∈ xs -> x ∈ known) ->
  filter_absent known xs = [].
Proof.
  induction xs as [|k xs IH]; intro Hsub; simpl; [reflexivity|].
  rewrite decide_True by (apply Hsub; left).
  apply IH. intros x Hx. apply Hsub. right; exact Hx.
Qed.

Lemma filter_absent_ext_in {K : Type} `{EqDecision K} (k1 k2 xs : list K) :
  (forall x, x ∈ xs -> (x ∈ k1 <-> x ∈ k2)) ->
  filter_absent k1 xs = filter_absent k2 xs.
Proof.
  induction xs as [|k xs IH]; intro Hext; simpl; [reflexivity|].
  assert (Hiff : k ∈ k1 <-> k ∈ k2) by (apply Hext; left).
  destruct (decide (k ∈ k1)) as [Hk1|Hk1];
    destruct (decide (k ∈ k2)) as [Hk2|Hk2]; try tauto.
  - apply IH. intros x Hx. apply Hext. right; exact Hx.
  - f_equal. apply IH. intros x Hx. apply Hext. right; exact Hx.
Qed.

(**md
**Trimming equal shared prefix `pref` and suffix `suf` before diffing `prev = pref ++ mid_prev ++ suf` against `next = pref ++ mid_next ++ suf` (under `NoDup prev`) visits only `mid_next` for additions and detects removals only within `mid_prev` via membership in `mid_next` (`Combinators.fs` `SliceView.Enumerate`, `GroupView.Enumerate`).**

- [pref:] shared prefix keys.
- [mid_prev:] differing window in `prev`.
- [mid_next:] differing window in `next`.
- [suf:] shared suffix keys.
*)
Theorem window_trim_diff_refines_visit
    {K : Type} `{EqDecision K} (pref mid_prev mid_next suf : list K) :
  let prev := pref ++ mid_prev ++ suf in
  let next := pref ++ mid_next ++ suf in
  NoDup prev ->
  filter_absent prev next = filter_absent prev mid_next /\
  filter_absent next prev = filter_absent mid_next mid_prev.
Proof.
  intros prev next Hnd.
  assert (Hprev_pref : forall x, x ∈ pref -> x ∈ prev).
  { intros x Hx. subst prev. rewrite elem_of_app; left; exact Hx. }
  assert (Hprev_suf : forall x, x ∈ suf -> x ∈ prev).
  { intros x Hx. subst prev. rewrite !elem_of_app; right; right; exact Hx. }
  assert (Hnext_pref : forall x, x ∈ pref -> x ∈ next).
  { intros x Hx. subst next. rewrite elem_of_app; left; exact Hx. }
  assert (Hnext_suf : forall x, x ∈ suf -> x ∈ next).
  { intros x Hx. subst next. rewrite !elem_of_app; right; right; exact Hx. }
  split.
  - subst next. rewrite !filter_absent_app.
    rewrite (filter_absent_sub prev pref Hprev_pref), (filter_absent_sub prev suf Hprev_suf).
    simpl. rewrite app_nil_r. reflexivity.
  - subst prev. rewrite !filter_absent_app.
    rewrite (filter_absent_sub next pref Hnext_pref), (filter_absent_sub next suf Hnext_suf).
    simpl. rewrite app_nil_r.
    apply filter_absent_ext_in. intros x Hx.
    subst next. rewrite !elem_of_app.
    rewrite !NoDup_app in Hnd.
    destruct Hnd as (_ & Hdisj1 & _ & Hdisj2 & _).
    split.
    + intros [Hp | [Hm | Hs]].
      * exfalso. eapply Hdisj1; [exact Hp | rewrite elem_of_app; left; exact Hx].
      * exact Hm.
      * exfalso. eapply Hdisj2; [exact Hx | exact Hs].
    + intro Hm. right; left; exact Hm.
Qed.

Definition fold_lookup_rows {K V S : Type} `{Countable K}
    (folder : S -> V -> S) (zero : S) (by_key : gmap K nat) (row_state : gmap nat V) (keys : list K) : S :=
  foldl (fun acc k =>
    match by_key !! k with
    | Some rid => match row_state !! rid with Some v => folder acc v | None => acc end
    | None => acc
    end) zero keys.

Definition fold_ordered_cache {V S : Type}
    (folder : S -> V -> S) (zero : S) (row_state : gmap nat V) (ordered_rids : list (option nat)) : S :=
  foldl (fun acc mrid =>
    match mrid with
    | Some rid => match row_state !! rid with Some v => folder acc v | None => acc end
    | None => acc
    end) zero ordered_rids.

(**md
**Caching the `Keys`-ordered sequence of `FoldRow` references (`orderedRows`) whenever key membership or order is unchanged makes non-invertible `Projection.fold` recomputation over `orderedRows` identical to `keys.Length` hash lookups across arbitrary in-place row value updates (`Combinators.fs` `ProjectionFold`).**

- [folder:] the fold accumulator function.
- [zero:] initial fold accumulator seed.
- [by_key:] current `rows` key-to-slot map.
- [row_state:] mutable per-row state (`HasValue` / `Value`) map.
- [row_state':] updated per-row state after in-place value edits.
- [keys:] upstream `Keys` sequence (`lastKeys`).
- [ordered_rids:] cached `orderedRows` sequence built from `keys` and `by_key`.
*)
Theorem fold_ordered_cache_refines_lookup_fold
    {K V S : Type} `{Countable K}
    (folder : S -> V -> S) (zero : S) (by_key : gmap K nat) (row_state row_state' : gmap nat V)
    (keys : list K) (ordered_rids : list (option nat)) :
  ordered_rids = (fun k => by_key !! k) <$> keys ->
  fold_ordered_cache folder zero row_state ordered_rids =
    fold_lookup_rows folder zero by_key row_state keys /\
  fold_ordered_cache folder zero row_state' ordered_rids =
    fold_lookup_rows folder zero by_key row_state' keys.
Proof.
  intros ->.
  assert (Hgen : forall (st : gmap nat V) z,
    fold_ordered_cache folder z st ((fun k => by_key !! k) <$> keys) =
    fold_lookup_rows folder z by_key st keys).
  { intros st z. unfold fold_ordered_cache, fold_lookup_rows. revert z.
    induction keys as [|k ks IH]; intro z; simpl.
    - reflexivity.
    - apply IH. }
  split; apply Hgen.
Qed.

(** ** Post-selector liveness guard and direct collection row fold equivalence *)

Inductive col_delta_kind := ColAdded | ColRemoved.

Record col_proj_state (K V : Type) `{Countable K} := mk_col_proj {
  cp_stopped : bool;
  cp_items : gmap K (nat * V);
  cp_order : list K;
  cp_changes : list (K * col_delta_kind);
  cp_revision : nat;
  cp_next_gen : nat;
}.
Arguments mk_col_proj {K V _ _} _ _ _ _ _ _.
Arguments cp_stopped {K V _ _} _.
Arguments cp_items {K V _ _} _.
Arguments cp_order {K V _ _} _.
Arguments cp_changes {K V _ _} _.
Arguments cp_revision {K V _ _} _.
Arguments cp_next_gen {K V _ _} _.

Definition col_liveness_inv {K V : Type} `{Countable K} (st : col_proj_state K V) : Prop :=
  cp_stopped st = true -> cp_items st = ∅ /\ cp_order st = [].

Definition col_add_or_update_core {K V : Type} `{Countable K}
    (k : K) (v : V) (st : col_proj_state K V) : col_proj_state K V :=
  match cp_items st !! k with
  | Some (gen, _) =>
      mk_col_proj (cp_stopped st) (<[k := (gen, v)]> (cp_items st))
                  (cp_order st) (cp_changes st) (cp_revision st) (cp_next_gen st)
  | None =>
      mk_col_proj (cp_stopped st) (<[k := (cp_next_gen st, v)]> (cp_items st))
                  (cp_order st ++ [k]) (cp_changes st ++ [(k, ColAdded)])
                  (S (cp_revision st)) (S (cp_next_gen st))
  end.

Definition col_dispose {K V : Type} `{Countable K}
    (st : col_proj_state K V) : col_proj_state K V :=
  mk_col_proj true ∅ [] [] (cp_revision st) (cp_next_gen st).

Definition col_add_or_update_by_unguarded {K V : Type} `{Countable K}
    (selector : V -> col_proj_state K V -> option (K * col_proj_state K V))
    (v : V) (st : col_proj_state K V) : option (col_proj_state K V) :=
  if cp_stopped st then None
  else match selector v st with
       | Some (k, st1) => Some (col_add_or_update_core k v st1)
       | None => None
       end.

Definition col_add_or_update_by_guarded {K V : Type} `{Countable K}
    (selector : V -> col_proj_state K V -> option (K * col_proj_state K V))
    (v : V) (st : col_proj_state K V) : option (col_proj_state K V) :=
  if cp_stopped st then None
  else match selector v st with
       | Some (k, st1) =>
           if cp_stopped st1 then None
           else Some (col_add_or_update_core k v st1)
       | None => None
       end.

(**md
**Restoring `EnsureLive ()` immediately after `keyOf value` in `CollectionProjection.AddOrUpdateBy` prevents inserting zombie items after reentrant disposal during the selector, while agreeing identically with unguarded upserts whenever the collection remains live (`Collections.fs` `AddOrUpdateBy`).**

Models the bug fix from `codex-astra-plan.md` §2:
- `col_add_or_update_by_guarded` preserves `col_liveness_inv` whenever `selector` does, rejecting (`None`) any run in which `selector` disposed the collection (`cp_stopped st1 = true`) without modifying `cp_items`, `cp_order`, `cp_changes`, or `cp_revision`.
- By contrast, `col_add_or_update_by_unguarded` permits `selector` to dispose the collection and then inserts `(k, v)` into the cleared `cp_items`, breaking `col_liveness_inv`.
- Whenever `selector` leaves the collection live (`cp_stopped st1 = false`), including under reentrant edits to the collection during `selector`, both guarded and unguarded paths return the exact same `Some (col_add_or_update_core k v st1)`.

- [selector:] the `keyOf` function, which may inspect or reentrantly mutate or dispose the collection.
- [k:] a key returned when `selector` disposes the collection.
- [v:] the upserted value.
- [st:] the collection state before `AddOrUpdateBy`.
*)
Theorem post_selector_liveness_guard
    {K V : Type} `{Countable K}
    (selector : V -> col_proj_state K V -> option (K * col_proj_state K V))
    (k : K) (v : V) (st : col_proj_state K V) :
  (forall v0 s0 k0 s1, col_liveness_inv s0 -> selector v0 s0 = Some (k0, s1) -> col_liveness_inv s1) ->
  (forall st', col_liveness_inv st -> col_add_or_update_by_guarded selector v st = Some st' ->
     col_liveness_inv st' /\ cp_stopped st' = false) /\
  (forall k0 st1, selector v st = Some (k0, st1) -> cp_stopped st1 = true ->
     col_add_or_update_by_guarded selector v st = None) /\
  (cp_stopped st = false ->
     exists st_bad,
       col_add_or_update_by_unguarded (fun _ s0 => Some (k, col_dispose s0)) v st = Some st_bad /\
       cp_stopped st_bad = true /\
       cp_items st_bad !! k = Some (cp_next_gen st, v) /\
       col_add_or_update_by_guarded (fun _ s0 => Some (k, col_dispose s0)) v st = None) /\
  (forall k0 st1, cp_stopped st = false -> selector v st = Some (k0, st1) -> cp_stopped st1 = false ->
     col_add_or_update_by_guarded selector v st = Some (col_add_or_update_core k0 v st1) /\
     col_add_or_update_by_unguarded selector v st = Some (col_add_or_update_core k0 v st1)).
Proof.
  intro Hsel. split_and!.
  - intros st' Hinv Hres. unfold col_add_or_update_by_guarded in Hres.
    destruct (cp_stopped st) eqn:Hst; [discriminate|].
    destruct (selector v st) as [[k0 st1]|] eqn:Hs; [|discriminate].
    destruct (cp_stopped st1) eqn:Hst1; [discriminate|].
    simplify_eq. split.
    + intros Hbad. unfold col_add_or_update_core in Hbad.
      destruct (cp_items st1 !! k0) as [[gen _]|]; simpl in Hbad; congruence.
    + unfold col_add_or_update_core.
      destruct (cp_items st1 !! k0) as [[gen _]|]; simpl; exact Hst1.
  - intros k0 st1 Hs Hst1. unfold col_add_or_update_by_guarded.
    destruct (cp_stopped st); [reflexivity|].
    rewrite Hs, Hst1. reflexivity.
  - intro Hlive. unfold col_add_or_update_by_unguarded, col_add_or_update_by_guarded.
    rewrite Hlive.
    eexists. split_and!; [reflexivity| | |reflexivity].
    + unfold col_add_or_update_core, col_dispose. simpl. rewrite lookup_empty. reflexivity.
    + unfold col_add_or_update_core, col_dispose. simpl. rewrite lookup_empty. simpl.
      apply lookup_insert_eq.
  - intros k0 st1 Hlive Hs Hst1. unfold col_add_or_update_by_guarded, col_add_or_update_by_unguarded.
    rewrite Hlive, Hs, Hst1. split; reflexivity.
Qed.

Inductive fold_row_reading (V : Type) :=
  | FRReady (v : V)
  | FRPending
  | FRFailed (err : Z).
Arguments FRReady {V} _.
Arguments FRPending {V}.
Arguments FRFailed {V} _.

Record col_row_cell (V : Type) := mk_row_cell {
  rc_gen : nat;
  rc_signal_val : V;
  rc_status : status;
  rc_err : Z;
}.
Arguments mk_row_cell {V} _ _ _ _.
Arguments rc_gen {V} _.
Arguments rc_signal_val {V} _.
Arguments rc_status {V} _.
Arguments rc_err {V} _.

Definition read_indirect_row {V : Type} (item_sig_by_gen : gmap nat (col_row_cell V))
    (outer_gen : nat) : fold_row_reading V :=
  match item_sig_by_gen !! outer_gen with
  | Some cell =>
      match rc_status cell with
      | StPending => FRPending
      | StError => FRFailed (rc_err cell)
      | _ => FRReady (rc_signal_val cell)
      end
  | None => FRPending
  end.

Definition read_direct_row {V : Type} (cell : col_row_cell V) : fold_row_reading V :=
  match rc_status cell with
  | StPending => FRPending
  | StError => FRFailed (rc_err cell)
  | _ => FRReady (rc_signal_val cell)
  end.

Record fold_row_acc (V : Type) := mk_fold_row_acc {
  fra_gen : nat;
  fra_has_value : bool;
  fra_value : V;
  fra_err : option Z;
}.
Arguments mk_fold_row_acc {V} _ _ _ _.
Arguments fra_gen {V} _.
Arguments fra_has_value {V} _.
Arguments fra_value {V} _.
Arguments fra_err {V} _.

Record fold_acc_state (Acc : Type) := mk_fold_acc {
  fas_state : Acc;
  fas_valued : nat;
  fas_failed : nat;
  fas_full : bool;
  fas_moved : bool;
}.
Arguments mk_fold_acc {Acc} _ _ _ _ _.
Arguments fas_state {Acc} _.
Arguments fas_valued {Acc} _.
Arguments fas_failed {Acc} _.
Arguments fas_full {Acc} _.
Arguments fas_moved {Acc} _.

Definition fold_step_with {Acc A : Type}
    (step : Acc -> A -> option Acc) (arg : A) (fs : fold_acc_state Acc) : fold_acc_state Acc :=
  if fas_full fs then fs
  else match step (fas_state fs) arg with
       | Some s' => mk_fold_acc s' (fas_valued fs) (fas_failed fs) false true
       | None => mk_fold_acc (fas_state fs) (fas_valued fs) (fas_failed fs) true (fas_moved fs)
       end.

Definition fold_fail_row {Acc V : Type} (err : Z) (r : fold_row_acc V) (fs : fold_acc_state Acc)
    : fold_row_acc V * fold_acc_state Acc :=
  let failed' := match fra_err r with None => S (fas_failed fs) | Some _ => fas_failed fs end in
  let moved' := match fra_err r with Some e => if decide (e = err) then fas_moved fs else true | None => true end in
  (mk_fold_row_acc (fra_gen r) (fra_has_value r) (fra_value r) (Some err),
   mk_fold_acc (fas_state fs) (fas_valued fs) failed' (fas_full fs) moved').

Definition fold_unfail_row {Acc V : Type} (r : fold_row_acc V) (fs : fold_acc_state Acc)
    : fold_row_acc V * fold_acc_state Acc :=
  match fra_err r with
  | Some _ =>
      (mk_fold_row_acc (fra_gen r) (fra_has_value r) (fra_value r) None,
       mk_fold_acc (fas_state fs) (fas_valued fs) (pred (fas_failed fs)) (fas_full fs) true)
  | None => (r, fs)
  end.

Definition fold_apply_reading {Acc V E : Type}
    (invertible : bool)
    (eq_v : V -> V -> option bool)
    (add_fn sub_fn : Acc -> E -> V -> option Acc)
    (ext : E) (default_v : V) (reading : fold_row_reading V)
    (r : fold_row_acc V) (fs : fold_acc_state Acc)
    : fold_row_acc V * fold_acc_state Acc :=
  match reading with
  | FRReady v =>
      let '(r1, fs1) := fold_unfail_row r fs in
      if negb (fra_has_value r1) then
        let r2 := mk_fold_row_acc (fra_gen r1) true v (fra_err r1) in
        let fs2 := mk_fold_acc (fas_state fs1) (S (fas_valued fs1)) (fas_failed fs1) (fas_full fs1) (fas_moved fs1) in
        if invertible then
          (r2, fold_step_with (fun s x => add_fn s ext x) v fs2)
        else
          (r2, mk_fold_acc (fas_state fs2) (fas_valued fs2) (fas_failed fs2) true (fas_moved fs2))
      else
        match eq_v (fra_value r1) v with
        | None => fold_fail_row (-1)%Z r1 fs1
        | Some true => (r1, fs1)
        | Some false =>
            let old := fra_value r1 in
            let r2 := mk_fold_row_acc (fra_gen r1) true v (fra_err r1) in
            if invertible then
              (r2, fold_step_with (fun s '(o, n) => match sub_fn s ext o with Some s' => add_fn s' ext n | None => None end) (old, v) fs1)
            else
              (r2, mk_fold_acc (fas_state fs1) (fas_valued fs1) (fas_failed fs1) true (fas_moved fs1))
        end
  | FRPending => fold_unfail_row r fs
  | FRFailed err => fold_fail_row err r fs
  end.

Definition fold_detach_row {Acc V E : Type}
    (invertible : bool) (zero_s : Acc)
    (sub_fn : Acc -> E -> V -> option Acc) (ext : E) (default_v : V)
    (r : fold_row_acc V) (fs : fold_acc_state Acc)
    : fold_row_acc V * fold_acc_state Acc :=
  let '(r1, fs1) := fold_unfail_row r fs in
  if fra_has_value r1 then
    let old := fra_value r1 in
    let valued' := pred (fas_valued fs1) in
    let r2 := mk_fold_row_acc (fra_gen r1) false default_v (fra_err r1) in
    let fs2 := mk_fold_acc (fas_state fs1) valued' (fas_failed fs1) (fas_full fs1) (fas_moved fs1) in
    if negb invertible then
      (r2, mk_fold_acc (fas_state fs2) valued' (fas_failed fs2) true (fas_moved fs2))
    else if bool_decide (valued' = 0) && negb (fas_full fs2) then
      (r2, mk_fold_acc zero_s 0 (fas_failed fs2) false true)
    else
      (r2, fold_step_with (fun s o => sub_fn s ext o) old fs2)
  else (r1, fs1).

Definition fold_direct_step {Acc V E : Type}
    (invertible : bool) (zero_s : Acc)
    (eq_v : V -> V -> option bool)
    (add_fn sub_fn : Acc -> E -> V -> option Acc)
    (ext : E) (default_v : V)
    (cell : col_row_cell V)
    (r : fold_row_acc V) (fs : fold_acc_state Acc)
    : fold_row_acc V * fold_acc_state Acc :=
  if decide (rc_gen cell = fra_gen r) then
    fold_apply_reading invertible eq_v add_fn sub_fn ext default_v (read_direct_row cell) r fs
  else
    let '(r_det, fs_det) := fold_detach_row invertible zero_s sub_fn ext default_v r fs in
    let r_fresh := mk_fold_row_acc (rc_gen cell) false default_v None in
    fold_apply_reading invertible eq_v add_fn sub_fn ext default_v (read_direct_row cell) r_fresh fs_det.

Definition fold_indirect_step {Acc V E : Type}
    (invertible : bool) (zero_s : Acc)
    (eq_v : V -> V -> option bool)
    (add_fn sub_fn : Acc -> E -> V -> option Acc)
    (ext : E) (default_v : V)
    (item_sig_by_gen : gmap nat (col_row_cell V)) (outer_gen : nat)
    (r : fold_row_acc V) (fs : fold_acc_state Acc)
    : fold_row_acc V * fold_acc_state Acc :=
  if decide (outer_gen = fra_gen r) then
    fold_apply_reading invertible eq_v add_fn sub_fn ext default_v (read_indirect_row item_sig_by_gen outer_gen) r fs
  else
    let '(r_det, fs_det) := fold_detach_row invertible zero_s sub_fn ext default_v r fs in
    let r_fresh := mk_fold_row_acc outer_gen false default_v None in
    fold_apply_reading invertible eq_v add_fn sub_fn ext default_v (read_indirect_row item_sig_by_gen outer_gen) r_fresh fs_det.

(**md
**Reading a live `CollectionProjection` row's `Signal` directly and applying cached `ProjectionFold` adapters preserves exact equivalence with the two-level `RowsOf` row-memo read across steady updates, batch-restored cutoffs, generation replacement (remove + re-add), and external `FoldReads` or comparer failures (`Collections.fs`, `Combinators.fs`).**

Models the Stage B direct collection-row contribution path from `codex-astra-plan.md` §3:
- When `item_sig_by_gen !! rc_gen cell = Some cell`, `read_direct_row cell` equals `read_indirect_row item_sig_by_gen (rc_gen cell)`.
- Under the same generation match, `fold_direct_step` produces the identical row state `(fra_gen, fra_has_value, fra_value, fra_err)` and fold accumulator state `(fas_state, fas_valued, fas_failed, fas_full, fas_moved)` as `fold_indirect_step` — both when `rc_gen cell = fra_gen r` (in-place update) and when `rc_gen cell <> fra_gen r` (remove + re-add triggering detach and re-attach).
- Writing an unchanged or batch-restored value (`eq_v (fra_value r) (rc_signal_val cell) = Some true` with `rc_status = StNone`, `fra_has_value = true`, `fra_err = None`, `rc_gen = fra_gen`) leaves both `r` and `fs` untouched (`fas_moved` unchanged, no reducer call).
- For an invertible, live, unfailed changed row (`rc_status = StNone`, `fra_has_value = true`, `fra_err = None`, `eq_v (fra_value r) (rc_signal_val cell) = Some false`), the replacement adapter computes `sub_fn (fas_state fs) ext (fra_value r) ≫= fun s' => add_fn s' ext (rc_signal_val cell)` under the external `FoldReads` context `ext`, setting `fas_full := true` if either fails.

- [invertible:] whether `ProjectionFold` has an invertible `subtract` function.
- [zero_s:] the fold's initial accumulator seed.
- [eq_v:] the value equality comparer (`None` models a throwing comparer).
- [add_fn:] the fold's `add` function taking external `FoldReads` state `ext` (`None` models throwing).
- [sub_fn:] the fold's `subtract` function taking external `FoldReads` state `ext` (`None` models throwing).
- [ext:] external `FoldReads` signal environment observed during `StepWith`.
- [default_v:] default value stored on detached rows.
- [item_sig_by_gen:] collection item signal table indexed by entry generation.
- [cell:] direct collection row cell for the live entry.
- [r:] the fold row's accepted contribution state.
- [fs:] the fold's aggregate accumulator state.
*)
Theorem direct_collection_row_fold_eq
    {Acc V E : Type}
    (invertible : bool) (zero_s : Acc)
    (eq_v : V -> V -> option bool)
    (add_fn sub_fn : Acc -> E -> V -> option Acc)
    (ext : E) (default_v : V)
    (item_sig_by_gen : gmap nat (col_row_cell V))
    (cell : col_row_cell V)
    (r : fold_row_acc V) (fs : fold_acc_state Acc) :
  item_sig_by_gen !! rc_gen cell = Some cell ->
  read_direct_row cell = read_indirect_row item_sig_by_gen (rc_gen cell) /\
  fold_direct_step invertible zero_s eq_v add_fn sub_fn ext default_v cell r fs =
    fold_indirect_step invertible zero_s eq_v add_fn sub_fn ext default_v item_sig_by_gen (rc_gen cell) r fs /\
  (rc_gen cell = fra_gen r -> rc_status cell = StNone -> fra_has_value r = true -> fra_err r = None ->
   eq_v (fra_value r) (rc_signal_val cell) = Some true ->
   fold_direct_step invertible zero_s eq_v add_fn sub_fn ext default_v cell r fs = (r, fs)) /\
  (rc_gen cell = fra_gen r -> rc_status cell = StNone -> fra_has_value r = true -> fra_err r = None ->
   eq_v (fra_value r) (rc_signal_val cell) = Some false -> invertible = true -> fas_full fs = false ->
   let fs_expected :=
     match sub_fn (fas_state fs) ext (fra_value r) ≫= fun s' => add_fn s' ext (rc_signal_val cell) with
     | Some s'' => mk_fold_acc s'' (fas_valued fs) (fas_failed fs) false true
     | None => mk_fold_acc (fas_state fs) (fas_valued fs) (fas_failed fs) true (fas_moved fs)
     end in
   fold_direct_step true zero_s eq_v add_fn sub_fn ext default_v cell r fs =
     (mk_fold_row_acc (fra_gen r) true (rc_signal_val cell) None, fs_expected)).
Proof.
  intro Hcell.
  assert (Hread : read_direct_row cell = read_indirect_row item_sig_by_gen (rc_gen cell)).
  { unfold read_direct_row, read_indirect_row. rewrite Hcell. reflexivity. }
  split_and!.
  - exact Hread.
  - unfold fold_direct_step, fold_indirect_step. rewrite Hread. reflexivity.
  - intros Hgen Hst Hhas Herr Heq.
    unfold fold_direct_step. rewrite decide_True by exact Hgen.
    unfold fold_apply_reading, read_direct_row. rewrite Hst.
    unfold fold_unfail_row. rewrite Herr, Hhas. simpl.
    rewrite Heq. destruct r as [g hv val err]; simpl in *; subst; reflexivity.
  - intros Hgen Hst Hhas Herr Heq Hinv Hfull fs_expected.
    unfold fold_direct_step. rewrite decide_True by exact Hgen.
    unfold fold_apply_reading, read_direct_row. rewrite Hst.
    unfold fold_unfail_row. rewrite Herr, Hhas. simpl.
    rewrite Heq. unfold fold_step_with. rewrite Hfull.
    subst fs_expected.
    destruct (sub_fn (fas_state fs) ext (fra_value r)) as [s'|]; simpl;
      [destruct (add_fn s' ext (rc_signal_val cell)); rewrite Herr; reflexivity | rewrite Herr; reflexivity].
Qed.

(** ** ProjectionFold clean-ready peek and SortView certified strict-order check (B15) *)

(**md
**An untracked read (`ProjectionFold.Read` `Peek`) on a clean, ready (`StNone`, `stale_live = false`) memo returns `Ok (n_value d)` in identical state `s` with zero edge mutations, trace emissions, or refresh allocations.**

Models the `Clean /\ StNone` `Peek` fast path in `ProjectionFold.Read` (`Combinators.fs`): because `read (S (S f)) m false s` runs `read_node (S f) m` inside `untracked`, `g_current` is `None` during `track m` (so `live_current = None` and no dependency edge is added), `stale_live d = false` bypasses `refresh_or_pull`, and `n_status d = StNone` directly returns `Ok (n_value d)` while restoring `s` verbatim.

- [f:] extra fuel beyond the outer `read`/`read_node` frame.
- [m:] memo node id being peeked.
- [d:] clean ready memo node snapshot at `m`.
- [s:] starting engine state.
*)
Theorem clean_ready_row_peek_refines_untracked_read
    (f : nat) (m : node_id) (d : node) (s : state) :
  g_nodes s !! m = Some d ->
  n_kind d = KMemo ->
  stale_live d = false ->
  n_status d = StNone ->
  read (S (S f)) m false s = Some (s, Ok (n_value d)).
Proof.
  intros Hm Hk Hstale Hst.
  cbn [read read_node].
  unfold untracked, m_bind, m_modify, m_return, get_node, track, live_current.
  simpl. rewrite Hm, Hk, Hstale. simpl. rewrite Hm, Hst. simpl.
  destruct s; reflexivity.
Qed.

(** Computable pairwise `rank_up_le` adjacency check matching `SortView`'s linear strict-order scan. *)
Fixpoint is_rank_up_sorted (xs : list (nat * Z * nat)) : bool :=
  match xs with
  | [] => true
  | [_] => true
  | x :: ((y :: _) as ys) => rank_up_le_b x y && is_rank_up_sorted ys
  end.

Lemma rank_up_le_b_true_iff x y :
  rank_up_le_b x y = true <-> rank_up_le x y.
Proof.
  unfold rank_up_le_b, rank_up_le.
  apply bool_decide_eq_true.
Qed.

Lemma is_rank_up_sorted_locally ys :
  is_rank_up_sorted ys = true ->
  LocallySorted rank_up_le ys.
Proof.
  induction ys as [|a l IH]; [constructor|].
  destruct l as [|b l'].
  - intros _. constructor.
  - simpl. intros H. apply andb_true_iff in H as [Hab Hl'].
    constructor.
    + exact (IH Hl').
    + apply rank_up_le_b_true_iff. exact Hab.
Qed.

(**md
**When `SortView`'s linear order scan confirms every adjacent pair `(rank, up_idx)` is ordered under `rank_up_le` (`LocallySorted` / `Sorted`), `full_insertion_sort` is the identity.**

Models the certified strict-order fast path for sorted reactive collections: if incoming `(rank, up_idx)` pairs already satisfy pairwise `rank_up_le` (whether witnessed by the computable scan `is_rank_up_sorted`, adjacent `LocallySorted rank_up_le`, or standard `Sorted rank_up_le`), `full_insertion_sort xs` leaves `xs` unchanged, justifying skipping insertion-sort allocations altogether.

- [xs:] list of `(item_id, rank, up_idx)` rows ordered under `rank_up_le`.
*)
Theorem certified_strict_order_skips_sort
    (xs : list (nat * Z * nat)) :
  (LocallySorted rank_up_le xs -> full_insertion_sort xs = xs) /\
  (Sorted rank_up_le xs -> full_insertion_sort xs = xs) /\
  (is_rank_up_sorted xs = true -> full_insertion_sort xs = xs).
Proof.
  assert (Hloc : forall l, LocallySorted rank_up_le l -> full_insertion_sort l = l).
  { induction l as [|x l IH]; intros Hls; [reflexivity|].
    destruct l as [|y ys]; [reflexivity|].
    inversion Hls; subst.
    unfold full_insertion_sort in *. simpl in *.
    rewrite IH by assumption.
    unfold sorted_insert.
    destruct (rank_up_le_b_true_iff x y) as [_ Htrue].
    rewrite (Htrue ltac:(assumption)). reflexivity. }
  repeat split.
  - exact (Hloc xs).
  - intros Hsort. apply Hloc. induction xs as [|x l IH].
    + constructor.
    + inversion Hsort; subst. destruct l as [|y ys]; [constructor|].
      inversion H2; subst. constructor; [exact (IH H1)|assumption].
  - intros Hbool. apply Hloc.
    eapply is_rank_up_sorted_locally. exact Hbool.
Qed.
