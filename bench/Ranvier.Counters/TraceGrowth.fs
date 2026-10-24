/// <summary>
/// Trace history growth under sustained updates on one graph, compiled into both the .NET counter bench and its Fable
/// harness, so both targets run identical graphs.
/// </summary>
module CounterBench.TraceGrowth

/// <summary>A workload's graph after <c>Updates</c> signal writes.</summary>
type Checkpoint =
    {
        Workload: string
        Updates: int
        /// <summary>Events in the graph's log; 0 in an untraced build.</summary>
        Events: int
        /// <summary>Heap bytes after a full collection, minus the heap before the graph was built.</summary>
        RetainedBytes: float
        /// <summary>Milliseconds to copy the full log with <c>Trace.events</c>.</summary>
        EventsMs: float
        /// <summary>Milliseconds to fold the full log with <c>Trace.snapshot</c>.</summary>
        SnapshotMs: float
        /// <summary>Milliseconds to write the full log with <c>Trace.dumpText</c>.</summary>
        DumpTextMs: float
        /// <summary>
        /// Milliseconds to read the events of the last <c>TailEvents</c> writes as the signal maps read them: a copy
        /// of the full log, sliced from the previous read's length.
        /// </summary>
        TailReadMs: float
        /// <summary>Events in the tail read.</summary>
        TailEvents: int
    }

/// <summary>A workload's growth across its checkpoints.</summary>
type GrowthSummary =
    {
        Workload: string
        Checkpoints: int
        /// <summary>Events per update at the checkpoint with the most updates, graph construction included.</summary>
        EventsPerUpdate: float
        /// <summary>Retained bytes per event at the margin: <c>RetainedSlope / EventSlope</c>, or 0 with no events.</summary>
        BytesPerEvent: float
        /// <summary>The least-squares slope of events against updates.</summary>
        EventSlope: float
        /// <summary>The least-squares slope of retained bytes against updates.</summary>
        RetainedSlope: float
    }

/// <summary>The least-squares slope of <c>ys</c> against <c>xs</c>; <c>xs</c> holds at least two distinct values.</summary>
let private slope (xs: float[]) (ys: float[]) =
    let mx = Array.average xs
    let my = Array.average ys
    let mutable covariance = 0.
    let mutable variance = 0.

    for i in 0 .. xs.Length - 1 do
        covariance <- covariance + (xs[i] - mx) * (ys[i] - my)
        variance <- variance + (xs[i] - mx) * (xs[i] - mx)

    covariance / variance

/// <summary>
/// The growth of one workload's <c>checkpoints</c>. An error when they hold fewer than two distinct update counts or
/// more than one workload.
/// </summary>
let summarise (checkpoints: Checkpoint[]) : Result<GrowthSummary, string> =
    let updateCounts =
        checkpoints
        |> Array.map _.Updates
        |> Array.distinct

    let workloads =
        checkpoints
        |> Array.map (fun c -> c.Workload)
        |> Array.distinct

    if updateCounts.Length < 2 then
        Error $"A growth summary needs checkpoints at two update counts or more; got %d{updateCounts.Length}."
    elif workloads.Length > 1 then
        Error (
            "A growth summary covers one workload; got "
            + String.concat ", " workloads
            + "."
        )
    else
        let updates =
            checkpoints
            |> Array.map (fun c -> float c.Updates)

        let eventSlope = slope updates (checkpoints |> Array.map (fun c -> float c.Events))
        let retainedSlope = slope updates (checkpoints |> Array.map _.RetainedBytes)
        let last = checkpoints |> Array.maxBy _.Updates

        Ok
            {
                Workload = workloads[0]
                Checkpoints = checkpoints.Length
                EventsPerUpdate = float last.Events / float last.Updates
                BytesPerEvent = if eventSlope = 0. then 0. else retainedSlope / eventSlope
                EventSlope = eventSlope
                RetainedSlope = retainedSlope
            }
