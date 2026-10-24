module CounterBench.Tests.TraceGrowthTests

open Expecto
open CounterBench.TraceGrowth

let private checkpoint (updates: int) (events: int) (retained: float) : Checkpoint =
    {
        Workload = "memo"
        Updates = updates
        Events = events
        RetainedBytes = retained
        EventsMs = 0.
        SnapshotMs = 0.
        DumpTextMs = 0.
        TailReadMs = 0.
        TailEvents = 0
    }

let private summary (checkpoints: Checkpoint[]) =
    match summarise checkpoints with
    | Ok summary -> summary
    | Error message -> failtest message

[<Tests>]
let tests =
    testList
        "TraceGrowth.summarise"
        [
            test "an exact linear series gives its exact slopes" {
                let s =
                    summary
                        [|
                            for updates in [ 1000; 10000; 100000 ] -> checkpoint updates (14 * updates + 16) (1100. * float updates + 3000.)
                        |]

                Expect.equal s.Workload "memo" "workload"
                Expect.equal s.Checkpoints 3 "checkpoints"
                Expect.floatClose Accuracy.high s.EventSlope 14. "events per update, marginal"
                Expect.floatClose Accuracy.high s.RetainedSlope 1100. "retained bytes per update, marginal"
                Expect.floatClose Accuracy.high s.BytesPerEvent (1100. / 14.) "retained bytes per event"
                Expect.floatClose Accuracy.high s.EventsPerUpdate (float (14 * 100000 + 16) / 100000.) "events per update at the last checkpoint"
            }

            test "an untraced series of zero events gives zero slopes" {
                let s =
                    summary [| for updates in [ 1000; 10000; 100000 ] -> checkpoint updates 0 5000. |]

                Expect.equal s.EventSlope 0. "event slope"
                Expect.equal s.RetainedSlope 0. "retained slope"
                Expect.equal s.BytesPerEvent 0. "bytes per event"
                Expect.equal s.EventsPerUpdate 0. "events per update"
            }

            test "the checkpoints may arrive in any order" {
                let s = summary [| checkpoint 100000 1400000 0.; checkpoint 1000 14000 0. |]

                Expect.floatClose Accuracy.high s.EventsPerUpdate 14. "events per update at the most updates"
            }

            test "fewer than two checkpoints is an error" {
                Expect.isError (summarise [||]) "no checkpoints"
                Expect.isError (summarise [| checkpoint 1000 14000 0. |]) "one checkpoint"
            }

            test "checkpoints at one update count are an error" {
                Expect.isError (summarise [| checkpoint 1000 14000 0.; checkpoint 1000 14000 0. |]) "one update count"
            }

            test "checkpoints of two workloads are an error" {
                Expect.isError
                    (summarise
                        [|
                            checkpoint 1000 14000 0.
                            { checkpoint 10000 140000 0. with
                                Workload = "fanout"
                            }
                        |])
                    "two workloads"
            }
        ]
