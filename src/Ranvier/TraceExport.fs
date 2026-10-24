namespace Ranvier

open System.Collections.Generic
open System.Threading.Tasks

/// <summary>One unit of an exported trace stream.</summary>
[<RequireQualifiedAccess>]
type TraceExportRecord =
    /// <summary>The dump header line and snapshot line the stream starts from.</summary>
    | Header of text: string
    /// <summary>The schema 3 dump lines of the events with seqs <c>from</c> to <c>to</c>, each ending in <c>\n</c>.</summary>
    | Events of from: int * ``to``: int * text: string
    /// <summary>The events with seqs <c>from</c> to <c>to</c> are absent from the stream.</summary>
    | Gap of from: int * ``to``: int

/// <summary>The JSONL text of export records.</summary>
[<RequireQualifiedAccess>]
module TraceExportRecord =
    /// <summary>
    /// The lines of <c>record</c>: a header's or a batch's text, and for a gap the line
    /// <c>{"gap":{"from":F,"to":T}}</c>. Each line ends in <c>\n</c>.
    /// </summary>
    let text (record: TraceExportRecord) : string =
        match record with
        | TraceExportRecord.Header text
        | TraceExportRecord.Events (_, _, text) -> text
        | TraceExportRecord.Gap (from, ``to``) ->
            "{\"gap\":{\"from\":"
            + string from
            + ",\"to\":"
            + string ``to``
            + "}}\n"

/// <summary>The destination of an exported trace stream.</summary>
/// <remarks>The exporter calls <c>Write</c> for one record at a time, in stream order.</remarks>
type ITraceSink =
    /// <summary>The sink's name; a checkpoint the exporter takes carries it.</summary>
    abstract Name: string
    /// <summary>Stores <c>record</c>. The returned task completes when the sink holds it.</summary>
    abstract Write: record: TraceExportRecord -> Task

/// <summary>What an exporter does with a batch when its queue is full.</summary>
[<RequireQualifiedAccess>]
type TraceExportFull =
    /// <summary>Leaves the batch in the trace log and queues it when the queue has room. The stream loses no event.</summary>
    | Wait
    /// <summary>Drops the batch and records its seqs in a <c>Gap</c>.</summary>
    | Drop

/// <summary>When an exporter checkpoints the graph's log at the events it has exported.</summary>
[<RequireQualifiedAccess>]
type TraceExportCheckpoint =
    | Never
    /// <summary>Once a batch is queued or dropped. The log stays bounded under a stalled sink.</summary>
    | Enqueued
    /// <summary>Once the sink completes a batch's <c>Write</c>.</summary>
    | Acknowledged

/// <summary>How an exporter queues, encodes and checkpoints.</summary>
type SinkPolicy =
    {
        /// <summary>The events the queue holds, queued or being written. A batch is split to fit.</summary>
        Capacity: int
        Full: TraceExportFull
        Checkpoint: TraceExportCheckpoint
        /// <summary>The codecs that encode the payloads of the exported lines.</summary>
        Codecs: TraceCodecs
    }

/// <summary>
/// The bounded queue between an exporter and its sink: records wait in order, and one at a time is written to the
/// sink. Its capacity counts events.
/// </summary>
/// <remarks>
/// <c>TryWrite</c> runs on the graph thread; a write's completion may run on any thread. Once the sink faults, the
/// queue discards every record.
/// </remarks>
[<Sealed>]
type internal TraceExportQueue(sink: ITraceSink, capacity: int) =
    let gate = obj ()
    let items = Queue<struct (TraceExportRecord * int)>()
    let mutable held = 0
    let mutable writing = false
    let mutable fault: exn = null
    let mutable freed: unit -> unit = ignore

    // Records the outcome of the head record's write, and returns whether the queue may write the next one.
    let settle (outcome: Platform.FlightOutcome<unit>) =
        lock gate (fun () ->
            let struct (_, count) = items.Dequeue ()
            held <- held - count
            writing <- false

            match outcome with
            | Platform.FlightOutcome.Completed () -> ()
            | Platform.FlightOutcome.Faulted error
            | Platform.FlightOutcome.Canceled error ->
                fault <- error
                items.Clear ()
                held <- 0)

    // Takes the head record for writing, or none while a write is in flight, the queue is empty or the sink faulted.
    let take () =
        lock gate (fun () ->
            if writing || items.Count = 0 || not (isNull fault) then
                ValueNone
            else
                writing <- true
                ValueSome (items.Peek ()))

    /// <summary>The events queued or being written.</summary>
    member _.Held = lock gate (fun () -> held)

    /// <summary>True when no record is queued or being written.</summary>
    member _.Idle = lock gate (fun () -> items.Count = 0)

    /// <summary>The error the sink's write raised or faulted with, or null.</summary>
    member _.Fault = lock gate (fun () -> fault)

    /// <summary>Runs after each completed write, on the thread that completed it.</summary>
    member _.Freed
        with set (callback: unit -> unit) = freed <- callback

    /// <summary>
    /// Queues <c>record</c>, which holds <c>events</c> events, and returns true; returns false when the queue lacks
    /// room for them. A record of no events always fits, and one of more events than the capacity never does.
    /// </summary>
    member this.TryWrite(record: TraceExportRecord, events: int) : bool =
        let queued =
            lock gate (fun () ->
                if not (isNull fault) then
                    true
                elif events > 0 && held + events > capacity then
                    false
                else
                    items.Enqueue (struct (record, events))
                    held <- held + events
                    true)

        if queued then
            this.Pump ()

        queued

    member private this.Pump() =
        let mutable go = true

        while go do
            match take () with
            | ValueNone -> go <- false
            | ValueSome (struct (record, _)) ->
                let written =
                    try
                        sink.Write record
                    with error ->
                        settle (Platform.FlightOutcome.Faulted error)
                        freed ()
                        null

                if not (isNull written) then
#if FABLE_COMPILER
                    go <- false

                    Platform.whenDone written (fun outcome ->
                        settle outcome
                        freed ()
                        this.Pump ())
#else
                    if written.IsCompleted then
                        settle (
                            if written.IsFaulted then
                                Platform.FlightOutcome.Faulted (written.Exception.GetBaseException ())
                            elif written.IsCanceled then
                                Platform.FlightOutcome.Canceled (TaskCanceledException written)
                            else
                                Platform.FlightOutcome.Completed ()
                        )

                        freed ()
                    else
                        go <- false

                        Platform.whenDone written (fun outcome ->
                            settle outcome
                            freed ()
                            this.Pump ())
#endif

/// <summary>A sink that holds its records in memory, on .NET and Fable.</summary>
/// <remarks><c>Stall</c> holds back each write's completion until <c>Release</c>.</remarks>
[<Sealed>]
type MemoryTraceSink(name: string) =
    let gate = obj ()
    let records = ResizeArray<TraceExportRecord>()
    let pending = Queue<struct (TraceExportRecord * Platform.Completion<unit>)>()
    let mutable stalled = false

    /// <summary>The records the sink holds, in write order.</summary>
    member _.Records: TraceExportRecord list = lock gate (fun () -> List.ofSeq records)

    /// <summary>The concatenated <c>TraceExportRecord.text</c> of the records the sink holds.</summary>
    member this.Text =
        this.Records
        |> List.map TraceExportRecord.text
        |> String.concat ""

    /// <summary>Holds back the completion of later writes until <c>Release</c>.</summary>
    member _.Stall() =
        lock gate (fun () -> stalled <- true)

    /// <summary>Completes the held-back writes in order, and the later writes at once.</summary>
    member _.Release() =
        let mutable go = true
        lock gate (fun () -> stalled <- false)

        while go do
            let next =
                lock gate (fun () ->
                    if pending.Count = 0 then
                        ValueNone
                    else
                        let struct (record, completion) = pending.Dequeue ()
                        records.Add record
                        ValueSome completion)

            match next with
            | ValueSome completion -> completion.Resolve ()
            | ValueNone -> go <- false

    interface ITraceSink with
        member _.Name = name

        member _.Write(record: TraceExportRecord) : Task =
            lock gate (fun () ->
                if stalled then
                    let completion = Platform.completion<unit>()
                    pending.Enqueue (struct (record, completion))
                    completion.Task :> Task
                else
                    records.Add record
                    Platform.completedTask)

/// <summary>Reference trace sinks.</summary>
[<RequireQualifiedAccess>]
module TraceSinks =
    /// <summary>An empty <c>MemoryTraceSink</c> named <c>memory</c>.</summary>
    let memory () : MemoryTraceSink =
        MemoryTraceSink "memory"

/// <summary>The checks of the trace operations that run between flushes on the graph's thread.</summary>
module internal TraceGate =
    /// <summary>The log of <c>graph</c>.</summary>
    let logOf (graph: Graph) =
        ((box graph) :?> ITraced).TraceLog

    /// <summary>Raises unless the caller is on <c>graph</c>'s thread with no flush, discharge or run in progress.</summary>
    /// <exception cref="T:System.InvalidOperationException">Off the graph's thread, or inside a flush, a discharge or a run.</exception>
    let check (graph: Graph) (operation: string) =
        if graph.Options.ThreadAffinity = Serialised then
            graph.AssertOnGraphThread operation
        elif not graph.IsOnGraphThread then
            raise (
                System.InvalidOperationException (
                    operation
                    + " ran off the graph's thread. Marshal it through Graph.Dispatch."
                )
            )

        if Platform.isNull (box graph.CurrentComputation) then
            Tracer.AbandonStale (logOf graph)

        if (logOf graph).Busy then
            raise (
                System.InvalidOperationException (
                    operation
                    + " ran inside a flush, a discharge or a computation's run. Call it between flushes."
                )
            )

/// <summary>
/// A running export of a graph's trace log to a sink: after each flush, on the graph's thread, the events since the
/// last export are encoded as schema 3 dump lines and queued for the sink.
/// </summary>
/// <remarks>
/// The stream starts with a <c>Header</c> of the checkpoint at <c>Trace.export</c> time. <c>Dispose</c> ends the
/// stream at the last event recorded before it. A sink's failure or a strict codec's
/// <c>TraceCodecException</c> ends the export and faults <c>Completion</c>; the graph keeps running.
/// </remarks>
[<Sealed>]
type TraceExport internal (graph: Graph, sink: ITraceSink, policy: SinkPolicy) =
    let log = TraceGate.logOf graph
    let encode = Some (TraceCodecs.encode policy.Codecs)
    let queue = TraceExportQueue (sink, policy.Capacity)
    let gate = obj ()

    // The seq of the last event queued, dropped or folded before the export.
    let mutable cursor = 0
    // True while a full queue under Wait holds back the next batch.
    let mutable waiting = false
    let mutable disposed = false
    // The last seq of the stream once disposed.
    let mutable target = 0
    let mutable fault: exn = null
    let mutable settled = false
    let mutable pulling = false
    let mutable gapFrom = 0
    let mutable gapTo = 0
    let mutable dropped = 0
#if FABLE_COMPILER
    let completion = Platform.completion<unit>()
#else
    let completion =
        TaskCompletionSource<unit> TaskCreationOptions.RunContinuationsAsynchronously
#endif

    member private _.Settle() =
        let outcome =
            lock gate (fun () ->
                if settled then
                    ValueNone
                else
                    let error = if isNull fault then queue.Fault else fault

                    if not (isNull error) then
                        settled <- true
                        ValueSome error
                    elif disposed && cursor >= target && queue.Idle then
                        settled <- true
                        ValueSome null
                    else
                        ValueNone)

        match outcome with
        | ValueNone -> ()
#if FABLE_COMPILER
        | ValueSome null -> completion.Resolve ()
        | ValueSome error -> completion.Reject error
#else
        | ValueSome null -> completion.TrySetResult () |> ignore
        | ValueSome error -> completion.TrySetException error |> ignore
#endif

    member private this.Enqueue(batch: TraceEvent[]) =
        let last = batch[batch.Length - 1].Seq

        let record =
            TraceExportRecord.Events (batch[0].Seq, last, TraceModel.eventLines encode batch)

        if
            gapTo <> 0
            && queue.TryWrite (TraceExportRecord.Gap (gapFrom, gapTo), 0)
        then
            gapFrom <- 0
            gapTo <- 0

        if queue.TryWrite (record, batch.Length) then
            lock gate (fun () -> cursor <- last)
            true
        else
            false

    member private this.Skip(last: int) =
        if gapTo = 0 then
            gapFrom <- cursor + 1

        gapTo <- last
        dropped <- dropped + (last - cursor)
        lock gate (fun () -> cursor <- last)

    // Queues the events up to the stream's end while the queue has room. Graph thread.
    member private this.Pull() =
        let mutable go = true

        while go do
            let last = lock gate (fun () -> if disposed then target else log.LastSeq)
            let room = policy.Capacity - queue.Held

            if cursor >= last || not (isNull queue.Fault) then
                go <- false
            elif room > 0 then
                let batch = log.EventsAfter (cursor, room)

                if not (this.Enqueue batch) then
                    go <- this.Full last
            else
                go <- this.Full last

    // Handles a full queue, and returns whether the pull continues.
    member private this.Full(last: int) =
        match policy.Full with
        | TraceExportFull.Drop ->
            this.Skip last
            true
        | TraceExportFull.Wait ->
            lock gate (fun () -> waiting <- true)

            if policy.Capacity - queue.Held > 0 then
                lock gate (fun () -> waiting <- false)
                true
            else
                false

    // Runs a pull unless one is in progress lower on the stack. Graph thread.
    member private this.Run() =
        if not pulling && isNull fault then
            pulling <- true

            try
                try
                    this.Pull ()
                with error ->
                    lock gate (fun () -> fault <- error)
                    log.RemoveFlushed this
            finally
                pulling <- false

        this.Settle ()

    member private this.Freed() =
        let resume =
            lock gate (fun () ->
                let was = waiting
                waiting <- false
                was)

        if resume then
            try
                graph.Dispatch (System.Action this.Run)
            with error ->
                lock gate (fun () ->
                    if isNull fault then
                        fault <- error)

        this.Settle ()

    member internal this.Start() =
        let struct (name, snapshot) = log.Checkpoint

        let before =
            if Platform.isNull snapshot then
                TraceModel.emptySnapshot
            else
                snapshot :?> TraceSnapshot

        cursor <- before.Seq
        let header = TraceModel.dumpHeader encode "net" name before (log.EventsSince cursor)
        queue.Freed <- fun () -> this.Freed ()

        queue.TryWrite (TraceExportRecord.Header header, 0)
        |> ignore

        log.AddFlushed this
        this.Run ()

    /// <summary>Exports the events recorded since the last export, as a flush's end does.</summary>
    /// <exception cref="T:System.InvalidOperationException">
    /// Called off the graph's thread, or while a flush, a discharge or a run is in progress.
    /// </exception>
    member this.Drain() =
        TraceGate.check graph "TraceExport.Drain"
        this.Run ()

    /// <summary>
    /// Completes after <c>Dispose</c> once the sink holds every record of the stream; faults with the sink's or the
    /// codec's error.
    /// </summary>
    member _.Completion: Task = completion.Task :> Task

    /// <summary>The events dropped by a full queue under <c>Drop</c>.</summary>
    member _.Dropped = dropped

    interface ITraceFlushed with
        member this.Flushed() =
            this.Run ()

    /// <summary>Ends the stream at the last event recorded, and exports the events up to it.</summary>
    /// <remarks>Runs on any thread: the export's end runs through <c>Graph.Dispatch</c>.</remarks>
    member this.Dispose() =
        let first =
            lock gate (fun () ->
                if disposed then
                    false
                else
                    disposed <- true
                    target <- log.LastSeq
                    true)

        if first then
            graph.Dispatch (
                System.Action (fun () ->
                    log.RemoveFlushed this
                    this.Run ())
            )

    interface System.IDisposable with
        member this.Dispose() =
            this.Dispose ()
