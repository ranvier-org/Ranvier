module Ranvier.Tests.StreamSplit

open System
open System.Threading
open System.Threading.Tasks
open Expecto
open Ranvier
open Ranvier.Tests.Streams

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

/// <summary>Completes once a manual stream start's task settlement has been observed.</summary>
let private settled (start: StreamStart<'T>) : Async<unit> =
#if FABLE_COMPILER
    async {
        let! _ = Async.Catch (Async.AwaitTask start.Done.Task)
        return ()
    }
#else
    async.Return ()
#endif

/// <summary>
/// Closed-window two-phase semantics of <c>createStreamOn</c> and <c>createStreamFoldOn</c>.
/// </summary>
[<Tests>]
let tests =
    testList
        "StreamSplit"
        [
            test "equal key keeps open stream running without seed reset; signals read inside start are untracked" {
                use g = new Graph ()
                use _ = g.Activate ()
                let room = createSignal ("lobby", 1)
                let untrackedMeta = createSignal "v1"
                let latestFeed = ManualStream<string>()
                let foldFeed = ManualStream<int>()
                let startedKeys = ResizeArray<string>()

                let latest =
                    createStreamOn (fun () -> fst room.Value) (fun key _ ->
                        // fsharpanalyzer: ignore-line-next RAN016
                        let meta = untrackedMeta.Value
                        startedKeys.Add $"{key}:{meta}"
                        latestFeed.Source)

                let folded =
                    createStreamFoldOn 100 (+) (fun () -> fst room.Value) (fun _ _ ->
                        // fsharpanalyzer: ignore-line-next RAN016
                        untrackedMeta.Value |> ignore
                        foldFeed.Source)

                // Lazy pull-on-read: construction starts neither stream.
                Expect.equal latestFeed.Starts 0 "createStreamOn starts on first read"
                Expect.equal foldFeed.Starts 0 "createStreamFoldOn starts on first read"

                Expect.equal latest.TryValue Pending "latest initial Pending"
                Expect.equal folded.TryValue Pending "folded initial Pending"
                Expect.equal latestFeed.Starts 1 "latest started once"
                Expect.equal foldFeed.Starts 1 "folded started once"
                Expect.equal untrackedMeta.ObserverCount 0 "signals read inside start are untracked"

                latestFeed.Push "hello"
                foldFeed.Push 10
                foldFeed.Push 5
                Expect.equal latest.Value "hello" "latest published item"
                Expect.equal folded.Value 115 "folded accumulated from seed 100"

                // Mutating untracked signal inside start does not restart streams.
                untrackedMeta.Value <- "v2"
                Expect.equal latest.Value "hello" "untracked mutation ignored by latest"
                Expect.equal folded.Value 115 "untracked mutation ignored by folded"
                Expect.equal latestFeed.Starts 1 "still 1 start on latest"
                Expect.equal foldFeed.Starts 1 "still 1 start on folded"

                let effectLog = ResizeArray<string * int>()
                createEffect (fun () -> effectLog.Add (latest.Value, folded.Value))
                Expect.sequenceEqual effectLog [ ("hello", 115) ] "initial effect observation"

                // Equal key ("lobby", 1) -> ("lobby", 2) keeps open StreamSource running (no cancellation, no seed reset, no spurious effect wakeup!).
                room.Value <- ("lobby", 2)
                Expect.equal latest.Value "hello" "equal key keeps latest value"
                Expect.equal folded.Value 115 "equal key keeps folded accumulator without seed reset"
                Expect.sequenceEqual effectLog [ ("hello", 115) ] "equal key does not re-run downstream effect"
                Expect.isFalse latestFeed.Latest.Token.IsCancellationRequested "latest token not cancelled on equal key"
                Expect.isFalse foldFeed.Latest.Token.IsCancellationRequested "folded token not cancelled on equal key"
                Expect.equal latestFeed.Starts 1 "no reconnect on latest"
                Expect.equal foldFeed.Starts 1 "no reconnect on folded"

                // Open streams continue delivering items after equal-key check!
                latestFeed.Push "world"
                foldFeed.Push 20
                Expect.equal latest.Value "world" "open stream continues publishing on latest"
                Expect.equal folded.Value 135 "open stream continues folding without seed reset"
                Expect.sequenceEqual effectLog [ ("hello", 115); ("world", 115); ("world", 135) ] "open streams wake downstream effect on new items"
                Expect.sequenceEqual startedKeys [ "lobby:v1" ] "start invoked once"
            }

            test "unequal key cancels previous stream token, resets to seed, and opens a new stream" {
                use g = new Graph ()
                use _ = g.Activate ()
                let topic = createSignal 1
                let feed = ManualStream<int>()
                let keys = ResizeArray<int>()

                let folded =
                    createStreamFoldOn 10 (+) (fun () -> topic.Value % 10) (fun k _ ->
                        keys.Add k
                        feed.Source)

                Expect.equal folded.TryValue Pending "initial Pending"
                feed.Push 5
                feed.Push 7
                Expect.equal folded.Value 22 "folded 10 + 5 + 7 = 22"
                let firstStart = feed.At 0

                // Unequal key (1 -> 2) cancels previous token, resets to Pending (from seed 10), and opens new stream.
                topic.Value <- 2
                Expect.equal folded.TryValue Pending "unequal key resets stream to Pending until first item or completion"
                Expect.isTrue firstStart.Token.IsCancellationRequested "previous stream token cancelled"
                Expect.equal feed.Starts 2 "second stream started"
                Expect.sequenceEqual keys [ 1; 2 ] "started with keys 1 then 2"

                // Stale pushes on old sink are ignored; new sink folds from fresh seed 10.
                firstStart.Sink.Next 999
                Expect.equal folded.TryValue Pending "closed first sink ignored"
                feed.Push 3
                Expect.equal folded.Value 13 "folded from fresh seed 10 + 3 = 13"
            }

            testCaseAsync "stream items or completion arriving while Phase 1 is Pending or Failed fold in background and surface on recovery"
            <| async {
                use g = new Graph ()
                use _ = g.Activate ()
                let key = createSignal 1
                let gate = createSignal 0
                let pending = createAsyncSource<int>()

                let upstreamErrNode =
                    createMemo (fun _ ->
                        if gate.Value = 2 then
                            failwith "phase1-stream-err"

                        0)

                let feed = ManualStream<int>()
                let seenReady = ResizeArray<int>()

                let folded =
                    createStreamFoldOn
                        0
                        (+)
                        (fun () ->
                            if gate.Value = 1 then
                                pending.Value |> ignore

                            upstreamErrNode.Value |> ignore
                            key.Value % 10)
                        (fun _ _ -> feed.Source)

                createEffect (fun () ->
                    match folded.TryValue with
                    | Ready v -> seenReady.Add v
                    | _ -> ())

                Expect.equal feed.Starts 1 "stream 0 started for key 1"
                Expect.isEmpty seenReady "initially pending"

                // Enter Phase 1 Pending overlay while stream for key 1 is open.
                gate.Value <- 1
                Expect.equal (folded.Status &&& Status.Pending) Status.Pending "Status has Pending"
                Expect.equal folded.TryValue Pending "TryValue is Pending"

                // Open stream for K1 pushes [10; 20] while Phase 1 is Pending!
                feed.Push 10
                feed.Push 20
                g.Flush ()

                Expect.equal folded.Status Status.Pending "Status stays Pending while Phase 1 is Pending"
                Expect.equal folded.TryValue Pending "TryValue stays Pending while Phase 1 is Pending"
                Expect.isEmpty seenReady "downstream observer did not see Ready values while Phase 1 is Pending"
                Expect.isFalse feed.Latest.Token.IsCancellationRequested "open stream was not cancelled"

                // Switch Phase 1 to Failed overlay, push another item (5 -> total 35), verify upstream ErrorOrigin preserved.
                gate.Value <- 2
                let p1Ex = failedWith folded.TryValue
                Expect.equal p1Ex.Message "phase1-stream-err" "Phase 1 Error overlay surfaced"
                Expect.isTrue (obj.ReferenceEquals (folded.ErrorOrigin, upstreamErrNode :> INode)) "upstream ErrorOrigin preserved"

                feed.Push 5
                g.Flush ()
                Expect.equal (failedWith folded.TryValue).Message "phase1-stream-err" "still masked by Phase 1 Error overlay"
                Expect.isEmpty seenReady "downstream observer still sees no Ready value"

                // Settle Phase 1 back to equal key 11 % 10 = 1 -> folded value (10 + 20 + 5 = 35) surfaces with 1 stream start!
                key.Value <- 11
                gate.Value <- 0
                g.Flush ()

                Expect.equal folded.Status Status.None "Status is None after Phase 1 settles back to K1"
                Expect.equal folded.Value 35 "folded Value = 35 from items delivered during unready excursion"
                Expect.sequenceEqual seenReady [ 35 ] "downstream observer woken with 35"
                Expect.equal feed.Starts 1 "1 total stream start"

                // Also verify stream failure while Phase 1 is Pending surfaces once Phase 1 recovers to K1!
                gate.Value <- 1
                Expect.equal folded.TryValue Pending "back in Phase 1 Pending"
                let streamBoom = InvalidOperationException "stream-boom"
                feed.Fail streamBoom
                do! settled feed.Latest

                Expect.equal folded.Status Status.Pending "stream failure masked while Phase 1 is Pending"
                gate.Value <- 0
                let surfacedErr = failedWith folded.TryValue
                Expect.equal surfacedErr.Message "stream-boom" "stream failure surfaces once Phase 1 recovers"
                Expect.isTrue (obj.ReferenceEquals (folded.ErrorOrigin, folded :> INode)) "ErrorOrigin is folded StreamMemo"
                Expect.equal feed.Starts 1 "still 1 stream start"
            }

            test "synchronous NotReadyException inside start fails with InvalidOperationException and Phase 1/Phase 2 enforce ScopeMode.Pure" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 1
                let pending = createAsyncSource<int>()
                let feed = ManualStream<int>()

                let m =
                    createStreamOn (fun () -> s.Value) (fun _ _ ->
                        // fsharpanalyzer: ignore-line-next RAN016
                        pending.Value |> ignore
                        feed.Source)

                let ex = failedWith m.TryValue
                Expect.equal m.Status Status.Error "fails with Status.Error rather than stranding in Pending"
                Expect.isTrue (obj.ReferenceEquals (m.ErrorOrigin, m :> INode)) "ErrorOrigin is StreamMemo"
                Expect.isTrue (isInvalidOperation ex) "InvalidOperationException"
                Expect.isNonEmpty ex.Message "carries descriptive message"

                let isNotReadyInner =
                    match ex.InnerException with
                    | NotReadyException src -> obj.ReferenceEquals (src, pending :> INode)
                    | _ -> false

                Expect.isTrue isNotReadyInner "InnerException preserves NotReadyException and source"

                // Phase 1 and Phase 2 both enforce ScopeMode.Pure.
                let expectedPure = ScopeMessages.forMode ScopeMode.Pure

                let mPureRead =
                    createStreamOn
                        (fun () ->
                            // Asserts the runtime failure of an owned node created in a pure derivation.
                            // fsharpanalyzer: ignore-line-next RAN010
                            onCleanup ignore
                            1)
                        (fun _ _ -> feed.Source)

                let exRead = failedWith mPureRead.TryValue
                Expect.equal exRead.Message expectedPure "Phase 1 enforces ScopeMode.Pure"

                let mPureStart =
                    createStreamFoldOn 0 (+) (fun () -> 1) (fun _ _ ->
                        // Asserts the runtime failure of an owned node created in a pure derivation.
                        // fsharpanalyzer: ignore-line-next RAN010
                        onCleanup ignore
                        feed.Source)

                let exStart = failedWith mPureStart.TryValue
                Expect.equal exStart.Message expectedPure "Phase 2 start enforces ScopeMode.Pure"
            }

            testCaseAsync
                "task-wrapped and fold NotReadyException wrap into InvalidOperationException for downstream readers, and caught Phase 1 error rethrown in Phase 2 sets ErrorOrigin to self"
            <| async {
                use g = new Graph ()
                let pending = AsyncSource<int> g

                let sTask =
                    g.Run (fun () -> createStreamOn (fun () -> 1) (fun _ _ -> StreamSource.create (fun _ _ -> task { pending.Value |> ignore })))

                let downTask = g.Run (fun () -> createMemo (fun _ -> sTask.Value + 1))
                downTask.TryValue |> ignore
                do! drained ()

                let exTask = failedWith sTask.TryValue
                let exDownTask = failedWith downTask.TryValue
                Expect.equal sTask.Status Status.Error "sTask enters Status.Error"
                Expect.equal downTask.Status Status.Error "downstream Memo over sTask enters Status.Error rather than Pending"
                Expect.isTrue (isInvalidOperation exTask) "wrapped into InvalidOperationException"
                Expect.equal exTask.Message UntrackedPhase.streamOnPending "carries streamOnPending message"
                Expect.isTrue (obj.ReferenceEquals (exDownTask, exTask)) "downstream sees wrapped InvalidOperationException"
                Expect.isTrue (obj.ReferenceEquals (sTask.ErrorOrigin, sTask :> INode)) "sTask.ErrorOrigin is sTask"
                Expect.isTrue (obj.ReferenceEquals (downTask.ErrorOrigin, sTask :> INode)) "downTask.ErrorOrigin is sTask"

                let hasPendingInnerTask =
                    match exTask.InnerException with
                    | NotReadyException src -> obj.ReferenceEquals (src, pending :> INode)
                    | _ -> false

                Expect.isTrue hasPendingInnerTask "sTask InnerException preserves NotReadyException(pending)"

                // Fold reading pending.Value also wraps into InvalidOperationException rather than stranding downstream in Pending.
                let feed = ManualStream<int>()

                let sFold =
                    g.Run (fun () -> createStreamFoldOn 0 (fun acc x -> acc + x + pending.Value) (fun () -> 1) (fun _ _ -> feed.Source))

                let downFold = g.Run (fun () -> createMemo (fun _ -> sFold.Value + 1))
                downFold.TryValue |> ignore
                feed.Push 5
                do! drained ()

                let exFold = failedWith sFold.TryValue
                let exDownFold = failedWith downFold.TryValue
                Expect.equal sFold.Status Status.Error "sFold enters Status.Error"
                Expect.equal downFold.Status Status.Error "downstream Memo over sFold enters Status.Error rather than Pending"
                Expect.isTrue (isInvalidOperation exFold) "fold NotReadyException wrapped into InvalidOperationException"
                Expect.equal exFold.Message UntrackedPhase.streamOnPending "fold carries streamOnPending message"
                Expect.isTrue (obj.ReferenceEquals (exDownFold, exFold)) "downFold sees wrapped InvalidOperationException"
                Expect.isTrue (obj.ReferenceEquals (downFold.ErrorOrigin, sFold :> INode)) "downFold.ErrorOrigin is sFold"

                let hasPendingInnerFold =
                    match exFold.InnerException with
                    | NotReadyException src -> obj.ReferenceEquals (src, pending :> INode)
                    | _ -> false

                Expect.isTrue hasPendingInnerFold "sFold InnerException preserves NotReadyException(pending)"

                // Caught failed read in Phase 1 rethrown in Phase 2 start attributes ErrorOrigin to self, not failedNode.
                let failedNode = Make.Memo (g, fun _ -> failwith "upstream-stream-failed": int)
                let mutable caught: exn = null

                let sOrigin =
                    g.Run (fun () ->
                        createStreamOn
                            (fun () ->
                                try
                                    failedNode.Value
                                with ex ->
                                    caught <- ex
                                    1)
                            (fun _ _ -> raise caught))

                let exOrigin = failedWith sOrigin.TryValue
                Expect.equal exOrigin.Message "upstream-stream-failed" "rethrown message preserved"
                Expect.isTrue (obj.ReferenceEquals (sOrigin.ErrorOrigin, sOrigin :> INode)) "Phase 2 start rethrow sets ErrorOrigin to self"
            }

#if RANVIER_TRACE
            test "Trace.reconcile holds across createStreamOn and createStreamFoldOn lifecycles" {
                use g = new Graph ()
                use _ = g.Activate ()
                let room = createSignal ("alpha", 0)
                let gate = createSignal false
                let pending = createAsyncSource<int>()
                let feed1 = ManualStream<int>()
                let feed2 = ManualStream<int>()
                let seen = ResizeArray<int>()

                let latest =
                    createStreamOn
                        (fun () ->
                            if gate.Value then
                                pending.Value |> ignore

                            fst room.Value)
                        (fun _ _ -> feed1.Source)

                let folded =
                    createStreamFoldOn 0 (+) (fun () -> latest.Value) (fun _ _ -> feed2.Source)

                createEffect (fun () ->
                    match folded.TryValue with
                    | Ready v -> seen.Add v
                    | _ -> ())

                feed1.Push 1
                feed2.Push 10
                Expect.sequenceEqual seen [ 10 ] "initial stream pipeline"

                // Equal key on both levels:
                room.Value <- ("alpha", 1)
                feed1.Push 1
                feed2.Push 5
                Expect.sequenceEqual seen [ 10; 15 ] "no restart on equal keys"

                // Transient Phase 1 Pending while feed2 pushes:
                gate.Value <- true
                g.Flush ()
                feed2.Push 7
                g.Flush ()
                gate.Value <- false
                g.Flush ()
                Expect.sequenceEqual seen [ 10; 15; 22 ] "folded item surfaced after Pending excursion"

                // Unequal key restarts:
                room.Value <- ("beta", 0)
                g.Flush ()
                feed1.Push 2
                feed2.Push 100
                Expect.sequenceEqual seen [ 10; 15; 22; 100 ] "restarted from seed 0 on unequal key"

                folded.Dispose ()
                latest.Dispose ()

                Expect.isEmpty (Trace.reconcile g) "live edges and observer sets match folded trace log"
            }
#endif
        ]
