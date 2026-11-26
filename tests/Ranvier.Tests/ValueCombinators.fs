module Ranvier.Tests.ValueCombinators

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Expecto
open Ranvier
open Ranvier.Tests.Streams

type private Flight<'T>() =
    let tcs = TaskCompletionSource<'T>()
    member _.Task = tcs.Task

    member _.Settle(v: 'T) =
        tcs.SetResult v

    member _.Fail(ex: exn) =
        tcs.SetException ex

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

let private isNullReference (ex: exn) =
#if FABLE_COMPILER
    not (isNull (box ex))
#else
    ex :? NullReferenceException
#endif

[<Tests>]
let tests =
    testList
        "ValueCombinators"
        [
            testCaseAsync "map, map2, and zip across all 5 modules work without graph.Activate and close tracking in mapping"
            <| async {
                use g = new Graph ()
                let mutable hasAmbient = true

                try
                    createSignal 0 |> ignore
                with _ ->
                    hasAmbient <- false

                Expect.isFalse hasAmbient "precondition: graph is not activated"

                let untracked = new Signal<int> (g, 100)
                let s1 = new Signal<int> (g, 2)
                let s2 = new Signal<int> (g, 3)

                let sigMap = Signal.map (fun x -> let _ = untracked.Value in x * 10) s1

                let sigMap2 = Signal.map2 (fun a b -> let _ = untracked.Value in a + b) s1 s2

                let sigZip = Signal.zip s1 s2

                let memoMap = Memo.map (fun x -> let _ = untracked.Value in x + 1) sigMap

                let memoMap2 =
                    Memo.map2 (fun a b -> let _ = untracked.Value in a * b) sigMap sigMap2

                let memoZip = Memo.zip sigMap sigMap2

                let asrc1 = new AsyncSource<int> (g)
                let asrc2 = new AsyncSource<int> (g)
                asrc1.Settle 4
                asrc2.Settle 5
                let asrcMap = AsyncSource.map (fun x -> let _ = untracked.Value in x * 2) asrc1

                let asrcMap2 =
                    AsyncSource.map2 (fun a b -> let _ = untracked.Value in a + b) asrc1 asrc2

                let asrcZip = AsyncSource.zip asrc1 asrc2

                let am1 = Make.AsyncMemo<int>(g, fun _ _ -> completed (s1.Value * 3))
                let am2 = Make.AsyncMemo<int>(g, fun _ _ -> completed (s2.Value * 4))
                let amMap = AsyncMemo.map (fun x -> let _ = untracked.Value in x + 1) am1
                let amMap2 = AsyncMemo.map2 (fun a b -> let _ = untracked.Value in a + b) am1 am2
                let amZip = AsyncMemo.zip am1 am2

                let feed1 = ManualStream<int>()
                let feed2 = ManualStream<int>()
                let sm1 = g.Run (fun () -> createStream (fun _ -> feed1.Source))
                let sm2 = g.Run (fun () -> createStream (fun _ -> feed2.Source))
                let smMap = StreamMemo.map (fun x -> let _ = untracked.Value in x * 10) sm1
                let smMap2 = StreamMemo.map2 (fun a b -> let _ = untracked.Value in a + b) sm1 sm2
                let smZip = StreamMemo.zip sm1 sm2

                Expect.equal sigMap.Value 20 "Signal.map without Activate"
                Expect.equal sigMap2.Value 5 "Signal.map2 without Activate"
                Expect.equal sigZip.Value (2, 3) "Signal.zip without Activate"
                Expect.equal memoMap.Value 21 "Memo.map without Activate"
                Expect.equal memoMap2.Value 100 "Memo.map2 without Activate"
                Expect.equal memoZip.Value (20, 5) "Memo.zip without Activate"
                Expect.equal asrcMap.Value 8 "AsyncSource.map without Activate"
                Expect.equal asrcMap2.Value 9 "AsyncSource.map2 without Activate"
                Expect.equal asrcZip.Value (4, 5) "AsyncSource.zip without Activate"

                amMap.TryValue |> ignore
                amMap2.TryValue |> ignore
                amZip.TryValue |> ignore
                do! drained ()
                Expect.equal amMap.Value 7 "AsyncMemo.map without Activate"
                Expect.equal amMap2.Value 18 "AsyncMemo.map2 without Activate"
                Expect.equal amZip.Value (6, 12) "AsyncMemo.zip without Activate"

                Expect.equal smMap.TryValue Pending "StreamMemo.map initial Pending"
                Expect.equal smMap2.TryValue Pending "StreamMemo.map2 initial Pending"
                Expect.equal smZip.TryValue Pending "StreamMemo.zip initial Pending"
                feed1.Push 7
                feed2.Push 8
                Expect.equal smMap.Value 70 "StreamMemo.map after Push"
                Expect.equal smMap2.Value 15 "StreamMemo.map2 after Push"
                Expect.equal smZip.Value (7, 8) "StreamMemo.zip after Push"

                Expect.equal untracked.ObserverCount 0 "mapping callbacks close dependency tracking"
                let runsBefore = sigMap.Runs
                untracked.Value <- 999
                Expect.equal sigMap.Value 20 "untracked signal write does not invalidate map"
                Expect.equal sigMap.Runs runsBefore "no extra run after untracked write"

                s1.Value <- 5
                Expect.equal sigMap.Value 50 "Signal.map updates after source write"
                Expect.equal sigMap2.Value 8 "Signal.map2 updates after source write"
                Expect.equal sigZip.Value (5, 3) "Signal.zip updates after source write"
            }

            test "mapping enforces ScopeMode.Pure, wraps NotReadyException, and clears lastRaised before running" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 1
                let pending = createAsyncSource<int>()
                let pureMsg = ScopeMessages.forMode ScopeMode.Pure

                // 1. Uncaught and caught purity violation inside mapping fails with ScopeMode.Pure.
                let mPureUncaught =
                    Signal.map
                        (fun x ->
                            // fsharpanalyzer: ignore-line-next RAN010
                            onCleanup ignore
                            x)
                        s

                let exPure1 = failedWith mPureUncaught.TryValue
                Expect.isTrue (isInvalidOperation exPure1) "Pure violation raises InvalidOperationException"
                Expect.equal exPure1.Message pureMsg "Pure violation message"

                let mPureCaught =
                    Signal.map2
                        (fun a b ->
                            (try
                                // fsharpanalyzer: ignore-line-next RAN010
                                createEffect ignore
                             with _ ->
                                 ())

                            a + b)
                        s
                        s

                let exPure2 = failedWith mPureCaught.TryValue
                Expect.isTrue (isInvalidOperation exPure2) "Caught Pure violation still fails"
                Expect.equal exPure2.Message pureMsg "Caught Pure violation message"

                // 2. Synchronous NotReadyException inside mapping wraps into InvalidOperationException with ErrorOrigin = self.
                let mPendingLeak = Signal.map (fun x -> x + pending.Value) s
                let exLeak = failedWith mPendingLeak.TryValue
                Expect.equal mPendingLeak.Status Status.Error "NotReadyException in mapping fails node"
                Expect.isTrue (obj.ReferenceEquals (mPendingLeak.ErrorOrigin, mPendingLeak)) "ErrorOrigin is map memo"
                Expect.isTrue (isInvalidOperation exLeak) "wrapped in InvalidOperationException"

                let hasNotReadyInner =
                    match exLeak.InnerException with
                    | NotReadyException src -> obj.ReferenceEquals (src, pending)
                    | _ -> false

                Expect.isTrue hasNotReadyInner "InnerException preserves NotReadyException"

                // 3. Calling a combinator inside a pure createMemo body fails the enclosing pure memo.
                let mConstructInPure =
                    createMemo (fun _ ->
                        let child = Signal.map ((+) 1) s
                        child.Value)

                let exConstruct = failedWith mConstructInPure.TryValue
                Expect.equal exConstruct.Message pureMsg "Constructing combinator in pure memo fails with Pure message"

                // 4. Clearing lastRaised before untracked compute/mapping prevents misattributing a caught upstream exception.
                let sharedBoom = InvalidOperationException "upstream-boom"
                let upstreamFail: Memo<int> = createMemo (fun _ -> raise sharedBoom)

                let splitRethrow =
                    createMemoOn
                        (fun () ->
                            try
                                upstreamFail.Value
                            with _ ->
                                s.Value)
                        (fun _ -> raise sharedBoom)

                let _ = failedWith splitRethrow.TryValue

                Expect.isTrue
                    (obj.ReferenceEquals (splitRethrow.ErrorOrigin, splitRethrow))
                    "createMemoOn Phase 2 rethrow attributes ErrorOrigin to self"

                let caughtGate =
                    createMemo (fun _ ->
                        try
                            upstreamFail.Value
                        with _ ->
                            s.Value)

                let mapRethrow = Memo.map (fun _ -> raise sharedBoom) caughtGate
                let _ = failedWith mapRethrow.TryValue
                Expect.isTrue (obj.ReferenceEquals (mapRethrow.ErrorOrigin, mapRethrow)) "Memo.map rethrow attributes ErrorOrigin to self"
            }

            test "cross-graph validation on map2, zip, Signal.bind, and Memo.bind" {
                use g1 = new Graph ()
                use g2 = new Graph ()

                let s1 = new Signal<int> (g1, 1)
                let s2 = new Signal<int> (g2, 2)
                let m1 = Signal.map id s1
                let m2 = Signal.map id s2
                let as1 = new AsyncSource<int> (g1)
                let as2 = new AsyncSource<int> (g2)
                as1.Settle 1
                as2.Settle 2
                let am1 = Make.AsyncMemo<int>(g1, fun _ _ -> completed 1)
                let am2 = Make.AsyncMemo<int>(g2, fun _ _ -> completed 2)
                let feed = ManualStream<int>()
                let sm1 = g1.Run (fun () -> createStream (fun _ -> feed.Source))
                let sm2 = g2.Run (fun () -> createStream (fun _ -> feed.Source))

                let expectCrossGraph (f: unit -> unit) (label: string) =
                    let mutable threw = false

                    try
                        f ()
                    with ex ->
                        threw <- isInvalidOperation ex

                    Expect.isTrue threw label

                expectCrossGraph (fun () -> Signal.map2 (+) s1 s2 |> ignore) "Signal.map2 cross-graph"
                expectCrossGraph (fun () -> Signal.zip s1 s2 |> ignore) "Signal.zip cross-graph"
                expectCrossGraph (fun () -> Memo.map2 (+) m1 m2 |> ignore) "Memo.map2 cross-graph"
                expectCrossGraph (fun () -> Memo.zip m1 m2 |> ignore) "Memo.zip cross-graph"
                expectCrossGraph (fun () -> AsyncSource.map2 (+) as1 as2 |> ignore) "AsyncSource.map2 cross-graph"
                expectCrossGraph (fun () -> AsyncSource.zip as1 as2 |> ignore) "AsyncSource.zip cross-graph"
                expectCrossGraph (fun () -> AsyncMemo.map2 (+) am1 am2 |> ignore) "AsyncMemo.map2 cross-graph"
                expectCrossGraph (fun () -> AsyncMemo.zip am1 am2 |> ignore) "AsyncMemo.zip cross-graph"
                expectCrossGraph (fun () -> StreamMemo.map2 (+) sm1 sm2 |> ignore) "StreamMemo.map2 cross-graph"
                expectCrossGraph (fun () -> StreamMemo.zip sm1 sm2 |> ignore) "StreamMemo.zip cross-graph"

                // Signal.bind and Memo.bind returning cross-graph Memo or null fail the bind node with ErrorOrigin = self.
                let sigBindCross = Signal.bind (fun _ -> m2) s1
                let exCross1 = failedWith sigBindCross.TryValue
                Expect.isTrue (isInvalidOperation exCross1) "Signal.bind cross-graph fails with InvalidOperationException"
                Expect.isTrue (obj.ReferenceEquals (sigBindCross.ErrorOrigin, sigBindCross)) "Signal.bind cross-graph ErrorOrigin is self"

                let memoBindCross = Memo.bind (fun _ -> m2) m1
                let exCross2 = failedWith memoBindCross.TryValue
                Expect.isTrue (isInvalidOperation exCross2) "Memo.bind cross-graph fails with InvalidOperationException"
                Expect.isTrue (obj.ReferenceEquals (memoBindCross.ErrorOrigin, memoBindCross)) "Memo.bind cross-graph ErrorOrigin is self"

                let sigBindNull = Signal.bind (fun _ -> Unchecked.defaultof<Memo<int>>) s1
                let exNull1 = failedWith sigBindNull.TryValue
                Expect.isTrue (isNullReference exNull1) "Signal.bind null return fails with NullReferenceException"
                Expect.isTrue (obj.ReferenceEquals (sigBindNull.ErrorOrigin, sigBindNull)) "Signal.bind null ErrorOrigin is self"

                let memoBindNull = Memo.bind (fun _ -> Unchecked.defaultof<Memo<int>>) m1
                let exNull2 = failedWith memoBindNull.TryValue
                Expect.isTrue (isNullReference exNull2) "Memo.bind null return fails with NullReferenceException"
                Expect.isTrue (obj.ReferenceEquals (memoBindNull.ErrorOrigin, memoBindNull)) "Memo.bind null ErrorOrigin is self"
            }

            test "map2 and zip enforce left-to-right Pending and Failed precedence and ErrorOrigin across all 3x3 Reading pairs" {
                use g = new Graph ()
                use _ = g.Activate ()

                let errLeft = InvalidOperationException "left-fail"
                let errRight = InvalidOperationException "right-fail"
                let pendingSrcLeft = createAsyncSource<int>()
                let pendingSrcRight = createAsyncSource<int>()
                let originLeft: Memo<int> = createMemo (fun _ -> raise errLeft)
                let originRight: Memo<int> = createMemo (fun _ -> raise errRight)

                let mode1 = createSignal 0
                let mode2 = createSignal 0

                let m1 =
                    createMemo (fun _ ->
                        match mode1.Value with
                        | 0 -> 10
                        | 1 -> pendingSrcLeft.Value
                        | _ -> originLeft.Value)

                let m2 =
                    createMemo (fun _ ->
                        match mode2.Value with
                        | 0 -> 20
                        | 1 -> pendingSrcRight.Value
                        | _ -> originRight.Value)

                let mapCalls = ref 0

                let mapped =
                    Memo.map2
                        (fun a b ->
                            mapCalls.Value <- mapCalls.Value + 1
                            a + b)
                        m1
                        m2

                let zipped = Memo.zip m1 m2

                for left in 0..2 do
                    for right in 0..2 do
                        mode1.Value <- left
                        mode2.Value <- right

                        let as1 = createAsyncSource<int>()
                        let as2 = createAsyncSource<int>()
                        let asMapped = AsyncSource.map2 (+) as1 as2
                        let asZipped = AsyncSource.zip as1 as2

                        match left with
                        | 0 -> as1.Settle 10
                        | 1 -> ()
                        | _ -> as1.Fail errLeft

                        match right with
                        | 0 -> as2.Settle 20
                        | 1 -> ()
                        | _ -> as2.Fail errRight

                        let beforeCalls = mapCalls.Value
                        let rMap = mapped.TryValue
                        let rZip = zipped.TryValue
                        let rAsMap = asMapped.TryValue
                        let rAsZip = asZipped.TryValue

                        match left, right with
                        | 0, 0 ->
                            Expect.equal rMap (Ready 30) "Memo.map2 (Ready, Ready)"
                            Expect.equal rZip (Ready (10, 20)) "Memo.zip (Ready, Ready)"
                            Expect.equal rAsMap (Ready 30) "AsyncSource.map2 (Ready, Ready)"
                            Expect.equal rAsZip (Ready (10, 20)) "AsyncSource.zip (Ready, Ready)"
                        | 1, _ ->
                            Expect.equal rMap Pending $"Memo.map2 (Pending, {right})"
                            Expect.equal rZip Pending $"Memo.zip (Pending, {right})"
                            Expect.equal mapCalls.Value beforeCalls "mapping skipped when left is Pending"
                            let pMap = Seq.toArray mapped.PendingSources
                            Expect.equal pMap.Length 1 "mapped has 1 pending source"
                            Expect.isTrue (obj.ReferenceEquals (pMap[0], m1)) "mapped pending source is m1"
                            Expect.equal rAsMap Pending $"AsyncSource.map2 (Pending, {right})"
                            Expect.equal rAsZip Pending $"AsyncSource.zip (Pending, {right})"
                            let pAs = Seq.toArray asMapped.PendingSources
                            Expect.isTrue (obj.ReferenceEquals (pAs[0], as1)) "asMapped pending source is as1"
                        | 2, _ ->
                            Expect.isTrue (obj.ReferenceEquals (failedWith rMap, errLeft)) $"Memo.map2 (Failed, {right}) error"
                            Expect.isTrue (obj.ReferenceEquals (failedWith rZip, errLeft)) $"Memo.zip (Failed, {right}) error"
                            Expect.isTrue (obj.ReferenceEquals (mapped.ErrorOrigin, originLeft)) $"Memo.map2 (Failed, {right}) ErrorOrigin"
                            Expect.isTrue (obj.ReferenceEquals (zipped.ErrorOrigin, originLeft)) $"Memo.zip (Failed, {right}) ErrorOrigin"
                            Expect.equal mapCalls.Value beforeCalls "mapping skipped when left is Failed"
                            Expect.isTrue (obj.ReferenceEquals (failedWith rAsMap, errLeft)) $"AsyncSource.map2 (Failed, {right})"
                            Expect.isTrue (obj.ReferenceEquals (asMapped.ErrorOrigin, as1)) $"AsyncSource.map2 (Failed, {right}) ErrorOrigin"
                            Expect.isTrue (obj.ReferenceEquals (asZipped.ErrorOrigin, as1)) $"AsyncSource.zip (Failed, {right}) ErrorOrigin"
                        | 0, 1 ->
                            Expect.equal rMap Pending "Memo.map2 (Ready, Pending)"
                            Expect.equal rZip Pending "Memo.zip (Ready, Pending)"
                            Expect.equal mapCalls.Value beforeCalls "mapping skipped when right is Pending"
                            let pMap = Seq.toArray mapped.PendingSources
                            Expect.isTrue (obj.ReferenceEquals (pMap[0], m2)) "mapped pending source is m2"
                            Expect.equal rAsMap Pending "AsyncSource.map2 (Ready, Pending)"
                            Expect.equal rAsZip Pending "AsyncSource.zip (Ready, Pending)"
                            let pAs = Seq.toArray asMapped.PendingSources
                            Expect.isTrue (obj.ReferenceEquals (pAs[0], as2)) "asMapped pending source is as2"
                        | 0, 2 ->
                            Expect.isTrue (obj.ReferenceEquals (failedWith rMap, errRight)) "Memo.map2 (Ready, Failed) error"
                            Expect.isTrue (obj.ReferenceEquals (failedWith rZip, errRight)) "Memo.zip (Ready, Failed) error"
                            Expect.isTrue (obj.ReferenceEquals (mapped.ErrorOrigin, originRight)) "Memo.map2 (Ready, Failed) ErrorOrigin"
                            Expect.isTrue (obj.ReferenceEquals (zipped.ErrorOrigin, originRight)) "Memo.zip (Ready, Failed) ErrorOrigin"
                            Expect.equal mapCalls.Value beforeCalls "mapping skipped when right is Failed"
                            Expect.isTrue (obj.ReferenceEquals (failedWith rAsMap, errRight)) "AsyncSource.map2 (Ready, Failed)"
                            Expect.isTrue (obj.ReferenceEquals (asMapped.ErrorOrigin, as2)) "AsyncSource.map2 (Ready, Failed) ErrorOrigin"
                            Expect.isTrue (obj.ReferenceEquals (asZipped.ErrorOrigin, as2)) "AsyncSource.zip (Ready, Failed) ErrorOrigin"
                        | _ -> ()
            }

            test "Signal.bind and Memo.bind split inner ticks from outer binder and preserve scope across transient outer Pending or Failed" {
                use g = new Graph ()
                use _ = g.Activate ()

                // 1. Pre-existing inner Memos survive outer switch without being disposed.
                let sigA = createSignal 10
                let sigB = createSignal 200
                let preA = Signal.map ((+) 1) sigA
                let preB = Signal.map ((+) 2) sigB
                let selector = createSignal true
                let binderRuns = ref 0

                let switched =
                    Signal.bind
                        (fun pickA ->
                            binderRuns.Value <- binderRuns.Value + 1
                            if pickA then preA else preB)
                        selector

                let seen = ResizeArray<int>()
                createEffect (fun () -> seen.Add switched.Value)
                Expect.sequenceEqual seen [ 11 ] "initial preA"
                Expect.equal binderRuns.Value 1 "binder ran once"

                // Inner tick updates switched without re-running binder!
                sigA.Value <- 20
                Expect.sequenceEqual seen [ 11; 21 ] "inner tick updates bound memo"
                Expect.equal binderRuns.Value 1 "inner tick does NOT re-run outer binder"

                // Switch outer to preB; preA must NOT be disposed!
                selector.Value <- false
                Expect.sequenceEqual seen [ 11; 21; 202 ] "switched to preB"
                Expect.equal binderRuns.Value 2 "binder ran for false"
                sigA.Value <- 30
                Expect.equal preA.Value 31 "pre-existing inner Memo preA remains alive after outer switch"
                Expect.sequenceEqual seen [ 11; 21; 202 ] "unselected preA tick does not wake switched"

                // 2. Dynamic inner Memo + onCleanup inside Memo.bind disposes on unequal outer key, survives transient Pending/Failed!
                let rawKey = createSignal 1
                let outerMode = createSignal 0
                let pendingSrc = createAsyncSource<int>()
                let outerErr = InvalidOperationException "outer-error"
                let outerFailOrigin: Memo<int> = createMemo (fun _ -> raise outerErr)
                let innerTick = createSignal 5
                let untrackedInBinder = createSignal 99
                let log = ResizeArray<string>()

                let outerKey =
                    createMemo (fun _ ->
                        match outerMode.Value with
                        | 0 -> rawKey.Value % 10
                        | 1 -> pendingSrc.Value
                        | _ -> outerFailOrigin.Value)

                let dynBound =
                    Memo.bind
                        (fun k ->
                            let _ = untrackedInBinder.Value
                            log.Add $"bind:{k}"
                            onCleanup (fun () -> log.Add $"cleanup:{k}")
                            createMemo (fun _ -> k * 100 + innerTick.Value))
                        outerKey

                Expect.equal dynBound.Value 105 "initial dynamic inner Memo"
                Expect.equal untrackedInBinder.ObserverCount 0 "binder closes dependency tracking"
                Expect.sequenceEqual log [ "bind:1" ] "binder ran once for key 1"

                // Inner tick updates dynBound without re-running binder or discharging cleanup!
                innerTick.Value <- 6
                Expect.equal dynBound.Value 106 "inner tick updates dynamic Memo.bind"
                Expect.sequenceEqual log [ "bind:1" ] "no binder re-run or cleanup on inner tick"

                // Equal outer key (1 -> 11, % 10 = 1) keeps inner Memo without re-running binder.
                rawKey.Value <- 11
                Expect.equal dynBound.Value 106 "equal outer key preserves inner Memo"
                Expect.sequenceEqual log [ "bind:1" ] "still 1 binder run"

                // Transient outer Pending -> back to equal key 1 preserves outer scope and inner Memo!
                outerMode.Value <- 1
                Expect.equal dynBound.TryValue Pending "outer Pending surfaces on Memo.bind"
                let dynPending = Seq.toArray dynBound.PendingSources
                Expect.isTrue (obj.ReferenceEquals (dynPending[0], outerKey)) "PendingSource is outerKey"
                Expect.sequenceEqual log [ "bind:1" ] "scope preserved during outer Pending"
                outerMode.Value <- 0
                Expect.equal dynBound.Value 106 "recovered to inner Memo after outer Pending"
                Expect.sequenceEqual log [ "bind:1" ] "0 extra binder runs after outer Pending recovery"

                // Transient outer Failed -> back to equal key 1 preserves outer scope and inner Memo!
                outerMode.Value <- 2
                let seenOuterErr = failedWith dynBound.TryValue
                Expect.isTrue (obj.ReferenceEquals (seenOuterErr, outerErr)) "outer Failed surfaces on Memo.bind"
                Expect.isTrue (obj.ReferenceEquals (dynBound.ErrorOrigin, outerFailOrigin)) "outer ErrorOrigin preserved"
                Expect.sequenceEqual log [ "bind:1" ] "scope preserved during outer Failed"
                outerMode.Value <- 0
                innerTick.Value <- 7
                Expect.equal dynBound.Value 107 "recovered and picked up inner tick after outer Failed"
                Expect.sequenceEqual log [ "bind:1" ] "0 extra binder runs after outer Failed recovery"

                // Unequal outer key 2 discharges key 1's scope (disposing dynamic inner memo) and runs binder for 2!
                rawKey.Value <- 2
                Expect.equal dynBound.Value 207 "updated to key 2"
                Expect.sequenceEqual log [ "bind:1"; "cleanup:1"; "bind:2" ] "key 1 scope discharged before key 2"
                Expect.equal innerTick.ObserverCount 1 "only key 2's dynamic inner memo observes innerTick"

                dynBound.Dispose ()
                Expect.sequenceEqual log [ "bind:1"; "cleanup:1"; "bind:2"; "cleanup:2" ] "Dispose discharges active binder scope"
                Expect.equal innerTick.ObserverCount 0 "dynamic inner memo disposed with bind memo"
            }

            test "Review Focus #1: Signal.bind and Memo.bind handle cleanup re-entry and self-disposal after Pending" {
                use g = new Graph ()
                use _ = g.Activate ()

                // Case A: Cleanup registered in Signal.bind mutates outer signal and reads self synchronously.
                let s = createSignal 1
                let inner = createSignal 0
                let binderKeys = ResizeArray<int>()
                let seen = ResizeArray<int>()
                let mutable selfBind: Memo<int> = Unchecked.defaultof<_>

                selfBind <-
                    Signal.bind
                        (fun k ->
                            binderKeys.Add k

                            onCleanup (fun () ->
                                if k = 2 then
                                    s.Value <- 10
                                    selfBind.TryValue |> ignore)

                            createMemo (fun _ -> k * 100 + inner.Value))
                        s

                createEffect (fun () -> seen.Add selfBind.Value)
                Expect.sequenceEqual seen [ 100 ] "initial effect observation"

                s.Value <- 2
                s.Value <- 3

                Expect.equal selfBind.Value 1000 "converges to cleanup's written outer signal 10"
                Expect.equal (Seq.last seen) 1000 "downstream effect converges to 1000"
                Expect.equal inner.ObserverCount 1 "exactly one inner generation stays attached"
                selfBind.Dispose ()
                Expect.equal inner.ObserverCount 0 "inner generation detached on Dispose"

                // Case B: Self-disposal in binder cleanup after transient Pending restores Status.Pending and PendingSources.
                let keySig = createSignal 1
                let gatePending = createSignal false
                let pendingSrc = createAsyncSource<int>()
                let log = ResizeArray<string>()
                let mutable rootOwner: Owner = Unchecked.defaultof<_>

                let outerMemo =
                    createMemo (fun _ -> if gatePending.Value then pendingSrc.Value else keySig.Value)

                let selfDisposing =
                    createRoot (fun owner ->
                        rootOwner <- owner

                        Memo.bind
                            (fun k ->
                                log.Add $"bind:{k}"

                                onCleanup (fun () ->
                                    log.Add $"cleanup:{k}"
                                    rootOwner.Dispose ())

                                Signal.map ((+) (k * 10)) keySig)
                            outerMemo)

                Expect.equal selfDisposing.Value 11 "initial Ready"
                gatePending.Value <- true
                Expect.equal selfDisposing.TryValue Pending "transient outer Pending"

                // Now recover to unequal key 2 -> discharges cleanup:1 which disposes rootOwner (and selfDisposing)!
                gatePending.Value <- false
                keySig.Value <- 2
                Expect.equal selfDisposing.TryValue Pending "self-disposal during cleanup after Pending preserves Pending"
                Expect.equal selfDisposing.Status Status.Pending "Status remains Pending"
                let selfPending = Seq.toArray selfDisposing.PendingSources
                Expect.equal selfPending.Length 1 "PendingSources preserved"
                Expect.isTrue (obj.ReferenceEquals (selfPending[0], outerMemo)) "PendingSource is outerMemo"
                Expect.sequenceEqual log [ "bind:1"; "cleanup:1" ] "stopped before bind:2"
            }

            testCaseAsync "AsyncMemo.bind, bindWithPolicy, AsyncSource.bind, StreamMemo.bind, and StreamMemo.bindFold"
            <| async {
                use g = new Graph ()

                let untracked = new Signal<int> (g, 42)
                let pendingLeak = new AsyncSource<int> (g)

                // 1. AsyncSource.bind + AsyncMemo.bind + AsyncMemo.bindWithPolicy
                let asrc = new AsyncSource<int * int> (g)
                let flights1 = Dictionary<int, Flight<int>>()
                let flights2 = Dictionary<int, Flight<string>>()
                let flightsKeep = Dictionary<int, Flight<string>>()

                let step1 =
                    AsyncSource.bind
                        (fun (k, _) _ ->
                            let _ = untracked.Value
                            let f = Flight<int>()
                            flights1[k] <- f
                            f.Task)
                        asrc

                let step2 =
                    AsyncMemo.bind
                        (fun v _ ->
                            let _ = untracked.Value
                            let f = Flight<string>()
                            flights2[v] <- f
                            f.Task)
                        step1

                let stepFinish =
                    AsyncMemo.bindWithPolicy
                        FlightPolicy.FinishCurrent
                        (fun v _ ->
                            let f = Flight<string>()
                            flightsKeep[v] <- f
                            f.Task)
                        step1

                Expect.equal step2.TryValue Pending "AsyncSource Pending -> step2 Pending"
                Expect.equal flights1.Count 0 "no flight started while AsyncSource is Pending"

                let srcErr = InvalidOperationException "asrc-err"
                asrc.Fail srcErr
                Expect.isTrue (obj.ReferenceEquals (failedWith step1.TryValue, srcErr)) "AsyncSource error propagates"
                Expect.isTrue (obj.ReferenceEquals (step1.ErrorOrigin, asrc)) "step1 ErrorOrigin is asrc"
                Expect.isTrue (obj.ReferenceEquals (failedWith step2.TryValue, srcErr)) "step2 propagates upstream error"
                Expect.isTrue (obj.ReferenceEquals (step2.ErrorOrigin, asrc)) "step2 preserves root ErrorOrigin"

                asrc.Settle (1, 0)
                Expect.equal step2.TryValue Pending "step1 flight 1 started"
                Expect.equal stepFinish.TryValue Pending "stepFinish waiting on step1"
                flights1[1].Settle 10
                do! drained ()

                Expect.equal step1.Value 10 "step1 settled 10"
                Expect.equal step2.TryValue Pending "step2 flight 10 started"
                Expect.equal stepFinish.TryValue Pending "stepFinish flight 10 started"
                flights2[10].Settle "ten"
                flightsKeep[10].Settle "keep:10"
                do! drained ()
                Expect.equal step2.Value "ten" "step2 settled ten"
                Expect.equal stepFinish.Value "keep:10" "stepFinish settled keep:10"
                Expect.equal untracked.ObserverCount 0 "async binders close dependency tracking"

                // FinishCurrent policy: when step1 moves 10 -> 20 while flight 20 is in-flight, moving 20 -> 30 lets 20 finish!
                asrc.Settle (2, 0)
                step1.TryValue |> ignore
                flights1[2].Settle 20
                do! drained ()
                stepFinish.TryValue |> ignore
                Expect.isTrue (flightsKeep.ContainsKey 20) "flight 20 started"

                asrc.Settle (3, 0)
                step1.TryValue |> ignore
                flights1[3].Settle 30
                do! drained ()
                stepFinish.TryValue |> ignore
                Expect.isFalse (flightsKeep.ContainsKey 30) "FinishCurrent defers flight 30 while 20 is active"
                flightsKeep[20].Settle "keep:20"
                do! drained ()
                stepFinish.TryValue |> ignore
                flightsKeep[30].Settle "keep:30"
                do! drained ()
                Expect.equal stepFinish.Value "keep:30" "FinishCurrent ran deferred flight 30 after 20 finished"

                // Synchronous NotReadyException inside AsyncMemo.bind wraps into InvalidOperationException.
                let asyncLeak = AsyncMemo.bind (fun _ _ -> completed pendingLeak.Value) step1
                let exAsyncLeak = failedWith asyncLeak.TryValue
                Expect.isTrue (isInvalidOperation exAsyncLeak) "AsyncMemo.bind wraps synchronous NotReadyException"
                Expect.isTrue (obj.ReferenceEquals (asyncLeak.ErrorOrigin, asyncLeak)) "ErrorOrigin is asyncLeak"

                // 2. StreamMemo.bind and StreamMemo.bindFold
                let keyFeed = ManualStream<string * int>()
                let itemFeed = ManualStream<int>()
                let foldFeed = ManualStream<int>()
                let keyStream = g.Run (fun () -> createStream (fun _ -> keyFeed.Source))

                let boundStream =
                    StreamMemo.bind
                        (fun (room, _) _ ->
                            let _ = untracked.Value
                            ignore room
                            itemFeed.Source)
                        keyStream

                let boundFold =
                    StreamMemo.bindFold
                        100
                        (+)
                        (fun (room, _) _ ->
                            let _ = untracked.Value
                            ignore room
                            foldFeed.Source)
                        keyStream

                Expect.equal boundStream.TryValue Pending "StreamMemo.bind Pending while upstream Pending"
                Expect.equal boundFold.TryValue Pending "StreamMemo.bindFold Pending while upstream Pending"
                Expect.equal itemFeed.Starts 0 "no inner stream started while upstream Pending"

                keyFeed.Push ("alpha", 1)
                Expect.equal boundStream.TryValue Pending "inner stream started after upstream Ready"
                Expect.equal boundFold.TryValue Pending "inner fold stream started"
                Expect.equal itemFeed.Starts 1 "1 inner stream start"
                Expect.equal foldFeed.Starts 1 "1 inner fold stream start"

                itemFeed.Push 5
                foldFeed.Push 10
                foldFeed.Push 20
                Expect.equal boundStream.Value 5 "StreamMemo.bind value"
                Expect.equal boundFold.Value 130 "StreamMemo.bindFold accumulated value"

                // Upstream emits new tuple with different rev -> restarts inner streams; emitting exact equal tuple cuts off at keyStream!
                keyFeed.Push ("alpha", 1)
                Expect.equal itemFeed.Starts 1 "equal upstream item keeps inner stream open"
                Expect.equal foldFeed.Starts 1 "equal upstream item keeps fold accumulator"

                // Synchronous NotReadyException inside StreamMemo.bind wraps into InvalidOperationException.
                let streamLeak =
                    StreamMemo.bind
                        (fun _ _ ->
                            let _ = pendingLeak.Value
                            itemFeed.Source)
                        keyStream

                let exStreamLeak = failedWith streamLeak.TryValue
                Expect.isTrue (isInvalidOperation exStreamLeak) "StreamMemo.bind wraps synchronous NotReadyException"
                Expect.isTrue (obj.ReferenceEquals (streamLeak.ErrorOrigin, streamLeak)) "ErrorOrigin is streamLeak"
            }

            test "Review Focus #5: identical-source zip/map2 and shared-diamond map2 evaluate glitch-free and dispose cleanly" {
                use g = new Graph ()
                use _ = g.Activate ()

                // 1. Identical source in Signal.zip, Signal.map2, Memo.zip, Memo.map2, AsyncSource.zip
                let s = createSignal 3
                let sameSigZip = Signal.zip s s
                let sameSigMap2 = Signal.map2 (+) s s

                let baseMemo = Signal.map ((*) 2) s
                let sameMemoZip = Memo.zip baseMemo baseMemo
                let sameMemoMap2 = Memo.map2 (+) baseMemo baseMemo

                let asrc = createAsyncSource<int>()
                asrc.Settle 5
                let sameAsrcZip = AsyncSource.zip asrc asrc

                let sigZipLog = ResizeArray<int * int>()
                createEffect (fun () -> sigZipLog.Add sameSigZip.Value)

                Expect.sequenceEqual sigZipLog [ (3, 3) ] "initial identical Signal.zip"
                Expect.equal sameSigMap2.Value 6 "initial identical Signal.map2"
                Expect.equal sameMemoZip.Value (6, 6) "initial identical Memo.zip"
                Expect.equal sameMemoMap2.Value 12 "initial identical Memo.map2"
                Expect.equal sameAsrcZip.Value (5, 5) "initial identical AsyncSource.zip"

                s.Value <- 7
                Expect.sequenceEqual sigZipLog [ (3, 3); (7, 7) ] "glitch-free single evaluation on identical Signal.zip"
                Expect.equal sameSigMap2.Value 14 "identical Signal.map2 updated"
                Expect.equal sameMemoZip.Value (14, 14) "identical Memo.zip updated"
                Expect.equal sameMemoMap2.Value 28 "identical Memo.map2 updated"

                asrc.Settle 9
                Expect.equal sameAsrcZip.Value (9, 9) "identical AsyncSource.zip updated"

                sameSigZip.Dispose ()
                sameSigMap2.Dispose ()
                sameMemoZip.Dispose ()
                sameMemoMap2.Dispose ()
                baseMemo.Dispose ()
                sameAsrcZip.Dispose ()
                asrc.Settle 99
                Expect.equal sameAsrcZip.Value (9, 9) "disposed AsyncSource.zip stays frozen after settle"
                Expect.equal s.ObserverCount 0 "clean ObserverSet teardown for s"

                // 2. Shared-diamond Memo.map2 (+) left right evaluates once per write and once per batch without glitches!
                let root = createSignal 1
                let left = Signal.map ((+) 1) root
                let right = Signal.map ((*) 10) root
                let map2Runs = ref 0

                let diamond =
                    Memo.map2
                        (fun l r ->
                            map2Runs.Value <- map2Runs.Value + 1
                            l + r)
                        left
                        right

                let observed = ResizeArray<int>()
                let eff = new Effect (g, fun () -> observed.Add diamond.Value)

                Expect.sequenceEqual observed [ 12 ] "initial diamond (2 + 10)"
                Expect.equal map2Runs.Value 1 "1 initial map2 run"

                root.Value <- 4
                Expect.sequenceEqual observed [ 12; 45 ] "glitch-free diamond (5 + 40)"
                Expect.equal map2Runs.Value 2 "exactly 1 map2 run on single write"

                batch (fun () ->
                    root.Value <- 5
                    root.Value <- 6)

                Expect.sequenceEqual observed [ 12; 45; 67 ] "glitch-free diamond after batch (7 + 60)"
                Expect.equal map2Runs.Value 3 "exactly 1 map2 run for batch"

                eff.Dispose ()
                diamond.Dispose ()
                left.Dispose ()
                right.Dispose ()
                Expect.equal root.ObserverCount 0 "diamond disposed cleanly"
            }

            test
                "static combinators mark reader Check when upstream source remains stale after Refresh, and Signal.map2 deduplicates identical source slots" {
                use g = new Graph ()
                use _ = g.Activate ()

                // 1. Upstream active memo that remains IsStale after Refresh() (self-dirties then returns equal value after reading combinator)!
                let makeActiveStaleScenario (attach: Memo<int> -> (unit -> int)) =
                    let s = createSignal 1
                    let mutable readComb: unit -> int = fun () -> 0

                    let up =
                        createMemo (fun _ ->
                            let v = s.Value

                            if v = 2 then
                                s.Value <- 99
                                readComb () |> ignore
                                10
                            else
                                v * 10)

                    let combReader = attach up
                    readComb <- combReader
                    Expect.equal (combReader ()) 11 "Run 1 warms combinator to 11"
                    s.Value <- 2
                    Expect.equal up.Value 10 "up Run 2 self-dirties to 99, reads combinator while Active+Stale, and cuts off at 10"
                    Expect.equal (combReader ()) 991 "combinator marked Check from IsStale up refreshes up to 990 -> 991"

                makeActiveStaleScenario (fun up ->
                    let m = Memo.map ((+) 1) up
                    fun () -> m.Value)

#if !RANVIER_TRACE
                makeActiveStaleScenario (fun up ->
                    let m = up |> Memo.map ((+) 0) |> Memo.map ((+) 1)
                    fun () -> m.Value)
#endif

                makeActiveStaleScenario (fun up ->
                    let one = createMemo (fun _ -> 1)
                    let m = Memo.map2 (+) up one
                    fun () -> m.Value)

                makeActiveStaleScenario (fun up ->
                    let m = Memo.bind (fun x -> createMemo (fun _ -> x + 1)) up
                    fun () -> m.Value)

                // 2. Signal.map2 (+) s s links a single slot on s
                let sSame = createSignal 7
                let sameMap2 = Signal.map2 (+) sSame sSame
                Expect.equal sameMap2.Value 14 "Signal.map2 (+) sSame sSame = 14"
                Expect.equal sSame.ObserverCount 1 "single observer registered on sSame"
#if RANVIER_TRACE
                let snap = Trace.snapshot g
                let sameSources = snap.Sources |> Map.find (sameMap2 :> INode).Id
                Expect.sequenceEqual sameSources [ (sSame :> INode).Id ] "Signal.map2 (+) s s records 1 source slot, not 2"
#endif
                sameMap2.Dispose ()
                Expect.equal sSame.ObserverCount 0 "disposed cleanly"
            }

#if RANVIER_TRACE
            test "Trace.reconcile holds across map, map2, zip, and split bind lifecycles" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 1
                let tick = createSignal 10
                let pre = createMemo (fun _ -> tick.Value + 100)
                let asrc = createAsyncSource<int>()

                let m1 = Signal.map ((+) 2) s
                let m2 = Signal.zip s s
                let m3 = Memo.map2 (fun a (x, y) -> a + x + y) m1 m2

                let bound =
                    Signal.bind
                        (fun k ->
                            if k = 1 then
                                pre
                            else
                                createMemo (fun _ -> k * 10 + tick.Value))
                        s

                let asyncMapped = AsyncSource.map ((*) 3) asrc

                Expect.equal m3.Value 5 "initial m3"
                Expect.equal bound.Value 110 "initial pre-existing inner memo"
                Expect.equal asyncMapped.TryValue Pending "initial asyncMapped pending"

                asrc.Settle 4
                Expect.equal asyncMapped.Value 12 "settled asyncMapped"

                s.Value <- 2
                Expect.equal m3.Value 8 "updated m3"
                Expect.equal bound.Value 30 "switched to dynamic inner memo"

                tick.Value <- 20
                Expect.equal bound.Value 40 "inner tick refreshed bound"

                s.Value <- 1
                Expect.equal bound.Value 120 "switched back to pre-existing inner memo"

                bound.Dispose ()
                m3.Dispose ()
                m2.Dispose ()
                m1.Dispose ()
                asyncMapped.Dispose ()

                Expect.isEmpty (Trace.reconcile g) "live edges and scopes match folded trace log"
            }
#endif
        ]
