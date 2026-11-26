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

[<AllowNullLiteral>]
type private BoxPayload(id: int, label: string) =
    member _.Id = id
    member _.Label = label

    override _.Equals(other: obj) =
        match other with
        | :? BoxPayload as that -> id = that.Id
        | _ -> false

    override _.GetHashCode() = id

type private TrackedDisposable(tag: string, log: ResizeArray<string>) =
    member _.Tag = tag

    interface IDisposable with
        member _.Dispose() =
            log.Add ("disp:" + tag)

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
    match ex with
    | NotReadyException _ -> true
    | _ -> false

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
                let timedIn = Signal (g, 3)

                let timedNode =
                    debounceWith { Clock = clock; Comparer = None } (TimeSpan.FromMilliseconds 100.) (fun () -> timedIn.Value) g

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

            test
                "memo static 1-source dispatch across all 8 node types and collection descriptors without Activate, closed Phase 2, and ColdPhase laziness" {
                use g = new Graph ()
                let mutable hasAmbient = true

                try
                    createSignal 0 |> ignore
                with _ ->
                    hasAmbient <- false

                Expect.isFalse hasAmbient "precondition: graph is not activated"

                let sig1 = Signal (g, 10)
                let stray = Signal (g, 1000)
                let mem1 = Make.Memo (g, (fun _ -> sig1.Value * 2))
                let edit1 = g.Run (fun () -> createEditable (fun _ -> sig1.Value + 1))
                let asrc1 = AsyncSource<int> g
                asrc1.Settle 5
                let am1 = Make.AsyncMemo<int>(g, (fun _ _ -> completed (sig1.Value + 2)))
                let feed = ManualStream<int>()
                let sm1 = g.Run (fun () -> createStream (fun _ -> feed.Source))

                let bnd1 =
                    g.Run (fun () -> createBoundary (fun _ -> -1) (fun _ _ -> -2) (fun _ -> sig1.Value + 3))

                let clock = ManualClock ()

                let tmd1 =
                    debounceWith { Clock = clock; Comparer = None } (TimeSpan.FromMilliseconds 50.) (fun () -> sig1.Value + 4) g

                let listSig = Signal (g, [ (1, "a"); (2, "b") ])
                let proj = g.Run (fun () -> createProjection fst snd (fun () -> listSig.Value))

                let lkp =
                    g.Run (fun () -> createLookup (fun m k -> m + k) (fun _ _ -> seq { yield 5 }) (fun () -> sig1.Value))

                let kc = g.Run (fun () -> createKeyedCollection fst)
                kc.AddOrUpdate (1, "k1")

                let closedRuns = ref 0

                let mSig =
                    memo {
                        let! x = sig1
                        closedRuns.Value <- closedRuns.Value + 1
                        return x + stray.Value
                    }

                let mMem = memo { let! x = mem1 in return x + 1 }
                let mEdit = memo { let! x = edit1 in return x + 1 }
                let mAsrc = memo { let! x = asrc1 in return x + 1 }
                let mAm = memo { let! x = am1 in return x + 1 }
                let mSm = memo { let! x = sm1 in return x + 1 }
                let mBnd = memo { let! x = bnd1 in return x + 1 }
                let mTmd = memo { let! x = tmd1 in return x + 1 }
                let mProjAt = memo { let! v = proj |> Projection.at 1 in return v + "!" }

                let mProjTry =
                    memo { let! v = proj |> Projection.tryAt 9 in return Option.defaultValue "miss" v }

                let mProjKeys = memo { let! ks = proj |> Projection.keys in return ks.Length }
                let mLkpAt = memo { let! v = lkp |> Lookup.at 5 in return v * 2 }

                let mLkpTry =
                    memo { let! v = lkp |> Lookup.tryAt 99 in return Option.defaultValue -1 v }

                let mKcAt = memo { let! (_, v) = kc |> KeyedCollection.at 1 in return v }

                let mKcTry =
                    memo { let! opt = kc |> KeyedCollection.tryAt 2 in return Option.isSome opt }

                let mKcKeys = memo { let! ks = kc |> KeyedCollection.keys in return ks.Length }

#if !RANVIER_TRACE
                let chainSig = memo { let! x = mSig in return x + 1 }
                let chainMem = memo { let! x = mMem in return x * 2 }
                let chainEdit = memo { let! x = mEdit in return x * 2 }
                let chainAsrc = memo { let! x = mAsrc in return x * 2 }
                let chainAm = memo { let! x = mAm in return x * 2 }
                let chainBnd = memo { let! x = mBnd in return x * 2 }
                let chainTmd = memo { let! x = mTmd in return x * 2 }

                Expect.equal chainSig.Value 1011 "fused chain over mSig"
                Expect.equal chainMem.Value 42 "fused chain over mMem"
                Expect.equal chainEdit.Value 24 "fused chain over mEdit"
                Expect.equal chainAsrc.Value 12 "fused chain over mAsrc"
                Expect.equal chainAm.Value 26 "fused chain over mAm"
                Expect.equal chainBnd.Value 28 "fused chain over mBnd"
                Expect.equal chainTmd.Value 30 "fused chain over mTmd"

                Expect.equal mSig.ObserverCount 0 "mSig started fusible FusedSignalMap1 and was claimed by chainSig"
                Expect.equal mMem.ObserverCount 0 "mMem started fusible FusedSourceMap1 and was claimed by chainMem"
                Expect.equal mEdit.ObserverCount 0 "mEdit started fusible FusedSourceMap1 over Editable.ValueMemo"
                Expect.equal mAsrc.ObserverCount 0 "mAsrc started fusible FusedSourceMap1 and was claimed by chainAsrc"
                Expect.equal mAm.ObserverCount 0 "mAm started fusible FusedSourceMap1 and was claimed by chainAm"
                Expect.equal mBnd.ObserverCount 0 "mBnd started fusible FusedSourceMap1 and was claimed by chainBnd"
                Expect.equal mTmd.ObserverCount 0 "mTmd started fusible FusedSourceMap1 and was claimed by chainTmd"
#endif

                Expect.equal mSig.Value 1010 "mSig initial"
#if !RANVIER_TRACE
                Expect.equal closedRuns.Value 1 "mSig materialized without re-running closed mapper"
#else
                Expect.equal closedRuns.Value 1 "mSig ran once"
#endif
                stray.Value <- 2000
                Expect.equal mSig.Value 1010 "closed Phase 2 does not subscribe to stray"
                Expect.equal closedRuns.Value 1 "mSig did not re-run on stray change"
                sig1.Value <- 11
                Expect.equal mSig.Value 2011 "mSig wakes on bound sig1 and reads updated stray"
                Expect.equal closedRuns.Value 2 "mSig ran twice"

                Expect.equal mMem.Value 23 "mMem value"
                Expect.equal mEdit.Value 13 "mEdit value"
                Expect.equal mAsrc.Value 6 "mAsrc value"
                Expect.equal mAm.Value 14 "mAm value"
                Expect.equal mSm.TryValue Pending "mSm initially Pending"
                feed.Push 9
                Expect.equal mSm.Value 10 "mSm updated on push"
                Expect.equal mBnd.Value 15 "mBnd value"
                Expect.equal mTmd.Value 15 "mTmd debounced before clock advance"
                clock.AdvanceTo 50.
                Expect.equal mTmd.Value 16 "mTmd value after clock advance"
                Expect.equal mProjAt.Value "a!" "Projection.at"
                Expect.equal mProjTry.Value "miss" "Projection.tryAt"
                Expect.equal mProjKeys.Value 2 "Projection.keys"
                Expect.equal mLkpAt.Value 32 "Lookup.at ((11 + 5) * 2)"
                sig1.Value <- 12
                Expect.equal mLkpAt.Value 34 "Lookup.at tracks diff key update ((12 + 5) * 2)"
                sig1.Value <- 11
                Expect.equal mLkpTry.Value -1 "Lookup.tryAt"
                Expect.equal mKcAt.Value "k1" "KeyedCollection.at"
                Expect.isFalse mKcTry.Value "KeyedCollection.tryAt"
                Expect.equal mKcKeys.Value 1 "KeyedCollection.keys"

                // Unready read inside closed Phase 2 fails immediately instead of hanging Pending
                let pendingSrc = AsyncSource<int> g

                let mBadPhase2 =
                    memo {
                        let! x = sig1
                        return x + pendingSrc.Value
                    }

                let badEx = failedWith mBadPhase2.TryValue
                Expect.isTrue (isInvalidOperation badEx) "unready read in closed Phase 2 fails with InvalidOperationException"
                Expect.isTrue (isNotReadyException badEx.InnerException) "wrapped InnerException is NotReadyException"

                // ColdPhase 0-bind memo never invokes body at construction time
                let coldRuns = ref 0

                let mCold =
                    g.Run (fun () ->
                        memo {
                            coldRuns.Value <- coldRuns.Value + 1
                            return sig1.Value * 3
                        })

                Expect.equal coldRuns.Value 0 "ColdPhase memo is lazy at construction"
                Expect.equal mCold.Value 33 "ColdPhase memo runs on first read"
                Expect.equal coldRuns.Value 1 "ColdPhase memo ran once"
                sig1.Value <- 12
                Expect.equal mCold.Value 36 "ColdPhase memo tracks reactive reads inside body"
            }

            test "Review Focus #1: 3-stage and 4-stage memo chains fuse and mid-chain .Value or .Dispose preserves values and ErrorOrigin" {
                use g = new Graph ()
                let s = Signal (g, 2)
                let stage2Calls = ref 0

                let m1 = memo { let! x = s in return x + 1 }

                let m2 =
                    memo {
                        let! x = m1
                        stage2Calls.Value <- stage2Calls.Value + 1
                        return x * 10
                    }

                let m3 = memo { let! x = m2 in return x + 5 }
                let m4 = memo { let! x = m3 in return x * 2 }

                Expect.equal m4.Value 70 "4-stage fused value ((2+1)*10 + 5)*2 = 70"
                Expect.equal stage2Calls.Value 1 "stage 2 mapper evaluated once for initial pull"
#if !RANVIER_TRACE
                Expect.equal m1.ObserverCount 0 "m1 is claimed inside 4-stage chain before mid-chain read"
                Expect.equal m2.ObserverCount 0 "m2 is claimed inside 4-stage chain before mid-chain read"
                Expect.equal m3.ObserverCount 0 "m3 is claimed inside 4-stage chain before mid-chain read"
                Expect.equal s.ObserverCount 1 "4-stage fused chain subscribes once at root signal"
#endif

                // Mid-chain observation materializes/de-fuses m2 cleanly without extra stage2 call or losing value
                Expect.equal m2.Value 30 "mid-chain m2.Value materializes cleanly"
                Expect.equal stage2Calls.Value 1 "mid-chain m2.Value runs stage 2 mapper 0 extra times"
                Expect.equal m2.Runs 1 "m2 materialized with Runs = 1"
                Expect.equal m4.Value 70 "m4 preserves cached value"
                s.Value <- 3
                Expect.equal m4.Value 90 "4-stage updates after mid-chain de-fusion ((3+1)*10 + 5)*2 = 90"
                Expect.equal m2.Value 40 "m2 shares updated value without duplicate stage2 evaluation"
                Expect.equal stage2Calls.Value 2 "stage 2 mapper evaluated once for s <- 3"

                // Mid-chain Dispose freezes downstream value and preserves ErrorOrigin
                let sVal = Signal (g, 4)
                let f1 = memo { let! x = sVal in return x + 1 }
                let f2 = memo { let! x = f1 in return x * 10 }
                let f3 = memo { let! x = f2 in return x + 7 }
                Expect.equal f3.Value 57 "warm 3-stage chain before mid-chain Dispose"
                f2.Dispose ()
                sVal.Value <- 99
                Expect.equal f3.Value 57 "f3 freezes at cached 57 when f2 disposes"

                // ErrorOrigin preserved across mid-chain observation & Dispose on stage-2 throw
                let errSig = Signal (g, 1)
                let stageErr = InvalidOperationException "ce-stage2-err"
                let e1 = memo { let! x = errSig in return x + 1 }

                let e2 =
                    memo {
                        let! x = e1

                        return if x = 2 then raise stageErr else x * 100
                    }

                let e3 = memo { let! x = e2 in return x + 1 }
                let e3Ex = failedWith e3.TryValue
                Expect.isTrue (obj.ReferenceEquals (e3Ex, stageErr)) "e3 surfaces stageErr"
                Expect.isTrue (obj.ReferenceEquals (e3.ErrorOrigin, e2 :> INode)) "e3 attributes ErrorOrigin to stage 2 (e2)"
                e2.Dispose ()
                let e3Frozen = failedWith e3.TryValue
                Expect.isTrue (obj.ReferenceEquals (e3Frozen, stageErr)) "e3 preserves cached failure after e2.Dispose()"
                Expect.isTrue (obj.ReferenceEquals (e3.ErrorOrigin, e2 :> INode)) "e3 preserves ErrorOrigin = e2 after e2.Dispose()"
            }

            test "memo and!, cross-graph validation, withComparer, owning/comparer/option headers, return! (MemoTail), and memo.previous" {
                use g = new Graph ()
                use otherG = new Graph ()

                let s1 = Signal (g, 1)
                let s2 = Signal (g, 10)
                let sOther = Signal (otherG, 99)
                let mA = Make.Memo (g, (fun _ -> s1.Value * 2))
                let mB = Make.Memo (g, (fun _ -> s2.Value * 3))
                let editA = g.Run (fun () -> createEditable (fun _ -> s1.Value + 5))
                let editB = g.Run (fun () -> createEditable (fun _ -> s2.Value + 5))

                let mSigs =
                    memo {
                        let! a = s1 in
                        and! b = s2
                        return a + b
                    }

                let mMems =
                    memo {
                        let! a = mA in
                        and! b = mB
                        return a + b
                    }

                let mEdits =
                    memo {
                        let! a = editA in
                        and! b = editB
                        return a + b
                    }

                Expect.equal mSigs.Value 11 "2-way signals"
                Expect.equal mMems.Value 32 "2-way memos"
                Expect.equal mEdits.Value 21 "2-way editables"

                // 3-way and 4-way heterogeneous and! without Activate() + cross-graph check
                let listSig = Signal (g, [ (1, 100); (2, 200) ])
                let proj = g.Run (fun () -> createProjection fst snd (fun () -> listSig.Value))

                let lkp =
                    g.Run (fun () -> createLookup (fun v k -> v + k) (fun _ _ -> Seq.empty) (fun () -> s1.Value))

                let m4Way =
                    memo {
                        let! a = s1
                        and! b = mB
                        and! c = proj |> Projection.tryAt 1
                        and! d = lkp |> Lookup.at 3
                        return a + b + (Option.defaultValue 0 c) + d
                    }

                Expect.equal m4Way.Value 135 "4-way heterogeneous and!"

                let exCross =
                    Expect.throwsC
                        (fun () ->
                            memo {
                                let! a = s1 in
                                and! b = sOther
                                return a + b
                            }
                            |> ignore)
                        id

                Expect.isTrue (isInvalidOperation exCross) "cross-graph and! throws InvalidOperationException"

                // owning, comparer, and both orders (owning; comparer and comparer; owning) + withComparer
                let mod10 = comparer (fun (a: int) b -> a % 10 = b % 10)
                let evenOut = comparer (fun (a: int) b -> a % 2 = b % 2)
                let ownLogs1 = ResizeArray<string>()
                let ownLogs2 = ResizeArray<string>()
                let sigOwn = Signal (g, 1)

                let mOwnCmp1 =
                    memo {
                        owning
                        comparer evenOut
                        let! k = sigOwn |> withComparer mod10
                        onCleanup (fun () -> ownLogs1.Add $"clean:{k}")
                        let child = createMemo (fun _ -> k * 10 + (k % 2))
                        return child.Value
                    }

                let mOwnCmp2 =
                    memo {
                        comparer evenOut
                        owning
                        let! k = sigOwn
                        onCleanup (fun () -> ownLogs2.Add $"c2:{k}")
                        let child = createMemo (fun _ -> k * 2)
                        return child.Value + 1
                    }

                let mCmpOnly =
                    memo {
                        comparer evenOut
                        let! a = sigOwn
                        and! b = s2
                        return a + b
                    }

                let downstream1 = ResizeArray<int>()
                let downstream2 = ResizeArray<int>()
                g.Run (fun () -> createEffect (fun () -> downstream1.Add mOwnCmp1.Value))
                g.Run (fun () -> createEffect (fun () -> downstream2.Add mCmpOnly.Value))
                Expect.equal mOwnCmp1.Value 11 "owning+comparer initial"
                Expect.equal mOwnCmp2.Value 3 "comparer+owning initial"
                Expect.sequenceEqual downstream1 [ 11 ] "downstream1 saw 11"
                Expect.sequenceEqual downstream2 [ 11 ] "downstream2 saw 11"

                // Equal Phase-1 key under mod10 preserves Phase-2 scope and skips recompute on mOwnCmp1;
                // odd->odd output keeps mCmpOnly output cutoff intact while mOwnCmp2 cleans old child scope on unequal key
                sigOwn.Value <- 11
                Expect.equal mOwnCmp1.Value 11 "withComparer keeps scope & value on 1 -> 11"
                Expect.isEmpty ownLogs1 "mOwnCmp1 skipped Phase 2 cleanup on equal key 1 -> 11"
                Expect.sequenceEqual downstream1 [ 11 ] "downstream1 did not wake"
                Expect.sequenceEqual downstream2 [ 11 ] "mCmpOnly output parity cutoff skipped downstream2"
                Expect.equal mOwnCmp2.Value 23 "mOwnCmp2 recomputed to 23"
                Expect.sequenceEqual ownLogs2 [ "c2:1" ] "mOwnCmp2 disposed old owning child scope on 1 -> 11"

                sigOwn.Value <- 2
                Expect.equal mOwnCmp1.Value 20 "unequal mod10 recomputes mOwnCmp1 (even output 20)"
                Expect.sequenceEqual ownLogs1 [ "clean:1" ] "mOwnCmp1 discharged old Phase-2 scope on 11 -> 2"
                Expect.sequenceEqual downstream1 [ 11; 20 ] "downstream1 woke on parity change 11 -> 20"

                // Verify output cutoff cleanly via signal transition odd(11) -> odd(13) -> even(14)
                let sOut = Signal (g, 11)

                let mPar =
                    memo {
                        comparer evenOut
                        let! k = sOut
                        return k
                    }

                let seenPar = ResizeArray<int>()
                g.Run (fun () -> createEffect (fun () -> seenPar.Add mPar.Value))
                sOut.Value <- 13
                sOut.Value <- 14
                Expect.sequenceEqual seenPar [ 11; 14 ] "comparer suppresses downstream on equal parity (11 -> 13) and wakes on 14"

                // option header deduplicates Some reference identity
                use optGraph = new Graph (GraphOptions.Default.WithEquality (StructuralPolicy ()))
                let sOpt = Signal (optGraph, (1, "v1"))

                let mOpt =
                    memo {
                        option
                        let! (id, lbl) = sOpt
                        return if id > 0 then Some (BoxPayload (id, lbl)) else None
                    }

                let firstSome = mOpt.Value
                sOpt.Value <- (1, "v2")
                let secondSome = mOpt.Value

                match firstSome, secondSome with
                | Some p1, Some p2 ->
                    Expect.isTrue (obj.ReferenceEquals (p1, p2)) "option header preserves Some payload reference when inner equals"
                    Expect.equal p2.Label "v1" "kept first payload label"
                | _ -> failtest "expected Some"

                sOpt.Value <- (0, "none")
                Expect.equal mOpt.Value None "option transitions to None"
                sOpt.Value <- (1, "v3")

                match firstSome, mOpt.Value with
                | Some p1, Some p3 -> Expect.isFalse (obj.ReferenceEquals (p1, p3)) "option issues fresh Some after None"
                | _ -> failtest "expected Some after None"

                // return! (MemoTail): Signal, Memo, and!, withComparer, match!, and sequential let!
                let keySig = Signal (g, 1)
                let inner1 = Signal (g, 100)
                let inner2 = Signal (g, 200)
                let mInner1 = Make.Memo (g, (fun _ -> inner1.Value))
                let mInner2 = Make.Memo (g, (fun _ -> inner2.Value))
                let bindCalls = ref 0
                let bindCleanups = ResizeArray<int>()

                let mBindSig =
                    memo {
                        let! k = keySig
                        bindCalls.Value <- bindCalls.Value + 1
                        if k = 1 then return! mInner1 else return! mInner2
                    }

                Expect.equal mBindSig.Value 100 "return! Signal initial"
                Expect.equal bindCalls.Value 1 "Signal.bind binder ran once"
                inner1.Value <- 101
                Expect.equal mBindSig.Value 101 "inner Memo update propagates without outer binder re-run"
                Expect.equal bindCalls.Value 1 "binder still ran only once"

                let mBindMatch =
                    memo {
                        owning
                        comparer evenOut
                        let! k = keySig |> withComparer mod10
                        onCleanup (fun () -> bindCleanups.Add k)

                        match k % 2 with
                        | 0 -> return! mInner2
                        | _ -> return! mInner1
                    }

                Expect.equal mBindMatch.Value 101 "match! return! initial"
                keySig.Value <- 11
                Expect.equal mBindMatch.Value 101 "withComparer keeps match! binder on 1 -> 11"
                Expect.isEmpty bindCleanups "no binder scope cleanup on 1 -> 11"
                keySig.Value <- 2
                Expect.equal mBindMatch.Value 200 "match! switches inner memo on 2"
                Expect.sequenceEqual bindCleanups [ 1 ] "old binder scope discharged on key switch"

                // Sequential let! before return!
                let gateSig = Signal (g, true)
                let innerKeySig = Signal (g, 1)
                let seqBindCleanups = ResizeArray<string>()

                let mSeqBind =
                    memo {
                        let! active = gateSig

                        if not active then
                            return! mInner1
                        else
                            onCleanup (fun () -> seqBindCleanups.Add "outer-branch")
                            let! k = innerKeySig
                            if k = 1 then return! mInner2 else return! mInner1
                    }

                Expect.equal mSeqBind.Value 200 "sequential let! before return! active branch"
                innerKeySig.Value <- 2
                Expect.equal mSeqBind.Value 101 "inner return! re-binds cleanly"
                Expect.isEmpty seqBindCleanups "outer branch kept while gate true"
                gateSig.Value <- false
                Expect.equal mSeqBind.Value 101 "mSeqBind switches to mInner1 when gate becomes false"
                Expect.sequenceEqual seqBindCleanups [ "outer-branch" ] "switching outer branch disposes sequential child SplitBind"

                // memo.previous and memo.previousOr (before reactive let!, after reactive let!, 0-bind, inert outside)
                let inertPrev = memo.previous
                let inertPrevOr = memo.previousOr 7
                ignore (inertPrev, inertPrevOr)

                let prevSig = Signal (g, 5)

                let mPrevBefore =
                    memo {
                        let! prev = memo.previous
                        let! x = prevSig

                        return
                            match prev with
                            | ValueNone -> x
                            | ValueSome p -> p + x
                    }

                let mPrevAfter =
                    memo {
                        let! x = prevSig
                        let! prev = memo.previousOr 100
                        return x + prev
                    }

                let mPrevCold =
                    g.Run (fun () ->
                        memo {
                            let! p = memo.previousOr 1
                            return p + prevSig.Value
                        })

                Expect.equal mPrevBefore.Value 5 "memo.previous before let! initial ValueNone"
                Expect.equal mPrevAfter.Value 105 "memo.previousOr after let! initial seed 100 + 5"
                Expect.equal mPrevCold.Value 6 "0-bind memo.previousOr initial"
                prevSig.Value <- 10
                Expect.equal mPrevBefore.Value 15 "memo.previous sees previous 5 -> 5 + 10"
                Expect.equal mPrevAfter.Value 115 "memo.previousOr sees previous 105 -> 105 + 10"
                Expect.equal mPrevCold.Value 16 "0-bind memo.previousOr sees previous 6 -> 6 + 10"
            }

            test
                "Review Focus #3 & #5: memo dynamic match!, nested memo in owning, evalDepth unwind on NotReadyException/exn, try/while/for/use disposal" {
                use g = new Graph ()
                let s1 = Signal (g, 1)
                let stray = Signal (g, 999)
                let listSig = Signal (g, [ (1, "Alice") ])
                let proj = g.Run (fun () -> createProjection fst snd (fun () -> listSig.Value))
                let ownerReads = ref 0

                let owners =
                    g.Run (fun () ->
                        createLookup
                            (fun n _ ->
                                ownerReads.Value <- ownerReads.Value + 1
                                n * 10)
                            (fun _ _ -> Seq.empty)
                            (fun () -> s1.Value))

                let matchMemo =
                    memo {
                        match! proj |> Projection.tryAt 2 with
                        | None -> return "none"
                        | Some name ->
                            let! o = owners |> Lookup.at name
                            return $"{name}:{o}:{stray.Value}"
                    }

                Expect.equal matchMemo.Value "none" "dynamic match! initially misses"
                Expect.equal ownerReads.Value 0 "right arm Lookup.at not queried while None"
                listSig.Value <- [ (1, "Alice"); (2, "Bob") ]
                Expect.equal matchMemo.Value "Bob:10:999" "match! switches to Some arm"
                Expect.equal ownerReads.Value 1 "Lookup.at ran once"
                stray.Value <- 555
                Expect.equal matchMemo.Value "Bob:10:999" "terminal BindReturn inside match! arm closes stray tracking"

                // Nested memo constructed inside owning Phase 2 (SuspendForNestedRun)
                let childCleanups = ResizeArray<int>()

                let outerOwn =
                    memo {
                        owning
                        let! x = s1

                        let child =
                            memo {
                                onCleanup (fun () -> childCleanups.Add x)
                                let! y = stray
                                return y + x
                            }

                        return child.Value
                    }

                Expect.equal outerOwn.Value 556 "nested child memo inside owning Phase 2"
                s1.Value <- 2
                Expect.equal outerOwn.Value 557 "outer owning recomputes child"
                Expect.sequenceEqual childCleanups [ 1 ] "old nested child memo disposed with Phase-2 scope"

                // Review Focus #3: NotReadyException and exn inside dynamic 2nd let! unwind evalDepth cleanly
                let pendSrc = AsyncSource<int> g
                let errSrc = Make.Memo (g, (fun _ -> failwith "dyn-2nd-boom"))
                let dynMode = Signal (g, 0)

                let mFailDyn =
                    memo {
                        let! a = dynMode

                        let! b =
                            (if a = 1 then (pendSrc :> IReactive<int>)
                             elif a = 2 then (errSrc :> IReactive<int>)
                             else (s1 :> IReactive<int>))

                        return a + b
                    }

                Expect.equal mFailDyn.Value 2 "dynamic sequential memo initial ready"
                dynMode.Value <- 1
                Expect.equal mFailDyn.TryValue Pending "2nd let! Pending suspends dynamic memo"

                // Immediately construct a top-level static 1-source memo on the same thread!
                let topAfterPend = memo { let! x = s1 in return x + 1 }
                let claimAfterPend = memo { let! y = topAfterPend in return y * 10 }
                Expect.equal claimAfterPend.Value 30 "claimAfterPend computes cleanly"
#if !RANVIER_TRACE
                Expect.equal topAfterPend.ObserverCount 0 "evalDepth unwound cleanly after NotReadyException -> topAfterPend fused and was claimed"
#endif
                Expect.equal topAfterPend.Value 3 "topAfterPend computes properly"

                dynMode.Value <- 2
                let dynErr = failedWith mFailDyn.TryValue
                Expect.equal dynErr.Message "dyn-2nd-boom" "2nd let! exception surfaces"

                let topAfterExn = memo { let! x = s1 in return x + 2 }
                let claimAfterExn = memo { let! y = topAfterExn in return y * 10 }
                Expect.equal claimAfterExn.Value 40 "claimAfterExn computes cleanly"
#if !RANVIER_TRACE
                Expect.equal topAfterExn.ObserverCount 0 "evalDepth unwound cleanly after exn -> topAfterExn fused and was claimed"
#endif
                Expect.equal topAfterExn.Value 4 "topAfterExn computes properly"

                // Review Focus #5 & try/with/finally/while/for inside memo { owning ... }
                let useLog = ResizeArray<string>()
                let throwUse = Signal (g, false)
                let resId = ref 0

                let mUse =
                    g.Run (fun () ->
                        memo {
                            owning
                            resId.Value <- resId.Value + 1
                            let tag = $"m{resId.Value}"
                            use _d = new TrackedDisposable (tag, useLog)
                            onCleanup (fun () -> useLog.Add ("scope:" + tag))
                            let! shouldFail = throwUse

                            if shouldFail then
                                failwith "use-boom"

                            return s1.Value
                        })

                Expect.equal mUse.Value 2 "mUse initial"
                Expect.sequenceEqual useLog [ "disp:m1" ] "use disposes deterministically when step returns, before scope discharge"
                throwUse.Value <- true
                Expect.equal (failedWith mUse.TryValue).Message "use-boom" "mUse throws cleanly"

                Expect.sequenceEqual
                    useLog
                    [ "disp:m1"; "scope:m1"; "disp:m2" ]
                    "old scope discharges on re-run, and use disposes deterministically on exception unwind"

                // while / for / try-with / try-finally inside memo
                let mLoops =
                    g.Run (fun () ->
                        memo {
                            let mutable acc = 0

                            for i in [ 1; 2; 3 ] do
                                acc <- acc + i

                            let mutable k = 0

                            while k < 2 do
                                k <- k + 1
                                acc <- acc + s1.Value

                            let finRan = ref false

                            try
                                try
                                    return acc + (if finRan.Value then 100 else 0)
                                finally
                                    finRan.Value <- true
                            with _ ->
                                return -1
                        })

                Expect.equal mLoops.Value 10 "for + while + try/finally + try/with inside memo (1+2+3 + 2*2 = 10)"
            }

            test
                "effect builder: node/unit, static EffectOn cutoff + withComparer, dynamic Bind & match!, ColdPhase, and Review Focus #5 use disposal" {
                use g = new Graph ()
                let mod10 = comparer (fun (a: int) b -> a % 10 = b % 10)
                let keySig = Signal (g, 1)
                let straySig = Signal (g, 100)
                let acted = ResizeArray<int>()
                let effCleanups = ResizeArray<string>()
                let useLog = ResizeArray<string>()

                // Static EffectOn with `node` + `withComparer` + `use` without Activate()
                let effNode =
                    effect {
                        node
                        let! k = keySig |> withComparer mod10
                        use _d = new TrackedDisposable ($"on{k}", useLog)
                        onCleanup (fun () -> effCleanups.Add $"scope:{k}")
                        acted.Add (k + straySig.Value)
                    }

                Expect.equal effNode.Status Status.None "effect { node ... } exposes INode.Status"
                Expect.sequenceEqual acted [ 101 ] "effect acted initially"
                Expect.sequenceEqual useLog [ "disp:on1" ] "Review Focus #5: use disposes deterministically after EffectOn action"
                Expect.isEmpty effCleanups "scope kept alive after act"

                // Changing straySig in closed Phase 2 or changing keySig to equal key under mod10 skips act
                straySig.Value <- 200
                keySig.Value <- 11
                Expect.sequenceEqual acted [ 101 ] "equal key under mod10 and stray mutation skip EffectOn act"
                Expect.isEmpty effCleanups "scope still preserved on equal key"

                keySig.Value <- 2
                Expect.sequenceEqual acted [ 101; 202 ] "unequal key re-acts with fresh stray read"
                Expect.sequenceEqual effCleanups [ "scope:1" ] "old scope discharged before new act"
                Expect.sequenceEqual useLog [ "disp:on1"; "disp:on2" ] "use disposed deterministically on second act"

                // and! EffectOn cutoff (sum equality comparer)
                let sA = Signal (g, 1)
                let sB = Signal (g, 2)
                let andActed = ResizeArray<int>()
                let sumCmp = comparer (fun (struct (a1, b1)) (struct (a2, b2)) -> a1 + b1 = a2 + b2)

                effect {
                    let! (struct (a, b)) =
                        effect.MergeSources (sA, sB)
                        |> withComparer sumCmp

                    andActed.Add (a + b)
                }

                // Also idiomatic `and!` syntax with `let!`
                let andActedPlain = ResizeArray<int>()

                effect {
                    let! a = sA
                    and! b = sB
                    andActedPlain.Add (a * 10 + b)
                }

                Expect.sequenceEqual andActed [ 3 ] "and! + withComparer EffectOn initial"
                Expect.sequenceEqual andActedPlain [ 12 ] "and! EffectOn initial"

                g.Batch (fun () ->
                    sA.Value <- 2
                    sB.Value <- 1)

                Expect.sequenceEqual andActed [ 3 ] "and! + withComparer cuts off when key sums equal (1+2 = 2+1)"
                Expect.sequenceEqual andActedPlain [ 12; 21 ] "plain and! acts on unequal (1,2) -> (2,1)"

                // Dynamic sequential Bind & match! mixing Signal<int>, Memo<string>, () arm, closed act, and use unwind
                let flagSig = Signal (g, false)
                let numSig = Signal (g, 10)
                let msgMemo = Make.Memo (g, (fun _ -> $"n={numSig.Value}"))
                let dynLog = ResizeArray<string>()
                let dynUseLog = ResizeArray<string>()
                let mutable runNo = 0

                let dynEffNode =
                    g.Run (fun () ->
                        effect {
                            node

                            use _d =
                                new TrackedDisposable (
                                    (runNo <- runNo + 1
                                     $"dyn{runNo}"),
                                    dynUseLog
                                )

                            match! flagSig with
                            | false -> dynLog.Add ("off:" + straySig.Value.ToString ())
                            | true ->
                                let! s = msgMemo

                                if numSig.Peek < 0 then
                                    failwith "dyn-eff-boom"

                                dynLog.Add (s + ":" + straySig.Value.ToString ())
                        })

                // Also verify BoundPhase effect without Activate() where `let!`/`match!` precedes `use`
                let boundUseLog = ResizeArray<string>()

                let _boundEffNoActivate =
                    effect {
                        node

                        match! flagSig with
                        | true ->
                            use _d = new TrackedDisposable ("bound-true", boundUseLog)
                            ()
                        | false ->
                            use _d = new TrackedDisposable ("bound-false", boundUseLog)
                            ()
                    }

                Expect.sequenceEqual boundUseLog [ "disp:bound-false" ] "BoundPhase effect resolves graph without Activate and disposes use"

                Expect.sequenceEqual dynLog [ "off:200" ] "dynamic match! initial () arm"
                Expect.sequenceEqual dynUseLog [ "disp:dyn1" ] "use disposed on normal dynamic effect exit"
                straySig.Value <- 300
                Expect.sequenceEqual dynLog [ "off:200" ] "straySig inside arm did not subscribe"

                // Updating numSig while flagSig=false does not wake effect (msgMemo not bound yet)
                numSig.Value <- 20
                Expect.sequenceEqual dynLog [ "off:200" ] "msgMemo not tracked while flagSig=false"

                flagSig.Value <- true
                Expect.sequenceEqual dynLog [ "off:200"; "n=20:300" ] "flagSig=true binds msgMemo"
                Expect.sequenceEqual dynUseLog [ "disp:dyn1"; "disp:dyn2" ] "use disposed on branch 2"
                Expect.sequenceEqual boundUseLog [ "disp:bound-false"; "disp:bound-true" ] "BoundPhase effect switches branch and disposes use"

                numSig.Value <- -1
                Expect.equal dynEffNode.Status Status.Error "exception inside dynamic effect surfaces on node.Status"

                Expect.sequenceEqual
                    dynUseLog
                    [ "disp:dyn1"; "disp:dyn2"; "disp:dyn3" ]
                    "Review Focus #5: use disposes deterministically on exception unwind"

                numSig.Value <- 30
                Expect.equal dynEffNode.Status Status.None "dynamic effect recovers on next update"

                // ColdPhase 0-bind effect on Graph.Current
                let coldLog = ResizeArray<int>()
                g.Run (fun () -> effect { coldLog.Add sA.Value })
                Expect.sequenceEqual coldLog [ 2 ] "0-bind effect ran once on start"
                sA.Value <- 5
                Expect.sequenceEqual coldLog [ 2; 5 ] "0-bind effect tracks dynamic signal reads"
            }

            test
                "editable and draft builders: graph resolution without Activate, closed Phase 2, previous/previousOr, ColdPhase laziness, and edit retention" {
                use g = new Graph ()
                let seedSig = Signal (g, 10)
                let otherSig = Signal (g, 5)
                let straySig = Signal (g, 1000)

                // Inert context tokens outside CE
                ignore (editable.previous, editable.previousOr 0, draft.previous, draft.previousOr 0)

                let eStatic =
                    editable {
                        let! x = seedSig
                        return x + straySig.Value
                    }

                let dStatic =
                    draft {
                        let! a = seedSig
                        and! b = otherSig
                        return a + b + straySig.Value
                    }

                Expect.equal eStatic.Value 1010 "editable static initial"
                Expect.equal dStatic.Value 1015 "draft static and! initial"

                eStatic.Value <- 42
                dStatic.Value <- 77
                Expect.equal eStatic.Value 42 "editable holds local edit"
                Expect.equal dStatic.Value 77 "draft holds local edit"

                // Changing straySig in closed Phase 2 does NOT drop local edit
                straySig.Value <- 2000
                Expect.equal eStatic.Value 42 "editable keeps local edit on closed stray change"
                Expect.equal dStatic.Value 77 "draft keeps local edit on closed stray change"

                // Changing seedSig drops editable edit, while draft retains edit until Reset()
                seedSig.Value <- 20
                Expect.equal eStatic.Value 2020 "editable drops edit when upstream seed changes"
                Expect.equal dStatic.Value 77 "draft retains edit across upstream seed change"
                dStatic.Reset ()
                Expect.equal dStatic.Value 2025 "draft returns to latest seed after Reset()"

                // previous and previousOr inside editable and draft
                let ePrev =
                    editable {
                        let! prev = editable.previous
                        let! x = seedSig

                        return
                            match prev with
                            | ValueNone -> x
                            | ValueSome p -> p + x
                    }

                let dPrev =
                    draft {
                        let! x = seedSig
                        let! prev = draft.previousOr 100
                        return x + prev
                    }

                Expect.equal ePrev.Value 20 "editable.previous initial ValueNone -> 20"
                Expect.equal dPrev.Value 120 "draft.previousOr initial seed 100 + 20"
                seedSig.Value <- 30
                Expect.equal ePrev.Value 50 "editable.previous sees 20 -> 20 + 30"
                Expect.equal dPrev.Value 150 "draft.previousOr sees 120 -> 120 + 30"

                // ColdPhase 0-bind laziness
                let eColdCalls = ref 0
                let dColdCalls = ref 0

                let eCold =
                    g.Run (fun () ->
                        editable {
                            eColdCalls.Value <- eColdCalls.Value + 1
                            return seedSig.Value
                        })

                let dCold =
                    g.Run (fun () ->
                        draft {
                            dColdCalls.Value <- dColdCalls.Value + 1
                            return seedSig.Value
                        })

                Expect.equal eColdCalls.Value 0 "editable ColdPhase is lazy at construction"
                Expect.equal dColdCalls.Value 0 "draft ColdPhase is lazy at construction"
                Expect.equal eCold.Value 30 "editable ColdPhase runs on read"
                Expect.equal dCold.Value 30 "draft ColdPhase runs on read"
                Expect.equal eColdCalls.Value 1 "editable ran once"
                Expect.equal dColdCalls.Value 1 "draft ran once"
            }

            test "boundary builder: pending, failed, both orders, comparer, Graph resolution without Activate, closed Phase 2, and ColdPhase laziness" {
                use g = new Graph ()
                let src = AsyncSource<int> g
                let modeSig = Signal (g, 0)
                let straySig = Signal (g, 10)
                let bndCleanups = ResizeArray<int>()

                // Resolves outer src.Graph via g.Entered when ambient graph is inactive
                let bPendingOnly =
                    boundary {
                        pending (fun last -> ValueOption.defaultValue -1 last)
                        let! v = src
                        return v + straySig.Value
                    }

                let bFailedOnly =
                    boundary {
                        failed (fun ex last ->
                            if ex.Message = "recover" then
                                ValueOption.defaultValue 0 last - 1
                            else
                                raise ex)

                        let! v = src
                        return v
                    }

                // pending; failed; comparer and failed; comparer; pending in different orders
                let mod10 = comparer (fun (a: int) b -> ((a % 10) + 10) % 10 = ((b % 10) + 10) % 10)
                let valSig = Signal (g, 6)
                let bnd1Seen = ResizeArray<int>()

                let bPendingFailedCmp =
                    boundary {
                        pending (fun _ -> -10)
                        failed (fun ex _ -> if ex.Message = "boom" then -20 else raise ex)
                        comparer mod10
                        let! m = modeSig
                        onCleanup (fun () -> bndCleanups.Add m)

                        if m = 1 then
                            return src.Value
                        elif m = 2 then
                            failwith "boom"
                            return 0
                        elif m = 3 then
                            let! x = valSig
                            return x
                        else
                            let! x = src
                            return x + straySig.Value
                    }

                let bFailedCmpPending =
                    boundary {
                        failed (fun _ _ -> -99)
                        comparer mod10
                        pending (fun _ -> -55)
                        let! v = src
                        return v
                    }

                g.Run (fun () -> createEffect (fun () -> bnd1Seen.Add bPendingFailedCmp.Value))

                Expect.equal bPendingOnly.Value -1 "pending handler runs initially"
                Expect.equal bFailedOnly.TryValue Pending "failed-only propagates Pending"
                Expect.equal bPendingFailedCmp.Value -10 "pending+failed+comparer handles Pending"
                Expect.equal bFailedCmpPending.Value -55 "failed+comparer+pending handles Pending"

                src.Settle 5
                Expect.equal bPendingOnly.Value 15 "pending-only settles (5 + 10)"
                Expect.equal bFailedOnly.Value 5 "failed-only settles"
                Expect.equal bPendingFailedCmp.Value 15 "settled Ready"
                Expect.equal bFailedCmpPending.Value 5 "settled Ready 2"

                // Closed Phase-2 straySig read in bPendingOnly does not re-run boundary
                straySig.Value <- 20
                Expect.equal bPendingOnly.Value 15 "closed Phase 2 does not track straySig"

                // Output comparer mod10 suppresses downstream notification when mod10 equal (6 -> 16)
                modeSig.Value <- 3
                Expect.sequenceEqual bnd1Seen [ -10; 15; 6 ] "switched to valSig=6 (uneq mod10 vs 15)"
                valSig.Value <- 16
                Expect.sequenceEqual bnd1Seen [ -10; 15; 6 ] "output comparer mod10 cuts off downstream (6 -> 16)"

                modeSig.Value <- 2
                Expect.equal bPendingFailedCmp.Value -20 "failed handler recovers on boom"

                Expect.sequenceEqual
                    bndCleanups
                    [ 0; 0; 3; 3 ]
                    "boundary discharges scope on every re-run (pending settle, mode switches, and cutoff updates)"

                // ColdPhase 0-bind boundary laziness
                let coldBndCalls = ref 0

                let bCold =
                    g.Run (fun () ->
                        boundary {
                            pending (fun _ -> -1)
                            coldBndCalls.Value <- coldBndCalls.Value + 1
                            return src.Value
                        })

                Expect.equal coldBndCalls.Value 0 "ColdPhase boundary does not execute at construction"
                Expect.equal bCold.Value 5 "ColdPhase boundary executes on read"
                Expect.equal coldBndCalls.Value 1 "ColdPhase boundary ran once"
            }

            test "row builder: projection item + reactive and!/match!, zero extra node allocations, closed return tracking, and factoryRow violation" {
                use g = new Graph ()
                use _ = g.Activate ()
                let itemsSig = createSignal [ (1, 10); (2, 20) ]
                let taxSig = createSignal 2
                let straySig = createSignal 500
                let rowRuns = ref 0

                let baselineBefore = g.NodeCount

                let baseProj =
                    createProjectionWith fst (fun item -> fun () -> item ()) (fun () -> itemsSig.Value)

                ignore (baseProj.Get 1)
                let baseNodesPerRowProj = g.NodeCount - baselineBefore

                let rowBefore = g.NodeCount

                let rowProj =
                    createProjectionWith
                        fst
                        (fun item ->
                            row {
                                let! (_, price) = item
                                and! tax = taxSig
                                rowRuns.Value <- rowRuns.Value + 1
                                return price * tax + straySig.Value
                            })
                        (fun () -> itemsSig.Value)

                Expect.equal (rowProj.Get 1) 520 "row initial on createProjectionWith (10 * 2 + 500 = 520)"
                let rowNodesDelta = g.NodeCount - rowBefore
                Expect.equal rowNodesDelta baseNodesPerRowProj "row builder allocates zero extra nodes compared to hand-written reader"

                let runsAfterKey1 = rowRuns.Value
                // Mutating straySig read inside closed return does NOT wake row
                straySig.Value <- 900
                Expect.equal (rowProj.Get 1) 520 "row closed return does not track straySig"
                Expect.equal rowRuns.Value runsAfterKey1 "row did not recompute on straySig update"

                // Mutating bound taxSig DOES wake row
                taxSig.Value <- 3
                Expect.equal (rowProj.Get 1) 930 "row recomputes on bound taxSig update (10 * 3 + 900 = 930)"

                // Projection.mapWith + createIndexProjectionWith + dynamic match! inside row
                let indexedProj =
                    createIndexProjectionWith
                        (fun item ->
                            row {
                                let! (k, v) = item
                                and! tax = taxSig
                                return $"{k}:{v * tax}"
                            })
                        (fun () -> itemsSig.Value)

                let mappedProj =
                    rowProj
                    |> Projection.mapWith (fun k item ->
                        row {
                            match! item with
                            | x when x > 900 ->
                                let! tax = taxSig
                                return k + x + tax
                            | x -> return x
                        })

                Expect.equal (indexedProj.Get 0) "1:30" "createIndexProjectionWith with row"
                Expect.equal (mappedProj.Get 1) 934 "Projection.mapWith with row dynamic match! (1 + 930 + 3 = 934)"

                // Owned node inside row closed return raises ScopeMessages.factoryRow
                let badProj =
                    createProjectionWith
                        fst
                        (fun item ->
                            row {
                                let! (_, v) = item
                                createEffect ignore
                                return v
                            })
                        (fun () -> itemsSig.Value)

                let badEx = Expect.throwsC (fun () -> badProj.Get 1 |> ignore) id
                Expect.isTrue (isInvalidOperation badEx) "owned node in row throws InvalidOperationException"
                Expect.stringContains badEx.Message "factory body" "error cites factoryRow scope message"
            }

            test "Fix Round 1 Critical #1: effect preserves nested let! inside control flow after outer let! and lazy top-level use" {
                use g = new Graph ()
                let s1 = Signal (g, 1)
                let s2 = Signal (g, 10)
                let straySig = Signal (g, 1000)

                // Case 1: outer let! followed by use + inner let!
                let useLog = ResizeArray<string>()

                let _nUse =
                    effect {
                        node
                        let! a = s1
                        use _d = new TrackedDisposable ($"u{a}", useLog)
                        let! b = s2
                        useLog.Add $"run:{a}:{b}:{straySig.Value}"
                    }

                Expect.sequenceEqual useLog [ "run:1:10:1000"; "disp:u1" ] "effect runs nested let! inside use and disposes"

                // Mutating untracked straySig in body does NOT re-run effect
                straySig.Value <- 2000
                Expect.sequenceEqual useLog [ "run:1:10:1000"; "disp:u1" ] "straySig stays untracked inside effect body"

                // Mutating inner let! s2 re-runs effect dynamically
                s2.Value <- 20

                Expect.sequenceEqual
                    useLog
                    [ "run:1:10:1000"; "disp:u1"; "run:1:20:2000"; "disp:u1" ]
                    "inner let! s2 inside use is tracked and re-runs effect"

                // Case 2: outer let! followed by if/for/while/try-finally wrapping inner let!
                let gateSig = Signal (g, 0)
                let flowLog = ResizeArray<string>()
                let compCount = ref 0

                let _nFlow =
                    effect {
                        node
                        let! gate = gateSig

                        try
                            if gate > 0 then
                                for idx in 1..2 do
                                    let! b = s2
                                    flowLog.Add $"for:{gate}:{idx}:{b}"
                        finally
                            compCount.Value <- compCount.Value + 1
                    }

                Expect.sequenceEqual flowLog [] "gate=0 skips inner for+let!"
                Expect.equal compCount.Value 1 "try-finally runs compensation initially"

                // s2 is NOT tracked while gate=0
                s2.Value <- 30
                Expect.equal compCount.Value 1 "s2 untracked while guarded branch skipped"

                gateSig.Value <- 2
                Expect.sequenceEqual flowLog [ "for:2:1:30"; "for:2:2:30" ] "gate=2 enters for+inner let! loop"
                Expect.equal compCount.Value 2 "try-finally compensated on gate=2"

                s2.Value <- 40

                Expect.sequenceEqual
                    flowLog
                    [ "for:2:1:30"; "for:2:2:30"; "for:2:1:40"; "for:2:2:40" ]
                    "inner let! inside try-finally + if + for tracks s2 dynamically"

                // Case 3: mix of use arm and inner let! arm in match after outer let!
                let branchLog = ResizeArray<string>()
                let branchMode = Signal (g, 0)

                let _nMatch =
                    effect {
                        node
                        let! m = branchMode

                        match m with
                        | 0 ->
                            use _d = new TrackedDisposable ("arm0", branchLog)
                            branchLog.Add "arm0"
                        | _ ->
                            let! b = s2
                            branchLog.Add $"arm1:{b}"
                    }

                Expect.sequenceEqual branchLog [ "arm0"; "disp:arm0" ] "match arm0 with use runs and disposes"
                s2.Value <- 50
                Expect.sequenceEqual branchLog [ "arm0"; "disp:arm0" ] "s2 not tracked on arm0"
                branchMode.Value <- 1
                Expect.sequenceEqual branchLog [ "arm0"; "disp:arm0"; "arm1:50" ] "match switches to inner let! arm1"
                s2.Value <- 60
                Expect.sequenceEqual branchLog [ "arm0"; "disp:arm0"; "arm1:50"; "arm1:60" ] "s2 tracked dynamically on arm1"

                // Case 4: top-level use before let! (BoundPhase -> EvalBound + EvalOnceThen, runs & disposes once per evaluation)
                let topUseLog = ResizeArray<string>()

                let _topUseNode =
                    g.Run (fun () ->
                        effect {
                            node
                            use _d = new TrackedDisposable ("top", topUseLog)
                            let! a = s1
                            topUseLog.Add $"top:{a}"
                        })

                Expect.sequenceEqual topUseLog [ "top:1"; "disp:top" ] "top-level use before let! evaluates and disposes"
                s1.Value <- 2
                Expect.sequenceEqual topUseLog [ "top:1"; "disp:top"; "top:2"; "disp:top" ] "top-level use+let! tracks s1"
            }

            test "Fix Round 1 Important #1: boundary closed Phase-2 pending read fails with InvalidOperationException instead of Pending" {
                use g = new Graph ()
                let modeSig = Signal (g, 0)
                let pendSrc = AsyncSource<int> g
                let failedLog = ResizeArray<string>()

                let bWithFailed =
                    boundary {
                        pending (fun _ -> -1)

                        failed (fun ex _ ->
                            failedLog.Add (
                                if isNotReadyException ex then
                                    "raw-pending"
                                else
                                    "wrapped-err"
                            )

                            -2)

                        let! m = modeSig

                        match m with
                        | 0 -> return pendSrc.Value
                        | _ ->
                            let! v = pendSrc
                            return v + 10
                    }

                let bPendingOnly =
                    boundary {
                        pending (fun _ -> -1)
                        let! m = modeSig

                        if m = 0 then
                            return pendSrc.Value
                        else
                            let! v = pendSrc
                            return v + 100
                    }

                // Multi-bind closed return after 2 reactive let! bindings
                let stepSig = Signal (g, 5)

                let bTwoBinds =
                    boundary {
                        pending (fun _ -> -1)
                        let! m = modeSig
                        let! k = stepSig
                        return m + k + pendSrc.Value
                    }

                Expect.equal bWithFailed.Value -2 "closed Phase-2 pending read triggers failed handler rather than pending handler"
                Expect.sequenceEqual failedLog [ "wrapped-err" ] "failed handler received non-NotReadyException failure"

                let poReading = bPendingOnly.TryValue

                match poReading with
                | Failed ex ->
                    Expect.isTrue (isInvalidOperation ex) "pending-only closed Phase-2 pending read wraps to InvalidOperationException"
                    Expect.isFalse (isNotReadyException ex) "must not leak raw NotReadyException into Pending state"
                | other -> failtestf "expected Failed InvalidOperationException on bPendingOnly, got %A" other

                let twoBindReading = bTwoBinds.TryValue

                match twoBindReading with
                | Failed ex ->
                    Expect.isTrue (isInvalidOperation ex) "two-bind closed Phase-2 pending read surfaces InvalidOperationException"
                    Expect.isFalse (isNotReadyException ex) "must not hang Pending after 2 reactive binds"
                | other -> failtestf "expected Failed InvalidOperationException on bTwoBinds, got %A" other

                // Settling pendSrc later must NOT silently wake untracked closed Phase-2 failure while m = 0,
                // while switching m = 1 MUST suspend Pending in Phase 1 and wake on Settle.
                modeSig.Value <- 1
                Expect.equal bWithFailed.Value -1 "reactive let! pendSrc branch enters pending handler"
                Expect.equal bPendingOnly.Value -1 "pending-only reactive let! pendSrc branch enters pending handler"

                pendSrc.Settle 40
                Expect.equal bWithFailed.Value 50 "reactive let! pendSrc wakes boundary after settle"
                Expect.equal bPendingOnly.Value 140 "pending-only boundary wakes after settle"
                modeSig.Value <- 0
                Expect.equal bWithFailed.Value 40 "closed Phase-2 read succeeds once pendSrc is settled"
            }

            test "Fix Round 1 Important #2: non-BindReturn return after reactive let! stays untracked across memo, editable, draft, boundary, row" {
                use g = new Graph ()
                let modeSig = Signal (g, 0)
                let stepSig = Signal (g, 1)
                let s2 = Signal (g, 20)
                let straySig = Signal (g, 500)
                let pendSrc = AsyncSource<int> g
                let memoRuns = ref 0
                let memoUseLog = ResizeArray<string>()

                // 1. Pure memo mixing closed return arm, use+return arm, and 3-level nested let! arm
                let mMix =
                    memo {
                        let! m = modeSig
                        memoRuns.Value <- memoRuns.Value + 1

                        match m with
                        | 0 -> return straySig.Value
                        | 1 ->
                            use _d = new TrackedDisposable ("m1", memoUseLog)
                            return straySig.Value + 1
                        | 2 -> return pendSrc.Value
                        | _ ->
                            let! k = stepSig

                            if k = 1 then
                                let! c = pendSrc
                                return m + k + c
                            else
                                return 0
                    }

                Expect.equal mMix.Value 500 "m=0 returns straySig initial value"
                let runs0 = memoRuns.Value
                straySig.Value <- 600
                Expect.equal mMix.Value 500 "non-BindReturn return arm after match! keeps straySig untracked"
                Expect.equal memoRuns.Value runs0 "memo did not recompute on straySig update"

                modeSig.Value <- 1
                Expect.equal mMix.Value 601 "m=1 use+return reads latest straySig=600"
                Expect.sequenceEqual memoUseLog [ "disp:m1" ] "use in mixed match arm disposes cleanly"
                let runs1 = memoRuns.Value
                straySig.Value <- 700
                Expect.equal mMix.Value 601 "use+return arm after let! also keeps straySig untracked"
                Expect.equal memoRuns.Value runs1 "memo did not recompute on straySig update after use+return"

                // Closed pending read in pure memo wraps InvalidOperationException
                modeSig.Value <- 2
                let pureClosedEx = failedWith mMix.TryValue
                Expect.isTrue (isInvalidOperation pureClosedEx) "closed match! return pendSrc wraps InvalidOperationException"
                Expect.isFalse (isNotReadyException pureClosedEx) "pure non-BindReturn return must not suspend Pending"

                // Owning memo non-BindReturn pending read wraps InvalidOperationException too
                let mOwning =
                    memo {
                        owning
                        let! m = modeSig

                        if m = 2 then
                            return pendSrc.Value
                        else
                            return straySig.Value
                    }

                let ownClosedEx = failedWith mOwning.TryValue
                Expect.isTrue (isInvalidOperation ownClosedEx) "owning memo closed return wraps InvalidOperationException"
                Expect.isFalse (isNotReadyException ownClosedEx) "owning memo closed return must not suspend Pending"

                // 3-level nested reactive let! (m=3 -> k=1 -> pendSrc) preserves raw Phase-1 Pending & wakes cleanly
                modeSig.Value <- 3
                Expect.equal mMix.TryValue Pending "3-level nested let! inside memo preserves raw Phase-1 Pending"

                // ColdPhase 0-bind regression check: 0-bind return remains tracked
                let mColdTracked = g.Run (fun () -> memo { return s2.Value })
                Expect.equal mColdTracked.Value 20 "ColdPhase 0-bind initial"
                s2.Value <- 25
                Expect.equal mColdTracked.Value 25 "ColdPhase 0-bind remains tracked"

                // 2. Editable and draft with match! / use non-BindReturn return stay untracked
                let editMode = Signal (g, 0)
                let editLog = ResizeArray<string>()

                let eMix =
                    editable {
                        let! m = editMode

                        match m with
                        | 0 ->
                            use _d = new TrackedDisposable ("e0", editLog)
                            return straySig.Value
                        | _ ->
                            let! k = stepSig
                            return k + straySig.Value
                    }

                let dMix =
                    draft {
                        let! m = editMode

                        if m = 0 then
                            return straySig.Value + 10
                        else
                            let! k = stepSig
                            return k + straySig.Value
                    }

                Expect.equal eMix.Value 700 "editable m=0 initial"
                Expect.equal dMix.Value 710 "draft m=0 initial"
                Expect.sequenceEqual editLog [ "disp:e0" ] "editable use disposed"

                eMix.Value <- 9999
                dMix.Value <- 8888
                straySig.Value <- 800
                Expect.equal eMix.Value 9999 "editable keeps local edit when closed return straySig changes"
                Expect.equal dMix.Value 8888 "draft keeps local edit when closed return straySig changes"
                editMode.Value <- 1
                Expect.equal eMix.Value 801 "editable updates cleanly when bound editMode changes"
                Expect.equal dMix.Value 8888 "draft retains edit across mode switch until Reset()"
                dMix.Reset ()
                Expect.equal dMix.Value 801 "draft Reset() reflects updated seed"

                // 3. Row non-BindReturn return keeps stray untracked, wraps closed Pending,
                //    propagates inner let! Pending to enclosing Boundary, and does not leak onto outer memo
                let itemsSig = Signal (g, [ (1, 0); (2, 3) ])
                let rowMixRuns = ref 0

                let projMix =
                    g.Run (fun () ->
                        createProjectionWith
                            fst
                            (fun item ->
                                row {
                                    let! (_, price) = item
                                    rowMixRuns.Value <- rowMixRuns.Value + 1

                                    match price with
                                    | 0 -> return straySig.Value
                                    | -1 -> return pendSrc.Value
                                    | _ ->
                                        let! v = pendSrc
                                        return price + v
                                })
                            (fun () -> itemsSig.Value))

                Expect.equal (projMix.Get 1) 800 "row m=0 reads closed straySig"
                let rRuns0 = rowMixRuns.Value
                straySig.Value <- 900
                Expect.equal (projMix.Get 1) 800 "row non-BindReturn return keeps straySig untracked"
                Expect.equal rowMixRuns.Value rRuns0 "row did not recompute on straySig update"

                // Row inside outer memo Phase 2 must NOT leak row's internal let! dependency onto outer memo
                let innerRow =
                    row {
                        let! v = s2
                        return v * 2
                    }

                let outerMemoCallingRow =
                    memo {
                        let! m = stepSig
                        return m + innerRow ()
                    }

                Expect.equal outerMemoCallingRow.Value 51 "outer memo calling row in closed Phase 2 computes 1 + 50"
                s2.Value <- 30
                Expect.equal outerMemoCallingRow.Value 51 "row called in outer memo closed Phase 2 does NOT leak s2 dependency"

                // Row pending check: key -1 triggers closed InvalidOperationException, key 2 suspends Boundary Pending until settled
                itemsSig.Value <- [ (1, -1); (2, 3) ]

                let bRowClosed =
                    g.Run (fun () -> createBoundary (fun _ -> -1) (fun _ _ -> -2) (fun () -> projMix.Get 1))

                let bRowPhase1 =
                    g.Run (fun () -> createBoundary (fun _ -> -1) (fun _ _ -> -2) (fun () -> projMix.Get 2))

                Expect.equal bRowClosed.Value -2 "row closed Phase-2 pending read fails with InvalidOperationException (-2)"
                Expect.equal bRowPhase1.Value -1 "row Phase-1 inner let! pendSrc suspends enclosing boundary (-1)"

                pendSrc.Settle 100
                Expect.equal mMix.Value 104 "3-level nested let! memo wakes with settled pendSrc (3 + 1 + 100 = 104)"
                Expect.equal bRowPhase1.Value 103 "enclosing boundary wakes when row inner let! pendSrc settles (3 + 100 = 103)"
            }

            test "Fix Round 1 Important #3: previous/previousOr construction does not eagerly execute preamble or unready reads" {
                use g = new Graph ()
                let sig1 = Signal (g, 10)
                let pendSrc = AsyncSource<int> g
                let mutable memoBoundPreamble = 0
                let mutable memoColdPreamble = 0
                let mutable editPreamble = 0
                let mutable draftPreamble = 0

                let mBoundLazy, mColdLazy, eLazy, dLazy =
                    g.Run (fun () ->
                        let mb =
                            memo {
                                let! prev = memo.previous
                                memoBoundPreamble <- memoBoundPreamble + 1
                                let p0 = ValueOption.defaultValue 0 prev
                                let v = pendSrc.Value
                                let! x = sig1
                                return p0 + v + x
                            }

                        let mc =
                            memo {
                                let! prev = memo.previousOr 1
                                memoColdPreamble <- memoColdPreamble + 1
                                return prev + sig1.Value
                            }

                        let ed =
                            editable {
                                let! prev = editable.previous
                                editPreamble <- editPreamble + 1
                                let v = pendSrc.Value
                                let! x = sig1

                                return
                                    match prev with
                                    | ValueNone -> v + x
                                    | ValueSome p -> p + x
                            }

                        let dr =
                            draft {
                                let! prev = draft.previousOr 5
                                draftPreamble <- draftPreamble + 1
                                let! x = sig1
                                return prev + x
                            }

                        mb, mc, ed, dr)

                Expect.equal memoBoundPreamble 0 "memo.previous + unready preamble did not execute at construction"
                Expect.equal memoColdPreamble 0 "0-bind memo.previousOr preamble did not execute at construction"
                Expect.equal editPreamble 0 "editable.previous + unready preamble did not execute at construction"
                Expect.equal draftPreamble 0 "draft.previousOr preamble did not execute at construction"

                // Also verify ColdPhase Combine before let! after previous stays lazy outside g.Run when inside g.Run
                let mutable combinedPreamble = 0

                let mCombined =
                    g.Run (fun () ->
                        memo {
                            let! prev = memo.previousOr 2

                            if sig1.Value > 0 then
                                combinedPreamble <- combinedPreamble + 1

                            let! x = sig1
                            return prev + x
                        })

                Expect.equal combinedPreamble 0 "Combine before reactive let! after previous stays lazy at construction"

                pendSrc.Settle 7
                Expect.equal mBoundLazy.Value 17 "mBoundLazy evaluates 0 + 7 + 10 on first read"
                Expect.equal mColdLazy.Value 11 "mColdLazy evaluates 1 + 10 on first read"
                Expect.equal eLazy.Value 17 "eLazy evaluates 7 + 10 on first read"
                Expect.equal dLazy.Value 15 "dLazy evaluates 5 + 10 on first read"
                Expect.equal mCombined.Value 12 "mCombined evaluates 2 + 10 on first read"
                Expect.equal memoBoundPreamble 1 "mBoundLazy preamble ran once on read"
                Expect.equal memoColdPreamble 1 "mColdLazy preamble ran once on read"
                Expect.equal editPreamble 1 "eLazy preamble ran once on read"
                Expect.equal draftPreamble 1 "dLazy preamble ran once on read"

                sig1.Value <- 20
                Expect.equal mBoundLazy.Value 44 "mBoundLazy uses previous=17 -> 17 + 7 + 20"
                Expect.equal mColdLazy.Value 31 "mColdLazy uses previous=11 -> 11 + 20"
                Expect.equal eLazy.Value 37 "eLazy uses previous=17 -> 17 + 20"
                Expect.equal dLazy.Value 35 "dLazy uses previous=15 -> 15 + 20"
                Expect.equal mCombined.Value 32 "mCombined uses previous=12 -> 12 + 20"
            }

            test
                "Fix Round 2 Important #1: if/match mixes direct let! arm with use/Combine+let! arms across all 6 builders and preserves EffectOn leaf use" {
                use g = new Graph ()
                let s1 = Signal (g, 10)
                let s2 = Signal (g, 20)
                let s3 = Signal (g, 30)
                let modeSig = Signal (g, 0)
                let log = ResizeArray<string>()

                // 1. Standalone top-level if/match mixing direct let! arm with use+let! arm (true and false arms)
                let mBranch cond =
                    memo {
                        if cond then
                            let! x = s1
                            return x + 1
                        else
                            use _d = new TrackedDisposable ("m", log)
                            let! y = s2
                            return y + 2
                    }

                let mOwnBranch cond =
                    memo {
                        owning

                        match cond with
                        | 0 ->
                            let! x = s1
                            return x
                        | 1 ->
                            use _d = new TrackedDisposable ("mown", log)
                            let! y = s2
                            return y + 100
                        | _ ->
                            try
                                let! z = s3
                                return z + 200
                            finally
                                log.Add "fin-leaf"
                    }

                let mOptBranch cond =
                    memo {
                        option

                        if cond then
                            let! x = s1
                            return Some x
                        else
                            use _d = new TrackedDisposable ("mopt", log)
                            let! y = s2
                            return Some y
                    }

                let eBranch cond =
                    editable {
                        if cond then
                            let! x = s1
                            return x
                        else
                            use _d = new TrackedDisposable ("edit", log)
                            let! y = s2
                            return y + 5
                    }

                let dBranch cond =
                    draft {
                        if cond then
                            let! x = s1
                            return x
                        else
                            use _d = new TrackedDisposable ("draft", log)
                            let! y = s2
                            return y + 6
                    }

                let bBranch cond =
                    boundary {
                        pending (fun _ -> -1)

                        if cond then
                            let! x = s1
                            return x
                        else
                            use _d = new TrackedDisposable ("bnd", log)
                            let! y = s2
                            return y + 7
                    }

                let rBranch cond =
                    row {
                        if cond then
                            let! x = s1
                            return x
                        else
                            use _d = new TrackedDisposable ("row", log)
                            let! y = s2
                            return y + 8
                    }

                let effBranch cond =
                    g.Run (fun () ->
                        effect {
                            node

                            if cond then
                                let! x = s1
                                log.Add $"eff0:{x}"
                            else
                                use _d = new TrackedDisposable ("eff", log)
                                let! y = s2
                                log.Add $"eff1:{y}"
                        })

                let mTrue = mBranch true

                let mFalse, mOwn1, mOwn2, mOptFalse, eFalse, dFalse, bFalse =
                    g.Run (fun () -> mBranch false, mOwnBranch 1, mOwnBranch 2, mOptBranch false, eBranch false, dBranch false, bBranch false)

                let rFalse = rBranch false
                let _effNodeFalse = effBranch false

                // Before any Memo/Editable/Draft/Boundary/Row is read, only eager Effect logged.
                Expect.sequenceEqual log [ "eff1:20"; "disp:eff" ] "mixed effect branch runs on construction while lazy branches defer"

                // Evaluate Run #1 and verify single disposal (no EvalBound double-call leak of _d on Run #1)
                Expect.equal mTrue.Value 11 "memo if direct let! branch"
                Expect.equal mFalse.Value 22 "memo else use+let! branch"
                Expect.equal mOwn1.Value 120 "owning memo match use+let! branch"
                Expect.equal mOwn2.Value 230 "owning memo match try-finally+let! branch"
                Expect.equal mOptFalse.Value (Some 20) "option memo else use+let! branch"
                Expect.equal eFalse.Value 25 "editable else use+let! branch"
                Expect.equal dFalse.Value 26 "draft else use+let! branch"
                Expect.equal bFalse.Value 27 "boundary else use+let! branch"
                Expect.equal (rFalse ()) 28 "row else use+let! branch"

                Expect.sequenceEqual
                    log
                    [
                        "eff1:20"
                        "disp:eff"
                        "disp:m"
                        "disp:mown"
                        "fin-leaf"
                        "disp:mopt"
                        "disp:edit"
                        "disp:draft"
                        "disp:bnd"
                        "disp:row"
                    ]
                    "all 6 mixed-branch use+let! runs execute and dispose exactly once on first evaluation"

                log.Clear ()

                // 2. Reactive updates on Run #2+ obtain fresh disposables
                s2.Value <- 21
                Expect.equal mFalse.Value 23 "memo else use+let! recomputes on s2"
                Expect.equal eFalse.Value 26 "editable else use+let! recomputes on s2"
                Expect.equal dFalse.Value 27 "draft else use+let! recomputes on s2"
                Expect.equal bFalse.Value 28 "boundary else use+let! recomputes on s2"
                Expect.equal (rFalse ()) 29 "row else use+let! recomputes on s2"

                // 3. Outer let! + match mixing direct let!, use+let!, and Combine+let!
                let mNested =
                    memo {
                        let! m = modeSig

                        match m with
                        | 0 ->
                            let! x = s1
                            return x
                        | 1 ->
                            use _d = new TrackedDisposable ("nm1", log)
                            let! y = s2
                            return y
                        | _ ->
                            if true then
                                log.Add "nm2"

                            let! z = s3
                            return z
                    }

                Expect.equal mNested.Value 10 "nested match arm 0"
                modeSig.Value <- 1
                Expect.equal mNested.Value 21 "nested match arm 1 (use+let!)"
                modeSig.Value <- 2
                Expect.equal mNested.Value 30 "nested match arm 2 (Combine+let!)"

                // 4. Leaf use inside effect { let! a = withComparer cmp s1; use _d = ...; log.Add a } keeps EffectOn cutoff
                let leafEffectLog = ResizeArray<string>()
                let mod2 = comparer (fun (a: int) b -> a % 2 = b % 2)
                let keySig = Signal (g, 1)

                let _leafEffNode =
                    effect {
                        node
                        let! k = withComparer mod2 keySig
                        use _d = new TrackedDisposable ($"leaf{k}", leafEffectLog)
                        leafEffectLog.Add $"k={k}"
                    }

                Expect.sequenceEqual leafEffectLog [ "k=1"; "disp:leaf1" ] "EffectOn with leaf use runs initially"
                keySig.Value <- 3
                Expect.sequenceEqual leafEffectLog [ "k=1"; "disp:leaf1" ] "EffectOn cutoff under withComparer preserved despite leaf use"
                keySig.Value <- 4
                Expect.sequenceEqual leafEffectLog [ "k=1"; "disp:leaf1"; "k=4"; "disp:leaf4" ] "EffectOn fires when mod2 key changes"
            }

            test
                "Fix Round 2 Important #2: try/with catching 2nd+ Phase-1 NotReadyException preserves Pending, clears Phase1Pending, and Row works outside Activate" {
                use g = new Graph ()
                let s1 = Signal (g, 1)
                let pendSrc = AsyncSource<int> g
                let pendOther = AsyncSource<int> g

                let mTryPending =
                    memo {
                        let! a = s1

                        try
                            let! b = pendSrc
                            return a + b
                        with _ ->
                            return 99
                    }

                let mTrySwallowClosedRead =
                    memo {
                        let! a = s1

                        try
                            let! _b = pendSrc
                            return a
                        with _ ->
                            return pendOther.Value
                    }

                let mTryReraise =
                    memo {
                        let! a = s1

                        try
                            let! b = pendSrc
                            return a + b
                        with ex ->
                            raise ex
                            return 0
                    }

                let bTryPending =
                    boundary {
                        pending (fun _ -> -1)
                        failed (fun _ _ -> -2)
                        let! a = s1

                        try
                            let! b = pendSrc
                            return a + b
                        with _ ->
                            return 99
                    }

                let rTryPending =
                    row {
                        let! a = s1

                        try
                            let! b = pendSrc
                            return a + b
                        with _ ->
                            return 99
                    }

                let mHostingRow = Make.Memo (g, (fun _ -> rTryPending ()))

                let bHostingRow =
                    g.Run (fun () -> createBoundary (fun _ -> -1) (fun _ _ -> -2) (fun () -> rTryPending ()))

                // 2nd+ let! NotReadyException caught by user try/with STILL suspends Pending (raisedPending preserved!)
                Expect.equal mTryPending.TryValue Pending "memo 2nd+ let! caught by try/with must still suspend Pending"
                Expect.isTrue (isNull InternalCeState.CurrentPhase1Pending) "Phase1Pending cleared when try/with swallows NotReadyException"

                // Inside the with handler itself, reading pendOther.Value untracked wraps as InvalidOperationException
                let handlerEx = failedWith mTrySwallowClosedRead.TryValue
                Expect.isTrue (isInvalidOperation handlerEx) "closed pending read inside try/with handler wraps InvalidOperationException"
                Expect.isFalse (isNotReadyException handlerEx) "closed pending read in handler must not leak raw NotReadyException"
                Expect.isTrue (isNull InternalCeState.CurrentPhase1Pending) "Phase1Pending remains cleared after handler fails"

                // Reraise in try/with still suspends Pending cleanly
                Expect.equal mTryReraise.TryValue Pending "memo try/with reraise preserves Pending"
                Expect.isTrue (isNull InternalCeState.CurrentPhase1Pending) "Phase1Pending cleared after reraise unwinds node"

                // Boundary and Row hosted in Memo/Boundary preserve raisedPending across try/with too
                Expect.equal bTryPending.Value -1 "boundary 2nd+ let! caught by try/with invokes pending (-1), not 99"
                Expect.equal mHostingRow.TryValue Pending "row 2nd+ let! caught by try/with suspends hosting Memo Pending"
                Expect.equal bHostingRow.Value -1 "row 2nd+ let! caught by try/with suspends hosting Boundary pending (-1)"

                // Row bound to IReactive outside ambient g.Run enters graph cleanly for factoryRow violation & closed return
                let straySig = Signal (g, 500)

                let rowOutside =
                    row {
                        let! x = s1
                        return x + straySig.Value
                    }

                let badRowOutside =
                    row {
                        let! x = s1
                        createEffect ignore
                        return x
                    }

                Expect.equal (rowOutside ()) 501 "row bound to Signal works outside g.Run"
                let badRowEx = Expect.throwsC (fun () -> badRowOutside () |> ignore) id
                Expect.isTrue (isInvalidOperation badRowEx) "owned node in row outside g.Run raises InvalidOperationException"
                Expect.stringContains badRowEx.Message "factory body" "error cites factoryRow instead of missing ambient graph"

                // Settle pendSrc -> all Pending computations wake up cleanly
                pendSrc.Settle 10
                Expect.equal mTryPending.Value 11 "mTryPending wakes to 11 after settle"
                Expect.equal mTryReraise.Value 11 "mTryReraise wakes to 11 after settle"
                Expect.equal bTryPending.Value 11 "bTryPending wakes to 11 after settle"
                Expect.equal mHostingRow.Value 11 "mHostingRow wakes to 11 after settle"
                Expect.equal bHostingRow.Value 11 "bHostingRow wakes to 11 after settle"
            }
        ]
