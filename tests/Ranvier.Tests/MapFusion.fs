module Ranvier.Tests.MapFusion

open System
open System.Collections.Generic
open Expecto
open Ranvier
open Ranvier.Tests.Streams

[<AllowNullLiteral>]
type private TierRef(tier: int) =
    member _.Tier = tier

[<CustomEquality; NoComparison>]
type private PendingCompareVal =
    | PendingCompareVal of id: int * pending: AsyncSource<int> * throwPending: bool

    override this.Equals(other: obj) =
        match other with
        | :? PendingCompareVal as that ->
            let (PendingCompareVal (a, pa, ta)) = this
            let (PendingCompareVal (b, pb, tb)) = that

            if ta then
                pa.Value |> ignore
            elif tb then
                pb.Value |> ignore

            a = b
        | _ -> false

    override _.GetHashCode() = 0

[<CustomEquality; NoComparison>]
type private DeFuseCompareVal =
    | DeFuseCompareVal of key: int * payload: int * onCompare: (unit -> unit)

    override this.Equals(other: obj) =
        match other with
        | :? DeFuseCompareVal as that ->
            let (DeFuseCompareVal (ka, _, cbA)) = this
            let (DeFuseCompareVal (kb, _, cbB)) = that
            cbA ()
            cbB ()
            ka = kb
        | _ -> false

    override _.GetHashCode() = 0

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

            test "tail.Dispose preserves FreshDirty, Pending, and Failed state on live intermediates across 2..5 stage chains" {
                use g = new Graph ()
                use _ = g.Activate ()

                // 1. FreshDirty Signal & 5-stage chains: s mutates after warmup, then tail.Dispose() runs before reading intermediates.
                let s = createSignal 1
                let m1 = Signal.map ((+) 1) s
                let m2 = Memo.map ((*) 10) m1
                let m3 = Memo.map ((+) 2) m2
                let m4 = Memo.map ((*) 3) m3
                let m5 = Memo.map ((+) 1) m4

                Expect.equal m5.Value 67 "warm 5-stage chain ((((1+1)*10 + 2)*3)+1 = 67)"
                s.Value <- 4
                m5.Dispose ()

                Expect.equal m1.Value 5 "m1 recomputes from s=4 after dirty m5.Dispose()"
                Expect.equal m2.Value 50 "m2 recomputes from s=4 after dirty m5.Dispose()"
                Expect.equal m3.Value 52 "m3 recomputes from s=4 after dirty m5.Dispose()"
                Expect.equal m4.Value 156 "m4 recomputes from s=4 after dirty m5.Dispose()"

                // Also verify 3-stage Signal chain dirty tail.Dispose()
                let s3 = createSignal 2
                let a1 = Signal.map ((+) 1) s3
                let a2 = Memo.map ((*) 10) a1
                let a3 = Memo.map ((+) 5) a2
                Expect.equal a3.Value 35 "warm 3-stage chain"
                s3.Value <- 6
                a3.Dispose ()
                Expect.equal a1.Value 7 "a1 recomputes after dirty a3.Dispose()"
                Expect.equal a2.Value 70 "a2 recomputes after dirty a3.Dispose()"

                // 2. Pending chains (2, 3, 4, and 5 stages): tail.Dispose() leaves live intermediates in Status.Pending, not Status.Error!
                let pendSrc = createAsyncSource<int>()
                let p1 = AsyncSource.map ((+) 1) pendSrc
                let p2 = Memo.map ((*) 2) p1
                let p3 = Memo.map ((+) 3) p2
                let p4 = Memo.map ((*) 4) p3
                let p5 = Memo.map ((+) 5) p4

                Expect.equal p5.TryValue Pending "5-stage pending before tail.Dispose()"
                p5.Dispose ()

                Expect.equal p1.Status Status.Pending "p1 stays Status.Pending after p5.Dispose()"
                Expect.equal p2.Status Status.Pending "p2 stays Status.Pending after p5.Dispose()"
                Expect.equal p3.Status Status.Pending "p3 stays Status.Pending after p5.Dispose()"
                Expect.equal p4.Status Status.Pending "p4 stays Status.Pending after p5.Dispose()"
                Expect.equal p1.TryValue Pending "p1.TryValue is Pending"
                Expect.equal p4.TryValue Pending "p4.TryValue is Pending"
                Expect.sequenceEqual (Seq.toArray p1.PendingSources) [| pendSrc :> INode |] "p1.PendingSources points to pendSrc"
                Expect.sequenceEqual (Seq.toArray p4.PendingSources) [| p3 :> INode |] "p4.PendingSources points to p3"

                pendSrc.Settle 10
                Expect.equal p4.Value 100 "p4 settles (((10+1)*2 + 3)*4 = 100) after p5.Dispose()"

                // 3. Failed chains (root failure and stage-2 throw): tail.Dispose() preserves FailureField and ErrorOrigin without NRE!
                let failSrc = createAsyncSource<int>()
                let rootErr = InvalidOperationException "root-boom"
                failSrc.Fail rootErr
                let f1 = AsyncSource.map ((+) 1) failSrc
                let f2 = Memo.map ((*) 2) f1
                let f3 = Memo.map ((+) 3) f2
                let f4 = Memo.map ((*) 4) f3

                Expect.isTrue (obj.ReferenceEquals (failedWith f4.TryValue, rootErr)) "f4 surfaces rootErr"
                f4.Dispose ()

                Expect.isTrue (obj.ReferenceEquals (failedWith f1.TryValue, rootErr)) "f1 preserves rootErr after f4.Dispose()"
                Expect.isTrue (obj.ReferenceEquals (failedWith f3.TryValue, rootErr)) "f3 preserves rootErr after f4.Dispose()"
                Expect.isTrue (obj.ReferenceEquals (f3.ErrorOrigin, failSrc :> INode)) "f3 preserves ErrorOrigin = failSrc"

                let sThrow = createSignal 1
                let stageErr = InvalidOperationException "stage2-boom"
                let st1 = Signal.map ((+) 1) sThrow
                let st2 = Memo.map (fun x -> if x = 2 then raise stageErr else x * 10) st1
                let st3 = Memo.map ((+) 5) st2
                let st4 = Memo.map ((*) 2) st3

                Expect.isTrue (obj.ReferenceEquals (failedWith st4.TryValue, stageErr)) "st4 surfaces stageErr"
                st4.Dispose ()

                Expect.equal st1.Value 2 "st1 is Ready 2 after st4.Dispose()"
                Expect.isTrue (obj.ReferenceEquals (failedWith st2.TryValue, stageErr)) "st2 preserves stageErr"
                Expect.isTrue (obj.ReferenceEquals (st2.ErrorOrigin, st2 :> INode)) "st2 ErrorOrigin = st2"
                Expect.isTrue (obj.ReferenceEquals (failedWith st3.TryValue, stageErr)) "st3 preserves stageErr"
                Expect.isTrue (obj.ReferenceEquals (st3.ErrorOrigin, st2 :> INode)) "st3 ErrorOrigin = st2"
            }

            test "mid-execution de-fusion inside stage callbacks preserves intermediate state without false cutoffs or StackOverflowException" {
                use g = new Graph ()
                use _ = g.Activate ()

                // (a) Leaf & observed 3-stage and 5-stage chains: f2 reads m1.Value on Run 2, then s reverts to Run 1's input!
                for observe in [ false; true ] do
                    let s = createSignal 10
                    let m1 = Signal.map ((+) 1) s
                    let m2 = Memo.map (fun v -> if v > 15 then m1.Value * 2 else v * 2) m1
                    let m3 = Memo.map ((+) 100) m2

                    let obsLog = ResizeArray<int>()

                    let eff =
                        if observe then
                            Some (new Effect (g, fun () -> obsLog.Add m3.Value))
                        else
                            None

                    Expect.equal m3.Value 122 $"Run 1 (observe={observe})"
                    s.Value <- 20
                    Expect.equal m3.Value 142 $"Run 2 de-fused inside f2 (observe={observe})"
                    Expect.equal m1.Value 21 $"m1 holds Run 2 value 21 (observe={observe})"
                    Expect.equal m2.Value 42 $"m2 holds Run 2 value 42, not Run 1's 22 (observe={observe})"

                    // Revert s back to 10 -> m2 MUST recompute 22 and NOT falsely cut off against stale 22!
                    s.Value <- 10
                    Expect.equal m2.Value 22 $"Run 3 m2 updated back to 22 (observe={observe})"
                    Expect.equal m3.Value 122 $"Run 3 m3 updated back to 122 (observe={observe})"

                    eff |> Option.iter (fun e -> e.Dispose ())

                // 5-stage mid-execution de-fusion inside stage 2 on Run 2, then revert!
                let s5 = createSignal 10
                let c1 = Signal.map ((+) 1) s5
                let c2 = Memo.map (fun v -> if v > 15 then c1.Value * 2 else v * 2) c1
                let c3 = Memo.map ((+) 3) c2
                let c4 = Memo.map ((*) 2) c3
                let c5 = Memo.map ((+) 1) c4

                Expect.equal c5.Value 51 "5-stage Run 1 ((((11*2)+3)*2)+1 = 51)"
                s5.Value <- 20
                Expect.equal c5.Value 91 "5-stage Run 2 de-fused in f2 ((((21*2)+3)*2)+1 = 91)"
                Expect.equal c2.Value 42 "5-stage c2 holds 42"
                s5.Value <- 10
                Expect.equal c5.Value 51 "5-stage Run 3 reverts to 51 without false cutoff"

                // (b) Stage f1 self-reading m1.Value on Run 1 (and Run 2) without StackOverflowException!
                for observe in [ false; true ] do
                    let sSelf = createSignal 5
                    let mutable self1: Memo<int> = Unchecked.defaultof<_>
                    let r1 = Signal.map (fun x -> self1.Value + x) sSelf
                    self1 <- r1
                    let r2 = Memo.map ((*) 10) r1

                    let eff =
                        if observe then
                            Some (new Effect (g, fun () -> r2.Value |> ignore))
                        else
                            None

                    Expect.equal r2.Value 50 $"Run 1 self-read yields default 0 + 5 -> 50 (observe={observe})"
                    Expect.equal r1.Value 5 $"r1.Value is 5 after Run 1 (observe={observe})"
                    Expect.equal r1.Runs 1 $"r1 ran once on Run 1 (observe={observe})"

                    sSelf.Value <- 7
                    Expect.equal r2.Value 120 $"Run 2 self-read yields previous 5 + 7 -> 120 (observe={observe})"
                    Expect.equal r1.Value 12 $"r1.Value is 12 after Run 2 (observe={observe})"

                    eff |> Option.iter (fun e -> e.Dispose ())
            }

            test "EvalSignalLeaf commits Memo.value before settling owed flush, and custom comparer throw is unwrapped" {
                // (a) Signal write inside leaf EvalSignalLeaf schedules an effect that reads tail.Value during LeaveUntrackedPureTop!
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 1
                let side = createSignal 0
                let seen = ResizeArray<int>()
                let mutable tailRef: Memo<int> = Unchecked.defaultof<_>

                let eff =
                    new Effect (
                        g,
                        fun () ->
                            if side.Value > 0 then
                                seen.Add tailRef.Value
                    )

                let tail =
                    s
                    |> Signal.map ((+) 1)
                    |> Memo.map (fun x ->
                        if x > 5 then
                            side.Value <- x

                        x * 10)

                tailRef <- tail
                Expect.equal tail.Value 20 "Run 1 warms leaf chain to 20"
                Expect.isEmpty seen "side is still 0"

                // Run 2 enters EvalSignalLeaf: writing side.Value <- 11 owes a flush settled on exit; eff MUST read 110, not stale 20!
                s.Value <- 10
                Expect.equal tail.Value 110 "Run 2 leaf returns 110"
                Expect.sequenceEqual seen [ 110 ] "owed flush saw updated tail.Value 110, not pre-commit 20"
                eff.Dispose ()

                // (b) Custom comparer throwing NotReadyException on leaf chain records raw exception without mapPending wrapper!
                use gEq =
                    new Graph (
                        { GraphOptions.Default with
                            Equality = StructuralPolicy ()
                        }
                    )

                use _ = gEq.Activate ()
                let sEq = createSignal 1
                let pend = createAsyncSource<int>()
                let arm = ref false

                let leafEq =
                    sEq
                    |> Signal.map ((+) 1)
                    |> Memo.map (fun x -> PendingCompareVal (x, pend, arm.Value))

                let (PendingCompareVal (initId, _, _)) = leafEq.Value
                Expect.equal initId 2 "Run 1 succeeds"

                arm.Value <- true
                sEq.Value <- 2
                let compEx = failedWith leafEq.TryValue
                Expect.equal leafEq.Status Status.Error "comparer throw puts leaf memo in Status.Error"
                Expect.isFalse (compEx.Message.Contains "mapping attempted to read a pending source") "comparer throw is not wrapped as mapPending"
                Expect.isTrue (obj.ReferenceEquals (leafEq.ErrorOrigin, leafEq :> INode)) "ErrorOrigin is leafEq"
            }

            test "5-stage FusedChainMap refreshes root outside Pure, clears root lastRaised, and tracks stage-3 unready transitions" {
                use g = new Graph ()
                use _ = g.Activate ()

                // (a) 5-stage chain over an owning createMemoWith root that catches an upstream error
                let s = createSignal 1
                let rootCleans = ResizeArray<int>()
                let caughtEx = ref (null: exn)

                let failingUp: Memo<int> =
                    createMemo (fun _ -> raise (InvalidOperationException "upstream-caught"))

                let owningRoot =
                    createMemoWith (fun _ ->
                        let v = s.Value
                        onCleanup (fun () -> rootCleans.Add v)

                        let fallback =
                            try
                                failingUp.Value
                            with ex ->
                                caughtEx.Value <- ex
                                0

                        v + fallback)

                let tail5 =
                    owningRoot
                    |> Memo.map ((+) 1)
                    |> Memo.map ((+) 1)
                    |> Memo.map ((+) 1)
                    |> Memo.map ((+) 1)
                    |> Memo.map (fun x -> if x > 10 then raise caughtEx.Value else x)

                Expect.equal tail5.Value 5 "owningRoot refreshed outside Pure (onCleanup allowed)"
                s.Value <- 20
                let ex5 = failedWith tail5.TryValue
                Expect.equal ex5.Message "upstream-caught" "stage 5 rethrew caught exception"
                Expect.sequenceEqual rootCleans [ 1 ] "owningRoot discharged Run 1 scope"
                Expect.isTrue (obj.ReferenceEquals (tail5.ErrorOrigin, tail5 :> INode)) "ErrorOrigin is tail5, not failingUp"

                // (b) Stage-3 Ready -> Error transition when stage-4 was already in Error with the same exception
                let mode = createSignal 1
                let sharedErr = InvalidOperationException "shared-err"
                let k1 = Signal.map ((+) 1) mode
                let k2 = Memo.map ((+) 1) k1

                let k3 = Memo.map (fun x -> if x = 4 then raise sharedErr else x + 1) k2

                let k4 = Memo.map (fun x -> if x = 4 then raise sharedErr else x + 1) k3

                let k5 = Memo.map ((+) 1) k4

                // mode = 1 -> k1=2, k2=3, k3=4 (Ready!), k4 throws sharedErr!
                Expect.isTrue (obj.ReferenceEquals (failedWith k5.TryValue, sharedErr)) "Run 1 k4 throws sharedErr"

                // mode = 2 -> k1=3, k2=4, k3 throws sharedErr (k3 transitions Ready -> Error!)
                mode.Value <- 2
                Expect.isTrue (obj.ReferenceEquals (failedWith k5.TryValue, sharedErr)) "Run 2 k3 throws sharedErr"
                Expect.equal k4.Runs 2 "k4 incremented runs when k3 transitioned Ready -> Error (Signal root)"
                Expect.isTrue (obj.ReferenceEquals (k4.ErrorOrigin, k3 :> INode)) "Run 2 k4.ErrorOrigin moves to k3"
                Expect.equal k5.Runs 1 "k5 cuts off on identical Failure.ErrorOf when k4 was already unready"

                // Same stage-3 Ready -> Error transition over a Memo root (FusedSourceMap4.EvalPrefix)
                let modeSrc = createSignal 1
                let rootMemo = createMemo (fun _ -> modeSrc.Value)
                let s1 = Memo.map ((+) 1) rootMemo
                let s2 = Memo.map ((+) 1) s1

                let s3 = Memo.map (fun x -> if x = 4 then raise sharedErr else x + 1) s2

                let s4 = Memo.map (fun x -> if x = 4 then raise sharedErr else x + 1) s3

                let s5 = Memo.map ((+) 1) s4
                Expect.isTrue (obj.ReferenceEquals (failedWith s5.TryValue, sharedErr)) "Run 1 s4 throws sharedErr"
                modeSrc.Value <- 2
                Expect.isTrue (obj.ReferenceEquals (failedWith s5.TryValue, sharedErr)) "Run 2 s3 throws sharedErr"
                Expect.equal s4.Runs 2 "s4 incremented runs when s3 transitioned Ready -> Error (Memo root)"
                Expect.isTrue (obj.ReferenceEquals (s4.ErrorOrigin, s3 :> INode)) "Run 2 s4.ErrorOrigin moves to s3"
                Expect.equal s5.Runs 1 "s5 cuts off on identical Failure.ErrorOf when s4 was already unready"
            }

            test "de-fusion inside upstream root refresh preserves intermediate state across 3-stage and 5-stage chains on Run 1 and Run 2" {
                use g = new Graph ()
                use _ = g.Activate ()

                // (a) Run 1 de-fusion inside Memo root on 3-stage chain
                let s3a = createSignal 10
                let mutable readM1_3a: unit -> int = fun () -> -1
                let seenInRoot3a = ResizeArray<int>()
                let f1Calls3a = ref 0

                let root3a =
                    createMemo (fun _ ->
                        let v = s3a.Value

                        if v = 10 then
                            seenInRoot3a.Add (readM1_3a ())

                        v)

                let m1_3a =
                    Memo.map
                        (fun x ->
                            f1Calls3a.Value <- f1Calls3a.Value + 1
                            x + 1)
                        root3a

                let m2_3a = Memo.map ((+) 1) m1_3a
                let m3_3a = Memo.map ((+) 1) m2_3a
                readM1_3a <- fun () -> m1_3a.Value

                Expect.equal m3_3a.Value 13 "3-stage Run 1 tail = 13"
                Expect.sequenceEqual seenInRoot3a [ 0 ] "3-stage Run 1 root saw default 0 on active/waiting m1"
                Expect.equal f1Calls3a.Value 1 "f1 ran once on Run 1 (not twice)"
                Expect.equal m1_3a.Value 11 "m1 = 11"
                Expect.equal m2_3a.Value 12 "m2 = 12"
                s3a.Value <- 20
                Expect.equal m3_3a.Value 23 "3-stage subsequent write updates to 23"

                // (b) Run 1 de-fusion inside Memo root on 5-stage chain
                let s5a = createSignal 10
                let mutable readM1_5a: unit -> int = fun () -> -1
                let seenInRoot5a = ResizeArray<int>()
                let f1Calls5a = ref 0

                let root5a =
                    createMemo (fun _ ->
                        let v = s5a.Value

                        if v = 10 then
                            seenInRoot5a.Add (readM1_5a ())

                        v)

                let m1_5a =
                    Memo.map
                        (fun x ->
                            f1Calls5a.Value <- f1Calls5a.Value + 1
                            x + 1)
                        root5a

                let m2_5a = Memo.map ((+) 1) m1_5a
                let m3_5a = Memo.map ((+) 1) m2_5a
                let m4_5a = Memo.map ((+) 1) m3_5a
                let m5_5a = Memo.map ((+) 1) m4_5a
                readM1_5a <- fun () -> m1_5a.Value

                Expect.equal m5_5a.Value 15 "5-stage Run 1 tail = 15"
                Expect.sequenceEqual seenInRoot5a [ 0 ] "5-stage Run 1 root saw default 0"
                Expect.equal f1Calls5a.Value 1 "5-stage f1 ran once on Run 1"
                Expect.equal m4_5a.Value 14 "5-stage m4 = 14 (not wedged in ActiveBody)"
                s5a.Value <- 20
                Expect.equal m4_5a.Value 24 "5-stage m4 updates to 24 on subsequent write"
                Expect.equal m5_5a.Value 25 "5-stage m5 updates to 25 on subsequent write"

                // (c) Run 2 de-fusion inside Memo root when tail is FreshDirty on 3-stage and 5-stage chains
                let s3b = createSignal 10
                let arm3b = ref false
                let mutable readM1_3b: unit -> int = fun () -> -1
                let f1Calls3b = ref 0

                let root3b =
                    createMemo (fun _ ->
                        let v = s3b.Value

                        if arm3b.Value then
                            arm3b.Value <- false
                            readM1_3b () |> ignore

                        v)

                let m1_3b =
                    Memo.map
                        (fun x ->
                            f1Calls3b.Value <- f1Calls3b.Value + 1
                            x + 1)
                        root3b

                let m2_3b = Memo.map ((+) 1) m1_3b
                let m3_3b = Memo.map ((+) 1) m2_3b
                readM1_3b <- fun () -> m1_3b.Value

                Expect.equal m3_3b.Value 13 "3-stage Run 1 = 13"
                s3b.Value <- 15
                Expect.equal root3b.Value 15 "root3b moves to 15, marking fused m3_3b FreshDirty"
                arm3b.Value <- true
                s3b.Value <- 20
                Expect.equal m3_3b.Value 23 "3-stage Run 2 tail = 23"
                Expect.equal m1_3b.Value 21 "3-stage Run 2 m1 = 21"
                Expect.equal f1Calls3b.Value 2 "3-stage f1 ran twice total (no dead fused run)"
                arm3b.Value <- false
                s3b.Value <- 30
                Expect.equal m3_3b.Value 33 "3-stage Run 3 tail = 33"

                let s5b = createSignal 10
                let arm5b = ref false
                let mutable readM1_5b: unit -> int = fun () -> -1
                let f1Calls5b = ref 0

                let root5b =
                    createMemo (fun _ ->
                        let v = s5b.Value

                        if arm5b.Value then
                            arm5b.Value <- false
                            readM1_5b () |> ignore

                        v)

                let m1_5b =
                    Memo.map
                        (fun x ->
                            f1Calls5b.Value <- f1Calls5b.Value + 1
                            x + 1)
                        root5b

                let m2_5b = Memo.map ((+) 1) m1_5b
                let m3_5b = Memo.map ((+) 1) m2_5b
                let m4_5b = Memo.map ((+) 1) m3_5b
                let m5_5b = Memo.map ((+) 1) m4_5b
                readM1_5b <- fun () -> m1_5b.Value

                Expect.equal m5_5b.Value 15 "5-stage Run 1 = 15"
                s5b.Value <- 15
                Expect.equal root5b.Value 15 "root5b moves to 15, marking fused m5_5b FreshDirty"
                arm5b.Value <- true
                s5b.Value <- 20
                Expect.equal m5_5b.Value 25 "5-stage Run 2 tail = 25"
                Expect.equal m4_5b.Value 24 "5-stage Run 2 m4 = 24"
                Expect.equal f1Calls5b.Value 2 "5-stage f1 ran twice total (no dead fused run)"
                arm5b.Value <- false
                s5b.Value <- 30
                Expect.equal m4_5b.Value 34 "5-stage Run 3 m4 = 34 (not wedged in ActiveBody)"
                Expect.equal m5_5b.Value 35 "5-stage Run 3 m5 = 35"

                // (d) Run 2 de-fusion inside Memo root when tail is FreshCheck (direct signal write -> tail.Value)
                let s5c = createSignal 10
                let arm5c = ref false
                let mutable readM1_5c: unit -> int = fun () -> -1
                let seenInRoot5c = ResizeArray<int>()
                let f1Calls5c = ref 0

                let root5c =
                    createMemo (fun _ ->
                        let v = s5c.Value

                        if arm5c.Value then
                            arm5c.Value <- false
                            seenInRoot5c.Add (readM1_5c ())

                        v)

                let m1_5c =
                    Memo.map
                        (fun x ->
                            f1Calls5c.Value <- f1Calls5c.Value + 1
                            x + 1)
                        root5c

                let m2_5c = Memo.map ((+) 1) m1_5c
                let m3_5c = Memo.map ((+) 1) m2_5c
                let m4_5c = Memo.map ((+) 1) m3_5c
                let m5_5c = Memo.map ((+) 1) m4_5c
                readM1_5c <- fun () -> m1_5c.Value

                Expect.equal m5_5c.Value 15 "5-stage FreshCheck Run 1 = 15"
                arm5c.Value <- true
                s5c.Value <- 20
                Expect.equal m5_5c.Value 25 "5-stage FreshCheck Run 2 tail = 25"
                Expect.sequenceEqual seenInRoot5c [ 11 ] "5-stage FreshCheck Run 2 root saw cached m1 = 11"
                Expect.equal f1Calls5c.Value 2 "5-stage FreshCheck f1 ran twice total"
                Expect.equal m4_5c.Value 24 "5-stage FreshCheck m4 = 24"
                s5c.Value <- 30
                Expect.equal m5_5c.Value 35 "5-stage FreshCheck Run 3 tail = 35"
            }

            test "de-fusion inside intermediate custom equality comparer preserves values on both true and false comparisons" {
                use g =
                    new Graph (
                        { GraphOptions.Default with
                            Equality = StructuralPolicy ()
                        }
                    )

                use _ = g.Activate ()

                let checkChain (useLeaf: bool) (expectEqualOnRun2: bool) =
                    let s = createSignal 10
                    let arm = ref false

                    let mutable readM1: unit -> DeFuseCompareVal =
                        fun () -> DeFuseCompareVal (-1, -1, ignore)

                    let seenKeys = ResizeArray<int>()

                    let cb () =
                        if arm.Value then
                            arm.Value <- false
                            let (DeFuseCompareVal (k, _, _)) = readM1 ()
                            seenKeys.Add k

                    let m1 = Signal.map (fun x -> DeFuseCompareVal (x / 10, x, cb)) s
                    let m2 = Memo.map (fun (DeFuseCompareVal (k, _, _)) -> k * 100) m1
                    let m3 = Memo.map ((+) 1) m2
                    readM1 <- fun () -> m1.Value

                    let obs = ResizeArray<int>()

                    let effOpt =
                        if useLeaf then
                            None
                        else
                            Some (new Effect (g, fun () -> obs.Add m3.Value))

                    if useLeaf then
                        Expect.equal m3.Value 101 "Run 1 leaf m3 = 101"
                    else
                        Expect.sequenceEqual obs [ 101 ] "Run 1 observed m3 = 101"

                    arm.Value <- true
                    s.Value <- if expectEqualOnRun2 then 14 else 24

                    let expectedRun2 = if expectEqualOnRun2 then 101 else 201

                    if useLeaf then
                        Expect.equal m3.Value expectedRun2 "Run 2 leaf m3"
                    else
                        Expect.equal m3.Value expectedRun2 "Run 2 observed m3"

                    Expect.sequenceEqual seenKeys [ 1 ] "comparer read pre-commit m1 key = 1"
                    let (DeFuseCompareVal (kAfter2, pAfter2, _)) = m1.Value
                    Expect.equal kAfter2 (if expectEqualOnRun2 then 1 else 2) "m1 key after Run 2 is not default"
                    Expect.equal pAfter2 (if expectEqualOnRun2 then 14 else 24) "m1 payload committed v1"

                    // Run 3 verifies neither m1 nor m2 is wedged in ActiveBody / ActiveCheck
                    s.Value <- 35
                    Expect.equal m3.Value 301 "Run 3 m3 updates to 301"
                    let (DeFuseCompareVal (kAfter3, _, _)) = m1.Value
                    Expect.equal kAfter3 3 "Run 3 m1 key updates to 3"
                    effOpt |> Option.iter (fun e -> e.Dispose ())

                checkChain true true
                checkChain true false
                checkChain false true
                checkChain false false

                // Also verify stage-4 (prevMemo) custom comparer on a 5-stage FusedChainMap
                for expectEqualOnRun2 in [ true; false ] do
                    let s5 = createSignal 10
                    let arm5 = ref false
                    let mutable readK1: unit -> int = fun () -> -1

                    let cb5 () =
                        if arm5.Value then
                            arm5.Value <- false
                            readK1 () |> ignore

                    let k1 = Signal.map ((+) 0) s5
                    let k2 = Memo.map ((+) 0) k1
                    let k3 = Memo.map ((+) 0) k2
                    let k4 = Memo.map (fun x -> DeFuseCompareVal (x / 10, x, cb5)) k3
                    let k5 = Memo.map (fun (DeFuseCompareVal (k, _, _)) -> k * 100 + 5) k4
                    readK1 <- fun () -> k1.Value

                    Expect.equal k5.Value 105 "5-stage Run 1 = 105"
                    arm5.Value <- true
                    s5.Value <- if expectEqualOnRun2 then 16 else 26
                    Expect.equal k5.Value (if expectEqualOnRun2 then 105 else 205) "5-stage Run 2"
                    let (DeFuseCompareVal (k4Key, k4Payload, _)) = k4.Value
                    Expect.equal k4Key (if expectEqualOnRun2 then 1 else 2) "k4 key after Run 2"
                    Expect.equal k4Payload (if expectEqualOnRun2 then 16 else 26) "k4 payload committed"
                    s5.Value <- 40
                    Expect.equal k5.Value 405 "5-stage Run 3 = 405 (not wedged)"
            }

            test "EvalSignalLeaf returns pre-flush snapshot when woken effect mutates root signal during owed flush" {
                use g = new Graph ()
                use _ = g.Activate ()

                let s = createSignal 1
                let side = createSignal 0
                let effSeen = ResizeArray<int>()
                let mutable tailRef: Memo<int> = Unchecked.defaultof<_>

                let eff =
                    new Effect (
                        g,
                        fun () ->
                            let sv = side.Value

                            if sv = 1 then
                                effSeen.Add (g.Untrack (fun () -> tailRef.Value))
                                // Mutate root signal to an error state and evaluate tail inside the owed flush!
                                s.Value <- -10
                                g.Untrack (fun () -> tailRef.TryValue) |> ignore
                            elif sv = 2 then
                                effSeen.Add (g.Untrack (fun () -> tailRef.Value))
                                // Mutate root signal to a new ready value and evaluate tail inside the owed flush!
                                s.Value <- 99
                                effSeen.Add (g.Untrack (fun () -> tailRef.Value))
                    )

                let tail =
                    s
                    |> Signal.map ((+) 1)
                    |> Memo.map (fun x ->
                        if x = 11 then
                            side.Value <- 1
                        elif x = 21 then
                            side.Value <- 2

                        if x < 0 then
                            raise (InvalidOperationException "post-flush-boom")

                        x * 10)
                    |> Memo.map ((+) 5)

                tailRef <- tail
                Expect.equal tail.Value 25 "Run 1 warms leaf chain to 25"

                // Case 1: Owed flush mutates s to -10 (putting tail into Status.Error); outer tail.Value MUST still return 115!
                s.Value <- 10
                Expect.equal tail.Value 115 "outer tail.Value returns pre-flush snapshot 115 despite post-flush error"
                Expect.sequenceEqual effSeen [ 115 ] "effect saw committed 115 before mutating s"
                let postEx = failedWith tail.TryValue
                Expect.equal postEx.Message "post-flush-boom" "subsequent read observes post-flush error"

                // Case 2: Recover and test owed flush mutating s to a new ready value 99 (1005); outer tail.Value MUST return 215!
                s.Value <- 20
                Expect.equal tail.Value 215 "outer tail.Value returns pre-flush snapshot 215, not post-flush 1005"
                Expect.sequenceEqual effSeen [ 115; 215; 1005 ] "effect saw 215 then updated tail to 1005"
                Expect.equal tail.Value 1005 "subsequent read observes post-flush 1005"
                eff.Dispose ()
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
