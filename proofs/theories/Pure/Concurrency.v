From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import SourceList State Engine Scheduler.

Open Scope nat_scope.

(**md # Graph concurrency, thread affinity, and inbox contracts (pure tier)

This module proves the exact minimum specifications for Ranvier's thread-affinity gates
(`ThreadAffinity.Guarded`, `ThreadAffinity.Unchecked`, `ThreadAffinity.Serialised`), the
reentrant `Serialised` holder protocol (`Acquire` / `Release` / `Entered`), the cross-thread
inbox handover (`PostQueued`, `Release`, `Pump`, `Drain`), and `PendingDispatch` three-state
atomic claim (`TryClaim` / `Cancel` / `Abandon`) in `Core.fs` (lines 1747–1805, 2427–3307). *)

(** ** 1. Thread-affinity gate and Serialised reentrant holder lock *)

Inductive thread_affinity :=
  | AffGuarded
  | AffUnchecked
  | AffSerialised.

(**md Concurrency control state of a `Graph` (`Core.fs` lines 2427–2478, 2546). *)
Record concurrency_state := {
  cs_affinity : thread_affinity;
  cs_owner_thread : nat;
  cs_context : nat;
  cs_holder : nat;          (* 0 when free, positive thread id me > 0 when held *)
  cs_inbox : list nat;      (* queued work item ids in FIFO arrival order *)
  cs_pump_posted : bool     (* whether a dispatcher drain wake is pending *)
}.

(**md `Graph.IsOnGraphThread` (`Core.fs` lines 2954–2962). *)
Definition is_on_graph_thread (me : nat) (cs : concurrency_state) : bool :=
  match cs_affinity cs with
  | AffSerialised => bool_decide (cs_holder cs = me)
  | _ => bool_decide (me = cs_owner_thread cs)
  end.

(**md Result of `Graph.Enter` (`Core.fs` lines 2736–2793):

- `None`: raises `InvalidOperationException` (off-thread under `Guarded`, wrong context or
  concurrent holder under `Serialised`) before any graph state is touched;
- `Some (cs', must_release)`: succeeds, returning the updated concurrency state and whether
  the caller acquired a free `Serialised` graph and must pair the entry with `Release`.
*)
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

(**md `Graph.Release` (`Core.fs` lines 2754–2763): clears `holder` to `0` and, if `cs_inbox` is
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

Matches `Graph.Enter` and `Graph.AssertOnGraphThread` (`Core.fs` lines 2786–2790, 3248–3253):
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

Matches `Graph.Acquire` and `Graph.Enter` (`Core.fs` lines 2736–2749, 2792, 3274–3306):
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

(** ** 2. No-lost-drain handover between PostQueued, PumpFromDispatcher, and Release *)

(**md `Graph.PostQueued` (`Core.fs` lines 2992–3008): enqueues `w` in `cs_inbox` and posts a
    dispatcher drain (`cs_pump_posted := true`). *)
Definition post_queued (w : nat) (cs : concurrency_state) : concurrency_state :=
  {| cs_affinity := cs_affinity cs;
     cs_owner_thread := cs_owner_thread cs;
     cs_context := cs_context cs;
     cs_holder := cs_holder cs;
     cs_inbox := cs_inbox cs ++ [w];
     cs_pump_posted := true |}.

(**md `Graph.PumpFromDispatcher` under `Serialised` (`Core.fs` lines 3215–3235):

- if `cs_holder = 0`, acquires `holder := me`, drains `cs_inbox`, and releases;
- if `cs_holder <> 0` (another thread holds the graph), the CAS `CompareExchange(&holder, me, 0)`
  fails and consumes the dispatcher wake (`cs_pump_posted := false`), leaving the inbox for the
  holding thread's `Release()` to re-post.
*)
Definition pump_from_dispatcher_contended (cs : concurrency_state) : concurrency_state :=
  {| cs_affinity := cs_affinity cs;
     cs_owner_thread := cs_owner_thread cs;
     cs_context := cs_context cs;
     cs_holder := cs_holder cs;
     cs_inbox := cs_inbox cs;
     cs_pump_posted := false |}.

(**md Drain availability invariant: whenever `cs_inbox` is non-empty, either a dispatcher wake is
    currently posted (`cs_pump_posted = true`) or a thread currently holds the `Serialised` graph
    (`cs_holder > 0`, whose `graph_release` will post a wake upon exit). *)
Definition drain_guaranteed (cs : concurrency_state) : Prop :=
  cs_inbox cs <> [] -> cs_pump_posted cs = true \/ cs_holder cs > 0.

(**md
**The `PostQueued` / `PumpFromDispatcher` / `Release` protocol preserves `drain_guaranteed` and re-posts a drain on exit whenever a contended pump was skipped.**

Proves the handover comment at `Core.fs` lines 2756 and 3229 (a full fence ensures the inbox check
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

(** ** 3. Fault-isolated FIFO inbox drain (Graph.Drain) *)

(**md Outcome of executing a single inbox work item (`unit -> unit` in `Graph.Drain`, `Core.fs` lines 3191–3208):
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

(**md `Graph.Drain` (`Core.fs` lines 3173–3213): drains every item in `ds_inbox` in FIFO arrival order,
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

Matches `Graph.Drain` (`Core.fs` lines 3198–3205, where one bad continuation must not strand the rest of the
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

(** ** 4. PendingDispatch three-state atomic claim (DispatchAsync) *)

(**md State of a `PendingDispatch<'T>` item (`Core.fs` lines 1747–1775, 3070–3140):

- `0` (`PdWaiting`): waiting in the inbox;
- `1` (`PdClaimed`): claimed by the inbox drain (`TryClaim` succeeded);
- `2` (`PdClosed`): cancelled via `CancellationToken` or abandoned by `Graph.Dispose`.
*)
Definition pd_state := nat.

Definition pd_try_claim (st : pd_state) : pd_state * bool :=
  if decide (st = 0) then (1, true) else (st, false).

Definition pd_try_close (st : pd_state) : pd_state * bool :=
  if decide (st = 0) then (2, true) else (st, false).

(**md
**Exactly one of `pd_try_claim` (drain execution), `pd_try_close` (cancellation), or `pd_try_close` (graph disposal) succeeds from `PdWaiting` (`0`), and all subsequent attempts are no-ops (`false`).**

Proves the `PendingDispatch<'T>` contract (`Core.fs` lines 1747–1775):
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
  split_and!.
  - reflexivity.
  - reflexivity.
  - intros st Hne. split; rewrite decide_False by done; reflexivity.
  - reflexivity.
  - reflexivity.
  - reflexivity.
Qed.

(** ** 5. Hot/cold fast-path decompositions for Memo.Value and Signal.Value *)

(**md Byte encoding of `Freshness` (`Core.fs` lines 29–44). *)
Definition freshness_byte (fr : freshness) : Z :=
  match fr with Clean => 0%Z | Check => 1%Z | Dirty => 2%Z end.

(**md Bit-flag encoding of `Status` (`Types.fs` lines 11–19): `None = 0`, `Pending = 1`, `Error = 2`,
    `Uninitialized = 4`, plus optional `Stale = 8` (`stale_flag = true`). *)
Definition status_byte (st : status) (stale_flag : bool) : Z :=
  (match st with
   | StNone => 0%Z
   | StPending => 1%Z
   | StError => 2%Z
   | StUninitialized => 4%Z
   end + if stale_flag then 8%Z else 0%Z)%Z.

(**md `StatusBits.isCleanAndReady` (`Core.fs` lines 172–173): checks `((byte freshness) ||| (byte status &&& 3uy)) = 0uy`. *)
Definition is_clean_and_ready_bits (fr : freshness) (st : status) (stale_flag : bool) : bool :=
  bool_decide (Z.lor (freshness_byte fr) (Z.land (status_byte st stale_flag) 3%Z) = 0%Z).

(**md
**`is_clean_and_ready_bits` holds if and only if freshness is `Clean` and status has neither `Pending` nor `Error` set, independent of `Uninitialized` and `Stale` bits.**

Matches `StatusBits.isCleanAndReady` (`Core.fs` lines 172–173): masking `status` with `3uy` ignores both
`Status.Uninitialized` (`4uy`) and `Status.Stale` (`8uy`) while rejecting `Status.Pending` (`1uy`) and `Status.Error` (`2uy`).

- [fr:] the node's `Freshness`.
- [st:] the node's readiness/error/uninitialized `Status`.
- [stale_flag:] whether the orthogonal `Status.Stale` (`8uy`) bit is set.
*)
Theorem clean_and_ready_bits_iff (fr : freshness) (st : status) (stale_flag : bool) :
  is_clean_and_ready_bits fr st stale_flag = true <->
  (fr = Clean /\ st <> StPending /\ st <> StError).
Proof.
  unfold is_clean_and_ready_bits, freshness_byte, status_byte.
  destruct fr, st, stale_flag.
  all: split; [intro H; vm_compute in H; try discriminate; repeat split; discriminate |].
  all: intros [Hfr [Hp He]]; try discriminate Hfr;
    try (exfalso; exact (Hp eq_refl));
    try (exfalso; exact (He eq_refl));
    vm_compute; reflexivity.
Qed.

Lemma track_preserves_nodes (n : node_id) (s s' : state) :
  track n s = Some (s', tt) -> g_nodes s' = g_nodes s.
Proof.
  unfold track.
  destruct (live_current s) as [c|]; [|intros [= <-]; reflexivity].
  destruct (sources_add c n (g_edges s)) as [w|]; [|discriminate].
  intros [= <-].
  unfold ghost_read.
  destruct (g_nodes (with_edges w s) !! n); destruct s; reflexivity.
Qed.

(**md Fast/slow decomposition of `Memo.Value` (`Core.fs` lines 5468–5504): inspects
    `is_clean_and_ready_bits` first, tracks and returns the cached value on hit, and otherwise delegates
    to `read_node`. *)
Definition read_memo_fast (fuel : nat) (n : node_id) : M outcome :=
  match fuel with
  | 0 => stuck
  | S f =>
    let* d := get_node n in
    match n_kind d with
    | KMemo =>
      if is_clean_and_ready_bits (n_freshness d) (n_status d) false then
        let* _ := track n in
        m_return (Ok (n_value d))
      else read_node (S f) n
    | _ => read_node (S f) n
    end
  end.

(**md
**Splitting `Memo.Value` into an inline `is_clean_and_ready_bits` fast path and a slow fallback preserves `read_node` and tracked `read` identically across all states.**

Matches the `#else` `.NET` split between `Memo.Value` (`get`) and `Memo.ReadSlow` (`Core.fs` lines 5468–5504).

- [f:] inner fuel budget for `read_node (S f)`.
- [n:] the target node id.
- [s:] the graph state before reading.
*)
Theorem memo_read_fast_split_eq (f : nat) (n : node_id) (s : state) :
  read_memo_fast (S f) n s = read_node (S f) n s /\
  read (S (S f)) n true s = read_memo_fast (S f) n s.
Proof.
  unfold read_memo_fast, m_bind, get_node. cbn [read].
  destruct (g_nodes s !! n) as [d|] eqn:Hnode.
  - destruct (n_kind d) eqn:Hk.
    + split; reflexivity.
    + destruct (is_clean_and_ready_bits (n_freshness d) (n_status d) false) eqn:Hfast.
      * apply clean_and_ready_bits_iff in Hfast as (Hfr & Hnp & Hne).
        assert (Hnsl : stale_live d = false) by (unfold stale_live; rewrite Hfr; reflexivity).
        cbn [read_node]. unfold m_bind, m_return, get_node. simpl. rewrite Hnode, Hk, Hnsl.
        destruct (track n s) as [[s' []]|] eqn:Htr; simpl.
        -- rewrite (track_preserves_nodes n s s' Htr), Hnode.
           destruct (n_status d); [split; reflexivity | split; reflexivity | exfalso; exact (Hne eq_refl) | split; reflexivity].
        -- split; reflexivity.
      * split; reflexivity.
    + split; reflexivity.
  - cbn [read_node]. unfold m_bind, get_node. simpl. rewrite Hnode. split; reflexivity.
Qed.

Definition signal_journal_record (v : val) (j : option (list Z)) : option (list Z) :=
  match j with
  | None => None
  | Some log => Some (log ++ [v])
  end.

(**md Reference `Entered`-composed `Signal.Value` setter (`#if FABLE_COMPILER`, `Core.fs` lines 4397–4427). *)
Definition signal_set_entered
    (me ctx fuel : nat) (n : node_id) (v : val)
    (cs : concurrency_state) (j : option (list Z)) (s : state)
    : option (concurrency_state * option (list Z) * state) :=
  match graph_enter me ctx cs with
  | None => None
  | Some (cs1, must_rel) =>
      let j1 := signal_journal_record v j in
      let cs2 := if must_rel then graph_release cs1 else cs1 in
      match write_signal fuel n v s with
      | None => None
      | Some (s', tt) => Some (cs2, j1, s')
      end
  end.

(**md Slow cold path for `Signal.Value` `set` after `Graph.Enter` returns `true` (`Signal.SetAfterAcquire`, `Core.fs` lines 4504–4515). *)
Definition signal_set_after_acquire
    (fuel : nat) (n : node_id) (v : val)
    (cs : concurrency_state) (j : option (list Z)) (s : state)
    : option (concurrency_state * option (list Z) * state) :=
  let cs_out := graph_release cs in
  match j with
  | None =>
      match g_nodes s !! n with
      | Some d =>
          match n_kind d with
          | KSignal =>
              if decide (n_value d = v) then Some (cs_out, None, s)
              else
                match write_signal fuel n v s with
                | Some (s', tt) => Some (cs_out, None, s')
                | None => None
                end
          | _ => None
          end
      | None => None
      end
  | Some log =>
      let j1 := Some (log ++ [v]) in
      match write_signal fuel n v s with
      | Some (s', tt) => Some (cs_out, j1, s')
      | None => None
      end
  end.

(**md Split `.NET` fast/cold decomposition of `Signal.Value` `set` (`Core.fs` lines 4429–4515). *)
Definition signal_set_fast_split
    (me ctx fuel : nat) (n : node_id) (v : val)
    (cs : concurrency_state) (j : option (list Z)) (s : state)
    : option (concurrency_state * option (list Z) * state) :=
  match graph_enter me ctx cs with
  | None => None
  | Some (cs1, false) =>
      match j with
      | None =>
          match g_nodes s !! n with
          | Some d =>
              match n_kind d with
              | KSignal =>
                  if decide (n_value d = v) then Some (cs1, None, s)
                  else
                    match write_signal fuel n v s with
                    | Some (s', tt) => Some (cs1, None, s')
                    | None => None
                    end
              | _ => None
              end
          | None => None
          end
      | Some log =>
          let j1 := Some (log ++ [v]) in
          match write_signal fuel n v s with
          | Some (s', tt) => Some (cs1, j1, s')
          | None => None
          end
      end
  | Some (cs1, true) =>
      signal_set_after_acquire fuel n v cs1 j s
  end.

Local Lemma write_signal_unjournaled_eq
    (f : nat) (n : node_id) (v : val) (cs_out : concurrency_state) (s : state) :
  (match write_signal (S f) n v s with
   | Some (s', tt) => Some (cs_out, None, s')
   | None => None
   end : option (concurrency_state * option (list Z) * state)) =
  match g_nodes s !! n with
  | Some d =>
      match n_kind d with
      | KSignal =>
          if decide (n_value d = v) then Some (cs_out, None, s)
          else
            match write_signal (S f) n v s with
            | Some (s', tt) => Some (cs_out, None, s')
            | None => None
            end
      | _ => None
      end
  | None => None
  end.
Proof.
  destruct (g_nodes s !! n) as [d|] eqn:Hnode.
  - destruct (n_kind d) eqn:Hk.
    + destruct (decide (n_value d = v)) as [Heq|Hne].
      * cbn [write_signal]. unfold m_bind, get_node, m_return. simpl.
        rewrite Hnode, Hk, decide_True; [reflexivity | exact Heq].
      * reflexivity.
    + cbn [write_signal]. unfold m_bind, get_node, stuck. simpl.
      rewrite Hnode, Hk. reflexivity.
    + cbn [write_signal]. unfold m_bind, get_node, stuck. simpl.
      rewrite Hnode, Hk. reflexivity.
  - cbn [write_signal]. unfold m_bind, get_node, stuck. simpl.
    rewrite Hnode. reflexivity.
Qed.

(**md
**Splitting `Signal.Value` `set` into inline `Enter` / unjournaled cutoff check and `SetChanged` / `SetJournaled` / `SetAfterAcquire` cold helpers refines the `Entered` setter identically and still enforces `Guarded` affinity before cutoff.**

Matches `Signal.Value` (`set`), `SetChanged`, `SetJournaled`, and `SetAfterAcquire` (`Core.fs` lines 4471–4515):
journaled equal writes still append to the journal, and off-thread equal writes under `ThreadAffinity.Guarded`
still fail (`None`) before any cutoff check is reached.

- [me:] the calling thread id.
- [ctx:] the calling thread's `SynchronizationContext` id.
- [f:] inner fuel budget for `write_signal (S f)`.
- [n:] the signal node id.
- [v:] the value being written.
- [cs:] the concurrency state before the write.
- [j:] `None` when unjournaled, or `Some log` for an attached journal log.
- [s:] the graph state before the write.
*)
Theorem signal_set_fast_split_refines
    (me ctx f : nat) (n : node_id) (v : val) (cs : concurrency_state) (j : option (list Z)) (s : state) :
  signal_set_fast_split me ctx (S f) n v cs j s = signal_set_entered me ctx (S f) n v cs j s /\
  (cs_affinity cs = AffGuarded -> me <> cs_owner_thread cs ->
   signal_set_fast_split me ctx f n v cs j s = None).
Proof.
  split.
  - unfold signal_set_fast_split, signal_set_after_acquire, signal_set_entered, signal_journal_record.
    destruct (graph_enter me ctx cs) as [[cs1 []]|]; [| |reflexivity].
    + destruct j as [log|]; [reflexivity | symmetry; apply write_signal_unjournaled_eq].
    + destruct j as [log|]; [reflexivity | symmetry; apply write_signal_unjournaled_eq].
  - intros Haff Hne.
    unfold signal_set_fast_split.
    destruct (guarded_enter_iff_owner me ctx cs Haff) as [_ Hiff].
    rewrite (proj1 Hiff Hne). reflexivity.
Qed.

(**md Outcome of invoking an `IEqualityComparer<'T>.Equals` implementation (`Core.fs` lines 4295–4323):
    returns `CmpBool b`, raises `NullReferenceException` (`CmpNullRef`), or throws a user exception (`CmpThrow err`). *)
Inductive cmp_outcome :=
  | CmpBool (b : bool)
  | CmpNullRef
  | CmpThrow (err : Z).

(**md Classification of the `IEqualityComparer<'T>` returned by `IEqualityPolicy.Comparer<'T>()` (`Core.fs` lines 4306–4313). *)
Inductive nullable_comparer :=
  | CmpNullInput
  | CmpDefaultInput
  | CmpCustomInput (f : Z -> Z -> cmp_outcome).

(**md Encoded `equal` field stored on `Signal` and `Memo` by `ComparerSentinel.encode` (`Core.fs` lines 4306–4313):
    `SentDefaultNull` represents `null` (signalling `EqualityComparer<'T>.Default.Equals`), `SentNullComparer`
    represents `NullComparer<'T>.Instance`, and `SentCustom f` stores a custom comparer unchanged. *)
Inductive comparer_sentinel :=
  | SentDefaultNull
  | SentNullComparer
  | SentCustom (f : Z -> Z -> cmp_outcome).

Definition eval_nullable_comparer (def_eq : Z -> Z -> bool) (c : nullable_comparer) (a b : Z) : cmp_outcome :=
  match c with
  | CmpNullInput => CmpNullRef
  | CmpDefaultInput => CmpBool (def_eq a b)
  | CmpCustomInput f => f a b
  end.

Definition encode_comparer_sentinel (c : nullable_comparer) : comparer_sentinel :=
  match c with
  | CmpNullInput => SentNullComparer
  | CmpDefaultInput => SentDefaultNull
  | CmpCustomInput f => SentCustom f
  end.

Definition eval_comparer_sentinel (def_eq : Z -> Z -> bool) (s : comparer_sentinel) (a b : Z) : cmp_outcome :=
  match s with
  | SentDefaultNull => CmpBool (def_eq a b)
  | SentNullComparer => CmpNullRef
  | SentCustom f => f a b
  end.

(**md
**Encoding `EqualityComparer<'T>.Default` as `null` and `null` as `NullComparer<'T>.Instance` preserves exact comparison and exception outcomes across default, custom, throwing, and faulty-null comparers.**

Matches `NullComparer<'T>` and `ComparerSentinel.encode` / `ComparerSentinel.equals` (`Core.fs` lines 4295–4323) and
`Signal.SetFast` / `Signal.SetCustom` (`Core.fs` lines 4453–4469):
- `EqualityComparer<'T>.Default` maps to `SentDefaultNull` (`null`), dispatching directly to `def_eq a b` without interface dispatch;
- a faulty policy returning `null` maps to `SentNullComparer` (`NullComparer<'T>.Instance`), still raising `NullReferenceException` (`CmpNullRef`) on comparison;
- any custom comparer (including one that throws) is preserved unchanged.

- [def_eq:] the intrinsic `EqualityComparer<'T>.Default.Equals` relation.
- [c:] the input comparer returned by `IEqualityPolicy.Comparer<'T>()`.
- [a:] the previous value.
- [b:] the candidate value.
*)
Theorem default_comparer_null_sentinel_eq
    (def_eq : Z -> Z -> bool) (c : nullable_comparer) (a b : Z) :
  eval_comparer_sentinel def_eq (encode_comparer_sentinel c) a b =
    eval_nullable_comparer def_eq c a b /\
  (encode_comparer_sentinel c = SentDefaultNull <-> c = CmpDefaultInput) /\
  (c = CmpNullInput ->
    eval_comparer_sentinel def_eq (encode_comparer_sentinel c) a b = CmpNullRef).
Proof.
  destruct c as [| |f]; simpl.
  - split_and!; [reflexivity | split; intro H; discriminate H | intros _; reflexivity].
  - split_and!; [reflexivity | split; intro H; reflexivity | intro H; discriminate H].
  - split_and!; [reflexivity | split; intro H; discriminate H | intro H; discriminate H].
Qed.

(**md `Graph.fastWriteToken` (`Core.fs` lines 2474–2477): `-1` when `Serialised` or journaled,
    `ownerToken` (`>= 1`, assigned by `OwnerThreadToken.Ensure`) when `Guarded` and unjournaled,
    and `0` when `Unchecked` and unjournaled. *)
Definition compute_fast_write_token (aff : thread_affinity) (has_journal : bool) (owner_tok : Z) : Z :=
  match aff with
  | AffSerialised => (-1)%Z
  | AffGuarded => if has_journal then (-1)%Z else owner_tok
  | AffUnchecked => if has_journal then (-1)%Z else 0%Z
  end.

(**md Inline fast-path gate in `Signal.SetFast` and `Signal.WriteExcept` (`Core.fs` lines 4450–4452, 4541–4543):
    `tok = 0L || tok = OwnerThreadToken.Current`, where `caller_tok = 0` on an uninitialized thread and
    `caller_tok >= 1` on any thread initialized by `OwnerThreadToken.Ensure`. *)
Definition check_fast_write_token (tok caller_tok : Z) : bool :=
  bool_decide (tok = 0%Z) || bool_decide (tok = caller_tok).

(**md Full `.NET` `Signal.Value` setter (`Signal.SetFast` + `Signal.SetSlow`, `Core.fs` lines 4446–4515):
    tests `check_fast_write_token` first for unjournaled `Unchecked` / owner-thread `Guarded` writes, and
    delegates `Serialised`, journaled, and off-thread `Guarded` writes to `signal_set_fast_split`. *)
Definition signal_set_token_fast
    (owner_tok caller_tok : Z) (me ctx fuel : nat) (n : node_id) (v : val)
    (cs : concurrency_state) (j : option (list Z)) (s : state)
    : option (concurrency_state * option (list Z) * state) :=
  let has_j := match j with Some _ => true | None => false end in
  let tok := compute_fast_write_token (cs_affinity cs) has_j owner_tok in
  if check_fast_write_token tok caller_tok then
    match g_nodes s !! n with
    | Some d =>
        match n_kind d with
        | KSignal =>
            if decide (n_value d = v) then Some (cs, None, s)
            else
              match write_signal fuel n v s with
              | Some (s', tt) => Some (cs, None, s')
              | None => None
              end
        | _ => None
        end
    | None => None
    end
  else
    signal_set_fast_split me ctx fuel n v cs j s.

(**md
**`check_fast_write_token` passes inline if and only if the graph is unjournaled and either `Unchecked` or `Guarded` on its owner thread (rejecting uninitialized `0` threads under `Guarded`), and `signal_set_token_fast` refines `signal_set_entered` identically.**

Matches `OwnerThreadToken` (`Core.fs` lines 1790–1805), `Graph.fastWriteToken` (`Core.fs` lines 2474–2477), and
`Signal.SetFast` / `Signal.SetSlow` (`Core.fs` lines 4446–4482):
- Since `owner_tok >= 1` and `caller_tok >= 0`, `tok = -1` (`Serialised` or journaled) never equals `0` or `caller_tok`, routing all `Serialised` (including reentrant) and journaled writes to `SetSlow`.
- Under `Guarded` without a journal (`tok = owner_tok >= 1`), `tok = 0` is false and `tok = caller_tok` holds iff the caller is the owner thread (in particular, an uninitialized thread with `caller_tok = 0` is rejected).
- Across all affinities, threads, and journal states, `signal_set_token_fast` equals `signal_set_entered`.

- [owner_tok:] the positive token (`>= 1`) assigned to `cs_owner_thread cs` by `OwnerThreadToken.Ensure`.
- [caller_tok:] `OwnerThreadToken.Current` on the calling thread (`0` if uninitialized, `>= 1` if initialized).
- [me:] the calling thread id.
- [ctx:] the calling thread's `SynchronizationContext` id.
- [f:] inner fuel budget for `write_signal (S f)`.
- [n:] the signal node id.
- [v:] the written value.
- [cs:] the concurrency state before the write.
- [j:] `None` when unjournaled, or `Some log` when journaled.
- [s:] the graph state before the write.
*)
Theorem fast_write_token_iff
    (owner_tok caller_tok : Z) (me ctx f : nat) (n : node_id) (v : val)
    (cs : concurrency_state) (j : option (list Z)) (s : state) :
  (1 <= owner_tok)%Z ->
  (0 <= caller_tok)%Z ->
  (caller_tok = owner_tok <-> me = cs_owner_thread cs) ->
  let has_j := match j with Some _ => true | None => false end in
  let tok := compute_fast_write_token (cs_affinity cs) has_j owner_tok in
  (check_fast_write_token tok caller_tok = true <->
    (j = None /\
     (cs_affinity cs = AffUnchecked \/
      (cs_affinity cs = AffGuarded /\ me = cs_owner_thread cs)))) /\
  (cs_affinity cs = AffGuarded -> caller_tok = 0%Z ->
    check_fast_write_token tok caller_tok = false) /\
  signal_set_token_fast owner_tok caller_tok me ctx (S f) n v cs j s =
    signal_set_entered me ctx (S f) n v cs j s.
Proof.
  intros Hown Hcaller Htok_eq. simpl.
  assert (Hgate :
    check_fast_write_token
      (compute_fast_write_token (cs_affinity cs) (match j with Some _ => true | None => false end) owner_tok)
      caller_tok = true <->
    (j = None /\
     (cs_affinity cs = AffUnchecked \/
      (cs_affinity cs = AffGuarded /\ me = cs_owner_thread cs)))).
  { unfold check_fast_write_token, compute_fast_write_token.
    destruct (cs_affinity cs) eqn:Haff, j as [log|].
    - rewrite !bool_decide_eq_false_2 by lia. simpl.
      split; [discriminate | intros [Hj _]; discriminate Hj].
    - rewrite (bool_decide_eq_false_2 (owner_tok = 0%Z)) by lia. simpl.
      split.
      + intro Hb. apply bool_decide_eq_true_1 in Hb. symmetry in Hb.
        apply Htok_eq in Hb. split; [reflexivity | right; split; [reflexivity | exact Hb]].
      + intros [_ [Hbad | [_ Hme]]]; [discriminate Hbad |].
        apply bool_decide_eq_true_2. symmetry. by apply Htok_eq.
    - rewrite !bool_decide_eq_false_2 by lia. simpl.
      split; [discriminate | intros [Hj _]; discriminate Hj].
    - rewrite (bool_decide_eq_true_2 (0%Z = 0%Z)) by reflexivity. simpl.
      split; [intros _; split; [reflexivity | by left] | intros _; reflexivity].
    - rewrite !bool_decide_eq_false_2 by lia. simpl.
      split; [discriminate | intros [Hj _]; discriminate Hj].
    - rewrite !bool_decide_eq_false_2 by lia. simpl.
      split; [discriminate | intros [_ [Hbad | [Hbad _]]]; discriminate Hbad]. }
  split_and!.
  - exact Hgate.
  - intros Haff Hc0.
    unfold check_fast_write_token, compute_fast_write_token.
    rewrite Haff, Hc0.
    destruct j as [log|]; rewrite !bool_decide_eq_false_2 by lia; reflexivity.
  - unfold signal_set_token_fast.
    destruct (check_fast_write_token _ caller_tok) eqn:Hchk.
    + destruct (proj1 Hgate eq_refl) as [-> Hcase].
      unfold signal_set_entered, graph_enter, signal_journal_record.
      destruct Hcase as [Haff | [Haff Hme]].
      * rewrite Haff. symmetry. apply write_signal_unjournaled_eq.
      * rewrite Haff, decide_True by exact Hme. symmetry. apply write_signal_unjournaled_eq.
    + apply (proj1 (signal_set_fast_split_refines me ctx f n v cs j s)).
Qed.
