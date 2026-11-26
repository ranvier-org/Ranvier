---
title: Reactive computation expressions
description: Choose reactive inputs, close dependency tracking and control per-key lifetimes in F#.
---

Open `Ranvier.CE` alongside `Ranvier` for the computation-expression API. The builders below describe the CE
release. For the public pre-CE equivalents and the internal runtime paths each builder selects, see
[primitive selection](#primitive-selection).

| Builder | Result | Header operations |
| --- | --- | --- |
| `memo` | `Memo<'T>` | `owning` and/or `comparer cmp`, or `option` alone |
| `effect` | `unit` by default, `INode` with `node` | `node` |
| `asyncMemo` | `AsyncMemo<'T>` | `owning`, `policy flightPolicy` |
| `stream` | `StreamMemo<'Item>`, or `StreamMemo<'State>` when folding | `fold seed folder` |
| `editable` | `Editable<'T>` whose edit is replaced by a changed seed | None |
| `draft` | `Editable<'T>` whose edit survives a changed seed | None |
| `boundary` | `Boundary<'T>` | `pending fallback` and/or `failed recover`, optional `comparer cmp` |
| `row` | `unit -> 'T` for a projection factory | None |

## Choose the inputs

Use `let!` for a tracked read. Use `and!` when both sources are known independently. A second `let!` can
select its source using the first value. After a reactive bind, the terminal calculation runs with tracking
closed. Capture each value that should wake the computation in a bind.

```fsharp
open Ranvier
open Ranvier.CE

let subtotal (price: Signal<decimal>) (quantity: Signal<int>) =
    memo {
        let! unitPrice = price
        and! units = quantity
        return unitPrice * decimal units
    }
```

The inputs can be signals, memos, editable values (including drafts), boundaries, async memos, async sources,
streams or timed values. They share the `IReactive<'T>` contract for the graph and a tracked read. Pending and
failed inputs propagate their state. Combining sources from different graphs with `and!` raises
`InvalidOperationException`. A static first source supplies the graph even outside `graph.Run`. Start blocks
with no static outer source inside an active graph. Lazy nodes defer the body until evaluated. Effects run on
installation.

## Memos and effects

`memo` returns a `Memo<_>`. Use `return!` to switch to an inner memo. Changes to the inner result update the
output, while an equal outer key keeps the selected memo and its scope. A plain `return` computes a value. Use
`memo.previous` for the last published output as a value option, or `memo.previousOr seed` for an initial
value.

```fsharp
let peak (temperature: Signal<int>) =
    memo {
        let! current = temperature
        let! previous = memo.previousOr System.Int32.MinValue
        return max previous current
    }

let selected (key: Signal<string>) (detail: string -> Memo<string>) =
    memo {
        let! id = key
        return! detail id
    }
```

Put header operations before binds and body expressions. `owning` gives the memo's computation a scope for
child nodes and cleanups. `comparer cmp` selects output equality. They can be combined. `option` retains the
previous `Some` wrapper when the payload compares equal. It is exclusive with `owning`, `comparer` and the
previous-value tokens. Header operations can each occur once.

An `effect` with explicit inputs runs its terminal action untracked. Put `node` first to receive an `INode`
for inspection. Without `node` the result is `unit`. An effect with no binds runs its body as an ordinary
tracked effect.

```fsharp
let reportSelection (name: Signal<string>) =
    effect {
        node
        let! selected = name |> withComparer System.StringComparer.OrdinalIgnoreCase
        printfn "Selected %s" selected
    }
```

`Ranvier.CE.withComparer cmp source` (`source |> withComparer cmp`) sets equality for the **input key** in a
split `memo`, `effect`, `asyncMemo` or `stream` — either on a single source (`let! x = src |> withComparer cmp`)
or on individual arms combined with `and!`:

```fsharp
let subscribePair (channel: Signal<string>) (page: Signal<int>) =
    effect {
        let! name = channel |> withComparer System.StringComparer.OrdinalIgnoreCase
        and! number = page
        printfn "Subscribed %s #%d" name number
    }
```

An equal key preserves the work attached to it (on `and!`, each customized arm uses its comparer while any
remaining arms keep the graph's equality policy). The memo or boundary header `comparer cmp` compares
**published outputs**. For example, ignoring case on a subscription name can keep the subscription alive
across spelling changes. Ignoring case on its output can suppress a downstream notification after the
computation has run.

## Async memos and streams

Bind reactive inputs before awaiting work in `asyncMemo`. A task or value-task bind closes the dependency
window. Placing a reactive bind after it is a type error. `policy p` overrides the graph's flight policy.
`owning` allows children and cleanups during the synchronous part of the flight before its first suspending
await.

```fsharp
let fetch (http: System.Net.Http.HttpClient) (address: Signal<System.Uri>) =
    asyncMemo {
        policy FlightPolicy.KeepLatest
        let! uri = address
        let! ct = asyncMemo.cancellationToken
        return! http.GetStringAsync(uri, ct)
    }
```

`let! prior = asyncMemo.previous` supplies a `Previous<_>` handle. Await `prior.Settled` in the task phase for
the optional preceding result, including ordered publication under `Queue`. `asyncMemo.cancellationToken`
belongs to this flight. An equal settled input preserves a running flight under every
[flight policy](async-memos.md#flight-policy).

A `stream` returns a `StreamSource<_>` from its body. The resulting `StreamMemo<_>` holds the latest item. Use
`fold seed folder` to accumulate items and `stream.cancellationToken` to pass the current subscription's token
to the producer.

```fsharp
let countMessages (room: Signal<string>)
                  (connect: string -> System.Threading.CancellationToken -> StreamSource<string>) =
    stream {
        fold 0 (fun count _ -> count + 1)
        let! channel = room
        let! ct = stream.cancellationToken
        return connect channel ct
    }
```

A new settled channel starts the fold from its seed. While the key is pending or failed, the old stream can
continue accumulating. Recovery to the same key reveals its current state.

## Editable values, drafts and boundaries

`editable` and `draft` both return an `Editable<_>`. Assign to `Value` to edit locally. An editable drops the
edit when its seed publishes an unequal value. A draft keeps the edit until `Reset ()`. `editable.previous`
and `draft.previous` return the seed's last publication as a value option. Use `editable.previousOr seed` or
`draft.previousOr seed` to supply an initial value. The edit itself is available through `Value`.

```fsharp
let names (upstream: Signal<string>) =
    let display = editable {
        let! name = upstream
        return name.Trim()
    }
    let form = draft {
        let! name = upstream
        let! previous = draft.previousOr ""
        return if name = "" then previous else name
    }
    display, form
```

`boundary` owns the body and handles pending reads, errors or both. Supply at least one handler. `pending`
receives the last published value as a value option. `failed` receives the exception and that same
previous-value argument. Add `comparer` for custom output equality. Create the boundary under the owner that
should dispose it.

```fsharp
let show (result: AsyncMemo<string>) =
    boundary {
        pending (ValueOption.defaultValue "Loading")
        failed (fun error _ -> error.Message)
        let! text = result
        return text
    }
```

Context tokens are inert values outside a bind. Each builder accepts only its own tokens. For example, binding
`memo.previous` in `draft` is a compile-time error.

## Pointwise collection reads

Choose a descriptor for the part of a collection the body consumes. Creating a descriptor leaves the read
until evaluation.

| Source | Required value | Optional value | Membership and order |
| --- | --- | --- | --- |
| `Projection<'K,'V>` | `Projection.at key rows` reads `Get`. An absent key raises `KeyNotFoundException`. | `Projection.tryAt key rows` reads `TryGet` and yields `None` for an absent key. | `Projection.keys rows` reads the key array. |
| `Lookup<'K,'V>` | `Lookup.at key lookup` reads `Get` and creates a cell on demand. | `Lookup.tryAt key lookup` reads a live cell. An absent cell yields `None` without creating a cell or subscribing. | A lookup has an open key domain. |
| `KeyedCollection<'K,'V>` | `KeyedCollection.at key store` | `KeyedCollection.tryAt key store` | `KeyedCollection.keys store` |

The keyed collection forms delegate to `store.Rows`. A pending or failed row can still suspend or fail an
optional read. Use `match!` to branch on absence and bind further reactive inputs only in the branch that
needs them.

```fsharp
let titleFor id (titles: Projection<int,string>) (missing: Signal<string>) =
    memo {
        match! Projection.tryAt id titles with
        | Some title -> return title
        | None ->
            let! fallback = missing
            return fallback
    }
```

A projection factory already supplies a row memo. Return a `row` reader from `createProjectionWith`,
`createProjectionWithOptions`, `createIndexProjectionWith` or `Projection.mapWith`. The builder returns
`unit -> 'V`. Each invocation tracks the binds on the calling row and closes tracking for the result
expression. Only `row` accepts a factory's `unit -> 'T` accessor directly alongside reactive inputs.

```fsharp
let labels (prefix: Signal<string>) (titles: Projection<int,string>) =
    titles
    |> Projection.mapWith (fun _ readTitle ->
        row {
            let! title = readTitle
            and! tag = prefix
            return tag + title
        })
```

## Primitive selection

The compiler selects a path from the expression shape and headers. In the table, a static key is one reactive
source or independent sources combined with `and!`. Dynamic reads include a source selected by a preceding
bind or a conditional branch. The **Public pre-CE equivalent** column lists the public functions callers invoke
directly; `Create*`, `CombinatorMemos.*` and the `Editable` constructor listed under runtime dispatch are
`internal` entry points that the builders call on the source graph or `Graph.Current`.

| Expression shape | Public pre-CE equivalent | Runtime dispatch and graph |
| --- | --- | --- |
| `memo` maps one signal, memo or editable | `Signal.map`, `Memo.map`, or `createMemoOn` over `edit.Value` | Public `Signal.map`, `Memo.map`, or `Memo.map` on the editable's `internal` value memo, on the source graph. Eligible chains can fuse. |
| `memo` maps an async, stream, boundary or timed node | `AsyncMemo.map`, `AsyncSource.map`, `StreamMemo.map`, or `createMemoOn` | `internal` `CombinatorMemos.createSourceMap` on the source graph. |
| `memo` maps a pointwise descriptor | `createMemoOn` reading `.Get`, `.TryGet`, or `.Keys` | `internal` `Memo.CreateOn` with the descriptor's tracked read on its graph. |
| `memo` maps a pair of signals or a pair of memos/editables | `Signal.map2` / `Signal.zip`, `Memo.map2` / `Memo.zip`, or `createMemoOn` | Public `Signal.map2` or `Memo.map2` on the shared graph. |
| `memo` maps mixed or larger independent inputs, or a static key with `owning` or `comparer` | `createMemoOn`, `createMemoOnWithComparer`, `createMemoWithOn`, or `createOwningMemoOnWithComparer` | `internal` `Memo.CreateOn` with the single or merged key, selected scope and output equality on the source graph. |
| `memo` with `option` | `createOptionMemo` | Public `createOptionMemo` semantics, on the static source graph or `Graph.Current`. |
| `memo` ends in `return!` of a selected memo, including alternative tails | `Signal.bind`, `Memo.bind`, or `createMemoWithOn` | Public `Signal.bind` or `Memo.bind` for a simple default source, otherwise the `internal` split bind on the outer graph. The binder owns newly created children. |
| `memo` uses previous output, sequential binds, branching or an imperative body | `createMemo`, `createMemoWithComparer`, `createMemoWith`, or `createOwningMemoWithComparer` | `internal` `Memo.Create` or `Memo.CreateWithComparer`. The outer static source supplies the graph when available, otherwise `Graph.Current`. |
| `effect` consumes a static key, optionally with `node` | `createEffectOn` or `createEffectOnWithComparer` | `internal` `EffectOn<'Key>.Create` or `EffectOn<'Key>.CreateWithComparer` on the source graph. |
| `effect` uses sequential binds or a body with no binds | `createEffect` or `createEffectNode` (with `untrack` around closed actions) | `internal` `Effect.Create` on the outer source graph or `Graph.Current`. With binds the terminal action is untracked. |
| `asyncMemo` consumes a static key | `createAsyncOn`, `createAsyncWithOn`, `createAsyncOnWithPolicy`, `createOwningAsyncOnWithPolicy`, `AsyncMemo.bind`/`bindWithPolicy`, or `AsyncSource.bind` | `internal` `AsyncMemo.CreateOn` on the source graph, with the selected policy and ownership. |
| `asyncMemo` starts with dynamic inputs or has no reactive binds | `createAsync`, `createAsyncWith`, `createAsyncWithPolicy`, or `createOwningAsyncWithPolicy` | `internal` `AsyncMemo.Create` on the outer source graph or `Graph.Current`. Tracking closes before asynchronous work. |
| `stream` consumes a static key | `createStreamOn`, `createStreamFoldOn`, `StreamMemo.bind`, or `StreamMemo.bindFold` | `internal` `StreamMemo.CreateOn` for latest item, `internal` `StreamMemo.CreateFoldOn` for `fold`, on the source graph. |
| `stream` uses sequential binds or has no reactive binds | `createStream` or `createStreamFold` | `internal` `StreamMemo.Create` with latest-item or fold state, on the outer source graph or `Graph.Current`. |
| `editable` or `draft` derives a seed | `createEditable` or `createDraft` | `internal` `Editable` constructor on the static source graph or `Graph.Current` with the selected edit lifetime. |
| `boundary` has `pending`, `failed` or both | `createSuspense*`, `createErrorBoundary*`, or `createBoundary*` | `internal` `Boundary.Create` or `Boundary.CreateWithComparer` under the current owner, using `Graph.Current` or entering the outer static source's graph. |
| `row` combines item accessors and reactive sources | `fun item -> fun () -> ...` passed to `createProjectionWith*`, `createIndexProjectionWith`, or `Projection.mapWith` | A reader thunk executes on the ambient row graph with tracking closed for the result expression. It uses the projection's existing row memo. |
