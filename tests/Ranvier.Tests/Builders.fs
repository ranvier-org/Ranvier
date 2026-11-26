module Ranvier.Tests.Builders

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Expecto
open Ranvier
open Ranvier.CE
open Ranvier.Internal
open Ranvier.Tests.Streams
open Ranvier.Tests.TimedTestClock

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

let private isNotReadyException (ex: exn) =
#if FABLE_COMPILER
    not (isNull (box ex))
#else
    match ex with
    | NotReadyException _ -> true
    | _ -> false
#endif

let private comparer (equals: 'T -> 'T -> bool) =
    { new IEqualityComparer<'T> with
        member _.Equals(a, b) =
            equals a b

        member _.GetHashCode _ = 0
    }

[<Tests>]
let tests =
    testList
        "Builders"
        [
            test "IReactive<'T> resolves Graph without Activate and ReadTracked delegates across all 8 reactive node types" {
                use g = new Graph ()
                let mutable hasAmbient = true

                try
                    createSignal 0 |> ignore
                with _ ->
                    hasAmbient <- false

                Expect.isFalse hasAmbient "precondition: graph is not activated"

                let sig1 = Signal (g, 10)
                let mem1 = Make.Memo (g, (fun _ -> sig1.Value * 2))
                let asrc1 = AsyncSource<int> g
                asrc1.Settle 30
                let am1 = Make.AsyncMemo<int>(g, (fun _ _ -> completed (sig1.Value + 5)))
                let feed = ManualStream<int>()
                let sm1 = g.Run (fun () -> createStream (fun _ -> feed.Source))

                let bnd1 =
                    g.Run (fun () -> createBoundary (fun _ -> -1) (fun _ _ -> -2) (fun _ -> sig1.Value + 100))

                let edit1 = g.Run (fun () -> createEditable (fun _ -> sig1.Value + 200))
                let clock = ManualClock ()

                let timed1 =
                    debounceWith { Clock = clock; Comparer = None } (TimeSpan.FromMilliseconds 50.) (fun () -> sig1.Value + 300) g

                let rSig = sig1 :> IReactive<int>
                let rMem = mem1 :> IReactive<int>
                let rAsrc = asrc1 :> IReactive<int>
                let rAm = am1 :> IReactive<int>
                let rSm = sm1 :> IReactive<int>
                let rBnd = bnd1 :> IReactive<int>
                let rEdit = edit1 :> IReactive<int>
                let rTimed = timed1 :> IReactive<int>

                Expect.isTrue (obj.ReferenceEquals (rSig.Graph, g)) "Signal.Graph"
                Expect.isTrue (obj.ReferenceEquals (rMem.Graph, g)) "Memo.Graph"
                Expect.isTrue (obj.ReferenceEquals (rAsrc.Graph, g)) "AsyncSource.Graph"
                Expect.isTrue (obj.ReferenceEquals (rAm.Graph, g)) "AsyncMemo.Graph"
                Expect.isTrue (obj.ReferenceEquals (rSm.Graph, g)) "StreamMemo.Graph"
                Expect.isTrue (obj.ReferenceEquals (rBnd.Graph, g)) "Boundary.Graph"
                Expect.isTrue (obj.ReferenceEquals (rEdit.Graph, g)) "Editable.Graph"
                Expect.isTrue (obj.ReferenceEquals (rTimed.Graph, g)) "Timed.Graph"
                Expect.isTrue (obj.ReferenceEquals (edit1.ValueMemo.Graph, g)) "Editable.ValueMemo.Graph"

                let readSig = Make.Memo (g, (fun _ -> rSig.ReadTracked ()))
                let readMem = Make.Memo (g, (fun _ -> rMem.ReadTracked ()))
                let readAsrc = Make.Memo (g, (fun _ -> rAsrc.ReadTracked ()))
                let readAm = Make.Memo (g, (fun _ -> rAm.ReadTracked ()))
                let readSm = Make.Memo (g, (fun _ -> rSm.ReadTracked ()))
                let readBnd = Make.Memo (g, (fun _ -> rBnd.ReadTracked ()))
                let readEdit = Make.Memo (g, (fun _ -> rEdit.ReadTracked ()))
                let readTimed = Make.Memo (g, (fun _ -> rTimed.ReadTracked ()))

                Expect.equal readSig.Value 10 "Signal ReadTracked"
                Expect.equal readMem.Value 20 "Memo ReadTracked"
                Expect.equal readAsrc.Value 30 "AsyncSource ReadTracked"
                Expect.equal readBnd.Value 110 "Boundary ReadTracked"
                Expect.equal readEdit.Value 210 "Editable ReadTracked"
                Expect.equal readTimed.Value 310 "Timed ReadTracked"

                am1.TryValue |> ignore
                Expect.equal readAm.Value 15 "AsyncMemo ReadTracked"

                Expect.equal sm1.TryValue Pending "StreamMemo starts Pending"
                Expect.throws (fun () -> rSm.ReadTracked () |> ignore) "StreamMemo ReadTracked raises NotReadyException when Pending"
                feed.Push 42
                Expect.equal readSm.Value 42 "StreamMemo ReadTracked after push"

                sig1.Value <- 11
                Expect.equal readSig.Value 11 "Signal ReadTracked tracks changes"
                Expect.equal readMem.Value 22 "Memo ReadTracked tracks changes"
                Expect.equal readBnd.Value 111 "Boundary ReadTracked tracks changes"
                Expect.equal readEdit.Value 211 "Editable ReadTracked tracks changes"
                clock.AdvanceTo 50.
                Expect.equal readTimed.Value 311 "Timed ReadTracked tracks changes"
            }

            test "BoundaryAdapter, TimedAdapter, and StreamMemoAdapter work via CombinatorMemos.createSourceMap" {
                use g = new Graph ()

                // BoundaryAdapter: ready, pending fallback, pending suspension, unhandled failure + ErrorOrigin, recovery
                let sigB = Signal (g, 5)
                let pendingSrc = AsyncSource<int> g
                let usePending = Signal (g, false)
                let throwErr = Signal (g, false)

                let boundaryNoFallback =
                    g.Run (fun () ->
                        createSuspense (fun _ -> -1) (fun _ ->
                            if usePending.Value then pendingSrc.Value
                            elif throwErr.Value then failwith "bnd-boom"
                            else sigB.Value))

                let boundaryRaw =
                    g.Run (fun () ->
                        createErrorBoundary (fun ex _ -> if ex.Message = "recover" then 999 else raise ex) (fun _ ->
                            if usePending.Value then pendingSrc.Value
                            elif throwErr.Value then failwith "raw-boom"
                            else sigB.Value))

                let mappedWithFallback =
                    CombinatorMemos.createSourceMap (fun x -> x * 10) (BoundaryAdapter<int> boundaryNoFallback)

                let mappedRaw =
                    CombinatorMemos.createSourceMap (fun x -> x + 1) (BoundaryAdapter<int> boundaryRaw)

                Expect.equal mappedWithFallback.Value 50 "BoundaryAdapter initial ready"
                Expect.equal mappedRaw.Value 6 "BoundaryAdapter raw initial ready"

                usePending.Value <- true
                Expect.equal mappedWithFallback.Value -10 "BoundaryAdapter surfaces pending fallback value"
                Expect.equal mappedRaw.TryValue Pending "BoundaryAdapter propagates Pending when no pending handler"

                usePending.Value <- false
                throwErr.Value <- true
                let exRaw = failedWith mappedRaw.TryValue
                Expect.equal exRaw.Message "raw-boom" "BoundaryAdapter surfaces unhandled error"
                Expect.isTrue (obj.ReferenceEquals (mappedRaw.ErrorOrigin, boundaryRaw :> INode)) "BoundaryAdapter preserves Boundary ErrorOrigin"

                throwErr.Value <- false
                sigB.Value <- 7
                Expect.equal mappedRaw.Value 8 "BoundaryAdapter recovers cleanly"

                // TimedAdapter: ready, pending capture, failure, timed update, cutoff
                let clock = ManualClock ()
                let timedGate = Signal (g, false)
                let timedIn = Signal (g, 3)

                let timedNode =
                    debounceWith
                        { Clock = clock; Comparer = None }
                        (TimeSpan.FromMilliseconds 100.)
                        (fun () -> if timedGate.Value then pendingSrc.Value else timedIn.Value)
                        g

                let mappedTimed =
                    CombinatorMemos.createSourceMap (fun x -> x * 2) (TimedAdapter<int> timedNode)

                Expect.equal mappedTimed.Value 6 "TimedAdapter initial ready"
                timedIn.Value <- 9
                Expect.equal mappedTimed.Value 6 "TimedAdapter debounced before window elapses"
                clock.AdvanceTo 100.
                Expect.equal mappedTimed.Value 18 "TimedAdapter updates after timed admission"

                let timedPending =
                    debounceWith { Clock = clock; Comparer = None } (TimeSpan.FromMilliseconds 100.) (fun () -> pendingSrc.Value) g

                let mappedTimedPending =
                    CombinatorMemos.createSourceMap (fun x -> x + 1) (TimedAdapter<int> timedPending)

                Expect.equal mappedTimedPending.TryValue Pending "TimedAdapter surfaces initial Pending"
                pendingSrc.Settle 20
                Expect.equal mappedTimedPending.Value 21 "TimedAdapter wakes on first settled capture"

                // StreamMemoAdapter: accessible internally from Builders
                let streamFeed = ManualStream<int>()
                let sm = g.Run (fun () -> createStream (fun _ -> streamFeed.Source))

                let mappedSm =
                    CombinatorMemos.createSourceMap (fun x -> x + 100) (StreamMemoAdapter<int> sm)

                Expect.equal mappedSm.TryValue Pending "StreamMemoAdapter initially Pending"
                streamFeed.Push 5
                Expect.equal mappedSm.Value 105 "StreamMemoAdapter updates on push"
            }

            test "Projection.at, Projection.tryAt, Projection.keys, Lookup.at, Lookup.tryAt, and KeyedCollection descriptors" {
                use g = new Graph ()
                let itemsSig = Signal (g, [ (1, "a"); (2, "b") ])

                let proj = g.Run (fun () -> createProjection fst snd (fun () -> itemsSig.Value))

                let at1 = Projection.at 1 proj :> IReactive<string>
                let tryMissing = Projection.tryAt 99 proj :> IReactive<string option>
                let keysDesc = Projection.keys proj :> IReactive<int[]>

                Expect.isTrue (obj.ReferenceEquals (at1.Graph, g)) "Projection.at Graph without Activate"
                Expect.isTrue (obj.ReferenceEquals (tryMissing.Graph, g)) "Projection.tryAt Graph without Activate"
                Expect.isTrue (obj.ReferenceEquals (keysDesc.Graph, g)) "Projection.keys Graph without Activate"

                let mAt1 = Make.Memo (g, (fun _ -> at1.ReadTracked ()))
                let mTry99 = Make.Memo (g, (fun _ -> tryMissing.ReadTracked ()))
                let mKeys = Make.Memo (g, (fun _ -> keysDesc.ReadTracked ()))

                Expect.equal mAt1.Value "a" "Projection.at reads existing row"
                Expect.equal mTry99.Value None "Projection.tryAt returns None for absent key"
                Expect.sequenceEqual mKeys.Value [ 1; 2 ] "Projection.keys reads key array"

                let atMissing = Projection.at 99 proj :> IReactive<string>

                Expect.throwsT<KeyNotFoundException>
                    (fun () ->
                        g.Run (fun () -> atMissing.ReadTracked ())
                        |> ignore)
                    "Projection.at raises KeyNotFoundException on missing key"

                itemsSig.Value <- [ (1, "a2"); (2, "b"); (99, "z") ]
                Expect.equal mAt1.Value "a2" "Projection.at tracks row edits"
                Expect.equal mTry99.Value (Some "z") "Projection.tryAt tracks key addition"
                Expect.sequenceEqual mKeys.Value [ 1; 2; 99 ] "Projection.keys tracks membership changes"

                // Lookup.tryAt on unread key returns None untracked without creating a cell
                let mapSig = Signal (g, Map.ofList [ ("k1", 10); ("k2", 20) ])

                let lookup =
                    g.Run (fun () ->
                        createLookup
                            (fun (m: Map<string, int>) k -> Map.tryFind k m |> Option.defaultValue -1)
                            (fun prev next ->
                                seq {
                                    for KeyValue (k, v) in prev do
                                        if Map.tryFind k next <> Some v then
                                            yield k

                                    for KeyValue (k, v) in next do
                                        if Map.tryFind k prev <> Some v then
                                            yield k
                                })
                            (fun () -> mapSig.Value))

                let lTry1 = Lookup.tryAt "k1" lookup :> IReactive<int option>
                let lAt1 = Lookup.at "k1" lookup :> IReactive<int>

                Expect.isTrue (obj.ReferenceEquals (lTry1.Graph, g)) "Lookup.tryAt Graph without Activate"
                Expect.isTrue (obj.ReferenceEquals (lAt1.Graph, g)) "Lookup.at Graph without Activate"

                let unreadMemoRuns = ref 0

                let mTryCold =
                    Make.Memo (
                        g,
                        (fun _ ->
                            unreadMemoRuns.Value <- unreadMemoRuns.Value + 1
                            lTry1.ReadTracked ())
                    )

                Expect.equal mTryCold.Value None "Lookup.tryAt returns None when no cell exists"
                Expect.equal lookup.CellCount 0 "Lookup.tryAt creates no cell on cold key"

                // Updating mapSig does NOT wake mTryCold because None was untracked!
                mapSig.Value <- Map.ofList [ ("k1", 15); ("k2", 20) ]
                Expect.equal mTryCold.Value None "Lookup.tryAt remains None untracked while no cell exists"
                Expect.equal unreadMemoRuns.Value 1 "Lookup.tryAt untracked None did not subscribe"
                Expect.equal lookup.CellCount 0 "still zero cells"

                // Lookup.at creates the cell and tracks it
                let mAt1 = Make.Memo (g, (fun _ -> lAt1.ReadTracked ()))
                Expect.equal mAt1.Value 15 "Lookup.at creates and reads live cell"
                Expect.equal lookup.CellCount 1 "Lookup.at created 1 cell"

                // Now Lookup.tryAt on the live cell returns Some 15 tracked!
                let mTryLive = Make.Memo (g, (fun _ -> lTry1.ReadTracked ()))
                Expect.equal mTryLive.Value (Some 15) "Lookup.tryAt reads Some on live cell"

                mapSig.Value <- Map.ofList [ ("k1", 25); ("k2", 20) ]
                Expect.equal mAt1.Value 25 "Lookup.at tracks cell update"
                Expect.equal mTryLive.Value (Some 25) "Lookup.tryAt tracks live cell update"

                // KeyedCollection descriptors delegate to collection.Rows
                let kc = g.Run (fun () -> createKeyedCollection fst)
                kc.AddOrUpdate (1, "one")
                kc.AddOrUpdate (2, "two")

                let kcAt1 = KeyedCollection.at 1 kc :> IReactive<int * string>
                let kcTry3 = KeyedCollection.tryAt 3 kc :> IReactive<(int * string) option>
                let kcKeys = KeyedCollection.keys kc :> IReactive<int[]>

                Expect.isTrue (obj.ReferenceEquals (kcAt1.Graph, g)) "KeyedCollection.at Graph"
                Expect.isTrue (obj.ReferenceEquals (kcTry3.Graph, g)) "KeyedCollection.tryAt Graph"
                Expect.isTrue (obj.ReferenceEquals (kcKeys.Graph, g)) "KeyedCollection.keys Graph"

                let mKcAt = Make.Memo (g, (fun _ -> kcAt1.ReadTracked ()))
                let mKcTry = Make.Memo (g, (fun _ -> kcTry3.ReadTracked ()))
                let mKcKeys = Make.Memo (g, (fun _ -> kcKeys.ReadTracked ()))

                Expect.equal mKcAt.Value (1, "one") "KeyedCollection.at initial"
                Expect.equal mKcTry.Value None "KeyedCollection.tryAt missing"
                Expect.sequenceEqual mKcKeys.Value [ 1; 2 ] "KeyedCollection.keys initial"

                kc.AddOrUpdate (3, "three")
                Expect.equal mKcTry.Value (Some (3, "three")) "KeyedCollection.tryAt after upsert"
                Expect.sequenceEqual mKcKeys.Value [ 1; 2; 3 ] "KeyedCollection.keys after upsert"
            }

            test "Null argument validation on descriptors, withComparer, Merged* carriers, and *On/createReactiveBind" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 1
                let validCmp = EqualityComparer<int>.Default
                let nullCmp = Unchecked.defaultof<IEqualityComparer<int>>
                let nullProj = Unchecked.defaultof<Projection<int, int>>
                let nullLookup = Unchecked.defaultof<Lookup<int, int>>
                let nullKc = Unchecked.defaultof<KeyedCollection<int, int>>
                let nullReactive = Unchecked.defaultof<IReactive<int>>
                let nullSig = Unchecked.defaultof<Signal<int>>
                let nullMemo = Unchecked.defaultof<Memo<int>>

                Expect.throwsT<ArgumentNullException> (fun () -> Projection.at 1 nullProj |> ignore) "Projection.at null"
                Expect.throwsT<ArgumentNullException> (fun () -> Projection.tryAt 1 nullProj |> ignore) "Projection.tryAt null"
                Expect.throwsT<ArgumentNullException> (fun () -> Projection.keys nullProj |> ignore) "Projection.keys null"
                Expect.throwsT<ArgumentNullException> (fun () -> Lookup.at 1 nullLookup |> ignore) "Lookup.at null"
                Expect.throwsT<ArgumentNullException> (fun () -> Lookup.tryAt 1 nullLookup |> ignore) "Lookup.tryAt null"
                Expect.throwsT<ArgumentNullException> (fun () -> KeyedCollection.at 1 nullKc |> ignore) "KeyedCollection.at null"
                Expect.throwsT<ArgumentNullException> (fun () -> KeyedCollection.tryAt 1 nullKc |> ignore) "KeyedCollection.tryAt null"
                Expect.throwsT<ArgumentNullException> (fun () -> KeyedCollection.keys nullKc |> ignore) "KeyedCollection.keys null"

                Expect.throwsT<ArgumentNullException> (fun () -> s |> withComparer nullCmp |> ignore) "withComparer null comparer"
                Expect.throwsT<ArgumentNullException> (fun () -> nullReactive |> withComparer validCmp |> ignore) "withComparer null source"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        ComparedReactive<int>(nullReactive, validCmp)
                        |> ignore)
                    "ComparedReactive null source"

                Expect.throwsT<ArgumentNullException> (fun () -> ComparedReactive<int>(s, nullCmp) |> ignore) "ComparedReactive null comparer"

                Expect.throwsT<ArgumentNullException> (fun () -> MergedSignals<int, int>(nullSig, s) |> ignore) "MergedSignals null source1"
                Expect.throwsT<ArgumentNullException> (fun () -> MergedSignals<int, int>(s, nullSig) |> ignore) "MergedSignals null source2"
                let m = createMemo (fun _ -> 1)
                Expect.throwsT<ArgumentNullException> (fun () -> MergedMemos<int, int>(nullMemo, m) |> ignore) "MergedMemos null source1"
                Expect.throwsT<ArgumentNullException> (fun () -> MergedMemos<int, int>(m, nullMemo) |> ignore) "MergedMemos null source2"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        MergedReactive<int, int>(nullReactive, s)
                        |> ignore)
                    "MergedReactive null source1"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        MergedReactive<int, int>(s, nullReactive)
                        |> ignore)
                    "MergedReactive null source2"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        Memo.CreateOn (g, (fun () -> 1), id, ScopeMode.Pure, ValueNone, ?keyComparer = Some nullCmp)
                        |> ignore)
                    "Memo.CreateOn null keyComparer"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        AsyncMemo.CreateOn (
                            g,
                            (fun () -> 1),
                            (fun x _ _ -> completed x),
                            ScopeMode.PureAsync,
                            FlightPolicy.CancelPrevious,
                            ?keyComparer = Some nullCmp
                        )
                        |> ignore)
                    "AsyncMemo.CreateOn null keyComparer"

                let validStart =
                    fun (_: int) (_: CancellationToken) -> StreamSource.create (fun _ _ -> TaskCompletionSource<unit>().Task :> Task)

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        StreamMemo.CreateOn (g, (fun () -> 1), validStart, ?keyComparer = Some nullCmp)
                        |> ignore)
                    "StreamMemo.CreateOn null keyComparer"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        StreamMemo.CreateFoldOn (g, 0, (+), (fun () -> 1), validStart, ?keyComparer = Some nullCmp)
                        |> ignore)
                    "StreamMemo.CreateFoldOn null keyComparer"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        CombinatorMemos.createReactiveBind (Unchecked.defaultof<Graph>, (fun () -> 1), (fun _ -> m), ValueNone, ValueNone)
                        |> ignore)
                    "createReactiveBind null graph"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        CombinatorMemos.createReactiveBind (g, Unchecked.defaultof<unit -> int>, (fun _ -> m), ValueNone, ValueNone)
                        |> ignore)
                    "createReactiveBind null readKey"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        CombinatorMemos.createReactiveBind (g, (fun () -> 1), Unchecked.defaultof<int -> Memo<int>>, ValueNone, ValueNone)
                        |> ignore)
                    "createReactiveBind null binder"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        CombinatorMemos.createReactiveBind (g, (fun () -> 1), (fun _ -> m), ValueSome nullCmp, ValueNone)
                        |> ignore)
                    "createReactiveBind null keyComparer"

                Expect.throwsT<ArgumentNullException>
                    (fun () ->
                        CombinatorMemos.createReactiveBind (g, (fun () -> 1), (fun _ -> m), ValueNone, ValueSome nullCmp)
                        |> ignore)
                    "createReactiveBind null outputComparer"
            }

            test "Merged* carriers enforce same-graph membership, preserve left-to-right precedence, and InternalCeState manages evalDepth" {
                use g1 = new Graph ()
                use g2 = new Graph ()

                let s1a = Signal (g1, 1)
                let s1b = Signal (g1, 2)
                let s2 = Signal (g2, 3)

                let m1a = Make.Memo (g1, (fun _ -> s1a.Value * 10))
                let m1b = Make.Memo (g1, (fun _ -> s1b.Value * 10))
                let m2 = Make.Memo (g2, (fun _ -> s2.Value * 10))

                let exSig = Expect.throwsC (fun () -> MergedSignals<int, int>(s1a, s2) |> ignore) id
                Expect.isTrue (isInvalidOperation exSig) "MergedSignals rejects cross-graph signals"
                let exMem = Expect.throwsC (fun () -> MergedMemos<int, int>(m1a, m2) |> ignore) id
                Expect.isTrue (isInvalidOperation exMem) "MergedMemos rejects cross-graph memos"

                let exReact =
                    Expect.throwsC (fun () -> MergedReactive<int, int>(s1a, m2) |> ignore) id

                Expect.isTrue (isInvalidOperation exReact) "MergedReactive rejects cross-graph sources"

                let ms = MergedSignals<int, int>(s1a, s1b)
                let mm = MergedMemos<int, int>(m1a, m1b)
                let mr = MergedReactive<struct (int * int), struct (int * int)>(ms, mm)

                Expect.isTrue (obj.ReferenceEquals ((ms :> IReactive<_>).Graph, g1)) "MergedSignals.Graph"
                Expect.isTrue (obj.ReferenceEquals ((mm :> IReactive<_>).Graph, g1)) "MergedMemos.Graph"
                Expect.isTrue (obj.ReferenceEquals ((mr :> IReactive<_>).Graph, g1)) "MergedReactive.Graph"

                let vNested =
                    (mr :> IReactive<struct (struct (int * int) * struct (int * int))>).ReadTracked()

                Expect.equal vNested (struct (struct (1, 2), struct (10, 20))) "MergedReactive ReadTracked returns nested struct tuple"

                // Left-to-right precedence: pending left wins before reading failing right
                let leftPending = AsyncSource<int> g1
                let rightFailed = AsyncSource<int> g1
                rightFailed.Fail (InvalidOperationException "right-err")

                let prec =
                    MergedReactive<int, int>(leftPending, rightFailed) :> IReactive<struct (int * int)>

                let exLeft = Expect.throwsC (fun () -> prec.ReadTracked () |> ignore) id
                Expect.isTrue (isNotReadyException exLeft) "left Pending takes precedence over right Failed"
                leftPending.Settle 7
                let exRight = Expect.throwsC (fun () -> prec.ReadTracked () |> ignore) id
                Expect.equal exRight.Message "right-err" "right Failed surfaces once left settles"

                // InternalCeState evalDepth lifecycle + ColdPhase/BoundPhase visibility
                let _cold = Unchecked.defaultof<ColdPhase>
                let _bound = Unchecked.defaultof<BoundPhase>
                Expect.isFalse InternalCeState.IsEvaluating "initial evalDepth = 0"
                let p0 = InternalCeState.EnterEval ()
                Expect.isTrue InternalCeState.IsEvaluating "evalDepth = 1"
                let p1 = InternalCeState.SuspendForNestedRun ()
                Expect.isFalse InternalCeState.IsEvaluating "suspended evalDepth = 0"
                InternalCeState.ExitEval p1
                Expect.isTrue InternalCeState.IsEvaluating "restored evalDepth = 1"
                InternalCeState.ExitEval p0
                Expect.isFalse InternalCeState.IsEvaluating "exited evalDepth = 0"
            }

            test "withComparer and custom keyComparer on Memo.CreateOn, AsyncMemo.CreateOn, StreamMemo.CreateOn/CreateFoldOn, and createReactiveBind" {
                use g = new Graph ()
                use _ = g.Activate ()
                let mod10 = comparer (fun (a: int) b -> a % 10 = b % 10)

                // 1. Memo.CreateOn with ?keyComparer via ComparedReactive
                let sigMemo = createSignal 1
                let wrappedMemo = sigMemo |> withComparer mod10
                let memoRuns = ref 0
                let memoCleanups = ResizeArray<int>()

                let m =
                    Memo<int>
                        .CreateOn(
                            g,
                            (wrappedMemo :> IReactive<int>).ReadTracked,
                            (fun k ->
                                memoRuns.Value <- memoRuns.Value + 1
                                onCleanup (fun () -> memoCleanups.Add k)
                                k * 100),
                            ScopeMode.Owning,
                            ValueNone,
                            ?keyComparer = Some wrappedMemo.Comparer
                        )

                Expect.equal m.Value 100 "Memo.CreateOn initial"
                Expect.equal memoRuns.Value 1 "ran once"

                sigMemo.Value <- 11
                Expect.equal m.Value 100 "equal key under mod10 cuts off Phase 2"
                Expect.equal memoRuns.Value 1 "Phase 2 skipped and scope preserved"
                Expect.isEmpty memoCleanups "no cleanup on equal key"

                sigMemo.Value <- 12
                Expect.equal m.Value 1200 "unequal key under mod10 runs Phase 2"
                Expect.equal memoRuns.Value 2 "ran twice"
                Expect.sequenceEqual memoCleanups [ 1 ] "old scope discharged on unequal key"

                // 2. AsyncMemo.CreateOn with ?keyComparer
                let sigAsync = createSignal 1
                let wrappedAsync = sigAsync |> withComparer mod10
                let flights = ResizeArray<Flight<string>>()
                let tokens = ResizeArray<CancellationToken>()

                let am =
                    AsyncMemo<string>
                        .CreateOn(
                            g,
                            (wrappedAsync :> IReactive<int>).ReadTracked,
                            (fun k _ token ->
                                tokens.Add token
                                let f = Flight<string>()
                                flights.Add f
                                f.Task),
                            ScopeMode.PureAsync,
                            FlightPolicy.CancelPrevious,
                            ?keyComparer = Some wrappedAsync.Comparer
                        )

                Expect.equal am.TryValue Pending "AsyncMemo.CreateOn initial flight"
                Expect.equal flights.Count 1 "1 flight started"

                sigAsync.Value <- 21
                Expect.equal am.TryValue Pending "equal key under mod10 keeps flight"
                Expect.equal flights.Count 1 "still 1 flight"
                Expect.isFalse tokens[0].IsCancellationRequested "flight 1 not cancelled"

                flights[0].Settle "ok-1"
                Expect.equal am.Value "ok-1" "flight 1 result"

                sigAsync.Value <- 22
                Expect.equal am.TryValue Pending "unequal key starts flight 2"
                Expect.equal flights.Count 2 "2 flights started"
                flights[1].Settle "ok-2"
                Expect.equal am.Value "ok-2" "flight 2 result"

                // 3. StreamMemo.CreateOn and CreateFoldOn with ?keyComparer
                let sigStream = createSignal 1
                let wrappedStream = sigStream |> withComparer mod10
                let latestFeed = ManualStream<string>()
                let foldFeed = ManualStream<int>()

                let smLatest =
                    StreamMemo<string>
                        .CreateOn(
                            g,
                            (wrappedStream :> IReactive<int>).ReadTracked,
                            (fun _ _ -> latestFeed.Source),
                            ?keyComparer = Some wrappedStream.Comparer
                        )

                let smFold =
                    StreamMemo<int>
                        .CreateFoldOn(
                            g,
                            10,
                            (+),
                            (wrappedStream :> IReactive<int>).ReadTracked,
                            (fun _ _ -> foldFeed.Source),
                            ?keyComparer = Some wrappedStream.Comparer
                        )

                Expect.equal smLatest.TryValue Pending "StreamMemo.CreateOn pending"
                Expect.equal smFold.TryValue Pending "StreamMemo.CreateFoldOn pending"
                latestFeed.Push "hello"
                foldFeed.Push 5
                Expect.equal smLatest.Value "hello" "StreamMemo.CreateOn pushed"
                Expect.equal smFold.Value 15 "StreamMemo.CreateFoldOn folded"

                sigStream.Value <- 31
                Expect.equal smLatest.Value "hello" "equal key under mod10 keeps latest stream"
                Expect.equal smFold.Value 15 "equal key under mod10 keeps folded accumulator"
                Expect.equal latestFeed.Starts 1 "1 latest stream start"
                Expect.equal foldFeed.Starts 1 "1 folded stream start"
                Expect.isFalse latestFeed.Latest.Token.IsCancellationRequested "latest token alive"
                Expect.isFalse foldFeed.Latest.Token.IsCancellationRequested "fold token alive"

                sigStream.Value <- 32
                Expect.equal smLatest.TryValue Pending "unequal key restarts latest stream"
                Expect.equal smFold.TryValue Pending "unequal key restarts folded stream"
                Expect.equal latestFeed.Starts 2 "2 latest stream starts"
                Expect.equal foldFeed.Starts 2 "2 folded stream starts"
                foldFeed.Push 7
                Expect.equal smFold.Value 17 "folded from reset seed 10 + 7"

                // 4. CombinatorMemos.createReactiveBind with MergedReactive + keyComparer + outputComparer
                let sigA = createSignal 1
                let sigB = createSignal 100
                let merged = MergedReactive<int, int>(sigA, sigB)

                let keyFirstMod10 =
                    comparer (fun (struct (a1, _)) (struct (a2, _)) -> a1 % 10 = a2 % 10)

                let outParity = comparer (fun (a: int) b -> a % 2 = b % 2)
                let inner1Val = createSignal 10
                let inner2Val = createSignal 20
                let bindRuns = ref 0
                let bindCleanups = ResizeArray<int>()
                let downstreamSeen = ResizeArray<int>()

                let boundMemo =
                    CombinatorMemos.createReactiveBind (
                        g,
                        (merged :> IReactive<struct (int * int)>).ReadTracked,
                        (fun (struct (a, _)) ->
                            bindRuns.Value <- bindRuns.Value + 1
                            let keyMod = a % 10
                            onCleanup (fun () -> bindCleanups.Add keyMod)

                            if keyMod = 1 then
                                createMemo (fun _ -> inner1Val.Value)
                            else
                                createMemo (fun _ -> inner2Val.Value)),
                        ValueSome keyFirstMod10,
                        ValueSome outParity
                    )

                createEffect (fun () -> downstreamSeen.Add boundMemo.Value)
                Expect.equal boundMemo.Value 10 "createReactiveBind initial"
                Expect.equal bindRuns.Value 1 "binder ran once"
                Expect.sequenceEqual downstreamSeen [ 10 ] "downstream saw 10"

                // sigB changes -> unequal raw struct tuple, equal under keyFirstMod10 -> binder skipped!
                sigB.Value <- 999
                Expect.equal boundMemo.Value 10 "equal under keyComparer keeps innerMemo"
                Expect.equal bindRuns.Value 1 "binder still ran once"
                Expect.isEmpty bindCleanups "no cleanup"

                // inner1Val changes to even 12 -> outputParity cuts off downstream, innerMemo change still updates Peek/Value!
                inner1Val.Value <- 12
                Expect.equal boundMemo.Value 12 "innerMemo update surfaced"
                Expect.equal bindRuns.Value 1 "binder NOT re-run when innerMemo changed"
                Expect.sequenceEqual downstreamSeen [ 10 ] "downstream suppressed by outputParity"

                // inner1Val changes to odd 13 -> downstream wakes!
                inner1Val.Value <- 13
                Expect.sequenceEqual downstreamSeen [ 10; 13 ] "downstream woken on unequal output"

                // sigA changes to 2 -> unequal key under keyFirstMod10 -> old scope discharged, binds inner2Val
                sigA.Value <- 2
                Expect.equal boundMemo.Value 20 "switched to inner2"
                Expect.equal bindRuns.Value 2 "binder ran second time"
                Expect.sequenceEqual bindCleanups [ 1 ] "old Phase 2 scope discharged"
                Expect.sequenceEqual downstreamSeen [ 10; 13; 20 ] "downstream saw 20"
            }

            test
                "Review Focus #2: throwing custom keyComparer surfaces Phase-1 Status.Error, preserves Phase-2 value/flight/stream/scope, and recovers on valid settle" {
                use g = new Graph ()
                use _ = g.Activate ()
                let armThrow = ref false

                let throwingCmp =
                    comparer (fun (struct (a: int, _: int)) (struct (b: int, _: int)) ->
                        if armThrow.Value then
                            raise (InvalidOperationException "throwing keyComparer")

                        a % 10 = b % 10)

                // 1. Memo.CreateOn (Owning) over and!-merged key |> withComparer throwingCmp
                let mLeft = createSignal 1
                let mRight = createSignal 100

                let mWrapped =
                    MergedReactive<int, int>(mLeft, mRight)
                    |> withComparer throwingCmp

                let tick = createSignal 0
                let mRuns = ref 0
                let mScopeLog = ResizeArray<string>()

                let mNode =
                    Memo<int>
                        .CreateOn(
                            g,
                            (mWrapped :> IReactive<struct (int * int)>).ReadTracked,
                            (fun (struct (k, _)) ->
                                mRuns.Value <- mRuns.Value + 1
                                createEffect (fun () -> mScopeLog.Add $"m:{k}:{tick.Value}")
                                onCleanup (fun () -> mScopeLog.Add $"mClean:{k}")
                                k * 10),
                            ScopeMode.Owning,
                            ValueNone,
                            ?keyComparer = Some mWrapped.Comparer
                        )

                Expect.equal mNode.Value 10 "Memo initial ready"
                Expect.sequenceEqual mScopeLog [ "m:1:0" ] "Memo Phase-2 scope active"

                armThrow.Value <- true
                mLeft.Value <- 2
                let mEx = failedWith mNode.TryValue
                Expect.equal mEx.Message "throwing keyComparer" "Memo surfaces Phase-1 throwingCmp exception"
                Expect.equal mNode.Status Status.Error "Memo enters Status.Error"
                Expect.equal mNode.Peek 10 "Memo preserves cached Peek value"
                Expect.equal mRuns.Value 1 "Memo did not re-run Phase 2"
                tick.Value <- 1
                Expect.sequenceEqual mScopeLog [ "m:1:0"; "m:1:1" ] "Memo preserves Phase-2 scope across keyComparer throw"

                // Recover to equal key (11 % 10 = 1)
                armThrow.Value <- false
                mLeft.Value <- 11
                Expect.equal mNode.Value 10 "Memo recovers cached 10 on valid equal settle"
                Expect.equal mNode.Status Status.None "Memo Status clears to None"
                Expect.equal mRuns.Value 1 "Memo still did not re-run Phase 2"
                Expect.sequenceEqual mScopeLog [ "m:1:0"; "m:1:1" ] "Memo Phase-2 scope intact after recovery"

                // 2. AsyncMemo.CreateOn over throwingCmp preserves in-flight task & cancellation token
                let aLeft = createSignal 1
                let aRight = createSignal 100

                let aWrapped =
                    MergedReactive<int, int>(aLeft, aRight)
                    |> withComparer throwingCmp

                let flights = ResizeArray<Flight<string>>()
                let tokens = ResizeArray<CancellationToken>()

                let aNode =
                    AsyncMemo<string>
                        .CreateOn(
                            g,
                            (aWrapped :> IReactive<struct (int * int)>).ReadTracked,
                            (fun _ _ token ->
                                tokens.Add token
                                let f = Flight<string>()
                                flights.Add f
                                f.Task),
                            ScopeMode.PureAsync,
                            FlightPolicy.CancelPrevious,
                            ?keyComparer = Some aWrapped.Comparer
                        )

                Expect.equal aNode.TryValue Pending "AsyncMemo initial flight running"
                armThrow.Value <- true
                aLeft.Value <- 2
                let aEx = failedWith aNode.TryValue
                Expect.equal aEx.Message "throwing keyComparer" "AsyncMemo surfaces Phase-1 throwingCmp exception"
                Expect.equal aNode.Status Status.Error "AsyncMemo enters Status.Error"
                Expect.isFalse tokens[0].IsCancellationRequested "AsyncMemo keeps active flight uncancelled"
                Expect.equal flights.Count 1 "AsyncMemo launches no new flight"

                flights[0].Settle "flight-done"
                Expect.equal (failedWith aNode.TryValue).Message "throwing keyComparer" "Phase-1 Error still overlays until Phase 1 recovers"
                armThrow.Value <- false
                aLeft.Value <- 11
                Expect.equal aNode.Value "flight-done" "AsyncMemo recovers and publishes settled flight value"
                Expect.equal flights.Count 1 "still only 1 flight"

                // 3. StreamMemo.CreateFoldOn over throwingCmp preserves open stream & accumulated items
                let sLeft = createSignal 1
                let sRight = createSignal 100

                let sWrapped =
                    MergedReactive<int, int>(sLeft, sRight)
                    |> withComparer throwingCmp

                let feed = ManualStream<int>()

                let sNode =
                    StreamMemo<int>
                        .CreateFoldOn(
                            g,
                            0,
                            (+),
                            (sWrapped :> IReactive<struct (int * int)>).ReadTracked,
                            (fun _ _ -> feed.Source),
                            ?keyComparer = Some sWrapped.Comparer
                        )

                Expect.equal sNode.TryValue Pending "StreamMemo initial Pending"
                feed.Push 5
                Expect.equal sNode.Value 5 "StreamMemo initial pushed item"

                armThrow.Value <- true
                sLeft.Value <- 2
                let sEx = failedWith sNode.TryValue
                Expect.equal sEx.Message "throwing keyComparer" "StreamMemo surfaces Phase-1 throwingCmp exception"
                Expect.equal sNode.Status Status.Error "StreamMemo enters Status.Error"
                Expect.isFalse feed.Latest.Token.IsCancellationRequested "StreamMemo keeps open stream running"
                Expect.equal feed.Starts 1 "StreamMemo does not restart"

                // Items arriving while Phase 1 is errored fold into the open stream
                feed.Push 7
                Expect.equal (failedWith sNode.TryValue).Message "throwing keyComparer" "Phase-1 Error masks stream while failing"

                armThrow.Value <- false
                sLeft.Value <- 11
                Expect.equal sNode.Value 12 "StreamMemo recovers with folded 5 + 7 = 12"
                Expect.equal sNode.Status Status.None "StreamMemo Status clears to None"
                Expect.equal feed.Starts 1 "still only 1 stream start"

                // 4. CombinatorMemos.createReactiveBind over throwingCmp preserves inner memo & Phase-2 scope
                let bLeft = createSignal 1
                let bRight = createSignal 100

                let bWrapped =
                    MergedReactive<int, int>(bLeft, bRight)
                    |> withComparer throwingCmp

                let innerVal = createSignal 42
                let bRuns = ref 0
                let bCleanups = ResizeArray<int>()

                let bNode =
                    CombinatorMemos.createReactiveBind (
                        g,
                        (bWrapped :> IReactive<struct (int * int)>).ReadTracked,
                        (fun (struct (k, _)) ->
                            bRuns.Value <- bRuns.Value + 1
                            onCleanup (fun () -> bCleanups.Add k)
                            createMemo (fun _ -> innerVal.Value)),
                        ValueSome bWrapped.Comparer,
                        ValueNone
                    )

                Expect.equal bNode.Value 42 "createReactiveBind initial ready"
                Expect.equal bRuns.Value 1 "binder ran once"

                armThrow.Value <- true
                bLeft.Value <- 2
                let bEx = failedWith bNode.TryValue
                Expect.equal bEx.Message "throwing keyComparer" "createReactiveBind surfaces Phase-1 throwingCmp exception"
                Expect.equal bNode.Status Status.Error "createReactiveBind enters Status.Error"
                Expect.equal bNode.Peek 42 "createReactiveBind preserves Peek"
                Expect.isEmpty bCleanups "createReactiveBind preserves Phase-2 scope"

                armThrow.Value <- false
                bLeft.Value <- 11
                Expect.equal bNode.Value 42 "createReactiveBind recovers on valid equal settle"
                Expect.equal bRuns.Value 1 "binder did not re-run"
                Expect.isEmpty bCleanups "scope still intact"
                innerVal.Value <- 43
                Expect.equal bNode.Value 43 "createReactiveBind still subscribed to innerMemo after recovery"
            }

            test "createReactiveBind edge cases, TimedAdapter errors, and KeyedCollection Remove/Clear" {
                use g = new Graph ()
                use otherG = new Graph ()
                use _ = g.Activate ()

                // 1. Default ValueNone comparers, Pending/Failed Phase 1, null/cross-graph/pending/pure-violating binder, Pending/Error innerMemo
                let p1Pending = AsyncSource<int> g
                let p1Mode = createSignal 0
                let p1Key = createSignal 1
                let binderMode = createSignal 0
                let binderPending = AsyncSource<int> g
                let innerSrc = AsyncSource<int> g
                let otherMemo = Make.Memo (otherG, (fun _ -> 777))

                let rb =
                    CombinatorMemos.createReactiveBind (
                        g,
                        (fun () ->
                            match p1Mode.Value with
                            | 1 -> p1Pending.Value
                            | 2 -> failwith "p1-fail"
                            | 3 ->
                                createEffect ignore
                                0
                            | _ -> p1Key.Value),
                        (fun k ->
                            match binderMode.Value with
                            | 1 -> failwith "binder-boom"
                            | 2 ->
                                binderPending.Value |> ignore
                                createMemo (fun _ -> k)
                            | 3 -> Unchecked.defaultof<Memo<int>>
                            | 4 -> otherMemo
                            | 5 -> createMemo (fun _ -> innerSrc.Value)
                            | _ -> createMemo (fun _ -> k * 10)),
                        ValueNone,
                        ValueNone
                    )

                // Phase 1 Pending -> Ready
                p1Mode.Value <- 1
                Expect.equal rb.TryValue Pending "Phase 1 Pending suspends createReactiveBind"
                p1Pending.Settle 5
                Expect.equal rb.Value 50 "Phase 1 settles to Ready"

                // Phase 1 Error -> recover
                p1Mode.Value <- 2
                Expect.equal (failedWith rb.TryValue).Message "p1-fail" "Phase 1 failure surfaces"
                p1Mode.Value <- 3
                let pureEx = failedWith rb.TryValue
                Expect.isTrue (isInvalidOperation pureEx) "Phase 1 pure violation fails"

                p1Mode.Value <- 0
                Expect.equal rb.Value 10 "Phase 1 recovers to p1Key=1"

                // Binder throwing latches across equal key
                binderMode.Value <- 1
                p1Key.Value <- 2
                Expect.equal (failedWith rb.TryValue).Message "binder-boom" "binder throw fails"

                // Binder synchronous NotReadyException wraps into InvalidOperationException
                binderMode.Value <- 2
                p1Key.Value <- 3
                let wrapEx = failedWith rb.TryValue
                Expect.isTrue (isInvalidOperation wrapEx) "binder NotReadyException wraps to InvalidOperationException"
                Expect.isTrue (isNotReadyException wrapEx.InnerException) "InnerException is NotReadyException"

                // Binder returning null Memo
                binderMode.Value <- 3
                p1Key.Value <- 4
                let nullEx = failedWith rb.TryValue
                Expect.isTrue (isNullReference nullEx) "null Memo fails with NullReferenceException"

                // Binder returning cross-graph Memo
                binderMode.Value <- 4
                p1Key.Value <- 5
                let crossEx = failedWith rb.TryValue
                Expect.isTrue (isInvalidOperation crossEx) "cross-graph Memo fails with InvalidOperationException"

                // Inner Memo Pending -> Error -> Ready
                binderMode.Value <- 5
                p1Key.Value <- 6
                Expect.equal rb.TryValue Pending "inner Memo Pending propagates"
                innerSrc.Fail (InvalidOperationException "inner-fail")
                Expect.equal (failedWith rb.TryValue).Message "inner-fail" "inner Memo Error propagates"
                binderMode.Value <- 0
                p1Key.Value <- 7
                Expect.equal rb.Value 70 "createReactiveBind recovers to normal inner Memo"

                // 2. TimedAdapter error & recovery
                let clock = ManualClock ()
                let tIn = createSignal 1
                let tThrow = createSignal false

                let timedErr =
                    debounceWith
                        { Clock = clock; Comparer = None }
                        (TimeSpan.FromMilliseconds 50.)
                        (fun () ->
                            if tThrow.Value then
                                failwith "timed-boom"

                            tIn.Value)
                        g

                let mappedErr =
                    CombinatorMemos.createSourceMap (fun x -> x + 1) (TimedAdapter<int> timedErr)

                Expect.equal mappedErr.Value 2 "TimedAdapter initial ready"
                tThrow.Value <- true
                Expect.equal (failedWith mappedErr.TryValue).Message "timed-boom" "TimedAdapter surfaces failure"
                Expect.isTrue (obj.ReferenceEquals (mappedErr.ErrorOrigin, timedErr :> INode)) "TimedAdapter ErrorOrigin is Timed"
                tThrow.Value <- false
                tIn.Value <- 9
                clock.AdvanceTo 50.
                Expect.equal mappedErr.Value 10 "TimedAdapter recovers on next admission"

                // 3. KeyedCollection.at KeyNotFoundException + Remove/Clear
                let kc = createKeyedCollection fst
                kc.AddOrUpdate (1, "one")
                kc.AddOrUpdate (2, "two")
                let kcAt2 = KeyedCollection.at 2 kc :> IReactive<int * string>
                let kcTry2 = KeyedCollection.tryAt 2 kc :> IReactive<(int * string) option>
                let kcKeys = KeyedCollection.keys kc :> IReactive<int[]>

                Expect.equal (kcAt2.ReadTracked ()) (2, "two") "KeyedCollection.at present"
                Expect.isTrue (kc.Remove 2) "remove key 2"
                Expect.throwsT<KeyNotFoundException> (fun () -> kcAt2.ReadTracked () |> ignore) "KeyedCollection.at raises after Remove"
                Expect.equal (kcTry2.ReadTracked ()) None "KeyedCollection.tryAt None after Remove"
                Expect.sequenceEqual (kcKeys.ReadTracked ()) [ 1 ] "KeyedCollection.keys after Remove"

                kc.Clear ()
                Expect.isEmpty (kcKeys.ReadTracked ()) "KeyedCollection.keys empty after Clear"
            }
        ]
