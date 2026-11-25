# Closed-Window `*On` Primitives, Reactive Combinators, and Unobserved Map-Chain Fusion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship closed-window `*On` primitives (`Memo`, `AsyncMemo`, `StreamMemo`), per-type static-edge combinator modules (`Signal`, `Memo`, `AsyncMemo`, `AsyncSource`, `StreamMemo` with `map`, `map2`, `zip`, `bind`) plus C# `Reactive.*On` and LINQ extensions, unobserved `Memo.map`-chain fusion beating `R3.chain`'s `318` instructions (`measured here`: CI `Perf` run `45519602409`), and Rocq 9.1 proofs `B16`–`B20`.

**Architecture:** Extend `Memo<'T>`, `AsyncMemo<'T>`, and `StreamMemo<'S>` in `Core.fs` and `Streams.fs` with two-phase `(read, phase2)` engines keyed strictly on settled `'T` transitions (holding Phase 2 state/scope/flight/stream across transient Phase 1 `Pending`/`Failed` overlays, matching `createEffectOn`), plus static-edge `Map`/`Map2`/`Bind` descriptors on `Memo<'T>` that bypass dynamic `SourcesField` re-tracking. Layer single-consumer, same-`Owner`, per-stage-cutoff `MapFusion` (with lazy de-fusion on intermediate inspection, fan-out, or disposal) in untraced/unjournaled builds, expose F# APIs in `Api.fs` and C# APIs in `Ranvier.CSharp`, and verify `B16`–`B20` in `proofs/theories/Pure/Combinators.v`.

**Tech Stack:** F# (`net10.0`, `netstandard2.1`, NativeAOT `--reflectionfree`), Fickl/Fable 5 (`node` & `node --jitless`), C# 13 / xUnit v3, Expecto / `Fable.Mocha`, Rocq 9.1 (`stdpp`, `Audit.v`, `Nacara.Plugin.Rocq`).

**Spec:** `docs/superpowers/specs/2026-11-25-closed-window-and-combinators-design.md`

## Global Constraints

- **Dual-target & NativeAOT parity:** Every `src/Ranvier` change compiles cleanly for `net10.0`, `netstandard2.1`, NativeAOT (`RanvierAotClean=true`, `--reflectionfree`), and Fickl/Fable (`#if FABLE_COMPILER`), in both untraced and `-p:RanvierTrace=true` builds, and never boxes multi-argument F# function fields (`box (a -> b -> c)` allocates `curry2` under Fickl).
- **Clef-compatible representation:** Prefer mutable records, struct records, `val mutable` fields, and module functions over new OOP hierarchy / object-expression allocations on hot paths.
- **Closed Phase 2 `NotReadyException` rule:** Every new closed-window Phase 2 callback (`createMemoOn*` `compute`, `createAsyncOn*` synchronous `compute` launch, `createStreamOn*` `start`, `*.map` / `*.map2` `mapping`, `*.bind` `binder`) wraps an uncaught synchronous `NotReadyException` into `InvalidOperationException(..., inner = pending)` (`Status.Error`, `ErrorOrigin = self / stage memo`), leaving existing `createEffectOn` unchanged.
- **Fable test registration:** Every new test module added to `tests/Ranvier.Tests/TestFiles.props` MUST also be added to `fable/Ranvier.Tests.Fable/Main.fs` in matching compile order.
- **Formatting, F# checking, & figure labels:** Format staged F# files with Fantomas and pass `pwsh -NoProfile -File scripts/check-staged.ps1` and `rfs check --fail-on-error` before each commit; label every reported benchmark/count figure as `measured here`, `from documentation`, or `general knowledge`.

## Review Focus

1. **Re-entrant source mutation or self-disposal inside an owning Phase 2 cleanup (`createMemoWithOn`, `createAsyncWithOn`, `Memo.bind`):** Discharging the previous `ScopeField` on an unequal `'T` can mutate a `Signal` read by Phase 1 or dispose the enclosing `Owner`; if disposed, Phase 2 must not run, and in `createAsyncWithOn` a dirtied Phase 1 source must re-evaluate `read ()` before launching a stale flight (pinned in Tasks 2, 3, and 4).
2. **Post-warmup de-fusion on a fused `Memo.map` chain (`m3 = Memo.map h m1`, `m1.Value`, or `m1.Dispose()` after `m2.Value` has already run):** Lazy de-fusion must materialize `m1` from its cached `stageValue_1` without re-invoking `f1` (preserving `ReferenceEquals` on allocated records), wire `s -> m1 -> m2`, and on `m1.Dispose()` freeze `m1` so subsequent `s` writes no longer move `m2` (pinned in Task 4).
3. **Phase 2 throwing on `K1` followed by `read ()` re-evaluating to an equal `K1` (or `K1 -> Pending -> K1`):** Because `lastKey <- K1; hasKey <- true` commits before Phase 2 runs, `createMemoOn` must retain `Status.Error` (`ErrorOrigin = self`) without re-invoking `compute K1` until `read ()` settles on an unequal `K2` (pinned in Task 2).
4. **Background Phase 2 flight settle or stream item arrival while Phase 1 `read ()` is in a transient `Pending`/`Failed` overlay:** `createAsyncOn` and `createStreamFoldOn` must keep external `Status` at Phase 1's `Pending`/`Error` while quietly recording the background flight outcome or folded stream items for `lastKey`, exposing them immediately with zero restart once `read ()` settles back to `lastKey` (pinned in Task 3).
5. **Same-source / shared-diamond `map2` & `zip` (`Signal.zip s s`, `Memo.map2 (+) left right` over one root signal):** Static two-edge linking must handle identical `source1`/`source2` references without duplicate/corrupt observer removal on `Dispose`, and evaluate `mapping` once per batch without glitching (pinned in Task 4).

---

### Task 1: Rocq 9.1 Formal Verification (`B16`–`B20`)

**Files:**
- Create: `proofs/theories/Pure/Combinators.v`
- Modify: `proofs/_CoqProject`
- Modify: `proofs/theories/Audit.v`
- Modify: `proofs/CORRESPONDENCE.md`

**Interfaces:**
- Consumes: `From stdpp Require Import prelude.`, `From Ranvier.Base Require Import Prelude.`
- Produces: Closed Rocq 9.1 theorems `memo_on_cutoff_eq` (`B16`), `async_on_flight_preservation_eq` (`B17`), `fused_map_chain_eq_unfused` (`B18`), `map2_zip_status_precedence_eq` (`B19`), and `split_bind_inner_tick_eq` (`B20`), all verified `Closed under the global context` in `proofs/theories/Audit.v`.

- [ ] **Step 1: Register `theories/Pure/Combinators.v` in `proofs/_CoqProject` and `Print Assumptions` hooks in `proofs/theories/Audit.v` before creating the theory**

Add `theories/Pure/Combinators.v` right after `theories/Pure/FastPaths.v` in `proofs/_CoqProject`, import `Combinators` in `proofs/theories/Audit.v`, and append:
```coq
Print Assumptions memo_on_cutoff_eq.
Print Assumptions async_on_flight_preservation_eq.
Print Assumptions fused_map_chain_eq_unfused.
Print Assumptions map2_zip_status_precedence_eq.
Print Assumptions split_bind_inner_tick_eq.
```

- [ ] **Step 2: Run `proofs/build.sh` to verify it fails on the missing module**

Run: `wsl.exe -d Ubuntu -- bash /mnt/c/wk/ranvier-release/_wt/ranvier-on-combinators/proofs/build.sh`
Expected: FAIL (`Cannot find a physical path bound to logical path ... Combinators`).

- [ ] **Step 3: Author `proofs/theories/Pure/Combinators.v` and update `proofs/CORRESPONDENCE.md`**

Model `comb_reading V := CReady (v : V) | CPending (src : nat) | CFailed (ex : nat) (origin : nat)` and prove all five theorems constructively (no axioms/admits), placing a `(** * Section *)` heading and `(**md **Bold guarantee title.** ... *)` comment directly above every `Theorem` so `proofs/docs/guarantees.py comments` and `outline` succeed:
1. `memo_on_cutoff_eq`: Phase 1 `CPending`/`CFailed` surfaces Phase 1 reading while preserving `(has_key, last_key, phase2_out, scope_gen)`; Phase 1 `CReady k` with `has_key = true && eq_K last_key k = true` restores `phase2_out` with `0` Phase 2 invocations and unchanged `scope_gen`; unequal `k` increments `scope_gen` (discharging prior scope) and invokes `compute k` once.
2. `async_on_flight_preservation_eq`: Across all 6 `FlightPolicy` constructors (`CancelPrevious`, `KeepLatest`, `Queue`, `FinishCurrent`, `Drop`, `DropWhilePending`), `CReady k` with `eq_K last_key k = true` clears any Phase 1 overlay and `owed_trailing` while preserving `flight_gen`, `queued`, and Phase 2 flight slot; Phase 1 `CPending`/`CFailed` uniformly sets the Phase 1 unready overlay across all 6 policies without mutating `has_key`, `last_key`, `flight_gen`, `queued`, `fifo`, `phase2_slot`, or `owed_trailing`; unequal `CReady k'` while `queued <> 0` under `FinishCurrent`/`Drop`/`DropWhilePending` sets `queued = 2` without clobbering `last_key` (with `owed` faulted drops resetting `has_key := false` so `TrailingRun` launches a fresh flight even on `K1 -> K2 -> K1`, and `owed` `FinishCurrent` `RVal` completions storing `CReady u` under `owed_trailing := true` until `CReady` settles); and background flight completion during an active Phase 1 `CPending`/`CFailed` overlay across all 6 policies updates the Phase 2 slot and surfaces immediately once Phase 1 recovers to `last_key`.
3. `fused_map_chain_eq_unfused`: Over a list of stages `(node_id, eq_i, step_i : St -> V_in -> St * result V_out)` with arbitrary boolean `eq_i` (no Leibniz hypothesis) and explicit state `St` (capturing allocation counters / heap identity tokens), a fused chain with per-stage cached cutoffs produces the exact same `(St * comb_reading V_out)` and `origin` as sequential unfused stage evaluation across `CPending`, upstream `CFailed`, stage throw at index `i`, intermediate cutoff at stage `i`, and full `CReady` propagation.
4. `map2_zip_status_precedence_eq`: Static two-input `eval_map2` matches left-to-right `let! x = r1 in let! y = r2 in f x y` across all `3 × 3` `comb_reading` pairs, preserving `origin`.
5. `split_bind_inner_tick_eq`: Outer `comb_reading K` + `binder : K -> nat * InnerId` + `inner : InnerId -> comb_reading U` invokes `binder` and discharges the previous outer scope iff outer settles to a new unequal `K`, never on inner `U` transitions or outer `CReady k -> CPending/CFailed -> CReady k`.

Add a `## Closed-window *On primitives and value combinators` table to `proofs/CORRESPONDENCE.md` mapping `memo_on_step`, `async_on_step`, `eval_fused_chain`, `eval_map2`, and `split_bind_step` to their F# counterparts.

- [ ] **Step 4: Run `proofs/check.sh` and `proofs/site.sh` to verify 0 axioms and clean site generation**

Run:
```powershell
wsl.exe -d Ubuntu -- bash /mnt/c/wk/ranvier-release/_wt/ranvier-on-combinators/proofs/check.sh
wsl.exe -d Ubuntu -- bash /mnt/c/wk/ranvier-release/_wt/ranvier-on-combinators/proofs/site.sh
```
Expected: PASS (`0` errors, all `Print Assumptions` closed under global context, `site/output/index.html` written).

- [ ] **Step 5: Commit**

```powershell
Set-Location "C:\wk\ranvier-release\_wt\ranvier-on-combinators"
git add proofs/_CoqProject proofs/theories/Pure/Combinators.v proofs/theories/Audit.v proofs/CORRESPONDENCE.md
pwsh -NoProfile -File "scripts/check-staged.ps1"
git commit -m "proofs: verify closed-window On, combinators, and map-chain fusion (B16-B20)"
```

---

### Task 2: Synchronous Closed-Window Memos (`createMemoOn*` / `createMemoWithOn`)

**Files:**
- Modify: `src/Ranvier/Core.fs`
- Modify: `src/Ranvier/Api.fs`
- Create: `tests/Ranvier.Tests/MemoSplit.fs`
- Modify: `tests/Ranvier.Tests/NullChecks.fs`
- Modify: `tests/Ranvier.Tests/TestFiles.props`
- Modify: `fable/Ranvier.Tests.Fable/Main.fs`

**Interfaces:**
- Consumes: `Graph`, `Memo<'U>`, `MemoNode`, `ScopeMode`, `ComparerSentinel` in `src/Ranvier/Core.fs`.
- Produces:
  - Internal `Memo.CreateOn<'T, 'U>(graph: Graph, read: unit -> 'T, compute: 'T -> 'U, mode: ScopeMode, comparer: IEqualityComparer<'U> voption) : Memo<'U>` in `src/Ranvier/Core.fs`.
  - Public `[<AutoOpen>] module Api` functions in `src/Ranvier/Api.fs`:
    - `createMemoOn (read: unit -> 'T) (compute: 'T -> 'U) : Memo<'U>`
    - `createMemoOnWithComparer (comparer: IEqualityComparer<'U>) (read: unit -> 'T) (compute: 'T -> 'U) : Memo<'U>`
    - `createMemoWithOn (read: unit -> 'T) (compute: 'T -> 'U) : Memo<'U>`
    - `createOwningMemoOnWithComparer (comparer: IEqualityComparer<'U>) (read: unit -> 'T) (compute: 'T -> 'U) : Memo<'U>`
  - Internal helper `UntrackedPhase.wrapPending (message: string) (ex: exn) : exn` in `src/Ranvier/Core.fs` wrapping `NotReadyException` into `InvalidOperationException(message, ex)` (with `Platform.setInner` under `#if FABLE_COMPILER`), reused by Tasks 3 and 4.

- [ ] **Step 1: Write the failing tests in `tests/Ranvier.Tests/MemoSplit.fs` and `tests/Ranvier.Tests/NullChecks.fs`**

Register `MemoSplit.fs` right after `EffectSplit.fs` in `tests/Ranvier.Tests/TestFiles.props` and `fable/Ranvier.Tests.Fable/Main.fs`. Include Expecto tests asserting:
```fsharp
// 1. Input cutoff & closed Phase 2 window:
// items = [1; 2] -> [3; 4] (equal List.length = 2): compute runs 1 time, signals read inside compute are NOT tracked.
// 2. Output cutoff & createMemoOnWithComparer / createOwningMemoOnWithComparer:
// unequal 'T producing equal 'U under custom comparer does not wake downstream Effect.
// 3. Phase 1 purity even in createMemoWithOn:
// calling onCleanup or createEffect inside `read` fails with InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure).
// 4. Phase 1 transient Pending / Failed preservation:
// user: AsyncMemo<string> settles "ada" -> createMemoWithOn builds inner effect + cleanup -> user re-flights (MemoSplit.Status = Pending) -> settles "ada" -> inner scope NOT discharged, compute runs = 1, MemoSplit.Value = formatted "ada"; same across failed re-flight recovering to "ada".
// 5. Phase 2 untracked NotReadyException wraps into InvalidOperationException (Status.Error, ErrorOrigin = memo).
// 6. Review Focus #1 (re-entrant cleanup write & cleanup self-dispose in createMemoWithOn):
// cleanup mutating `s` schedules follow-up refresh cleanly; cleanup disposing root stops before running next `compute`.
// 7. Review Focus #3 (Phase 2 throw counts as keyed on 'T):
// read = (fun () -> s.Value % 10), compute 2 throws "boom"; writing s <- 12 (and transient Pending -> 12) keeps Status.Error without re-running compute until s <- 3.
// 8. NullChecks.fs: null `read`, `compute`, or `comparer` raises ArgumentNullException across all 4 functions.
```

- [ ] **Step 2: Run `rfs check --fail-on-error` to verify compilation fails on missing `createMemoOn*`**

Run: `rfs check --workspace "C:\wk\ranvier-release\_wt\ranvier-on-combinators" "C:\wk\ranvier-release\_wt\ranvier-on-combinators\tests\Ranvier.Tests\MemoSplit.fs" --fail-on-error`
Expected: FAIL (`The value or constructor 'createMemoOn' is not defined`).

- [ ] **Step 3: Implement `MemoOnState<'T, 'U>`, `Memo.CreateOn`, and `Api.createMemoOn*` in `src/Ranvier/Core.fs` and `src/Ranvier/Api.fs`**

Add a dedicated `computeKind` on `Memo<'U>` backed by a generic sealed state holder `MemoOnState<'T, 'U>` (`read`, `compute`, `keyEqual = graph.EncodedComparer<'T>()`, `mutable hasKey: bool`, `mutable lastKey: 'T`, `mutable phase2Failure: Failure`, `ownerMode: ScopeMode`). Run Phase 1 (`read ()`) hosted on `MemoNode` under `ScopeMode.Pure` (so `SourcesField` tracks `read` only and node creation in `read` violates `Pure`); on `NotReadyException` / `ex`, set `node`'s `Pending` / `Error` status while preserving `hasKey`, `lastKey`, `value`, `phase2Failure`, and `node.ScopeField`. When `read ()` settles equal to `lastKey`, restore `phase2Failure` (if non-null) or `Status.None` + `value` without running `compute`. When `not hasKey` or unequal, set `lastKey <- nextKey; hasKey <- true`, discharge any `node.ScopeField` untracked (`if not node.IsDisposed`), and invoke `compute nextKey` under `EnterUntrackedHosted` / `RunUntrackedHostedWith` with `ownerMode` (`Pure` or lazy `Owning`), wrapping `NotReadyException` via `UntrackedPhase.wrapPending` and applying `equal` output cutoff. Emit matching `Tracer.RunStart` / `Tracer.RunEnd` and `Counters.MemoRecomputed ()` so `--reconcile` holds.

- [ ] **Step 4: Run `.NET` (untraced + `RanvierTrace=true`) and Fable tests**

Run:
```powershell
Set-Location "C:\wk\ranvier-release\_wt\ranvier-on-combinators"
rfs check "tests/Ranvier.Tests/MemoSplit.fs" --fail-on-error
dotnet test tests/Ranvier.Tests/Ranvier.Tests.fsproj -c Release -- --filter-test-list "MemoSplit"
dotnet test tests/Ranvier.Tests/Ranvier.Tests.fsproj -c Release -p:RanvierTrace=true -- --filter-test-list "MemoSplit"
dotnet test tests/Ranvier.Tests/Ranvier.Tests.fsproj -c Release -- --filter-test-list "NullChecks"
dotnet fsi --exec build.fsx -- test-fable --ci
```
Expected: PASS on all commands (`0` errors, all Expecto and Mocha tests green).

- [ ] **Step 5: Format, validate staged files, and commit**

```powershell
Set-Location "C:\wk\ranvier-release\_wt\ranvier-on-combinators"
dotnet fantomas src/Ranvier/Core.fs src/Ranvier/Api.fs tests/Ranvier.Tests/MemoSplit.fs tests/Ranvier.Tests/NullChecks.fs fable/Ranvier.Tests.Fable/Main.fs
git add src/Ranvier/Core.fs src/Ranvier/Api.fs tests/Ranvier.Tests/MemoSplit.fs tests/Ranvier.Tests/NullChecks.fs tests/Ranvier.Tests/TestFiles.props fable/Ranvier.Tests.Fable/Main.fs
pwsh -NoProfile -File "scripts/check-staged.ps1"
git commit -m "feat(core): add closed-window createMemoOn and createMemoWithOn primitives"
```

---

### Task 3: Closed-Window Async & Stream Primitives (`createAsyncOn*` & `createStreamOn*`)

**Files:**
- Modify: `src/Ranvier/Core.fs`
- Modify: `src/Ranvier/Streams.fs`
- Modify: `src/Ranvier/Api.fs`
- Create: `tests/Ranvier.Tests/AsyncSplit.fs`
- Create: `tests/Ranvier.Tests/StreamSplit.fs`
- Modify: `tests/Ranvier.Tests/NullChecks.fs`
- Modify: `tests/Ranvier.Tests/TestFiles.props`
- Modify: `fable/Ranvier.Tests.Fable/Main.fs`

**Interfaces:**
- Consumes: `UntrackedPhase.wrapPending` from Task 2; `AsyncMemo<'U>`, `FlightPolicy`, `Previous<'U>` in `Core.fs`; `StreamMemo<'S>`, `StreamSource<'T>` in `Streams.fs`.
- Produces:
  - Internal `AsyncMemo.CreateOn<'T, 'U>(graph: Graph, read: unit -> 'T, compute: 'T -> Previous<'U> -> CancellationToken -> Task<'U>, mode: ScopeMode, policy: FlightPolicy) : AsyncMemo<'U>` in `src/Ranvier/Core.fs`.
  - Internal `StreamMemo.CreateOn<'K, 'T>(graph: Graph, read: unit -> 'K, start: 'K -> CancellationToken -> StreamSource<'T>) : StreamMemo<'T>` and `StreamMemo.CreateFoldOn<'K, 'S, 'T>(graph: Graph, seed: 'S, fold: 'S -> 'T -> 'S, read: unit -> 'K, start: 'K -> CancellationToken -> StreamSource<'T>) : StreamMemo<'S>` in `src/Ranvier/Streams.fs`.
  - Public `[<AutoOpen>] module Api` functions in `src/Ranvier/Api.fs`:
    - `createAsyncOn`, `createAsyncWithOn`, `createAsyncOnWithPolicy`, `createOwningAsyncOnWithPolicy`
    - `createStreamOn`, `createStreamFoldOn`

- [ ] **Step 1: Write failing tests in `tests/Ranvier.Tests/AsyncSplit.fs`, `tests/Ranvier.Tests/StreamSplit.fs`, and `tests/Ranvier.Tests/NullChecks.fs`**

Register `AsyncSplit.fs` (after `Async.fs`) and `StreamSplit.fs` (after `Streams.fs`) in `tests/Ranvier.Tests/TestFiles.props` and `fable/Ranvier.Tests.Fable/Main.fs`. Cover:
```fsharp
// AsyncSplit.fs:
// 1. Equal 'T leaves in-flight Task uncancelled and settled 'U un-refetched; signals read inside `compute` (even before first await or on completed Task.FromResult) are untracked.
// 2. All 6 FlightPolicy modes:
//    - FinishCurrent / Drop / DropWhilePending while flight for K1 is active: equal K1 or transient Phase 1 Pending/Failed keeps queued = 1 (no spurious TrailingRun); K1 -> K2 -> K1 during K1 flight under FinishCurrent holds Pending (`owedTrailing`) on RVal settle then surfaces K1's settled value without launching K2, whereas if K1's owed flight faults under FinishCurrent or DropWhilePending (`hasKey <- false`), K1 -> K2 -> K1 launches a fresh flight instead of hanging in Pending; K1 -> K2 launches K2 on settle.
//    - Queue: unequal settled keys chain FIFO (out-of-order completions apply in start order); transient Phase 1 Pending/Failed sets Phase 1 overlay without appending a fault slot to Queue's FIFO chain.
// 3. Review Focus #4 (background settle while Phase 1 is Pending/Failed):
//    flight for K1 settles 42 while Phase 1 source is Pending; AsyncMemo.Status stays Pending until Phase 1 settles back to K1, then AsyncMemo.Value = 42 with 1 total flight launched.
// 4. Review Focus #1 (cleanup in createAsyncWithOn mutating `read` source re-evaluates `read` before launching stale key; cleanup disposing scope stops).
// 5. Synchronous NotReadyException inside `compute` fails with InvalidOperationException.
//
// StreamSplit.fs:
// 1. Equal 'K keeps open StreamSource running (no cancellation, no `seed` reset in createStreamFoldOn); signals read inside `start` are untracked.
// 2. Unequal 'K cancels previous token (`StreamEnd.Restarted`), resets to `seed`, and opens new stream.
// 3. Review Focus #4 (stream items arriving while Phase 1 is Pending/Failed):
//    open stream for K1 pushes [10; 20] while Phase 1 is Pending; Status stays Pending, and once Phase 1 settles back to K1, folded Value = 30 with 1 stream start.
// 4. Synchronous NotReadyException inside `start` fails with InvalidOperationException.
// 5. NullChecks.fs: null `read`, `compute`, `fold`, or `start` raises ArgumentNullException.
```

- [ ] **Step 2: Run `rfs check --fail-on-error` to verify compilation fails**

Run: `rfs check --workspace "C:\wk\ranvier-release\_wt\ranvier-on-combinators" "C:\wk\ranvier-release\_wt\ranvier-on-combinators\tests\Ranvier.Tests\AsyncSplit.fs" --fail-on-error`
Expected: FAIL (`'createAsyncOn' is not defined`).

- [ ] **Step 3: Implement two-phase `AsyncMemo` (`Core.fs`), `StreamMemo` (`Streams.fs`), and `Api.fs` entry points**

In `AsyncMemo<'U>` (`Core.fs`), attach an optional internal two-phase driver interface `IAsyncKeyPhase<'U>` (`EvaluatePhase1`, `LaunchUntracked`, `ResetKey`, Phase 1 overlay fields `phase1Status` / `phase1Failure` plus `owedTrailing` so `AsyncMemo<'U>` stays single-type-parameter `'U` without extra generic wrappers on non-`On` memos). Implement the exact 4-case transition in Spec §3.1 (`Equal key` clearing overlay and `owedTrailing`, uniform `Phase 1 unready` overlay across all 6 policies without bumping `queued` or appending to `Queue` FIFO, `Unequal key` across all 6 policies, `hasKey <- false` on `owed` faulted drops under `FinishCurrent`/`DropWhilePending`, `owedTrailing <- true` on `owed` `FinishCurrent` `Completed` outcomes, and background `ApplyResult` storing into Phase 2's slot while `phase1Status <> Status.None`).
In `StreamMemo<'S>` (`Streams.fs`), add a matching internal `IStreamKeyStarter` variant on `starter` holding `keyEqual`, `hasKey`, `lastKey`, and Phase 1 overlay state on `StreamMemo<'S>` so items arriving during a Phase 1 `Pending`/`Error` overlay fold into `lastKey`'s captured state while external `Status` / `Value` / `TryValue` surfaces Phase 1's overlay until `read ()` settles back to `lastKey`. Wire the 6 public functions in `src/Ranvier/Api.fs`.

- [ ] **Step 4: Run `.NET` (untraced + `RanvierTrace=true`) and Fable tests**

Run:
```powershell
Set-Location "C:\wk\ranvier-release\_wt\ranvier-on-combinators"
rfs check "tests/Ranvier.Tests/StreamSplit.fs" --fail-on-error
dotnet test tests/Ranvier.Tests/Ranvier.Tests.fsproj -c Release -- --filter-test-list "AsyncSplit"
dotnet test tests/Ranvier.Tests/Ranvier.Tests.fsproj -c Release -p:RanvierTrace=true -- --filter-test-list "AsyncSplit"
dotnet test tests/Ranvier.Tests/Ranvier.Tests.fsproj -c Release -- --filter-test-list "StreamSplit"
dotnet test tests/Ranvier.Tests/Ranvier.Tests.fsproj -c Release -p:RanvierTrace=true -- --filter-test-list "StreamSplit"
dotnet test tests/Ranvier.Tests/Ranvier.Tests.fsproj -c Release -- --filter-test-list "NullChecks"
dotnet fsi --exec build.fsx -- test-fable --ci
```
Expected: PASS on all commands.

- [ ] **Step 5: Format, validate staged files, and commit**

```powershell
Set-Location "C:\wk\ranvier-release\_wt\ranvier-on-combinators"
dotnet fantomas src/Ranvier/Core.fs src/Ranvier/Streams.fs src/Ranvier/Api.fs tests/Ranvier.Tests/AsyncSplit.fs tests/Ranvier.Tests/StreamSplit.fs tests/Ranvier.Tests/NullChecks.fs fable/Ranvier.Tests.Fable/Main.fs
git add src/Ranvier/Core.fs src/Ranvier/Streams.fs src/Ranvier/Api.fs tests/Ranvier.Tests/AsyncSplit.fs tests/Ranvier.Tests/StreamSplit.fs tests/Ranvier.Tests/NullChecks.fs tests/Ranvier.Tests/TestFiles.props fable/Ranvier.Tests.Fable/Main.fs
pwsh -NoProfile -File "scripts/check-staged.ps1"
git commit -m "feat(core,streams): add closed-window createAsyncOn and createStreamOn families"
```

---

### Task 4: Companion Combinator Modules (`map`, `map2`, `zip`, `bind`) & Unobserved `MapFusion`

**Files:**
- Modify: `src/Ranvier/Core.fs`
- Modify: `src/Ranvier/Api.fs`
- Create: `tests/Ranvier.Tests/ValueCombinators.fs`
- Create: `tests/Ranvier.Tests/MapFusion.fs`
- Modify: `tests/Ranvier.Tests/NullChecks.fs`
- Modify: `tests/Ranvier.Tests/TestFiles.props`
- Modify: `fable/Ranvier.Tests.Fable/Main.fs`

**Interfaces:**
- Consumes: `Memo<'T>`, `UntrackedPhase.wrapPending`, `AsyncMemo.CreateOn`, `StreamMemo.CreateOn` / `CreateFoldOn` from Tasks 2 and 3.
- Produces: `[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]` companion modules in `src/Ranvier/Api.fs` (extending existing `module Signal` at `Api.fs:917` so `Ranvier.SignalModule` stays a single module):
  - `Signal`: `update` (existing), `map`, `map2`, `zip`, `bind`
  - `Memo`: `map`, `map2`, `zip`, `bind`
  - `AsyncMemo`: `map`, `map2`, `zip`, `bind`, `bindWithPolicy`
  - `AsyncSource`: `map`, `map2`, `zip`, `bind`
  - `StreamMemo`: `map`, `map2`, `zip`, `bind`, `bindFold`

- [ ] **Step 1: Write failing tests in `tests/Ranvier.Tests/ValueCombinators.fs`, `tests/Ranvier.Tests/MapFusion.fs`, and `tests/Ranvier.Tests/NullChecks.fs`**

Register `ValueCombinators.fs` and `MapFusion.fs` in `tests/Ranvier.Tests/TestFiles.props` and `fable/Ranvier.Tests.Fable/Main.fs`. Cover:
```fsharp
// ValueCombinators.fs:
// 1. map / map2 / zip across all 5 modules work WITHOUT graph.Activate() (resolving source.Graph), close dependency tracking inside mapping, and enforce cross-graph InvalidOperationException on map2/zip and cross-graph/null return on Signal.bind/Memo.bind.
// 2. Left-to-right Pending/Failed precedence and exact ErrorOrigin preservation across all 3x3 Reading pairs in map2 and zip.
// 3. Split bind (Signal.bind, Memo.bind): inner Memo<'U> ticks do NOT re-run outer binder or discharge outer scope; pre-existing inner Memo allocates 0 child scopes; dynamic inner Memo/cleanup disposes when outer 'T changes; outer transient Pending/Failed in Memo.bind preserves outer scope & inner Memo on recovery to equal 'T; Review Focus #1 cleanup re-entry.
// 4. AsyncMemo.bind / bindWithPolicy / AsyncSource.bind / StreamMemo.bind / bindFold propagate upstream Pending/Failed, preserve flights/streams on equal key, and wrap synchronous Phase 2 NotReadyException into InvalidOperationException.
// 5. Review Focus #5: Signal.zip s s and shared-diamond Memo.map2 (+) left right evaluate glitch-free once per batch and dispose cleanly without ObserverSet errors.
//
// MapFusion.fs:
// 1. 4-stage `s |> Signal.map (+1) |> Memo.map (+1) |> Memo.map (+1) |> Memo.map (+1)` updates, cuts off, and propagates upstream Pending/Failed identically in untraced and RanvierTrace=true builds.
// 2. Per-stage cutoff & ReferenceEquals preservation: `s |> Signal.map (fun n -> n / 10) |> Memo.map (fun t -> { Tier = t })` does NOT invoke record constructor or wake downstream effect when `s` moves 10 -> 11, and ReferenceEquals holds on consecutive reads.
// 3. Per-stage ErrorOrigin attribution: throwing in stage 1, 2, 3, or 4 reports `tail.ErrorOrigin = stage_i` (`obj.ReferenceEquals(box tail.ErrorOrigin, box m_i)`).
// 4. Review Focus #2 (post-warmup de-fusion): warming `m2.Value` first and then reading `m1.Value`, attaching `m3 = Memo.map h m1`, or calling `m1.Dispose()` preserves `ReferenceEquals` on `m1.Value` (0 extra `f1` runs), shares `m1` across `m2`/`m3`, and freezes `m2` at `m1`'s value after `m1.Dispose()`.
// 5. Cross-owner boundary (`createRoot` inner `Memo.map` over outer `m1`) does not fuse lifecycle across distinct owners.
// 6. NullChecks.fs: null source / source1 / source2 / mapping / binder / fold across all 5 modules raises ArgumentNullException.
```

- [ ] **Step 2: Run `rfs check --fail-on-error` to verify compilation fails**

Run: `rfs check --workspace "C:\wk\ranvier-release\_wt\ranvier-on-combinators" "C:\wk\ranvier-release\_wt\ranvier-on-combinators\tests\Ranvier.Tests\MapFusion.fs" --fail-on-error`
Expected: FAIL (`'Signal.map' / 'Memo' is not defined`).

- [ ] **Step 3: Implement static `Map`/`Map2`/`Bind` descriptors, `MapFusion` with lazy de-fusion, and the 5 companion modules in `src/Ranvier/Core.fs` and `src/Ranvier/Api.fs`**

In `src/Ranvier/Core.fs`:
- Add lightweight `Graph.EnterUntrackedPure()` / `LeaveUntrackedPure()` guards (incrementing untracked depth and rejecting node/cleanup creation with `ScopeMessages.forMode ScopeMode.Pure` without swapping `CurrentOwner` or touching `SourcesField`).
- Implement static 1-source `Map`, 2-source `Map2` (handling `obj.ReferenceEquals(source1, source2)` by linking a single `SourcesField` slot when identical!), and 2-slot `SplitBind` (slot 0 = outer `Signal`/`Memo`, slot 1 = current `inner: Memo<'U>`) execution kinds on `Memo<'U>`.
- Under `#if !RANVIER_TRACE` when `Platform.isNull graph.Journal`, attach an internal `IFusedStage` / `MapFusion` chain descriptor:
  - Single-consumer claim (`FusedConsumer`) when `m1` is unclaimed, unread (`RunsField = 0`), unobserved (`ObserverCount = 0`), not disposed, and shares `Owner` (`obj.ReferenceEquals(m1Owner, m2Owner)`).
  - Typed fast-path specializations for 1–4 stages (with general stage-chain fallback for depth `> 4`) caching each stage's output, checking `ComparerSentinel.equals eq_i prev_i next_i` inline (stopping early on cutoff), and tracking `stepIdx` on `try/with` so `FailureOf(ex, stageNode[stepIdx], ...)` preserves exact `ErrorOrigin`.
  - Add an inline leaf-`Dirty` fast path on `Memo<'U>.ReadSlow` when `computeKind` is a fused `Signal<'R>` map chain with `0` observers (`n.ObserverCount = 0` and idle `Guarded`/`Unchecked` graph): refresh directly from `signal.Peek` under `EnterUntrackedPure` without `EnterPull` / `RefreshChain`.
  - Hook `EnsureMaterialized()` on `Memo` external entry points (`Value`, `TryValue`, `Peek`, `Status`, `Runs`, `Failure`, `ErrorOrigin`, `Dispose`, `ISource.AddObserver` / `TryAddObserver`, and second `Memo.map`) when `FusedConsumer <> null`, materializing `m_1 .. m_i` from cached stage values and re-pointing the downstream consumer to `m_i`.
- Expose `Signal`, `Memo`, `AsyncMemo`, `AsyncSource`, and `StreamMemo` modules in `src/Ranvier/Api.fs`.

- [ ] **Step 4: Run `.NET` (untraced + `RanvierTrace=true`) and Fable tests**

Run:
```powershell
Set-Location "C:\wk\ranvier-release\_wt\ranvier-on-combinators"
rfs check "tests/Ranvier.Tests/MapFusion.fs" --fail-on-error
dotnet test tests/Ranvier.Tests/Ranvier.Tests.fsproj -c Release -- --filter-test-list "ValueCombinators"
dotnet test tests/Ranvier.Tests/Ranvier.Tests.fsproj -c Release -p:RanvierTrace=true -- --filter-test-list "ValueCombinators"
dotnet test tests/Ranvier.Tests/Ranvier.Tests.fsproj -c Release -- --filter-test-list "MapFusion"
dotnet test tests/Ranvier.Tests/Ranvier.Tests.fsproj -c Release -p:RanvierTrace=true -- --filter-test-list "MapFusion"
dotnet test tests/Ranvier.Tests/Ranvier.Tests.fsproj -c Release -- --filter-test-list "NullChecks"
dotnet fsi --exec build.fsx -- test-fable --ci
```
Expected: PASS on all commands in both untraced and `RanvierTrace=true` modes.

- [ ] **Step 5: Format, validate staged files, and commit**

```powershell
Set-Location "C:\wk\ranvier-release\_wt\ranvier-on-combinators"
dotnet fantomas src/Ranvier/Core.fs src/Ranvier/Api.fs tests/Ranvier.Tests/ValueCombinators.fs tests/Ranvier.Tests/MapFusion.fs tests/Ranvier.Tests/NullChecks.fs fable/Ranvier.Tests.Fable/Main.fs
git add src/Ranvier/Core.fs src/Ranvier/Api.fs tests/Ranvier.Tests/ValueCombinators.fs tests/Ranvier.Tests/MapFusion.fs tests/Ranvier.Tests/NullChecks.fs tests/Ranvier.Tests/TestFiles.props fable/Ranvier.Tests.Fable/Main.fs
pwsh -NoProfile -File "scripts/check-staged.ps1"
git commit -m "feat(core): add reactive companion combinators and unobserved Memo.map fusion"
```

---

### Task 5: C# Facade (`Reactive.*On` & LINQ `RanvierExtensions`) + NativeAOT Smoke

**Files:**
- Modify: `src/Ranvier.CSharp/Reactive.fs`
- Modify: `src/Ranvier.CSharp/Extensions.fs`
- Create: `tests/Ranvier.CSharp.Tests/ClosedWindowAndCombinatorTests.cs`
- Modify: `tests/Ranvier.AotSmoke/Program.fs`

**Interfaces:**
- Consumes: `Api.createMemoOn*`, `Api.createAsyncOn*`, `Api.createStreamOn*`, and modules `Signal`, `Memo`, `AsyncMemo`, `AsyncSource`, `StreamMemo` from Tasks 2–4.
- Produces:
  - `Reactive` static methods in `src/Ranvier.CSharp/Reactive.fs`:
    - `MemoOn<T, U>(Func<T> read, Func<T, U> compute)` and `(..., IEqualityComparer<U> comparer)`
    - `OwningMemoOn<T, U>(Func<T> read, Func<T, U> compute)` and `(..., IEqualityComparer<U> comparer)`
    - `AsyncOn<T, U>` (`Func<T, CancellationToken, Task<U>>` and `Func<T, Previous<U>, CancellationToken, Task<U>>`, each with and without `FlightPolicy`)
    - `OwningAsyncOn<T, U>` (`Func<T, CancellationToken, Task<U>>`, with and without `FlightPolicy`)
    - `StreamOn<K, T>(Func<K> read, Func<K, CancellationToken, StreamSource<T>> start)`
    - `StreamFoldOn<K, S, T>(S seed, Func<S, T, S> fold, Func<K> read, Func<K, CancellationToken, StreamSource<T>> start)`
  - `[<Extension; AbstractClass; Sealed>] type RanvierExtensions` in `src/Ranvier.CSharp/Extensions.fs`:
    - `Select` on `Signal<T>`, `Memo<T>`, `AsyncMemo<T>`, `AsyncSource<T>`, `StreamMemo<T>` -> `Memo<U>`
    - `Zip` on all 5 types: 2-arg returning `Memo<struct (A * B)>` (`ValueTuple<A, B>`) and 3-arg `(first, second, Func<A, B, C>)` returning `Memo<C>`
    - `SelectMany` on `Signal<T>` and `Memo<T>`: 2-arg `(source, Func<T, Memo<U>>)` -> `Memo<U>` and 3-arg `(source, Func<T, Memo<U>>, Func<T, U, R>)` -> `Memo<R>`
    - `SelectMany` on `AsyncMemo<T>` (+ `FlightPolicy` overload) and `AsyncSource<T>`: `(source, Func<T, CancellationToken, Task<U>>)` -> `AsyncMemo<U>`
    - `SelectMany` and `SelectManyFold` on `StreamMemo<K>`: `(source, Func<K, CancellationToken, StreamSource<T>>)` -> `StreamMemo<T>` and `(source, S seed, Func<S, T, S> fold, Func<K, CancellationToken, StreamSource<T>>)` -> `StreamMemo<S>`

- [ ] **Step 1: Write failing C# xUnit tests in `tests/Ranvier.CSharp.Tests/ClosedWindowAndCombinatorTests.cs` and NativeAOT checks in `tests/Ranvier.AotSmoke/Program.fs`**

Add C# tests exercising every `Reactive.*On` overload, `.Select` 4-stage chains, 2-arg `(a, b)` ValueTuple destructuring `.Zip` and 3-arg selector `.Zip`, C# LINQ query comprehension `from x in sig from y in Memo(() => x + offset.Value) select x * y`, async/stream `.SelectMany` / `.SelectManyFold`, and `ArgumentNullException` guards on null delegates/sources. Add NativeAOT smoke assertions in `tests/Ranvier.AotSmoke/Program.fs` calling `createMemoOn`, `Signal.map` -> `Memo.map` fused chain, `Memo.zip`, `Signal.bind`, `Reactive.MemoOn`, and C# `RanvierExtensions.Select`/`Zip`.

- [ ] **Step 2: Run `dotnet test tests/Ranvier.CSharp.Tests` to verify it fails to compile**

Run: `dotnet test "C:\wk\ranvier-release\_wt\ranvier-on-combinators\tests\Ranvier.CSharp.Tests\Ranvier.CSharp.Tests.csproj" -c Release`
Expected: FAIL (`'Reactive' does not contain a definition for 'MemoOn'`, `'Signal<int>' does not contain a definition for 'Select'`).

- [ ] **Step 3: Implement `Reactive.*On` in `src/Ranvier.CSharp/Reactive.fs` and `RanvierExtensions` in `src/Ranvier.CSharp/Extensions.fs`**

Validate every C# delegate and reactive source parameter with `if isNull ... then nullArg ...` before wrapping `Func<...>.Invoke` (otherwise `func.Invoke` allocates a non-null F# closure around a `null` delegate and defers the `NullReferenceException` until evaluation!). Implement 3-arg `SelectMany(source, collectionSelector, resultSelector)` via `bind` + `Memo.map` so inner ticks apply `resultSelector` with `'R` equality cutoff without re-running `collectionSelector`.

- [ ] **Step 4: Run `Ranvier.CSharp.Tests` and publish + execute `Ranvier.AotSmoke` (both untraced and `RanvierTrace=true`)**

Run:
```powershell
Set-Location "C:\wk\ranvier-release\_wt\ranvier-on-combinators"
rfs check "src/Ranvier.CSharp/Extensions.fs" --fail-on-error
dotnet test tests/Ranvier.CSharp.Tests/Ranvier.CSharp.Tests.csproj -c Release
dotnet test tests/Ranvier.CSharp.Tests/Ranvier.CSharp.Tests.csproj -c Release -p:RanvierTrace=true
dotnet publish tests/Ranvier.AotSmoke/Ranvier.AotSmoke.fsproj -c Release -o artifacts/aot-untraced
& "C:\wk\ranvier-release\_wt\ranvier-on-combinators\artifacts\aot-untraced\Ranvier.AotSmoke.exe"
dotnet publish tests/Ranvier.AotSmoke/Ranvier.AotSmoke.fsproj -c Release -p:RanvierTrace=true -o artifacts/aot-traced
& "C:\wk\ranvier-release\_wt\ranvier-on-combinators\artifacts\aot-traced\Ranvier.AotSmoke.exe"
Remove-Item -Recurse -Force artifacts/aot-untraced, artifacts/aot-traced
```
Expected: PASS (`0` IL2xxx/IL3xxx trim/AOT warnings, exit code `0` on both native executables).

- [ ] **Step 5: Format, validate staged files, and commit**

```powershell
Set-Location "C:\wk\ranvier-release\_wt\ranvier-on-combinators"
dotnet fantomas src/Ranvier.CSharp/Reactive.fs src/Ranvier.CSharp/Extensions.fs tests/Ranvier.AotSmoke/Program.fs
git add src/Ranvier.CSharp/Reactive.fs src/Ranvier.CSharp/Extensions.fs tests/Ranvier.CSharp.Tests/ClosedWindowAndCombinatorTests.cs tests/Ranvier.AotSmoke/Program.fs
pwsh -NoProfile -File "scripts/check-staged.ps1"
git commit -m "feat(csharp): expose Reactive.*On factories and LINQ Select/Zip/SelectMany extensions"
```

---

### Task 6: Counter Benchmarks (`chain-map`, `chain-on`), Reconcile, & Full Suite Verification

**Files:**
- Modify: `bench/Ranvier.Counters/Scenarios.fs`
- Modify: `fable/Ranvier.Counters/Scenarios.fs`
- Modify: `bench/Ranvier.Counters.Tests/` (only if existing tests enumerate exact scenario counts)

**Interfaces:**
- Consumes: `Signal.map`, `Memo.map`, `createMemoOn` from Tasks 2 and 4.
- Produces: `chain-map` and `chain-on` scenarios in both `.NET` (`bench/Ranvier.Counters/Scenarios.fs`) and Fable (`fable/Ranvier.Counters/Scenarios.fs`), verified under `Ranvier.Counters --reconcile` and measured against `R3.chain`.

- [ ] **Step 1: Add `chain-map` and `chain-on` to `bench/Ranvier.Counters/Scenarios.fs` and `fable/Ranvier.Counters/Scenarios.fs`**

Keep `chain` (`Ranvier`, `FSharp.Data.Adaptive`, `R3` / `Fable.Ripple`) untouched. Right after `dispose-on` (`bench/Ranvier.Counters/Scenarios.fs:940` and `fable/Ranvier.Counters/Scenarios.fs:486`), add:
- `chain-map` (`"write the source of 4 mapped memos, read the tail"`, `5000 * scale`, `[ "Ranvier", Ranvier.chainMap ]`) building `source |> Signal.map (fun v -> v + 1) |> Memo.map (fun v -> v + 1) |> Memo.map (fun v -> v + 1) |> Memo.map (fun v -> v + 1)` on `Workloads.newGraph ()` (`.NET`) / `new Graph ()` (`Fable`).
- `chain-on` (`"write the source of 4 createMemoOn memos, read the tail"`, `5000 * scale`, `[ "Ranvier", Ranvier.chainMemoOn ]`) building 4 `createMemoOn (fun () -> prev.Value) (fun v -> v + 1)` stages.

- [ ] **Step 2: Run `Ranvier.Counters.Tests` and `Ranvier.Counters --reconcile` (`-p:RanvierCounters=true -p:RanvierTrace=true`)**

Run:
```powershell
Set-Location "C:\wk\ranvier-release\_wt\ranvier-on-combinators"
rfs check "bench/Ranvier.Counters/Scenarios.fs" --fail-on-error
dotnet test bench/Ranvier.Counters.Tests/Ranvier.Counters.Tests.fsproj -c Release
dotnet run --project bench/Ranvier.Counters/Ranvier.Counters.fsproj -c Release -p:RanvierCounters=true -p:RanvierTrace=true -- --reconcile
```
Expected: PASS (`PASS  chain-map`, `PASS  chain-on`, and `PASS` on every other `Ranvier` scenario with `0` failures).

- [ ] **Step 3: Measure `chain`, `chain-map`, and `chain-on` on `.NET` and `node --jitless` (`--only chain`)**

Run:
```powershell
Set-Location "C:\wk\ranvier-release\_wt\ranvier-on-combinators"
dotnet run --project bench/Ranvier.Counters/Ranvier.Counters.fsproj -c Release -- --no-pmc --only chain --repeat 3
```
(And run the Fable harness under `node --jitless` on `--only chain` to confirm `chain-map` outperforms `R3.chain` and `chain` has not regressed.)

- [ ] **Step 4: Run the full repository verification gate**

Run:
```powershell
Set-Location "C:\wk\ranvier-release\_wt\ranvier-on-combinators"
dotnet test Ranvier.slnx -c Release
dotnet test tests/Ranvier.Tests/Ranvier.Tests.fsproj -c Release -p:RanvierTrace=true
dotnet fsi --exec build.fsx -- test-fable --ci
node scripts/packages.cjs check
pwsh -NoProfile -File scripts/check-package-compatibility.ps1
pwsh -NoProfile -File scripts/check-package-compatibility.ps1 -CheckFable
wsl.exe -d Ubuntu -- bash /mnt/c/wk/ranvier-release/_wt/ranvier-on-combinators/proofs/check.sh
wsl.exe -d Ubuntu -- bash /mnt/c/wk/ranvier-release/_wt/ranvier-on-combinators/proofs/site.sh
```
Expected: PASS across every `.NET`, `RanvierTrace=true`, Fable, package-archive, Fable-consumer, and Rocq gate.

- [ ] **Step 5: Format, validate staged files, and commit**

```powershell
Set-Location "C:\wk\ranvier-release\_wt\ranvier-on-combinators"
dotnet fantomas bench/Ranvier.Counters/Scenarios.fs fable/Ranvier.Counters/Scenarios.fs
git add bench/Ranvier.Counters/Scenarios.fs fable/Ranvier.Counters/Scenarios.fs
pwsh -NoProfile -File "scripts/check-staged.ps1"
git commit -m "bench(counters): add chain-map and chain-on scenarios"
```
