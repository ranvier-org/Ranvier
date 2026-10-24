---
title: Inspecting a running graph over MCP
order: 13
---

:::info
Spike — `Ranvier.Mcp` is an experiment for #168 and is not packed. Its names, tools and documents may change.
:::

`Ranvier.Mcp` serves a running application's graph to an MCP client, such as a coding agent. The application
chooses what the client reads and writes: named values and authorised Elmish messages. The client evaluates no code.
A traced build also serves provenance tools, and those read the whole graph's trace log: `trace_state` and a
`trace_lookup` by `path` cover every node, and the `why` chains name the nodes they pass through. The nodes
registered with `Expose.node` are the names a client can pass as `node`, not a limit on what it can read.

The runnable sample, a cart over stdio, is
[examples/Ranvier.Mcp.Sample](https://github.com/shayanhabibi/Ranvier/tree/master/examples/Ranvier.Mcp.Sample). Its
README shows how to register it beside SageFs.

## Hosting the graph

`GraphHost.Start setup` starts a dedicated thread, builds a `Guarded` graph on it, and runs `setup` there. The host's
inbox drains on that thread, so `Graph.DispatchAsync` from a transport thread completes without a pump.

```fsharp
type Msg = Add of int

let decoders: (string * MessageDecoder<Msg>) list =
    [ "add", fun args -> Result.Ok (Add (args.GetProperty("amount").GetInt32 ())) ]

use host =
    GraphHost.Start (fun _ ->
        let mvu = Mvu.create 0 (fun (Add n) count -> count + n)
        let count = mvu.Select id
        let exposure = Exposure ()
        exposure |> Expose.value "count" (fun (n: int) -> JsonValue.Create n :> JsonNode) (fun () -> count.Value)
        exposure |> Expose.messages mvu decoders
        exposure |> Expose.node "count" count
        exposure.Start ()
        new McpAdapter (exposure))

let adapter = host.Invoke(id).Result
let server = McpServer.Create (new StdioServerTransport ("counter", NullLoggerFactory.Instance), adapter.Options)
do! server.RunAsync ()
```

- `host.Invoke work` runs `work` on the graph thread with the setup's value and returns a task of its result. It is the
  only way into the graph from another thread.
- A node touched from any other thread raises `InvalidOperationException`. `GraphHost.Start (options, setup)` raises
  `ArgumentException` for a `ThreadAffinity` other than `Guarded`.
- After disposal, `Invoke` faults with `ObjectDisposedException`, and so does work still queued.

## Exposing state

Register everything inside the setup, before `exposure.Start ()`:

| Registration | Exposes |
|---|---|
| `Expose.value name encode read` | `read ()`, encoded to JSON by `encode`, as the value `name` |
| `Expose.messages mvu [ name, decoder ]` | each message for `dispatch`; a `MessageDecoder<'Msg>` is `JsonElement -> Result<'Msg, string>` |
| `Expose.node name node` | `node` to the traced provenance tools; no effect in an untraced build |

Names use `[A-Za-z0-9_.~-]` and are unique within their kind. `Start` closes registration and creates one effect that
reads every value. The effect publishes a `Snapshot { Version; Values }`:

- Every value in a snapshot comes from the same flush.
- A flush that changes any encoding publishes one snapshot, one version higher. A batch publishes once.
- A memo cutoff, an equal write, or a re-run whose encodings all match the previous snapshot publishes nothing.

`exposure.Snapshot` is safe to read from any thread and does not enter the graph.

## Resources

| URI | Document |
|---|---|
| ranvier://state | `{"version": n, "values": {name: value}}` |
| ranvier://state/{name} | `{"version": n, "value": value}` |

Both support `resources/subscribe`. Each publication sends `notifications/resources/updated` for ranvier://state and
for each value whose encoding changed. The adapter pins MCP protocol `2025-11-25`, since `2026-07-28` replaces
`resources/subscribe` with `subscriptions/listen`.

## Tools

| Tool | Arguments | Result |
|---|---|---|
| `snapshot` | none | the ranvier://state document |
| `dispatch` | `message`: an exposed name; `args`: JSON for its decoder | the snapshot published once the message and its effects have run |
| `wait_version` | `version`; `timeoutMs` (default 5000, at most 60000) | the first snapshot at or past `version` |

`dispatch` is listed only when messages are exposed, and its input schema enumerates them. The decoder runs on the graph
thread. Results are text and structured content holding the same JSON.

A rejected call is a tool result with `isError: true` and the reason as text, and it changes nothing:

- `dispatch`: an unknown message, a missing or non-string `message`, a decoder `Error`, or a decoder that raises (for
  example, a missing property). The decoder's `Error` text is the reason.
- `wait_version`: a non-integer `version`, a negative `timeoutMs`, or a timeout.

An exception from `update` is also a tool error; the model write it interrupted does not happen.

## Traced builds

With `RANVIER_TRACE` (Debug builds, or `-p:RanvierTrace=true`), the adapter also lists these tools. Each runs its
`Trace` query on the graph thread and returns the `TraceJson` document, which `TraceJson.read*` parses:

| Tool | Arguments | Document |
|---|---|---|
| `trace_state` | none | `state`: nodes, owners, sources and observers folded from the log |
| `trace_lookup` | `node`, or `path` (an identity path) | `lookup` |
| `why` | `node` | `why`: the cause chain of the node's last run |
| `why_not` | `node` | `whyNot`: why the node has not re-run, or `null` |
| `waiting_on` | `node` | `waiting`: pending sources and flights |

`node` names a node registered with `Expose.node`; any other name is a tool error that lists the exposed nodes. The
results still cover the whole graph: `trace_state` lists every node, `trace_lookup` resolves any identity `path`, and
a `why` chain names each node it passes through. Values
are encoded without codecs, so a non-scalar value appears as `Lossy` text.

An untraced build lists none of these tools, and calling one is an unknown-tool error.
