module Ranvier.Tests.TraceExportTests

open Expecto
open Ranvier

#if RANVIER_TRACE
let private events (from: int) (``to``: int) =
    TraceExportRecord.Events (from, ``to``, string from + ".." + string ``to`` + "\n")
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
                queue.Freed <- fun () -> freed <- freed + 1

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

            test "a gap record writes a gap line" {
                Expect.equal (TraceExportRecord.text (TraceExportRecord.Gap (3, 7))) "{\"gap\":{\"from\":3,\"to\":7}}\n" "the gap line"
            }
#endif
        ]
