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
