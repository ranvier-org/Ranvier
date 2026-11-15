# Ranvier

Fine-grained reactive computation for .NET and Fable.

> **Preview.** Ranvier is pre-release. Its APIs may change before the first release.

Ranvier connects signals, memos and effects in a reactive graph. Signals hold writable state;
memos derive values and recompute on demand after their inputs change; effects run side effects
after changes. Async sources carry an explicit Pending state, and boundaries handle pending or failed reads.

```fsharp
open Ranvier

use graph = new Graph ()

graph.Run (fun () ->
    let count = createSignal 1
    let doubled = createMemo (fun _ -> count.Value * 2)
    createEffect (fun () -> printfn "doubled = %d" doubled.Value)
    count.Value <- 5)
```

C# projects reference [Ranvier.CSharp](https://www.nuget.org/packages/Ranvier.CSharp), which adds delegate-based
factories and extension methods.

## Targets

`net10.0` and `netstandard2.1`. Applications on .NET 8 and .NET 9 use the `netstandard2.1` build.

Package builds from this source include F# sources for Fable's JavaScript target. The same core APIs support
signals, memos, effects, bindings and collections on both targets; see the
[Fable guide](https://ranvier-org.github.io/Ranvier/fable/).

## Fixed-dependency bindings and ownership

`bindSignal source target write` and `bindMemo source target write` copy one source into an
external target in the flush. The writer receives the target and current value, runs untracked
and must not create reactive nodes. A pending or failed memo leaves the target unchanged;
an equal source value does not write again. Both factories return `unit` and attach to the current owner.
Use a memo before `bindMemo` to derive a target value, or an effect for work with dynamic dependencies.

Owners dispose children and cleanups together in reverse acquisition order. A cleanup registered
after a child is created runs before that child is disposed, both on disposal and before a run's
owned resources are replaced. See [Bindings](https://ranvier-org.github.io/Ranvier/guide/bindings/)
and [Cleanup](https://ranvier-org.github.io/Ranvier/guide/cleanup/).

## Keyed row factories

`createProjectionWithOptions ProjectionOptions.CreateFirst keyOf factory source` creates added
rows before retiring removed rows. If a new factory throws, the pass disposes its additions and
keeps the previously applied rows, items and keys until a source change retries the pass.

`createProjectionOnce options keyOf factory source` stores one factory result per key without a
row memo. It suits a view object whose factory creates its own bindings. `Keys`, `Count`, `Contains`,
`Get`, `TryGet`, `NewKeyReader`, `length`, `isEmpty`, and positional readers (`tryHead`, `tryLast`,
`tryItem`, `head`, `last`, `item`) are supported; `AsObservableCollection`, `NewValueReader`, all
`Projection` view operators (`filter`, `choose`, `map`, `mapWith`, `sortBy`, `sortByDescending`,
`take`, `skip`, `sub`, `partition`, `distinctBy`, `groupBy`), and fold/search/extreme aggregates
raise `NotSupportedException`. Use `createProjectionWith` when consumers need reactive row values.
See [Projections](https://ranvier-org.github.io/Ranvier/guide/projections/).

## Per-node equality

Ordinary factories use `GraphOptions.Equality`. Override a node's value cutoff with a typed `IEqualityComparer<T>`
via `createSignalWithComparer`, `createMemoWithComparer`, `createOwningMemoWithComparer`,
`createEffectOnWithComparer`, `createSuspenseWithComparer`, `createErrorBoundaryWithComparer`, and
`createBoundaryWithComparer` (or the C# `Reactive` comparer overloads). Pending, failed, waiting, and
caught-error transitions still propagate; see [Per-node equality](https://ranvier-org.github.io/Ranvier/guide/equality/#per-node-equality).

## Editable collections and change readers

```fsharp
let items = createKeyedCollection (fun (id, _) -> id)
items.Edit(fun edit ->
    edit.AddOrUpdate(1, "write")
    edit.AddOrUpdate(2, "test"))
let titles = items.Rows |> Projection.map snd
use reader = titles.NewValueReader ()
reader.Read () |> ignore // initial reset
items.AddOrUpdate(2, "retest")
let delta = reader.Read () // Changed for key 2
```

`Reactive.KeyedCollection<T, K>(keyOf)` is the C# factory. `Rows` supports the existing
map/filter/sort/group/aggregate operators. Updating a live key writes its value directly, preserving
key order without scanning the collection. New keys append; `Remove` unlinks in O(1) via an
intrusive doubly-linked list, and net-empty append/remove batches reuse the prior key array.
Removing and re-adding a key creates a new row, reported as `Replaced` to a reader that saw the old row.
`Edit` batches synchronous writes and retains applied edits if its callback throws.

`NewKeyReader()` reports membership and order; `NewValueReader()` also reports `Changed` for unequal
settled values. See [Collections](https://ranvier-org.github.io/Ranvier/guide/collections/) and
[Collection views](https://ranvier-org.github.io/Ranvier/guide/collection-views/) for reader cursors,
observation lifetime, and incremental view costs.

## Tracing

`Ranvier.Traced` is the same assembly, namespaces and version built with the per-graph event log and its queries.
Reference it in place of `Ranvier` for development builds; see
[Tracing](https://ranvier-org.github.io/Ranvier/guide/tracing/).

## Links

- [Documentation](https://ranvier-org.github.io/Ranvier/)
- [Source](https://github.com/ranvier-org/Ranvier)
- [MIT License](https://github.com/ranvier-org/Ranvier/blob/master/LICENSE)
