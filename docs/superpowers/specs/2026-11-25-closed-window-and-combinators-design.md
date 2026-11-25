# Closed-Window `*On` Primitives, Reactive Combinators, and Unobserved Map-Chain Fusion

Date: 2026-11-25
Branch: `feat/closed-window-and-combinators`
Baseline: `ca61af434e09952cb55ca17a49c344c523e4971a` (`Ranvier` `0.2.2` / `Ranvier.Dom` `0.1.1`)
Status: ready for user approval (builder review completed).

---

## 1. Intent and Why

Following the `0.2.2` reactive core optimisation campaign, `Ranvier` beats `R3` on `cutoff` (`14` vs `50` instructions), `update` (`28,306` vs `38,805` instructions), `create` (`1,574,638` vs `2,896,680` instructions), and `dispose` (`284,345` vs `806,290` instructions), while trailing `R3` on `chain` (`1,240` vs `318` instructions across `ChainDepth = 4`, all figures `measured here` in GitHub Actions `Perf` run `45519602409` on `ubuntu-24.04-arm` hardware PMU counters).

Two missing pieces in `Ranvier`'s public vocabulary explain both the `chain` gap and an authoring asymmetry across the library (`from documentation`: `src/Ranvier/Api.fs`, `src/Ranvier/Core.fs`, `src/Ranvier/Streams.fs`):

1. **Incomplete `*On` split-phase coverage**: `createEffectOn (read: unit -> 'T) (act: 'T -> unit)` separates a tracked `read` window (with cutoff on settled `'T` values) from a closed/untracked `act` body, but `Memo<'U>`, `AsyncMemo<'U>`, and `StreamMemo<'S>` lack their `*On` counterparts. Today:
   - Authors cannot express a `Memo<'U>` whose tracked dependency window closes after `read: unit -> 'T` and skips running `compute: 'T -> 'U` when `'T` settles to an equal key without allocating two `Memo` nodes and manual `untrack`.
   - `createAsync` keeps tracking open until the first `await` that actually suspends (so an already-completed `Task` keeps tracking open into subsequent reads) and cancels/restarts in-flight tasks whenever *any* read signal ticks, even if the derived request key `'T` did not change.
   - `createStream` / `createStreamFold` restarts an open stream whenever any signal read inside `start` ticks, even if the extracted stream key `'K` is unchanged.
2. **No static-edge value combinators (`map`, `map2`, `zip`, `bind`) or unobserved `map`-chain fusion**: Every `Memo` today runs dynamic dependency tracking (`EnterHostedTop`, `SourcesField.BeginRun`, `Graph.Track`, `cursor < count` trim checks). Static-source combinators (`Signal.map`, `Memo.map`, `AsyncMemo.map`, `AsyncSource.map`, `StreamMemo.map`, plus `map2`, `zip`, and `bind`) know their exact upstream source(s) at construction time, require zero dynamic tracking per hop, and allow unobserved linear `map` chains in untraced builds to fuse straight to the root source — bringing a 4-stage mapped chain strictly below `R3.chain`'s `318` instructions while retaining per-stage equality cutoffs, glitch-freedom, lazy pull-on-read evaluation, `Pending`/`Failed` propagation, and exact `ErrorOrigin` attribution.

---

## 2. Goals, Non-Goals, and Anti-Goals

### Goals
1. **Complete the `*On` family across `Memo`, `AsyncMemo`, and `StreamMemo`** (user-decided):
   - Two-phase semantics matching `createEffectOn`: tracked `read: unit -> 'T` with equality cutoff on settled `'T` values, followed by a closed (untracked) consuming computation (`'T -> 'U`, `'T -> Previous<'U> -> CancellationToken -> Task<'U>`, or `'K -> CancellationToken -> StreamSource<'T>`).
   - Include owning `*WithOn` / `*Owning*On*` counterparts where child nodes/cleanups created inside the consuming phase belong to a lazy scope discharged only when `read` settles on an unequal `'T` (or on node disposal).
2. **Ship companion combinator modules (`map`, `map2`, `zip`, `bind`) across `Signal`, `Memo`, `AsyncMemo`, `AsyncSource`, and `StreamMemo`, plus idiomatic C# `Reactive.*On` factories and LINQ extensions (`.Select`, `.Zip`, `.SelectMany`)** (user-decided).
3. **Eliminate dynamic tracking on static `map`/`map2`/`zip` nodes and fuse unobserved `map` chains** (user-decided):
   - Static-edge `Memo<'U>` execution paths avoid `SourcesField` re-tracking and `EnterHostedTop`/`LeaveHostedTop` tracking swaps.
   - Unobserved linear `map` chains (`Signal.map` / `Memo.map` / `AsyncMemo.map` / `AsyncSource.map` / `StreamMemo.map` followed by `Memo.map`) fuse directly to the root source in untraced, unjournaled builds while preserving per-stage cutoffs and lazily de-fusing if an intermediate node is observed/read/disposed directly, achieving fewer hardware instructions than `R3.chain` (`318` instructions, `measured here` in CI `Perf` run `45519602409`) on a 4-stage mapped chain.
4. **Formal Rocq 9.1 verification (`proofs/`)**:
   - Prove equivalence/refinement theorems for two-phase `*On` settled-key cutoff & Phase 1 unready preservation, `AsyncOn` 6-policy flight preservation, unobserved `map`-chain fusion (with per-stage cutoffs, `Pending`, `Failed`, and per-stage `ErrorOrigin`), static `map2`/`zip` left-to-right status precedence, and split `bind` inner-tick & transient-unready preservation (`0` axioms in `Audit.v`, `proofs/check.sh` and `proofs/site.sh` clean).
5. **Full Dual-Target & NativeAOT Parity**:
   - Identical observable semantics on `.NET` (`net10.0`, `netstandard2.1`, NativeAOT `--reflectionfree`) and Fickl/Fable (`node` and `node --jitless`), in both untraced and `RanvierTrace=true` builds, with zero CI performance regressions.

### Non-Goals
- **Shared read-only reactive interface (`IReadOnlyValue<'T>` / cross-kind `map2`/`zip`)**: Explicitly deferred to a future computation-expression DSL design (user-decided). Combinators operate within each concrete companion module (`Signal`, `Memo`, `AsyncMemo`, `AsyncSource`, `StreamMemo`), with lightweight conversion where needed (`Signal.map id` or `Memo` projection).
- **Computation expression builders (`memo { ... }`, `rx { ... }`)**: Out of scope for this work.
- **Pairwise index-queued stream zipping**: `StreamMemo` is a latest-state node (`Seed` + `Fold`), not an unbounded item queue (`from documentation`: `docs/superpowers/specs/2026-10-19-stream-memo-design.md`). `StreamMemo.zip` pairs the latest published states of two `StreamMemo` nodes into a `Memo<'A * 'B>`, consistent with `Signal.zip`, `Memo.zip`, `AsyncMemo.zip`, and `AsyncSource.zip`.
- **Changes to `Ranvier.Analyzers` in `ranvier-org/Ranvier.View`**: Downstream analyzer symbol tables (`tools/Ranvier.Analyzers/Symbols.fs` in `Ranvier.View`) consume published `Ranvier` packages and are updated in a separate `Ranvier.View` follow-up after `Ranvier` publishes.

### Anti-Goals (Failure Conditions Even If Tests Pass)
- Breaking existing public signatures or binary/Fable compatibility of `Signal<'T>`, `Memo<'T>`, `Effect`, `AsyncMemo<'T>`, `AsyncSource<'T>`, or `StreamMemo<'T>`.
- Cancelling or restarting an in-flight `AsyncMemo` task, open `StreamMemo` stream, or owned Phase 2 scope when `read` goes temporarily `Pending`/`Failed` or settles back to an equal key `'T` / `'K`.
- Re-running the outer binder `'T -> Memo<'U>` in `Signal.bind` / `Memo.bind` when only the inner `Memo<'U>` ticks or when outer `source` goes `Pending`/`Failed` and settles back to the same `'T`.
- Losing intermediate stage cutoffs, heap-allocation identity (`ReferenceEquals` under `JsIdentityPolicy`), `ErrorOrigin` identity, or `Pending`/`Failed` precedence when fusing `map` chains or evaluating `map2`/`zip`.
- Stranding a node permanently in `Status.Pending` with zero wake edges if user code reads a pending source inside a closed/untracked Phase 2 callback.
- Regressing any existing `.NET` or `node --jitless` hardware instruction counter scenario in `Perf`.

---

## 3. Public API Design

### 3.1 Split-Phase `*On` Primitives (`src/Ranvier/Api.fs`, `[<AutoOpen>]`)

All `*On` functions validate non-null function/comparer arguments (`ArgumentNullException`, matching existing `Api.fs` contracts).

**Shared two-phase invariant across all `*On` primitives (user-decided, matching `createEffectOn` in `tests/Ranvier.Tests/EffectSplit.fs`)**:
- **Phase 1 (`read: unit -> 'T`)** runs synchronously and tracked under `ScopeMode.Pure` (creating nodes or cleanups inside `read` always fails with `ScopeMessages.forMode ScopeMode.Pure`, even on owning `*WithOn` / `*Owning*On*` forms, because child scopes belong to Phase 2).
- **Phase 2 is keyed strictly on settled `'T` values (and node disposal)**:
  - `Pending` (`NotReadyException`) and `Failed` (`ex`) in Phase 1 are **not** new `'T` values. While `read` is unready, the outer node surfaces `Status.Pending` or `Status.Error` (preserving Phase 1's `ErrorOrigin`) to its own downstream readers (`Suspense` / `ErrorBoundary`), **while holding Phase 2's last settled key (`hasKey`, `lastKey`), cached Phase 2 outcome, owned `ScopeField`, in-flight `Task`, or open `StreamSource` intact**.
  - If `read` subsequently settles to `nextKey` equal to `lastKey`, the Phase 1 unready overlay clears and the node restores Phase 2's current state without discharging `ScopeField`, re-running `compute`, cancelling an active flight, or reconnecting an open stream.
  - Only when `read` settles on `nextKey` unequal to `lastKey` (or before Phase 2 has ever run: `not hasKey`) does Phase 2 discharge any previous `ScopeField` untracked and run untracked for `nextKey`.
- **Untracked `NotReadyException` in closed Phase 2 callbacks (project rule, matching `Projections.fs:363` and `Streams.fs:595`)**:
  - Across all new closed-window Phase 2 callbacks (`createMemoOn*` `compute`, `createAsyncOn*` synchronous `compute` launch, `createStreamOn*` `start`, `*.map` / `*.map2` `mapping`, and `*.bind` `binder`), an uncaught synchronous `NotReadyException` raised by reading a pending source inside the untracked body is wrapped into `InvalidOperationException(..., inner = pending)` (`Status.Error`, `ErrorOrigin = self / stage memo`), so a closed body can never strand the node permanently in `Status.Pending` with zero tracked wake edges. (Existing `createEffectOn` is left unchanged.)

#### Synchronous Memos
```fsharp
/// Tracked pure `read`; when its settled result changes under the graph's `'T` equality policy,
/// runs `compute` untracked (pure) and applies the graph's `'U` equality cutoff.
val createMemoOn :
    read: (unit -> 'T) -> compute: ('T -> 'U) -> Memo<'U>

/// `createMemoOn` whose output `'U` cutoff uses `comparer` (`'T` uses the graph's equality policy).
val createMemoOnWithComparer :
    comparer: IEqualityComparer<'U> -> read: (unit -> 'T) -> compute: ('T -> 'U) -> Memo<'U>

/// Owning counterpart to `createMemoOn`: `read` is pure and tracked; when `read` settles on an
/// unequal `'T`, any scope owned by the previous `compute` run is discharged untracked, and
/// `compute` runs untracked with a lazy owning scope (`ScopeMode.Owning`).
val createMemoWithOn :
    read: (unit -> 'T) -> compute: ('T -> 'U) -> Memo<'U>

/// `createMemoWithOn` whose output `'U` cutoff uses `comparer`.
val createOwningMemoOnWithComparer :
    comparer: IEqualityComparer<'U> -> read: (unit -> 'T) -> compute: ('T -> 'U) -> Memo<'U>
```
*(Designer call: `comparer` customizes output `'U`, matching `createMemoWithComparer` and `createOwningMemoWithComparer`; input `'T` uses `graph.EncodedComparer<'T>()`.)*

**Exact state transitions for `createMemoOn` / `createMemoWithOn`**:
1. **Phase 1 (`read ()`)**:
   - If `read ()` raises `NotReadyException` or throws `ex`: record Phase 1's `Status.Pending` or `Status.Error` (`FailureOf(ex, ...)` preserving upstream `ErrorOrigin`), keep `hasKey`, `lastKey`, `phase2Result` (`Ready value` or `Failed phase2Failure`), and `ScopeField` intact, and notify observers if visible status/failure moved.
   - If `read ()` returns `nextKey` and `hasKey && ComparerSentinel.equals keyEqual lastKey nextKey` (if `keyEqual` itself throws `cmpEx`, treat as Phase 1 failure `cmpEx`): restore `phase2Result` (`Status.None` with `value`, or `Status.Error` with `phase2Failure` if `compute lastKey` threw on that key, matching `EffectSplit.fs:534-552` *"the thrown action still counts as acted on"*), notifying observers only if status/failure moved (e.g. recovering from Phase 1 `Pending`/`Error` back to `Ready value`).
2. **Phase 2 (`compute nextKey`)**:
   - Runs when `not hasKey` or `nextKey` is unequal to `lastKey`.
   - Sets `lastKey <- nextKey; hasKey <- true`. In owning forms (`createMemoWithOn`, `createOwningMemoOnWithComparer`), discharges any non-null `ScopeField` untracked first (if discharge disposes this memo, stops immediately).
   - Runs `compute nextKey` untracked (`ScopeMode.Pure` or lazy `ScopeMode.Owning`).
   - If `compute` throws `ex` (wrapping `NotReadyException` into `InvalidOperationException` as above, or `ScopeMessages.failure` on purity violation): stores `phase2Failure` (`ErrorOrigin = this`) and sets `Status.Error`, waking observers if status/failure moved.
   - If `compute` returns `result`: compares `result` against the last published `Ready` value via `outputEqual` (or marks moved if transitioning from uninitialized / `Pending` / `Error`), stores `value <- result`, `phase2Failure <- null`, `Status.None`, and wakes observers iff moved.

#### Async Memos
```fsharp
/// Tracked synchronous `read: unit -> 'T`; when `read` settles on a new `'T` (or before the first flight),
/// launches `compute` untracked under the graph's `FlightPolicy`. Equal `'T` and transient `Pending`/`Failed`
/// in `read` leave any active flight or settled `'U` for `lastKey` untouched.
val createAsyncOn :
    read: (unit -> 'T) ->
    compute: ('T -> Previous<'U> -> CancellationToken -> Task<'U>) ->
    AsyncMemo<'U>

/// Owning counterpart to `createAsyncOn`: child nodes/cleanups created synchronously by `compute`
/// belong to the flight's lazy scope, discharged only when a new flight launches (on unequal settled `'T`)
/// or on disposal.
val createAsyncWithOn :
    read: (unit -> 'T) ->
    compute: ('T -> Previous<'U> -> CancellationToken -> Task<'U>) ->
    AsyncMemo<'U>

val createAsyncOnWithPolicy :
    policy: FlightPolicy ->
    read: (unit -> 'T) ->
    compute: ('T -> Previous<'U> -> CancellationToken -> Task<'U>) ->
    AsyncMemo<'U>

val createOwningAsyncOnWithPolicy :
    policy: FlightPolicy ->
    read: (unit -> 'T) ->
    compute: ('T -> Previous<'U> -> CancellationToken -> Task<'U>) ->
    AsyncMemo<'U>
```

**Exact two-phase & 6-policy semantics for `createAsyncOn*`**:
- On initial pull or when a source of `read` marks the `AsyncMemo` stale (`Dirty` / `Check`):
  1. `read ()` runs synchronously and tracked (`ScopeMode.Pure`), updating `sources` and setting `freshness <- Clean`.
  2. **Equal key (`hasKey && ComparerSentinel.equals keyEqual lastKey nextKey`)**:
     - Clears any Phase 1 unready overlay (`phase1Status <- Status.None; phase1Failure <- null; pendingSource <- null`) so external `Status` / `Value` / `TryValue` reflects `lastKey`'s current Phase 2 state (in-flight `Pending`, settled `Ready`, or flight `Error`), waking observers if the Phase 1 overlay changed visible status.
     - Leaves `queued`, `flying`, `generation`, `cts`, `ScopeField`, and `value` completely untouched (so under `FinishCurrent` / `Drop` / `DropWhilePending`, an active flight keeps `queued = 1` with zero spurious `RunDeferred` or `TrailingRun`).
  3. **Phase 1 unready (`NotReadyException source` or thrown `ex`)**:
     - If a flight is currently active under `FinishCurrent`, `Drop`, or `DropWhilePending` (`queued <> 0`): sets `queued <- 2` (`RunDeferred`), keeping the active flight running and `lastKey` intact until settle (at settle, `FinishCurrent` and faulted `DropWhilePending` schedule `TrailingRun`, which re-evaluates `read ()`).
     - If a flight is active under `FlightPolicy.Queue` (`queued > 0`): if `read ()` threw `ex`, chains a faulted slot in FIFO order behind active flights (`FailLaunch`, matching `Core.fs:14348`) while keeping `lastKey`; if `read ()` raised `NotReadyException source`, records `pendingSource <- source` so the node stays `Pending` once queued flights ahead drain (`Core.fs:14175`).
     - Otherwise (`CancelPrevious`, `KeepLatest`, or `queued = 0`): sets the Phase 1 unready overlay (`phase1Status = Status.Pending` with `pendingSource <- source`, or `phase1Status = Status.Error` with `phase1Failure <- FailureOf(ex, ...)`), while keeping `hasKey`, `lastKey`, `ScopeField`, and any in-flight `Task<'U>` or settled outcome for `lastKey` alive in the background. If `lastKey`'s background flight settles while the Phase 1 overlay is active, its outcome updates Phase 2's stored slot without clearing the Phase 1 overlay until `read ()` settles.
  4. **Unequal key (`not hasKey || not (equals keyEqual lastKey nextKey)`)**:
     - Clears any Phase 1 unready overlay.
     - If a flight is active under `FinishCurrent`, `Drop`, or `DropWhilePending` (`queued <> 0`): sets `queued <- 2` (`RunDeferred`) **without** overwriting `lastKey` yet — under `FinishCurrent`, `TrailingRun` after the current flight settles re-evaluates `read ()` (so if `read` moved `K1 -> K2 -> K1` during `K1`'s flight, `K1`'s freshly settled result is kept without a redundant second flight; if still `K2`, `K2` launches); under `Drop` (and `DropWhilePending` when the active flight succeeds), `lastKey` remains `K1`.
     - Otherwise (`CancelPrevious`, `KeepLatest`, `FlightPolicy.Queue`, or `queued = 0`): sets `lastKey <- nextKey; hasKey <- true`, discharges any previous `ScopeField` untracked (re-checking `read ()` if a cleanup mutated a source of `read`), and launches `compute nextKey previous token` **untracked** (dependency window closed before `compute` runs, so synchronous `await` continuations never record reactive edges, and any uncaught synchronous `NotReadyException` wraps into `InvalidOperationException`).

#### Stream Memos
```fsharp
/// Tracked synchronous `read: unit -> 'K`; starts `start key token` untracked, and restarts the
/// stream only when `read` settles on a key unequal to the current stream's key.
val createStreamOn :
    read: (unit -> 'K) ->
    start: ('K -> CancellationToken -> StreamSource<'T>) ->
    StreamMemo<'T>

/// Fold counterpart to `createStreamOn`: restarts from `seed` only when `read` settles on an unequal `'K`.
val createStreamFoldOn :
    seed: 'S ->
    fold: ('S -> 'T -> 'S) ->
    read: (unit -> 'K) ->
    start: ('K -> CancellationToken -> StreamSource<'T>) ->
    StreamMemo<'S>
```

**Exact two-phase semantics for `createStreamOn*`**:
- Phase 1 `read ()` runs synchronously and tracked (`ScopeMode.Pure`).
- While `read ()` raises `NotReadyException` or throws `ex`, external `Status` surfaces Phase 1's `Pending` or `Error` (`ErrorOrigin` preserved), while keeping `hasKey`, `lastKey`, and any open/completed stream for `lastKey` intact (an open stream continues folding incoming items into `lastKey`'s state in the background without clearing the Phase 1 unready overlay).
- When `read ()` settles to `nextKey` equal to `lastKey`, any Phase 1 overlay clears and the node exposes `lastKey`'s current stream state (`Pending` before first item, `Ready` folded state, or stream `Error`) with zero reconnect and zero `seed` reset.
- When `read ()` settles to `nextKey` unequal to `lastKey` (or `not hasKey`), any open stream is stopped/cancelled (`StreamEnd.Restarted`), `lastKey <- nextKey; hasKey <- true`, state resets to `seed` (`Status.Pending`), and `start nextKey token` runs **untracked** (`ScopeMode.Pure`, wrapping any uncaught synchronous `NotReadyException` into `InvalidOperationException`).

---

### 3.2 Companion Combinator Modules (`src/Ranvier/Combinators.fs` / `Api.fs`)

All modules carry `[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]` (matching `Signal`, `Projection`, `Grouping`, and `StreamSource`). Argument order puts function parameters before the piped reactive value (`('T -> 'U) -> Node<'T> -> Memo<'U>`), matching F# `List`/`Array`/`Option` and `Projection`.

#### `module Signal` (extending existing `Signal.update`)
```fsharp
val update : signal: Signal<'T> -> f: ('T -> 'T) -> unit // existing
val map    : mapping: ('T -> 'U) -> source: Signal<'T> -> Memo<'U>
val map2   : mapping: ('A -> 'B -> 'C) -> source1: Signal<'A> -> source2: Signal<'B> -> Memo<'C>
val zip    : source1: Signal<'A> -> source2: Signal<'B> -> Memo<'A * 'B>
val bind   : binder: ('T -> Memo<'U>) -> source: Signal<'T> -> Memo<'U>
```

#### `module Memo`
```fsharp
val map  : mapping: ('T -> 'U) -> source: Memo<'T> -> Memo<'U>
val map2 : mapping: ('A -> 'B -> 'C) -> source1: Memo<'A> -> source2: Memo<'B> -> Memo<'C>
val zip  : source1: Memo<'A> -> source2: Memo<'B> -> Memo<'A * 'B>
val bind : binder: ('T -> Memo<'U>) -> source: Memo<'T> -> Memo<'U>
```

#### `module AsyncMemo`
```fsharp
val map            : mapping: ('T -> 'U) -> source: AsyncMemo<'T> -> Memo<'U>
val map2           : mapping: ('A -> 'B -> 'C) -> source1: AsyncMemo<'A> -> source2: AsyncMemo<'B> -> Memo<'C>
val zip            : source1: AsyncMemo<'A> -> source2: AsyncMemo<'B> -> Memo<'A * 'B>
val bind           : binder: ('T -> CancellationToken -> Task<'U>) -> source: AsyncMemo<'T> -> AsyncMemo<'U>
val bindWithPolicy : policy: FlightPolicy -> binder: ('T -> CancellationToken -> Task<'U>) -> source: AsyncMemo<'T> -> AsyncMemo<'U>
```

#### `module AsyncSource`
```fsharp
val map  : mapping: ('T -> 'U) -> source: AsyncSource<'T> -> Memo<'U>
val map2 : mapping: ('A -> 'B -> 'C) -> source1: AsyncSource<'A> -> source2: AsyncSource<'B> -> Memo<'C>
val zip  : source1: AsyncSource<'A> -> source2: AsyncSource<'B> -> Memo<'A * 'B>
val bind : binder: ('T -> CancellationToken -> Task<'U>) -> source: AsyncSource<'T> -> AsyncMemo<'U>
```

#### `module StreamMemo`
```fsharp
val map      : mapping: ('T -> 'U) -> source: StreamMemo<'T> -> Memo<'U>
val map2     : mapping: ('A -> 'B -> 'C) -> source1: StreamMemo<'A> -> source2: StreamMemo<'B> -> Memo<'C>
val zip      : source1: StreamMemo<'A> -> source2: StreamMemo<'B> -> Memo<'A * 'B>
val bind     : binder: ('K -> CancellationToken -> StreamSource<'T>) -> source: StreamMemo<'K> -> StreamMemo<'T>
val bindFold : seed: 'S -> fold: ('S -> 'T -> 'S) -> binder: ('K -> CancellationToken -> StreamSource<'T>) -> source: StreamMemo<'K> -> StreamMemo<'S>
```

**Combinator contracts across all 5 modules**:
- **Closed dependency window**: Every `mapping` / `binder` runs **untracked** (`ScopeMode.Pure` for `map`/`map2`/`zip` and for `AsyncMemo.bind`/`AsyncSource.bind`/`StreamMemo.bind*`'s factory; lazy `ScopeMode.Owning` for `Signal.bind`/`Memo.bind`'s outer `'T -> Memo<'U>` stage so dynamically constructed inner `Memo<'U>` nodes are owned and disposed when outer `'T` settles to an unequal value, while pre-existing inner `Memo<'U>` nodes allocate zero `Owner`), wrapping any uncaught synchronous `NotReadyException` into `InvalidOperationException`.
- **Null checks, `Graph` resolution, & cross-graph checks**:
  - Null `source` / `source1` / `source2` / `mapping` / `binder` / `fold` raises `ArgumentNullException` immediately at construction.
  - Rather than requiring `Graph.Current` (`graph.Activate()`), combinators resolve their `Graph` directly from `source.Graph` (matching `Projection.*` in `Combinators.fs`) and attach to `source.Graph.CurrentOwner` (active scope on `source.Graph`, or `source.Graph.Root` when at root).
  - In `map2` / `zip`, if `not (obj.ReferenceEquals(source1.Graph, source2.Graph))`, raise `InvalidOperationException` immediately at construction; in `Signal.bind` / `Memo.bind`, if `binder x` returns `null` or a `Memo<'U>` from a different `Graph`, fail the node with `NullReferenceException` / `InvalidOperationException` (`ErrorOrigin = self`).
- **`Pending` / `Failed` left-to-right precedence in `map2` and `zip`**:
  - Links both static upstream edges once on first read (zero edge churn across transient `Pending`/`Failed` transitions), refreshes `source1` then `source2`, and applies left-to-right status precedence: if `source1` is `Pending` or `Failed`, propagate `source1`'s `Pending` / `Failure` (`ErrorOrigin` preserved); else if `source2` is `Pending` or `Failed`, propagate `source2`'s `Pending` / `Failure`; else invoke `mapping v1 v2` untracked.
- **Split `bind` inner-tick & transient-unready preservation**:
  - `Signal.bind f s` and `Memo.bind f m`:
    - When `s` / `m` settles on an unequal `'T` (or on initial `Ready` run), any previous outer `ScopeField` is discharged untracked, any previous `inner` edge is unlinked, and `f x` runs untracked in a lazy owning scope to yield `inner: Memo<'U>`, linking `inner` as the second static source (`slot 1`).
    - When `inner` subsequently updates while `s` / `m` is unchanged, the returned `Memo<'U>` refreshes and pulls `inner` directly **without re-running `f x` or discharging `f`'s scope**.
    - In `Memo.bind f m`, if outer `m` goes temporarily `Pending` or `Failed` after binding `inner` for `lastKey`, the returned `Memo<'U>` surfaces outer `m`'s `Pending` / `Failed` status while keeping `lastKey`, `f`'s `ScopeField`, and `inner` intact (just like `createMemoWithOn`); if `m` settles back to `lastKey`, it resumes reading `inner` without re-running `f` or tearing down `f`'s scope.
  - `AsyncMemo.bind` / `AsyncMemo.bindWithPolicy` / `AsyncSource.bind`: implemented directly atop `AsyncMemo`'s `On` engine (`read = fun () -> source.Value`, `compute = fun x _ ct -> binder x ct`).
  - `StreamMemo.bind` / `StreamMemo.bindFold`: implemented directly atop `StreamMemo`'s `On` engine (`read = fun () -> source.Value`, `start = binder`).

---

### 3.3 C# Facade (`src/Ranvier.CSharp/Reactive.fs` & `Extensions.fs`)

- **`Reactive` static factories (`Reactive.fs`)**:
  - `MemoOn<T, U>(Func<T> read, Func<T, U> compute)` and `(..., IEqualityComparer<U> comparer)`
  - `OwningMemoOn<T, U>(Func<T> read, Func<T, U> compute)` and `(..., IEqualityComparer<U> comparer)`
  - `AsyncOn<T, U>(Func<T> read, Func<T, CancellationToken, Task<U>> compute)` (+ `FlightPolicy`, `Func<T, Previous<U>, CancellationToken, Task<U>>`, and `OwningAsyncOn` overloads matching `Reactive.Async`)
  - `StreamOn<K, T>(Func<K> read, Func<K, CancellationToken, StreamSource<T>> start)` and `StreamFoldOn<K, S, T>(S seed, Func<S, T, S> fold, Func<K> read, Func<K, CancellationToken, StreamSource<T>> start)`
- **`RanvierExtensions` LINQ/combinator extensions (`Extensions.fs`)**:
  - `Select` (`map`) on `Signal<T>`, `Memo<T>`, `AsyncMemo<T>`, `AsyncSource<T>`, `StreamMemo<T>` -> `Memo<U>`
  - `Zip` (`(first, second) -> Memo<struct (A * B)>` `ValueTuple<A, B>` AND 3-arg `(first, second, Func<A, B, C>) -> Memo<C>`) on `Signal`, `Memo`, `AsyncMemo`, `AsyncSource`, `StreamMemo`
  - `SelectMany`:
    - On `Signal<T>` and `Memo<T>`: both 2-arg `(source, Func<T, Memo<U>>)` -> `Memo<U>` and 3-arg LINQ query-comprehension `(source, Func<T, Memo<U>>, Func<T, U, R>)` -> `Memo<R>` (so `from x in sig from y in f(x) select g(x, y)` compiles natively).
    - On `AsyncMemo<T>` and `AsyncSource<T>`: method-syntax `SelectMany(source, Func<T, CancellationToken, Task<U>>)` -> `AsyncMemo<U>` (plus `FlightPolicy` overload on `AsyncMemo<T>`), keeping `CancellationToken` mandatory in delegates to match `Reactive.Async`.
    - On `StreamMemo<K>`: method-syntax `SelectMany(source, Func<K, CancellationToken, StreamSource<T>>)` -> `StreamMemo<T>` and `SelectManyFold(source, S seed, Func<S, T, S> fold, Func<K, CancellationToken, StreamSource<T>>)` -> `StreamMemo<S>`, keeping `CancellationToken` mandatory to match `Reactive.Stream`.

---

## 4. Runtime Architecture: Static `Memo` Fast Paths & Unobserved `map`-Chain Fusion

### 4.1 Static-Edge `Memo<'U>` (`computeKind` Specialization)
`Memo<'U>` in `src/Ranvier/Core.fs` dispatches by `computeKind: byte` + `RunCallback: Func<MemoNode, Node, bool>` (`.NET`) / `RunFromNode` (`Fable`). We extend `Memo<'U>` with static-edge descriptors so all synchronous combinators return the existing `Memo<'U>` type without public type proliferation:

1. **Static 1-source `Map` descriptor (`Signal<'T>` root, `Memo<'T>` root, or `ISource` async/stream root)**:
   - Links its single upstream source **once** on first read (`node.SourcesField` fixed at `count = 1`, `cursor = 1`) and unlinks once on `Dispose`.
   - Steady-state recompute skips `SourcesField.BeginRun()`, `EnterHostedTop` / `LeaveHostedTop`, `Graph.Track`, and `cursor < count` trim checks; sets a lightweight untracked-pure guard (`EnterUntrackedPure` / `LeaveUntrackedPure`) while invoking the typed `'T -> 'U` function against upstream's settled value (`Signal.Peek` / `Memo.Peek`), then applies `ComparerSentinel.equals`.
   - When the root is a `Signal<'T>` and `tail` is `Dirty` with `0` observers (the `chain` / `chain-map` tail-read pattern), `tail.Value` refreshes inline on `signal.Peek` without `EnterPull` / `RefreshChain`: validate thread affinity (`Guarded` token check), run the pipeline under the untracked-pure guard, apply per-stage cutoffs, clear `Dirty`, increment `Runs`, and return `value`.

### 4.2 Unobserved `map`-Chain Fusion (Full Observational Equivalence + Beating `R3.chain`)
In untraced builds (`!RANVIER_TRACE`) when `Platform.isNull graph.Journal`:
- **Single-consumer claim & same-owner eligibility**:
  - Every `Memo<'B>` created by `*.map` carries a fusable stage descriptor.
  - When `m2 = Memo.map g m1` is constructed, fusion claims `m1` iff `not m1.Node.IsDisposed`, `m1` is unclaimed (`m1.FusedConsumer = null`), unread (`m1.Node.RunsField = 0`), unobserved (`m1.Node.ObserverCount = 0`), and attached to the **same `Owner`** as `m2` (`obj.ReferenceEquals(m1Owner, m2Owner)`, ensuring scope disposal tears down the chain together).
  - Because `Memo` is lazy in `Ranvier`, `m1` has not subscribed to `Root` yet; `m2` records `m1.FusedConsumer <- m2` and extends the fused pipeline directly from `Root`, so when `m_k.Value` is read, **only `m_k` subscribes to `Root`** (`Root` has `1` observer instead of `k`).
- **Per-stage cached value & cutoff (preserving allocating maps and intermediate cutoffs)**:
  - Each fused stage `i` caches its last settled output `stageValue_i` and checks `ComparerSentinel.equals eq_i stageValue_i next_i` inline (~3 CPU instructions per primitive stage).
  - If stage `i` cuts off (`next_i` equals `stageValue_i` after a prior settled run of stage `i`), the fused pipeline **stops immediately at stage `i`** without invoking `f_{i+1} .. f_k`, leaving `m_k`'s cached `value` (and heap reference identity under `ReferenceComparer`) untouched and waking `0` downstream observers — identical to an unfused `Memo` chain!
- **Lazy de-fusion (materialization) on fan-out, intermediate inspection, or individual disposal**:
  - If user or test code ever accesses a claimed intermediate `m_i` directly (`Value`, `TryValue`, `Peek`, `Status`, `Runs`, `Failure`, `ErrorOrigin`, `Dispose()`) or attaches a second consumer (`Memo.map h m_i`, `Memo.map2`, or a tracked read inside another computation), `m_i` first **de-fuses** its downstream consumer: materializing `m_1 .. m_i`'s live `SourcesField` edges and cached state from any prior fused runs, and re-pointing the downstream consumer's `Root` to `m_i`.
  - Consequently, diamond fan-outs (`m2 = Memo.map g m1; m3 = Memo.map h m1`), direct intermediate reads, and `m1.Dispose()` behave **100% identically** to unfused and `RanvierTrace=true` builds, while unobserved linear chains (`s |> Signal.map f1 |> Memo.map f2 |> Memo.map f3 |> Memo.map f4`) execute completely fused at ~`100–150` instructions (`< 50%` of `R3.chain`'s `318` instructions).
- **Exact `ErrorOrigin` preservation**:
  - During fused step evaluation, tracking the active stage index attributes any uncaught exception (including `NotReadyException -> InvalidOperationException` or comparer throws) to `stageNodes[stepIdx]`, preserving exact `ErrorOrigin` identity (`m_1` vs `m_2` vs `m_k`).
- **In `RanvierTrace=true` or journaled graphs**: `MapFusion` is disabled so every intermediate `Memo` node subscribes, runs, and emits full causal `TraceEvent` records.

### 4.3 Benchmark Additions (`bench/Ranvier.Counters`)
- Preserve the existing 5 canonical 3-engine scenarios (`create`, `update`, `chain`, `cutoff`, `dispose`) completely untouched (`from documentation`: `bench/Ranvier.Counters/Scenarios.fs:1-6` and `CONTRIBUTING.md`).
- Following the exact `create-on`, `update-on`, `dispose-on`, `derive-on` convention in `Scenarios.fs:937-952`, add:
  - `chain-map` (`Engine = "Ranvier"`): `source |> Signal.map (+ 1) |> Memo.map (+ 1) |> Memo.map (+ 1) |> Memo.map (+ 1)` (`ChainDepth = 4`, `5000 * scale` ops, targeting `< 250` instructions on `arm64-counters` vs `R3.chain`'s `318` instructions), and
  - `chain-on` (`Engine = "Ranvier"`): `ChainDepth = 4` chain built with `createMemoOn (fun () -> prev.Value) (fun v -> v + 1)`.
- Because both use `Engine = "Ranvier"`, `Ranvier.Counters --reconcile` (`Reconcile.fs:68-69`) automatically verifies exact counter-to-trace reconciliation for both scenarios under `RANVIER_TRACE && RANVIER_COUNTERS`!

---

## 5. Formal Proof Boundary (`proofs/`)

Per `ranvier-proof-and-perf-campaign`, every invariant is classified before authoring `.v` or `.fs` files:

| Invariant / Theorem | Layer | Obligation |
|---|---|---|
| Core `Signal`/`Memo`/`Effect`/`Owner` LIFO & glitch-freedom (`B1`–`B15`) | **Inherited** (`proofs/theories/`) | Existing `544` closed theorems remain untouched and green. |
| **B16 (`memo_on_cutoff_eq`)**: Two-phase `(read, compute)` skips `compute` and preserves owned scope when `read` settles to `eq_T`-equal key, surfaces Phase 1 `Pending`/`Failed` while holding `lastKey`, `phase2Result`, and scope in place, and resumes without re-running `compute` when `read` recovers to `lastKey`. | **Pure Rocq** (`Pure/Combinators.v`) | Constructive state-machine proof over `Reading<'T>` and `Reading<'U>` + scope generation; registered in `_CoqProject` and `Audit.v`. |
| **B17 (`async_on_flight_preservation_eq`)**: Two-phase async `(read, launch)` across all 6 `FlightPolicy` modes preserves active in-flight generation, `queued = 1`, and settled `'U` on equal `'T` and transient Phase 1 `Pending`/`Failed` (`CancelPrevious`/`KeepLatest`/`queued = 0`), defers/drops via `queued = 2` without clobbering `lastKey` when `queued <> 0` (`FinishCurrent`/`Drop`/`DropWhilePending`), and preserves FIFO ordering under `Queue`. | **Pure Rocq** (`Pure/Combinators.v`) | Constructive state-machine refinement across all 6 `FlightPolicy` cases; registered in `Audit.v`. |
| **B18 (`fused_map_chain_eq_unfused`)**: Over arbitrary stage equality predicates `eq_i : V_i -> V_i -> bool` (without assuming Leibniz equality) and stateful/allocating step functions, a single-consumer fused $k$-stage `MapFusion` pipeline with per-stage cutoffs invokes the exact same prefix of step functions and produces the exact same `Reading<'U>`, heap-identity token, and `ErrorOrigin` node id as $k$ unfused `Memo.map` nodes across `Ready`, `Pending`, upstream `Failed(ex, orig)`, and intermediate stage throw at index $i$. | **Pure Rocq** (`Pure/Combinators.v`) | Induction on chain length $k$ over stateful step traces + stage cutoffs; registered in `Audit.v`. |
| **B19 (`map2_zip_status_precedence_eq`)**: Static `map2` / `zip` over two `Reading` inputs is glitch-free after batch settlement and matches left-to-right tracked `(let x = a.Value in let y = b.Value in f x y)` for all `3 × 3` `Ready`/`Pending`/`Failed` combinations, preserving `ErrorOrigin`. | **Pure Rocq** (`Pure/Combinators.v`) | Exhaustive case analysis over `Reading` pairs; registered in `Audit.v`. |
| **B20 (`split_bind_inner_tick_eq`)**: Split `bind` over `Reading<'T>` and inner `InnerId -> Reading<'U>` matches outer+inner evaluation across `Ready`, `Pending`, and `Failed`, invokes `outer` (`0` vs `1` times) and discharges the previous outer scope strictly when `Reading<'T>` settles to a new unequal `'T` (never on inner `'U` ticks or transient outer `Pending`/`Failed` recovering to the same `'T`). | **Pure Rocq** (`Pure/Combinators.v`) | State-transition simulation + outer-run count & scope-discharge invariant; registered in `Audit.v`. |

All new theorems must carry `(**md **Title** ... *)` doc headers for `guarantees.py`, pass `Print Assumptions` (`Closed under the global context`, `0` axioms) in `proofs/theories/Audit.v`, update `proofs/CORRESPONDENCE.md`, and pass `bash proofs/check.sh` + `bash proofs/site.sh` cleanly.

---

## 6. User Decisions vs Designer Calls

### Decided with User
1. `*On` primitives take `read: unit -> 'T` + closed/untracked consuming `'T -> ...` across `Memo`, `AsyncMemo`, and `StreamMemo` (including owning `*WithOn` / `*Owning*On*` counterparts).
2. Phase 2 across all `*On` and `bind` primitives is keyed on **settled `'T` transitions** (matching `createEffectOn`): transient `Pending` or `Failed` in `read` surfaces as the node's own `Status` to downstream readers/boundaries while holding Phase 2's last `'T` key, cached `'U`, owned scope, in-flight task, or open stream in place, resuming with zero recompute/restart if `read` settles back to an equal `'T`.
3. Companion combinator modules provide `map`, `map2`, `zip`, and `bind` per concrete reactive type (`Signal`, `Memo`, `AsyncMemo`, `AsyncSource`, `StreamMemo`) + C# `Reactive.*On` and `.Select`/`.Zip`/`.SelectMany`.
4. Unobserved `map` chains fuse in untraced builds so mapped chains outperform `R3.chain`.
5. Shared read-only reactive interface (`IReadOnlySignal<'T>`) is deferred until computation-expression DSL work.

### Designer Calls (Settled for Builder — Please Check While Reading)
1. **Full observational equivalence in `MapFusion` (§4.2)**: Rather than skipping intermediate cutoffs or duplicating diamond work, `MapFusion` caches each stage's value and applies each stage's `ComparerSentinel.equals` cutoff inline (early-exiting before downstream stages run so allocating maps preserve `ReferenceEquals`), claims single-consumer + same-`Owner` on intermediates, and lazily de-fuses if an intermediate `Memo` is read directly, observed by a second branch, or disposed individually.
2. **Untracked `NotReadyException` in closed Phase 2 callbacks (§3.1, §3.2)**: Following `Projections.fs:363` and `Streams.fs:595`, reading a pending source synchronously inside any new closed Phase 2 callback wraps `NotReadyException` into `InvalidOperationException(..., inner = pending)` (`Status.Error`, `ErrorOrigin = self / stage memo`) so the node is never stranded in `Status.Pending` with zero wake edges (existing `createEffectOn` is left untouched).
3. **`createMemoOnWithComparer` / `createOwningMemoOnWithComparer` comparer target (§3.1)**: Customizes the output `'U` comparer (matching `createMemoWithComparer`), while input `'T` uses `graph.EncodedComparer<'T>()`.
4. **`AsyncMemo.bind` / `AsyncSource.bind` & `StreamMemo.bind*` signatures (§3.2)**: Chain task/stream factories (`'T -> CancellationToken -> Task<'U>` and `'K -> CancellationToken -> StreamSource<'T>`) directly atop the `*On` engine rather than nested node-returning `'T -> AsyncMemo<'U>`, avoiding double `CancellationTokenSource` and double flight-policy management.
5. **C# `.Zip` and `.SelectMany` conventions (§3.3)**: 2-arg `.Zip` returns `Memo<struct ('A * 'B)>` (`ValueTuple<A, B>`, matching modern `Enumerable.Zip`); `.SelectMany` supports 2-arg and 3-arg LINQ query syntax on `Signal<T>` and `Memo<T>`, and keeps `CancellationToken` mandatory in method-syntax delegates on `AsyncMemo<T>`, `AsyncSource<T>`, and `StreamMemo<K>` (matching `Reactive.Async` and `Reactive.Stream`).
6. **Benchmark scenarios (§4.3)**: Keep the canonical 3-engine `chain` panel untouched in `Scenarios.fs` and add `chain-map` and `chain-on` alongside `create-on`, `update-on`, `dispose-on`, and `derive-on` (automatically covered by `Ranvier.Counters --reconcile`).

### Left to the Builder during Planning & Implementation
- Exact internal `computeKind` tag numbering and struct/record/field layout of `MapFusion` and `*On` state inside `Core.fs` and `Streams.fs` (preserving Clef-compatible mutable record / `val mutable` discipline where zero-cost and NativeAOT `--reflectionfree` safety).
- File organization across `Core.fs`, `Streams.fs`, `Combinators.fs`, and `Api.fs` to respect F# single-pass compilation order in `Ranvier.fsproj`.
- Exact version bump (`0.2.3` or `0.3.0` for new minor public APIs in pre-`1.0` packages) when preparing any subsequent release PR.
