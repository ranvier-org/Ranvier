From stdpp Require Import prelude gmap.
From Stdlib Require Import ZArith.
From Ranvier.Base Require Import Prelude Syntax.
From Ranvier.Pure Require Import ObserverSet SourceList State Marking Engine.

Open Scope nat_scope.

(**md # Ranvier.Query and Ranvier.Elmish boundary contracts

`Ranvier.Query` (`Types.fs`, `Entry.fs`, `Queries.fs`, `Mutations.fs`) and `Ranvier.Elmish` (`Mvu.fs`)
build higher-level state management on top of the core reactive graph. To keep their implementation open
to internal optimization while guaranteeing their documented consumer contracts, this module proves the
exact minimum specifications required at their boundaries:

1. **`PublicationComparer` (`CmpNever`) with explicit equality gating (`Types.fs`, `Entry.fs`)**:
   - `publication_comparer_guarded_refines_cutoff`: gating a write to a `CmpNever` signal behind an
     explicit `old <> v` check leaves the graph state and observers untouched when `old = v` and wakes
     every observer (`ends_dirty`) when `old <> v`, avoiding duplicate comparison inside `Batch`.
2. **`QueryEntry` generation-fenced publication and stale-while-revalidate (`Entry.fs`)**:
   - `query_publish_fenced`: `qe_publish` mutates the entry if and only if the entry is not disposed and
     the completing flight's generation matches the current generation (`flight_gen = qe_gen`); stale or
     post-disposal flights are ignored.
   - `query_refresh_retains_data`: starting a refresh (`qe_start`) or receiving a refresh failure
     (`qe_publish` with `Inr err`) when cached data `Some v` is present retains `qe_data = Some v`,
     records `qe_error = Some err` and `qe_stale = true`, and never faults the initial-load gate.
3. **Reference-counted `QueryLease` lifecycle (`Queries.fs`)**:
   - `query_lease_dispose_idempotent_refcount`: disposing a `QueryLease` is idempotent (`lease_disposed`
     guards the release callback), decrements the family's reference count at most once, and evicts/disposes
     the shared `QueryEntry` if and only if the last active lease releases (`count = 1`).
4. **Two-phase atomic `QueryStaging` commit and reentrancy guard (`Types.fs`, `Queries.fs`)**:
   - `query_commit_atomic_staging`: `QueryDraft` edits compose in order on loaded entries while leaving
     unloaded entries empty and stale; `staging = true` blocks reentrant client calls during `Prepare()`;
     and if validation fails during `Prepare()`, zero cache entries are mutated.
5. **`Ranvier.Elmish` (`Mvu.fs`) scope-gated dispatch and command ordering**:
   - `mvu_disposed_drops_dispatch`: once the owning scope's cleanup sets `mvu_disposed = true`, `mvu_apply`
     drops incoming messages without mutating the model, and `mvu_run_cmds` halts immediately without
     running remaining commands in the tail.
   - `mvu_apply_writes_before_commands`: `mvu_apply` writes the updated model before running commands, so
     every synchronous command observes the new model state. *)

(** ** 1. PublicationComparer (CmpNever) with explicit equality gating *)

(**md Models the `Entry.fs` lines 55–65 / 231–249 publication pattern: the signal `n` is constructed with
    `PublicationComparer` (`CmpNever`), and the caller evaluates `changed` before the batch, writing `n`
    only when `changed = true`. *)
Definition guarded_pub_write (changed : bool) (fuel : nat) (n : node_id) (v : Z) (s : state)
    : option (state * unit) :=
  if changed then write_signal fuel n v s else Some (s, tt).

(**md
**Gating a `CmpNever` signal write behind `bool_decide (n_value d <> v)` preserves exact equality cutoff when `n_value d = v` and wakes all observers (`ends_dirty`) when `n_value d <> v`.**

Proves the `PublicationComparer` optimization contract (`Types.fs` lines 133–136, `Entry.fs` lines 10–16,
55–65, 231–249): precomputing `changed` outside `graph.Batch` over a `CmpNever` signal avoids comparing a
second time inside `Signal.Set` while preserving both halves of the signal cutoff contract:
- When `n_value d = v`, `guarded_pub_write` is a no-op (`Some (s, tt)`), waking zero observers.
- When `n_value d <> v`, publishing `v` updates `n` and marks every observer `m` dirty (`ends_dirty s1 m`).

- [fuel:] the marking fuel.
- [n:] the signal node id.
- [v:] the published value.
- [d:] the signal descriptor at `n` with `n_kind d = KSignal` and `n_comparer d = CmpNever`.
- [s:] the graph state before publication.
*)
Theorem publication_comparer_guarded_refines_cutoff
    (fuel : nat) (n : node_id) (v : Z) (d : node) (s : state) :
  g_nodes s !! n = Some d ->
  n_kind d = KSignal ->
  n_comparer d = CmpNever ->
  (n_value d = v ->
   guarded_pub_write (bool_decide (n_value d <> v)) fuel n v s = Some (s, tt)) /\
  (n_value d <> v ->
   forall s1 m,
     m ∈ observer_view (observers_of (g_edges s) n) ->
     takes_marks s m ->
     (g_current s <> Some m \/ n ∈ read_prefix (g_edges s) m) ->
     notify_dirty_except fuel n (g_current s) (ghost_publish n (set_node n (node_with_value v d) s)) = Some s1 ->
     ends_dirty s1 m).
Proof.
  intros Hd Hk Hcmp. split.
  - intros Heq. unfold guarded_pub_write.
    rewrite bool_decide_eq_false_2; [done|].
    intros Hne. exact (Hne Heq).
  - intros Hne s1 m Hin Ht Hcond Hnot.
    set (s_pub := ghost_publish n (set_node n (node_with_value v d) s)) in *.
    assert (Hedges : g_edges s_pub = g_edges s) by (subst s_pub; unfold ghost_publish; simpl; rewrite lookup_insert_eq; done).
    assert (Ht_pub : takes_marks s_pub m).
    { destruct Ht as (dm & Hdm & Hkm).
      assert (Hmn : m <> n).
      { intros ->. rewrite Hd in Hdm. injection Hdm as <-.
        destruct Hkm as [Hkm|[Hkm _]]; congruence. }
      exists dm. subst s_pub. unfold ghost_publish. simpl.
      rewrite lookup_insert_eq. simpl. rewrite lookup_insert_ne by done. done. }
    eapply (notify_dirty_ends_dirty fuel n (g_current s) s_pub s1 m).
    + by rewrite Hedges.
    + exact Ht_pub.
    + right. rewrite Hedges. done.
Qed.

(** ** 2. QueryEntry generation-fenced publication and stale-while-revalidate *)

Inductive fetch_status :=
  | FsIdle
  | FsFetching.

Global Instance fetch_status_eq_dec : EqDecision fetch_status.
Proof. solve_decision. Defined.

(**md Pure state of a `QueryEntry<'T>` (`Entry.fs` lines 9–21):

- `qe_data`: accepted cached record (`None` before initial load settles, `Some v` afterwards).
- `qe_status`: `FsIdle` or `FsFetching`.
- `qe_error`: failure from the latest attempt, or `None`.
- `qe_stale`: whether `Ensure` should start a fresh request.
- `qe_gen`: monotonic flight generation counter (`generation` in `Entry.fs`).
- `qe_disposed`: whether `Dispose` has retired the entry.
- `qe_gate_faulted`: whether the initial-load suspension gate (`AsyncSource`) was faulted.
*)
Record query_entry_state := {
  qe_data : option Z;
  qe_status : fetch_status;
  qe_error : option nat;
  qe_stale : bool;
  qe_gen : nat;
  qe_disposed : bool;
  qe_gate_faulted : bool;
}.

(**md `Entry.start` (`Entry.fs` lines 145–159): increments `qe_gen`, cancels any superseded flight,
    replaces `gate` only when `qe_data = None`, and transitions metadata to `(FsFetching, None, true)`
    while retaining `qe_data`. *)
Definition qe_start (qe : query_entry_state) : query_entry_state :=
  if qe_disposed qe then qe
  else
    {| qe_data := qe_data qe;
       qe_status := FsFetching;
       qe_error := None;
       qe_stale := true;
       qe_gen := S (qe_gen qe);
       qe_disposed := false;
       qe_gate_faulted := false |}.

(**md `Entry.publish` (`Entry.fs` lines 48–106): accepts `outcome` (`inl v` for `Result.Ok v`,
    `inr err` for `Result.Error err`) if and only if `not (qe_disposed qe)` and `flight_gen = qe_gen qe`. *)
Definition qe_publish (flight_gen : nat) (outcome : Z + nat) (qe : query_entry_state) : query_entry_state :=
  if decide (qe_disposed qe = false /\ flight_gen = qe_gen qe) then
    match outcome with
    | inl v =>
        {| qe_data := Some v;
           qe_status := FsIdle;
           qe_error := None;
           qe_stale := false;
           qe_gen := qe_gen qe;
           qe_disposed := false;
           qe_gate_faulted := false |}
    | inr err =>
        {| qe_data := qe_data qe;
           qe_status := FsIdle;
           qe_error := Some err;
           qe_stale := true;
           qe_gen := qe_gen qe;
           qe_disposed := false;
           qe_gate_faulted := bool_decide (qe_data qe = None) |}
    end
  else qe.

(**md `Entry.Dispose` (`Entry.fs` lines 275–281): marks `qe_disposed = true` and advances `qe_gen`. *)
Definition qe_dispose (qe : query_entry_state) : query_entry_state :=
  if qe_disposed qe then qe
  else
    {| qe_data := qe_data qe;
       qe_status := qe_status qe;
       qe_error := qe_error qe;
       qe_stale := qe_stale qe;
       qe_gen := S (qe_gen qe);
       qe_disposed := true;
       qe_gate_faulted := qe_gate_faulted qe |}.

(**md
**`qe_publish` mutates the entry only for the active generation of a live entry; superseded or post-disposal completions are no-ops.**

Proves the generation-fencing contract (`Entry.fs` lines 48–106, 151–158, 275–281):
- Any completion whose `flight_gen <> qe_gen qe` or that arrives after `qe_dispose` leaves `qe` untouched.
- In particular, starting a refresh (`qe_start`) or disposing the entry (`qe_dispose`) advances `qe_gen`
  from `g` to `S g`, so any late reply from generation `g` is ignored.

- [flight_gen:] the generation of the completing request.
- [outcome:] the request outcome (`inl v` or `inr err`).
- [qe:] the query entry state.
*)
Theorem query_publish_fenced (flight_gen : nat) (outcome : Z + nat) (qe : query_entry_state) :
  (qe_disposed qe = true \/ flight_gen <> qe_gen qe ->
   qe_publish flight_gen outcome qe = qe) /\
  (qe_publish (qe_gen qe) outcome (qe_start qe) = qe_start qe) /\
  (qe_publish (qe_gen qe) outcome (qe_dispose qe) = qe_dispose qe).
Proof.
  split_and!.
  - intros Hbad. unfold qe_publish.
    rewrite decide_False; [done|].
    intros [Hlive Hgen]. destruct Hbad as [Hdisp|Hne]; congruence.
  - unfold qe_publish, qe_start.
    destruct (qe_disposed qe) eqn:Edisp.
    + rewrite decide_False; [done|]. intros [Hfalse _]. congruence.
    + simpl. rewrite decide_False; [done|]. intros [_ Hgen]. lia.
  - unfold qe_publish, qe_dispose.
    destruct (qe_disposed qe) eqn:Edisp.
    + rewrite decide_False; [done|]. intros [Hfalse _]. congruence.
    + simpl. done.
Qed.

(**md
**Refreshing a loaded `QueryEntry` (`qe_data = Some v`) or failing a refresh retains `qe_data = Some v` and never faults the initial-load gate.**

Proves the stale-while-revalidate contract (`Entry.fs` lines 81–100, 111–115, 167–185):
- When `qe_data qe = Some v`, `qe_start` preserves `qe_data = Some v` while setting `qe_status = FsFetching`
  and `qe_stale = true`.
- When `qe_data qe = Some v` and the active flight fails (`outcome = inr err`), `qe_publish` preserves
  `qe_data = Some v`, records `qe_error = Some err` and `qe_stale = true`, and keeps
  `qe_gate_faulted = false` (only an initial load failure with `qe_data = None` faults the gate).

- [v:] the previously accepted cached record.
- [err:] the refresh error code.
- [qe:] the query entry state.
*)
Theorem query_refresh_retains_data (v : Z) (err : nat) (qe : query_entry_state) :
  qe_disposed qe = false ->
  qe_data qe = Some v ->
  let qe_refresh := qe_start qe in
  let qe_failed := qe_publish (qe_gen qe_refresh) (inr err) qe_refresh in
  qe_data qe_refresh = Some v /\
  qe_status qe_refresh = FsFetching /\
  qe_data qe_failed = Some v /\
  qe_status qe_failed = FsIdle /\
  qe_error qe_failed = Some err /\
  qe_stale qe_failed = true /\
  qe_gate_faulted qe_failed = false.
Proof.
  intros Hlive Hdata. simpl.
  unfold qe_start, qe_publish.
  rewrite Hlive. simpl.
  rewrite decide_True by done. simpl.
  rewrite Hdata.
  rewrite bool_decide_eq_false_2; [done|discriminate].
Qed.

(** ** 3. Reference-counted QueryLease lifecycle *)

(**md State of a `QueryFamily` entry slot and a `QueryLease` (`Queries.fs` lines 11–83, 113–144):

- `ql_lease_disposed`: whether this `QueryLease` has already been disposed.
- `ql_ref_count`: number of active leases holding the key's `QueryEntry`.
- `ql_entry_alive`: whether the key remains in `entries` with an active `QueryEntry`.
*)
Record query_lease_state := {
  ql_lease_disposed : bool;
  ql_ref_count : nat;
  ql_entry_alive : bool;
}.

(**md `QueryLease.Dispose` (`Queries.fs` lines 66–79, 127–132): if `ql_lease_disposed = false`, sets
    `ql_lease_disposed := true`, decrements `ql_ref_count`, and evicts/disposes the entry when the
    decremented count reaches `0`. *)
Definition query_lease_dispose (st : query_lease_state) : query_lease_state :=
  if ql_lease_disposed st then st
  else
    let c' := ql_ref_count st - 1 in
    {| ql_lease_disposed := true;
       ql_ref_count := c';
       ql_entry_alive := bool_decide (0 < c') && ql_entry_alive st |}.

(**md
**`QueryLease.Dispose` is idempotent, decrements the family reference count at most once, and evicts the shared `QueryEntry` iff the last active lease releases.**

Proves the `QueryLease` ownership contract (`Queries.fs` lines 66–79, 125–133):
- Disposing a lease twice equals disposing it once (`query_lease_dispose (query_lease_dispose st) = query_lease_dispose st`).
- When `ql_lease_disposed st = false` and `ql_ref_count st = S k` with `ql_entry_alive st = true`,
  `query_lease_dispose` sets `ql_ref_count` to `k` and keeps `ql_entry_alive = true` iff `k > 0`
  (evicting and disposing the entry exactly when `k = 0`, i.e., `ql_ref_count st = 1`).

- [st:] the lease and family reference-count state.
- [k:] the number of remaining leases after this lease releases.
*)
Theorem query_lease_dispose_idempotent_refcount (st : query_lease_state) (k : nat) :
  query_lease_dispose (query_lease_dispose st) = query_lease_dispose st /\
  (ql_lease_disposed st = false ->
   ql_ref_count st = S k ->
   ql_entry_alive st = true ->
   ql_ref_count (query_lease_dispose st) = k /\
   ql_entry_alive (query_lease_dispose st) = bool_decide (0 < k)).
Proof.
  split.
  - unfold query_lease_dispose.
    destruct (ql_lease_disposed st) eqn:E; simpl; [rewrite E; reflexivity | reflexivity].
  - intros Hlive Hcnt Halive.
    unfold query_lease_dispose.
    rewrite Hlive, Hcnt, Halive. simpl.
    rewrite Nat.sub_0_r, andb_true_r. split; reflexivity.
Qed.

(** ** 4. Two-phase atomic QueryStaging commit and reentrancy guard *)

(**md A staged edit on a single `QueryDraft` (`Types.fs` lines 52–69):

- `DraftUpdate f`: patches a loaded value `Some v` to `Some (f v)` and marks `stale = false, updated = true`;
  leaves an unloaded entry (`None`) as `None` with `stale = true`.
- `DraftInvalidate`: marks the draft `stale = true` while preserving its data.
*)
Inductive draft_edit :=
  | DraftUpdate (f : Z -> Z)
  | DraftInvalidate.

Record query_draft_state := {
  qd_data : option Z;
  qd_stale : bool;
  qd_updated : bool;
}.

Definition init_draft (initial : option Z) : query_draft_state :=
  {| qd_data := initial; qd_stale := true; qd_updated := false |}.

Definition apply_draft_edit (e : draft_edit) (d : query_draft_state) : query_draft_state :=
  match e with
  | DraftUpdate f =>
      match qd_data d with
      | Some v => {| qd_data := Some (f v); qd_stale := false; qd_updated := true |}
      | None => d
      end
  | DraftInvalidate =>
      {| qd_data := qd_data d; qd_stale := true; qd_updated := qd_updated d |}
  end.

Fixpoint apply_draft_edits (es : list draft_edit) (d : query_draft_state) : query_draft_state :=
  match es with
  | [] => d
  | e :: rest => apply_draft_edits rest (apply_draft_edit e d)
  end.

(**md Two-phase commit (`Queries.fs` lines 252–273): `prepare_all` runs all draft validations while
    `staging = true`. If `prepare_all` returns `None` (an updater or guard threw during staging),
    `commit_staging` leaves `cache` untouched; only when `prepare_all` succeeds (`Some next_cache`)
    does `Publish()` replace `cache` with `next_cache`. *)
Definition client_reentrancy_allowed (disposed staging : bool) : bool :=
  negb disposed && negb staging.

Definition commit_staging {A : Type} (prepare_all : option A) (cache : A) : A :=
  match prepare_all with
  | Some published => published
  | None => cache
  end.

(**md
**`QueryDraft` edits compose in order on loaded data, leave unloaded entries empty, reject reentrant client calls during `staging`, and leave the cache untouched if `Prepare()` fails.**

Proves the `QueryStaging` and `Commit` contracts (`Types.fs` lines 52–80, `Queries.fs` lines 163–207, 234–273):
- Applying `[DraftUpdate f; DraftUpdate g]` to a loaded draft (`Some v`) yields `Some (g (f v))` with
  `qd_stale = false` and `qd_updated = true`.
- Applying any sequence of `DraftUpdate` and `DraftInvalidate` edits to an unloaded draft (`None`) preserves
  `qd_data = None` and `qd_stale = true` (unloaded entries are invalidated, never fabricated).
- While `staging = true`, `client_reentrancy_allowed` is `false`.
- If `prepare_all = None`, `commit_staging` returns the original `cache` unmodified.

- [v:] a loaded entry's initial value.
- [f:] the first updater function.
- [g:] the second updater function.
- [es:] an arbitrary list of draft edits.
*)
Theorem query_commit_atomic_staging (v : Z) (f g : Z -> Z) (es : list draft_edit) :
  apply_draft_edits [DraftUpdate f; DraftUpdate g] (init_draft (Some v)) =
    {| qd_data := Some (g (f v)); qd_stale := false; qd_updated := true |} /\
  (let d_none := apply_draft_edits es (init_draft None) in
   qd_data d_none = None /\ qd_stale d_none = true) /\
  (forall disp, client_reentrancy_allowed disp true = false) /\
  (forall (A : Type) (cache : A), commit_staging None cache = cache).
Proof.
  split_and!.
  - reflexivity.
  - assert (Hinv : forall d, qd_data d = None -> qd_stale d = true ->
              qd_data (apply_draft_edits es d) = None /\ qd_stale (apply_draft_edits es d) = true).
    { induction es as [|e rest IH]; intros d Hd Hst; [ done |].
      destruct e as [h|]; simpl; rewrite Hd; apply IH; simpl; done. }
    apply Hinv; reflexivity.
  - intros disp. unfold client_reentrancy_allowed. by rewrite andb_false_r.
  - intros A cache. reflexivity.
Qed.

(** ** 5. Ranvier.Elmish (Mvu.fs) scope-gated dispatch and command ordering *)

(**md State of an `Mvu<'Model, 'Msg>` loop (`Mvu.fs` lines 27–68):

- `mvu_model`: current value of the `root` model signal.
- `mvu_disposed`: set to `true` when the constructing scope's `onCleanup` runs.
- `mvu_trace`: sequence of `(observed_model, cmd_id)` pairs recording the model value visible to each
  executed command.
*)
Record mvu_state := {
  mvu_model : Z;
  mvu_disposed : bool;
  mvu_trace : list (Z * nat);
}.

(**md A command in `Mvu.Run` (`Mvu.fs` lines 60–65): inspects the current `mvu_state` (observing `mvu_model`
    after the model write) and may synchronously dispatch or dispose the owning scope. *)
Definition mvu_cmd := mvu_state -> mvu_state.

(**md `Mvu.Run` (`Mvu.fs` lines 60–65): `while not disposed && not rest.IsEmpty do rest.Head dispatch; rest <- rest.Tail`. *)
Fixpoint mvu_run_cmds (cmds : list mvu_cmd) (st : mvu_state) : mvu_state :=
  match cmds with
  | [] => st
  | c :: rest =>
      if mvu_disposed st then st
      else mvu_run_cmds rest (c st)
  end.

(**md `Mvu.Apply` (`Mvu.fs` lines 38–46): if `not disposed`, computes `(next_model, cmds) := step msg (mvu_model st)`,
    writes `mvu_model := next_model`, and then runs `mvu_run_cmds cmds`. *)
Definition mvu_apply (step : nat -> Z -> Z * list mvu_cmd) (msg : nat) (st : mvu_state) : mvu_state :=
  if mvu_disposed st then st
  else
    let (next_model, cmds) := step msg (mvu_model st) in
    let st_written := {| mvu_model := next_model;
                         mvu_disposed := false;
                         mvu_trace := mvu_trace st |} in
    mvu_run_cmds cmds st_written.

(**md
**Once the constructing scope is torn down (`mvu_disposed = true`), `mvu_apply` drops every message and `mvu_run_cmds` skips all remaining commands.**

Proves the scope-bound lifetime contract of `Ranvier.Elmish` (`Mvu.fs` lines 17–20, 38–46, 63–65):
- When `mvu_disposed st = true`, `mvu_apply step msg st = st` and `mvu_run_cmds cmds st = st`.
- If a command `c` tears down the owning scope (`mvu_disposed (c st) = true`), `mvu_run_cmds (c :: rest) st`
  runs `c` and immediately halts without executing any command in `rest`.

- [step:] the MVU `update` / `step` function.
- [msg:] the dispatched message.
- [cmds:] a command list.
- [c:] a command that disposes the owning scope.
- [rest:] the remaining commands after `c`.
- [st:] the `Mvu` state.
*)
Theorem mvu_disposed_drops_dispatch
    (step : nat -> Z -> Z * list mvu_cmd) (msg : nat)
    (cmds : list mvu_cmd) (c : mvu_cmd) (rest : list mvu_cmd) (st : mvu_state) :
  (mvu_disposed st = true ->
   mvu_apply step msg st = st /\ mvu_run_cmds cmds st = st) /\
  (mvu_disposed st = false ->
   mvu_disposed (c st) = true ->
   mvu_run_cmds (c :: rest) st = c st).
Proof.
  split.
  - intros Hdisp. split.
    + unfold mvu_apply. by rewrite Hdisp.
    + destruct cmds as [|c0 cs]; simpl; [done|by rewrite Hdisp].
  - intros Hlive Hc_disp. simpl.
    rewrite Hlive.
    destruct rest as [|r rs]; simpl; [done|by rewrite Hc_disp].
Qed.

(**md An observational command `obs_cmd cid` that records `(mvu_model st, cid)` into `mvu_trace`. *)
Definition obs_cmd (cid : nat) (st : mvu_state) : mvu_state :=
  {| mvu_model := mvu_model st;
     mvu_disposed := mvu_disposed st;
     mvu_trace := mvu_trace st ++ [(mvu_model st, cid)] |}.

(**md
**`mvu_apply` writes the updated model before running commands (`root.Value <- model; this.Run commands`), so every command observes the post-update model in list order.**

Proves the command-ordering contract of `Ranvier.Elmish` (`Mvu.fs` lines 43–45, 62–65, 131–132):
when `mvu_disposed st = false` and `step msg (mvu_model st)` returns `(next_model, [obs_cmd c1; obs_cmd c2])`,
`mvu_apply` updates `mvu_model` to `next_model` first and executes `obs_cmd c1` then `obs_cmd c2` against
`next_model`.

- [step:] the MVU `step` function.
- [msg:] the dispatched message.
- [next_model:] the new model returned by `step`.
- [c1:] the first command identifier.
- [c2:] the second command identifier.
- [st:] the live `Mvu` state before `Dispatch`.
*)
Theorem mvu_apply_writes_before_commands
    (step : nat -> Z -> Z * list mvu_cmd) (msg : nat) (next_model : Z) (c1 c2 : nat) (st : mvu_state) :
  mvu_disposed st = false ->
  step msg (mvu_model st) = (next_model, [obs_cmd c1; obs_cmd c2]) ->
  let st' := mvu_apply step msg st in
  mvu_model st' = next_model /\
  mvu_trace st' = mvu_trace st ++ [(next_model, c1); (next_model, c2)].
Proof.
  intros Hlive Hstep. simpl.
  unfold mvu_apply. rewrite Hlive, Hstep. simpl.
  unfold obs_cmd. simpl.
  rewrite <- app_assoc. simpl.
  split; reflexivity.
Qed.

(** ** 6. Elmish untracked/unjournaled bypass, lazy gate, inline staging, QueryKey, and linked leases *)

Record arrive_env := {
  ae_tracking : bool;
  ae_host_set : bool;
}.

Definition arrive_run_untracked (e : arrive_env) (apply : bool -> Z) : Z * arrive_env :=
  if ae_tracking e then
    let _e_inner := {| ae_tracking := false; ae_host_set := true |} in
    (apply false, {| ae_tracking := true; ae_host_set := ae_host_set e |})
  else
    (apply false, e).

Definition arrive_fast (e : arrive_env) (apply : bool -> Z) : Z * arrive_env :=
  if ae_tracking e then arrive_run_untracked e apply
  else (apply false, e).

Inductive cmd_work :=
  | WorkNone
  | WorkJournaled.

Definition run_cmds_journaled_spec (has_journal : bool) (cmds : list mvu_cmd) (st : mvu_state)
    : mvu_state * cmd_work :=
  if has_journal then (mvu_run_cmds cmds st, WorkJournaled)
  else (mvu_run_cmds cmds st, WorkNone).

Definition run_cmds_opt (has_journal : bool) (cmds : list mvu_cmd) (st : mvu_state)
    : mvu_state * cmd_work :=
  if negb has_journal then (mvu_run_cmds cmds st, WorkNone)
  else (mvu_run_cmds cmds st, WorkJournaled).

(**md
**`Mvu.Arrive` bypassing `RunUntracked` when `graph.IsTracking () = false` and `Mvu.RunCommands` skipping `JournalWork` when `graph.Options.Journal.IsNone` agree identically with the full untracked and journaled paths.**

Proves the `Mvu.fs` hot-path bypasses:
- For any tracking environment `e` and callback `apply`, `arrive_fast e apply = arrive_run_untracked e apply`, and when `ae_tracking e = false` it returns `(apply false, e)` directly without touching host scope state.
- For any `has_journal` flag, command list `cmds`, and `Mvu` state `st`, `run_cmds_opt` agrees with `run_cmds_journaled_spec` while emitting `WorkNone` when `has_journal = false`.

- [e:] the tracking environment at dispatch arrival.
- [apply:] the message application function parameterized by whether tracking is active.
- [has_journal:] whether `graph.Options.Journal.IsSome`.
- [cmds:] the returned Elmish command list.
- [st:] the initial `Mvu` state.
*)
Theorem mvu_untracked_bypass_and_direct_cmd_eq
    (e : arrive_env) (apply : bool -> Z) (has_journal : bool) (cmds : list mvu_cmd) (st : mvu_state) :
  arrive_fast e apply = arrive_run_untracked e apply /\
  (ae_tracking e = false -> arrive_fast e apply = (apply false, e)) /\
  run_cmds_opt has_journal cmds st = run_cmds_journaled_spec has_journal cmds st /\
  run_cmds_opt false cmds st = (mvu_run_cmds cmds st, WorkNone).
Proof.
  split_and!.
  - unfold arrive_fast, arrive_run_untracked. destruct (ae_tracking e); reflexivity.
  - intros Hnt. unfold arrive_fast. rewrite Hnt. reflexivity.
  - unfold run_cmds_opt, run_cmds_journaled_spec. destruct has_journal; reflexivity.
  - reflexivity.
Qed.

Inductive gate_state :=
  | GatePending
  | GateSettled (v : Z)
  | GateFailed (e : nat).

Record eager_entry := {
  ee_has_data : bool;
  ee_data : Z;
  ee_error : option nat;
  ee_gate : gate_state;
}.

Record lazy_entry := {
  le_has_data : bool;
  le_data : Z;
  le_error : option nat;
  le_gate : option gate_state;
}.

Definition eager_init : eager_entry :=
  {| ee_has_data := false; ee_data := 0%Z; ee_error := None; ee_gate := GatePending |}.

Definition lazy_init : lazy_entry :=
  {| le_has_data := false; le_data := 0%Z; le_error := None; le_gate := None |}.

Inductive gate_op :=
  | OpStart
  | OpOk (v : Z)
  | OpErr (e : nat)
  | OpReadValue.

Definition eval_gate (g : gate_state) : Z + option nat :=
  match g with
  | GatePending => inr None
  | GateSettled v => inl v
  | GateFailed e => inr (Some e)
  end.

Definition step_eager (op : gate_op) (ee : eager_entry) : option (Z + option nat) * eager_entry :=
  match op with
  | OpStart =>
      (None, {| ee_has_data := ee_has_data ee;
                ee_data := ee_data ee;
                ee_error := None;
                ee_gate := if ee_has_data ee then ee_gate ee else GatePending |})
  | OpOk v =>
      (None, {| ee_has_data := true;
                ee_data := v;
                ee_error := None;
                ee_gate := if ee_has_data ee then ee_gate ee else GateSettled v |})
  | OpErr e =>
      (None, {| ee_has_data := ee_has_data ee;
                ee_data := ee_data ee;
                ee_error := Some e;
                ee_gate := if ee_has_data ee then ee_gate ee else GateFailed e |})
  | OpReadValue =>
      let res := if ee_has_data ee then inl (ee_data ee) else eval_gate (ee_gate ee) in
      (Some res, ee)
  end.

Definition ensure_gate (le : lazy_entry) : gate_state * lazy_entry :=
  match le_gate le with
  | Some g => (g, le)
  | None =>
      let g := match le_error le with
               | Some e => GateFailed e
               | None => GatePending
               end in
      (g, {| le_has_data := le_has_data le;
             le_data := le_data le;
             le_error := le_error le;
             le_gate := Some g |})
  end.

Definition step_lazy (op : gate_op) (le : lazy_entry) : option (Z + option nat) * lazy_entry :=
  match op with
  | OpStart =>
      (None, {| le_has_data := le_has_data le;
                le_data := le_data le;
                le_error := None;
                le_gate := if le_has_data le then le_gate le
                           else match le_gate le with
                                | Some _ => Some GatePending
                                | None => None
                                end |})
  | OpOk v =>
      (None, {| le_has_data := true;
                le_data := v;
                le_error := None;
                le_gate := if le_has_data le then le_gate le
                           else match le_gate le with
                                | Some _ => Some (GateSettled v)
                                | None => None
                                end |})
  | OpErr e =>
      (None, {| le_has_data := le_has_data le;
                le_data := le_data le;
                le_error := Some e;
                le_gate := if le_has_data le then le_gate le
                           else match le_gate le with
                                | Some _ => Some (GateFailed e)
                                | None => None
                                end |})
  | OpReadValue =>
      if le_has_data le then (Some (inl (le_data le)), le)
      else let '(g, le') := ensure_gate le in (Some (eval_gate g), le')
  end.

Fixpoint run_eager (ops : list gate_op) (ee : eager_entry)
    : list (option (Z + option nat)) * eager_entry :=
  match ops with
  | [] => ([], ee)
  | op :: rest =>
      let '(r, ee1) := step_eager op ee in
      let '(rs, ee') := run_eager rest ee1 in
      (r :: rs, ee')
  end.

Fixpoint run_lazy (ops : list gate_op) (le : lazy_entry)
    : list (option (Z + option nat)) * lazy_entry :=
  match ops with
  | [] => ([], le)
  | op :: rest =>
      let '(r, le1) := step_lazy op le in
      let '(rs, le') := run_lazy rest le1 in
      (r :: rs, le')
  end.

Definition entry_gate_rel (ee : eager_entry) (le : lazy_entry) : Prop :=
  ee_has_data ee = le_has_data le /\
  ee_data ee = le_data le /\
  ee_error ee = le_error le /\
  match le_gate le with
  | Some g => ee_gate ee = g
  | None =>
      ee_has_data ee = false ->
      ee_gate ee = match ee_error ee with
                   | Some e => GateFailed e
                   | None => GatePending
                   end
  end.

Fixpoint reads_while_empty (ops : list gate_op) (has_data : bool) : bool :=
  match ops with
  | [] => false
  | OpStart :: rest => reads_while_empty rest has_data
  | OpOk _ :: rest => reads_while_empty rest true
  | OpErr _ :: rest => reads_while_empty rest has_data
  | OpReadValue :: rest => if negb has_data then true else reads_while_empty rest has_data
  end.

(**md
**Lazy initial-load gate allocation (`EnsureGate`) simulates eager `AsyncSource` allocation across every trace of entry operations and leaves `le_gate = None` whenever `.Value` is never read while empty.**

Proves the lazy `gate` optimization in `QueryEntry<'T>` (`Entry.fs`):
- Across any sequence of `OpStart`, `OpOk`, `OpErr`, and `OpReadValue`, `run_lazy` preserves `entry_gate_rel` and produces the exact same sequence of read results as `run_eager`.
- On any trace whose `OpReadValue` calls occur only after data has loaded (`reads_while_empty ops false = false`), `le_gate` stays `None` (zero `AsyncSource` allocations).

- [ops:] the trace of entry operations starting from `eager_init` and `lazy_init`.
*)
Theorem lazy_query_gate_refines_eager (ops : list gate_op) :
  (run_lazy ops lazy_init).1 = (run_eager ops eager_init).1 /\
  entry_gate_rel (run_eager ops eager_init).2 (run_lazy ops lazy_init).2 /\
  (reads_while_empty ops false = false -> le_gate (run_lazy ops lazy_init).2 = None).
Proof.
  assert (Hsim : forall trace ee le,
    entry_gate_rel ee le ->
    (run_lazy trace le).1 = (run_eager trace ee).1 /\
    entry_gate_rel (run_eager trace ee).2 (run_lazy trace le).2).
  { induction trace as [|op rest IH]; intros ee le Hrel.
    - split; [reflexivity | exact Hrel].
    - assert (Hstep :
        (step_lazy op le).1 = (step_eager op ee).1 /\
        entry_gate_rel (step_eager op ee).2 (step_lazy op le).2).
      { destruct ee as [ehd edata eerr egate], le as [lhd ldata lerr lgate].
        destruct Hrel as [Hd [Hv [He Hg]]]; simpl in *.
        subst lhd ldata lerr.
        destruct op as [|v|e|]; unfold entry_gate_rel, ensure_gate; simpl.
        - destruct ehd, lgate as [g|]; simpl in *;
            split_and!; try reflexivity; try exact Hg; try (intros Hfalse; discriminate Hfalse); try (intros _; reflexivity).
        - destruct ehd, lgate as [g|]; simpl in *;
            split_and!; try reflexivity; try exact Hg; try (intros Hfalse; discriminate Hfalse).
        - destruct ehd, lgate as [g|]; simpl in *;
            split_and!; try reflexivity; try exact Hg; try (intros Hfalse; discriminate Hfalse); try (intros _; reflexivity).
        - destruct ehd, lgate as [g|]; simpl in *.
          + split_and!; try reflexivity; exact Hg.
          + split_and!; try reflexivity; intros Hfalse; discriminate Hfalse.
          + rewrite Hg. split_and!; reflexivity.
          + rewrite (Hg eq_refl). split_and!; reflexivity. }
      destruct Hstep as [Hr Hrel'].
      simpl. destruct (step_lazy op le) as [rl le1] eqn:El.
      destruct (step_eager op ee) as [re ee1] eqn:Ee.
      simpl in Hr, Hrel'.
      specialize (IH ee1 le1 Hrel') as [Hrs Hrel''].
      destruct (run_lazy rest le1) as [rls le2] eqn:Erl.
      destruct (run_eager rest ee1) as [res ee2] eqn:Ere.
      simpl in Hrs, Hrel''.
      simpl. split; [rewrite Hr, Hrs; reflexivity | exact Hrel'']. }
  assert (Hnone : forall trace le,
    le_gate le = None ->
    reads_while_empty trace (le_has_data le) = false ->
    le_gate (run_lazy trace le).2 = None).
  { induction trace as [|op rest IH]; intros [lhd ldata lerr lgate] Hgate Hno; simpl in *.
    - exact Hgate.
    - subst lgate. destruct op as [|v|e|]; simpl in *.
      + destruct lhd; simpl in *;
          [ pose proof (IH {| le_has_data := true; le_data := ldata; le_error := None; le_gate := None |} eq_refl Hno) as Hrec;
            destruct (run_lazy rest _) as [rs le']; exact Hrec
          | pose proof (IH {| le_has_data := false; le_data := ldata; le_error := None; le_gate := None |} eq_refl Hno) as Hrec;
            destruct (run_lazy rest _) as [rs le']; exact Hrec ].
      + destruct lhd; simpl in *;
          [ pose proof (IH {| le_has_data := true; le_data := v; le_error := None; le_gate := None |} eq_refl Hno) as Hrec;
            destruct (run_lazy rest _) as [rs le']; exact Hrec
          | pose proof (IH {| le_has_data := true; le_data := v; le_error := None; le_gate := None |} eq_refl Hno) as Hrec;
            destruct (run_lazy rest _) as [rs le']; exact Hrec ].
      + destruct lhd; simpl in *;
          [ pose proof (IH {| le_has_data := true; le_data := ldata; le_error := Some e; le_gate := None |} eq_refl Hno) as Hrec;
            destruct (run_lazy rest _) as [rs le']; exact Hrec
          | pose proof (IH {| le_has_data := false; le_data := ldata; le_error := Some e; le_gate := None |} eq_refl Hno) as Hrec;
            destruct (run_lazy rest _) as [rs le']; exact Hrec ].
      + destruct lhd; [|discriminate Hno]. simpl in *.
        pose proof (IH {| le_has_data := true; le_data := ldata; le_error := lerr; le_gate := None |} eq_refl Hno) as Hrec.
        destruct (run_lazy rest _) as [rs le']; exact Hrec. }
  assert (Hinit_rel : entry_gate_rel eager_init lazy_init) by (repeat split; reflexivity).
  destruct (Hsim ops eager_init lazy_init Hinit_rel) as [Hout Hfinal].
  split; [exact Hout|].
  split; [exact Hfinal|].
  apply (Hnone ops lazy_init eq_refl).
Qed.

Inductive ext_draft_edit :=
  | ExtUpdate (f : Z -> Z)
  | ExtSet (v : Z)
  | ExtInvalidate.

Record inline_draft := {
  id_staged : bool;
  id_has_value : bool;
  id_value : Z;
  id_stale : bool;
  id_updated : bool;
}.

Definition init_inline_draft (has_data : bool) (val : Z) : inline_draft :=
  {| id_staged := true; id_has_value := has_data; id_value := val; id_stale := true; id_updated := false |}.

Definition step_inline_draft (e : ext_draft_edit) (d : inline_draft) : inline_draft :=
  match e with
  | ExtUpdate f =>
      if id_has_value d then
        {| id_staged := true; id_has_value := true; id_value := f (id_value d); id_stale := false; id_updated := true |}
      else d
  | ExtSet v =>
      if id_has_value d then
        {| id_staged := true; id_has_value := true; id_value := v; id_stale := false; id_updated := true |}
      else d
  | ExtInvalidate =>
      {| id_staged := true; id_has_value := id_has_value d; id_value := id_value d; id_stale := true; id_updated := id_updated d |}
  end.

Fixpoint apply_inline_drafts (es : list ext_draft_edit) (d : inline_draft) : inline_draft :=
  match es with
  | [] => d
  | e :: rest => apply_inline_drafts rest (step_inline_draft e d)
  end.

Definition to_model_edit (e : ext_draft_edit) : draft_edit :=
  match e with
  | ExtUpdate f => DraftUpdate f
  | ExtSet v => DraftUpdate (fun _ => v)
  | ExtInvalidate => DraftInvalidate
  end.

Definition decode_draft (d : inline_draft) : query_draft_state :=
  {| qd_data := if id_has_value d then Some (id_value d) else None;
     qd_stale := id_stale d;
     qd_updated := id_updated d |}.

Definition inline_prepare (has_data : bool) (cur : Z) (eqb : Z -> Z -> bool) (d : inline_draft) : bool :=
  if negb (id_updated d) || negb (id_has_value d) then false
  else if negb has_data then true
  else negb (eqb cur (id_value d)).

Definition model_prepare (initial : option Z) (eqb : Z -> Z -> bool) (d : query_draft_state) : bool :=
  match qd_updated d, initial, qd_data d with
  | true, Some old, Some v => negb (eqb old v)
  | true, None, None => false
  | true, _, _ => true
  | false, _, _ => false
  end.

Definition commit_reset_draft (publish : bool) (has_data : bool) (cur : Z) (d : inline_draft)
    : bool * (bool * Z) :=
  let next := if publish && id_has_value d then (true, id_value d) else (has_data, cur) in
  (false, next).

(**md
**Unboxed `(hasData, data)` and per-entry inline `draftStaged` staging (with `DraftSet` fusion and pre-batch / rollback reset) refine map-allocated `QueryDraft` over `'T option` for arbitrary edit sequences.**

Proves the `Entry.fs` / `Queries.fs` inline staging optimization:
- For any initial `(has_data, cur)` and arbitrary sequence `es` of `ExtUpdate`, `ExtSet`, and `ExtInvalidate` edits, `decode_draft (apply_inline_drafts es (init_inline_draft has_data cur))` equals `apply_draft_edits (map to_model_edit es) (init_draft (if has_data then Some cur else None))`.
- Under any edit sequence `es`, `inline_prepare` computes the exact same `changed` bit as `model_prepare`.
- Both successful `Publish` (`publish = true`) and rollback `ResetDraft` (`publish = false`) clear `id_staged` to `false`, while rollback leaves `(has_data, cur)` unmodified.

- [has_data:] whether the entry held a committed value before staging.
- [cur:] the committed value stored in the unboxed `data` signal when `has_data = true`.
- [eqb:] the graph's sentinel-encoded comparer predicate.
- [es:] the ordered list of edits targeting this entry.
*)
Theorem unboxed_data_and_inline_draft_refines_staged_map
    (has_data : bool) (cur : Z) (eqb : Z -> Z -> bool) (es : list ext_draft_edit) :
  let initial_opt := if has_data then Some cur else None in
  let final_inline := apply_inline_drafts es (init_inline_draft has_data cur) in
  decode_draft final_inline = apply_draft_edits (map to_model_edit es) (init_draft initial_opt) /\
  inline_prepare has_data cur eqb final_inline = model_prepare initial_opt eqb (decode_draft final_inline) /\
  (commit_reset_draft true has_data cur final_inline).1 = false /\
  commit_reset_draft false has_data cur final_inline = (false, (has_data, cur)).
Proof.
  assert (Hstep : forall edits d,
    decode_draft (apply_inline_drafts edits d) =
    apply_draft_edits (map to_model_edit edits) (decode_draft d) /\
    id_has_value (apply_inline_drafts edits d) = id_has_value d /\
    (id_has_value d = false -> id_updated d = false -> id_updated (apply_inline_drafts edits d) = false)).
  { induction edits as [|e rest IH]; intros d.
    - split; [reflexivity | split; [reflexivity | intros _ Hu; exact Hu]].
    - destruct e as [f|v|]; simpl.
      + destruct (id_has_value d) eqn:Ehv; simpl.
        * destruct (IH {| id_staged := true; id_has_value := true; id_value := f (id_value d); id_stale := false; id_updated := true |})
            as [Hdec [Hh Hupd]].
          split; [exact Hdec | split; [exact Hh | intros Hfalse; discriminate Hfalse]].
        * destruct (IH d) as [Hdec [Hh Hupd]].
          split; [exact Hdec | split; [rewrite Hh, Ehv; reflexivity | intros _ Hu; exact (Hupd Ehv Hu)]].
      + destruct (id_has_value d) eqn:Ehv; simpl.
        * destruct (IH {| id_staged := true; id_has_value := true; id_value := v; id_stale := false; id_updated := true |})
            as [Hdec [Hh Hupd]].
          split; [exact Hdec | split; [exact Hh | intros Hfalse; discriminate Hfalse]].
        * destruct (IH d) as [Hdec [Hh Hupd]].
          split; [exact Hdec | split; [rewrite Hh, Ehv; reflexivity | intros _ Hu; exact (Hupd Ehv Hu)]].
      + destruct (IH {| id_staged := true; id_has_value := id_has_value d; id_value := id_value d; id_stale := true; id_updated := id_updated d |})
          as [Hdec [Hh Hupd]].
        split; [exact Hdec | split; [exact Hh | exact Hupd]]. }
  specialize (Hstep es (init_inline_draft has_data cur)).
  destruct Hstep as [Hdec [Hh Hupd]].
  split_and!.
  - rewrite Hdec. unfold init_inline_draft, decode_draft, init_draft. simpl. reflexivity.
  - unfold inline_prepare, model_prepare, decode_draft. simpl.
    rewrite Hh. destruct has_data eqn:Ehd; simpl.
    + destruct (id_updated (apply_inline_drafts es (init_inline_draft true cur))); reflexivity.
    + rewrite (Hupd eq_refl eq_refl). reflexivity.
  - reflexivity.
  - reflexivity.
Qed.

Definition qk_default_eq (eqZ : Z -> Z -> bool) (k1 k2 : option Z) : bool :=
  match k1, k2 with
  | Some a, Some b => eqZ a b
  | None, None => true
  | _, _ => false
  end.

Definition qk_default_hash (hashZ : Z -> Z) (k : option Z) : Z :=
  match k with
  | Some a => hashZ a
  | None => 0%Z
  end.

Definition session_guard_ok (on_thread disposed_client staging : bool) : bool :=
  on_thread && negb disposed_client && negb staging.

Definition single_checked_op {A : Type}
    (on_thread disposed_client staging disposed_lease disposed_entry : bool) (op : A) : option A :=
  if session_guard_ok on_thread disposed_client staging && negb disposed_lease && negb disposed_entry
  then Some op
  else None.

Definition double_checked_op {A : Type}
    (on_thread disposed_client staging disposed_lease disposed_entry : bool) (op : A) : option A :=
  if session_guard_ok on_thread disposed_client staging && negb disposed_lease then
    if session_guard_ok on_thread disposed_client staging && negb disposed_entry then Some op
    else None
  else None.

(**md
**`QueryKey` default-comparer equality/hashing handles nullable and `unit` (`None`) keys consistently, and checking `QuerySession.Guard` once at `QueryLease` matches double-checked `QueryLease` + `QueryEntry` guards.**

Proves the `Queries.fs` `QueryKey` devirtualization and `QuerySession` single-guard elimination:
- Whenever `eqZ` is reflexive (`eqZ x x = true`) and hash-compatible (`eqZ x y = true -> hashZ x = hashZ y`), `qk_default_eq eqZ` is reflexive on all `option Z` (including `None` for `unit`/`null`) and preserves `qk_default_hash hashZ` without invoking a null dereference on `None`.
- For all thread, disposal, and staging flags, `single_checked_op` agrees with `double_checked_op`.

- [eqZ:] the default equality predicate on non-null keys.
- [hashZ:] the default hash function on non-null keys.
- [k1:] first wrapped key (`None` represents `()` / null).
- [k2:] second wrapped key.
*)
Theorem query_key_default_equatable_and_session_guard_eq
    (eqZ : Z -> Z -> bool) (hashZ : Z -> Z) (k1 k2 : option Z) :
  (forall x, eqZ x x = true) ->
  (forall x y, eqZ x y = true -> hashZ x = hashZ y) ->
  qk_default_eq eqZ k1 k1 = true /\
  (qk_default_eq eqZ k1 k2 = true -> qk_default_hash hashZ k1 = qk_default_hash hashZ k2) /\
  forall (A : Type) (on_thread dc st dl de : bool) (op : A),
    single_checked_op on_thread dc st dl de op = double_checked_op on_thread dc st dl de op.
Proof.
  intros Hrefl Hhash. split_and!.
  - destruct k1; simpl; [apply Hrefl | reflexivity].
  - destruct k1, k2; simpl; try discriminate; [apply Hhash | intros _; reflexivity].
  - intros A on_thread dc st dl de op.
    unfold single_checked_op, double_checked_op, session_guard_ok.
    destruct on_thread, dc, st, dl, de; reflexivity.
Qed.

Inductive await_outcome :=
  | AwaitOk (v : Z)
  | AwaitErr (err : nat)
  | AwaitCanceled.

Definition await_immediate_decision (err : option nat) (has_data stale : bool) (data_val : Z) : await_outcome :=
  match err with
  | Some e => AwaitErr e
  | None => if has_data && negb stale then AwaitOk data_val else AwaitCanceled
  end.

Definition await_waiter_spec
    (status : fetch_status) (err : option nat) (has_data stale : bool) (data_val : Z)
    : bool * option await_outcome :=
  match status with
  | FsFetching => (true, None)
  | FsIdle => (false, Some (await_immediate_decision err has_data stale data_val))
  end.

Definition await_fast_opt
    (status : fetch_status) (err : option nat) (has_data stale : bool) (data_val : Z)
    : bool * option await_outcome :=
  match status with
  | FsFetching => (true, None)
  | FsIdle => (false, Some (await_immediate_decision err has_data stale data_val))
  end.

Record linked_lease_state := {
  ll_lease_state : query_lease_state;
  ll_owner_attached : bool;
  ll_waiter_detached : bool;
}.

Definition linked_lease_dispose (st : linked_lease_state) : linked_lease_state :=
  {| ll_lease_state := query_lease_dispose (ll_lease_state st);
     ll_owner_attached := false;
     ll_waiter_detached := true |}.

(**md
**`QueryEntry.AwaitImmediate` allocates zero waiters when `FetchStatus = FsIdle` with identical outcome, `QueryAwaiter.TryClaim` is idempotent, and `OwnerLink`-backed `QueryLease.Dispose` detaches cleanly and decrements refcount at most once.**

Proves the `Entry.fs` / `Queries.fs` waiter and lease-link optimizations:
- When `status = FsIdle`, `await_fast_opt` matches `await_waiter_spec` and requires zero waiter allocation (`fst = false`).
- Disposing a `linked_lease_state` is idempotent (`linked_lease_dispose (linked_lease_dispose st) = linked_lease_dispose st`), sets `ll_owner_attached = false` and `ll_waiter_detached = true`, and decrements the entry's reference count once (`query_lease_dispose_idempotent_refcount`).

- [status:] current `FetchStatus` of the entry.
- [err:] current `Error` option in metadata.
- [has_data:] whether data is loaded.
- [stale:] whether cached data is stale.
- [data_val:] cached payload value.
- [st:] combined lease, owner-link, and waiter-claim state.
- [k:] number of sibling leases remaining.
*)
Theorem waiter_fast_task_and_linked_lease_eq
    (status : fetch_status) (err : option nat) (has_data stale : bool) (data_val : Z)
    (st : linked_lease_state) (k : nat) :
  await_fast_opt status err has_data stale data_val = await_waiter_spec status err has_data stale data_val /\
  fst (await_fast_opt FsIdle err has_data stale data_val) = false /\
  linked_lease_dispose (linked_lease_dispose st) = linked_lease_dispose st /\
  ll_owner_attached (linked_lease_dispose st) = false /\
  ll_waiter_detached (linked_lease_dispose st) = true /\
  (ql_lease_disposed (ll_lease_state st) = false ->
   ql_ref_count (ll_lease_state st) = S k ->
   ql_entry_alive (ll_lease_state st) = true ->
   ql_ref_count (ll_lease_state (linked_lease_dispose st)) = k /\
   ql_entry_alive (ll_lease_state (linked_lease_dispose st)) = bool_decide (0 < k)).
Proof.
  split_and!.
  - reflexivity.
  - reflexivity.
  - unfold linked_lease_dispose. simpl.
    destruct (query_lease_dispose_idempotent_refcount (ll_lease_state st) k) as [Hid _].
    rewrite Hid. reflexivity.
  - reflexivity.
  - reflexivity.
  - intros Hlive Hcnt Halive. simpl.
    destruct (query_lease_dispose_idempotent_refcount (ll_lease_state st) k) as [_ Hdec].
    exact (Hdec Hlive Hcnt Halive).
Qed.
