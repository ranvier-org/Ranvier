---
title: Input journal and replay
order: 8
---

:::info
Preview — Ranvier is pre-release; its APIs may change. The input journal is a research feature: its entry
format and replay rules may change before the first release.
:::

An input journal records a graph's external inputs as they arrive: signal writes, async-source
settles and failures, and MVU messages. `Journal.replay` applies a recorded journal to a fresh graph,
either to reproduce the run (diagnostic replay) or to run it again with live async work
(re-enactment). The journal works in the untraced build; under a traced build a diagnostic replay
also reproduces the trace.

## Recording

Pass an `IInputJournal` in the graph's options. `MemoryJournal` keeps the entries in a list:

```fsharp
let journal = MemoryJournal "cart"
let graph = new Graph (GraphOptions.Default.WithJournal journal)
```

With `Journal = None`, the default, a graph records nothing. Every input site then costs one field
load and a null check.

Each entry is a `JournalEntry`:

| Field | Meaning |
| --- | --- |
| `Seq` | The 1-based position of the entry in its graph's journal. |
| `Kind` | `Write`, `Settle`, `Fail`, `Message` or `Flush`. |
| `Key` | The application key of the input; `None` for an unkeyed input and for `Flush`. |
| `PayloadType` | The runtime type name of the value. |
| `Payload` | `Encoded (codec, version, data)`, `NonReplayable payloadType`, or `Empty` for `Flush`. |
| `Origin` | `External`, or `Produced` for an input of the application's attributed async work. |

A `Flush` entry ends each flush pass that followed a recorded entry. Replay flushes at the same
points, so effects run as often as they did in the recorded run.

### What is recorded

An input is recorded when it arrives from outside every computation: a write from an event handler, a
settle from a completed task, a dispatched message. An input made by a running computation is derived
from the inputs before it, and is not recorded:

- a write or settle made in the body of a memo, effect or async memo, including under `untrack`;
- the model write and the synchronous commands of a journaled MVU message.

An equal write is recorded, as the traced log records it.

### Async work

An input that the application's own async work makes after an `await` is recorded with
`Origin = Produced`: a re-run of that work makes the input again. Replay uses the origin so that each
recorded input reaches the graph once in either mode. The journal attributes two kinds of work:

- an `Mvu` command: a message it dispatches after its synchronous part;
- work captured with `Journal.work`: an input passed to `Journal.produce`.

Capture the work in its synchronous part, such as an effect's body before its first `await`, and pass
each later write, settle or failure through `Journal.produce`:

```fsharp
createEffect (fun () ->
    let q = query.Value
    let work = Journal.work graph

    task {
        let! hits = service.Search q
        Journal.produce work (fun () -> results.Settle hits)
    }
    |> ignore)
```

`Journal.produce` runs the input on the graph's thread, and queues it when called from another
thread. An input of async work that bypasses `Journal.produce` is recorded as `External`, and a replay
of a run that re-runs the work applies it twice.

## Keys and codecs

A replay finds each input by its application key, a string stable across runs. `Journal.key`
registers a signal, `Journal.keySource` an async source:

```fsharp
let intCodec: JournalCodec<int> =
    { Name = "int"
      Version = 1
      Encode = string
      Decode = fun text ->
          match System.Int32.TryParse text with
          | true, value -> Ok value
          | _ -> Error ("not an int: " + text) }

let count = createSignal 0 |> Journal.key "count" intCodec
let load = AsyncSource<int> graph |> Journal.keySource "load" intCodec
```

A `JournalCodec<'T>` is a record of functions. `Name` and `Version` are recorded with each value, and
replay requires the target's codec to carry the same pair. A Ranvier.Wire codec adapts in one
expression:

```fsharp
let lineCodec (wire: WireCodec<CartLine>) : JournalCodec<CartLine> =
    { Name = "cart-line"
      Version = 1
      Encode = wire.Encode
      Decode = wire.Decode >> Result.mapError string }
```

A write to an unkeyed input, or a value its codec fails to encode, is recorded as `NonReplayable`.
The entry stays in the journal, and a replay that reaches it fails.

A key is unique on its graph, and a node holds one key. The registration belongs to the innermost
enclosing scope, as an `onCleanup` does: when the scope is disposed, or its computation re-runs, the
key is free for another node. On a graph with a journal, `Journal.key` raises
`InvalidOperationException` in the body of a pure computation, such as a `createMemo` memo. In a traced build `Journal.key` also labels an unlabelled
node with the key.

The settles of an async memo are not recorded: an async memo's flight is a computation over its
inputs. Route an external result through a keyed `AsyncSource` to journal it.

## MVU messages

`Mvu.journal` registers an MVU loop's messages:

```fsharp
let app =
    Mvu.withCmd init update
    |> Mvu.journal "cart" cartMsgCodec
```

A message dispatched from outside every computation records one `Message` entry. A message a command
dispatches later, after an `await`, records a `Produced` entry; the command needs no journal code.
Another adapter registers its message site with `Journal.messages`, wraps each external message in
`Journal.recordMessage`, and passes the messages of its async work through `Journal.produce`.

## Replay

`Journal.replay mode graph entries` applies the entries to `graph` in `Seq` order, on the graph's
thread. The target graph needs a journal, with the inputs registered under the recorded keys: build it
with the same code as the recorded run.

```fsharp
let replayed = MemoryJournal "cart-replay"
let fresh = new Graph (GraphOptions.Default.WithJournal replayed)
buildCart fresh
Journal.replay ReplayMode.Diagnostic fresh journal.Entries
```

The entries between two `Flush` entries run in one batch; a segment of one entry runs unbatched. The
replay's own inputs are recorded on the target's journal.

### Diagnostic replay

`ReplayMode.Diagnostic` reproduces the recorded run. Every entry is applied: async sources settle
with the recorded values, a recorded failure arrives as a `JournalReplayedFailure` carrying the
recorded message, and the `Produced` messages and writes are applied in their recorded order. The
attributed async work still runs, and its inputs are dropped: a command started by a replayed message
dispatches into nothing, as does `Journal.produce` for work captured before the replay returns. Work
captured after the replay returns is live.

A gateway that also wants to skip its external call reads `Journal.replaying` in its synchronous part:

```fsharp
createEffect (fun () ->
    let q = query.Value
    let work = Journal.work graph

    if Journal.replaying graph <> Some ReplayMode.Diagnostic then
        startSearch q (fun hits -> Journal.produce work (fun () -> results.Settle hits)))
```

Under a traced build, a diagnostic replay of a run whose batches each end in a flush gives the recorded
run's `Trace.snapshot` and the `Trace.why` of every effect run.

### Re-enactment

`ReplayMode.Reenact` applies the `External` writes and messages, and skips the `Produced` entries and
the recorded settles and failures. The application's async work runs again and makes those inputs,
and a changed result flows through the graph. The re-run inputs arrive when the work completes, after
the replay returns. Use it to check a
recorded session against a new service or a changed `update`.

### Errors

A replay raises `JournalReplayException` on an entry it cannot apply, naming its `Seq`, `Key` and
`PayloadType`, with the `Reason`:

- the entry is `NonReplayable`;
- its key is not registered on the target graph;
- the key's input takes no entry of that kind, such as a `Settle` for a signal;
- the codec name or version differs from the key's codec;
- the payload fails to decode.

The entries of a batch are decoded before the batch runs. A failing entry leaves the earlier batches
applied and its own batch unapplied. A replay into a graph with no journal raises
`InvalidOperationException`.

## Restore

The journal position is an entry's `Seq`. A stored-state snapshot taken at a position, restored
into a fresh graph, is followed by a replay of the entries with a greater `Seq`. Restoring a
snapshot lands with the stored-state format of
[#68](https://github.com/shayanhabibi/Ranvier/issues/68); until then a replay starts from a fresh
graph.

## Fable

The journal, `Mvu.journal` and both replay modes behave the same under Fable, with one difference: a
write or settle after an `await` in an async memo's body is recorded as an `External` input, where
.NET records nothing. Pass it through `Journal.produce`, with the work captured before the `await`,
to record it as `Produced`. See [Fable](../fable/index.md).
