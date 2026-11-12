From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import State Engine Scheduler.

Open Scope nat_scope.

(** * Graph concurrency, thread affinity, and inbox contracts (pure tier)

This module proves the exact minimum specifications for Ranvier's thread-affinity gates
(`ThreadAffinity.Guarded`, `ThreadAffinity.Unchecked`, `ThreadAffinity.Serialised`), the
reentrant `Serialised` holder protocol (`Acquire` / `Release` / `Entered`), the cross-thread
inbox handover (`PostQueued`, `Release`, `Pump`, `Drain`), and `PendingDispatch` three-state
atomic claim (`TryClaim` / `Cancel` / `Abandon`) in `Core.fs` (lines 1608–1629, 2520–3087). *)

(** ** 1. Thread-affinity gate and `Serialised` reentrant holder lock *)

Inductive thread_affinity :=
  | AffGuarded
  | AffUnchecked
  | AffSerialised.

(** Concurrency control state of a `Graph` (`Core.fs` lines 2242–2264, 2345). *)
Record concurrency_state := {
  cs_affinity : thread_affinity;
  cs_owner_thread : nat;
  cs_context : nat;
  cs_holder : nat;          (** `0` when free, positive thread id `me > 0` when held *)
  cs_inbox : list nat;      (** queued work item ids in FIFO arrival order *)
  cs_pump_posted : bool     (** whether a dispatcher drain wake is pending *)
}.

(** `Graph.IsOnGraphThread` (`Core.fs` lines 2734–2742). *)
Definition is_on_graph_thread (me : nat) (cs : concurrency_state) : bool :=
  match cs_affinity cs with
  | AffSerialised => bool_decide (cs_holder cs = me)
  | _ => bool_decide (me = cs_owner_thread cs)
  end.

(** Result of `Graph.Enter` (`Core.fs` lines 2527–2573):
    - `None`: raises `InvalidOperationException` (off-thread under `Guarded`, wrong context or
      concurrent holder under `Serialised`) before any graph state is touched;
    - `Some (cs', must_release)`: succeeds, returning the updated concurrency state and whether
      the caller acquired a free `Serialised` graph and must pair the entry with `Release`. *)
Definition graph_enter (me ctx : nat) (cs : concurrency_state) : option (concurrency_state * bool) :=
  match cs_affinity cs with
  | AffGuarded =>
      if decide (me = cs_owner_thread cs) then Some (cs, false)
      else None
  | AffUnchecked =>
      Some (cs, false)
  | AffSerialised =>
      if decide (cs_holder cs = me) then Some (cs, false)
      else if decide (ctx = cs_context cs /\ cs_holder cs = 0) then
        Some ({| cs_affinity := cs_affinity cs;
                 cs_owner_thread := cs_owner_thread cs;
                 cs_context := cs_context cs;
                 cs_holder := me;
                 cs_inbox := cs_inbox cs;
                 cs_pump_posted := cs_pump_posted cs |}, true)
      else None
  end.

(** `Graph.Release` (`Core.fs` lines 2545–2554): clears `holder` to `0` and, if `cs_inbox` is
    non-empty, posts a dispatcher drain (`cs_pump_posted := true`). *)
Definition graph_release (cs : concurrency_state) : concurrency_state :=
  {| cs_affinity := cs_affinity cs;
     cs_owner_thread := cs_owner_thread cs;
     cs_context := cs_context cs;
     cs_holder := 0;
     cs_inbox := cs_inbox cs;
     cs_pump_posted := if decide (cs_inbox cs = []) then cs_pump_posted cs else true |}.

(**md
**Under `ThreadAffinity.Guarded`, `graph_enter` succeeds if and only if the calling thread is `cs_owner_thread`, leaving the state unchanged on failure.**

Matches `Graph.Enter` and `Graph.AssertOnGraphThread` (`Core.fs` lines 2566–2570, 3028–3033):
any off-thread call raises `InvalidOperationException` before mutating the graph, based on thread
identity alone regardless of `SynchronizationContext`.

- [me:] the calling thread id.
- [ctx:] the calling thread's `SynchronizationContext` id.
- [cs:] the concurrency state with `cs_affinity cs = AffGuarded`.
*)
Theorem guarded_enter_iff_owner (me ctx : nat) (cs : concurrency_state) :
  cs_affinity cs = AffGuarded ->
  (graph_enter me ctx cs = Some (cs, false) <-> me = cs_owner_thread cs) /\
  (me <> cs_owner_thread cs <-> graph_enter me ctx cs = None).
Proof.
  intros Haff. unfold graph_enter. rewrite Haff.
  destruct (decide (me = cs_owner_thread cs)) as [Heq|Hne].
  - split; split; intros; [done|done|contradiction|discriminate].
  - split; split; intros; [discriminate|contradiction|done|done].
Qed.

(**md
**Under `ThreadAffinity.Serialised`, `graph_enter` grants exclusive ownership to a free caller on the construction context (`must_release = true`), allows reentrant entry by the current holder (`must_release = false`), and rejects any concurrent thread or wrong context.**

Matches `Graph.Acquire` and `Graph.Enter` (`Core.fs` lines 2527–2540, 2572, 3054–3086):
- If `cs_holder cs = me`, a nested entry succeeds with `must_release = false` and keeps `cs_holder = me`.
- If `cs_holder cs = 0` and `ctx = cs_context cs`, the outermost entry sets `cs_holder = me` and returns `must_release = true`.
- If `cs_holder cs` holds another thread (`cs_holder cs <> 0 /\ cs_holder cs <> me`) or `ctx <> cs_context cs`, entry fails (`None`).

- [me:] the calling thread id (`me > 0`).
- [ctx:] the calling thread's `SynchronizationContext` id.
- [cs:] the concurrency state with `cs_affinity cs = AffSerialised`.
*)
Theorem serialised_acquire_mutual_exclusion (me ctx : nat) (cs : concurrency_state) :
  cs_affinity cs = AffSerialised ->
  me > 0 ->
  (cs_holder cs = me ->
     graph_enter me ctx cs = Some (cs, false)) /\
  (cs_holder cs = 0 -> ctx = cs_context cs ->
     exists cs',
       graph_enter me ctx cs = Some (cs', true) /\
       cs_holder cs' = me /\
       is_on_graph_thread me cs' = true) /\
  ((cs_holder cs <> me /\ (cs_holder cs <> 0 \/ ctx <> cs_context cs)) ->
     graph_enter me ctx cs = None).
Proof.
  intros Haff Hme. unfold graph_enter. rewrite Haff. split_and!.
  - intros Hhold. rewrite decide_True by done. reflexivity.
  - intros Hfree Hctx.
    rewrite decide_False by lia.
    rewrite decide_True by done.
    eexists. split_and!; [done|done|].
    unfold is_on_graph_thread; simpl.
    by apply bool_decide_eq_true_2.
  - intros [Hne Hbad].
    rewrite decide_False by done.
    rewrite decide_False; [done|].
    intros [Hctx Hfree]. destruct Hbad; contradiction.
Qed.

(**md
**A nested (`reentrant`) entry on the holding thread leaves `must_release = false`, and only the outermost `Entered` scope releases `cs_holder` back to `0`.**

Certifies that `Entered` (`if this.Enter operation then try body () finally this.Release () else body ()`)
never releases `holder` prematurely during a nested call on the same thread.

- [me:] the holding thread id (`me > 0`).
- [ctx:] the construction context id.
- [cs:] the free `Serialised` state before the outer entry.
*)
Theorem serialised_reentrant_preserves_holder (me ctx : nat) (cs : concurrency_state) :
  cs_affinity cs = AffSerialised ->
  cs_holder cs = 0 ->
  ctx = cs_context cs ->
  me > 0 ->
  exists cs_outer,
    graph_enter me ctx cs = Some (cs_outer, true) /\
    graph_enter me ctx cs_outer = Some (cs_outer, false) /\
    cs_holder cs_outer = me /\
    cs_holder (graph_release cs_outer) = 0.
Proof.
  intros Haff Hfree Hctx Hme.
  unfold graph_enter. rewrite Haff.
  rewrite decide_False by lia.
  rewrite decide_True by done.
  eexists. split_and!; [done| |done|done].
  simpl. rewrite decide_True by done. reflexivity.
Qed.

(** ** 2. No-lost-drain handover between `PostQueued`, `PumpFromDispatcher`, and `Release` *)

(** `Graph.PostQueued` (`Core.fs` lines 2772–2788): enqueues `w` in `cs_inbox` and posts a
    dispatcher drain (`cs_pump_posted := true`). *)
Definition post_queued (w : nat) (cs : concurrency_state) : concurrency_state :=
  {| cs_affinity := cs_affinity cs;
     cs_owner_thread := cs_owner_thread cs;
     cs_context := cs_context cs;
     cs_holder := cs_holder cs;
     cs_inbox := cs_inbox cs ++ [w];
     cs_pump_posted := true |}.

(** `Graph.PumpFromDispatcher` under `Serialised` (`Core.fs` lines 3002–3014):
    - if `cs_holder = 0`, acquires `holder := me`, drains `cs_inbox`, and releases;
    - if `cs_holder <> 0` (another thread holds the graph), the CAS `CompareExchange(&holder, me, 0)`
      fails and consumes the dispatcher wake (`cs_pump_posted := false`), leaving the inbox for the
      holding thread's `Release()` to re-post. *)
Definition pump_from_dispatcher_contended (cs : concurrency_state) : concurrency_state :=
  {| cs_affinity := cs_affinity cs;
     cs_owner_thread := cs_owner_thread cs;
     cs_context := cs_context cs;
     cs_holder := cs_holder cs;
     cs_inbox := cs_inbox cs;
     cs_pump_posted := false |}.

(** Drain availability invariant: whenever `cs_inbox` is non-empty, either a dispatcher wake is
    currently posted (`cs_pump_posted = true`) or a thread currently holds the `Serialised` graph
    (`cs_holder > 0`, whose `graph_release` will post a wake upon exit). *)
Definition drain_guaranteed (cs : concurrency_state) : Prop :=
  cs_inbox cs <> [] -> cs_pump_posted cs = true \/ cs_holder cs > 0.

(**md
**The `PostQueued` / `PumpFromDispatcher` / `Release` protocol preserves `drain_guaranteed` and re-posts a drain on exit whenever a contended pump was skipped.**

Proves the handover comment at `Core.fs` lines 2547 and 3009 (a full fence ensures the inbox check
observes every enqueue whose drain found the graph held by the exiting thread):
1. `post_queued` establishes `drain_guaranteed`.
2. If `PumpFromDispatcher` runs while another thread holds the graph (`cs_holder cs > 0`), skipping the
   pump (`pump_from_dispatcher_contended`) preserves `drain_guaranteed` because `cs_holder > 0`.
3. When the holding thread subsequently calls `graph_release`, `cs_pump_posted` is guaranteed to be `true`
   whenever `cs_inbox` is non-empty, so no queued work item is ever stranded.

- [w:] a work item id enqueued via `PostQueued`.
- [cs:] the concurrency state.
*)
Theorem serialised_release_reposts_nonempty_inbox (w : nat) (cs : concurrency_state) :
  drain_guaranteed (post_queued w cs) /\
  (cs_holder cs > 0 ->
     drain_guaranteed (pump_from_dispatcher_contended (post_queued w cs)) /\
     cs_pump_posted (graph_release (pump_from_dispatcher_contended (post_queued w cs))) = true /\
     cs_inbox (graph_release (pump_from_dispatcher_contended (post_queued w cs))) = cs_inbox cs ++ [w]).
Proof.
  split.
  - intros _. left. reflexivity.
  - intros Hheld.
    assert (Hne : cs_inbox cs ++ [w] <> []).
    { intros Hnil. apply app_eq_nil in Hnil as [_ Hnil]. discriminate. }
    split_and!.
    + intros _. right. simpl. lia.
    + unfold graph_release, pump_from_dispatcher_contended, post_queued; simpl.
      rewrite decide_False by exact Hne. reflexivity.
    + reflexivity.
Qed.

(** ** 3. Fault-isolated FIFO inbox drain (`Graph.Drain`) *)

(** Outcome of executing a single inbox work item (`unit -> unit` in `Graph.Drain`, `Core.fs` lines 2972–2988):
    either returns normally (producing state update `eff`) or throws exception `err` (which `Drain` catches
    and passes to `root.RecordError err` so remaining inbox items still run). *)
Inductive inbox_item_outcome :=
  | InboxOk (eff : nat)
  | InboxThrow (err : Z).

Record drain_state := {
  ds_inbox : list inbox_item_outcome;
  ds_applied : list nat;
  ds_root_errors : list Z;
  ds_flush_requested : bool
}.

Fixpoint drain_inbox_loop (items : list inbox_item_outcome) (applied : list nat) (errs : list Z)
    : list nat * list Z * nat :=
  match items with
  | [] => (applied, errs, 0)
  | InboxOk eff :: rest =>
      let '(app', errs', ran) := drain_inbox_loop rest (applied ++ [eff]) errs in
      (app', errs', S ran)
  | InboxThrow err :: rest =>
      let '(app', errs', ran) := drain_inbox_loop rest applied (errs ++ [err]) in
      (app', errs', S ran)
  end.

(** `Graph.Drain` (`Core.fs` lines 2953–2993): drains every item in `ds_inbox` in FIFO arrival order,
    records any thrown exception in `ds_root_errors`, requests a flush iff `ran > 0`, and returns `ran`. *)
Definition graph_drain_inbox (ds : drain_state) : drain_state * nat :=
  let '(app', errs', ran) := drain_inbox_loop (ds_inbox ds) (ds_applied ds) (ds_root_errors ds) in
  ({| ds_inbox := [];
      ds_applied := app';
      ds_root_errors := errs';
      ds_flush_requested := if decide (0 < ran) then true else ds_flush_requested ds |},
   ran).

Lemma drain_inbox_loop_spec items applied errs :
  drain_inbox_loop items applied errs =
  (applied ++ omap (fun it => match it with InboxOk e => Some e | InboxThrow _ => None end) items,
   errs ++ omap (fun it => match it with InboxOk _ => None | InboxThrow ex => Some ex end) items,
   length items).
Proof.
  revert applied errs.
  induction items as [|[eff|err] rest IH]; intros applied errs; simpl.
  - rewrite !app_nil_r. reflexivity.
  - rewrite IH. rewrite <- app_assoc. reflexivity.
  - rewrite IH. rewrite <- app_assoc. reflexivity.
Qed.

(**md
**`graph_drain_inbox` drains every item in FIFO arrival order, isolates throwing items into `ds_root_errors` without stranding later items, and requests a flush whenever at least one item ran.**

Matches `Graph.Drain` (`Core.fs` lines 2972–2988, where one bad continuation must not strand the rest of the
inbox, for the same reason one bad effect must not strand the flush queue):
- Returns `ran = length (ds_inbox ds)` and empties `ds_inbox`.
- Appends every `InboxOk eff` in arrival order to `ds_applied`.
- Appends every `InboxThrow err` in arrival order to `ds_root_errors`.
- Sets `ds_flush_requested = true` whenever `ds_inbox ds <> []`.

- [ds:] the inbox drain state before `Pump` / `Drain`.
*)
Theorem inbox_drain_fifo_fault_isolated (ds : drain_state) :
  let '(ds', ran) := graph_drain_inbox ds in
  ran = length (ds_inbox ds) /\
  ds_inbox ds' = [] /\
  ds_applied ds' =
    ds_applied ds ++ omap (fun it => match it with InboxOk e => Some e | InboxThrow _ => None end) (ds_inbox ds) /\
  ds_root_errors ds' =
    ds_root_errors ds ++ omap (fun it => match it with InboxOk _ => None | InboxThrow ex => Some ex end) (ds_inbox ds) /\
  (ds_inbox ds <> [] -> ds_flush_requested ds' = true).
Proof.
  unfold graph_drain_inbox.
  rewrite drain_inbox_loop_spec. simpl.
  split_and!; [done|done|done|done|].
  intros Hne. destruct (ds_inbox ds) as [|x xs]; [contradiction|done].
Qed.

(** ** 4. `PendingDispatch` three-state atomic claim (`DispatchAsync`) *)

(** State of a `PendingDispatch<'T>` item (`Core.fs` lines 1608–1629, 2892–2930):
    - `0` (`PdWaiting`): waiting in the inbox;
    - `1` (`PdClaimed`): claimed by the inbox drain (`TryClaim` succeeded);
    - `2` (`PdClosed`): cancelled via `CancellationToken` or abandoned by `Graph.Dispose`. *)
Definition pd_state := nat.

Definition pd_try_claim (st : pd_state) : pd_state * bool :=
  if decide (st = 0) then (1, true) else (st, false).

Definition pd_try_close (st : pd_state) : pd_state * bool :=
  if decide (st = 0) then (2, true) else (st, false).

(**md
**Exactly one of `pd_try_claim` (drain execution), `pd_try_close` (cancellation), or `pd_try_close` (graph disposal) succeeds from `PdWaiting` (`0`), and all subsequent attempts are no-ops (`false`).**

Proves the `PendingDispatch<'T>` contract (`Core.fs` lines 1604–1629):
- Starting from `st = 0`, the first transition (`pd_try_claim` or `pd_try_close`) returns `true` and moves
  `st` to `1` or `2`.
- Once `st` is `1` (claimed by the drain) or `2` (cancelled or abandoned on disposal), both `pd_try_claim`
  and `pd_try_close` return `(st, false)`, so a cancelled/abandoned item is skipped by the drain and a
  claimed item cannot be cancelled or faulted by disposal.

- [st:] the `PendingDispatch` state.
*)
Theorem pending_dispatch_claim_exclusive :
  pd_try_claim 0 = (1, true) /\
  pd_try_close 0 = (2, true) /\
  (forall st, st <> 0 -> pd_try_claim st = (st, false) /\ pd_try_close st = (st, false)) /\
  snd (pd_try_close (fst (pd_try_claim 0))) = false /\
  snd (pd_try_claim (fst (pd_try_close 0))) = false /\
  snd (pd_try_close (fst (pd_try_close 0))) = false.
Proof.
  unfold pd_try_claim, pd_try_close.
  split_and!; try done.
  intros st Hne. rewrite decide_False by done. done.
Qed.
