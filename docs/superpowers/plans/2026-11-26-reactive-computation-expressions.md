# Reactive Computation Expressions & Companion Analyzers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship zero-reflection, fusion-preserving F# computation expressions (`Ranvier.CE`: `memo`, `effect`, `asyncMemo`, `stream`, `editable`, `draft`, `boundary`, `row`), pointwise collection descriptors (`Projection`, `Lookup`, `KeyedCollection`), Rocq `9.1` formal proofs (`B21`), a `Ranvier` `0.2.3` NuGet release, and companion `Ranvier.Analyzers` rules (`RAN010`–`RAN012` & `RAN015` extensions + new `RAN016`, `RAN017`, `RAN018`) in `Ranvier.View`.

**Architecture:** All eight builders live in `namespace Ranvier.CE` (`src/Ranvier/Builders.fs`) over a shared `[<EditorBrowsable(EditorBrowsableState.Never)>] IReactive<'T>` interface and phantom-phased `[<Struct>]` step carriers (`ColdPhase` intrinsic `.Run` vs `BoundPhase` `[<AutoOpen>]` extension `.Run`). Static reactive shapes compile directly to `Signal.map`/`Memo.map`/`FusedSourceMap1` (`B18`), `*.map2` (`B19`), `*On` (`B16`–`B17`), or `SplitBind` (`B20`) without quadratic overload tables or construction-time eager execution, while `Ranvier.Analyzers` inspects the shared `Index.FileIndex` in `O(|Calls| + |Lets|)` to catch untracked Phase-2 reads (`RAN016`), promotable sequential `let!` binds (`RAN017`), and 0-bind direct reactive reads (`RAN018`).

**Tech Stack:** F# (`net10.0`, `netstandard2.1`, `--reflectionfree` NativeAOT), Fickl `0.13.0` (`Fable 5.27.0` JS), Rocq `9.1` (`proofs/`), Expecto `11.1.1`, `FSharp.Analyzers.SDK` `0.41.0`, EasyBuild.ShipIt `3.3.0`.

**Spec:** `C:\wk\ranvier-release\_wt\ranvier-ce\docs\superpowers\specs\2026-11-26-reactive-computation-expressions-design.md`

## Global Constraints

- **Explicit namespace gate**: All CE builders (`memo`, `effect`, `asyncMemo`, `stream`, `editable`, `draft`, `boundary`, `row`), `withComparer`, and `BoundPhase` `.Run` type extensions live in `namespace Ranvier.CE` — never `[<AutoOpen>]` under `Ranvier`.
- **Cross-assembly `inline` visibility**: Every helper type, module, or function referenced by an `inline` public member in `Ranvier.CE` or `Projection`/`Lookup`/`KeyedCollection` must be `public` with `[<EditorBrowsable(EditorBrowsableState.Never)>]` so `Debug`, `Release`, `Ranvier.AotSmoke`, and `Fickl` compile with `0` `FS1113` / `FS1118` warnings or errors.
- **NativeAOT & dual-target parity**: `Ranvier.fsproj` compiles with `<RanvierAotClean>true</RanvierAotClean>` (`--reflectionfree`, `IsAotCompatible=true`) across `net10.0`, `netstandard2.1`, `RanvierTrace=false/true`, and `Fickl 0.13.0` (`#if FABLE_COMPILER` guards wherever `.NET` `[<ThreadStatic>]` or BCL APIs differ).
- **Zero quadratic overload bloat & analyzer budget**: Keep `MergeSources` to 3 overloads (`Signal*Signal`, `Memo*Memo`, `IReactive*IReactive`, plus `RowBuilder`'s `unit -> 'T` adapters); `dotnet fsi --exec build.fsx -- analyze --group library --build` must stay within its `4 GiB` heap limit, and every rule in `Ranvier.Analyzers.Tests/Budget.fs` must run in `<= 250 ms` (fastest of 3 runs) over `Core.fs` and `Template.fs`.
- **Figure labeling & CLI rules (`AGENTS.md`)**: Label every number in commits/PRs/reports (`measured here`, `from documentation`, or `general knowledge`); pass `--exec` to `dotnet fsi`; write multi-line shell logic to `.ps1` files rather than heredocs; unset `ANTHROPIC_API_KEY` and `OPENAI_API_KEY` in child processes when invoking `claude.exe` or `codex.exe`; merging, publishing, and remote branch deletion are coordinator-only steps.

## Review Focus

1. **Mid-chain observation or disposal of a fused `memo { ... }` stage**: Reading `.Value` or calling `.Dispose()` on an intermediate `memo { let! x = ...; return ... }` inside a 3-stage fused CE chain must lazily materialize/de-fuse identically to `Memo.map` without losing cached values or `ErrorOrigin` (tested in Task 3).
2. **Throwing custom `IEqualityComparer<'K>` passed to `withComparer`**: When `cmp.Equals` throws on a recompute of `let! k = src |> withComparer cmp` (including over an `and!`-merged source), `memo`/`asyncMemo`/`stream` must surface a Phase-1 `Status.Error` (`EffectOn` records the error and skips `act`) while preserving the active flight, stream, or Phase-2 scope without corrupting `hasKey`/`lastKey` (tested in Task 2 and Task 4).
3. **`NotReadyException` or exception unwind inside a dynamic `match!` / sequential `let!` leaving `evalDepth` dirty**: When a nested `let!` inside `memo { let! a = s1; let! b = pendingAsync; return a + b }` suspends with `NotReadyException` or throws, `InternalCeState.evalDepth` must unwind to `0` so the very next top-level `memo { let! x = s1; return x + 1 }` constructed on that thread still dispatches to static `Signal.map` rather than `OfInline` (tested in Task 3).
4. **Tuple/record destructuring in `let! (x, y) = pairSig` under `RAN017`**: When `fsc` lowers `let! (x, y) = pairSig` into a synthetic parameter `_arg1` plus compiler-generated `Let` destructuring bindings, `RAN017` must *not* fire when the next `let!` references `x` or `y`, and *must* still fire with a valid `let!` -> `and!` `Fix` when the next `let!` references neither (tested in Task 7).
5. **`use` (`Using`) deterministic disposal inside CE bodies**: `use d = disposable` inside `memo { owning ... }`, `effect { ... }`, and `asyncMemo` (across an `await` and on exception unwind) must dispose `d` deterministically when the step exits, distinct from `onCleanup` scope lifetime (tested in Task 3 and Task 4).

---

### Task 1: Rocq 9.1 Proofs for CE Reentrant Dispatch & $N$-ary `MergeSources` (`B21`)

**Files:**
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\proofs\theories\Pure\Combinators.v` (append Section `B21`)
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\proofs\theories\Audit.v` (append `Print Assumptions` checks)
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\proofs\CORRESPONDENCE.md` (append `B21` correspondence row)

**Interfaces:**
- Consumes: `comb_reading`, `result`, `comb_bind`, `eval_map2`, `memo_on_step`, `split_bind_step` from `proofs/theories/Pure/Combinators.v`.
- Produces: Zero-axiom theorems `ce_dispatch_and_nested_eval_eq` (`B21a`) and `merge_sources_nary_precedence_eq` (`B21b`) in `Ranvier.Pure.Combinators`.

- [ ] **Step 1: Add `B21` definitions, theorem statements, and `Audit.v` checks (red check before proofs)**

In `proofs/theories/Pure/Combinators.v`, add Section `B21`:
1. Model `ce_eval_state` (`ce_depth : nat`, `ce_tracking : bool`, `ce_nodes_created : nat`), `with_eval_depth` (incrementing `ce_depth`, running a step returning `result A`, and restoring `ce_depth` on both `RVal` and `RThrow`), `with_suspended_run` (saving `ce_depth`, setting `0` to construct a child node, and restoring `ce_depth` on both `RVal` and `RThrow`), and `ce_bind_return_eval` (branching on `Nat.eqb (ce_depth st) 0`: constructing `1` static node at depth `0`, or reading Phase 1 with `ce_tracking = true` then evaluating `f` with `ce_tracking = false` and `0` nodes created at depth `S d`).
2. State `Theorem ce_dispatch_and_nested_eval_eq`: proves depth restoration across arbitrary `RVal`/`RThrow` outcomes for both `with_eval_depth` and `with_suspended_run`, static dispatch at depth `0`, and `0`-node untracked-Phase-2 inline evaluation at depth `S d`.
3. Define heterogeneous right-nested $n$-source reading list/tree `merge_n_readings` and product key equality `merge_n_eq` over `comb_reading`, and state `Theorem merge_sources_nary_precedence_eq`: proves right-nested `MergeSources` agrees with sequential left-to-right `comb_bind` across all `CReady` / `CPending` / `CFailed` combinations (preserving exact leftmost unready `origin`) and cuts off iff every component satisfies `eq_i k_i k_i' = true`.
4. Append `Print Assumptions ce_dispatch_and_nested_eval_eq.` and `Print Assumptions merge_sources_nary_precedence_eq.` to `proofs/theories/Audit.v`.

- [ ] **Step 2: Prove `ce_dispatch_and_nested_eval_eq` and `merge_sources_nary_precedence_eq` and update `proofs/CORRESPONDENCE.md`**

Complete both proofs in `proofs/theories/Pure/Combinators.v` with zero `Admitted` / axioms, and document `B21` in `proofs/CORRESPONDENCE.md` mapping to `InternalCeState`, `MemoStep`, `MergedReactive`, and `Ranvier.CE`.

- [ ] **Step 3: Run the Rocq proof & audit verification script**

Run (via Git Bash / WSL `bash` as `build.fsx` / CI runs `proofs/check.sh`):
`bash C:/wk/ranvier-release/_wt/ranvier-ce/proofs/check.sh`
Expected: `OK` with `Closed under the global context` for both new theorems in `Audit.v`.

- [ ] **Step 4: Commit**

Commit `proofs/theories/Pure/Combinators.v`, `proofs/theories/Audit.v`, and `proofs/CORRESPONDENCE.md` with message `feat(proofs): verify B21 CE step dispatch reentrancy and n-ary MergeSources`.

---

### Task 2: `IReactive<'T>`, Custom Key-Comparer `*On`/`SplitBind` Hooks, `withComparer`, and Pointwise Collection Descriptors

**Files:**
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\src\Ranvier\Types.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\src\Ranvier\Core.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\src\Ranvier\Timed.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\src\Ranvier\Streams.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\src\Ranvier\Api.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\src\Ranvier\Combinators.fs`
- Create: `C:\wk\ranvier-release\_wt\ranvier-ce\src\Ranvier\Builders.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\src\Ranvier\Ranvier.fsproj`
- Create: `C:\wk\ranvier-release\_wt\ranvier-ce\tests\Ranvier.Tests\Builders.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\tests\Ranvier.Tests\TestFiles.props`

**Interfaces:**
- Consumes: Existing `Signal<'T>`, `Memo<'T>`, `AsyncMemo<'T>`, `AsyncSource<'T>`, `Boundary<'T>`, `Timed<'T>`, `StreamMemo<'T>`, `Editable<'T>`, `Projection<'K, 'V>`, `Lookup<'K, 'V>`, `KeyedCollection<'K, 'V>`.
- Produces:
  - `Ranvier.IReactive<'T>` (`[<EditorBrowsable(EditorBrowsableState.Never)>]`): `abstract Graph: Graph`, `abstract ReadTracked: unit -> 'T` implemented on all 8 reactive value types.
  - Internal members: `Editable<'T>.Graph`, `Editable<'T>.ValueMemo`, `Boundary<'T>.Graph`, `BoundaryAdapter<'T> : CombinatorSource<'T>`, `Timed<'T>.Graph`, `TimedAdapter<'T> : CombinatorSource<'T>`.
  - Optional `?keyComparer: IEqualityComparer<'K>` on `Memo.CreateOn`, `AsyncMemo.CreateOn`, `StreamMemo.CreateOn`, `StreamMemo.CreateFoldOn`, and `CombinatorMemos.createReactiveBind<'K, 'U>(graph: Graph, readKey: unit -> 'K, binder: 'K -> Memo<'U>, keyComparer: IEqualityComparer<'K> voption, outputComparer: IEqualityComparer<'U> voption) : Memo<'U>`.
  - Public descriptors & functions:
    - `Projection.at : 'K -> Projection<'K, 'V> -> ProjectionAt<'K, 'V>` (`IReactive<'V>`)
    - `Projection.tryAt : 'K -> Projection<'K, 'V> -> ProjectionTryAt<'K, 'V>` (`IReactive<'V option>`)
    - `Projection.keys : Projection<'K, 'V> -> ProjectionKeys<'K, 'V>` (`IReactive<'K[]>`)
    - `Lookup.at : 'K -> Lookup<'K, 'V> -> LookupAt<'K, 'V>` (`IReactive<'V>`)
    - `Lookup.tryAt : 'K -> Lookup<'K, 'V> -> LookupTryAt<'K, 'V>` (`IReactive<'V option>`)
    - `KeyedCollection.at`, `KeyedCollection.tryAt`, `KeyedCollection.keys`
    - `Ranvier.CE.withComparer : IEqualityComparer<'K> -> IReactive<'K> -> ComparedReactive<'K>` (`IReactive<'K>`)
    - `Ranvier.Internal` carriers: `ColdPhase`, `BoundPhase`, `InternalCeState`, `MergedSignals<'A,'B>`, `MergedMemos<'A,'B>`, `MergedReactive<'A,'B>`.

- [ ] **Step 1: Write failing tests in `tests/Ranvier.Tests/Builders.fs` and register in `TestFiles.props`**

Create `tests/Ranvier.Tests/Builders.fs` (`module Ranvier.Tests.Builders`, registered at the bottom of `tests/Ranvier.Tests/TestFiles.props`) with `[<Tests>]` covering:
- `IReactive<'T>` `.Graph` identity and `.ReadTracked()` across all 8 reactive node types (`Signal`, `Memo`, `AsyncMemo`, `AsyncSource`, `StreamMemo`, `Boundary`, `Editable`, `Timed`).
- `Projection.at`, `Projection.tryAt`, `Projection.keys`, `Lookup.at`, `Lookup.tryAt` (verifying `Lookup.tryAt` returns `None` untracked without creating a cell when cold, and `Some v` tracked when live), and `KeyedCollection.at`/`tryAt`/`keys` — including `ArgumentNullException` on null collection arguments and `.Graph` resolution without `graph.Activate ()`.
- `Ranvier.CE.withComparer` null checks (`ArgumentNullException` on null `comparer` or `source`) and custom key-comparer behavior on `Memo.CreateOn`, `AsyncMemo.CreateOn`, `StreamMemo.CreateOn`, and `CombinatorMemos.createReactiveBind` — **including Review Focus #2**: a `throwingCmp` whose `.Equals` raises on the second settle surfaces `Status.Error` from Phase 1 while preserving the cached Phase-2 value/flight/stream and recovering cleanly when a valid key settles later.

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet run --project C:\wk\ranvier-release\_wt\ranvier-ce\tests\Ranvier.Tests\Ranvier.Tests.fsproj -- --filter-test-list "Builders"`
Expected: FAIL to compile (`Projection.at`, `Lookup`, `KeyedCollection`, `Ranvier.CE.withComparer` undefined).

- [ ] **Step 3: Implement `IReactive<'T>`, `*On`/`SplitBind` `keyComparer` hooks, pointwise descriptors, and `Builders.fs` foundation**

1. Add `IReactive<'T>` in `Types.fs` and implement it across `Core.fs`, `Timed.fs`, `Streams.fs`, and `Api.fs` (adding `BoundaryAdapter<'T>` and `TimedAdapter<'T>` alongside `StreamMemoAdapter<'T>`).
2. Add optional `?keyComparer: IEqualityComparer<'K>` (encoded via `ComparerSentinel.encode` when `Some`, validating non-null, defaulting to `graph.EncodedComparer<'K>()`) on `MemoOnState`/`Memo.CreateOn`, `AsyncOnState`/`AsyncMemo.CreateOn`, `StreamKeyStarter`/`StreamMemo.CreateOn`/`StreamMemo.CreateFoldOn`, and add `CombinatorMemos.createReactiveBind` (generalizing `SplitBindState` to `readKey: unit -> 'K` + encoded `keyComparer` + encoded `outputComparer`).
3. Add `ProjectionAt`, `ProjectionTryAt`, `ProjectionKeys`, `LookupAt`, `LookupTryAt`, `Projection.at`/`tryAt`/`keys`, `Lookup.at`/`tryAt`, and `KeyedCollection.at`/`tryAt`/`keys` in `Combinators.fs`.
4. Create `src/Ranvier/Builders.fs` (registered after `Combinators.fs` in `Ranvier.fsproj`) with `namespace Ranvier.Internal` (`ColdPhase`, `BoundPhase`, `InternalCeState`, `ComparedReactive<'K>`, `MergedSignals<'A,'B>`, `MergedMemos<'A,'B>`, `MergedReactive<'A,'B>`) and `namespace Ranvier.CE` (`[<AutoOpen>] module Builders` exposing `withComparer`).

- [ ] **Step 4: Run `.NET` (Debug + Release) and `rfs` checks to verify Task 2 passes**

Run:
- `rfs check --workspace C:\wk\ranvier-release\_wt\ranvier-ce src/Ranvier/Builders.fs --fail-on-error`
- `dotnet run --project C:\wk\ranvier-release\_wt\ranvier-ce\tests\Ranvier.Tests\Ranvier.Tests.fsproj -c Release -- --filter-test-list "Builders"`
Expected: `0` errors/warnings, all Task 2 `Builders` tests PASS.

- [ ] **Step 5: Commit**

Commit all Task 2 files with message `feat(core): add IReactive, collection read descriptors, and key-comparer hooks`.

---

### Task 3: Synchronous Builders in `Ranvier.CE` (`memo`, `effect`, `editable`, `draft`, `boundary`, `row`)

**Files:**
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\src\Ranvier\Builders.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\tests\Ranvier.Tests\Builders.fs`

**Interfaces:**
- Consumes: Task 2 `IReactive<'T>`, `ComparedReactive<'K>`, `MergedSignals`, `MergedMemos`, `MergedReactive`, `InternalCeState`, `BoundaryAdapter`, `TimedAdapter`, `CombinatorMemos.*`.
- Produces (in `namespace Ranvier.CE`):
  - Builders & singletons: `MemoBuilder` (`memo`), `EffectBuilder` (`effect`), `EditableBuilder` (`editable`), `DraftBuilder` (`draft`), `BoundaryBuilder` (`boundary`), `RowBuilder` (`row`), plus `[<AutoOpen>] module BuilderExtensions` holding `BoundPhase` `.Run` overloads.
  - Context tokens (`inline` members on singletons): `memo.previous` (`MemoPreviousToken`), `memo.previousOr seed` (`MemoPreviousOrToken<'U>`), `editable.previous`, `editable.previousOr seed`, `draft.previous`, `draft.previousOr seed`.
  - Header `[<CustomOperation>]`s:
    - `MemoBuilder`: `owning`, `comparer`, `option`
    - `EffectBuilder`: `node`
    - `BoundaryBuilder`: `pending`, `failed`, `comparer`

- [ ] **Step 1: Write failing Expecto tests in `tests/Ranvier.Tests/Builders.fs` for all 6 synchronous builders**

Add test lists covering:
1. **`memo` static dispatch & fusion**:
   - 1-source `Signal`, `Memo`, `Editable`, `AsyncMemo`, `AsyncSource`, `StreamMemo`, `Boundary`, `Timed`, `Projection.at`/`tryAt`/`keys`, `Lookup.at`/`tryAt` without `graph.Activate ()`.
   - 4-stage `memo { let! x = ...; return ... }` chain fusion (`ComputeKind >= 8uy` in untraced unjournaled build; `0` intermediate node runs) **plus Review Focus #1**: reading `.Value` or calling `.Dispose()` on stage 2 mid-chain materializes/de-fuses without losing values or `ErrorOrigin`.
   - 2-source `Signal * Signal` (`Signal.map2`), `Memo * Memo` (`Memo.map2`), and 3-way/4-way heterogeneous `and!` (`Signal` + `Memo` + `Projection.tryAt` + `Lookup.at`) + cross-graph `InvalidOperationException` + `withComparer` key cutoff.
   - Header ops: `owning` (child node/cleanup disposal on unequal key, preservation on equal key), `comparer` (output cutoff), `owning` + `comparer` (in either order), `option` (`Some` wrapper reference preservation).
   - `return!` (`MemoTail`): `let! x = sig; return! inner x`, `let! a = s1; and! b = s2; return! inner a b`, and `match! mode with | A -> return! m1 | B -> return! m2` (verifying `0` outer binder re-runs when only `m1`/`m2` ticks, `B20`).
   - Context tokens: `memo.previous` (`ValueNone` then `ValueSome`), `memo.previousOr seed`, and inertness of `let _ = memo.previous` outside a CE.
   - Dynamic branching & reentrancy: `match! rows |> Projection.tryAt id with | None -> return "none" | Some r -> let! o = Lookup.at r.Owner owners; return ...`, nested `memo { ... }` constructed inside `memo { owning ... }`, **Review Focus #3** (`NotReadyException` or `exn` thrown on second `let!` unwinds `evalDepth` to `0` so a subsequent top-level `memo { let! x = sig; return x + 1 }` still fuses as `Signal.map`), **Review Focus #5** (`use d = ...` inside `memo { owning ... }` disposes `d` as soon as the run returns, before scope discharge), and `ColdPhase` 0-bind `memo { sideEffect (); return 42 }` executing `0` times at construction.
2. **`effect`**:
   - Default `unit` vs `node` (`INode`), 1-source & `and!` `EffectOn` cutoff (skipping `act` & keeping `onCleanup` alive on equal keys or pending Phase 1), `withComparer`, `match!` mixing `Signal<int>`, `Memo<string>`, and `()` arms, `ColdPhase` 0-bind `effect { ... }`, and **Review Focus #5** (`use` deterministic disposal).
3. **`editable` & `draft`**:
   - Static 1-source/`and!` without `Activate ()`, `previous`/`previousOr`, local `.Value <-` edit dropped on unequal upstream seed for `editable` vs retained until `.Reset()` for `draft`, and `ColdPhase` 0-bind laziness.
4. **`boundary`**:
   - `pending` (`createSuspense*`), `failed` (`createErrorBoundary*`), `pending` + `failed` in both orders (`createBoundary*`), `comparer`, and closed Phase-2 `return` after `let!`.
5. **`row`**:
   - `let! v = item` (`unit -> 'T`) + `and! rate = taxSignal` inside `createProjectionWith`, `createIndexProjectionWith`, and `Projection.mapWith`, verifying `0` extra nodes created and closed tracking inside `return` (reading an unbound signal via helper inside `return` does not subscribe the row).

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet run --project C:\wk\ranvier-release\_wt\ranvier-ce\tests\Ranvier.Tests\Ranvier.Tests.fsproj -- --filter-test-list "Builders"`
Expected: FAIL to compile (`memo`, `effect`, `editable`, `draft`, `boundary`, `row` undefined in `Ranvier.CE`).

- [ ] **Step 3: Implement `memo`, `effect`, `editable`, `draft`, `boundary`, and `row` in `src/Ranvier/Builders.fs`**

Implement all 6 builders, phantom header structs (`MemoHeader<'Mode, 'Cmp>`, `EffectHeader<'Out>`, `BoundaryHeader<'Pend, 'Fail, 'Cmp>`), context token structs, `RowInput<'T>` `MergeSources` overloads on `RowBuilder`, `TryWith` / `TryFinally` / `Using` / `While` / `For` (sequence loop) / `Combine` / `Zero`, intrinsic `ColdPhase` `.Run` members, and `[<AutoOpen>] module BuilderExtensions` `BoundPhase` `.Run` overloads per Spec §§3.2, 4.1, 4.2, 4.3.3. Ensure `EnterUntrackedPure` / `RunUntracked` closes tracking before terminal `return` / `act` continuations on both static and dynamic paths.

- [ ] **Step 4: Run `.NET` (untraced Release + traced Debug) tests and `rfs` checks**

Run:
- `rfs check --workspace C:\wk\ranvier-release\_wt\ranvier-ce src/Ranvier/Builders.fs tests/Ranvier.Tests/Builders.fs --fail-on-error`
- `dotnet run --project C:\wk\ranvier-release\_wt\ranvier-ce\tests\Ranvier.Tests\Ranvier.Tests.fsproj -c Release -- --filter-test-list "Builders"`
- `dotnet run --project C:\wk\ranvier-release\_wt\ranvier-ce\tests\Ranvier.Tests\Ranvier.Tests.fsproj -c Debug -- --filter-test-list "Builders"`
Expected: PASS in both untraced `Release` and traced `Debug`.

- [ ] **Step 5: Commit**

Commit `src/Ranvier/Builders.fs` and `tests/Ranvier.Tests/Builders.fs` with message `feat(ce): add memo, effect, editable, draft, boundary, and row builders`.

---

### Task 4: Asynchronous & Streaming Builders in `Ranvier.CE` (`asyncMemo`, `stream`)

**Files:**
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\src\Ranvier\Builders.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\tests\Ranvier.Tests\Builders.fs`

**Interfaces:**
- Consumes: Task 2 & Task 3 infrastructure in `src/Ranvier/Builders.fs`, `AsyncMemo.Create`/`CreateOn`, `StreamMemo.Create`/`CreateOn`/`CreateFoldOn`.
- Produces (in `namespace Ranvier.CE`):
  - `AsyncMemoBuilder` (`asyncMemo`) with `asyncMemo.previous` (`AsyncPreviousToken` -> `Previous<'U>`), `asyncMemo.cancellationToken` (`AsyncCancellationToken` -> `CancellationToken`), header ops `owning` and `policy (p: FlightPolicy)`, Phase-1 reactive binds (`BoundPhase`), and Phase-2 `Task<'A>` / `Task` (and `#if !FABLE_COMPILER` `ValueTask<'A>` / `ValueTask` if Fable lacks `ValueTask`, or dual-target adapter — check `Platform.fs` / Fable BCL!) awaits (`ColdPhase` continuation, polymorphic `'P` return).
  - `StreamBuilder` (`stream`) with `stream.cancellationToken` (`StreamCancellationToken` -> `CancellationToken`), header op `fold (seed: 'S) (folder: 'S -> 'T -> 'S)`, Phase-1 reactive binds, and `return (source: StreamSource<'T>)`.

- [ ] **Step 1: Write failing Expecto tests in `tests/Ranvier.Tests/Builders.fs` for `asyncMemo` and `stream`**

Add `.NET` + `Fable`-compatible async/stream tests (`testTask` / `Async`) covering:
1. **`asyncMemo`**:
   - Static 1-source and `and!`-merged `AsyncMemo.CreateOn` dispatch without `Activate ()`, verifying all 6 `FlightPolicy` behaviors (`CancelPrevious`, `KeepLatest`, `Queue` with `let! prev = asyncMemo.previous` + `let! last = prev.Settled`, `FinishCurrent`, `Drop`, `DropWhilePending`), equal-key in-flight preservation, transient Phase-1 `Pending`/`Failed` flight preservation, and `withComparer` (**Review Focus #2** throwing key comparer in `asyncMemo`).
   - `asyncMemo.cancellationToken` bound *before* and *after* Phase-1 reactive `let!`, plus `owning` header scope disposal across flights.
   - `ColdPhase` laziness: `asyncMemo { let! x = fireTask (); return x + 1 }` and `asyncMemo { sideEffect (); return 7 }` execute `0` times at construction and start only on first `.Value` / `.TryValue` read.
   - `match!` mixing: Arm 1 `return fallback`, Arm 2 direct `let! x = fireTask (); return x`, Arm 3 nested `let! u = urlSig; let! x = fetch u; return x`.
   - **Review Focus #5**: `use d = ...` inside `asyncMemo` across a suspending `Task` await and across an exception throw disposes `d` deterministically when the flight completes/faults.
2. **`stream`**:
   - Latest-item `stream { let! k = roomSig; let! ct = stream.cancellationToken; return openRoom k ct }` (`StreamMemo.CreateOn`) and folded `stream { fold 0 (+); let! k = roomSig; return openNumbers k }` (`StreamMemo.CreateFoldOn`) without `Activate ()`.
   - Equal settled key & `withComparer` keeping the open `StreamSource` connected without cancelling `ct`; unequal key cancelling previous `ct` and restarting (resetting `seed` on `fold`).
   - `ColdPhase` 0-reactive-bind `stream { let! ct = stream.cancellationToken; return openSource ct }` starting `0` streams at construction time until first read.

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet run --project C:\wk\ranvier-release\_wt\ranvier-ce\tests\Ranvier.Tests\Ranvier.Tests.fsproj -- --filter-test-list "Builders"`
Expected: FAIL to compile (`asyncMemo` and `stream` undefined).

- [ ] **Step 3: Implement `AsyncMemoBuilder` (`asyncMemo`) and `StreamBuilder` (`stream`) in `src/Ranvier/Builders.fs`**

Implement `AsyncHeader<'Mode, 'Pol>`, `AsyncStep<'Phase, 'U>`, `AsyncMemoBuilder` (`Task<'A>` & `Task` on both targets; `ValueTask<'A>` & `ValueTask` on `#if !FABLE_COMPILER` — verify whether `Fickl 0.13.0` supports `ValueTask`, enabling on Fable as well if supported), `StreamHeader<'Fold>`, `StreamStep<'Phase, 'U>`, `StreamBuilder`, intrinsic `ColdPhase` `.Run` members, and `[<AutoOpen>]` `BoundPhase` `.Run` extensions. Ensure Phase-2 execution enters `UntrackedPhase` so synchronous `NotReadyException` in Phase 2 wraps into `InvalidOperationException` (`B17`).

- [ ] **Step 4: Run `.NET` (Release + Debug) AND `Fable` (`node`) test suites**

Run:
- `dotnet run --project C:\wk\ranvier-release\_wt\ranvier-ce\tests\Ranvier.Tests\Ranvier.Tests.fsproj -c Release -- --filter-test-list "Builders"`
- `dotnet run --project C:\wk\ranvier-release\_wt\ranvier-ce\tests\Ranvier.Tests\Ranvier.Tests.fsproj -c Debug -- --filter-test-list "Builders"`
- `Push-Location C:\wk\ranvier-release\_wt\ranvier-ce; dotnet fsi --exec build.fsx -- test-fable; Pop-Location` (also refreshes `docs/.ai/fable-compat.md`)
Expected: All `.NET` (untraced + traced) and `Fable` (`node` untraced + traced) tests PASS!

- [ ] **Step 5: Commit**

Commit `src/Ranvier/Builders.fs`, `tests/Ranvier.Tests/Builders.fs`, and `docs/.ai/fable-compat.md` with message `feat(ce): add two-phase asyncMemo and stream computation expressions`.

---

### Task 5: NativeAOT Smoke, Benchmark/Counter Parity, Guide Docs, `0.2.3` Bump, and PR 1 Publish (`ranvier-org/Ranvier`)

**Files:**
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\tests\Ranvier.AotSmoke\Program.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\benchmarks\Ranvier.Benchmarks\` (add `chain-ce` or verify `memo` CE fusion parity in benchmark/counter suite without altering existing counter baselines unless extending)
- Create: `C:\wk\ranvier-release\_wt\ranvier-ce\docs\content\guide\computation-expressions.md`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\docs\Site.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\src\Ranvier\Ranvier.fsproj` (`<Version>0.2.3</Version>`)
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\src\Ranvier.CSharp\Ranvier.CSharp.fsproj` (`<Version>0.2.3</Version>`)
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\src\Ranvier.Elmish\Ranvier.Elmish.fsproj` (`<Version>0.2.3</Version>`)
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\src\Ranvier.Query\Ranvier.Query.fsproj` (`<Version>0.2.3</Version>`)
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\src\Ranvier.Wire\Ranvier.Wire.fsproj` (`<Version>0.2.3</Version>`)
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\src\Ranvier.Mcp\Ranvier.Mcp.fsproj` (`<Version>0.2.3</Version>`)
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\docs\content\fable\index.md` (`0.2.3`)
- Modify: `C:\wk\ranvier-release\_wt\ranvier-ce\docs\content\guide\installation.md` (`0.2.3`)

**Interfaces:**
- Consumes: Tasks 1–4.
- Produces: Published `Ranvier` `0.2.3` core family (+ `.Traced` twins) on `nuget.org`, ready for `Ranvier.View` PR 2 CI.

- [ ] **Step 1: Add NativeAOT `--reflectionfree` smoke coverage in `tests/Ranvier.AotSmoke/Program.fs`**

Open `Ranvier.CE` in `tests/Ranvier.AotSmoke/Program.fs` and add smoke assertions exercising `memo` (1-source fused chain, `and!`, `owning`, `comparer`, `option`, `memo.previousOr`, `match!` over `Projection.tryAt` + `Lookup.at`, `return!`), `effect` (`node` + `withComparer`), `asyncMemo` (`policy` + `cancellationToken`), `stream` (`fold`), `editable`, `draft`, `boundary` (`pending` + `failed`), and `row` (`createProjectionWith`). Publish & run NativeAOT smoke with both `-p:RanvierTrace=false` and `-p:RanvierTrace=true`.

- [ ] **Step 2: Author `docs/content/guide/computation-expressions.md`, register in `docs/Site.fs`, and bump core family `<Version>` to `0.2.3`**

1. Write `docs/content/guide/computation-expressions.md` documenting `open Ranvier.CE`, all 8 builders, header custom operations, `withComparer`, builder-scoped context tokens, pointwise collection descriptors, and the compile-time dispatch rules, and add `Menu.page "guide/computation-expressions.md"` in `docs/Site.fs`.
2. Bump `<Version>` from `0.2.2` to `0.2.3` across `Ranvier`, `Ranvier.CSharp`, `Ranvier.Elmish`, `Ranvier.Query`, `Ranvier.Wire`, and `Ranvier.Mcp`, plus `docs/content/fable/index.md` and `docs/content/guide/installation.md` (matching commit `1fd09a3`).

- [ ] **Step 3: Run the full `Ranvier` local verification gate**

Run (from `C:\wk\ranvier-release\_wt\ranvier-ce`):
- `dotnet fsi --exec build.fsx -- format`
- `dotnet publish tests/Ranvier.AotSmoke/Ranvier.AotSmoke.fsproj -c Release -p:RanvierTrace=false` and execute binary (`0` warnings, exit `0`)
- `dotnet publish tests/Ranvier.AotSmoke/Ranvier.AotSmoke.fsproj -c Release -p:RanvierTrace=true` and execute binary (`0` warnings, exit `0`)
- `dotnet run --project benchmarks/Ranvier.Counters/Ranvier.Counters.fsproj -c Release -p:RanvierCounters=true -- --reconcile` (`31/31` reconciled)
- `dotnet fsi --exec build.fsx -- analyze --group library --build` (passes within `4 GiB` heap)
- `dotnet run --project docs/docs.fsproj -- check-menus` (`0` unlisted/missing pages)
- `dotnet fsi --exec build.fsx -- test` and `node .github/scripts/packages.cjs bin src` (after `pack` / `packTraced`).

- [ ] **Step 4: Commit**

Commit NativeAOT smoke, guide docs, and `0.2.3` version bump on `feat/computation-expressions`.

- [ ] **Step 5: Coordinator Gate — Push PR 1 (`ranvier-org/Ranvier`), Watch All Checks Green, Squash-Merge, and Verify `0.2.3` on `nuget.org`**

*(Coordinator-only step per `AGENTS.md` & `RELEASE-RUNBOOK.md`)*:
1. Push `feat/computation-expressions`, confirm with `git ls-remote origin feat/computation-expressions`, open tracking issue + PR 1 (`feat(ce): add Ranvier.CE computation expressions, collection descriptors, and 0.2.3 release`).
2. Watch `gh pr checks <pr> -R ranvier-org/Ranvier --watch --fail-fast` in background until `CI result`, `Perf result`, `Proofs site`, `differential`, and `Conventional commits` are all `pass`.
3. Squash-merge PR 1 (`gh pr merge <pr> -R ranvier-org/Ranvier --squash --delete-branch`), watch `publish.yml` on `master` to completion, and confirm `0.2.3` in `https://api.nuget.org/v3-flatcontainer/<id>/index.json` for `ranvier`, `ranvier.traced`, `ranvier.csharp`, `ranvier.csharp.traced`, `ranvier.elmish`, `ranvier.query`, `ranvier.wire`, and `ranvier.mcp`.

---

### Task 6: Extend `Ranvier.Analyzers` `Symbols.Core` & Existing Rules (`RAN010`, `RAN011`, `RAN012`, `RAN015`)

*(Note: Tasks 6 and 7 work in a fresh isolated worktree `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers` branched from `Ranvier.View` `origin/main`. Before PR 1's `0.2.3` finishes publishing, wire temporary untracked `Local.Build.props` with `<UseLocalRanvier>true</UseLocalRanvier>` and `<RanvierRoot>C:/wk/ranvier-release/_wt/ranvier-ce</RanvierRoot>` so Tasks 6–7 can develop and test in parallel with Task 5!)*

**Files:**
- Modify: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\tools\Ranvier.Analyzers\Symbols.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\tools\Ranvier.Analyzers\Rules\Core.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\tools\Ranvier.Analyzers.Tests\Ran010.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\tools\Ranvier.Analyzers.Tests\Ran011.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\tools\Ranvier.Analyzers.Tests\Ran012.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\tools\Ranvier.Analyzers.Tests\Ran015.fs`

**Interfaces:**
- Consumes: `Ranvier` PR `#389` (`*On` & combinator modules) + `Ranvier.CE` builder type & method names from Tasks 2–4 (`Ranvier.CE.MemoBuilder`, `EffectBuilder`, `AsyncMemoBuilder`, `StreamBuilder`, `EditableBuilder`, `DraftBuilder`, `BoundaryBuilder`, `RowBuilder`, `Ranvier.CE.BuilderExtensions`).
- Produces: Extended `Symbols.Core` symbol classification (`valueTypes`, `isCreator`, `pureContext`, `ownerBody`, `isAsyncCreator`, `asyncComputeIndex`, CE builder recognition) and updated `RAN010`, `RAN011`, `RAN012`, `RAN015` rules.

- [ ] **Step 1: Create `_wt/ranvier-view-ce-analyzers` worktree and write failing tests in `Ran010.fs`, `Ran011.fs`, `Ran012.fs`, `Ran015.fs`**

Add Expecto tests covering:
- **`RAN010`**:
  - Owned nodes inside `createMemoOn` (`read` and `compute`, with `createMemoWithOn` auto-fix on `compute`), `createMemoOnWithComparer` (`createOwningMemoOnWithComparer` fix), `createAsyncOn` (`createAsyncWithOn` fix), `createAsyncOnWithPolicy` (`createOwningAsyncOnWithPolicy` fix), `createStreamOn`, `Signal.map`, `Memo.map2`, `AsyncMemo.bind`.
  - Owned nodes inside `memo { let! x = s; createEffect ignore; return x }` (reports `RAN010`, auto-fix inserts `owning` header; `memo { owning; let! x = s; createEffect ignore; return x }` is clean while `memo { owning; let! x = (createEffect ignore; s); return x }` still reports `RAN010` on Phase-1 source!), `memo { let! x = s; return! memo { let! y = s2; return x + y } }` (`MemoTail` `SplitBind` binder is clean without `owning`!), `asyncMemo { ... }` without `owning` vs with `owning`, `effect { let! x = (onCleanup ignore; s); () }` (Phase-1 `RAN010`) vs Phase-2 clean, `editable { ... }`, `draft { ... }`, and `row { ... }` (and `row.Run` itself inside `createProjectionWith` factory is *not* flagged as an owned creator!).
- **`RAN011`**:
  - Async value (`createAsync*`, `createStream*`, `asyncMemo { ... }`, `stream { ... }`) created inside `createMemoWithOn`, `Signal.bind`, `Memo.bind`, `memo { owning ... }`, `memo` `return!` (`MemoTail`), `effect { ... }`, `asyncMemo { owning ... }`, or `boundary { ... }` and read in that same body (via `.Value`/`.TryValue` OR via `let!`!).
- **`RAN012`**:
  - `StreamMemo.Value` and `Timed.Value` read after await; direct `sig.Value` read after `do! Task.Yield ()` / `let! _ = taskVal` inside `createAsyncOn*` and `asyncMemo { ... }`.
- **`RAN015`**:
  - `createAsyncOnWithPolicy FlightPolicy.CancelPrevious` / `createOwningAsyncOnWithPolicy FlightPolicy.CancelPrevious` ignoring token; `asyncMemo { policy FlightPolicy.CancelPrevious; let! x = s; return! work x }` (never binding `asyncMemo.cancellationToken` -> `RAN015` at `policy FlightPolicy.CancelPrevious` range) and `let! _ = asyncMemo.cancellationToken` / unused `let! ct = asyncMemo.cancellationToken` (`RAN015` at token binding range), vs clean when `ct` is used.

- [ ] **Step 2: Run `Ranvier.Analyzers.Tests` to verify the new test cases fail**

Run: `dotnet test C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\tools\Ranvier.Analyzers.Tests\Ranvier.Analyzers.Tests.fsproj -c Release`
Expected: FAIL on the new `*On`, combinator, and `Ranvier.CE` test cases.

- [ ] **Step 3: Implement `Symbols.Core` and `Rules/Core.fs` updates for `RAN010`, `RAN011`, `RAN012`, `RAN015`**

Update `tools/Ranvier.Analyzers/Symbols.fs` (including `"Ranvier.StreamMemo`1"` and `"Ranvier.Timed`1"` in `valueTypes`, PR `#389` APIs, `Ranvier.CE` intrinsic & `BuilderExtensions` `.Run` / `.Bind` / `.BindReturn` / `.For` / custom-op symbols) and `tools/Ranvier.Analyzers/Rules/Core.fs` (`ownedInPure`, `recreated`, `untrackedReads`, `ignoredTokens`) using single-pass `input.Index` lookups.

- [ ] **Step 4: Run `Ranvier.Analyzers.Tests` to verify all `RAN010`–`RAN015` tests and `Budget.fs` pass**

Run: `dotnet test C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\tools\Ranvier.Analyzers.Tests\Ranvier.Analyzers.Tests.fsproj -c Release`
Expected: PASS (`0` failures, `Budget` `<= 250 ms` per rule).

- [ ] **Step 5: Commit**

Commit `Symbols.fs`, `Rules/Core.fs`, and `Ran010.fs`–`Ran015.fs` (leaving temporary `Local.Build.props` untracked) with message `feat(analyzers): extend RAN010-RAN012 and RAN015 to On primitives, combinators, and Ranvier.CE`.

---

### Task 7: Add `RAN016`, `RAN017`, and `RAN018` Rules, Docs, and Upgrade `Ranvier.View` to `Ranvier 0.2.3` (PR 2 + ShipIt Release)

**Files:**
- Modify: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\tools\Ranvier.Analyzers\Symbols.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\tools\Ranvier.Analyzers\Rules\Core.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\tools\Ranvier.Analyzers\Analyzers.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\tools\Ranvier.Analyzers\Ranvier.Analyzers.fsproj`
- Create: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\tools\Ranvier.Analyzers.Tests\Ran016.fs`
- Create: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\tools\Ranvier.Analyzers.Tests\Ran017.fs`
- Create: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\tools\Ranvier.Analyzers.Tests\Ran018.fs`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\tools\Ranvier.Analyzers.Tests\Ranvier.Analyzers.Tests.fsproj`
- Create: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\docs\content\analyzers\ran016.md`
- Create: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\docs\content\analyzers\ran017.md`
- Create: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\docs\content\analyzers\ran018.md`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\docs\content\analyzers\index.md`
- Modify: `C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\Directory.Build.props`

**Interfaces:**
- Consumes: Task 6 `Symbols.Core` & `Rules.Core`, published `Ranvier` `0.2.3` packages (for final CI / non-local restore check).
- Produces:
  - `Rules.Core.ran016` (`RAN016`, `Severity.Warning`, `"Reactive read inside an untracked compute phase"`), `ran016Cli` / `ran016Editor` (`HelpUri = "https://shayanhabibi.github.io/Ranvier/analyzers/ran016/"`).
  - `Rules.Core.ran017` (`RAN017`, `Severity.Hint`, `"Independent sequential let! in a reactive computation expression"` + `let!` -> `and!` `Fix`), `ran017Cli` / `ran017Editor` (`HelpUri = ".../ran017/"`).
  - `Rules.Core.ran018` (`RAN018`, `Severity.Hint`, `"Reactive read in a computation expression with no let! bindings"`), `ran018Cli` / `ran018Editor` (`HelpUri = ".../ran018/"`).

- [ ] **Step 1: Write failing test suites `Ran016.fs`, `Ran017.fs`, `Ran018.fs` and docs pages `ran016.md`, `ran017.md`, `ran018.md`**

1. **`Ran016.fs` (`Warning`)**:
   - Triggers on direct `other.Value` / `other.TryValue` / `rows.Get k` / `rows.Keys` inside: `memo { let! x = s; return x + other.Value }`, `effect { let! x = s; ignore other.Value }`, `row { let! v = item; return v + other.Value }`, `s |> Signal.map (fun x -> x + other.Value)`, `Memo.map2`, `createMemoOn`, `createEffectOn`, `createStreamOn`, and synchronous Phase 2 of `createAsyncOn` / `asyncMemo` before any `Task` await (verifying no duplicate `RAN016` when `RAN012` already fires after a `Task` await!).
   - Clean when: bound with `and!`, read via `.Peek`, wrapped in `untrack (fun () -> other.Value)`, or inside a nested tracked computation's own body (`memo { owning; let! x = s; createEffect (fun () -> ignore other.Value); return x }`).
2. **`Ran017.fs` (`Hint` + `Fix`)**:
   - Triggers on `let! a = s1` immediately followed by `let! b = s2` across `memo`, `effect`, `asyncMemo`, `stream`, `editable`, `draft`, and `row` when `s2` does not use `a`; verifies `Options.applyFix` turns `let! b = s2` into `and! b = s2` and re-analyzes with `0` findings.
   - **Review Focus #4**: Clean when `let! (x, y) = pairSig` is followed by `let! z = pick x` or `let! z = pick y`; triggers with valid `let!` -> `and!` `Fix` when `let! (x, y) = pairSig` is followed by `let! z = independentSig`.
   - Clean when an intervening `let`, side-effect statement (`Combine`), `if`, or `match` separates the two binds, or when either bind is a context token (`memo.previous`, `asyncMemo.cancellationToken`, `Task`).
3. **`Ran018.fs` (`Hint`)**:
   - Triggers on 0-`let!` `memo { return s.Value + 1 }`, `editable { return s.Value }`, `draft { return s.Value }`, `asyncMemo { return s.Value }`, `stream { return makeStream s.Value }`, and `row { return item () + 1 }` (both `s.Value` and projection factory `item ()`).
   - Clean on 0-`let!` `effect { if cond.Value then act () }`, `boundary { pending (fun _ -> 0); return asyncVal.Value }`, `memo { return 42 }`, `memo { return untrack (fun () -> s.Value) }`, and `memo { return s.Peek }`.
4. Author `docs/content/analyzers/{ran016,ran017,ran018}.md` (each with `### Triggers` and `### Fixed` blocks exercised by `DocsExamples.fs`) and add all three to the table in `docs/content/analyzers/index.md`.

- [ ] **Step 2: Run `Ranvier.Analyzers.Tests` to verify failure**

Run: `dotnet test C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\tools\Ranvier.Analyzers.Tests\Ranvier.Analyzers.Tests.fsproj -c Release`
Expected: FAIL (`ran016`, `ran017`, `ran018` undefined).

- [ ] **Step 3: Implement `ran016`, `ran017`, and `ran018` in `Rules/Core.fs` and register in `Analyzers.fs` & `Ranvier.Analyzers.fsproj`**

Implement all three rules over `input.Index` (`O(|Calls| + |Lets|)`) with exact messages from Spec §5.2, register CLI + Editor attributes in `Analyzers.fs` (`rules` list + `ran016Cli`/`Editor`, `ran017Cli`/`Editor`, `ran018Cli`/`Editor`), and update `<Description>` in `Ranvier.Analyzers.fsproj` (do **not** hand-edit `<Version>` in `Ranvier.View` — EasyBuild.ShipIt owns it per `RELEASE-RUNBOOK.md:74`).

- [ ] **Step 4: Run `Ranvier.Analyzers.Tests` (including `DocsExamples` + `Budget`) AND self-analyze both `Ranvier` and `Ranvier.View`**

Run:
- `dotnet test C:\wk\ranvier-release\_wt\ranvier-view-ce-analyzers\tools\Ranvier.Analyzers.Tests\Ranvier.Analyzers.Tests.fsproj -c Release` (`Ran001`–`Ran025`, `DocsExamples`, `Budget` `<= 250 ms` per rule)
- Run `dotnet fsi --exec build.fsx -- analyze --group library --build` in `_wt/ranvier-ce` pointing `RanvierViewRoot` at `_wt/ranvier-view-ce-analyzers`
- Run `dotnet fsi --exec build.fsx -- analyze` in `_wt/ranvier-view-ce-analyzers` (if `RAN016` uncovers any genuine untracked read in existing `Ranvier.View` closed callbacks, fix or mark intentional `.Peek`/`untrack`).

- [ ] **Step 5: Upgrade `Ranvier.View/Directory.Build.props` to published `Ranvier 0.2.3`, remove `Local.Build.props`, and run full `Ranvier.View` suite**

Once Task 5 Step 5 confirms `0.2.3` is indexed on `nuget.org`:
1. Delete temporary `Local.Build.props` in `_wt/ranvier-view-ce-analyzers`.
2. Update `Directory.Build.props`: `RanvierVersion`, `RanvierElmishVersion`, `RanvierWireVersion`, `RanvierQueryVersion` `0.2.0` -> `0.2.3`, and `RanvierDomVersion` `0.1.0` -> `0.1.1`.
3. Run `dotnet build Ranvier.View.slnx -c Release`, `dotnet test tools/Ranvier.Analyzers.Tests/Ranvier.Analyzers.Tests.fsproj -c Release`, and `Ranvier.View`'s `build.fsx` CI/test/docs checks against the restored `nuget.org` `0.2.3` packages!
4. Commit with message `feat(analyzers): add RAN016-RAN018 CE rules and upgrade to Ranvier 0.2.3`.

- [ ] **Step 6: Coordinator Gate — Push PR 2 (`ranvier-org/Ranvier.View`), Merge Green, and Drive EasyBuild.ShipIt Release**

*(Coordinator-only step per `AGENTS.md` & `RELEASE-RUNBOOK.md`)*:
1. Check open `release/main` PR state on `ranvier-org/Ranvier.View` first (`RELEASE-RUNBOOK.md:146`).
2. Push branch, confirm via `git ls-remote`, open PR 2 (`feat(analyzers): add RAN016-RAN018 computation expression rules and upgrade to Ranvier 0.2.3`), watch `gh pr checks --watch --fail-fast` go green, and squash-merge.
3. Wait for `shipit.yml` to open/update `release/main` (`chore: release ...`), close & reopen `release/main` if `GITHUB_TOKEN` holds workflow runs (`RELEASE-RUNBOOK.md:67-70`), watch `release/main` checks go green, squash-merge keeping its exact `chore: release ...` title, wait for `publish.yml` (`publish`, `publish-anime`, `publish-analyzers`, `verify-published`), confirm new package versions on `nuget.org`, and clean up `_wt/ranvier-ce` and `_wt/ranvier-view-ce-analyzers` worktrees and local branches!
