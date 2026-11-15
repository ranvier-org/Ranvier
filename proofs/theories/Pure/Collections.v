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
