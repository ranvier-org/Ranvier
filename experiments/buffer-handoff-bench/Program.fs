// Times handing a float32 buffer from a producer to one Ranvier effect.
//
// One tick: the kernel updates the buffer, the strategy publishes it, and the effect reads the first and last element.
//
//   bare     the kernel in place and the read, with no graph
//   fresh    a new array per tick, written to a signal
//   swap     two arrays written to a signal in turn
//   version  one array updated in place, beside a counter signal the effect reads
//   always   one array updated in place, rewritten to a signal whose comparer reports every write as a change
//
// Workloads: `dense` rewrites every element (x * 0.5 + 1, one Vector<float32> at a time); `sparse` rewrites one.
//
// The strategies are visited in rotating order. A visit is a blocking collection, untimed batches while the heap
// settles, then timed batches back to back: the collections a strategy causes fall inside its own timed batches.
// `bare`, `version` and `always` update one shared array, so they differ in the hand-off alone.
//
// Times are wall time on the calling thread. Background collector threads run on other cores.
//
// Run from the repository root: dotnet run -c Release --project experiments/buffer-handoff-bench [-- --quick]
//
// results-run1.txt to results-run3.txt hold the output of three such runs, each in its own process. summarise.awk
// reports the lowest, middle and highest of their per-run medians; docs/content/guide/equality.md quotes the middle.
module Program

open System
open System.Collections.Generic
open System.Diagnostics
open System.Globalization
open System.Numerics
open System.Runtime
open System.Runtime.CompilerServices
open System.Runtime.InteropServices
open System.Threading
open Ranvier

type NeverEqual<'T>() =
    interface IEqualityComparer<'T> with
        member _.Equals(_: 'T, _: 'T) = false
        member _.GetHashCode(_: 'T) = 0

[<Sealed>]
type Reader() =
    let mutable sink = 0.0
    let mutable runs = 0L
    member _.Runs = runs
    member _.Sink = sink

    member _.Read(a: float32[]) =
        sink <- sink + float a[0] + float a[a.Length - 1]
        runs <- runs + 1L

type Workload =
    | Dense
    | Sparse

    member this.Name =
        match this with
        | Dense -> "dense"
        | Sparse -> "sparse"

module Kernel =
    // One compiled body, called by every strategy: an inlined copy per caller ran at two speeds on one array.
    [<MethodImpl(MethodImplOptions.NoInlining)>]
    let dense (src: float32[]) (dst: float32[]) =
        let width = Vector<float32>.Count
        let half = Vector<float32> 0.5f
        let one = Vector<float32> 1.0f
        let mutable i = 0

        while i <= src.Length - width do
            (Vector<float32>(src, i) * half + one).CopyTo(dst, i)
            i <- i + width

        while i < src.Length do
            dst[i] <- src[i] * 0.5f + 1.0f
            i <- i + 1

    // Visits every index of a power-of-two length, 7919 elements apart.
    [<Sealed>]
    type Cursor(n: int) =
        let mutable k = 0

        member _.Poke(buf: float32[]) =
            buf[k] <- buf[k] * 0.5f + 1.0f
            k <- (k + 7919) &&& (n - 1)

    let inPlace (workload: Workload) (n: int) : float32[] -> unit =
        match workload with
        | Dense -> fun buf -> dense buf buf
        | Sparse ->
            let cursor = Cursor n
            fun buf -> cursor.Poke buf

    let into (workload: Workload) (n: int) : float32[] -> float32[] -> unit =
        match workload with
        | Dense -> fun src dst -> dense src dst
        | Sparse ->
            let cursor = Cursor n

            fun src dst ->
                Array.Copy (src, dst, n)
                cursor.Poke dst

    let fresh (workload: Workload) (n: int) : float32[] -> float32[] =
        match workload with
        | Dense ->
            fun src ->
                let dst = Array.zeroCreate<float32> n
                dense src dst
                dst
        | Sparse ->
            let cursor = Cursor n

            fun src ->
                let dst = Array.copy src
                cursor.Poke dst
                dst

type Strategy =
    {
        Name: string
        Tick: unit -> unit
        Wakes: unit -> int64
        Latest: unit -> float32[]
        Dispose: unit -> unit
    }

let private dispose (graph: Graph) () =
    (graph :> IDisposable).Dispose()

// `inPlaceBuffer` supplies the array of each in-place strategy: `bare`, `version` and `always`.
let build (workload: Workload) (n: int) (inPlaceBuffer: unit -> float32[]) : Strategy[] =
    if n &&& (n - 1) <> 0 then
        invalidArg (nameof n) "The length must be a power of two."

    let bare =
        let step = Kernel.inPlace workload n
        let buf = inPlaceBuffer ()
        let reader = Reader ()

        {
            Name = "bare"
            Tick =
                (fun () ->
                    step buf
                    reader.Read buf)
            Wakes = (fun () -> reader.Runs)
            Latest = (fun () -> buf)
            Dispose = ignore
        }

    let fresh =
        let next = Kernel.fresh workload n
        let graph = new Graph ()
        let reader = Reader ()

        let signal =
            graph.Run (fun () ->
                let signal = createSignal (Array.zeroCreate<float32> n)
                createEffect (fun () -> reader.Read signal.Value)
                signal)

        {
            Name = "fresh"
            Tick = (fun () -> signal.Value <- next signal.Peek)
            Wakes = (fun () -> reader.Runs)
            Latest = (fun () -> signal.Peek)
            Dispose = dispose graph
        }

    let swap =
        let step = Kernel.into workload n
        let graph = new Graph ()
        let reader = Reader ()
        let back = ref (Array.zeroCreate<float32> n)

        let signal =
            graph.Run (fun () ->
                let signal = createSignal (Array.zeroCreate<float32> n)
                createEffect (fun () -> reader.Read signal.Value)
                signal)

        {
            Name = "swap"
            Tick =
                (fun () ->
                    let front = signal.Peek
                    let next = back.Value
                    step front next
                    signal.Value <- next
                    back.Value <- front)
            Wakes = (fun () -> reader.Runs)
            Latest = (fun () -> signal.Peek)
            Dispose = dispose graph
        }

    let version =
        let step = Kernel.inPlace workload n
        let graph = new Graph ()
        let reader = Reader ()
        let buf = inPlaceBuffer ()

        let counter =
            graph.Run (fun () ->
                let counter = createSignal 0

                createEffect (fun () ->
                    counter.Value |> ignore
                    reader.Read buf)

                counter)

        {
            Name = "version"
            Tick =
                (fun () ->
                    step buf
                    counter.Value <- counter.Peek + 1)
            Wakes = (fun () -> reader.Runs)
            Latest = (fun () -> buf)
            Dispose = dispose graph
        }

    let always =
        let step = Kernel.inPlace workload n
        let graph = new Graph ()
        let reader = Reader ()
        let buf = inPlaceBuffer ()

        let signal =
            graph.Run (fun () ->
                let comparer = NeverEqual<float32[]>() :> IEqualityComparer<float32[]>
                let signal = createSignalWithComparer comparer buf
                createEffect (fun () -> reader.Read signal.Value)
                signal)

        {
            Name = "always"
            Tick =
                (fun () ->
                    step buf
                    signal.Value <- buf)
            Wakes = (fun () -> reader.Runs)
            Latest = (fun () -> buf)
            Dispose = dispose graph
        }

    [| bare; fresh; swap; version; always |]

[<Struct>]
type Sample =
    {
        Ns: float
        Bytes: float
        Gen0: int
        Gen1: int
        Gen2: int
        Ticks: int
    }

type Shape =
    {
        Visits: int
        Settle: int
        Timed: int
        BatchMs: float
        WarmMs: float
    }

let private toNs (stopwatchTicks: int64) =
    float stopwatchTicks * 1e9
    / float Stopwatch.Frequency

let private median (values: float seq) =
    let sorted = values |> Seq.sort |> Seq.toArray
    let mid = sorted.Length / 2

    if sorted.Length % 2 = 1 then
        sorted[mid]
    else
        (sorted[mid - 1] + sorted[mid]) / 2.0

// Runs the strategy for the budget in growing batches. Returns the time per tick of the last batch, in nanoseconds.
let warm (strategy: Strategy) (budgetMs: float) : float =
    let tick = strategy.Tick
    let mutable ticks = 16
    let mutable spent = 0L
    let mutable last = 0.0

    while toNs spent < budgetMs * 1e6 do
        let t0 = Stopwatch.GetTimestamp ()

        for _ in 1..ticks do
            tick ()

        let elapsed = Stopwatch.GetTimestamp () - t0
        spent <- spent + elapsed
        last <- toNs elapsed / float ticks

        if toNs elapsed < 5e6 && ticks < (1 <<< 28) then
            ticks <- ticks * 2

    last

let timedBatch (strategy: Strategy) (ticks: int) : Sample =
    let tick = strategy.Tick
    let wakes = strategy.Wakes ()
    let c0 = GC.CollectionCount 0
    let c1 = GC.CollectionCount 1
    let c2 = GC.CollectionCount 2
    let bytes = GC.GetAllocatedBytesForCurrentThread ()
    let t0 = Stopwatch.GetTimestamp ()

    for _ in 1..ticks do
        tick ()

    let elapsed = Stopwatch.GetTimestamp () - t0
    let allocated = GC.GetAllocatedBytesForCurrentThread () - bytes
    // `CollectionCount g` includes the collections of every older generation.
    let d0 = GC.CollectionCount 0 - c0
    let d1 = GC.CollectionCount 1 - c1
    let d2 = GC.CollectionCount 2 - c2
    let woke = strategy.Wakes () - wakes

    if woke <> int64 ticks then
        failwithf "%s: %d ticks woke the reader %d times." strategy.Name ticks woke

    {
        Ns = toNs elapsed / float ticks
        Bytes = float allocated / float ticks
        Gen0 = d0 - d1
        Gen1 = d1 - d2
        Gen2 = d2
        Ticks = ticks
    }

let visit (strategy: Strategy) (ticks: int) (shape: Shape) (samples: ResizeArray<Sample>) =
    GC.Collect ()
    GC.WaitForPendingFinalizers ()
    GC.Collect ()

    let tick = strategy.Tick

    for _ in 1 .. shape.Settle * ticks do
        tick ()

    for _ in 1 .. shape.Timed do
        samples.Add (timedBatch strategy ticks)

let private count (n: int) =
    n.ToString ("N0", CultureInfo.InvariantCulture)

let measure (workload: Workload) (n: int) (shape: Shape) =
    let shared = Array.zeroCreate<float32> n
    let strategies = build workload n (fun () -> shared)

    for strategy in strategies do
        warm strategy shape.WarmMs |> ignore

    // Time for the background compiler to install the optimised methods.
    Thread.Sleep 200

    let ticks =
        strategies
        |> Array.map (fun strategy ->
            let ns = warm strategy (shape.WarmMs / 2.0)
            max 16 (int (min 4e6 (shape.BatchMs * 1e6 / ns))))

    let samples =
        strategies
        |> Array.map (fun _ -> ResizeArray<Sample>())

    for round in 0 .. shape.Visits - 1 do
        for offset in 0 .. strategies.Length - 1 do
            let i = (round + offset) % strategies.Length
            visit strategies[i] ticks[i] shape samples[i]

    let medians =
        samples
        |> Array.map (fun runs -> runs |> Seq.map (fun s -> s.Ns) |> median)

    let floor = medians[0]

    printfn ""

    printfn
        "workload=%s  N=%s  buffer=%s KB  samples=%d  batch=%.0f ms"
        workload.Name
        (count n)
        (count (n * 4 / 1024))
        (shape.Visits * shape.Timed)
        shape.BatchMs

    printfn
        "%-8s %13s %13s %13s %8s %13s %9s %9s %9s %11s"
        "strategy"
        "ns/tick med"
        "ns/tick min"
        "over bare ns"
        "x bare"
        "B/tick"
        "gen0/1k"
        "gen1/1k"
        "gen2/1k"
        "ticks/batch"

    for i in 0 .. strategies.Length - 1 do
        let runs = samples[i]
        let total = runs |> Seq.sumBy (fun s -> float s.Ticks)

        let per (pick: Sample -> int) =
            (runs |> Seq.sumBy (fun s -> float (pick s)))
            * 1000.0
            / total

        printfn
            "%-8s %13.1f %13.1f %13.1f %8.2f %13.1f %9.3f %9.3f %9.3f %11d"
            strategies[i].Name
            medians[i]
            (runs |> Seq.map (fun s -> s.Ns) |> Seq.min)
            (medians[i] - floor)
            (medians[i] / floor)
            (runs |> Seq.map (fun s -> s.Bytes) |> median)
            (per (fun s -> s.Gen0))
            (per (fun s -> s.Gen1))
            (per (fun s -> s.Gen2))
            ticks[i]

    for strategy in strategies do
        strategy.Dispose ()

    GC.Collect ()

// The premise: a default signal holding an array compares by reference, so the same array rewritten wakes no reader.
let premise () =
    use graph = new Graph ()
    let reader = Reader ()
    let buf = Array.zeroCreate<float32> 16

    let signal =
        graph.Run (fun () ->
            let signal = createSignal buf
            createEffect (fun () -> reader.Read signal.Value)
            signal)

    let before = reader.Runs
    buf[0] <- 1.0f
    signal.Value <- buf
    printfn "premise: one array changed in place and rewritten to a default signal woke the reader %d time(s)" (reader.Runs - before)

let verify () =
    for workload in [ Dense; Sparse ] do
        let strategies = build workload 64 (fun () -> Array.zeroCreate<float32> 64)

        for strategy in strategies do
            for _ in 1..1000 do
                strategy.Tick ()

        let expected = strategies[0].Latest()

        for strategy in strategies do
            if strategy.Latest () <> expected then
                failwithf "%s/%s differs from bare after 1,000 ticks." workload.Name strategy.Name

            if strategy.Wakes () < 1000L then
                failwithf "%s/%s woke the reader %d times in 1,000 ticks." workload.Name strategy.Name (strategy.Wakes ())

            strategy.Dispose ()

    printfn "verified: the five strategies hold equal buffers after 1,000 ticks, dense and sparse, N=64"

let header (shape: Shape) =
    let env (name: string) =
        match Environment.GetEnvironmentVariable name with
        | null
        | "" -> "unset"
        | value -> value

    printfn
        "buffer hand-off: %s, %O, %d logical processors"
        RuntimeInformation.FrameworkDescription
        RuntimeInformation.ProcessArchitecture
        Environment.ProcessorCount

    printfn
        "server GC %b, latency mode %O, Vector<float32>.Count %d, hardware accelerated %b"
        GCSettings.IsServerGC
        GCSettings.LatencyMode
        Vector<float32>.Count
        Vector.IsHardwareAccelerated

    printfn
        "DOTNET_TieredCompilation %s, DOTNET_TieredPGO %s, DOTNET_gcConcurrent %s"
        (env "DOTNET_TieredCompilation")
        (env "DOTNET_TieredPGO")
        (env "DOTNET_gcConcurrent")

    printfn "per strategy: %d visits of %d untimed and %d timed batches, %.0f ms a batch" shape.Visits shape.Settle shape.Timed shape.BatchMs

[<EntryPoint>]
let main argv =
    let shape =
        if argv |> Array.contains "--quick" then
            {
                Visits = 2
                Settle = 1
                Timed = 2
                BatchMs = 5.0
                WarmMs = 50.0
            }
        else
            {
                Visits = 5
                Settle = 3
                Timed = 5
                BatchMs = 25.0
                WarmMs = 250.0
            }

    header shape
    premise ()
    verify ()

    for workload in [ Dense; Sparse ] do
        for n in [ 1024; 16384; 131072; 1048576 ] do
            measure workload n shape

    0
