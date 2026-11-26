---
title: Roots and owners
---

Every memo, effect and binding belongs to an owner. Disposing that owner runs its cleanups and disposes
its children ([`dispose_reverse_order`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Ownership.html#dispose_reverse_order), [`heap_ownership_guarantees`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Heap.Ownership.html#heap_ownership_guarantees)). Disposing it again is safe ([`retire_unlinked_child_idempotent`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Collections.html#retire_unlinked_child_idempotent)).

The owner can be the graph's `Root`, a scope created by `createRoot`, or the current run of an
enclosing effect, owning memo or boundary.

## Create a scope

- `createRoot body` creates a scope, runs `body` with its `Owner`, and returns `body`'s result.
- `owner.Dispose ()` disposes everything created inside that scope.
- `graph.Dispose ()` disposes every node the graph owns. Use `use graph = new Graph ()` to dispose
  the graph automatically when the enclosing scope ends.

In this replay, disposing the root removes its effect's dependency ([`disposed_no_edges_new_graph`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Ownership.html#disposed_no_edges_new_graph)). The signal remains available,
but later writes no longer run that effect ([`disposed_effect_never_runs`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Ownership.html#disposed_effect_never_runs)).

```fsharp map replay code=collapsed
let count = createSignal 0
let mutable runs = 0
let owner =
    createRoot (fun owner ->
        createEffect (fun () ->
            runs <- runs + 1
            printfn "count = %d" count.Value)
        owner)

controls [
    button "Write 1" (fun () -> count.Value <- 1)
    |> describe "The root's effect responds while its owner is alive."
    |> expect "The effect responds before disposal." (fun () -> runs = 2)
    button "Dispose root" (fun () -> owner.Dispose ())
    |> describe "Disposal removes the effect and its dependency edge."
    |> expect "Disposal does not run the effect again." (fun () -> runs = 2)
    button "Write 2" (fun () -> count.Value <- 2)
    |> describe "The signal changes, but the disposed effect stays silent."
    |> expect "The disposed effect stays silent." (fun () -> runs = 2 && count.Peek = 2)
]
```

## Capture an owner for later work

`getOwner ()` returns the scope new nodes and cleanups will join. Capture it before an await. On the graph
thread, `runWithOwner owner body` attaches new resources there and preserves the caller's tracking state. A
disposed owner immediately disposes a child attached to it.

Use `tryGetOwner ()` when a helper can run in a pure computation. It returns `ValueNone` there. Calling
`getOwner ()` there raises a scope violation and fails the computation even if the helper catches the
exception. Both functions resolve the active graph.

## Disposal

:::details Disposal order and nested roots

An owner tears down its cleanups and children together in reverse acquisition order, as `use` bindings
are disposed ([`dispose_reverse_order`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Ownership.html#dispose_reverse_order), [`teardown_runs_in_reverse`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Ownership.html#teardown_runs_in_reverse), [`owners_in_order`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Ownership.html#owners_in_order)). A cleanup registered after a node is created runs before that node is disposed.
Nested owners apply that rule to their own children when they are reached; cleanups are not a
separate phase that runs before or after all children.

A root created inside an effect belongs to that effect's current run. It is disposed before the
effect's next run.
:::

:::warning Leaving a root or graph undisposed

A memo retains its source dependencies, and an effect keeps responding to changes, until its owner
is disposed. Dispose each `createRoot` owner when its work ends. Dispose the graph with
`use graph = new Graph ()` or `graph.Dispose ()`.
:::

Register subscriptions and other resources with [Cleanup](cleanup.md).

## Performance

Instruction counts for creating and disposing rows, compared with other libraries and followed across commits, are on
[Benchmarks → Lifetimes](../benchmarks/lifetimes.md#ci-instruction-counts).
