module Ranvier.Tests.TraceExportTests

open Expecto
open Ranvier

#if RANVIER_TRACE
let private events (from: int) (``to``: int) =
    TraceExportRecord.Events (from, ``to``, string from + ".." + string ``to`` + "\n")

let private waiting =
    {
        Capacity = 64
        Full = TraceExportFull.Wait
        Checkpoint = TraceExportCheckpoint.Never
        Codecs = TraceCodecs.ofList []
    }

// The last seq of the events the sink holds, 0 for none.
let private exportedTo (sink: MemoryTraceSink) =
    sink.Records
    |> List.fold
        (fun last record ->
            match record with
            | TraceExportRecord.Events (_, ``to``, _) -> ``to``
            | _ -> last)
        0

// The seqs of the events the sink holds, in stream order.
let private exportedSeqs (sink: MemoryTraceSink) =
    sink.Records
    |> List.collect (fun record ->
        match record with
        | TraceExportRecord.Events (from, ``to``, _) -> [ from..``to`` ]
        | TraceExportRecord.Gap (from, ``to``) -> failtestf "a gap from %d to %d" from ``to``
        | TraceExportRecord.Header _ -> [])

let private dropping =
    { waiting with
        Full = TraceExportFull.Drop
    }

// The seqs of the events and gaps the sink holds, in stream order.
let private streamSeqs (sink: MemoryTraceSink) =
    sink.Records
    |> List.collect (fun record ->
        match record with
        | TraceExportRecord.Events (from, ``to``, _)
        | TraceExportRecord.Gap (from, ``to``) -> [ from..``to`` ]
        | TraceExportRecord.Header _ -> [])

let private gaps (sink: MemoryTraceSink) =
    sink.Records
    |> List.choose (fun record ->
        match record with
        | TraceExportRecord.Gap (from, ``to``) -> Some (from, ``to``)
        | _ -> None)

let private checkpointSeq (graph: Graph) =
    (Trace.checkpointOf graph).Snapshot.Seq

type private Opaque = { Value: int }

type private ThrowingSink() =
    interface ITraceSink with
        member _.Name = "throwing"

        member _.Write _ =
            raise (System.InvalidOperationException "the sink is down")
#endif

[<Tests>]
let tests =
    testList
        "TraceExport"
        [
#if RANVIER_TRACE
            test "TryWrite returns false once the queued events reach the capacity" {
                let sink = TraceSinks.memory ()
                sink.Stall ()
                let queue = TraceExportQueue (sink, 4)

                Expect.isTrue (queue.TryWrite (events 1 3, 3)) "three events fit"
                Expect.isFalse (queue.TryWrite (events 4 5, 2)) "five events exceed the capacity"
                Expect.isTrue (queue.TryWrite (events 4 4, 1)) "four events fit"
                Expect.isFalse (queue.TryWrite (events 5 5, 1)) "the queue is full"
                Expect.isTrue (queue.TryWrite (TraceExportRecord.Gap (5, 5), 0)) "a record of no events fits"
                Expect.equal queue.Held 4 "the queue holds four events"
            }

            test "the capacity counts events, not records" {
                let sink = TraceSinks.memory ()
                sink.Stall ()
                let queue = TraceExportQueue (sink, 4)

                for seq in 1..4 do
                    Expect.isTrue (queue.TryWrite (events seq seq, 1)) ("record " + string seq)

                Expect.isFalse (queue.TryWrite (events 5 5, 1)) "the fifth event"

                let wide = TraceExportQueue (TraceSinks.memory (), 4)
                Expect.isFalse (wide.TryWrite (events 1 5, 5)) "a record wider than the capacity never fits"
            }

            test "a completed write frees its events, and the sink holds the records in order" {
                let sink = TraceSinks.memory ()
                sink.Stall ()
                let queue = TraceExportQueue (sink, 4)
                let mutable freed = 0
                queue.Freed <- fun _ -> freed <- freed + 1

                queue.TryWrite (events 1 2, 2) |> ignore
                queue.TryWrite (events 3 4, 2) |> ignore
                Expect.isEmpty sink.Records "a stalled sink holds nothing"
                Expect.isFalse (queue.TryWrite (events 5 5, 1)) "the queue is full"

                sink.Release ()

                Expect.equal queue.Held 0 "every write completed"
                Expect.isGreaterThan freed 0 "the queue reported the freed slots"
                Expect.isTrue (queue.TryWrite (events 5 5, 1)) "the freed slots take a record"
                Expect.sequenceEqual sink.Records [ events 1 2; events 3 4; events 5 5 ] "the records in write order"
                Expect.equal sink.Text "1..2\n3..4\n5..5\n" "the text of the records"
            }

            test "export writes the header first, then the retained events" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                s.Value <- 1
                let sink = TraceSinks.memory ()
                use _export = Trace.export g sink waiting

                match sink.Records with
                | TraceExportRecord.Header header :: _ ->
                    Expect.stringStarts header "{\"schema\":3," "a schema 3 header"
                    Expect.equal (header.Split('\n').Length) 3 "the header line and the snapshot line"
                | records -> failtestf "the first record is a header: %A" records

                Expect.equal (exportedTo sink) (Trace.lastSeq g) "the retained events"
            }

            test "the events of a flush are exported when it ends" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                let mutable seen = 0
                createEffect (fun () -> seen <- s.Value)
                let sink = TraceSinks.memory ()
                use _export = Trace.export g sink waiting

                s.Value <- 1

                Expect.equal seen 1 "the effect ran"
                Expect.equal (exportedTo sink) (Trace.lastSeq g) "the flush's events, with no Drain call"

                let kinds =
                    Trace.eventsSince g 0
                    |> Array.filter (fun e -> e.Seq > 0)
                    |> Array.map _.Kind

                Expect.contains kinds TraceEventKind.FlushEnd "the log holds the flush"
            }

            test "Drain exports the events of a write that wakes no effect" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                let sink = TraceSinks.memory ()
                use export = Trace.export g sink waiting

                s.Value <- 1
                s.Value <- 2
                let before = exportedTo sink
                export.Drain ()

                Expect.isLessThan before (Trace.lastSeq g) "the writes ran no flush"
                Expect.equal (exportedTo sink) (Trace.lastSeq g) "Drain exported the writes"
            }

            test "Dispose stops the export and completes Completion once the sink holds every record" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                createEffect (fun () -> s.Value |> ignore)
                let sink = TraceSinks.memory ()
                sink.Stall ()
                let export = Trace.export g sink waiting
                s.Value <- 1
                let last = Trace.lastSeq g
                export.Dispose ()

                Expect.isFalse export.Completion.IsCompleted "the sink holds back the writes"

                s.Value <- 2
                sink.Release ()

                Expect.isTrue export.Completion.IsCompleted "the sink holds every record"
                Expect.equal (exportedTo sink) last "the events up to Dispose"
            }

            test "Wait loses no event behind a stalled sink, and the stream is the dumpTextWith text" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                let mutable seen = 0
                createEffect (fun () -> seen <- s.Value)
                let sink = TraceSinks.memory ()
                sink.Stall ()
                let export = Trace.export g sink waiting

                for i in 1..10000 do
                    s.Value <- i

                let expected = Trace.dumpTextWith g waiting.Codecs
                let last = Trace.lastSeq g
                Expect.isLessThan (exportedTo sink) last "the stalled sink holds back the stream"

                sink.Release ()
                export.Dispose ()

                Expect.isTrue export.Completion.IsCompleted "the sink holds every record"
                Expect.equal export.Dropped 0 "no event dropped"
                Expect.sequenceEqual (exportedSeqs sink) [ 1..last ] "every seq, once and in order"
                Expect.equal sink.Text expected "the header and events concatenate to dumpTextWith"
            }

            test "a write completed off the graph's thread resumes the export through Graph.Dispatch" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                createEffect (fun () -> s.Value |> ignore)
                let sink = TraceSinks.memory ()
                sink.Stall ()
                let export = Trace.export g sink waiting

                for i in 1..1000 do
                    s.Value <- i

                // A dedicated thread: Task.Wait may run a queued task inline on the graph's thread.
                let releasing = System.Threading.Thread (fun () -> sink.Release ())
                releasing.Start ()
                releasing.Join ()
                Expect.isLessThan (exportedTo sink) (Trace.lastSeq g) "the rest waits for the graph's thread"

                Expect.equal (g.Pump ()) 1 "the pump ran the dispatched resume"
                Expect.equal (exportedTo sink) (Trace.lastSeq g) "the dispatched resume exported the rest"

                export.Dispose ()
                Expect.isTrue export.Completion.IsCompleted "the sink holds every record"
                Expect.sequenceEqual (exportedSeqs sink) [ 1 .. Trace.lastSeq g ] "every seq, once and in order"
            }

            test "Drop writes the dropped seqs in one gap once the stalled sink frees a slot" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                createEffect (fun () -> s.Value |> ignore)
                let sink = TraceSinks.memory ()
                sink.Stall ()
                let export = Trace.export g sink dropping

                for i in 1..1000 do
                    s.Value <- i

                let last = Trace.lastSeq g
                Expect.isGreaterThan export.Dropped 0 "the full queue dropped events"

                sink.Release ()

                match gaps sink with
                | [ (from, ``to``) ] -> Expect.equal (``to`` - from + 1) export.Dropped "the gap names every dropped seq"
                | other -> failtestf "one gap before Dispose: %A" other

                export.Dispose ()
                Expect.isTrue export.Completion.IsCompleted "the sink holds every record"
                Expect.sequenceEqual (streamSeqs sink) [ 1..last ] "the events and the gap cover every seq once, in order"
            }

            test "Drop writes the pending gap when Dispose follows the drop" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                createEffect (fun () -> s.Value |> ignore)
                let sink = TraceSinks.memory ()
                sink.Stall ()
                let export = Trace.export g sink dropping

                for i in 1..1000 do
                    s.Value <- i

                let last = Trace.lastSeq g
                export.Dispose ()
                sink.Release ()

                Expect.isTrue export.Completion.IsCompleted "the sink holds every record"
                Expect.equal (List.length (gaps sink)) 1 "one gap"
                Expect.sequenceEqual (streamSeqs sink) [ 1..last ] "the events and the gap cover every seq once, in order"
#if !FABLE_COMPILER
                let dump = TraceContract.parseDumpWith dropping.Codecs sink.Text
                Expect.equal dump.Gaps (gaps sink) "the stream's gap parses"

                Expect.sequenceEqual
                    (dump.Events |> Array.map _.Seq)
                    (streamSeqs sink
                     |> List.except
                         [
                             for from, ``to`` in gaps sink do
                                 yield! [ from..``to`` ]
                         ])
                    "the stream's events parse"
#endif
            }

            // Sequenced: the memory reading is process-wide.
            testSequenced
            <| test "Enqueued keeps the log and the process memory bounded under a stalled sink with Drop" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                createEffect (fun () -> s.Value |> ignore)
                let sink = TraceSinks.memory ()
                sink.Stall ()

                let export =
                    Trace.export
                        g
                        sink
                        { dropping with
                            Checkpoint = TraceExportCheckpoint.Enqueued
                        }

                for i in 1..10000 do
                    s.Value <- i

                let bound = System.GC.GetTotalMemory true

                for i in 10001..100000 do
                    s.Value <- i

                let used = System.GC.GetTotalMemory true
                let last = Trace.lastSeq g
                Expect.isLessThan used (bound + 1_000_000L) "90,000 more writes hold under 1 MB more"

                Expect.isLessThanOrEqual (TraceGate.logOf g).Events.Length dropping.Capacity "the log retains at most a queue of events"

                Expect.equal (Trace.checkpointOf g).Name "memory" "the checkpoint carries the sink's name"

                sink.Release ()
                export.Dispose ()

                Expect.isTrue export.Completion.IsCompleted "the sink holds every record"
                Expect.equal (List.length (gaps sink)) 1 "one gap"
                Expect.sequenceEqual (streamSeqs sink) [ 1..last ] "the events and the gap cover every seq once, in order"
            }

            test "Enqueued checkpoints once the exported events past the checkpoint reach the capacity" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                createEffect (fun () -> s.Value |> ignore)
                let sink = TraceSinks.memory ()

                let policy =
                    { waiting with
                        Checkpoint = TraceExportCheckpoint.Enqueued
                    }

                let export = Trace.export g sink policy
                s.Value <- 1

                Expect.isLessThan (Trace.lastSeq g) policy.Capacity "fewer events than the capacity"
                Expect.equal (checkpointSeq g) 0 "fewer exported events than the capacity take no checkpoint"

                for i in 2..1000 do
                    s.Value <- i

                    Expect.isLessThan (Trace.lastSeq g - checkpointSeq g) policy.Capacity "the log retains fewer events than the capacity"

                Expect.isGreaterThan (checkpointSeq g) 0 "the exported events took a checkpoint"
                export.Dispose ()

                Expect.isTrue export.Completion.IsCompleted "the sink holds every record"
                Expect.equal (checkpointSeq g) (Trace.lastSeq g) "the stream's end checkpoints every event"
                Expect.sequenceEqual (exportedSeqs sink) [ 1 .. Trace.lastSeq g ] "every seq, once and in order"
            }

            test "Enqueued checkpoints before a NodeNew whose Label follows it" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = Trace.named "count" (fun () -> createSignal 0)

                let created, _ =
                    Trace.eventsSince g 0
                    |> Array.pairwise
                    |> Array.find (fun (e, l) ->
                        e.Kind = TraceEventKind.NodeNew
                        && l.Kind = TraceEventKind.Label)

                let sink = TraceSinks.memory ()
                sink.Stall ()

                let export =
                    Trace.export
                        g
                        sink
                        { waiting with
                            Capacity = created.Seq
                            Checkpoint = TraceExportCheckpoint.Enqueued
                        }

                Expect.equal (checkpointSeq g) (created.Seq - 1) "the checkpoint precedes the NodeNew"

                s.Value <- 1
                sink.Release ()
                export.Dispose ()

                Expect.isTrue export.Completion.IsCompleted "the sink holds every record"
                Expect.equal (checkpointSeq g) (Trace.lastSeq g) "the checkpoint follows the stream's end"
                Expect.equal (Trace.checkpointOf g).Snapshot.Nodes[created.Node].Label (Some "count") "the checkpoint keeps the label"
            }

            test "Acknowledged checkpoints once the sink completes a write" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                createEffect (fun () -> s.Value |> ignore)
                let sink = TraceSinks.memory ()
                sink.Stall ()

                let export =
                    Trace.export
                        g
                        sink
                        { waiting with
                            Checkpoint = TraceExportCheckpoint.Acknowledged
                        }

                for i in 1..100 do
                    s.Value <- i

                Expect.equal (checkpointSeq g) 0 "a stalled sink acknowledges nothing"

                sink.Release ()

                Expect.isGreaterThan (checkpointSeq g) 0 "the acknowledged events took a checkpoint"

                Expect.isLessThan
                    (exportedTo sink - checkpointSeq g)
                    waiting.Capacity
                    "fewer acknowledged events than the capacity stay past the checkpoint"

                Expect.equal (Trace.checkpointOf g).Name "memory" "the checkpoint carries the sink's name"

                export.Dispose ()
                Expect.isTrue export.Completion.IsCompleted "the sink holds every record"
                Expect.equal (checkpointSeq g) (Trace.lastSeq g) "every event is acknowledged"
            }

            test "a checkpoint taken past the cursor yields a gap" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                createEffect (fun () -> s.Value |> ignore)
                let sink = TraceSinks.memory ()
                sink.Stall ()
                let export = Trace.export g sink waiting

                for i in 1..100 do
                    s.Value <- i

                let folded = Trace.lastSeq g
                Trace.checkpoint g folded "elsewhere"

                for i in 101..110 do
                    s.Value <- i

                sink.Release ()
                export.Dispose ()

                Expect.isTrue export.Completion.IsCompleted "the sink holds every record"
                Expect.equal export.Dropped 0 "a checkpoint drops no event"

                match gaps sink with
                | [ (_, ``to``) ] -> Expect.equal ``to`` folded "the gap ends at the checkpoint"
                | other -> failtestf "one gap: %A" other

                Expect.sequenceEqual (streamSeqs sink) [ 1 .. Trace.lastSeq g ] "the events and the gap cover every seq once, in order"
            }

            test "a throwing sink faults Completion, and the graph keeps running" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                let mutable seen = 0
                createEffect (fun () -> seen <- s.Value)
                let export = Trace.export g (ThrowingSink ()) waiting

                s.Value <- 1
                s.Value <- 2

                Expect.equal seen 2 "the effect ran"
                Expect.isTrue export.Completion.IsFaulted "the sink's error faults Completion"

                Expect.equal (export.Completion.Exception.GetBaseException ()).Message "the sink is down" "the sink's error"
            }

            test "a strict codec faults Completion, and the graph keeps running" {
                use g = new Graph ()
                use _ = g.Activate ()
                let sink = TraceSinks.memory ()

                let export =
                    Trace.export
                        g
                        sink
                        { waiting with
                            Codecs = TraceCodecs.strict (TraceCodecs.ofList [])
                        }

                let s = createSignal { Value = 0 }
                let mutable seen = 0
                createEffect (fun () -> seen <- s.Value.Value)
                s.Value <- { Value = 1 }
                s.Value <- { Value = 2 }

                Expect.equal seen 2 "the effect ran"
                Expect.isTrue export.Completion.IsFaulted "the codec's error faults Completion"

                Expect.isTrue (export.Completion.Exception.GetBaseException () :? TraceCodecException) "a TraceCodecException"
            }

            test "a gap record writes a gap line" {
                Expect.equal (TraceExportRecord.text (TraceExportRecord.Gap (3, 7))) "{\"gap\":{\"from\":3,\"to\":7}}\n" "the gap line"
            }
#endif
        ]
