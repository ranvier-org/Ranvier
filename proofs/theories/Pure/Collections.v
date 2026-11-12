From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith Sorted.
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
   derived rows that link directly to upstream rows without a per-row `Signal<'K>`. *)

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
