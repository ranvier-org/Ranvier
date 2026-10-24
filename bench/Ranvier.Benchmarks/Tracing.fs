module Ranvier.Benchmarks.Tracing

open System
open System.Threading
open BenchmarkDotNet.Attributes
open Ranvier

/// <summary>
/// Benchmark invocations per iteration of <c>TraceCostBenchmarks</c>. A traced graph's log will hold at most this
/// many invocations' events.
/// </summary>
[<Literal>]
let private Invocations = 4096

/// <summary>A graph built by <c>build</c>, which returns the graph's write.</summary>
type private Rig(build: Graph -> (unit -> unit)) =
    let mutable graph = Unchecked.defaultof<Graph>
    let mutable write = ignore

    /// <summary>Disposes the current graph, if any, and builds a new one.</summary>
    member this.Rebuild() =
        this.Dispose ()
        graph <- new Graph ()
        write <- build graph

    member _.Step() = write ()

    member _.Dispose() =
        if not (isNull (box graph)) then
            (graph :> IDisposable).Dispose()
            graph <- Unchecked.defaultof<Graph>

    /// <summary>
    /// Runs five rounds of <c>Invocations</c> writes on fresh graphs, each followed by a pause, so the write path
    /// will run as tier-1 code from the first measured iteration.
    /// </summary>
    member this.Warm() =
        for _ in 1..5 do
            this.Rebuild ()

            for _ in 1..Invocations do
                this.Step ()

            this.Dispose ()
            Thread.Sleep 150

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

    let body =
        Func<unit>(fun () ->
            for _ in 1..10 do
                source.Value <- source.Peek + 1)

    fun () -> graph.Batch body

/// <summary>
/// Updates on a built graph, the same source in traced and untraced builds. Every iteration runs 4,096 invocations
/// on graphs rebuilt before it and disposed after it, outside the measurement.
/// </summary>
[<MemoryDiagnoser; BenchmarkCategory "Trace"; Config.TracedConfig; InvocationCount(Invocations, 16)>]
type TraceCostBenchmarks() =
    let signal = Rig signalWorkload
    let memo = Rig memoWorkload
    let chain = Rig chainWorkload
    let fanout = Rig fanoutWorkload
    let cutoff = Rig cutoffWorkload
    let batch = Rig batchWorkload
    let rigs = [ signal; memo; chain; fanout; cutoff; batch ]

    [<GlobalSetup(Target = "Signal")>]
    member _.WarmSignal() =
        signal.Warm ()

    [<GlobalSetup(Target = "Memo")>]
    member _.WarmMemo() =
        memo.Warm ()

    [<GlobalSetup(Target = "Chain")>]
    member _.WarmChain() =
        chain.Warm ()

    [<GlobalSetup(Target = "Fanout")>]
    member _.WarmFanout() =
        fanout.Warm ()

    [<GlobalSetup(Target = "Cutoff")>]
    member _.WarmCutoff() =
        cutoff.Warm ()

    [<GlobalSetup(Target = "Batch")>]
    member _.WarmBatch() =
        batch.Warm ()

    [<IterationSetup>]
    member _.Setup() =
        for rig in rigs do
            rig.Rebuild ()

    [<IterationCleanup>]
    member _.Cleanup() =
        for rig in rigs do
            rig.Dispose ()

    /// <summary>One signal write observed by one effect.</summary>
    [<Benchmark>]
    member _.Signal() =
        signal.Step ()

    /// <summary>One signal write read by a memo and an effect.</summary>
    [<Benchmark>]
    member _.Memo() =
        memo.Step ()

    /// <summary>One write to the head of a 4-deep memo chain, observed at the tail by an effect.</summary>
    [<Benchmark>]
    member _.Chain() =
        chain.Step ()

    /// <summary>One signal write read by 64 memos, all read by one effect.</summary>
    [<Benchmark>]
    member _.Fanout() =
        fanout.Step ()

    /// <summary>One signal write whose memo recomputes to an equal value, so the observing effect stays clean.</summary>
    [<Benchmark>]
    member _.Cutoff() =
        cutoff.Step ()

    /// <summary>Ten writes to one effect-observed signal inside one <c>Batch</c>.</summary>
    [<Benchmark>]
    member _.Batch() =
        batch.Step ()

/// <summary>Rows built by <c>DisposeRows</c>.</summary>
[<Literal>]
let private Rows = 1000

/// <summary>
/// Builds a graph and disposes it, per invocation, so construction and teardown are measured apart from
/// <c>TraceCostBenchmarks</c>' updates. Each construction case builds the graph of the steady-state case of the
/// same name.
/// </summary>
[<MemoryDiagnoser; BenchmarkCategory "Trace"; Config.TracedConfig>]
type TraceConstructionBenchmarks() =
    let construct (build: Graph -> (unit -> unit)) =
        let graph = new Graph ()
        build graph |> ignore
        (graph :> IDisposable).Dispose()

    /// <summary>A graph with one signal observed by one effect.</summary>
    [<Benchmark>]
    member _.Signal() =
        construct signalWorkload

    /// <summary>A graph with one signal read by a memo and an effect.</summary>
    [<Benchmark>]
    member _.Memo() =
        construct memoWorkload

    /// <summary>A graph with a 4-deep memo chain observed at the tail by an effect.</summary>
    [<Benchmark>]
    member _.Chain() =
        construct chainWorkload

    /// <summary>A graph with one signal read by 64 memos, all read by one effect.</summary>
    [<Benchmark>]
    member _.Fanout() =
        construct fanoutWorkload

    /// <summary>A root of 1,000 effects, each reading one shared signal, created and then disposed.</summary>
    [<Benchmark>]
    member _.DisposeRows() =
        let graph = new Graph ()
        let source = Signal (graph, 0)

        let root =
            graph.CreateRoot (fun owner ->
                for _ in 1..Rows do
                    new Effect (graph, (fun () -> sink <- sink + source.Value))
                    |> ignore

                owner)

        root.Dispose ()
        (graph :> IDisposable).Dispose()
