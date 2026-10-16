module CounterBench.Tests.WorkerTests

open System
open System.Runtime
open System.Text.Json
open Expecto
open CounterBench
open CounterBench.Scenarios

let private allocating: Case =
    {
        Scenario = "test-allocate"
        Engine = "Test"
        Unit = "one array"
        Ops = 1000
        Prepare =
            fun ops ->
                {
                    Run =
                        fun () ->
                            for _ in 1..ops do
                                GC.KeepAlive (Array.zeroCreate<byte> 16)
                    Teardown = ignore
                }
    }

[<Tests>]
let tests =
    testList
        "Worker.measure"
        [
            test "without Linux counters every native figure reads -1" {
                let m = Worker.measure None allocating 1000 0
                Expect.equal (m.Instructions, m.Cycles, m.BranchMisses, m.CpuNs) (-1L, -1L, -1L, -1L) "unmeasured"
            }
            test "Linux counter reads leave allocation bytes unchanged" {
                if not (OperatingSystem.IsLinux ()) then
                    skiptest "Linux only"

                use counters =
                    LinuxPmc.ThreadCounters.Open (Environment.ProcessId, LinuxPmc.currentThreadId ())

                let without = Worker.measure None allocating 1_000_000 0
                let measured = Worker.measure (Some counters) allocating 1_000_000 1
                Expect.equal measured.Bytes without.Bytes "counter reads sit outside allocation reads"
                Expect.isGreaterThan measured.CpuNs 0L "a substantial allocation workload records thread CPU"
            }
            test "failed work tears down and exits the no-GC region" {
                let mutable disposed = false

                let broken =
                    { allocating with
                        Prepare =
                            fun _ ->
                                {
                                    Run = (fun () -> failwith "work failed")
                                    Teardown = (fun () -> disposed <- true)
                                }
                    }

                Expect.throws (fun () -> Worker.measure None broken 1 0 |> ignore) "work failure propagates"
                Expect.isTrue disposed "teardown runs on failure"
                Expect.notEqual GCSettings.LatencyMode GCLatencyMode.NoGCRegion "no-GC region closed"
            }
            test "failed Linux work leaves counters reusable" {
                if not (OperatingSystem.IsLinux ()) then
                    skiptest "Linux only"

                use counters =
                    LinuxPmc.ThreadCounters.Open (Environment.ProcessId, LinuxPmc.currentThreadId ())

                let broken =
                    { allocating with
                        Prepare =
                            fun _ ->
                                {
                                    Run = (fun () -> failwith "work failed")
                                    Teardown = ignore
                                }
                    }

                Expect.throws
                    (fun () ->
                        Worker.measure (Some counters) broken 1 0
                        |> ignore)
                    "work failure propagates"

                let next = Worker.measure (Some counters) allocating 1000 1
                Expect.isGreaterThanOrEqual next.CpuNs 0L "next measurement succeeds"
            }
            test "legacy report JSON retains its original measurement shape while transport includes Linux fields" {
                let measurement = Worker.measure None allocating 1 0

                let names (options: JsonSerializerOptions) =
                    use json =
                        JsonDocument.Parse (
                            JsonSerializer.Serialize (
                                {|
                                    Runs = [| {| Measurements = [| measurement |] |} |]
                                |},
                                options
                            )
                        )

                    (json.RootElement.GetProperty("Runs").[0].GetProperty("Measurements").[0]).EnumerateObject()
                    |> Seq.map (fun p -> p.Name)
                    |> Seq.toList

                Expect.equal
                    (names (Program.reportJsonOptions ()))
                    [ "Ops"; "Region"; "Bytes"; "Gen0Collections"; "NoGcRegion"; "Counters" ]
                    "legacy ordered shape"

                Expect.containsAll
                    (names (JsonSerializerOptions ()))
                    [ "Instructions"; "Cycles"; "BranchMisses"; "CpuNs" ]
                    "transport fields retained"
            }
        ]
    |> testSequenced
