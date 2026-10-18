# Stream memo design

Date: 2026-10-19
Branch: `feat/67-stream-memo`
Baseline: `305d00908d3f7cfbc015bd3df051c650886377dc`
Issue: [#67](https://github.com/shayanhabibi/Ranvier/issues/67). The [exploration comment](https://github.com/shayanhabibi/Ranvier/issues/67#issuecomment-6230105257) holds the prior art, the source survey and the burst probe; this design selects its option 1 with option 2's first converters.
Status: draft for review.

## Purpose and boundaries

Add an owned async node whose state is a fold over the items of a stream. A burst of items delivered off the graph thread reaches readers as one publication per inbox drain, where a loop of `AsyncSource.Settle` calls publishes once per item. The node ships the contracts the async-graph page lists as open: lifetime, restart and empty completion.

The first implementation includes:

- `createStream` and `createStreamFold`, and their C# facade factories;
- one restart policy: a change to a dependency read by `start` cancels the running stream and starts a new one from `seed`;
- converters from `IAsyncEnumerable<'T>` (.NET), `IObservable<'T>` (.NET and Fable) and JavaScript `AsyncIterable` (Fable);
- the counter scenarios `stream-burst` and `stream-restart`.

It excludes every-item delivery, back-pressure from the graph to the producer, a `ChannelReader<'T>` converter (`ReadAllAsync` already gives an `IAsyncEnumerable<'T>` on .NET 8 and later, and netstandard2.1 would need a new package reference), `EventTarget` and `System.IO.Pipelines` converters, and any change to `AsyncSource` or `AsyncMemo` semantics. No `GraphOptions` member is added, and the graph's `FlightPolicy` does not apply to streams.

## Public interface

### Node and factories

`StreamMemo<'T>` is a read-only tracked value with the reading surface of `AsyncMemo<'T>`: `Value`, `TryValue`, `Status`, `ErrorOrigin`, graph identity, owner lifetime and `Dispose`. Its constructor is internal.

In `Api.fs`, beside `createAsync`:

```fsharp
/// The latest item of the stream `start` returns. Pending until the first item; a change to a value read by `start`
/// cancels the stream and starts a new one.
val createStream : start: (CancellationToken -> StreamSource<'T>) -> StreamMemo<'T>

/// The fold of the stream's items into `seed`. Pending until the first item or the stream's completion.
val createStreamFold :
    seed: 'S -> fold: ('S -> 'T -> 'S) -> start: (CancellationToken -> StreamSource<'T>) -> StreamMemo<'S>
```

`createStream` is `createStreamFold` with the fold `fun _ item -> item`, apart from empty completion (below).

### Stream sources

```fsharp
/// A producer of items, run once per stream start.
[<AbstractClass>]
type StreamSource<'T> =
    new: unit -> StreamSource<'T>
    /// Delivers items to `sink` until the stream ends. The task completes when the stream completes, faults when it
    /// fails, and is cancelled or faults with OperationCanceledException after `token` is cancelled.
    abstract Run: sink: StreamSink<'T> * token: CancellationToken -> Task

/// The receiver of a running stream's items.
[<Sealed>]
type StreamSink<'T> =
    /// Folds `item` into the stream's state. Callable from any thread; calls must not overlap.
    member Next: item: 'T -> unit
```

A push contract covers every source family with one node: a converter for a pull source runs the enumeration loop inside `Run`, and a push source forwards its callbacks to `Next`. `Task` maps to a JavaScript `Promise` under Fable, so the abstraction compiles on both targets.

Converters in `[<RequireQualifiedAccess>] module StreamSource` (`Collections.fs`' convention for companion modules):

| Function | Targets | Behaviour |
|---|---|---|
| `StreamSource.create : (StreamSink<'T> -> CancellationToken -> Task) -> StreamSource<'T>` | both | A source from a function |
| `StreamSource.ofAsyncEnumerable : IAsyncEnumerable<'T> -> StreamSource<'T>` | .NET | `GetAsyncEnumerator token`, a `MoveNextAsync` loop calling `Next`, then `DisposeAsync`, also on failure and cancellation |
| `StreamSource.ofObservable : IObservable<'T> -> StreamSource<'T>` | both | Subscribes; `OnNext` calls `Next`, `OnCompleted` completes the task, `OnError` faults it; cancellation disposes the subscription |
| `StreamSource.ofAsyncIterable : JS.AsyncIterable<'T> -> StreamSource<'T>` | Fable | `for await` over the iterator; cancellation calls the iterator's `return ()` |

`ofAsyncEnumerable` covers `IChatClient.GetStreamingResponseAsync`, `ChannelReader.ReadAllAsync`, `taskSeq` and `AsyncSeq` on .NET.

### C#

`Ranvier.CSharp`'s `Reactive` gains `Stream<T>(Func<CancellationToken, StreamSource<T>>)` and `StreamFold<TState, T>(TState seed, Func<TState, T, TState> fold, Func<CancellationToken, StreamSource<T>> start)`. A static `StreamSources` class exposes `Create`, `FromAsyncEnumerable` and `FromObservable`.

Before writing signatures, record the public API with `fcs_public_api`. Every new name is an explicit addition to the public API baseline; no existing signature changes.

## Observable semantics

| Question | Contract |
|---|---|
| When does it start? | On first read, as an async memo's first flight does. |
| What tracks dependencies? | Reads inside `start`, which runs synchronously on the graph thread. `Run` executes outside tracking. |
| What owns it? | The owner current at creation. A pure memo body that creates one raises `InvalidOperationException`, as for an async memo. |
| Status before the first item | `Pending`. `Value` raises `NotReadyException`; `TryValue` returns `Pending`. |
| A dependency changes | The running stream's token is cancelled, its sink is closed, and the state resets to `seed`. The node is `Pending` until the new stream's first item or completion. |
| A dependency read by `start` is pending | The node is `Pending` and starts the stream when the dependency settles. |
| Items while running | Each item is folded into the state when it is delivered. The node publishes the state at most once per graph pass (see Scheduling). |
| An equal published state | Compared with the graph's equality comparer against the last published state; an equal state notifies no reader. The transition from `Pending` to `Ready` always notifies. |
| The stream fails | The node is `Failed` with the exception; `ErrorOrigin` is the stream memo. A dependency change starts a new stream. |
| `fold` throws | As a stream failure: the token is cancelled and the node is `Failed` with the exception. |
| `start` or `Run` throws synchronously | `Failed` with the exception. |
| The stream completes after items | The node keeps its last state, `Ready`. |
| The stream completes with no items | `createStreamFold`: `Ready` with `seed`. `createStream`: `Failed` with `InvalidOperationException`, so no reader waits on an ended stream. |
| Cancellation requested by the node | Not published. A cancelled or `OperationCanceledException`-faulted task after a restart or disposal is observed and ignored. |
| An `OperationCanceledException` the node did not request | A failure. |
| Disposal | Cancels the token and closes the sink. A `Pending` node becomes `Failed` with `ObjectDisposedException`, as an async memo does. Items delivered after disposal are ignored. |
| Unobserved exceptions | None: every `Run` task is observed, including a superseded stream's. |

## Node and scheduling architecture

`StreamMemo<'T>` is a specialized node in a new `Streams.fs`, compiled after `Timed.fs`. It follows `Timed<'T>`'s structure: a `SourceList` for the reads in `start`, an `ObserverSet`, an owner link, and `IScheduled` for its wake. It is not built from an async memo plus a signal: that adds a node, an edge and a scheduling turn per publication.

### State handoff

Each started stream gets a fresh `StreamSink<'T>` holding the accumulator, a closed flag and the generation it belongs to. On .NET, `Next` folds under the sink's lock and reads the closed flag under it; the graph thread takes the same lock to snapshot the accumulator. Under Fable the lock is absent. A sink closed by a restart or disposal ignores further items, so a superseded stream can never publish.

### Scheduling

- **During `Run`'s synchronous part.** Items delivered before `Run` returns fold into the accumulator. When `Run` returns, a state that received an item is published as part of the node's own execution, with no extra pass.
- **Off the graph thread (.NET).** After folding, `Next` posts one wake while none is outstanding, guarded by a `wakePosted` flag set with `Interlocked.Exchange`, as `Timed` does. The wake clears the flag before snapshotting, then enqueues the node and requests a flush; the node's execution snapshots the accumulator and publishes. Items that arrive while the wake is queued fold into the same snapshot. An item that arrives after the snapshot sees a cleared flag and posts the next wake, so no item's state is lost and at most one wake is queued.
- **On the graph thread (.NET and Fable).** `Next` follows the graph's write rule: a write on the graph thread settles before the next line runs. Outside a batch each item publishes; inside `batch`, a burst publishes once when the batch ends. The exploration proposed a microtask per burst under Fable; this design drops it, because `AsyncIterable` delivers each item on its own microtask and a microtask wake would publish per item there as well.
- **Completion and failure.** `Run`'s task outcome is applied through `Graph.Post` on the graph thread after any outstanding wake, so a completion never overtakes the final items' publication.

User code runs only in `start` (tracked, pure scope), `Run` (untracked) and `fold` (on the delivering thread, under the sink's lock on .NET). `fold` must be pure and must not read or write graph nodes.

### Trace

Trace builds record the node's creation, each stream start with its generation, each publication and the stream's end (completed, failed, cancelled). The trace event numbers and the `TraceModel` shapes follow `Timed`'s additions.

## Testing

Contract tests in `tests/Ranvier.Tests/Streams.fs`, run on .NET and under Fable, one per row of the semantics table:

- lazy start; tracking of reads in `start` and no tracking in `Run`;
- the enumerator or subscription is disposed exactly once on owner disposal and on each restart;
- a restart resets to `seed`, and an item from the superseded stream after the restart publishes nothing;
- failure from the stream and from `fold` publishes `Failed` with `ErrorOrigin` set to the stream memo;
- completion keeps the last state; empty completion gives `seed` for a fold and `InvalidOperationException` for `createStream`;
- an equal published state notifies no reader;
- disposal of a `Pending` node publishes `ObjectDisposedException`;
- no `TaskScheduler.UnobservedTaskException`.

Threading tests in `tests/Ranvier.Tests/Threading.fs`:

- 1,000 off-thread items delivered before a `Pump` produce one inbox item and one effect run, and the effect reads the final state;
- an item delivered during the wake's execution posts exactly one further wake, and its state is published;
- concurrent `Next` callers under the lock leave the fold consistent with sequential delivery (a stress test with a commutative fold).

Converter tests for each converter: completion, failure, cancellation mid-stream, and disposal of the underlying enumerator, subscription or iterator.

Mutation checks before review: removing the closed-sink check, the `wakePosted` guard, the reset to `seed`, and the generation check on completion must each fail a test.

## Performance gates

Counter scenarios in `bench/Ranvier.Counters/Scenarios.fs`, beside `async-resolve`, on .NET and Node:

- `stream-burst`: *B* items (B = 1, 16, 256) per drain into a stream memo read by a memo and an effect. Baseline: the same items through per-item `AsyncSource.Settle`. Reports retired instructions per item, `(m(2N) − m(N)) / N`.
- `stream-restart`: a dependency write restarts an open stream. Compared with an async memo flight under `CancelPrevious`.

The node justifies its surface when:

- effect runs per drain are exactly 1 for every *B*, where the baseline gives *B*;
- instructions per item at *B* = 256 are below the baseline's by at least the per-item flush cost the scenario measures;
- at *B* = 1, instructions per item are within 10% of the baseline.

The new scenarios do not change existing scenarios' counts; the CI gate holds for every existing scenario.

## Documentation

- `docs/content/guide/streams.md`: the factories, the converters, the semantics table, and how to choose between a stream memo, an async memo and an async source.
- `docs/content/concepts/async-graph.md`: replace the streaming item under *Not implemented* and the open-questions paragraph with a link to the guide and the restart contract.
- `docs/content/concepts/roadmap.md`: move streaming `IAsyncEnumerable<'T>` adapters to *Implemented in current source*.
- `docs/.ai/fable-compat.md`: `ofAsyncEnumerable` is .NET only; `ofAsyncIterable` is Fable only.

## Decisions for review

1. **Push sink rather than a pull abstraction.** One node serves pull and push sources; the cost is that converters own their loops. If a pull-only internal shape later proves faster, `StreamSource<'T>` can gain an internal fast path without a public change.
2. **On-graph-thread delivery publishes per item.** This keeps the core rule that a graph-thread write settles before the next line. A Fable or UI-thread burst from a synchronous observable costs one pass per item unless wrapped in `batch`.
3. **One restart policy.** `FlightPolicy` does not apply. `Queue` would block later streams behind an infinite one; `KeepLatest` and `FinishCurrent` have no meaning for a stream that never finishes.
4. **Restart resets to `seed` and goes `Pending`.** The previous state is not shown as `Ready` while the new stream warms up; a boundary's fallback receives the last value, as for an async memo.
5. **`createStream` fails on empty completion.** A `Ready` state with no item has no value to show; a fold has `seed`.
