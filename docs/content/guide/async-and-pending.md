---
title: Async and pending
---

A pending read waits for data; a failed read reports an exception. Both states travel on the
pending channel through dependent computations until a [boundary](boundaries.md) supplies a
display value.

## Choosing an async primitive

| Primitive | Source | Result | Use it when |
| --- | --- | --- | --- |
| [`createAsyncSource`](async-sources.md) | Application `Settle` / `Fail` | Latest settled value | Callbacks or external code push single outcomes. |
| [`createAsync`](async-memos.md) | `Task<'T>` per tracked run | Latest flight's result | A request computes from reactive inputs under a [flight policy](async-memos.md#flight-policy). |
| [`createStream` / `createStreamFold`](streams.md) | Continuous `StreamSource<'T>` | Latest item or folded seed | A producer emits many items (SSE, tokens, observables, channels) and restarts on input change. |
| [`createBoundary` / `createSuspense` / `createErrorBoundary`](boundaries.md) | Surrounding body + fallback/recovery | Display value or recovered value | UI or callers convert loading and failure into concrete values. |
| [`Ranvier.Query`](queries.md) | Shared key-indexed `QueryClient` | Scoped cached response | Shared remote queries need page leases, refresh retention, and mutation reconciliation. |

The async pages below use a manual dispatcher for off-thread completions: see
[Threading and dispatch](threading.md) and the `pumpUntil` helper in [Async memos](async-memos.md),
and [Testing async state](testing.md) for deterministic fake-service tests.

## Node status

Every node reports a `Status` (`[<Flags>]` enum):

| Flag | Meaning |
|------|---------|
| `Status.None` | No flag is set: the node holds a settled value and has no pending re-run. |
| `Status.Pending` | The node is waiting on a value that has not arrived. |
| `Status.Error` | The node's last run failed. Reads report the exception. |
| `Status.Uninitialized` | The node has not produced a value yet. |
| `Status.Stale` | The graph has marked the computation out of date since its last run. It re-runs, or confirms its value, on its next read or flush. |

Flags combine: an unsettled `AsyncSource` reports `Pending ||| Uninitialized`, and an effect
woken inside a [batch](batch.md) reports `Stale` until the batch ends. Memos, effects, async memos,
boundaries, projections, stream memos, timed values and bindings can report `Stale`; signals and
async sources never do. Reading `Status` runs nothing and records no dependency, so `Stale`
reflects marks already made.

Test one flag with `HasFlag` rather than comparing the whole status with `Status.None`:

```fsharp
let isPending (node: INode) = node.Status.HasFlag Status.Pending
let hasFailed (node: INode) = node.Status.HasFlag Status.Error
```

Pending is independent of dirtiness. A dirty node is out of date and recomputes on its next read.
A pending node has run and is waiting on a source. Pending propagates: a memo, effect or boundary
that reads a pending node becomes pending itself until a [boundary](boundaries.md) stops it.

## Reading pending or failed nodes

`graph.TrackStatus node` accepts nodes of different value types. It refreshes a readable node
and tracks its status inside a computation. For an effect it reads the status untracked.
To inspect the value, choose a read according to what the caller should see while waiting.

- `TryValue` returns a `Reading<'T>`: `Ready value`, `Pending`, or `Failed error`.
- `.Value` returns the value. On a pending node it raises `NotReadyException`, which carries the
  source that is not ready. On a failed node it raises the stored exception.
- `Peek` reads the last settled value (or default before first settle) untracked and without
  starting a flight.

Both `.Value` and `TryValue` link the same dependency edge, so a reader that saw `Pending` is woken
when the source settles.

Settle the source, fail it, then settle it again. The state travels through the memo;
the boundary supplies a display value for the effect.

```fsharp map replay code=collapsed
let price = createAsyncSource<int> ()
let total = createMemo (fun _ -> price.Value * 3)
let view = createBoundary (fun _ -> "Loading") (fun ex _ -> "Error: " + ex.Message) (fun () -> sprintf "Total %d" total.Value)
createEffect (fun () -> printfn "%s" view.Value)

controls [
    button "Settle 4" (fun () -> price.Settle 4)
    |> describe "The source settles at 4; the boundary displays Total 12."
    |> expect "The source settles at 4; the boundary displays Total 12." (fun () -> view.Peek = "Total 12")
    button "Fail" (fun () -> price.Fail (exn "offline"))
    |> describe "The boundary catches the failed source and displays the error."
    |> expect "The boundary catches the failed source and displays the error." (fun () -> view.Peek = "Error: offline")
    button "Recover with 5" (fun () -> price.Settle 5)
    |> describe "A new settlement clears the error and displays Total 15."
    |> expect "A new settlement clears the error and displays Total 15." (fun () -> view.Peek = "Total 15")
]
```

:::details Catching pending reads and using untrack

A body that catches `NotReadyException` in its own `try/with` is still pending, whatever it returns.
A read inside `untrack` is the exception: the body's result stands. A pending read inside `untrack`
that the body does not catch leaves the body pending with no edge to the source, so settling the
source does not re-run it. Read the source tracked, or catch the exception.

:::

```fsharp
open System
open Ranvier

let newGraph () =
    new Graph ({ GraphOptions.Default with Dispatcher = Some (ManualDispatcher () :> IGraphDispatcher) })

/// Renders a reading with the exception type and message.
let show (reading: Reading<'T>) =
    match reading with
    | Ready v -> sprintf "Ready %A" v
    | Pending -> "Pending"
    | Failed e -> sprintf "Failed %s: %s" (e.GetType().Name) e.Message

let graph = newGraph ()

let user, greeting =
    graph.Run (fun () ->
        let user = createAsyncSource<string> ()
        let greeting = createMemo (fun _ -> "Hello, " + user.Value)
        user, greeting)

show greeting.TryValue // Pending
user.Settle "Ada"
show greeting.TryValue // Ready "Hello, Ada"
```

A pending read stops an effect's body at that read and re-runs it from the start when the source settles;
see [Why bodies re-run from the start](../concepts/suspension.md#re-running-versus-resuming).

::::details Test your understanding

Does this effect add a message before the name settles? What is in the log afterward?

```fsharp
let log = ResizeArray<string> ()

let name =
    graph.Run (fun () ->
        let name = createAsyncSource<string> ()
        createEffect (fun () -> log.Add ("saw " + name.Value))
        name)

let beforeSettle = log.Count
name.Settle "Grace"
beforeSettle, List.ofSeq log
```

:::details Answer

```text
(0, ["saw Grace"])
```

:::
::::

## Failures

A failure is a settled outcome, not a slow success. A memo whose source fails settles as `Failed`,
and its readers see the error, not `Pending`. The exception survives unchanged through every memo
between the source and the reader (`ErrorOrigin` preserves its origin).

A throwing effect records `Status.Error` and `Effect.Error` without stopping other effects in the
same flush. Since `createEffect` returns `unit`, inspect `Status` and `Error` on `new Effect (graph, body)`
or via [Tracing](tracing.md). See [Effects — Failures](effects.md#failures) and
[Boundaries](boundaries.md) for recovery.

Start with [Signals](signals.fsx), [Memos](memos.md) and [Effects](effects.md) for the synchronous
primitives, [Fable](../fable/index.md) for promise microtask differences, and
[Projections](projections.fsx#pending-and-failed-rows) for async collection rows.
