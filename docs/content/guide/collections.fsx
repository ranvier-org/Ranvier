(**
---
title: Collections
---
*)
(*** hide ***)
#load "../../literate.fsx"

open Ranvier
open Ranvier.Docs.Maps

let graph = new Graph ()
let active = graph.Activate ()
(**
Give each collection row its own reactive value, so an edit updates the readers of that row.
Use a lookup for arbitrary requested keys and a selector for selection membership.

- [Projections](projections.fsx): keyed and index rows, factory lifetimes, pending rows and UI lists.
- [Lookups](lookups.fsx): one derived value per requested key.
- [Selectors](selectors.fsx): selection membership that wakes only the two ends of a change.
- [Deep and keyed updates](collection-updates.fsx): immutable updates, focused reads and editable keyed collections.
- [Collection views](collection-views.fsx): filtering, mapping, sorting, grouping and paging.
- [Aggregates](aggregates.fsx): incremental totals, counts and folds over rows.

Start with [Signals](signals.fsx) and [Memos](memos.md). For loading and errors, see
[Async and pending](async-and-pending.md).

Use `createKeyedCollection keyOf` when updates arrive one item at a time. Its `Rows` property
feeds the same views and aggregates as a projection over an immutable collection. Use
`NewKeyReader()` for membership and order, or `NewValueReader()` for settled row changes too ([`key_changes_added_removed_cancels`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Deltas.html#key_changes_added_removed_cancels), [`key_changes_removed_added_replaces`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Deltas.html#key_changes_removed_added_replaces), [`projection_reader_cap_resets`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Deltas.html#projection_reader_cap_resets));
see [Reading changes](projections.fsx#reading-changes).

`AddOrUpdate value` appends a new key or replaces its row in place. `Remove key` returns whether a row was
present. `Clear ()` empties the collection. `Edit` batches synchronous edits. `Dispose ()` releases its rows
and resets existing readers to empty.

In a reactive CE, `KeyedCollection.at key store`, `KeyedCollection.tryAt key store` and
`KeyedCollection.keys store` select the corresponding read of `store.Rows`. See
[pointwise collection reads](computation-expressions.md#pointwise-collection-reads).

*)
open Ranvier.CE

let store = createKeyedCollection fst
store.AddOrUpdate (1, "Write")
store.AddOrUpdate (2, "Review")

let storeKeys = memo { let! ks = KeyedCollection.keys store in return ks }

let rowLabel =
    memo {
        match! KeyedCollection.tryAt 2 store with
        | Some (_, label) -> return label
        | None ->
            let! (_, firstLabel) = KeyedCollection.at 1 store
            return firstLabel
    }

let before = storeKeys.Value, rowLabel.Value
ignore (store.Remove 2)
let after = storeKeys.Value, rowLabel.Value
before, after
(**

```text
(([|1; 2|], "Review"), ([|1|], "Write"))
```

For renderers that create one view object per key, projections also offer create-first teardown
ordering and `createProjectionOnce`, which stores a factory result without a row memo. The latter
supports `Keys`, key readers, `length`, `isEmpty`, and positional readers, while value readers,
`AsObservableCollection`, collection view operators, and fold/search/extreme aggregates require row
memos; see [Store a factory result once per key](projections.fsx#store-a-factory-result-once-per-key).
*)
(*** hide ***)
store.Dispose ()
active.Dispose ()
graph.Dispose ()
