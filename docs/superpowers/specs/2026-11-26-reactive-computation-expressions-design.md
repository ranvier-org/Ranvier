# Reactive Computation Expressions, Collection Read Descriptors, and Companion Static Analyzers

Date: 2026-11-26
Branch: `feat/computation-expressions`
Baseline: `b8ee210dad43fcc2807f080a6e2523842d201359` (`origin/master` after PR `#389`, `Ranvier` `0.2.2` / `Ranvier.Dom` `0.1.1`)
Companion Repository: `ranvier-org/Ranvier.View` (`tools/Ranvier.Analyzers`, `tools/Ranvier.Analyzers.Tests`, `docs/content/analyzers/`)
Status: draft — pending builder check and user approval.

---

## 1. Intent and Why

PR `#389` (`b8ee210`) completed `Ranvier`'s closed-window `*On` family (`createMemoOn*`, `createAsyncOn*`, `createStreamOn*`), static combinator modules (`Signal`, `Memo`, `AsyncMemo`, `AsyncSource`, `StreamMemo` `map`/`map2`/`zip`/`bind`), and unobserved `map`-chain fusion (`chain-map`: `507` ARM64 instructions, `123.41` cycles, `0 B/op` on `.NET` vs `Ranvier` dynamic `chain`'s `1,244` instructions; `734.8` tiered `node` instructions vs `2,708.8`, all figures `measured here` in GitHub Actions `Perf` run `45732453778`).

While C# callers already enjoy LINQ query comprehension syntax (`.Select`, `.Zip`, `.SelectMany`) over those fast primitives (`from documentation`: `src/Ranvier.CSharp/Extensions.fs`), F# callers composing multiple reactive sources, `AsyncMemo` flights, `StreamMemo` sources, `Boundary` fallbacks, or per-key `Projection` row readers still face three ergonomic and performance friction points:

1. **Manual primitive selection vs. dynamic tracking overhead**: Writing `createMemo (fun _ -> a.Value + b.Value)` is ergonomic, yet pays for dynamic `SourceList` tracking, requires an active `Graph.Current`, leaves dependency tracking open across helper calls, and forfeits `Signal.map` / `Memo.map` fusion and `*On` settled-key cutoffs. Writing `Signal.map2` / `createMemoOn` / `createAsyncOn` manually gets optimal runtime dispatch at the cost of nested lambdas and explicit struct tuple packing once 2+ heterogeneous sources (`Signal` + `Memo` + `Projection` row) or conditional `match!` paths meet.
2. **Two-phase async & stream ceremony**: `createAsync` and `createAsyncOn` pass `(prev: Previous<'U>) (ct: CancellationToken)` into a nested `task { ... }`, making accidental post-`await` reactive reads (`RAN012`) easy to write unless Phase 1 reactive reads and Phase 2 `Task` suspension are structurally separated.
3. **Collection pointwise reads and `row` double-thunk ceremony**: `createProjectionWith` and `Projection.mapWith` require `(fun item -> fun () -> ... (item ()) ...)`, and pointwise lookups (`rows.TryGet key`, `lookup.Get key`) lack zero-allocation bindable descriptors that carry `.Graph` into `let!` / `match!` without `graph.Activate ()`.

Before cutting a `1.0` release, we want idiomatic F# computation expressions (`memo`, `effect`, `asyncMemo`, `stream`, `editable`, `draft`, `boundary`, `row`), pointwise collection descriptors, and companion `Ranvier.Analyzers` build-time diagnostics that compile straight down to the fastest applicable primitive with zero reflection and zero steady-state overhead.

---

## 2. Goals, Non-Goals, and Anti-Goals

### Goals
1. **Eight singleton computation expression builders in `[<AutoOpen>]` `Ranvier`** (user-decided):
   - `memo`, `effect`, `asyncMemo`, `stream`, `editable`, `draft`, `boundary`, and `row`.
2. **Header-only `[<CustomOperation>]` configuration** (user-decided):
   - Builder settings (`owning`, `comparer`, `option`, `node`, `policy`, `fold`, `pending`, `failed`) are expressed via custom operations restricted by the F# type system to the header of the CE (before the first `let!` / `match!` / body statement), without `[<MaintainsVariableSpace>]` tuple allocations (`0` anonymous tuples emitted on both `fsc` and `Fickl 0.13.0`, `measured here` via `dotnet fsi` and `dotnet tool run fickl` probes).
   - Duplicate or mutually exclusive header operations (e.g., two `comparer` ops, `owning` + `option` on `memo`, `comparer` on a 0-`let!` `effect`, or a `boundary` missing both `pending` and `failed`) fail at compile time (`FS0001` / `FS0193` / `FS0041`).
3. **Builder-scoped `inline` context tokens** (user-decided):
   - Context values valid only inside a specific CE (`memo.previous`, `memo.previousOr seed`, `editable.previous`, `editable.previousOr seed`, `draft.previous`, `draft.previousOr seed`, `asyncMemo.previous`, `asyncMemo.cancellationToken`, `stream.cancellationToken`) are exposed as `inline` members on their respective singleton builder instances for IDE dot-discoverability, zero `[<AutoOpen>]` namespace clutter, compile-time rejection inside the wrong builder, and inert zero-exception safety if referenced outside a CE.
4. **Compile-time primitive selection & closed-window Phase 2 by default** (user-decided):
   - 1-source `memo { let! x = s; return f x }` (default header) compiles to `Signal.map` / `Memo.map` / `FusedSourceMap1`, preserving end-to-end `MapFusion` (`B18`) and resolving `s.Graph` without `graph.Activate ()`.
   - Homogeneous 2-source `let! a = s1; and! b = s2; return f a b` (default header) compiles to `Signal.map2` / `Memo.map2` (`B19`).
   - Heterogeneous 2..$N$-source `and!` chains, pointwise collection descriptors, and configured headers (`owning`, `comparer`) compile to `*On` (`Memo.CreateOn`, `EffectOn`, `AsyncMemo.CreateOn`, `StreamMemo.CreateOn` / `CreateFoldOn`, `B16`–`B17`) over nested `struct ('A * 'B)` keys (`0` heap tuple allocations on `.NET`) without quadratic $O(K^2)$ overload explosion.
   - Pure `return!` tail-binds (`let! x = s; return! innerMemo x` and multi-arm `match! s with | A -> return! m1 | B -> return! m2`) compile to `Signal.bind` / `Memo.bind` (`SplitBind` `B20`).
   - Dynamic multi-step sequential `let!` and mixed `match!` (`| None -> return 0 | Some x -> let! y = s; return x + y`) compile to a single dynamic node whose Phase-1 `let!` reads run tracked and whose terminal `return` / action body runs with tracking closed (`EnterUntrackedPure` / `RunUntracked`), allocating `0` child nodes or closures per evaluation.
   - `asyncMemo` enforces the two-phase reactive-before-await boundary in the F# type system: any reactive `let!` placed *after* a `Task` / `ValueTask` `let!` or `do!` is rejected at compile time.
5. **Pointwise collection read descriptors, `Editable`/`Boundary`/`Timed` unwrapping, and `row` reader CE** (user-decided):
   - Support `let!` / `and!` / `match!` over all 8 reactive value types (`Signal<'T>`, `Memo<'T>`, `AsyncMemo<'T>`, `AsyncSource<'T>`, `StreamMemo<'T>`, `Boundary<'T>`, `Editable<'T>`, `Timed<'T>`) plus zero-allocation `[<Struct>]` descriptors `Projection.at`, `Projection.tryAt`, `Projection.keys`, `Lookup.at`, `Lookup.tryAt`, `KeyedCollection.at`, `KeyedCollection.tryAt`, and `KeyedCollection.keys`.
   - `row { ... }` additionally unwraps `unit -> 'T` item accessors so `createProjectionWith`, `createProjectionWithOptions`, `createIndexProjectionWith`, and `Projection.mapWith` eliminate manual nested thunks while closing dependency tracking before `return`.
6. **Companion `Ranvier.Analyzers` updates & new CE static analysis rules** (user-decided):
   - Extend `Symbols.Core` and `RAN010`, `RAN011`, `RAN012`, `RAN015` in `ranvier-org/Ranvier.View` to cover PR `#389`'s `*On` / combinator APIs, `StreamMemo<'T>` / `Timed<'T>` reads, and all 8 CE builders (including automatic `owning` header insertion fixes on `RAN010`).
   - Ship new core rules `RAN016` (`Warning`: untracked reactive read inside a closed Phase-2 body), `RAN017` (`Hint` + automatic `let!` -> `and!` code fix for independent sequential reactive binds), and `RAN018` (`Hint`: 0-bind reactive CE performing direct lexical `.Value`/`.Get` reads instead of `let!`), each within `Budget.fs`'s `250 ms` per-rule ceiling (`from documentation`: `tools/Ranvier.Analyzers.Tests/Budget.fs:1-4`).
7. **Formal Rocq 9.1 verification (`proofs/`) & Dual-Target / NativeAOT Parity**:
   - Prove `B21` (`ce_dispatch_and_nested_eval_eq` and `merge_sources_nary_precedence_eq`) in `proofs/theories/Pure/Combinators.v` (`0` axioms in `Audit.v`, `CORRESPONDENCE.md` updated).
   - Full parity across `.NET` (`net10.0`, `netstandard2.1`, `--reflectionfree` NativeAOT, untraced + `RANVIER_TRACE`) and `Fickl 0.13.0` (`node` + `node --jitless`), staying comfortably under `Ranvier.Analyzers`'s `4 GiB` FCS TypedTree memory ceiling.

### Non-Goals
- **F# quotations (`[<ReflectedDefinition>]`, `Expr<'T>`) or custom F# compiler plugins**: Explicitly rejected (user-decided). Quotations fail `--reflectionfree` NativeAOT, inflate JS bundles, and allocate expression trees at construction time; compiler transforms fork `.NET` vs `Fickl` / Rider semantics.
- **SQL-style collection query CE (`projection { where ...; sortBy ... }`)**: Explicitly excluded (user-decided). `MaintainsVariableSpace = true` packs CE locals into anonymous tuples per row; `Projection.filter`, `Projection.sortBy`, `Projection.take`, `Projection.foldGroup`, etc. remain point-free pipe combinators.
- **Roslyn analyzers for C#**: `Ranvier.CSharp` already uses idiomatic `.Select` / `.Zip` / `.SelectMany` LINQ extensions (`from documentation`: `src/Ranvier.CSharp/Extensions.fs`).

### Anti-Goals (Failure Conditions Even If Tests Pass)
- Emitting `FS1113` or cross-assembly `FS1118` inlining warnings/failures in `Debug` or `Release` consumers (`Ranvier.Tests`, `Ranvier.AotSmoke`, `Fickl` precompile) because an `inline` builder member referenced a `private` or `internal` symbol.
- Quadratic $O(K^2)$ `MergeSources` / `Bind` overload tables that inflate pickled F# signature/optimization metadata or cause `dotnet fsi --exec build.fsx -- analyze --group library --build` to exceed its `4 GiB` heap budget (`from documentation`: `fsharp-dsl-design` skill §3, `RELEASE-RUNBOOK.md:157`).
- Eagerly evaluating a 0-`let!` `memo { return asyncVal.Value }` outside its hosted `Memo` run at construction time (which would prematurely trigger `AsyncMemo` flights or throw `NotReadyException` during builder construction).
- Falsely cutting off a sequential dependent `let! a = sigA; let! b = constZeroMemo; return a + b` by keying only on `b` while `a` changed inside the continuation closure.
- Leaving `evalDepth` corrupted if user code throws inside a dynamic CE body or constructs a nested `memo { ... }` inside an `owning` CE's `return` phase.
- Breaking any existing `Perf` hardware instruction counter baseline on `.NET` (`arm64-counters`, `x64-timing`) or `node --jitless`.

---

## 3. Public API Design (`ranvier-org/Ranvier`)

All public types and builders below live in namespace `Ranvier` (`src/Ranvier/Builders.fs`, compiled immediately after `Combinators.fs` and before `TraceModel.fs` in `src/Ranvier/Ranvier.fsproj`). Builder singletons live in `[<AutoOpen>] module Builders`. Infrastructure carrier structs and factory interfaces required by `inline` builder methods live in `namespace Ranvier.Internal` (or `module BuildersInternal`) with `public` visibility and `[<EditorBrowsable(EditorBrowsableState.Never)>]` so cross-assembly `Release` and `Fickl` inlining never triggers `FS1113` / `FS1118`.

### 3.1 Bindable Reactive Sources & Pointwise Collection Descriptors

#### Shared Reactive Input Contract
Every single-value reactive node and pointwise collection descriptor implements `[<EditorBrowsable(EditorBrowsableState.Never)>] type IReactive<'T>`:
```fsharp
[<EditorBrowsable(EditorBrowsableState.Never)>]
type IReactive<'T> =
    abstract Graph: Graph
    /// Tracked read of the current value (raising NotReadyException when Pending, or the source's error when Failed).
    abstract ReadTracked: unit -> 'T
```
*(Designer call: Implementing `IReactive<'T>` directly on `Signal<'T>`, `Memo<'T>`, `AsyncMemo<'T>`, `AsyncSource<'T>`, `StreamMemo<'T>`, `Boundary<'T>`, `Editable<'T>`, and `Timed<'T>` adds zero fields and zero per-instance bytes on `.NET` and `Fable` while collapsing heterogeneous `MergeSources` and `Bind` from $8^2 = 64+$ overloads down to 1 interface fallback alongside specialized `Signal` and `Memo` fast overloads.)*

Internal core hooks added to existing types (`0` public breaking changes):
- `Editable<'T>` (`Api.fs`): add `member internal _.Graph = graph` and `member internal _.ValueMemo = value`, plus `interface IReactive<'T>` delegating to `value.Value`.
- `Boundary<'T>` (`Core.fs`): add `member internal _.Graph = graph`, `internal BoundaryAdapter<'T> : CombinatorSource<'T>`, and `interface IReactive<'T>`.
- `Timed<'T>` (`Timed.fs`): add `member internal _.Graph = graph`, `internal TimedAdapter<'T> : CombinatorSource<'T>`, and `interface IReactive<'T>`.
- `Signal<'T>`, `Memo<'T>`, `AsyncMemo<'T>`, `AsyncSource<'T>`, `StreamMemo<'T>`: implement `IReactive<'T>` (`Graph` + `Value`).

#### Pointwise Collection Descriptors
All collection descriptors are `[<Struct; NoEquality; NoComparison>]` on `.NET` (`#if FABLE_COMPILER` lightweight `[<Sealed>]` record/class if smaller in JS, matching `Stamp<'T>` in `Api.fs:55-90`) implementing `IReactive<_>`, validating non-null collection arguments (`ArgumentNullException`) on construction:

```fsharp
[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Projection =
    // Added alongside existing Projection combinators:
    /// Pointwise tracked read of `projection.Get key` (raises KeyNotFoundException while `key` is absent).
    val inline at : key: 'K -> projection: Projection<'K, 'V> -> ProjectionAt<'K, 'V>
    /// Pointwise tracked read of `projection.TryGet key` (`Some v` when live, `None` when absent).
    val inline tryAt : key: 'K -> projection: Projection<'K, 'V> -> ProjectionTryAt<'K, 'V>
    /// Tracked read of `projection.Keys` (`'K[]`).
    val inline keys : projection: Projection<'K, 'V> -> ProjectionKeys<'K, 'V>

[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module Lookup =
    /// Pointwise tracked read of `lookup.Get key` (creates and tracks the key's cell on demand).
    val inline at : key: 'K -> lookup: Lookup<'K, 'V> -> LookupAt<'K, 'V>
    /// Tracked read of `lookup.TryGet key` if the key's cell is currently live; `None` untracked without creating a cell otherwise.
    val inline tryAt : key: 'K -> lookup: Lookup<'K, 'V> -> LookupTryAt<'K, 'V>

[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module KeyedCollection =
    /// Equivalent to `Projection.at key collection.Rows`.
    val inline at : key: 'K -> collection: KeyedCollection<'K, 'V> -> ProjectionAt<'K, 'V>
    /// Equivalent to `Projection.tryAt key collection.Rows`.
    val inline tryAt : key: 'K -> collection: KeyedCollection<'K, 'V> -> ProjectionTryAt<'K, 'V>
    /// Equivalent to `Projection.keys collection.Rows`.
    val inline keys : collection: KeyedCollection<'K, 'V> -> ProjectionKeys<'K, 'V>
```

---

### 3.2 Builder Catalog, Context Tokens, and Header Custom Operations

| Builder singleton | Output type | Context token members (`inline` on singleton) | Header `[<CustomOperation>]`s (before first `!` / body) |
|---|---|---|---|
| `memo` | `Memo<'U>` | `memo.previous` (`'U voption`), `memo.previousOr (seed: 'U)` (`'U`) | `owning`, `comparer (cmp: IEqualityComparer<'U>)`, `option` |
| `effect` | `unit` (default) or `INode` (with `node`) | — | `node`, `comparer (cmp: IEqualityComparer<'K>)` |
| `asyncMemo` | `AsyncMemo<'U>` | `asyncMemo.previous` (`Previous<'U>`), `asyncMemo.cancellationToken` (`CancellationToken`) | `owning`, `policy (p: FlightPolicy)` |
| `stream` | `StreamMemo<'U>` | `stream.cancellationToken` (`CancellationToken`) | `fold (seed: 'S) (folder: 'S -> 'T -> 'S)` |
| `editable` | `Editable<'U>` | `editable.previous` (`'U voption`), `editable.previousOr (seed: 'U)` (`'U`) | — |
| `draft` | `Editable<'U>` | `draft.previous` (`'U voption`), `draft.previousOr (seed: 'U)` (`'U`) | — |
| `boundary` | `Boundary<'U>` | — | `pending (fn: 'U voption -> 'U)`, `failed (fn: exn -> 'U voption -> 'U)`, `comparer (cmp: IEqualityComparer<'U>)` |
| `row` | `unit -> 'V` | — | — |

#### Context Token Semantics (User-Decided)
- Each context token is a distinct `[<Struct; NoEquality; NoComparison>]` value with an `internal` constructor and zero public fields/methods:
  - `MemoPreviousToken`, `MemoPreviousOrToken<'U>` (for `memo`)
  - `EditablePreviousToken`, `EditablePreviousOrToken<'U>` (for `editable`)
  - `DraftPreviousToken`, `DraftPreviousOrToken<'U>` (for `draft`)
  - `AsyncPreviousToken`, `AsyncCancellationToken` (for `asyncMemo`)
  - `StreamCancellationToken` (for `stream`)
- Calling `memo.previous` or `asyncMemo.cancellationToken` outside a CE simply constructs an inert empty struct on the stack (`0` exceptions, `0` ambient state reads).
- Passing `asyncMemo.previous` to `memo`'s `let!` (or `memo.previous` to `editable`'s `let!`) fails to compile (`FS0041`) because only the owning builder defines `Bind` / `BindReturn` for its token type.
- *(Designer call: `memo.previous` / `memo.previousOr` cannot be combined with `option` header mode on `memo`, because `createOptionMemo` manages its own `Some` instance retention over `'A option`; attempting both fails at compile time.)*

#### Compile-Time Header State Transitions
Every builder with custom operations defines `Yield(_: unit)` returning a phantom-typed `[<Struct; NoEquality; NoComparison>]` header struct (with `MaintainsVariableSpace` omitted, so F# never packs variables into tuples):
1. **`memo`**: `MemoHeader<'Mode, 'Cmp>` where `'Mode` transitions `PureMode -> OwningMode` (`owning`) or `PureMode -> OptionMode` (`option`, only when `'Cmp = NoCmp`), and `'Cmp` transitions `NoCmp -> WithCmp<'U>` (`comparer cmp`, only when `'Mode <> OptionMode`). Duplicate `owning`, duplicate `comparer`, or `option` + `owning`/`comparer` fails to unify (`FS0193`).
2. **`effect`**: `EffectHeader<'Out, 'KeyCmp>` where `Yield(_: unit)` and `Zero()` both return `EffectHeader<UnitOut, NoCmp>`. `node` transitions `UnitOut -> NodeOut`; `comparer cmp` transitions `NoCmp -> WithCmp<'K>`. `For` transitions `EffectHeader<'Out, 'KeyCmp>` into `ConfiguredEffect<'Out, 'KeyCmp, 'K>` **only** when `'KeyCmp = NoCmp` OR the body has a static Phase-1 key `'K` (so `effect { comparer cmp; doWork () }` with `0` `let!` bindings is rejected at compile time!).
3. **`asyncMemo`**: `AsyncHeader<'Mode, 'Pol>` where `owning` transitions `PureMode -> OwningMode` and `policy p` transitions `DefaultPolicy -> ExplicitPolicy`.
4. **`stream`**: `StreamHeader<'Fold>` where `fold seed folder` transitions `LatestMode -> FoldMode<'S, 'T>`.
5. **`boundary`**: `BoundaryHeader<'Pend, 'Fail, 'Cmp>` where `pending fn` transitions `NoPending -> WithPending<'U>`, `failed fn` transitions `NoFailed -> WithFailed<'U>`, and `comparer cmp` transitions `NoCmp -> WithCmp<'U>`. `Run` is defined for `(WithPending, NoFailed)`, `(NoPending, WithFailed)`, and `(WithPending, WithFailed)` (each with `NoCmp` or `WithCmp`), and **omitted** for `(NoPending, NoFailed)` so an empty `boundary { let! x = a; return x }` without `pending` or `failed` fails at compile time!

---

## 4. Exact Compile-Time & Runtime Dispatch Architecture

### 4.1 Avoiding Both Overload Explosion and `match!` Type Mismatches

Two empirical F# compiler invariants govern the step representation (`measured here` in `dotnet fsi` `.NET SDK 10.0.404` and `Fickl 0.13.0` / `Fable 5.27.0`):
1. **`MergeSources` struct-tuple nesting**: Defining `MergeSources(Signal<'A>, Signal<'B>) -> MergedSignals<'A, 'B>`, `MergeSources(Memo<'A>, Memo<'B>) -> MergedMemos<'A, 'B>`, and `MergeSources(IReactive<'A>, IReactive<'B>) -> MergedReactive<'A, 'B>` (where all three implement `IReactive<struct ('A * 'B)>` and validate same-graph membership via `obj.ReferenceEquals(g1, g2)` at construction time, raising `InvalidOperationException UntrackedPhase.crossGraphNodes` on mismatch) handles:
   - `Signal` + `Signal` via `Signal.map2` (`SignalMap2State`),
   - `Memo` + `Memo` (including `Editable` via `.ValueMemo`) via `Memo.map2` (`MemoMap2State`), and
   - **any** 2-way, 3-way, or $N$-way heterogeneous `and!` combination across all 8 node types and 5 collection descriptors via right-nested `struct ('A * struct ('B * ...))` with **3 `MergeSources` overloads total** (`0` quadratic overload explosion).
2. **`match!` / `if` arm unification (`MemoStep<'U>`)**: F# syntax-desugars `match! src with | None -> return 0 | Some a -> let! b = s2; return a + b` into `Return(0)` in Arm 1 and `BindReturn(s2, ...)` in Arm 2 *before* typechecking. Therefore `Return`, `BindReturn`, and `Bind` on `memo` must share one unified `[<Struct; NoEquality; NoComparison>] MemoStep<'U>(inlineVal: 'U, factory: IMemoFactory<'U>)` return type, while `ReturnFrom(m: Memo<'U>)` returns a distinct `[<Struct>] MemoTail<'U>(m)` so pure `return!` chains select `SplitBind` (`B20`) at compile time.

#### Reentrant `evalDepth` Step Dispatch (`MemoStep<'U>`, Theorem `B21a`)
`InternalCeState` holds a thread-local (`[<ThreadStatic>]` on `.NET`; module `mutable` on single-threaded `#if FABLE_COMPILER`) `evalDepth: int`:
- **At top-level `memo.Run` (`evalDepth = 0`)**:
  - `memo.Run` saves and zeroes `evalDepth` (`let saved = suspendForNestedRun ()`) while invoking the outer `Delay` thunk `d ()` inside a `try ... finally` that restores `evalDepth <- saved`:
    - If `d` begins with `BindReturn`, `Bind`, `Combine`, `TryWith`, `TryFinally`, `Using`, `While`, or `For`, `d ()` **never** evaluates user compute callbacks or `.Value` reads; it immediately returns `MemoStep<'U>.OfFactory(factory)` where `factory` is the exact specialized node builder (`SignalMapFactory`, `MemoMapFactory`, `SourceMapFactory`, `SignalMap2Factory`, `MemoMap2Factory`, `MemoOnFactory`, `PreviousMemoFactory`, or `DynamicMemoFactory`).
    - If `d` is a 0-`let!` `memo { return expr }`: because F# evaluates `expr` inside `Delay(fun () -> Return(expr))`, running `d ()` unguarded outside a hosted `Memo` could prematurely read `Graph.Current`, trigger an `AsyncMemo` flight, or raise `NotReadyException` at builder construction time. Therefore `MemoRun.build` runs the construction probe `d ()` under an internal `Graph` probe guard (`IsProbingCe`): if `d ()` attempts any tracked reactive read or throws during the probe, `Graph` aborts the probe immediately before starting any `AsyncMemo` flight or recording any edge, and `MemoRun.build` wraps `d` directly into `Memo.CreateUnit(Graph.Current, (fun () -> ... (d ()).InlineVal), mode)` so `expr` runs lazily inside the hosted `Memo` on its first `.Value` read. Even when `d ()` succeeds purely without reactive reads (e.g. `memo { return 42 }`), `MemoRun.build` wraps `(fun () -> ... (d ()).InlineVal)` in `Memo.CreateUnit(Graph.Current, ..., mode)` so 0-`let!` bodies always execute on every memo run.
- **Inside a dynamic `Memo` evaluation (`evalDepth > 0`)**:
  - `DynamicMemoFactory` increments `evalDepth` around `body ()`.
  - Every nested `Bind` reads its source **tracked** on the enclosing `Memo`.
  - When execution reaches the terminal `BindReturn(src, f)` (for example inside `match!` Arm 2 `| Some a -> let! b = s2; return a + b`), `BindReturn` sees `IsEvaluating = true`: it reads `let v = src.ReadTracked()` **tracked** on the enclosing `Memo`, enters `UntrackedPhase` (`EnterUntrackedPure` / `RunUntracked` depending on pure vs owning mode) to **close dependency tracking**, computes `let res = f v` inline, and returns `MemoStep<'U>.OfInline(res)` on the stack (`0` heap allocations, `0` child nodes).
  - If an `owning` Phase-2 body constructs a nested `memo { ... }` (`memo.Run`), `suspendForNestedRun ()` temporarily resets `evalDepth <- 0` while constructing the inner `Memo` node and restores `evalDepth` in `finally`, so nested CEs inside owning CEs construct real child nodes owned by the outer Phase-2 scope.

---

### 4.2 Complete Primitive Selection Table

| CE Expression Shape | Header Ops | Selected Runtime Primitive | Graph Resolution |
|---|---|---|---|
| `memo { let! x = sig; return f x }` | *(none)* | `Signal.map f sig` (`FusedSignalMap1`, `B18`) | `sig.Graph` (no `Activate ()`) |
| `memo { let! x = mem; return f x }` | *(none)* | `Memo.map f mem` (`FusedExtendable` chain fusion, `B18`) | `mem.Graph` (no `Activate ()`) |
| `memo { let! x = edit; return f x }` | *(none)* | `Memo.map f edit.ValueMemo` (`B18`) | `edit.Graph` (no `Activate ()`) |
| `memo { let! x = asyncOrStreamOrBoundOrTimed; return f x }` | *(none)* | `CombinatorMemos.createSourceMap` (`FusedSourceMap1`, `B18`) | `x.Graph` (no `Activate ()`) |
| `memo { let! x = projAtOrLookupAt; return f x }` | *(none)* | `Memo.CreateOn(x.Graph, x.ReadTracked, f, Pure, ValueNone)` (`B16`) | `x.Graph` (no `Activate ()`) |
| `memo { let! a = s1; and! b = s2; return f a b }` (`Signal * Signal`) | *(none)* | `Signal.map2` (`SignalMap2State`, `B19`) | `s1.Graph` (cross-graph checked) |
| `memo { let! a = m1; and! b = m2; return f a b }` (`Memo/Editable` pair) | *(none)* | `Memo.map2` (`MemoMap2State`, `B19`) | `m1.Graph` (cross-graph checked) |
| `memo { let! a = r1; and! b = r2 [; and! c = r3 ...]; return f ... }` (any $2..N$ sources) | `owning`? `comparer`? | `Memo.CreateOn(r1.Graph, readStructTuple, f, mode, cmp)` (`B16`) | `r1.Graph` (cross-graph checked) |
| `memo { owning / comparer; let! x = r; return f x }` | `owning` and/or `comparer` | `Memo.CreateOn(r.Graph, r.ReadTracked, f, mode, cmp)` (`B16`) | `r.Graph` (no `Activate ()`) |
| `memo { option; ... }` | `option` | `createOptionMemo`-equivalent deduplicating `Some` wrapper (`Graph` from static source when present, else `Graph.Current`) | `r.Graph` or `Graph.Current` |
| `memo { let! x = sig; return! innerMemo x }` (or multi-arm `return!`) | *(none)* | `Signal.bind innerMemo sig` (`SplitBind`, `B20`) | `sig.Graph` (no `Activate ()`) |
| `memo { let! x = mem; return! innerMemo x }` (or multi-arm `return!`) | *(none)* | `Memo.bind innerMemo mem` (`SplitBind`, `B20`) | `mem.Graph` (no `Activate ()`) |
| `memo { let! x = r; return! innerMemo x }` (other `IReactive<'T>`, or `comparer`) | `comparer`? | `Memo.CreateOn` / `SplitBind` over `r.Graph` | `r.Graph` |
| `memo { let! prev = memo.previous[Or]; ... }` | `owning`? `comparer`? | `Memo.Create` / `Memo.CreateWithComparer` passing `prev` (Phase 1 tracked through last reactive `!`, Phase 2 `return` closed) | First reactive source's `.Graph`, else `Graph.Current` |
| `memo { let! a = r1; let! b = pick a; return f a b }` / `match!` / `if` / `try` / `use` | `owning`? `comparer`? | `Memo.Create` / `Memo.CreateWithComparer` (`1` dynamic node; Phase-1 `!`s tracked, terminal `return` closed) | Outer `r1.Graph` when outer bind is static, else `Graph.Current` |
| `effect { [node;] [comparer cmp;] let! k = r1 [and! ...]; act k }` | `node`? `comparer`? | `EffectOn<'K>.Create[WithComparer](r1.Graph, readKey, act)` (`B16`-style split effect) | `r1.Graph` (no `Activate ()`) |
| `effect { [node;] let! a = r1; let! b = pick a; act a b }` | `node`? | `Effect.Create(r1.Graph, ...)` with tracked `!` reads and `RunUntracked` around terminal `act` | `r1.Graph` or `Graph.Current` |
| `effect { [node;] body () }` (0 `let!`s) | `node`? | `Effect.Create(Graph.Current, body)` | `Graph.Current` |
| `asyncMemo { [owning;] [policy p;] let! k = r1 [and! ...]; <Phase 2> }` | `owning`? `policy`? | `AsyncMemo.CreateOn(r1.Graph, readKey, computePhase2, mode, pol)` (`B17`) | `r1.Graph` (no `Activate ()`) |
| `asyncMemo { [owning;] [policy p;] <dynamic Phase 1 / 0 reactive let!s>; <Phase 2> }` | `owning`? `policy`? | `AsyncMemo.Create(g, compute, mode, pol)` closing tracking before Phase 2 | Outer `r1.Graph` or `Graph.Current` |
| `stream { [fold seed fn;] let! k = r1 [and! ...]; [let! ct = ...;] return src }` | `fold`? | `StreamMemo.CreateOn` / `StreamMemo.CreateFoldOn` (`B16`) | `r1.Graph` (no `Activate ()`) |
| `stream { [fold seed fn;] [let! ct = ...;] return src }` (0 or sequential reactive `let!`s) | `fold`? | `StreamMemo.Create` (closing tracking before `src` construction after last reactive `let!`) | Outer `r1.Graph` or `Graph.Current` |
| `editable { [let! prev = editable.previous[Or];] ... }` | — | `Editable<'U>(g, seed, draft = false)` (closing tracking before `return`) | Static source's `.Graph` or `Graph.Current` |
| `draft { [let! prev = draft.previous[Or];] ... }` | — | `Editable<'U>(g, seed, draft = true)` (closing tracking before `return`) | Static source's `.Graph` or `Graph.Current` |
| `boundary { pending fn; [failed fn;] [comparer cmp;] ... }` | `pending` / `failed` / `comparer` | `Boundary<'U>.Create[WithComparer](Graph.Current, body, onPending, onError[, cmp])` | Outer source `.Graph` if static `let!` and ambient owner matches, else `Graph.Current` *(Designer call: `Boundary` always attaches to `CurrentOwner`; when `Graph.Current` is active use `Graph.Current`, otherwise `r1.Graph` via `g.Entered`)* |
| `row { let! v = item; [and! x = sig;] return f v x }` | — | Pure `unit -> 'V` thunk reading `item ()` & `sig.Value` tracked, then closing tracking (`EnterUntrackedPure` on active graph) before `f v x` | Ambient row `Graph` at read time (`0` extra nodes allocated per key!) |

---

### 4.3 Detailed Builder Specifications

#### 4.3.1 `asyncMemo` Two-Phase State Machine
`AsyncMemoBuilder` separates reactive inputs from asynchronous suspension using phantom phase types `Phase1` and `Phase2`:
- **Context tokens**:
  - `let! prev = asyncMemo.previous` binds `prev: Previous<'U>` (`prev.Settled : Task<'U voption>` can then be awaited via standard `Task` `let!`, preserving `FlightPolicy.Queue` ordering per `Api.fs:540-543`).
  - `let! ct = asyncMemo.cancellationToken` binds `ct: CancellationToken`.
  - Both context token binds are polymorphic in `'Phase` (`AsyncStep<'P, 'U> -> AsyncStep<'P, 'U>`), so authors can bind `ct` or `prev` either before or after Phase-1 reactive `let!`s without changing phase typing.
- **Phase-1 Reactive Binds (`Signal`, `Memo`, `IReactive`, `MergedSignals`, `MergedMemos`, `MergedReactive`)**:
  - Accept continuations returning either `AsyncStep<Phase2, 'U>` (no further reactive binds -> static `AsyncMemo.CreateOn`, Theorem `B17`) or `AsyncStep<Phase1, 'U>` (sequential/branching reactive binds -> dynamic Phase 1 + untracked Phase 2) and always produce `AsyncStep<Phase1, 'U>`.
- **Phase-2 Task Binds (`Task<'A>`, `ValueTask<'A>`, `Task`, `ValueTask`)**:
  - Strictly require continuations of type `'A -> AsyncStep<Phase2, 'U>` and produce `AsyncStep<Phase2, 'U>`.
  - **Compile-time invariant**: Writing `let! x = someSignal` *after* `let! data = http.GetAsync(...)` or `do! Task.Yield()` produces `AsyncStep<Phase1, 'U>` where `AsyncStep<Phase2, 'U>` is required — **rejected at compile time (`FS0001` / `FS0193`)**.
  - Inside Phase 2, standard control flow (`let`, `if`/`else`, `match`, `try`/`with`, `try`/`finally`, `use`, `while`, `for`) and `return` / `return! (taskOrValueTask)` lower onto F#'s `task { ... }` / `AsyncTaskMethodBuilder` machinery (compiled cleanly on both `.NET` and `Fickl` `Promise`-backed `Task`).
  - Every Phase-2 execution enters `UntrackedPhase` (`EnterUntrackedPure` for pure mode, `EnterUntrackedOwning` up to first suspension for `owning` mode), wrapping any uncaught synchronous `NotReadyException` into `InvalidOperationException` (`B17`).

#### 4.3.2 `stream` Builder
- Supports 0, 1, `and!`-merged, or sequential reactive `let!`s in Phase 1, optional `let! ct = stream.cancellationToken`, and `return (source: StreamSource<'T>)`.
- Header `fold seed folder` switches output from `StreamMemo<'T>` (`createStream` / `createStreamOn`) to `StreamMemo<'S>` (`createStreamFold` / `createStreamFoldOn`).
- Single static reactive `let!` or `let! ... and! ...` compiles directly to `StreamMemo.CreateOn` / `StreamMemo.CreateFoldOn` (`B16`), so equal settled keys keep the open `StreamSource<'T>` running uninterrupted.

#### 4.3.3 `row` Builder (Per-Key Projection Reader Sugar)
- `row { ... }` returns a plain function `unit -> 'V` (zero `Memo` allocation — `RowsOf` / `MapView` already wrap the returned reader in the projection's own row memo!).
- Supports `let!` / `and!` / `match!` over:
  1. Row item accessors `unit -> 'T` (the exact parameter passed to `createProjectionWith`, `createProjectionWithOptions`, `createIndexProjectionWith`, and `Projection.mapWith`), AND
  2. Every `IReactive<'T>` (`Signal`, `Memo`, `Editable`, `Boundary`, `AsyncMemo`, `AsyncSource`, `StreamMemo`, `Timed`, `Projection.at`/`tryAt`/`keys`, `Lookup.at`/`tryAt`, `KeyedCollection.*`).
  *(Designer call: `MergeSources` for `unit -> 'A` paired with `IReactive<'B>` or `unit -> 'B` lives exclusively on `RowBuilder` via lightweight `[<Struct>] RowInput<'T>`, keeping `unit -> 'T` out of `memo`/`effect`/`asyncMemo` so arbitrary functions never cause overload ambiguity outside `row`.)*
- When the returned `unit -> 'V` runs inside a row memo (`Graph.TryCurrent` active): all Phase-1 `let!`/`and!` reads execute tracked on the row memo, and the terminal `return` expression runs inside `EnterUntrackedPure` so helper functions called when formatting/constructing `'V` cannot subscribe the row to stray signals or create owned nodes.

---

## 5. Companion Static Analyzers (`ranvier-org/Ranvier.View`)

All core rules live in `tools/Ranvier.Analyzers/{Symbols.fs, Rules/Core.fs, Analyzers.fs}` gated on `Symbols.Assemblies.Ranvier` (`"Ranvier.dll"`), share the single memoized `Index.ofFile` traversal (`ConditionalWeakTable<FSharpImplementationFileContents, FileIndex>`), and must execute in `<= 250 ms` (fastest of 3 runs) over both `Core.fs` and `Template.fs` (`Budget.fs`).

### 5.1 Updates to `Symbols.Core` and Existing Rules (`RAN010`, `RAN011`, `RAN012`, `RAN015`)

1. **`Symbols.Core.valueTypes`**:
   - Add `"Ranvier.StreamMemo`1"` and `"Ranvier.Timed`1"` alongside `"Ranvier.Signal`1"`, `"Ranvier.AsyncSource`1"`, `"Ranvier.Memo`1"`, `"Ranvier.AsyncMemo`1"`, `"Ranvier.Boundary`1"`, and `"Ranvier.Editable`1"` so `.Value`, `.TryValue`, and `.Status` on streams and timed nodes are recognized by `isReactiveRead` across every core and view rule.
2. **`Symbols.Core.isCreator` & `RAN010` (`Error`: Owned node created in a pure derivation)**:
   - Register PR `#389` creators (`createMemoOn`, `createMemoOnWithComparer`, `createMemoWithOn`, `createOwningMemoOnWithComparer`, `createEffectNode`, `createAsyncOn`, `createAsyncWithOn`, `createAsyncOnWithPolicy`, `createOwningAsyncOnWithPolicy`, `createStreamOn`, `createStreamFoldOn`, `createEditable`, `createDraft`, `TimedCombinators.*`, `Signal.map`/`map2`/`zip`/`bind`, `Memo.map`/`map2`/`zip`/`bind`, `AsyncMemo.*`, `AsyncSource.*`, `StreamMemo.*`, `Projection.*` view/aggregate creators, `Grouping.aggregate`).
   - Register `.Run` on `MemoBuilder`, `EffectBuilder`, `AsyncMemoBuilder`, `StreamBuilder`, `EditableBuilder`, `DraftBuilder`, and `BoundaryBuilder` as owned-node creators (`RowBuilder.Run` is *not* a node creator; it returns `unit -> 'V`).
   - Register pure contexts for PR `#389` (`createMemoOn*` `read` and non-owning `compute`, `createAsyncOn*` `read` and non-owning `compute`, `createStreamOn*` `read` and `start`, `*.map` / `*.map2` `mapping`, `AsyncMemo.bind*` / `AsyncSource.bind` `binder`) with automatic owning rename fixes (`createMemoOn` -> `createMemoWithOn`, `createMemoOnWithComparer` -> `createOwningMemoOnWithComparer`, `createAsyncOn` -> `createAsyncWithOn`, `createAsyncOnWithPolicy` -> `createOwningAsyncOnWithPolicy`).
   - Register CE pure contexts:
     - `memo { ... }` without `owning` header -> `Pure.Memo` (with automatic `Fix` inserting `owning` header when single-line or multi-line `memo {` layout permits; Phase-1 bound source expressions inside `memo { owning ... }` remain pure!).
     - `effect { ... }` with `let!` -> Phase-1 bound source expressions are `Pure.EffectOn`; Phase-2 body is an `ownerBody`.
     - `asyncMemo { ... }` without `owning` -> `Pure.Async` (with `owning` fix); with `owning` -> Phase-1 bound source expressions remain pure, Phase-2 body before first `Task` await is an `ownerBody`.
     - `editable { ... }` and `draft { ... }` -> `Pure.Seed`.
     - `row { ... }` -> `Pure.Reader`.
3. **`RAN011` (`Warning`: Async value created and read in the owner that replaces it)**:
   - Extend `ownerBody` to `createMemoWithOn` (`compute`), `createOwningMemoOnWithComparer` (`compute`), `createEffectNode`, `createAsyncWithOn` (`compute`), `createOwningAsyncOnWithPolicy` (`compute`), `Signal.bind`, `Memo.bind`, and the owning bodies of `memo { owning ... }`, `effect { ... }`, `asyncMemo { owning ... }`, and `boundary { ... }`.
   - Extend `isAsyncCreator` to `AsyncMemoBuilder.Run`, `StreamBuilder.Run`, `AsyncMemo.bind*`, `AsyncSource.bind`, and `StreamMemo.bind*` (and detect reads via both `.Value`/`.TryValue` and reactive `Bind`/`BindReturn` inside the same owner body!).
4. **`RAN012` (`Warning`: Reactive read after the first await of an async memo)**:
   - Extend `asyncComputeIndex` / CE walker to cover `createAsyncOn*` and `asyncMemo { ... }` Phase-2 `Task`/`ValueTask` continuations so direct lexical `sig.Value` reads after an await inside `asyncMemo` report `RAN012` (complementing the compiler's rejection of reactive `let!` after await).
5. **`RAN015` (`Hint`: `CancelPrevious` with a body that ignores its token)**:
   - Extend to `createAsyncOnWithPolicy` / `createOwningAsyncOnWithPolicy` AND `asyncMemo { policy FlightPolicy.CancelPrevious ... }` when `asyncMemo.cancellationToken` is either never bound in the `asyncMemo` block or bound to `_` / an unused identifier.

---

### 5.2 New Static Analyzer Rules (`RAN016`, `RAN017`, `RAN018`)

#### `RAN016` (`Severity.Warning`): Reactive read inside an untracked closed phase
- **Why**: In two-phase `*On` primitives (`createMemoOn*` `compute`, `createEffectOn*` `act`, `createAsyncOn*` `compute`, `createStreamOn*` `start`), static combinators (`*.map`, `*.map2`, `*.bind`), and reactive CEs (`BindReturn`'s `return` body across `memo`, `editable`, `draft`, `row`, plus Phase 2 of `effect` with `let!`, `asyncMemo`, and `stream`), dependency tracking is **closed** once Phase 1 finishes. A direct `x.Value` / `x.TryValue` / `x.Status` / `p.Get` / `p.TryGet` / `p.Keys` read inside that closed phase records **zero** dependency edges (so mutations to `x` never wake the computation) and fails immediately with `InvalidOperationException` if `x` is `Pending`.
- **Detection**: Using `input.Index.Calls`, find any `isReactiveRead` call whose lexical `Path` enters a closed Phase-2 argument (e.g., `BindReturn` arg 1 on a reactive CE builder, `EffectBuilder.Bind` terminal unit continuation, `AsyncMemoBuilder` / `StreamBuilder` Phase-2 continuation, `*.map` arg 0, `*.map2` arg 0, `createMemoOn` arg 1, `createEffectOn` arg 1, etc.) at `lambdaCount = 1` without crossing into a nested tracked body (`trackedBody`: `createEffect`, `createMemo`, `memo.Run`, `row.Run`, etc.) or `untrack`. (Avoid duplicate reporting with `RAN012` when a read in `asyncMemo` is also after a `Task` await: `RAN012` takes precedence after a `Task` await, `RAN016` covers synchronous closed Phase-2 reads.)
- **Message**:
  > ``"`{read}` runs inside an untracked compute phase, so changes to `{source}` never wake this computation and a pending read fails it. Bind `{source}` with `let!`/`and!` in the tracked phase, or use `{source}.Peek` / `untrack` for an intentional snapshot."``
- **Docs & Tests**: `docs/content/analyzers/ran016.md` + `tools/Ranvier.Analyzers.Tests/Ran016.fs`.

#### `RAN017` (`Severity.Hint`, with `Fix`): Independent sequential `let!` in a reactive CE
- **Why**: Inside `memo`, `effect`, `asyncMemo`, `stream`, `editable`, `draft`, or `row`, writing two consecutive reactive binds sequentially:
  ```fsharp
  memo {
      let! a = price
      let! b = quantity
      return a * b
  }
  ```
  when `quantity` does not reference `a` forces dynamic sequential `Bind` (`Memo.Create` with dynamic `SourceList`, no `Signal.map2` / `Memo.map2` static dispatch, no `*On` settled-key cutoff in `effect`/`asyncMemo`/`stream`/`memo { owning }`, and right-source unready blindness while `a` is `Pending`). Replacing the second `let!` with `and!` promotes the block at compile time to `*.map2` or `*On`!
- **Detection**: On a reactive CE builder `Bind` call `outer` (`outer.Arguments = [ s1; Lambda(paramA, innerCall) ]`) where `outer.Callee` binds a reactive source (not a context token or `Task`) and `innerCall` (stripping compiler-generated tuple/pattern `Let` bindings of `paramA` only, with **zero** user `let` bindings, `Sequential`, `Combine`, `IfThenElse`, or `DecisionTree` in between) is immediately another reactive `Bind` or `BindReturn` `[ s2; ... ]` on the same builder:
  - Check `Index.uses` of `paramA` (and any destructured pattern locals bound directly from `paramA`) inside `s2` (note: if `s2` is itself a `MergeSources` tree from `let! b = s2; and! c = s3`, check the entire `s2` expression).
  - If `uses` is empty AND `s2`'s source range starts with keyword `let!` on a subsequent line: emit `RAN017` at the `let!` token range of `s2` with a `Fix` replacing `"let!"` with `"and!"`.
- **Message**:
  > ``"`{s2Text}` does not depend on `{paramName}`; sequential `let!` forces dynamic tracking and skips static `map2`/`*On` key cutoff. Write `and!` to bind both sources statically."``
- **Docs & Tests**: `docs/content/analyzers/ran017.md` + `tools/Ranvier.Analyzers.Tests/Ran017.fs` (verifying `applyFix` compiles and triggers `0` findings).

#### `RAN018` (`Severity.Hint`): Zero-bind reactive CE performing direct reactive reads
- **Why**: Writing `memo { return count.Value + 1 }`, `editable { return count.Value }`, `draft { return count.Value }`, or `row { return (item ()) + 1 }` with zero `let!` / `match!` bindings forfeits `.Graph` resolution from `count` (requiring `Graph.Current`), bypasses `Signal.map` / `Memo.map` chain fusion (`B18`), and leaves dependency tracking open across helper calls inside `return`. (`boundary` and 0-bind `effect` without direct lexical `.Value` reads are intentionally excluded, since `boundary` catches dynamic subtree suspensions/errors and `effect { helper () }` is idiomatic for dynamic helper effects; for 0-bind `effect`, `RAN018` triggers only if the top-level body reads a single `x.Value` directly with no `let!`, or *(Designer call: restrict `RAN018` to `memo`, `editable`, `draft`, `asyncMemo`, `stream`, and `row`, leaving 0-bind `effect { ... }` and `boundary { ... }` completely unflagged because Imperative `effect { if cond.Value then ... }` with dynamic short-circuit reads is a valid Solid/Ranvier pattern)*).
- **Detection**: A `.Run` call on `memo`, `editable`, `draft`, `asyncMemo`, `stream`, or `row` whose body contains **zero** reactive `Bind` / `BindReturn` calls on that builder, and contains at least one lexical `isReactiveRead` (or `item ()` call of the enclosing projection factory parameter in `row`) at `lambdaCount = 0` outside `untrack` and nested tracked computations.
- **Message**:
  > ``"`{read}` is read directly inside `{builderName} {{ ... }}` with no `let!` bindings, which skips static source fusion, `.Graph` resolution, and closed-phase tracking. Bind `{source}` with `let!`."``
- **Docs & Tests**: `docs/content/analyzers/ran018.md` + `tools/Ranvier.Analyzers.Tests/Ran018.fs`.

---

## 6. Formal Verification, Testing, and Performance Gate

### 6.1 Rocq 9.1 Proofs (`proofs/theories/Pure/Combinators.v`)
Most CE forms compile directly to primitives already certified by `B16`–`B20` (`Pure/Combinators.v`). We add Section **`B21`** in `Pure/Combinators.v` (registered in `Audit.v` and `proofs/CORRESPONDENCE.md`, `0` axioms):
1. **`ce_dispatch_and_nested_eval_eq` (`B21a`)**: Models `evalDepth` (`0` = top-level `Run` static node dispatch; `S d` = nested `Bind` inline tracked evaluation with terminal closed-phase `return`; `suspendForNestedRun` saving `S d`, running nested `Run` at `0`, and restoring `S d` across both normal return and exception unwind), proving:
   - `evalDepth` is restored to its entry value on all paths,
   - top-level `BindReturn` / `MergeSources` / `MemoTail` select the exact `B16`–`B20` machine, and
   - nested `BindReturn` inside `Bind` / `match!` evaluates with `0` child node allocations while closing tracking before the terminal mapping function.
2. **`merge_sources_nary_precedence_eq` (`B21b`)**: Proves by induction on $n \ge 2$ that right-nested `MergedReactive` (`MergeSources(r_1, MergeSources(r_2, ...))`) over `comb_reading` preserves exact left-to-right `CPending` / `CFailed` short-circuit status & `origin` precedence (generalizing `B19`) and product settled-key equality `eq_1 k_1 k_1' && ... && eq_n k_n k_n'`.

### 6.2 Test & Benchmark Acceptance Criteria
1. **`Ranvier` (`ranvier-org/Ranvier`)**:
   - Expecto unit & property tests (`tests/Ranvier.Tests/Builders.fs`, compiled into both `.NET` untraced/traced and `Fable` JS suites):
     - Every row of the §4.2 dispatch table (verifying actual fused node kinds / `Runs` / cutoff counts / `Graph`-without-`Activate ()` construction / cross-graph `InvalidOperationException`).
     - Header custom operations on `memo`, `effect`, `asyncMemo`, `stream`, `boundary` (valid combinations, order independence where valid, disposal & scope ownership).
     - Context tokens (`memo.previous`, `memo.previousOr`, `editable.previous*`, `draft.previous*`, `asyncMemo.previous`, `asyncMemo.cancellationToken`, `stream.cancellationToken`, plus inertness outside CEs).
     - `match!` + `Projection.at` / `Projection.tryAt` / `Projection.keys` / `Lookup.at` / `Lookup.tryAt` / `KeyedCollection.*` + `row { ... }` with `createProjectionWith` and `Projection.mapWith`.
     - Reentrancy & exception safety: throwing inside dynamic `memo` / `effect` / `asyncMemo` leaves `evalDepth = 0`; nested `memo { ... }` inside `memo { owning ... }` builds a real child `Memo` owned by the outer Phase-2 scope.
   - NativeAOT `--reflectionfree` smoke (`tests/Ranvier.AotSmoke/Program.fs`): exercise `memo`, `effect`, `asyncMemo`, `stream`, `editable`, `draft`, `boundary`, `row`, and pointwise descriptors under both `RanvierTrace=false` and `RanvierTrace=true`.
   - Performance & Counter Verification:
     - 4-stage `memo { let! x = ...; return x + 1 }` chain fuses identically to `Signal.map |> Memo.map |> Memo.map |> Memo.map` (`0 B/op` steady-state on `.NET`, matching `chain-map` counter class).
     - `dotnet fsi --exec build.fsx -- analyze --group library --build` passes cleanly (both untraced and `RanvierTrace=true`).
2. **`Ranvier.View` (`ranvier-org/Ranvier.View`)**:
   - `Ran010.fs`, `Ran011.fs`, `Ran012.fs`, `Ran015.fs` extended with `*On`, combinator, `StreamMemo`/`Timed`, and CE cases (positive triggers, negative clean cases, exact ranges, and `applyFix` round-trips).
   - New `Ran016.fs`, `Ran017.fs`, `Ran018.fs` suites + `docs/content/analyzers/{ran016,ran017,ran018}.md` passing `DocsExamples.fs` and `Budget.fs` (`<= 250 ms` per rule over `Core.fs` and `Template.fs`) using `-p:UseLocalRanvier=true -p:RanvierRoot=...` against the `Ranvier` worktree.
   - Full `Ranvier.View` repo self-analysis (`build.fsx -- analyze`) passes with `0` regressions (if `RAN016` surfaces any genuine untracked read in existing `Ranvier` / `Ranvier.View` sources, fix or wrap intentional snapshots in `.Peek` / `untrack`).

---

## 7. Multi-Agent Execution & Cross-Repo Coordination Strategy

### 7.1 Workhorse Division (Post-Approval Implementation Phase)
Once this spec and the implementation plan (`writing-plans`) are approved, work will be parallelized across isolated git worktrees using:
- **External CLI workhorses (`claude.exe` and `codex.exe`)**: Always invoked with `ANTHROPIC_API_KEY` and `OPENAI_API_KEY` unset in the child process environment so user subscriptions are used, assigned self-contained implementation slices (e.g., `Builders.fs` core + collection descriptors, Expecto + Fable + AotSmoke suites, Rocq `B21` proofs, and `Ranvier.Analyzers` rules + docs).
- **Antigravity `self` / `research` subagents**: Independent two-stage spec-compliance and code-quality reviewers on every task slice, plus cross-target (`fsc` Debug/Release, `Fickl`, `Ranvier.Counters`, `Budget.fs`) verification.
- **Coordinator (this session)**: Owns worktree integration, cross-repo compatibility gates, PR creation, CI/Perf monitoring, and squash-merging (`AGENTS.md`).

### 7.2 Cross-Repo Release & PR Sequencing (`RELEASE-RUNBOOK.md`)
- **PR 1 (`ranvier-org/Ranvier`)**: Ships `Builders.fs`, pointwise collection descriptors, Rocq `B21` proofs, tests, NativeAOT smoke, benchmarks, and docs.
  - *Important Packaging Gate*: `Ranvier.Analyzers` itself (`Ranvier.Analyzers.fsproj`) matches symbols by string name and has **no** compile-time binary dependency on `Ranvier.dll`, whereas `Ranvier.Analyzers.Tests` compiles fixture strings against the `Ranvier.dll` in `Ranvier.View`'s build output (`Options.fs:43-54`). Locally, `-p:UseLocalRanvier=true` tests everything end-to-end immediately. On GitHub Actions, `Ranvier.View` restores `Ranvier` from `nuget.org` (`$(RanvierVersion)` in `Ranvier.View/Directory.Build.props`).
  - Therefore, before opening PR 2 on `ranvier-org/Ranvier.View`, we will confirm whether PR 1 on `Ranvier` should keep `<Version>0.2.2</Version>` (no NuGet publish yet) or bump `<Version>0.2.3</Version>` so `Ranvier` `0.2.3` publishes to `nuget.org` on merge and `Ranvier.View` PR 2 can bump `RanvierVersion` to `0.2.3` and run all new CE analyzer tests in `Ranvier.View` CI (`RELEASE-RUNBOOK.md:214-222`).

---

## 8. What Is Left to the Builder

- Exact internal helper module/type naming under `Ranvier` / `Ranvier.Internal` (provided all symbols referenced by `inline` public CE methods are `public` + `[<EditorBrowsable(EditorBrowsableState.Never)>]` and pass `Release` + cross-assembly + `Fickl` builds with zero `FS1113`/`FS1118` warnings).
- Whether `ProjectionAt<'K, 'V>` / `LookupAt<'K, 'V>` use `#if FABLE_COMPILER` `[<Sealed>]` classes vs `[<Struct>]` on Fable, guided by emitted JS size/allocations in `Fickl 0.13.0`.
- Exact internal `Task` / `ValueTask` state-machine delegation in `AsyncMemoBuilder` Phase 2 (`task { ... }` wrapper vs `AsyncTaskMethodBuilder` helpers), provided both `.NET` (`net10.0` + `netstandard2.1`) and `Fickl 0.13.0` (`Promise`-backed `Task`) pass all flight-policy, cancellation, and exception-propagation tests.
- Exact formatting/indentation handling of `RAN010`'s optional `owning` auto-fix on multi-line `memo { ... }` / `asyncMemo { ... }` blocks (emitting the `Fix` where the `{` token position is unambiguous and omitting the auto-fix while keeping the diagnostic message where macro/pipe syntax makes insertion ambiguous).
