module CounterBench.Tests.LinuxPmcTests

open System
open Expecto
open CounterBench

let private linuxOnly () =
    if not (OperatingSystem.IsLinux ()) then
        skiptest "Linux only"

[<Tests>]
let tests =
    testList
        "LinuxPmc"
        [
            test "attributes encode a disabled user-mode hardware counter" {
                let bytes = LinuxPmc.attributes 1UL
                Expect.equal bytes.Length 112 "PERF_ATTR_SIZE_VER5"
                Expect.equal (BitConverter.ToUInt32 (bytes, 0)) 0u "PERF_TYPE_HARDWARE"
                Expect.equal (BitConverter.ToUInt32 (bytes, 4)) 112u "size"
                Expect.equal (BitConverter.ToUInt64 (bytes, 8)) 1UL "config"
                Expect.equal (BitConverter.ToUInt64 (bytes, 40)) 0x61UL "flags"
            }
            test "schedstat first field is thread CPU nanoseconds" {
                Expect.equal (LinuxPmc.parseSchedstat "123456 789 10\n") 123456L "CPU time"
                Expect.equal (LinuxPmc.parseSchedstat "  123456\t789 10\n") 123456L "whitespace"
            }
            test "unmeasured counts use minus one" {
                let counts = LinuxPmc.Counts.Unmeasured
                Expect.equal (counts.Instructions, counts.Cycles, counts.BranchMisses, counts.CpuNs) (-1L, -1L, -1L, -1L) "sentinels"
            }
            test "thread counters measure work and disposal is idempotent" {
                linuxOnly ()

                use counters =
                    LinuxPmc.ThreadCounters.Open (Environment.ProcessId, LinuxPmc.currentThreadId ())

                let measure n =
                    counters.Start ()
                    let mutable sum = 0L

                    for i in 1..n do
                        sum <- sum + int64 i

                    let counts = counters.Stop ()
                    GC.KeepAlive sum
                    counts

                let small = measure 1_000_000
                let large = measure 10_000_000
                Expect.isGreaterThan large.CpuNs 0L "CPU time"

                if counters.Hardware then
                    Expect.isGreaterThan large.Instructions small.Instructions "more work"
                else
                    Expect.equal large.Instructions -1L "unavailable PMU"

                (counters :> IDisposable).Dispose()
                (counters :> IDisposable).Dispose()
                Expect.throwsT<ObjectDisposedException> (fun () -> counters.Start ()) "disposed counter"
            }
        ]
