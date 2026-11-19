---
title: Troubleshooting
order: 9
---

Search for an exception message or symptom below. Each entry gives the **Cause**, the **Fix**, and a link to the full guide. [Contracts](../concepts/contracts.md) covers threading, error recovery, and ownership rules.

## Exceptions

### No ambient graph on this thread. Activate one with `use _ = graph.Activate ()`, or construct nodes against an explicit graph.

- **Cause:** Calling an ambient-graph helper (`signal`, `memo`, `effect`, `createSignal`, `createMemo`, and so on) or triggering the first read of a lazy memo or projection factory on a thread with no active ambient `Graph`.
- **Fix:** Wrap graph setup in `use _ = graph.Activate ()` (or `graph.ActivateAsync ()`), pass the `graph` instance explicitly to constructor / `Graph.*` APIs, or marshal lazy reads through `Graph.Dispatch`.
- **See:** [Graphs](graph.fsx).

### &lt;operation&gt; ran on thread N, but this graph is owned by thread M. Marshal through Graph.Dispatch, or set GraphOptions.ThreadAffinity to Unchecked if affinity is guaranteed some other way.

- **Cause:** An operation on a `Guarded` graph (`A signal write`, `A stale read`, `A batch`, `An editable write`, `Creating a node`, `Pump`, or a journal/trace call) ran on thread N instead of owning thread M — commonly after an `await` resumes on the thread pool under `ImmediateDispatcher`, or from a timer/background thread. [RAN013](https://ranvier-org.github.io/Ranvier.View/analyzers/ran013/) flags off-thread writes statically when using `Ranvier.Analyzers`.
- **Fix:** Marshal operations back to the owning thread with `Graph.Dispatch`, use a UI/hosted dispatcher, or set `GraphOptions.ThreadAffinity = ThreadAffinity.Unchecked` if external synchronisation guarantees exclusive access.
- **See:** [Threads and dispatch](threading.md) and [Contracts](../concepts/contracts.md#threading).

### &lt;operation&gt; ran on thread N outside the synchronisation context this Serialised graph was constructed on. Marshal through Graph.Dispatch.

- **Cause:** A `Serialised` graph was accessed on a thread running outside the `SynchronizationContext` captured when the graph was constructed.
- **Fix:** Marshal work into the graph via `Graph.Dispatch` so execution runs inside its captured synchronisation context and gate.
- **See:** [Contracts](../concepts/contracts.md#serialised-hosts).

### &lt;operation&gt; ran on thread N while thread M was inside this Serialised graph. Two threads entered it at once.

- **Cause:** Concurrent entry into a `Serialised` graph — for example, re-entrant `Dispatch` calls from two threads or a host `SynchronizationContext` that allows concurrent inline execution (see `dotnet/aspnetcore#69323`).
- **Fix:** Route external callers through `Graph.Dispatch` and never hold a lock or block synchronously across work that waits on another thread entering the same graph.
- **See:** [Contracts](../concepts/contracts.md#serialised-hosts).

### NotReadyException

- **Cause:** Reading `.Value` or deriving from an async source or async memo while its state is still `Pending`, outside a boundary.
- **Fix:** Guard reads with a boundary, check `.Status` / pattern-match `AsyncState` before reading, or handle `Pending` explicitly.
- **See:** [Error and suspense boundaries](boundaries.md) and [Async and pending](async-and-pending.md).

### A memo created by createMemo created an owned node in its body: a memo, effect, async value, boundary, root, projection, lookup, selector or onCleanup.

- **Cause:** A computation without its own owner scope created a reactive node or registered `onCleanup`. This applies to `createMemo` / `createMemoWith`, `createAsync` / `createAsyncWith` factories, `createEffectOn` `compute` callbacks (only `act` is an owning scope), `createLookup` `f` and `affected` callbacks, and projection aggregate callbacks (`add`, `subtract`, `folder`). [RAN010](https://ranvier-org.github.io/Ranvier.View/analyzers/ran010/) catches this at compile time.
- **Fix:** Move node creation or `onCleanup` to an owning scope (`createRoot`, an effect body, `createEffectOn`'s `act`, or a factory projection), or switch the memo to `createOwningMemo`.
- **See:** [Pure and owning memos](memos.md#pure-and-owning-memos).

### A projection row's reader created an owned node: a memo, effect, async value, boundary, root, projection, lookup, selector or onCleanup.

- **Cause:** A per-row reader passed to a projection operator allocated an owned node or registered `onCleanup` during evaluation.
- **Fix:** Keep row readers pure, or use a factory projection when per-row reactive allocations and cleanup scopes are required.
- **See:** [Projections](projections.fsx#the-factory-form).

### A projection's map created an owned node: a memo, effect, async value, boundary, root, projection, lookup, selector or onCleanup.

- **Cause:** The mapper passed to a pure projection (`createKeyedProjection`, `createIndexProjection`, `Projection.map`) allocated an owned node or registered `onCleanup`.
- **Fix:** Switch to the factory form (`createKeyedProjectionWith` / `Projection.mapWith`) so each row receives an owner disposed on removal, or keep the mapper pure.
- **See:** [Projections](projections.fsx#the-factory-form).

### The projection produced the key &lt;k&gt; twice in one pass. Keys must be unique; check the keyOf function.

- **Cause:** Two elements in the source collection mapped to the same key `<k>` in a single pass.
- **Fix:** Make `keyOf` return unique keys per element, or deduplicate keys upstream before passing items into the projection.
- **See:** [Projections](projections.fsx#identity).

### The projection's factory for key &lt;k&gt; read a pending source.

- **Cause:** A row factory created inside a factory projection read an async source whose state was `Pending`, throwing `NotReadyException` before the row finished initialising.
- **Fix:** Handle `Pending` via `.Status` / `AsyncState` inside the row, or isolate async reads inside a child memo or boundary rather than failing row construction.
- **See:** [Projections](projections.fsx#the-factory-form) and [Error and suspense boundaries](boundaries.md).

### The projection has no key &lt;k&gt;.

- **Cause:** Calling `Get(k)` or `NewValueReader(k)` on a projection for a key `<k>` that is not currently present.
- **Fix:** Call `TryGet(k)` when a key may be absent, or guard `Get(k)` / `NewValueReader(k)` with `Contains(k)`.
- **See:** [Projections](projections.fsx#reading-a-projection).

### Cannot access a disposed object. Object name: 'LookupOf`3'.

- **Cause:** Reading or calling methods on a `Lookup` (`` `LookupOf`3` ``), `` `KeyedProjection`3` ``, `` `IndexProjection`2` ``, or an inner group view from `Projection.groupBy` (`GroupByView`) after its owning scope was disposed — or, for `groupBy`, after its last item was removed and the group key was removed and disposed.
- **Fix:** Do not retain references to owned nodes beyond their owner scope, and do not hold inner `groupBy` `GroupByView` references across removals; re-read via `TryGet(groupKey)` while the group is present.
- **See:** [Lookups](lookups.fsx) and [Collection views](collection-views.fsx#group-rows).

## Symptoms

### An async value stays Pending forever

- **Cause:** Either a manual/queued dispatcher (`ManualDispatcher`) is not being drained, or `ImmediateDispatcher` is active on a `Guarded` graph so continuations hit the thread-affinity check off-thread instead of settling on the owning thread.
- **Fix:** Pump queued work via `Graph.Pump ()` when using `ManualDispatcher`, or marshal async continuations via `Graph.Dispatch` / configure a host dispatcher that posts back to the owning thread.
- **See:** [Threads and dispatch](threading.md) and [Async sources](async-sources.md).

### An exception inside an effect disappears

- **Cause:** Exceptions inside an effect body are captured into the effect's state (or surfaced via `new Effect (graph, body)` / `Status` / `Error`) rather than propagating out of the signal write that triggered the flush.
- **Fix:** Observe `Error` / `Status`, configure a host exception handler, or place fallible reads inside a `Boundary`.
- **See:** [Effects](effects.md) and [Error and suspense boundaries](boundaries.md).

### A lookup returns an old value

- **Cause:** The `affected prev next` predicate given to `createLookup` returned `false` even though the key's computed result changed, so the lookup skipped marking that key stale.
- **Fix:** Widen `affected prev next` so it returns `true` whenever a source transition can affect that key's derived output.
- **See:** [Lookups](lookups.fsx).

### An async memo does not re-run when a value read after await changes

- **Cause:** Dependency tracking stops at the first asynchronous suspension (`await`). Reactive reads performed after `await` happen after the tracking frame closes. [RAN012](https://ranvier-org.github.io/Ranvier.View/analyzers/ran012/) warns on reactive reads after `await`.
- **Fix:** Capture all reactive reads into local values before the first `await` and pass those locals into the asynchronous step.
- **See:** [Async memos](async-memos.md).

### A boundary shows its fallback forever and starts a flight on every settle

- **Cause:** An async source or async memo was created inside a reactive scope inside the boundary that re-executes when flights settle, recreating the node and launching a new flight in a loop. [RAN011](https://ranvier-org.github.io/Ranvier.View/analyzers/ran011/) warns on allocating reactive nodes inside reactive bodies that recreate them.
- **Fix:** Hoist `createAsync` / async source creation outside the re-evaluating child scope so the boundary only reads it.
- **See:** [Pure and owning memos](memos.md#pure-and-owning-memos) and [Error and suspense boundaries](boundaries.md).

### An effect did not run inside batch

- **Cause:** `batch` delays notification flushes until the outermost `batch` block finishes, so effects scheduled inside have not executed yet while execution is still inside `batch`.
- **Fix:** Perform follow-up assertions or side effects after `batch` completes, or call `flush ()` explicitly when an intermediate flush is required.
- **See:** [Batch](batch.md).
