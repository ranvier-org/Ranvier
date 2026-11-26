---
title: Equality
---

`GraphOptions.Equality` sets the default value comparison for signals and memos in the graph.
Individual nodes can override it with a typed comparer.

- `JsIdentityPolicy`, the default, follows JavaScript `===`.
- `StructuralPolicy` uses `EqualityComparer<'T>.Default`.

:::details How the policies compare values on .NET

| Value kind | `JsIdentityPolicy` (default) | `StructuralPolicy` |
| --- | --- | --- |
| Primitives, strings, structs, struct tuples | By value | By value |
| Records, unions, `Some x`, other reference types | By reference: an equal copy propagates | By value: an equal copy is cut off |
| `None` over `None` | Cut off (`None` is `null`) | Cut off |
| `nan` over `nan` | Propagates (IEEE: `nan` equals nothing) | Cut off |
| `0.0` over `-0.0` | Cut off | Cut off |

:::

:::tip Write a new object to propagate a change
Under `JsIdentityPolicy`, mutating a referenced object in place and writing the same reference back
is cut off. Write a new value to propagate a change. For a large array rewritten on every frame, see
[Buffers updated in place](#buffers-updated-in-place).
:::

:::details Compare the policies on .NET

This example counts the effect runs caused by each write, excluding the initial run.

```fsharp
type Point = { X: int; Y: int }

let countWakes (options: GraphOptions) (first: 'T) (second: 'T) =
    use graph = new Graph (options)
    use _ = graph.Activate ()
    let source = createSignal first
    let mutable runs = 0
    createEffect (fun () -> source.Value |> ignore; runs <- runs + 1)
    source.Value <- second
    runs - 1

let structural = { GraphOptions.Default with Equality = StructuralPolicy () }

let equalityRows =
    [ "equal record", countWakes GraphOptions.Default { X = 1; Y = 2 } { X = 1; Y = 2 },
                      countWakes structural { X = 1; Y = 2 } { X = 1; Y = 2 }
      "Some 1 over Some 1", countWakes GraphOptions.Default (Some 1) (Some 1),
                            countWakes structural (Some 1) (Some 1)
      "None over None", countWakes GraphOptions.Default (None: int option) None,
                        countWakes structural (None: int option) None
      "nan over nan", countWakes GraphOptions.Default nan nan, countWakes structural nan nan
      "0.0 over -0.0", countWakes GraphOptions.Default 0.0 -0.0, countWakes structural 0.0 -0.0 ]

for name, js, st in equalityRows do
    printfn "%-20s default wakes: %d, structural wakes: %d" name js st
```

```text
equal record         default wakes: 1, structural wakes: 0
Some 1 over Some 1   default wakes: 1, structural wakes: 0
None over None       default wakes: 0, structural wakes: 0
nan over nan         default wakes: 1, structural wakes: 0
0.0 over -0.0        default wakes: 0, structural wakes: 0
```

:::

:::details Differences between .NET and Fable

The table below shows whether writing an equal but separately built value is cut off. Some value
types, including `DateTime` and `decimal`, compile to objects under Fable, so the default comparison
can differ between targets.

Writing the same object instance back is cut off on both targets.

| Type | Default on .NET | Default under Fable | `StructuralPolicy`, both targets |
| --- | --- | --- | --- |
| `int`, `float`, `string` | Cut off | Cut off | Cut off |
| `nan` | Propagates | Propagates | Cut off on .NET, propagates under Fable |
| `DateTime`, `DateTimeOffset`, `decimal`, `KeyValuePair` | Cut off | Propagates | Cut off |
| Struct records, struct tuples | Cut off | Propagates | Cut off |
| `Some 1` | Propagates | Cut off | Cut off |
| `None` | Cut off | Cut off | Cut off |
| Records, tuples, lists, `Some` of a record | Propagates | Propagates | Cut off |
| Class without custom equality | Propagates | Propagates | Propagates |

:::

## Per-node comparers

Use a `WithComparer` factory to give one node an `IEqualityComparer<'T>`. Other nodes keep the
graph policy. Call these factories inside an active graph, as with their ordinary counterparts.

```fsharp
let name = createSignalWithComparer System.StringComparer.OrdinalIgnoreCase "Ada"
let label =
    createMemoWithComparer System.StringComparer.OrdinalIgnoreCase (fun _ -> name.Value.Trim())

name.Value <- "ADA"
printfn "%s" name.Value // Ada: an equal signal write keeps the stored value
```

A signal rejects an equal write ([`signal_update_noop`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Check.html#signal_update_noop)). A memo caches its newly computed result but suppresses
downstream work when it compares equal ([`cutoff`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Runs.html#cutoff), [`resolve_clean_unmoved`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Check.html#resolve_clean_unmoved); a comparer that returns `false` on every run—such as a memo allocating a fresh reference record under `JsIdentityPolicy` or returning `nan`—always publishes a move: [`cmp_never_passes_source`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.WholeGraph.html#cmp_never_passes_source)). The comparer controls settled-value cutoffs; pending,
failed, waiting and caught-error state changes still propagate.

The opt-in factories are `createSignalWithComparer`, `createMemoWithComparer`,
`createOwningMemoWithComparer`, `createEffectOnWithComparer`, `createSuspenseWithComparer`,
`createErrorBoundaryWithComparer` and `createBoundaryWithComparer`. Their ownership, purity and
previous-value rules match the ordinary factories. Comparers are selected once at construction.
Async sources, async memos, stream memos and collection value cutoffs continue to use the graph policy.

[Bindings](bindings.md) use their source's cutoff. Give the signal or memo a comparer when a
target should ignore equivalent values; the binding has no separate equality policy. Projection
item equality also determines which key-selection work a pass can skip, while keys themselves
always compare structurally; see [Projection identity](projections.fsx#identity).

:::details Invalid comparers and comparer failures
Null comparers are rejected at construction. If a signal's comparer throws, the signal stays
unchanged and the exception reaches its writer. On a computed node, a comparer exception fails
the node through its existing error handling ([`comparer_throw_contained`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Ownership.html#comparer_throw_contained), [`memo_comparer_replaces_failure`](https://ranvier-org.github.io/Ranvier/proofs/Ranvier.Pure.Ownership.html#memo_comparer_replaces_failure)). A boundary's recovery handler handles body errors;
a comparer exception fails the boundary directly.
:::

From C#, see [C# — Per-node equality](csharp.md#per-node-equality). Note that seeded boundaries do not have per-node comparer overloads and always use `GraphOptions.Equality`.

## Buffers updated in place

On .NET a signal compares an array by reference under both policies: `StructuralPolicy` does not
compare array contents. An array rewritten in place and written back is cut off, and a new array
propagates.

For a small array, write a new one. For a large numeric buffer, publish the same array in one of
two forms. `integrate` and `upload` stand for your kernel and your consumer.

**A counter beside the buffer.** Readers track the counter and read the array directly. A reader
that skips `version.Value` is not woken.

```fsharp
let positions = Array.zeroCreate<float32> 1_048_576
let version = createSignal 0

createEffect (fun () ->
    version.Value |> ignore
    upload positions)

// One tick
integrate positions
version.Value <- version.Peek + 1
```

**A comparer that reports every write as a change.** The array travels through the signal, so a
reader gets the tracked read and the data together.

```fsharp
let everyWrite =
    { new System.Collections.Generic.IEqualityComparer<float32[]> with
        member _.Equals(_, _) = false
        member _.GetHashCode _ = 0 }

let positions = createSignalWithComparer everyWrite (Array.zeroCreate<float32> 1_048_576)

createEffect (fun () -> upload positions.Value)

// One tick
integrate positions.Peek
positions.Value <- positions.Peek
```

Both forms follow these rules:

- **A memo that returns the array is cut off.** Its own comparison sees the same reference, so its
  readers stay asleep. Give the memo the comparer with `createMemoWithComparer`, or return a derived
  value such as a sum.
- **Readers see the current contents.** The signal holds one array. A reader sees its contents at
  the time the reader runs, and the previous value a memo receives is that same array.
- **Writes in one `batch` wake a reader once.** A [binding](bindings.md) on the comparer signal
  writes its target once per flush in which the signal was written.
- **Ranvier sees the signal write, not the element writes.** The thread-affinity guard covers the
  write to the signal. Access to the array's elements from several threads is yours to order.

Two arrays written in turn also avoid allocation: each write is a different reference, so the
default comparison propagates it. A full rewrite then reads one array and writes the other, and a
one-element edit first copies the whole array.

### When to publish in place

The timings below were **measured here** with the buffer-handoff benchmark described later on this page.

- **Up to a few kilobytes:** keep writing a new array. At 4 KB it cost about 110 to 170 ns more
  per write than publishing in place.
- **From 85,000 bytes:** publish in place. .NET allocates each new array of that size on the large
  object heap, and at 512 KB a new array per write cost 3.9 times the rewrite alone.
- **A few elements changed per write:** publish in place at any size. Its cost stayed between 37
  and 40 ns from 4 KB to 4 MB, while a new array cost a copy of the whole buffer.

:::details Check the wakes on .NET

This example counts the effect runs caused by one in-place rewrite, excluding the initial run.

```fsharp
let inPlaceRows =
    use graph = new Graph ()
    use _ = graph.Activate ()
    let buffer = Array.zeroCreate<float32> 4

    let plain = createSignal buffer
    let version = createSignal 0
    let marked = createSignalWithComparer everyWrite buffer
    let passed = createMemo (fun _ -> marked.Value)
    let passedOn = createMemoWithComparer everyWrite (fun _ -> marked.Value)
    let total = createMemo (fun _ -> Array.sum marked.Value)

    let runs = Array.zeroCreate<int> 6
    createEffect (fun () -> plain.Value |> ignore; runs[0] <- runs[0] + 1)
    createEffect (fun () -> version.Value |> ignore; runs[1] <- runs[1] + 1)
    createEffect (fun () -> marked.Value |> ignore; runs[2] <- runs[2] + 1)
    createEffect (fun () -> passed.Value |> ignore; runs[3] <- runs[3] + 1)
    createEffect (fun () -> passedOn.Value |> ignore; runs[4] <- runs[4] + 1)
    createEffect (fun () -> total.Value |> ignore; runs[5] <- runs[5] + 1)

    buffer[0] <- 1.0f
    plain.Value <- buffer
    version.Value <- version.Peek + 1
    marked.Value <- buffer

    [ "default signal, same array", runs[0] - 1
      "counter signal", runs[1] - 1
      "comparer signal, same array", runs[2] - 1
      "memo returning the array", runs[3] - 1
      "memo with the comparer", runs[4] - 1
      "memo returning a sum", runs[5] - 1 ]

for name, wakes in inPlaceRows do
    printfn "%-28s wakes: %d" name wakes
```

```text
default signal, same array   wakes: 0
counter signal               wakes: 1
comparer signal, same array  wakes: 1
memo returning the array     wakes: 0
memo with the comparer       wakes: 1
memo returning a sum         wakes: 1
```

:::

:::details Buffer handoff on .NET (measured here)

One tick rewrites a `float32` array, publishes it and runs one effect that reads two elements.
**No graph** is the rewrite and the read alone. Times are per tick.

Every element rewritten, one `Vector<float32>` at a time:

| Elements | Array size | No graph | New array | Two arrays | Counter | Comparer |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1,024 | 4 KB | 85 ns | 216 ns | 112 ns | 109 ns | 108 ns |
| 16,384 | 64 KB | 1.42 µs | 2.74 µs | 1.40 µs | 1.50 µs | 1.49 µs |
| 131,072 | 512 KB | 10.9 µs | 42.8 µs | 11.8 µs | 10.8 µs | 11.0 µs |
| 1,048,576 | 4 MB | 87.1 µs | 478.3 µs | 99.6 µs | 88.1 µs | 89.9 µs |

One element rewritten:

| Elements | Array size | No graph | New array | Two arrays | Counter | Comparer |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 1,024 | 4 KB | 5.5 ns | 208 ns | 83.5 ns | 37.6 ns | 37.0 ns |
| 16,384 | 64 KB | 5.5 ns | 1.61 µs | 697 ns | 38.2 ns | 37.3 ns |
| 131,072 | 512 KB | 5.8 ns | 32.6 µs | 7.30 µs | 37.2 ns | 40.3 ns |
| 1,048,576 | 4 MB | 5.8 ns | 403.2 µs | 179.6 µs | 39.6 ns | 37.4 ns |

A new array per tick allocated the array's size on every tick. With every element rewritten it
triggered 102 gen2 collections per 1,000 ticks at 512 KB and 199 at 4 MB. The other forms
allocated nothing.

Recorded 2026-11-19 on .NET 10.0.15, x64, an AMD Ryzen 9 9900X, with the workstation concurrent
collector. Each figure is the middle of three processes, and each process reports the median of 25
batches of 25 ms. Times are wall time on the calling thread, and other work was running on the
machine during the recording. Source: `experiments/buffer-handoff-bench` against the core at commit
`404f358`. Compare the columns with each other; the absolute times do not predict your
application's performance.

:::

## Graph-wide policies

`GraphOptions.Default.WithEquality(policy)` returns options for constructing a graph with that policy.
In a split memo, the graph policy compares settled input keys. Pass an explicit output comparer to
`createMemoOnWithComparer` to control which results reach readers. See
[Memos](memos.md#compute-from-a-settled-key).

:::details Define a custom equality policy

Pass an `IEqualityPolicy` as `GraphOptions.Equality`. Its `Comparer<'T>` supplies the comparer for
each value type and is called once when a node using the graph policy is created. A per-node
comparer overrides it for the node.

For example, this graph treats strings that differ only in case as equal:

```fsharp
type CaseInsensitivePolicy() =
    interface IEqualityPolicy with
        member _.Comparer<'T>() =
            { new System.Collections.Generic.IEqualityComparer<'T> with
                member _.Equals(a, b) =
                    System.String.Equals (string (box a), string (box b), System.StringComparison.OrdinalIgnoreCase)
                member _.GetHashCode a = (string (box a)).ToLowerInvariant().GetHashCode () }

let caseInsensitiveWakes =
    countWakes { GraphOptions.Default with Equality = CaseInsensitivePolicy () } "abc" "ABC"

printfn "case-insensitive wakes: %d" caseInsensitiveWakes
```

```text
case-insensitive wakes: 0
```

:::
