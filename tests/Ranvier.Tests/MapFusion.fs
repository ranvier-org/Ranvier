module Ranvier.Tests.MapFusion

open System
open System.Collections.Generic
open Expecto
open Ranvier
open Ranvier.Tests.Streams

[<AllowNullLiteral>]
type private TierRef(tier: int) =
    member _.Tier = tier

let private failedWith (reading: Reading<'T>) =
    match reading with
    | Failed ex -> ex
    | other -> failtestf "expected Failed, got %A" other

let private isInvalidOperation (ex: exn) =
#if FABLE_COMPILER
    not (isNull (box ex))
#else
    ex :? InvalidOperationException
#endif

#if !FABLE_COMPILER && !RANVIER_TRACE && !RANVIER_COUNTERS
let private assertZeroAllocLeaf4 () =
    use g = new Graph ()

    g.Run (fun () ->
        let sAlloc = createSignal 0

        let allocLeaf4 =
            sAlloc
            |> Signal.map ((+) 1)
            |> Memo.map ((+) 1)
            |> Memo.map ((+) 1)
            |> Memo.map ((+) 1)

        for i in 1..1_000 do
            sAlloc.Value <- i
            allocLeaf4.Value |> ignore

        let beforeBytes = GC.GetAllocatedBytesForCurrentThread ()

        for i in 1_001..11_000 do
            sAlloc.Value <- i
            allocLeaf4.Value |> ignore

        let allocatedBytes =
            GC.GetAllocatedBytesForCurrentThread ()
            - beforeBytes

        Expect.isLessThan
            allocatedBytes
            8_192L
            $"10,000 4-stage fused int leaf updates allocated %d{allocatedBytes} bytes; boxing stages allocates ~960,000")
#endif

[<Tests>]
let tests =
    testList
        "MapFusion"
        [
            testCaseAsync "4-stage and 5-stage map chains update, cut off, and propagate Pending/Failed identically across root types"
            <| async {
                use g = new Graph ()
                use _ = g.Activate ()

                // 1. Unobserved leaf 4-stage Signal chain (exercises leaf-Dirty ReadSlow fast path) + 5-stage (> 4 fallback).
                let s = createSignal 1

                let leaf4 =
                    s
                    |> Signal.map ((+) 1)
                    |> Memo.map ((+) 1)
                    |> Memo.map ((+) 1)
                    |> Memo.map ((+) 1)

                let leaf5 =
                    s
                    |> Signal.map ((+) 1)
                    |> Memo.map ((+) 1)
                    |> Memo.map ((+) 1)
                    |> Memo.map ((+) 1)
                    |> Memo.map ((+) 1)

                Expect.equal leaf4.Value 5 "initial 4-stage leaf"
                Expect.equal leaf5.Value 6 "initial 5-stage leaf"

                s.Value <- 10
                Expect.equal leaf4.Value 14 "updated 4-stage leaf"
                Expect.equal leaf5.Value 15 "updated 5-stage leaf"

                s.Value <- 20
                Expect.equal leaf4.Value 24 "second leaf-Dirty update on 4-stage"
                Expect.equal leaf5.Value 25 "second leaf-Dirty update on 5-stage"

#if !FABLE_COMPILER && !RANVIER_TRACE && !RANVIER_COUNTERS
                assertZeroAllocLeaf4 ()
#endif

                // Observed 4-stage chain
                let obsLog = ResizeArray<int>()

                let obs4 =
                    s
                    |> Signal.map ((+) 1)
                    |> Memo.map ((*) 2)
                    |> Memo.map ((+) 3)
                    |> Memo.map (fun x -> x - 1)

                createEffect (fun () -> obsLog.Add obs4.Value)
                Expect.sequenceEqual obsLog [ 44 ] "initial observed 4-stage ((20+1)*2 + 3 - 1 = 44)"
                s.Value <- 5
                Expect.sequenceEqual obsLog [ 44; 14 ] "updated observed 4-stage ((5+1)*2 + 3 - 1 = 14)"

                // 2. AsyncSource, AsyncMemo, and StreamMemo 4-stage chains propagating Pending and Failed
                let asrc = createAsyncSource<int>()

                let asrcChain =
                    asrc
                    |> AsyncSource.map ((+) 1)
                    |> Memo.map ((+) 1)
                    |> Memo.map ((+) 1)
                    |> Memo.map ((+) 1)

                Expect.equal asrcChain.TryValue Pending "AsyncSource 4-stage Pending"
                let asrcErr = InvalidOperationException "asrc-boom"
                asrc.Fail asrcErr
                Expect.isTrue (obj.ReferenceEquals (failedWith asrcChain.TryValue, asrcErr)) "AsyncSource 4-stage error"
                Expect.isTrue (obj.ReferenceEquals (asrcChain.ErrorOrigin, asrc)) "AsyncSource 4-stage ErrorOrigin"
                asrc.Settle 10
                Expect.equal asrcChain.Value 14 "AsyncSource 4-stage settled"

                let amTrigger = createSignal 3
                let am = Make.AsyncMemo<int>(g, fun _ _ -> completed amTrigger.Value)

                let amChain =
                    am
                    |> AsyncMemo.map ((+) 1)
                    |> Memo.map ((+) 1)
                    |> Memo.map ((+) 1)
                    |> Memo.map ((+) 1)

                amChain.TryValue |> ignore
                do! drained ()
                Expect.equal amChain.Value 7 "AsyncMemo 4-stage settled"

                let feed = ManualStream<int>()
                let sm = createStream (fun _ -> feed.Source)

                let smChain =
                    sm
                    |> StreamMemo.map ((+) 1)
                    |> Memo.map ((+) 1)
                    |> Memo.map ((+) 1)
                    |> Memo.map ((+) 1)

                Expect.equal smChain.TryValue Pending "StreamMemo 4-stage Pending"
                feed.Push 8
                Expect.equal smChain.Value 12 "StreamMemo 4-stage settled"
            }

            test "B18 clear_fused_ready: Ready x -> Pending/Failed -> Ready x recovers tail without false stage-1 cutoff" {
                use g = new Graph ()
                use _ = g.Activate ()

                let mode = createSignal 0
                let valSig = createSignal 10
                let pendingSrc = createAsyncSource<int>()
                let boom = InvalidOperationException "transient-upstream-fail"
                let failOrigin: Memo<int> = createMemo (fun _ -> raise boom)

                let root =
                    createMemo (fun _ ->
                        match mode.Value with
                        | 0 -> valSig.Value
                        | 1 -> pendingSrc.Value
                        | _ -> failOrigin.Value)

                let stage2Runs = ref 0
                let stage4Runs = ref 0

                let tail =
                    root
                    |> Memo.map (fun x -> x / 10)
                    |> Memo.map (fun a ->
                        stage2Runs.Value <- stage2Runs.Value + 1
                        a + 100)
                    |> Memo.map ((*) 2)
                    |> Memo.map (fun c ->
                        stage4Runs.Value <- stage4Runs.Value + 1
                        c + 1)

                // 1. Warm at Ready 10 -> stage 1 caches 1, tail = 203.
                Expect.equal tail.Value 203 "initial Ready 203"
                Expect.equal stage2Runs.Value 1 "stage 2 ran once"
                Expect.equal stage4Runs.Value 1 "stage 4 ran once"

                // 2. Transition Ready 10 -> Pending -> Ready 11 (which produces equal stage-1 output 1!).
                mode.Value <- 1
                Expect.equal tail.TryValue Pending "tail transitions to Pending"
                Expect.equal tail.Status Status.Pending "tail.Status is Pending"

                valSig.Value <- 11
                mode.Value <- 0
                Expect.equal tail.TryValue (Ready 203) "tail recovers to Ready 203 instead of staying Pending"
                Expect.equal tail.Status Status.None "tail.Status cleared back to Ready"
                Expect.equal stage2Runs.Value 2 "stage 2 re-ran because clear_fused_ready cleared stage 1 hasReady"
                Expect.equal stage4Runs.Value 2 "stage 4 re-ran after Pending excursion"

                // 3. Transition Ready 11 -> Failed -> Ready 11!
                mode.Value <- 2
                Expect.isTrue (obj.ReferenceEquals (failedWith tail.TryValue, boom)) "tail transitions to Failed"
                Expect.equal tail.Status Status.Error "tail.Status is Error"
                Expect.isTrue (obj.ReferenceEquals (tail.ErrorOrigin, failOrigin)) "upstream ErrorOrigin preserved"

                mode.Value <- 0
                Expect.equal tail.TryValue (Ready 203) "tail recovers to Ready 203 after Failed excursion"
                Expect.equal tail.Status Status.None "tail.Status cleared after Failed excursion"
                Expect.equal stage2Runs.Value 3 "stage 2 re-ran after Failed excursion"
                Expect.equal stage4Runs.Value 3 "stage 4 re-ran after Failed excursion"

                // 4. Stage-throw clearing downstream stages: stage 1 throws on negative input, then recovers to equal tier 1!
                let s = createSignal 10

                let throwRecoverTail =
                    s
                    |> Signal.map (fun x ->
                        if x < 0 then
                            failwith "neg"

                        x / 10)
                    |> Memo.map ((+) 50)
                    |> Memo.map ((*) 2)
                    |> Memo.map ((+) 1)

                Expect.equal throwRecoverTail.Value 103 "initial Ready 103"
                s.Value <- -5
                let exNeg = failedWith throwRecoverTail.TryValue
                Expect.equal exNeg.Message "neg" "stage 1 throw surfaces"
                s.Value <- 12
                Expect.equal throwRecoverTail.Value 103 "recovers to 103 when stage 1 returns 1 again after throwing"
            }

            test "per-stage cutoff suppresses downstream stages and effects and preserves ReferenceEquals" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 10
                let allocCount = ref 0
                let effectRuns = ref 0

                let tiered =
                    s
                    |> Signal.map (fun n -> n / 10)
                    |> Memo.map (fun t ->
                        allocCount.Value <- allocCount.Value + 1
                        TierRef t)

                createEffect (fun () ->
                    effectRuns.Value <- effectRuns.Value + 1
                    tiered.Value |> ignore)

                let firstRef = tiered.Value
                Expect.equal firstRef.Tier 1 "initial tier 1"
                Expect.equal allocCount.Value 1 "1 TierRef allocated"
                Expect.equal effectRuns.Value 1 "1 effect run"

                // 10 -> 11 cuts off at stage 1: stage 2 constructor and downstream effect do NOT run!
                s.Value <- 11
                let secondRef = tiered.Value
                Expect.isTrue (obj.ReferenceEquals (firstRef, secondRef)) "ReferenceEquals preserved across cutoff"
                Expect.equal allocCount.Value 1 "TierRef constructor not invoked on cutoff"
                Expect.equal effectRuns.Value 1 "downstream effect not woken on cutoff"

                // Consecutive clean reads preserve ReferenceEquals
                Expect.isTrue (obj.ReferenceEquals (firstRef, tiered.Value)) "ReferenceEquals on consecutive clean read"

                // 11 -> 25 crosses tier boundary -> allocates once and wakes effect once
                s.Value <- 25
                let thirdRef = tiered.Value
                Expect.equal thirdRef.Tier 2 "updated tier 2"
                Expect.isFalse (obj.ReferenceEquals (firstRef, thirdRef)) "new TierRef on tier change"
                Expect.equal allocCount.Value 2 "2nd TierRef allocated"
                Expect.equal effectRuns.Value 2 "effect woken once"
            }

            test "per-stage ErrorOrigin attribution across stages 1..5, NotReadyException wrapping, and Pure violation" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                let pending = createAsyncSource<int>()

                let m1 =
                    Signal.map
                        (fun x ->
                            if x = 1 then
                                failwith "s1"

                            x + 1)
                        s

                let m2 =
                    Memo.map
                        (fun x ->
                            if x = 3 then
                                failwith "s2"

                            if x = 101 then
                                pending.Value |> ignore

                            if x = 201 then
                                // fsharpanalyzer: ignore-line-next RAN010
                                onCleanup ignore

                            x + 1)
                        m1

                let m3 =
                    Memo.map
                        (fun x ->
                            if x = 5 then
                                failwith "s3"

                            x + 1)
                        m2

                let m4 =
                    Memo.map
                        (fun x ->
                            if x = 7 then
                                failwith "s4"

                            x + 1)
                        m3

                Expect.equal m4.Value 4 "initial Ready 4 (0 -> 1 -> 2 -> 3 -> 4)"

                // Stage 1 throw (s = 1) -> ErrorOrigin is m1! (Test via fresh chain so m1..m3 aren't materialized before throw!)
                let checkStageOrigin (triggerVal: int) (expectedIdx: int) =
                    let sigLocal = createSignal triggerVal

                    let s1 =
                        Signal.map
                            (fun x ->
                                if x = 1 then
                                    failwith "err1"

                                x + 10)
                            sigLocal

                    let s2 =
                        Memo.map
                            (fun x ->
                                if x = 12 then
                                    failwith "err2"

                                x + 10)
                            s1

                    let s3 =
                        Memo.map
                            (fun x ->
                                if x = 23 then
                                    failwith "err3"

                                x + 10)
                            s2

                    let s4 =
                        Memo.map
                            (fun x ->
                                if x = 34 then
                                    failwith "err4"

                                x + 10)
                            s3

                    let _ = failedWith s4.TryValue

                    let expected: INode =
                        match expectedIdx with
                        | 1 -> s1 :> INode
                        | 2 -> s2 :> INode
                        | 3 -> s3 :> INode
                        | _ -> s4 :> INode

                    Expect.isTrue (obj.ReferenceEquals (s4.ErrorOrigin, expected)) $"stage {expectedIdx} ErrorOrigin matches m{expectedIdx}"

                checkStageOrigin 1 1
                checkStageOrigin 2 2
                checkStageOrigin 3 3
                checkStageOrigin 4 4

                // Stage 2 synchronous NotReadyException wraps into InvalidOperationException with ErrorOrigin = m2
                s.Value <- 100
                let exLeak = failedWith m4.TryValue
                Expect.isTrue (isInvalidOperation exLeak) "NotReadyException in fused stage wraps to InvalidOperationException"
                Expect.isTrue (obj.ReferenceEquals (m4.ErrorOrigin, m2)) "NotReadyException ErrorOrigin is m2"

                // Stage 2 pure violation fails with Pure message and ErrorOrigin = m2
                s.Value <- 200
                let exPure = failedWith m4.TryValue
                Expect.equal exPure.Message (ScopeMessages.forMode ScopeMode.Pure) "Pure violation message in fused stage"
                Expect.isTrue (obj.ReferenceEquals (m4.ErrorOrigin, m2)) "Pure violation ErrorOrigin is m2"
            }

            test "Review Focus #2: post-warmup lazy de-fusion on m1.Value, second Memo.map, and m1.Dispose preserves ReferenceEquals and 0 extra runs" {
                use g = new Graph ()
                use _ = g.Activate ()

                // Case 1: Warm m2.Value first, then read m1.Value and m1.Runs (including after a stage-1 cutoff!).
                let sA = createSignal 10
                let f1RunsA = ref 0

                let m1A =
                    Signal.map
                        (fun n ->
                            f1RunsA.Value <- f1RunsA.Value + 1
                            TierRef (n / 10))
                        sA

                let m2A = Memo.map (fun (t: TierRef) -> t.Tier * 100) m1A

                Expect.equal m2A.Value 100 "warm m2A first"
                Expect.equal f1RunsA.Value 1 "f1 ran once during m2A warmup"

                let ref1 = m1A.Value
                Expect.equal f1RunsA.Value 1 "reading m1A.Value after m2A warmup runs f1 0 extra times"
                Expect.equal ref1.Tier 1 "m1A materialized value has Tier 1"
                Expect.isTrue (obj.ReferenceEquals (ref1, m1A.Value)) "m1A ReferenceEquals preserved"
                Expect.equal m1A.Runs 1 "m1A.Runs is 1"

                sA.Value <- 20
                Expect.equal m2A.Value 200 "m2A continues updating through materialized m1A"
                Expect.equal m1A.Value.Tier 2 "m1A updated to Tier 2"
                Expect.equal f1RunsA.Value 2 "f1 ran once for 20"

                // Case 2: Warm m2B.Value first, then attach m3B = Memo.map h m1B -> shares m1B with 0 extra f1 runs!
                let sB = createSignal 10
                let f1RunsB = ref 0

                let m1B =
                    Signal.map
                        (fun n ->
                            f1RunsB.Value <- f1RunsB.Value + 1
                            TierRef (n / 10))
                        sB

                let m2B = Memo.map (fun (t: TierRef) -> t.Tier + 1) m1B

                Expect.equal m2B.Value 2 "warm m2B first"
                Expect.equal f1RunsB.Value 1 "f1B ran once"

                let m3B = Memo.map (fun (t: TierRef) -> t.Tier * 10) m1B
                Expect.equal f1RunsB.Value 1 "attaching m3B to warm m1B runs f1B 0 extra times"
                let refB = m1B.Value
                Expect.equal f1RunsB.Value 1 "reading m1B.Value still 0 extra runs"
                Expect.equal m3B.Value 10 "m3B reads shared m1B without re-running f1B"
                Expect.equal f1RunsB.Value 1 "still 1 total f1B run"
                Expect.isTrue (obj.ReferenceEquals (refB, m1B.Value)) "ReferenceEquals preserved on m1B"

                // Subsequent update runs shared f1B ONCE across both m2B and m3B!
                sB.Value <- 30
                Expect.equal m2B.Value 4 "m2B updated to 4"
                Expect.equal m3B.Value 30 "m3B updated to 30"
                Expect.equal f1RunsB.Value 2 "shared m1B ran f1B only once for sB <- 30"

                // Case 3: Warm m2C.Value first, then call m1C.Dispose() -> 0 extra f1 runs and m2C freezes at m1C's last value!
                let sC = createSignal 5
                let f1RunsC = ref 0

                let m1C =
                    Signal.map
                        (fun n ->
                            f1RunsC.Value <- f1RunsC.Value + 1
                            n * 2)
                        sC

                let m2C = Memo.map ((+) 1) m1C

                Expect.equal m2C.Value 11 "warm m2C first (5 * 2 + 1)"
                Expect.equal f1RunsC.Value 1 "f1C ran once"

                m1C.Dispose ()
                Expect.equal f1RunsC.Value 1 "m1C.Dispose() runs f1C 0 extra times"
                Expect.equal sC.ObserverCount 0 "sC has 0 observers after m1C.Dispose()"

                sC.Value <- 99
                Expect.equal m2C.Value 11 "m2C freezes at 11 after upstream m1C.Dispose()"
                Expect.equal f1RunsC.Value 1 "f1C never runs again after Dispose"

                // Case 4: Mid-chain de-fusion on a 4-stage warm chain when root is already Dirty!
                let sD = createSignal 1
                let d1 = Signal.map ((+) 1) sD
                let d2 = Memo.map ((*) 10) d1
                let d3 = Memo.map ((+) 2) d2
                let d4 = Memo.map ((*) 3) d3

                Expect.equal d4.Value 66 "warm d4 (((1+1)*10 + 2)*3 = 66)"
                sD.Value <- 2
                // De-fuse at d2 while d4 is FreshDirty!
                Expect.equal d2.Value 30 "de-fusing d2 while dirty computes (2+1)*10 = 30"
                Expect.equal d4.Value 96 "d4 sees updated d2 (((2+1)*10 + 2)*3 = 96)"
            }

            test "cross-owner boundary does not fuse lifecycle across distinct owners" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 2
                let outerM1 = Signal.map ((+) 1) s

                let mutable innerOwner: Owner = Unchecked.defaultof<_>
                let mutable innerM2: Memo<int> = Unchecked.defaultof<_>

                createRoot (fun o ->
                    innerOwner <- o
                    innerM2 <- Memo.map ((*) 10) outerM1)

                Expect.equal innerM2.Value 30 "innerM2 reads outerM1 ((2+1)*10 = 30)"
                innerOwner.Dispose ()

                // Disposing innerOwner disposes innerM2, leaving outerM1 alive and reactive!
                s.Value <- 5
                Expect.equal outerM1.Value 6 "outerM1 remains alive and updates after innerOwner.Dispose()"
            }

#if RANVIER_TRACE
            test "Trace.reconcile holds across multi-stage Signal.map and Memo.map chains" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 10

                let tail =
                    s
                    |> Signal.map (fun x -> x / 10)
                    |> Memo.map ((+) 1)
                    |> Memo.map ((*) 2)
                    |> Memo.map ((+) 5)

                let log = ResizeArray<int>()
                let eff = new Effect (g, fun () -> log.Add tail.Value)
                Expect.sequenceEqual log [ 9 ] "initial tail (((1+1)*2)+5 = 9)"

                s.Value <- 14
                Expect.sequenceEqual log [ 9 ] "cutoff at stage 1"

                s.Value <- 25
                Expect.sequenceEqual log [ 9; 11 ] "propagated (((2+1)*2)+5 = 11)"

                eff.Dispose ()
                tail.Dispose ()

                Expect.isEmpty (Trace.reconcile g) "live edges match folded trace log"
            }
#endif
        ]
