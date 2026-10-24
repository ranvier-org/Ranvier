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
