module Ranvier.AotSmoke.Program

open System
open System.Collections.Generic
open System.Threading
open Ranvier
open Ranvier.CSharp
open Ranvier.Elmish
open Ranvier.Query

let mutable private failures = 0

let private report (name: string) (ok: bool) (detail: string) =
    if ok then
        Console.WriteLine ("ok   " + name)
    else
        failures <- failures + 1
        Console.WriteLine ("FAIL " + name + ": " + detail)

/// <summary>Runs <c>f</c> and reports whether it raised exactly <c>'E</c> with a message starting with <c>prefix</c>.</summary>
let private raises<'E when 'E :> exn> (name: string) (prefix: string) (f: unit -> unit) =
    try
        f ()
        report name false "no exception"
    with ex ->
        let ok =
            ex.GetType () = typeof<'E>
            && ex.Message.StartsWith (prefix, StringComparison.Ordinal)

        report name ok (ex.GetType().FullName + ": " + ex.Message)

let private equals (name: string) (expected: string) (actual: string) =
    report name (actual = expected) ("got \"" + actual + "\"")

[<AllowNullLiteral>]
type private CeOptBox(id: int, tag: string) =
    member _.Id = id
    member _.Tag = tag

    override _.Equals(other: obj) =
        match other with
        | :? CeOptBox as that -> id = that.Id
        | _ -> false

    override _.GetHashCode() = id

module private CeSmoke =
    open System.Threading.Tasks
    open Ranvier.CE

    let private mod10Comparer =
        { new IEqualityComparer<int> with
            member _.Equals(a, b) =
                a % 10 = b % 10

            member _.GetHashCode x = x % 10
        }

    let private parityComparer =
        { new IEqualityComparer<int> with
            member _.Equals(a, b) =
                (a &&& 1) = (b &&& 1)

            member _.GetHashCode x = x &&& 1
        }

    let private arrayLengthComparer =
        { new IEqualityComparer<int[]> with
            member _.Equals(a, b) =
                not (isNull a)
                && not (isNull b)
                && a.Length = b.Length

            member _.GetHashCode a =
                if isNull a then 0 else a.Length
        }

    let private pumpUntilSettled (g: Graph) (readTry: unit -> Reading<'T>) =
        let deadline = DateTime.UtcNow.AddSeconds 5.0

        while readTry () = Pending do
            if DateTime.UtcNow > deadline then
                raise (TimeoutException "Timed out waiting for async CE flight in AotSmoke.")

            if g.PendingWork > 0 then
                g.Pump () |> ignore
            else
                Thread.Sleep 1

    let checks () : (string * bool * string) list =
        use g = new Graph ()
        use _ = g.Activate ()

        // 1. memo: 4-stage fused chain matching Memo.map fusion
        let rootSig = Signal (g, 2)
        let stage2Calls = ref 0
        let f1 = memo { let! x = rootSig in return x + 1 }

        let f2 =
            memo {
                let! x = f1
                stage2Calls.Value <- stage2Calls.Value + 1
                return x * 10
            }

        let f3 = memo { let! x = f2 in return x + 5 }
        let f4 = memo { let! x = f3 in return x * 2 }
        let fusedInit = f4.Value
        let stage2AfterF4 = stage2Calls.Value
        let midInit = f2.Value
        let midNoRecompute = stage2Calls.Value = 1 && f2.Runs = 1
        rootSig.Value <- 3
        let fusedUpd = f4.Value
        let midUpd = f2.Value
        let stage2AfterUpd = stage2Calls.Value

        // 2. memo: and! (Signal * Signal, Memo * Memo, 3-way mixed)
        let sA = Signal (g, 3)
        let sB = Signal (g, 4)
        let mA = memo { let! x = sA in return x * 10 }
        let mB = memo { let! x = sB in return x * 100 }
        let eSeed = editable { let! x = sA in return x + 1 }

        let mSigPair =
            memo {
                let! a = sA
                and! b = sB
                return a + b
            }

        let mMemoPair =
            memo {
                let! a = mA
                and! b = mB
                return a + b
            }

        let mMixed3 =
            memo {
                let! a = sA
                and! b = mB
                and! c = eSeed
                return a + b + c
            }

        // 3. memo: owning + comparer + withComparer + memo.previousOr
        let ownSig = Signal (g, 1)
        let ownCleanups = ResizeArray<int>()

        let mOwnCmp =
            memo {
                owning
                comparer parityComparer
                let! k = ownSig |> withComparer mod10Comparer
                onCleanup (fun () -> ownCleanups.Add k)
                let child = createMemo (fun _ -> k * 10 + (k &&& 1))
                return child.Value
            }

        let paritySeen = ResizeArray<int>()
        createEffect (fun () -> paritySeen.Add mOwnCmp.Value)
        let ownV1 = mOwnCmp.Value
        ownSig.Value <- 11
        let ownEqMod10 = mOwnCmp.Value = 11 && ownCleanups.Count = 0
        ownSig.Value <- 3

        let ownOddCutoff =
            mOwnCmp.Value = 31
            && ownCleanups.Count = 1
            && ownCleanups[0] = 1
            && paritySeen.Count = 1

        ownSig.Value <- 2

        let ownEvenWake =
            mOwnCmp.Value = 20
            && ownCleanups.Count = 2
            && ownCleanups[1] = 3
            && paritySeen.Count = 2
            && paritySeen[1] = 20

        let prevSig = Signal (g, 5)

        let mPrev =
            memo {
                let! x = prevSig
                let! p = memo.previousOr 100
                return x + p
            }

        let prev1 = mPrev.Value
        prevSig.Value <- 10
        let prev2 = mPrev.Value

        // 4. memo: option header
        use optGraph = new Graph (GraphOptions.Default.WithEquality (StructuralPolicy ()))
        let optSig = Signal (optGraph, (1, "v1"))

        let mOpt =
            memo {
                option
                let! (id, tag) = optSig
                return if id > 0 then Some (CeOptBox (id, tag)) else None
            }

        let optFirst = mOpt.Value
        optSig.Value <- (1, "v2")
        let optSecond = mOpt.Value

        let optSameRef =
            match optFirst, optSecond with
            | Some a, Some b -> obj.ReferenceEquals (a, b) && b.Tag = "v1"
            | _ -> false

        optSig.Value <- (0, "none")
        let optIsNone = mOpt.Value.IsNone
        optSig.Value <- (1, "v3")

        let optFreshRef =
            match optFirst, mOpt.Value with
            | Some a, Some c -> not (obj.ReferenceEquals (a, c)) && c.Tag = "v3"
            | _ -> false

        // 5. memo match! over Projection.tryAt + Lookup.at + KeyedCollection.tryAt, and return! (SplitBind)
        let listSig = Signal (g, [ (1, "Alice") ])
        let proj = createProjection fst snd (fun () -> listSig.Value)
        let scoreBase = Signal (g, 10)

        let lkp =
            createLookup (fun s k -> s + (if k = "Bob" then 20 else 5)) (fun _ _ -> [ "Alice"; "Bob" ]) (fun () -> scoreBase.Value)

        let kc = createKeyedCollection fst

        let mDesc =
            memo {
                match! proj |> Projection.tryAt 2 with
                | None ->
                    match! kc |> KeyedCollection.tryAt 2 with
                    | None -> return "absent"
                    | Some (_, lbl) -> return "kc:" + lbl
                | Some name ->
                    let! score = lkp |> Lookup.at name
                    return name + ":" + string score
            }

        let desc0 = mDesc.Value
        kc.AddOrUpdate (2, "two")
        let desc1 = mDesc.Value
        listSig.Value <- [ (1, "Alice"); (2, "Bob") ]
        let desc2 = mDesc.Value
        scoreBase.Value <- 15
        let desc3 = mDesc.Value

        let splitKey = Signal (g, 1)
        let inOdd = Signal (g, 100)
        let inEven = Signal (g, 200)
        let mInOdd = memo { let! x = inOdd in return x + 1 }
        let mInEven = memo { let! x = inEven in return x + 2 }
        let splitCleanups = ResizeArray<int>()

        let mSplit =
            memo {
                owning
                let! k = splitKey |> withComparer mod10Comparer
                onCleanup (fun () -> splitCleanups.Add k)

                if (k &&& 1) = 0 then return! mInEven else return! mInOdd
            }

        let split1 = mSplit.Value
        inOdd.Value <- 105
        let split2 = mSplit.Value
        splitKey.Value <- 11
        let split3 = mSplit.Value
        let splitNoClean = splitCleanups.Count = 0
        splitKey.Value <- 2
        let split4 = mSplit.Value

        // 6. effect: node + withComparer + Projection.keys / KeyedCollection.keys
        let projKeyLog = ResizeArray<int>()
        let kcKeyLog = ResizeArray<int>()

        let effProj =
            effect {
                node

                let! ks =
                    proj
                    |> Projection.keys
                    |> withComparer arrayLengthComparer

                projKeyLog.Add ks.Length
            }

        let effKc =
            effect {
                node

                let! ks =
                    kc
                    |> KeyedCollection.keys
                    |> withComparer arrayLengthComparer

                kcKeyLog.Add ks.Length
            }

        let effInitOk =
            effProj.Status = Status.None
            && effKc.Status = Status.None
            && projKeyLog.Count = 1
            && projKeyLog[0] = 2
            && kcKeyLog.Count = 1
            && kcKeyLog[0] = 1

        listSig.Value <- [ (1, "Alice"); (3, "Carol") ]
        let effProjCutOff = projKeyLog.Count = 1
        listSig.Value <- [ (1, "Alice"); (3, "Carol"); (4, "Dan") ]
        let effProjWoke = projKeyLog.Count = 2 && projKeyLog[1] = 3
        kc.AddOrUpdate (3, "three")
        let effKcWoke = kcKeyLog.Count = 2 && kcKeyLog[1] = 2

        // 7. asyncMemo: owning + policy FlightPolicy.CancelPrevious + cancellationToken + previous before withComparer
        let amSig = Signal (g, 1)
        let amCleanups = ResizeArray<int>()
        let amTokens = ResizeArray<CancellationToken>()
        let amFlights = ResizeArray<TaskCompletionSource<int>>()

        let am =
            asyncMemo {
                owning
                policy FlightPolicy.CancelPrevious
                let! ct = asyncMemo.cancellationToken
                let! prev = asyncMemo.previous
                let! k = amSig |> withComparer mod10Comparer
                onCleanup (fun () -> amCleanups.Add k)
                amTokens.Add ct
                let tcs = TaskCompletionSource<int>()
                amFlights.Add tcs
                let! v = tcs.Task
                let! p = prev.Settled
                return k * 100 + v + ValueOption.defaultValue 0 p
            }

        let amInitPending = am.TryValue = Pending
        amSig.Value <- 11
        let amEqPending = am.TryValue = Pending

        let amEqMod10 =
            amEqPending
            && amFlights.Count = 1
            && not amTokens[0].IsCancellationRequested
            && amCleanups.Count = 0

        amFlights[0].SetResult 7
        pumpUntilSettled g (fun () -> am.TryValue)
        let amV1 = am.Value
        amSig.Value <- 2
        let amPend1 = am.TryValue = Pending

        let amClean1 =
            amPend1
            && amCleanups.Count = 1
            && amCleanups[0] = 1

        amSig.Value <- 3
        let amPend2 = am.TryValue = Pending

        let amCancelledPrev =
            amPend2
            && amTokens.Count = 3
            && amTokens[1].IsCancellationRequested
            && not amTokens[2].IsCancellationRequested

        amFlights[1].TrySetCanceled() |> ignore
        amFlights[2].SetResult 8
        pumpUntilSettled g (fun () -> am.TryValue)
        let amV2 = am.Value

        // 8. stream: fold + stream.cancellationToken before withComparer
        let stSig = Signal (g, 1)
        let stTokens = ResizeArray<CancellationToken>()
        let stSinks = ResizeArray<StreamSink<int>>()
        let stCloseTcs = ResizeArray<TaskCompletionSource<unit>>()

        let stFold =
            stream {
                fold 10 (+)
                let! ct = stream.cancellationToken
                let! _k = stSig |> withComparer mod10Comparer
                stTokens.Add ct
                let tcs = TaskCompletionSource<unit>()
                stCloseTcs.Add tcs

                return
                    StreamSource.create (fun sink _ ->
                        stSinks.Add sink
                        tcs.Task :> Task)
            }

        let stInitPending = stFold.TryValue = Pending
        stSinks[0].Next 5
        let stV1 = stFold.Value
        stSig.Value <- 11

        let stEqMod10 =
            stSinks.Count = 1
            && not stTokens[0].IsCancellationRequested
            && stFold.Value = 15

        stSig.Value <- 2

        let stResetPending =
            stFold.TryValue = Pending
            && stTokens[0].IsCancellationRequested
            && stSinks.Count = 2

        stSinks[1].Next 7
        let stV2 = stFold.Value
        stCloseTcs[0].TrySetCanceled() |> ignore
        stCloseTcs[1].TrySetResult() |> ignore

        // 9. editable (previousOr) & draft (previousOr)
        let editSig = Signal (g, 5)

        let ed =
            editable {
                let! x = editSig
                let! p = editable.previousOr 10
                return x + p
            }

        let dr =
            draft {
                let! x = editSig
                let! p = draft.previousOr 20
                return x + p
            }

        let edInit = ed.Value
        let drInit = dr.Value
        ed.Value <- 999
        dr.Value <- 888
        let editsHeld = ed.Value = 999 && dr.Value = 888
        editSig.Value <- 6
        let edDropped = ed.Value
        let drKept = dr.Value
        dr.Reset ()
        let drReset = dr.Value

        // 10. boundary: pending + failed + comparer
        let bndSrc = AsyncSource<int> g

        let bnd =
            boundary {
                pending (ValueOption.defaultValue -1)

                failed (fun ex prev ->
                    if ex.Message = "recover" then
                        ValueOption.defaultValue 0 prev - 10
                    else
                        raise ex)

                comparer parityComparer
                let! v = bndSrc
                return v
            }

        let bndSeen = ResizeArray<int>()
        createEffect (fun () -> bndSeen.Add bnd.Value)
        let bndInitPending = bnd.Value = -1 && bndSeen.Count = 1
        bndSrc.Settle 5
        let bndSettleWake = bnd.Value = 5 && bndSeen.Count = 2
        bndSrc.Settle 7
        let bndParityCutoff = bnd.Value = 7 && bndSeen.Count = 2
        bndSrc.Settle 6

        let bndParityWake =
            bnd.Value = 6
            && bndSeen.Count = 3
            && bndSeen[2] = 6

        bndSrc.Fail (InvalidOperationException "recover")
        let bndRecovered = bnd.Value

        // 11. row: createProjectionWith combining unit -> 'T row reader with Signal via and!
        let rowItems = Signal (g, [ (1, 10); (2, 20) ])
        let multSig = Signal (g, 3)

        let rowProj =
            createProjectionWith
                fst
                (fun item ->
                    row {
                        let! (_, v) = item
                        and! mult = multSig
                        return v * mult
                    })
                (fun () -> rowItems.Value)

        let rowV1 = rowProj.Get 1
        multSig.Value <- 4
        let rowV2 = rowProj.Get 1
        let rowV3 = rowProj.Get 2

        [
            "CE memo 4-stage fused chain",
            fusedInit = 70
            && stage2AfterF4 = 1
            && midInit = 30
            && midNoRecompute
            && fusedUpd = 90
            && midUpd = 40
            && stage2AfterUpd = 2,
            string fusedInit
            + ":"
            + string stage2AfterF4
            + ":"
            + string midInit
            + ":"
            + string midNoRecompute
            + ":"
            + string fusedUpd
            + ":"
            + string midUpd
            "CE memo and! (Signal*Signal, Memo*Memo, 3-way mixed)",
            mSigPair.Value = 7
            && mMemoPair.Value = 430
            && mMixed3.Value = 407,
            string mSigPair.Value
            + ":"
            + string mMemoPair.Value
            + ":"
            + string mMixed3.Value
            "CE memo owning + comparer + withComparer",
            ownV1 = 11
            && ownEqMod10
            && ownOddCutoff
            && ownEvenWake,
            string ownV1
            + ":"
            + string ownEqMod10
            + ":"
            + string ownOddCutoff
            + ":"
            + string ownEvenWake
            "CE memo.previousOr", prev1 = 105 && prev2 = 115, string prev1 + ":" + string prev2
            "CE memo option identity deduplication",
            optSameRef && optIsNone && optFreshRef,
            string optSameRef
            + ":"
            + string optIsNone
            + ":"
            + string optFreshRef
            "CE memo match! over Projection.tryAt + Lookup.at + KeyedCollection.tryAt",
            desc0 = "absent"
            && desc1 = "kc:two"
            && desc2 = "Bob:30"
            && desc3 = "Bob:35",
            desc0 + "|" + desc1 + "|" + desc2 + "|" + desc3
            "CE memo return! SplitBind",
            split1 = 101
            && split2 = 106
            && split3 = 106
            && splitNoClean
            && split4 = 202
            && splitCleanups.Count = 1,
            string split1
            + ":"
            + string split2
            + ":"
            + string split3
            + ":"
            + string splitNoClean
            + ":"
            + string split4
            "CE effect node + withComparer + Projection.keys + KeyedCollection.keys",
            effInitOk
            && effProjCutOff
            && effProjWoke
            && effKcWoke,
            string effInitOk
            + ":"
            + string effProjCutOff
            + ":"
            + string effProjWoke
            + ":"
            + string effKcWoke
            "CE asyncMemo owning + CancelPrevious + cancellationToken + previous + withComparer",
            amInitPending
            && amEqMod10
            && amV1 = 107
            && amClean1
            && amCancelledPrev
            && amV2 = 415,
            string amInitPending
            + ":"
            + string amEqMod10
            + ":"
            + string amV1
            + ":"
            + string amClean1
            + ":"
            + string amCancelledPrev
            + ":"
            + string amV2
            "CE stream fold + cancellationToken + withComparer",
            stInitPending
            && stV1 = 15
            && stEqMod10
            && stResetPending
            && stV2 = 17,
            string stInitPending
            + ":"
            + string stV1
            + ":"
            + string stEqMod10
            + ":"
            + string stResetPending
            + ":"
            + string stV2
            "CE editable and draft previousOr",
            edInit = 15
            && drInit = 25
            && editsHeld
            && edDropped = 21
            && drKept = 888
            && drReset = 31,
            string edInit
            + ":"
            + string drInit
            + ":"
            + string edDropped
            + ":"
            + string drKept
            + ":"
            + string drReset
            "CE boundary pending + failed + comparer",
            bndInitPending
            && bndSettleWake
            && bndParityCutoff
            && bndParityWake
            && bndRecovered = -4,
            string bndInitPending
            + ":"
            + string bndSettleWake
            + ":"
            + string bndParityCutoff
            + ":"
            + string bndParityWake
            + ":"
            + string bndRecovered
            "CE row and! with createProjectionWith",
            rowV1 = 30 && rowV2 = 40 && rowV3 = 80,
            string rowV1
            + ":"
            + string rowV2
            + ":"
            + string rowV3
        ]

[<EntryPoint>]
let main _ =
#if RANVIER_TRACE
    for name, expected, value in TraceTextCases.cases () do
        equals ("trace text: " + name) expected (value.ToString ())
#endif
    use g = new Graph ()
    use _ = g.Activate ()

    let ints = Signal (g, [ 1; 2 ])
    let byInt = Api.createProjection id (fun x -> x * 10) (fun () -> ints.Value)
    equals "an int projection reads a row" "20" (string (byInt.Get 2))

    raises<KeyNotFoundException> "a missing int key" "The projection has no key 99." (fun () -> byInt.Get 99 |> ignore)

    let none = Signal (g, ([]: int list))
    let emptyRows = Api.createProjection id id (fun () -> none.Value)

    raises<KeyNotFoundException> "an empty head" "Projection.head: the projection has no keys." (fun () ->
        (Projection.head emptyRows).Value |> ignore)

    let least = Projection.tryMinBy float byInt
    equals "a minimum over float ranks" "Some(10)" (string least.Value)

    let names = Signal (g, [ "a"; "b" ])

    let byName =
        Reactive.Projection (Func<_>(fun () -> names.Value :> seq<string>), Func<_, _>(fun (s: string) -> s), Func<_, _>(fun (s: string) -> s.Length))

    raises<KeyNotFoundException> "a missing string key" "The projection has no key nope." (fun () -> byName.Get "nope" |> ignore)

    let twice = Signal (g, [ 1; 1 ])
    let dup = Api.createProjection id id (fun () -> twice.Value)

    raises<InvalidOperationException> "a duplicate int key" "The projection produced the key 1 twice" (fun () -> dup.Keys |> ignore)

    let counter = Signal (g, 0)

    use quiet =
        Reactive.Debounce (TimeSpan.Zero, Func<int>(fun () -> counter.Value), null, null)

    use first =
        Reactive.ThrottleFirst (TimeSpan.Zero, Func<int>(fun () -> counter.Value), null, null)

    use last =
        Reactive.ThrottleLast (TimeSpan.Zero, Func<int>(fun () -> counter.Value), null, null)

    use both =
        Reactive.Throttle (TimeSpan.Zero, Func<int>(fun () -> counter.Value), null, null)

    counter.Value <- 2
    equals "debounce publishes a zero-delay input" "2" (string quiet.Value)
    equals "leading throttle publishes a zero-delay input" "2" (string first.Value)
    equals "trailing throttle publishes a zero-delay input" "2" (string last.Value)
    equals "combined throttle publishes a zero-delay input" "2" (string both.Value)
    let mutable offThread: exn = null

    let writer =
        Thread (fun () ->
            try
                counter.Value <- 1
            with ex ->
                offThread <- ex)

    writer.Start ()
    writer.Join ()

    raises<InvalidOperationException> "an off-thread write" "A signal write ran on thread " (fun () ->
        if not (isNull offThread) then
            raise offThread)

    let answer = g.DispatchAsync (fun () -> counter.Value + 40)
    equals "an inline DispatchAsync result" "42" (string answer.Result)

    let boom = InvalidOperationException "boom"
    let failing = g.DispatchAsync (fun () -> raise boom: int)

    report
        "a DispatchAsync fault carries the work's exception"
        (failing.IsFaulted
         && obj.ReferenceEquals (failing.Exception.InnerException, boom))
        (string failing.Status)

    let queued = ref null

    let caller =
        Thread (fun () -> queued.Value <- g.DispatchAsync (fun () -> counter.Value * 10))

    caller.Start ()
    caller.Join ()
    g.Pump () |> ignore

    report
        "an off-thread DispatchAsync completes on the owner's pump"
        (queued.Value.Wait (TimeSpan.FromSeconds 5.0)
         && queued.Value.Result = 20)
        (string queued.Value.Status)

    equals "Ready 3" "Ready 3" ((Ready 3).ToString())
    equals "Ready \"x\"" "Ready \"x\"" ((Ready "x").ToString())
    equals "Pending" "Pending" (Reading<int>.Pending.ToString())
    equals "Guarded" "Guarded" (Guarded.ToString ())
    equals "KeepLatest" "KeepLatest" (KeepLatest.ToString ())

    equals
        "GraphOptions.Default"
        "{ Equality = Ranvier.JsIdentityPolicy\n  FlightPolicy = CancelPrevious\n  ThreadAffinity = Guarded\n  Dispatcher = None\n  Journal = None }"
        (GraphOptions.Default.ToString ())

    raises<NotReadyException> "a pending read" "NotReadyException " (fun () ->
        let source = AsyncSource<int>(g)
        source.Value |> ignore)

    let app = Mvu.create 0 (fun (step: int) model -> model + step)
    let doubled = app.Select (fun model -> model * 2)
    app.Dispatch 3
    equals "an Mvu selector reads the dispatched model" "6" (string doubled.Value)

    let aotInput = Signal (g, 4)
    let aotHidden = Signal (g, 100)

    let splitFSharp =
        createMemoOn (fun () -> aotInput.Value) (fun k -> k + aotHidden.Value)

    equals "createMemoOn initial pull" "104" (string splitFSharp.Value)
    aotHidden.Value <- 200
    equals "createMemoOn closes tracking inside compute" "104" (string splitFSharp.Value)

    let fusedChain =
        aotInput
        |> Signal.map (fun x -> x + 1)
        |> Memo.map (fun x -> x * 2)
        |> Memo.map (fun x -> x + 3)

    equals "Signal.map into Memo.map fused chain" "13" (string fusedChain.Value)

    let zippedMemos = Memo.zip splitFSharp fusedChain
    let zLeft, zRight = zippedMemos.Value
    equals "Memo.zip pairs derived values" "104:13" (string zLeft + ":" + string zRight)

    let offsetSig = Signal (g, 5)

    let boundSig =
        aotInput
        |> Signal.bind (fun x -> offsetSig |> Signal.map (fun o -> x * o))

    equals "Signal.bind switches and tracks inner memo" "20" (string boundSig.Value)
    offsetSig.Value <- 6
    equals "Signal.bind updates on inner signal write" "24" (string boundSig.Value)

    let splitCSharp =
        Reactive.MemoOn (Func<int>(fun () -> aotInput.Value), Func<int, int>(fun k -> k * 10 + aotHidden.Value))

    equals "Reactive.MemoOn initial pull" "240" (string splitCSharp.Value)
    aotHidden.Value <- 300
    equals "Reactive.MemoOn closes tracking inside compute" "240" (string splitCSharp.Value)

    let csSelected =
        RanvierExtensions.Select (RanvierExtensions.Select (aotInput, Func<int, int>(fun x -> x * 3)), Func<int, string>(fun x -> "cs:" + string x))

    equals "RanvierExtensions.Select chain" "cs:12" csSelected.Value

    let csZippedPair = RanvierExtensions.Zip (aotInput, offsetSig)
    let struct (zpA, zpB) = csZippedPair.Value
    equals "RanvierExtensions.Zip ValueTuple" "4,6" (string zpA + "," + string zpB)

    let csZippedSel =
        RanvierExtensions.Zip (aotInput, offsetSig, Func<int, int, int>(fun a b -> a + b))

    equals "RanvierExtensions.Zip selector" "10" (string csZippedSel.Value)

    use client = new QueryClient (g)

    let numbers =
        client.Define (fun (key: int) _ -> System.Threading.Tasks.Task.FromResult key)

    use query = numbers.Acquire 7
    use shared = numbers.Acquire 7
    equals "query value" "7" (string query.Value)
    equals "awaited query value" "7" (string (query.EnsureAsync().Result))
    use pageOwner = new Owner ()
    let owned = numbers.AcquireOwned (7, pageOwner)
    pageOwner.Dispose ()
    raises<ObjectDisposedException> "owned lease disposal" "Cannot access a disposed object." (fun () -> owned.Value |> ignore)

    let saved =
        client.Mutate (10, (fun value _ -> System.Threading.Tasks.Task.FromResult value), fun value -> [ numbers.SetIfLoaded (7, value) ])

    report "mutation applied" (saved.Result = MutationOutcome.Applied 10) "unexpected mutation outcome"
    equals "shared query reconciliation" "10" (string shared.Value)

    let failed =
        client.Mutate ((), (fun () _ -> raise (InvalidOperationException "offline"): System.Threading.Tasks.Task<int>), fun _ -> [])

    report
        "mutation failure"
        (match failed.Result with
         | MutationOutcome.RequestFailed _ -> true
         | _ -> false)
        "unexpected failure outcome"

    query.Dispose ()
    equals "remaining query lease" "10" (string shared.Value)

    for name, ok, detail in WireSmoke.checks () do
        report name ok detail

    for name, ok, detail in CeSmoke.checks () do
        report name ok detail

    if failures = 0 then
        Console.WriteLine "all checks passed"
        0
    else
        Console.WriteLine (string failures + " checks failed")
        1
