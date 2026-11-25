# Closed-Window `*On` Primitives, Reactive Combinators, and Unobserved Map-Chain Fusion

Date: 2026-11-25
Branch: `feat/closed-window-and-combinators`
Baseline: `ca61af434e09952cb55ca17a49c344c523e4971a` (`Ranvier` `0.2.2` / `Ranvier.Dom` `0.1.1`)
Status: draft for review.

---

## 1. Intent and Why

Following the `0.2.2` reactive core optimisation campaign, `Ranvier` beats `R3` on `cutoff` (`14` vs `50` instructions), `update` (`28,306` vs `38,805` instructions), `create` (`1,574,638` vs `2,896,680` instructions), and `dispose` (`284,345` vs `806,290` instructions), while trailing `R3` on `chain` (`1,240` vs `318` instructions across `ChainDepth = 4`, all figures `measured here` in GitHub Actions `Perf` run `45519602409` on `ubuntu-24.04-arm` hardware PMU counters).

Two missing pieces in `Ranvier`'s public vocabulary explain both the `chain` gap and an authoring asymmetry across the library (`from documentation`: `src/Ranvier/Api.fs`, `src/Ranvier/Core.fs`, `src/Ranvier/Streams.fs`):

1. **Incomplete `*On` split-phase coverage**: `createEffectOn (read: unit -> 'T) (act: 'T -> unit)` separates a tracked `read` window (with cutoff on `'T`) from a closed/untracked `act` body, but `Memo<'U>`, `AsyncMemo<'U>`, and `StreamMemo<'S>` lack their `*On` counterparts. Today:
   - Authors cannot express a `Memo<'U>` whose tracked dependency window closes after `read: unit -> 'T` and skips running `compute: 'T -> 'U` when `'T` settles to an equal key without allocating two `Memo` nodes and manual `untrack`.
   - `createAsync` keeps tracking open until the first `await` that actually suspends (so an already-completed `Task` keeps tracking open into subsequent reads) and cancels/restarts in-flight tasks whenever *any* read signal ticks, even if the derived request key `'T` did not change.
   - `createStream` / `createStreamFold` restarts an open stream whenever any signal read inside `start` ticks, even if the extracted stream key `'K` is unchanged.
2. **No static-edge value combinators (`map`, `map2`, `zip`, `bind`) or unobserved `map`-chain fusion**: Every `Memo` today runs dynamic dependency tracking (`EnterHostedTop`, `SourcesField.BeginRun`, `Graph.Track`, `cursor < count` trim checks). Static-source combinators (`Signal.map`, `Memo.map`, `AsyncMemo.map`, `AsyncSource.map`, `StreamMemo.map`, plus `map2`, `zip`, and `bind`) know their exact upstream source(s) at construction time, require zero dynamic tracking per hop, and allow unobserved `map` chains in untraced builds to fuse straight to the root source — bringing a 4-stage mapped chain strictly below `R3.chain`'s `318` instructions while retaining glitch-freedom, lazy pull-on-read evaluation, `Pending`/`Failed` propagation, and exact `ErrorOrigin` attribution.

---

## 2. Goals, Non-Goals, and Anti-Goals

### Goals
1. **Complete the `*On` family across `Memo`, `AsyncMemo`, and `StreamMemo`** (user-decided):
   - Two-phase semantics matching `createEffectOn`: tracked `read: unit -> 'T` with equality cutoff on `'T`, followed by a closed (untracked) consuming computation (`'T -> 'U`, `'T -> Previous<'U> -> CancellationToken -> Task<'U>`, or `'K -> CancellationToken -> StreamSource<'T>`).
   - Include owning `*WithOn` / `*Owning*On*` counterparts where child nodes/cleanups created inside the consuming phase belong to a lazy scope discharged only when `'T` changes to an unequal value (or on node disposal).
2. **Ship companion combinator modules (`map`, `map2`, `zip`, `bind`) across `Signal`, `Memo`, `AsyncMemo`, `AsyncSource`, and `StreamMemo`, plus idiomatic C# `Reactive.*On` factories and LINQ extensions (`.Select`, `.Zip`, `.SelectMany`)** (user-decided).
3. **Eliminate dynamic tracking on static `map`/`map2`/`zip` nodes and fuse unobserved `map` chains** (user-decided):
   - Static-edge `Memo<'U>` execution paths avoid `SourcesField` re-tracking and `EnterHostedTop`/`LeaveHostedTop` tracking swaps.
   - Unobserved `map` chains (`Signal.map` / `Memo.map` / `AsyncMemo.map` / `AsyncSource.map` / `StreamMemo.map` followed by `Memo.map`) fuse directly to the root source in untraced, unjournaled builds when intermediate stages use safe default equality (`SafeEquality.isSafe`), achieving fewer hardware instructions than `R3.chain` (`318` instructions, `measured here` in CI `Perf` run `45519602409`) on a 4-stage mapped chain.
4. **Formal Rocq 9.1 verification (`proofs/`)**:
   - Prove equivalence/refinement theorems for two-phase `*On` input-cutoff preservation, static `map`/`map2`/`zip` glitch-free status/value propagation, unobserved `map`-chain fusion (including `Pending`, `Failed`, and per-stage `ErrorOrigin` preservation), and split `bind` inner-tick preservation (`0` axioms in `Audit.v`, `proofs/check.sh` and `proofs/site.sh` clean).
5. **Full Dual-Target & NativeAOT Parity**:
   - Identical observable semantics on `.NET` (`net10.0`, `netstandard2.1`, NativeAOT `--reflectionfree`) and Fickl/Fable (`node` and `node --jitless`), in both untraced and `RanvierTrace=true` builds, with zero CI performance regressions.

### Non-Goals
- **Shared read-only reactive interface (`IReadOnlyValue<'T>` / cross-kind `map2`/`zip`)**: Explicitly deferred to a future computation-expression DSL design (user-decided). Combinators operate within each concrete companion module (`Signal`, `Memo`, `AsyncMemo`, `AsyncSource`, `StreamMemo`), with lightweight conversion where needed (`Signal.map id` or `Memo` projection).
- **Computation expression builders (`memo { ... }`, `rx { ... }`)**: Out of scope for this work.
- **Pairwise index-queued stream zipping**: `StreamMemo` is a latest-state node (`Seed` + `Fold`), not an unbounded item queue (`from documentation`: `docs/superpowers/specs/2026-10-19-stream-memo-design.md`). `StreamMemo.zip` pairs the latest published states of two `StreamMemo` nodes into a `Memo<'A * 'B>`, consistent with `Signal.zip`, `Memo.zip`, `AsyncMemo.zip`, and `AsyncSource.zip`.
- **Changes to `Ranvier.Analyzers` in `ranvier-org/Ranvier.View`**: Downstream analyzer symbol tables (`tools/Ranvier.Analyzers/Symbols.fs` in `Ranvier.View`) consume published `Ranvier` packages and are updated in a separate `Ranvier.View` follow-up after `Ranvier` publishes.

### Anti-Goals (Failure Conditions Even If Tests Pass)
- Breaking existing public signatures or binary/Fable compatibility of `Signal<'T>`, `Memo<'T>`, `Effect`, `AsyncMemo<'T>`, `AsyncSource<'T>`, or `StreamMemo<'T>`.
- Cancelling or restarting an in-flight `AsyncMemo` task or open `StreamMemo` stream in `createAsyncOn` / `createStreamOn` when `read` re-evaluates to an equal key `'T` / `'K`.
- Re-running the outer binder `'T -> Memo<'U>` in `Signal.bind` / `Memo.bind` when only the inner `Memo<'U>` ticks while the outer `'T` has not changed.
- Losing `ErrorOrigin` identity or `Pending` / `Failed` precedence when fusing `map` chains or evaluating `map2` / `zip`.
- Regressing any existing `.NET` or `node --jitless` hardware instruction counter scenario in `Perf`.

---

## 3. Public API Design

### 3.1 Split-Phase `*On` Primitives (`src/Ranvier/Api.fs`, `[<AutoOpen>]`)

All `*On` functions validate non-null function/comparer arguments (`ArgumentNullException`, matching existing `Api.fs` contracts).

#### Synchronous Memos
```fsharp
/// Tracked pure `read`; when its result changes under the graph's `'T` equality policy,
/// runs `compute` untracked (pure) and applies the graph's `'U` equality cutoff.
val createMemoOn :
    read: (unit -> 'T) -> compute: ('T -> 'U) -> Memo<'U>

/// `createMemoOn` whose output `'U` cutoff uses `comparer` (`'T` uses the graph's equality policy).
val createMemoOnWithComparer :
    comparer: IEqualityComparer<'U> -> read: (unit -> 'T) -> compute: ('T -> 'U) -> Memo<'U>

/// Owning counterpart to `createMemoOn`: `read` is pure and tracked; when `'T` changes,
/// any scope owned by the previous `compute` run is discharged untracked, and `compute`
/// runs untracked with an owning scope (`ScopeMode.Owning`).
val createMemoWithOn :
    read: (unit -> 'T) -> compute: ('T -> 'U) -> Memo<'U>

/// `createMemoWithOn` whose output `'U` cutoff uses `comparer`.
val createOwningMemoOnWithComparer :
    comparer: IEqualityComparer<'U> -> read: (unit -> 'T) -> compute: ('T -> 'U) -> Memo<'U>
```
*(Builder call: `read` uses `graph.EncodedComparer<'T>()` for the input cutoff, matching `createEffectOn`; callers needing custom input key equality can wrap the key in a struct/type with custom equality or a `createMemoWithComparer`.)*

**Exact two-phase semantics for `createMemoOn` / `createMemoWithOn`**:
1. **Phase 1 (`read: unit -> 'T`)**: Runs tracked under `ScopeMode.Pure` — creating nodes inside `read` always fails with `ScopeMessages.forMode ScopeMode.Pure`, even in `createMemoWithOn`, because child node lifetimes belong to the `'T`-keyed `compute` phase rather than `read` re-checks.
   - If `read` raises `NotReadyException` (`Pending`) or throws an exception `ex`: any scope owned by a prior `compute` run in `createMemoWithOn` is discharged untracked, the cached input key (`hasInputKey`) is cleared so recovery re-runs `compute` cleanly, and the memo records `Pending` or `Failed(ex, origin)` on `read`'s tracked sources.
   - If `read` succeeds with `nextInput` and a prior run has a committed input key (`hasInputKey = true` with `Status.None`) where `ComparerSentinel.equals inputEqual lastInput nextInput` is `true`: **neither** scope discharge **nor** `compute` runs; `value` and `ScopeField` stay intact, and downstream observers stay clean.
2. **Phase 2 (`compute: 'T -> 'U`)**: Runs only when `not hasInputKey` or `nextInput` differs from `lastInput`. In `createMemoWithOn` / `createOwningMemoOnWithComparer`, any previous `ScopeField` is discharged untracked first. `compute nextInput` then runs with dependency tracking **closed** (`Untracked` — reads of `Signal.Value` / `Memo.Value` inside `compute` add zero edges to this memo; pure forms enforce `ScopeMode.Pure`, while owning forms bind `ScopeMode.Owning` lazily on first child node or `onCleanup` registration).
   - If `compute` throws an exception (or raises `NotReadyException` from an untracked pending read, which fails with `InvalidOperationException` matching `EffectOn`'s untracked action phase because no upstream edge was tracked to wake the memo): `hasInputKey` is cleared and the memo enters `Status.Error` (`ErrorOrigin = this`), waking downstream observers if status/failure moved.
   - If `compute` returns `result`: `lastInput <- nextInput; hasInputKey <- true`, and output cutoff `ComparerSentinel.equals outputEqual previous result` determines whether `value` moved and downstream observers are notified.

#### Async Memos
```fsharp
/// Tracked synchronous `read: unit -> 'T`; when `'T` changes (or no flight has started for `'T`),
/// runs `compute` untracked under the graph's `FlightPolicy`. Equal `'T` leaves any active flight
/// or settled `'U` untouched.
val createAsyncOn :
    read: (unit -> 'T) ->
    compute: ('T -> Previous<'U> -> CancellationToken -> Task<'U>) ->
    AsyncMemo<'U>

/// Owning counterpart to `createAsyncOn`: child nodes/cleanups created by `compute` before its
/// first suspending `await` belong to the flight's scope, discharged only when a new flight starts
/// (on unequal `'T`) or on disposal.
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

**Exact two-phase semantics for `createAsyncOn*`**:
- On initial pull or when a source of `read` marks the `AsyncMemo` stale (`Dirty` / `Check`):
  1. `read ()` runs synchronously and tracked (`ScopeMode.Pure`).
  2. If `read ()` suspends (`NotReadyException`) or throws `ex`: any active flight is cancelled/discharged per policy, `hasKey` is cleared, and the `AsyncMemo` enters `Pending` (waiting on `pendingSource`) or `Error`.
  3. If `hasKey && ComparerSentinel.equals keyEqual lastKey nextKey`: `sources` updates to the edges read by this `read ()` pass, `freshness` becomes `Clean`, and **no flight is cancelled, deferred, or started** — any in-flight `Task<'U>` continues undisturbed, and any settled `'U` or flight failure for `lastKey` remains as-is until `read` produces an unequal key.
  4. If `not hasKey || not (equals keyEqual lastKey nextKey)`: `lastKey <- nextKey; hasKey <- true`, and the flight state machine (`CancelPrevious`, `KeepLatest`, `Queue`, `FinishCurrent`, `Drop`, `DropWhilePending`) launches `compute nextKey previous token` **untracked** (dependency window closed before `compute` is invoked, so even synchronous `await` continuations inside `compute` never record reactive dependencies!). Under `FinishCurrent` / `Drop` / `DropWhilePending` when a flight is already in progress (`queued <> 0`): `FinishCurrent` defers the re-check until the current flight settles (so `lastKey` is NOT overwritten until a flight actually launches for `nextKey`!), whereas `Drop` / `DropWhilePending` discards the trigger without overwriting `lastKey` (so if `'T` changed `A -> B` while dropped, `lastKey` remains `A`).

#### Stream Memos
```fsharp
/// Tracked synchronous `read: unit -> 'K`; starts `start key token` untracked, and restarts the
/// stream only when `read` settles on a key unequal to the running/completed stream's key.
val createStreamOn :
    read: (unit -> 'K) ->
    start: ('K -> CancellationToken -> StreamSource<'T>) ->
    StreamMemo<'T>

/// Fold counterpart to `createStreamOn`: restarts from `seed` only when `read` produces an unequal `'K`.
val createStreamFoldOn :
    seed: 'S ->
    fold: ('S -> 'T -> 'S) ->
    read: (unit -> 'K) ->
    start: ('K -> CancellationToken -> StreamSource<'T>) ->
    StreamMemo<'S>
```

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
- **Closed dependency window**: Every `mapping` / `binder` runs **untracked** (`ScopeMode.Pure` for `map`/`map2`/`zip` and for `AsyncMemo.bind`/`StreamMemo.bind`'s task/stream factory; lazy `ScopeMode.Owning` for `Signal.bind`/`Memo.bind`'s outer `'T -> Memo<'U>` stage so dynamically constructed inner `Memo<'U>` nodes are owned and disposed when outer `'T` changes, while pre-existing inner `Memo<'U>` nodes allocate zero `Owner`!).
- **Null checks & cross-graph checks**:
  - Null `source` / `source1` / `source2` / `mapping` / `binder` raises `ArgumentNullException` immediately at construction.
  - Rather than reading `Graph.Current` (which requires `graph.Activate()`), `map`/`map2`/`zip`/`bind` take their `Graph` directly from `source.Graph` (just as `Projection.*` takes `upstream.Graph` in `Combinators.fs`!). In `map2` / `zip`, if `not (obj.ReferenceEquals(source1.Graph, source2.Graph))`, raise `InvalidOperationException` immediately at construction; in `Signal.bind` / `Memo.bind`, if `binder x` returns `null` or a `Memo<'U>` from a different `Graph`, fail the node with `NullReferenceException` / `InvalidOperationException` (matching node error semantics).
  - Attachment to owner: attaches to `source.Graph.CurrentOwner` (the active scope on `source.Graph`, or `source.Graph.Root` when at root, matching `Memo.Create(graph, ...)`).
- **`Pending` / `Failed` left-to-right precedence in `map2` and `zip`**:
  - Links both static upstream edges once on first read (zero edge churn across transient `Pending`/`Failed` transitions), refreshes `source1` then `source2`, and applies left-to-right status precedence: if `source1` is `Pending` or `Failed`, propagate `source1`'s `Pending` / `Failure` (`ErrorOrigin` preserved); else if `source2` is `Pending` or `Failed`, propagate `source2`'s `Pending` / `Failure`; else invoke `mapping v1 v2` untracked.
- **Split `bind` inner-tick preservation**:
  - `Signal.bind f s` and `Memo.bind f m`: when `s` / `m` changes to an unequal `'T`, `f x` runs untracked in a lazy owning scope to yield `inner: Memo<'U>`, and the returned `Memo<'U>` subscribes to `inner`. When `inner` subsequently updates while `s` / `m` is unchanged, the returned `Memo<'U>` pulls `inner` directly **without re-running `f x` or discharging `f`'s scope**.
  - `AsyncMemo.bind` / `AsyncSource.bind`: implemented directly atop `AsyncMemo`'s `On` machinery (`read = fun () -> source.Value`, `compute = fun x _ ct -> binder x ct`), so `Pending` / `Failed` upstream propagates without starting a flight, and settling to an equal `'T` preserves the active/settled flight.
  - `StreamMemo.bind` / `StreamMemo.bindFold`: implemented directly atop `StreamMemo`'s `On` machinery (`read = fun () -> source.Value`, `start = binder`).

---

### 3.3 C# Facade (`src/Ranvier.CSharp/Reactive.fs` & `Extensions.fs`)

- **`Reactive` static factories (`Reactive.fs`)**:
  - `MemoOn<T, U>(Func<T> read, Func<T, U> compute)` and `(..., IEqualityComparer<U> comparer)`
  - `OwningMemoOn<T, U>(Func<T> read, Func<T, U> compute)` and `(..., IEqualityComparer<U> comparer)`
  - `AsyncOn<T, U>(Func<T> read, Func<T, CancellationToken, Task<U>> compute)` (+ `FlightPolicy`, `Func<T, Previous<U>, CancellationToken, Task<U>>`, and `OwningAsyncOn` overloads matching `Reactive.Async`)
  - `StreamOn<K, T>(Func<K> read, Func<K, CancellationToken, StreamSource<T>> start)` and `StreamFoldOn<K, S, T>(S seed, Func<S, T, S> fold, Func<K> read, Func<K, CancellationToken, StreamSource<T>> start)`
- **`RanvierExtensions` LINQ/combinator extensions (`Extensions.fs`)**:
  - `Select` (`map`) on `Signal<T>`, `Memo<T>`, `AsyncMemo<T>`, `AsyncSource<T>`, `StreamMemo<T>` -> `Memo<U>`
  - `Zip` (`(first, second) -> Memo<(A, B)>` ValueTuple / tuple AND 3-arg `(first, second, Func<A, B, C>) -> Memo<C>`) on `Signal`, `Memo`, `AsyncMemo`, `AsyncSource`, `StreamMemo`
    *(Builder call: C# idiomatic 2-arg `Zip` returns `Memo<struct (A * B)>` (`ValueTuple<A, B>`), matching `System.Linq.Enumerable.Zip` in modern .NET!)*
  - `SelectMany` (`bind`, both 2-arg `(source, binder)` and 3-arg LINQ query-syntax `(source, collectionSelector, resultSelector)`) on `Signal<T>` (`Func<T, Memo<U>>`), `Memo<T>` (`Func<T, Memo<U>>`), `AsyncMemo<T>` (`Func<T, CancellationToken, Task<U>>`), `AsyncSource<T>` (`Func<T, CancellationToken, Task<U>>`), and `StreamMemo<K>` (`Func<K, CancellationToken, StreamSource<T>>`).

---

## 4. Runtime Architecture: Static `Memo` Fast Paths & Unobserved `map`-Chain Fusion

### 4.1 Static-Edge `Memo<'U>` (`computeKind` Specialization)
`Memo<'U>` in `src/Ranvier/Core.fs` already dispatches by `computeKind: byte` + `RunCallback: Func<MemoNode, Node, bool>` (`.NET`) / `RunFromNode` (`Fable`). We extend `Memo<'U>` with static-edge descriptors so all combinators return the existing `Memo<'U>` type without public type proliferation:

1. **Static 1-source `Map` descriptor (`Signal<'T>` root, `Memo<'T>` root, or `ISource` async/stream root)**:
   - Links its single upstream source **once** on first read (`node.SourcesField` fixed at `count = 1`, `cursor = 1`) and unlinks once on `Dispose`.
   - Steady-state recompute skips `SourcesField.BeginRun()`, `EnterHostedTop` / `LeaveHostedTop`, `Graph.Track`, and `cursor < count` trim checks; sets a lightweight untracked-pure guard (`HasUntrackedHost`) while invoking the typed `'T -> 'U` delegate (`OptimizedClosures.FSharpFunc` / direct call) against upstream's settled value (`Signal.Peek` / `Memo.Peek`), then applies `ComparerSentinel.equals`.
   - When the root is a `Signal<'T>` and `tail = Signal.map ...` is `FreshDirty` with `0` observers (the `chain` tail-read pattern!), `tail.Value` refreshes inline without `EnterPull` / `RefreshChain`: validate thread affinity (`Guarded` token check), run the fused mapping on `signal.Peek` under the untracked-pure guard, store `value`, clear `FreshDirty`, and return `value`.

### 4.2 Unobserved `map`-Chain Fusion (Beating `R3.chain`)
In untraced builds (`!RANVIER_TRACE`) when `Platform.isNull graph.Journal`:
- Every `Memo<'B>` created by `*.map` carries an internal optional `MapFusion` descriptor recording:
  - `Root`: the root `ISource` (`Signal<'R>`, non-fusable `Memo<'R>`, `AsyncMemo<'R>`, `AsyncSource<'R>`, or `StreamMemo<'R>`),
  - `Steps`: up to 4 typed/untyped function steps inline (avoiding nested closure chain overhead for depths `1..4`, collapsing into a composed prefix beyond depth `4`), paired with each intermediate `MemoNode` reference `StepNodes` for exact `ErrorOrigin` attribution!
- **When `m2 = Memo.map g m1` fuses**:
  - Fusion triggers when `m1` has a `MapFusion` descriptor, `not m1.Node.IsDisposed`, `m1.Node.ObserverCount = 0`, `m1.Node.Flags` is still uninitialized/unread (`RunsField = 0`), and `SafeEquality.isSafe m1.Equal` (reference/primitive Leibniz equality where `a = b => g a = g b`).
  - Because `Memo` is lazy in `Ranvier`, `m1` has **not subscribed to `Root` yet**! `m2` builds a extended `MapFusion` pointing directly to `m1`'s `Root` with `(m1.Steps + (m2.Node, g))`.
  - When `m_k.Value` is read, **only `m_k` subscribes to `Root`** (`Root` has `1` observer, not `k`!).
  - **What if someone later reads `m1.Value` or attaches a second observer `m3` to `m1`?** Because `m1` still retains its own `MapFusion` descriptor, reading `m1.Value` or observing `m1` simply initializes `m1` against `Root` normally — both `m1` and `m2` remain 100% valid, independent `Memo` instances!
  - **Exact `ErrorOrigin` preservation on exceptions**: During fused step evaluation `y1 = f1 x; y2 = f2 y1; ...`, a local `mutable stepIdx = 0` tracks which stage is executing; on the cold `catch` path, `Failure(ex, stepNodes[stepIdx])` attributes the thrown exception to the exact `MemoNode` (`m1` vs `m2` vs `m_k`) that would have failed in an unfused chain!
  - **In `RanvierTrace=true` or journaled graphs**: `MapFusion` is disabled so every intermediate `Memo` node subscribes, runs, and emits full causal `TraceEvent` records.

### 4.3 Benchmark Addition (`bench/Ranvier.Counters`)
- Preserve the existing `chain` scenario (`Ranvier` dynamic `Memo` vs `FSharp.Data.Adaptive` vs `R3`) intact per `CONTRIBUTING.md` ("Preserve compiler diagnostic fixtures and benchmark baselines; changing their syntax can change the behavior they are intended to test").
- Add `Ranvier (Memo.map)` (using `source |> Signal.map (+ 1) |> Memo.map (+ 1) |> Memo.map (+ 1) |> Memo.map (+ 1)`) and `Ranvier (createMemoOn)` entries to `chain` (or a companion `chain-map` scenario if `Perf` compare requires existing scenario engine sets unchanged — **Builder check item**: verify `perf compare` behavior on new engines in an existing scenario vs new scenario; adding a new engine or new scenario in `Scenarios.fs` never triggers a regression because `perf compare` compares matching `(Runtime, Scenario, Engine)` pairs present in both `base` and `head`!). Including `"Ranvier (Memo.map)"` directly inside `chain` lets CI `comment.md` display `R3` (`318` insns), `Ranvier` (`1,240` insns), and `Ranvier (Memo.map)` (`< 250` insns target) side by side!

---

## 5. Formal Proof Boundary (`proofs/`)

Per `ranvier-proof-and-perf-campaign`, every invariant is classified before authoring `.v` or `.fs` files:

| Invariant / Theorem | Layer | Obligation |
|---|---|---|
| Core `Signal`/`Memo`/`Effect`/`Owner` LIFO & glitch-freedom (`B1`–`B15`) | **Inherited** (`proofs/theories/`) | Existing `544` closed theorems remain untouched and green. |
| **B16 (`memo_on_cutoff_eq`)**: Two-phase `(read, compute)` skips `compute` when `eq_T prev_t next_t = true` after prior settled run, and clears cached key on `Pending`/`Error` in `read`. | **Pure Rocq** (`Pure/Combinators.v` or `Pure/FastPaths.v`) | Constructive proof over `Reading` (`Ready \| Pending \| Failed`) + state record; registered in `Audit.v`. |
| **B17 (`async_on_flight_preservation_eq`)**: Two-phase async `(read, launch)` preserves active in-flight generation and settled value when `read` settles to `eq_T`-equal key, and updates key under `FinishCurrent`/`Drop`/`DropWhilePending` only when a flight actually launches. | **Pure Rocq** (`Pure/Combinators.v`) | Constructive state-machine refinement across all 6 `FlightPolicy` cases; registered in `Audit.v`. |
| **B18 (`fused_map_chain_eq_unfused`)**: Under Leibniz-congruent intermediate equality (`eq_B b1 b2 = true -> b1 = b2`), evaluating a fused $k$-step `MapFusion` chain over `Reading<'R>` produces the exact same `Reading<'U>`, output cutoff decision, and `ErrorOrigin` node id as $k$ staged `Memo.map` nodes. | **Pure Rocq** (`Pure/Combinators.v`) | Induction on chain length $k$ covering `Pending`, upstream `Failed(ex, orig)`, intermediate step throw at index $i$ (`ErrorOrigin = node_i`), and `Ready`; registered in `Audit.v`. |
| **B19 (`map2_zip_status_precedence_eq`)**: Static `map2` / `zip` over two `Reading` inputs is glitch-free after batch settlement and matches left-to-right tracked `(let x = a.Value in let y = b.Value in f x y)` for all `Ready`/`Pending`/`Failed` combinations. | **Pure Rocq** (`Pure/Combinators.v`) | Exhaustive case analysis over `3 × 3` `Reading` pairs; registered in `Audit.v`. |
| **B20 (`split_bind_inner_tick_eq`)**: Split `bind` (`outer: 'T -> InnerId`, `inner: InnerId -> Reading<'U>`) produces the same `Reading<'U>` as re-evaluating `outer` on every tick while invoking `outer` (`0` vs `1` times) strictly when `'T` moves, discharging the previous outer scope iff `outer` re-runs or `bind` disposes. | **Pure Rocq** (`Pure/Combinators.v`) | State-transition simulation + outer-run count & scope-discharge invariant; registered in `Audit.v`. |

All new theorems must pass `Print Assumptions` (`Closed under the global context`, `0` axioms) in `proofs/theories/Audit.v`, `proofs/CORRESPONDENCE.md` updated, and `bash proofs/check.sh` + `bash proofs/site.sh` passing cleanly.

---

## 6. Builder Decisions vs Open Choices Left to Implementation

### Decided with User
- `*On` primitives take `read: unit -> 'T` + consuming `'T -> ...` with closed dependency window, across `Memo`, `AsyncMemo`, and `StreamMemo` (plus owning `*WithOn` / `*Owning*On*` forms).
- Companion combinator modules provide `map`, `map2`, `zip`, and `bind` per concrete reactive type (`Signal`, `Memo`, `AsyncMemo`, `AsyncSource`, `StreamMemo`) + C# `Reactive.*On` and `.Select`/`.Zip`/`.SelectMany`.
- Unobserved `map` chains fuse in untraced builds so mapped chains outperform `R3.chain`.
- Shared read-only reactive interface (`IReadOnlySignal<'T>`) is deferred until computation-expression DSL work.

### Settled by Designer (Marked as Designer's Call for Review)
1. **Comparer customization on `createMemoOnWithComparer`**: Customizes the output `'U` comparer (matching `createMemoWithComparer` and `createOwningMemoWithComparer`), while input `'T` uses `graph.EncodedComparer<'T>()`.
2. **`AsyncMemo.bind` / `AsyncSource.bind` & `StreamMemo.bind` signatures**: Chain task/stream factories (`'T -> CancellationToken -> Task<'U>` and `'K -> CancellationToken -> StreamSource<'T>`) rather than nested node-returning `'T -> AsyncMemo<'U>`, avoiding double `CancellationTokenSource` / double flight-policy management while directly reusing the `*On` engine.
3. **C# 2-argument `.Zip` return type**: Returns `Memo<struct ('A * 'B)>` (`ValueTuple<A, B>`), matching `System.Linq.Enumerable.Zip` in modern .NET, alongside the 3-argument `Zip(first, second, Func<A, B, C>)` overload (`map2`).
4. **Fusion eligibility**: Active when `!RANVIER_TRACE`, `Platform.isNull graph.Journal`, intermediate stage has `SafeEquality.isSafe` and `0` observers (`RunsField = 0`), preserving exact per-stage `ErrorOrigin` via step index tracking on the `catch` path.

### Left to the Builder during Planning & Implementation
- Exact internal `computeKind` tag numbering and struct/class layout of `MapFusion` / `MemoOn` state inside `Core.fs` (subject to Clef-compatible mutable record / `val mutable` discipline where zero-cost and NativeAOT `--reflectionfree` safety).
- File organization between `Core.fs`, `Streams.fs`, `Combinators.fs`, and `Api.fs` to respect F# single-pass compilation order in `Ranvier.fsproj`.
- Exact version bump (`0.2.3` or `0.3.0` for new minor public APIs in pre-`1.0` packages) when preparing any subsequent release PR.
