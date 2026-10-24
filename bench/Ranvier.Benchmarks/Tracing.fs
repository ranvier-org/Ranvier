module Ranvier.Benchmarks.Tracing

open System
open BenchmarkDotNet.Attributes
open Ranvier

/// <summary>Writes between rebuilds of a <c>Rig</c>'s graph, which bounds a traced graph's log.</summary>
[<Literal>]
let private Period = 4096

/// <summary>
/// A graph built by <c>build</c>, which returns the graph's write. The graph is disposed and rebuilt every
/// <c>Period</c> writes, in traced and untraced builds alike.
/// </summary>
type private Rig(build: Graph -> (unit -> unit)) =
    let mutable graph = Unchecked.defaultof<Graph>
    let mutable write = ignore
    let mutable remaining = 0

    member _.Rebuild() =
        if not (isNull (box graph)) then
            (graph :> IDisposable).Dispose()

        graph <- new Graph ()
        write <- build graph
        remaining <- Period

    /// <summary>Runs the write, counting <c>writes</c> signal writes against the rebuild period.</summary>
    member this.Step(writes: int) =
        if remaining <= 0 then
            this.Rebuild ()

        remaining <- remaining - writes
        write ()

    member _.Dispose() =
        if not (isNull (box graph)) then
            (graph :> IDisposable).Dispose()

/// <summary>Written by every reaction, so no reaction's result is provably unused.</summary>
let mutable private sink = 0

let private signalWorkload (graph: Graph) =
    let source = Signal (graph, 0)

    new Effect (graph, (fun () -> sink <- sink + source.Value))
    |> ignore

    fun () -> source.Value <- source.Peek + 1

let private memoWorkload (graph: Graph) =
    let source = Signal (graph, 0)
    let doubled = Memo (graph, (fun _ -> source.Value * 2))

    new Effect (graph, (fun () -> sink <- sink + doubled.Value))
    |> ignore

    fun () -> source.Value <- source.Peek + 1

let private chainWorkload (graph: Graph) =
    let source = Signal (graph, 0)
    let mutable last: unit -> int = fun () -> source.Value

    for _ in 1..4 do
        let previous = last
        let memo = Memo (graph, (fun _ -> previous () + 1))
        last <- fun () -> memo.Value

    let tail = last

    new Effect (graph, (fun () -> sink <- sink + tail ()))
    |> ignore

    fun () -> source.Value <- source.Peek + 1

let private fanoutWorkload (graph: Graph) =
    let source = Signal (graph, 0)
    let memos = Array.init 64 (fun i -> Memo (graph, (fun _ -> source.Value + i)))

    new Effect (
        graph,
        fun () ->
            for memo in memos do
                sink <- sink + memo.Value
    )
    |> ignore

    fun () -> source.Value <- source.Peek + 1

let private cutoffWorkload (graph: Graph) =
    let source = Signal (graph, 0)
    let sign = Memo (graph, (fun _ -> source.Value >>> 31))

    new Effect (graph, (fun () -> sink <- sink + sign.Value))
    |> ignore

    fun () -> source.Value <- source.Peek + 1

let private batchWorkload (graph: Graph) =
    let source = Signal (graph, 0)

    new Effect (graph, (fun () -> sink <- sink + source.Value))
    |> ignore

    fun () ->
        graph.Batch (fun () ->
            for _ in 1..10 do
                source.Value <- source.Peek + 1)

/// <summary>
/// Updates on a built graph, the same source in traced and untraced builds. Each graph is rebuilt every
/// 4,096 writes, so a traced log holds at most that many writes' events.
/// </summary>
[<MemoryDiagnoser; BenchmarkCategory "Trace"; Config.TracedConfig>]
type TraceCostBenchmarks() =
    let signal = Rig signalWorkload
    let memo = Rig memoWorkload
    let chain = Rig chainWorkload
    let fanout = Rig fanoutWorkload
    let cutoff = Rig cutoffWorkload
    let batch = Rig batchWorkload
    let rigs = [ signal; memo; chain; fanout; cutoff; batch ]

    [<GlobalSetup>]
    member _.Setup() =
        for rig in rigs do
            rig.Rebuild ()

    [<GlobalCleanup>]
    member _.Cleanup() =
        for rig in rigs do
            rig.Dispose ()

    /// <summary>One signal write observed by one effect.</summary>
    [<Benchmark>]
    member _.Signal() =
        signal.Step 1

    /// <summary>One signal write read by a memo and an effect.</summary>
    [<Benchmark>]
    member _.Memo() =
        memo.Step 1

    /// <summary>One write to the head of a 4-deep memo chain, observed at the tail by an effect.</summary>
    [<Benchmark>]
    member _.Chain() =
        chain.Step 1

    /// <summary>One signal write read by 64 memos, all read by one effect.</summary>
    [<Benchmark>]
    member _.Fanout() =
        fanout.Step 1

    /// <summary>One signal write whose memo recomputes to an equal value, so the observing effect stays clean.</summary>
    [<Benchmark>]
    member _.Cutoff() =
        cutoff.Step 1

    /// <summary>Ten writes to one effect-observed signal inside one <c>Batch</c>.</summary>
    [<Benchmark>]
    member _.Batch() =
        batch.Step 10
