module Ranvier.Tests.AsyncSplit

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open Expecto
open Ranvier

/// <summary>A flight settled manually on the test thread.</summary>
type private Flight<'T>() =
    let source = TaskCompletionSource<'T>()
    member _.Task = source.Task

    member _.Settle(v: 'T) =
        source.SetResult v

    member _.Fail(e: exn) =
        source.SetException e

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

[<CustomEquality; NoComparison>]
type private GuardedKey =
    | GuardedKey of id: int * throwOnCompare: bool

    override this.Equals(other: obj) =
        match other with
        | :? GuardedKey as that ->
            let (GuardedKey (a, ta)) = this
            let (GuardedKey (b, tb)) = that

            if ta || tb then
                raise (InvalidOperationException "key comparer")

            a = b
        | _ -> false

    override _.GetHashCode() = 0

/// <summary>
/// Closed-window two-phase semantics of <c>createAsyncOn</c>, <c>createAsyncWithOn</c>,
/// <c>createAsyncOnWithPolicy</c>, and <c>createOwningAsyncOnWithPolicy</c>.
/// </summary>
[<Tests>]
let tests =
    testList
        "AsyncSplit"
        [
            testCaseAsync "equal key leaves active flight uncancelled and settled value un-refetched; compute reads are untracked"
            <| async {
                use g = new Graph ()
                let items = Signal (g, [ 1; 2 ])
                let theme = Signal (g, "light")
                let flights = ResizeArray<Flight<string>>()
                let tokens = ResizeArray<CancellationToken>()
                let keys = ResizeArray<int>()

                let a =
                    g.Run (fun () ->
                        createAsyncOn (fun () -> List.length items.Value) (fun len _ token ->
                            let currentTheme = theme.Value
                            keys.Add len
                            tokens.Add token
                            let f = Flight<string>()
                            flights.Add f

                            task {
                                let! prefix = f.Task
                                return $"{prefix}:{currentTheme}"
                            }))

                // Lazy pull-on-read: construction starts no flight; observing Effect then pulls flight 1.
                Expect.equal flights.Count 0 "construction starts no flight"
                let observed = ResizeArray<string>()
                g.Run (fun () -> createEffect (fun () -> observed.Add a.Value))

                Expect.equal a.TryValue Pending "initial flight pending"
                Expect.equal flights.Count 1 "one flight launched"
                Expect.sequenceEqual keys [ 2 ] "launched with key 2"
                Expect.equal theme.ObserverCount 0 "compute read of theme is untracked"
                Expect.isEmpty observed "effect suspended while initial flight is pending"

                // Mutating untracked theme or feeding equal key (length 2 -> length 2) during flight 1 leaves it running.
                theme.Value <- "dark"
                items.Value <- [ 3; 4 ]
                Expect.equal a.TryValue Pending "still pending on flight 1"
                Expect.isFalse tokens[0].IsCancellationRequested "equal key does not cancel active flight"
                Expect.equal flights.Count 1 "no second flight launched"

                // Flight 1 settles -> Ready; subsequent equal key (length 2 -> length 2) neither refetches nor wakes observing Effect.
                flights[0].Settle "2"
                do! drained ()
                Expect.equal a.Value "2:light" "settled flight 1 value"
                Expect.sequenceEqual observed [ "2:light" ] "effect ran once on settle"

                items.Value <- [ 5; 6 ]
                Expect.equal a.Value "2:light" "equal key keeps settled value without refetch"
                Expect.equal flights.Count 1 "still one flight total"
                Expect.sequenceEqual observed [ "2:light" ] "equal key does not re-run downstream effect"

                // Unequal key (length 2 -> length 3) launches flight 2 and observes latest untracked theme.
                items.Value <- [ 7; 8; 9 ]
                Expect.equal a.TryValue Pending "unequal key starts flight 2"
                Expect.equal flights.Count 2 "second flight launched"
                Expect.sequenceEqual keys [ 2; 3 ] "launched with key 3"

                flights[1].Settle "3"
                do! drained ()
                Expect.equal a.Value "3:dark" "settled flight 2 value"

                // Also verify synchronous already-completed Task.FromResult (`completed`) closes tracking before compute runs.
                let sSync = Signal (g, 1)
                let untrackedExtra = Signal (g, 100)
                let syncRuns = ref 0

                let aSync =
                    g.Run (fun () ->
                        createAsyncOn (fun () -> sSync.Value % 10) (fun k _ _ ->
                            syncRuns.Value <- syncRuns.Value + 1
                            completed (k + untrackedExtra.Value)))

                aSync.TryValue |> ignore
                do! drained ()
                Expect.equal aSync.Value 101 "completed task settles"
                Expect.equal untrackedExtra.ObserverCount 0 "completed task compute is also untracked"

                untrackedExtra.Value <- 200
                Expect.equal aSync.Value 101 "mutating untracked signal does not dirty aSync"
                Expect.equal syncRuns.Value 1 "compute still ran once"

                sSync.Value <- 11
                Expect.equal aSync.Value 101 "equal key does not re-run completed compute"
                Expect.equal syncRuns.Value 1 "compute skipped on equal key"

                sSync.Value <- 2
                aSync.TryValue |> ignore
                do! drained ()
                Expect.equal aSync.Value 202 "unequal key runs compute with latest untracked value"
                Expect.equal syncRuns.Value 2 "compute ran twice"
            }

            testCaseAsync "CancelPrevious and KeepLatest preserve active flights across equal keys and transient Phase 1 unready"
            <| async {
                use g = new Graph ()
                let s = Signal (g, 1)
                let gate = Signal (g, 0)
                let pending = AsyncSource<int>(g)
                let cancelFlights = ResizeArray<Flight<int>>()
                let cancelTokens = ResizeArray<CancellationToken>()
                let latestFlights = ResizeArray<Flight<int>>()
                let latestTokens = ResizeArray<CancellationToken>()

                let readKey () =
                    let m = gate.Value

                    if m = 1 then
                        pending.Value |> ignore
                    elif m = 2 then
                        failwith "phase1"

                    s.Value % 10

                let cancelling =
                    g.Run (fun () ->
                        createAsyncOnWithPolicy CancelPrevious readKey (fun _ _ token ->
                            cancelTokens.Add token
                            let f = Flight<int>()
                            cancelFlights.Add f
                            f.Task))

                let keeping =
                    g.Run (fun () ->
                        createAsyncOnWithPolicy KeepLatest readKey (fun _ _ token ->
                            latestTokens.Add token
                            let f = Flight<int>()
                            latestFlights.Add f
                            f.Task))

                Expect.equal cancelling.TryValue Pending "cancelling initial pending"
                Expect.equal keeping.TryValue Pending "keeping initial pending"

                // Equal key (11 % 10 = 1) and transient Phase 1 Pending / Failed keep flight 0 alive on both policies.
                s.Value <- 11
                Expect.equal cancelling.TryValue Pending "equal key cancelling"
                Expect.equal keeping.TryValue Pending "equal key keeping"

                gate.Value <- 1
                Expect.equal cancelling.TryValue Pending "Phase 1 Pending cancelling"
                Expect.equal keeping.TryValue Pending "Phase 1 Pending keeping"

                gate.Value <- 2
                Expect.equal (failedWith cancelling.TryValue).Message "phase1" "Phase 1 Error cancelling"
                Expect.equal (failedWith keeping.TryValue).Message "phase1" "Phase 1 Error keeping"

                gate.Value <- 0
                Expect.equal cancelling.TryValue Pending "back to active flight cancelling"
                Expect.equal keeping.TryValue Pending "back to active flight keeping"
                Expect.isFalse cancelTokens[0].IsCancellationRequested "CancelPrevious flight 0 uncancelled"
                Expect.isFalse latestTokens[0].IsCancellationRequested "KeepLatest flight 0 uncancelled"
                Expect.equal cancelFlights.Count 1 "CancelPrevious still 1 flight"
                Expect.equal latestFlights.Count 1 "KeepLatest still 1 flight"

                // Unequal key 2: CancelPrevious cancels flight 0; KeepLatest keeps flight 0 uncancelled and takes latest.
                s.Value <- 2
                Expect.equal cancelling.TryValue Pending "cancelling flight 1 started"
                Expect.equal keeping.TryValue Pending "keeping flight 1 started"
                Expect.isTrue cancelTokens[0].IsCancellationRequested "CancelPrevious cancelled superseded flight 0"
                Expect.isFalse latestTokens[0].IsCancellationRequested "KeepLatest left flight 0 uncancelled"
                Expect.equal cancelFlights.Count 2 "CancelPrevious 2 flights"
                Expect.equal latestFlights.Count 2 "KeepLatest 2 flights"

                latestFlights[1].Settle 20
                cancelFlights[1].Settle 20
                do! drained ()
                Expect.equal keeping.Value 20 "KeepLatest published newest flight"
                Expect.equal cancelling.Value 20 "CancelPrevious published newest flight"

                latestFlights[0].Settle 10
                cancelFlights[0].Settle 10
                do! drained ()
                Expect.equal keeping.Value 20 "KeepLatest ignored older flight 0"
                Expect.equal cancelling.Value 20 "CancelPrevious ignored older flight 0"
            }

            testCaseAsync "FinishCurrent, Drop, and DropWhilePending handle equal keys, unready overlays, owed trailing runs, and owed fault drops"
            <| async {
                use g = new Graph ()

                // 1. Equal key and transient Phase 1 Pending/Failed during active flight keep queued = 1 (zero spurious TrailingRun).
                let sEq = Signal (g, 1)
                let gateEq = Signal (g, 0)
                let pendingEq = AsyncSource<int>(g)
                let fcEqFlights = ResizeArray<Flight<int>>()
                let dwpEqFlights = ResizeArray<Flight<int>>()

                let readEq () =
                    let m = gateEq.Value

                    if m = 1 then
                        pendingEq.Value |> ignore
                    elif m = 2 then
                        failwith "phase1-eq"

                    sEq.Value % 10

                let fcEq =
                    g.Run (fun () ->
                        createAsyncOnWithPolicy FinishCurrent readEq (fun _ _ _ ->
                            let f = Flight<int>()
                            fcEqFlights.Add f
                            f.Task))

                let dwpEq =
                    g.Run (fun () ->
                        createAsyncOnWithPolicy DropWhilePending readEq (fun _ _ _ ->
                            let f = Flight<int>()
                            dwpEqFlights.Add f
                            f.Task))

                Expect.equal fcEq.TryValue Pending "fcEq flight 0"
                Expect.equal dwpEq.TryValue Pending "dwpEq flight 0"

                sEq.Value <- 11
                Expect.equal fcEq.TryValue Pending "fcEq equal key keeps queued = 1"
                Expect.equal dwpEq.TryValue Pending "dwpEq equal key keeps queued = 1"

                gateEq.Value <- 1
                Expect.equal fcEq.TryValue Pending "fcEq Phase 1 Pending"
                Expect.equal dwpEq.TryValue Pending "dwpEq Phase 1 Pending"

                gateEq.Value <- 2
                Expect.equal (failedWith fcEq.TryValue).Message "phase1-eq" "fcEq Phase 1 Failed"
                Expect.equal (failedWith dwpEq.TryValue).Message "phase1-eq" "dwpEq Phase 1 Failed"

                gateEq.Value <- 0
                Expect.equal fcEq.TryValue Pending "fcEq recovered to active flight"
                Expect.equal dwpEq.TryValue Pending "dwpEq recovered to active flight"

                // Also write an unpulled equal key (21 % 10 = 1) right before settling/failing!
                sEq.Value <- 21
                fcEqFlights[0].Settle 100
                dwpEqFlights[0].Fail(exn "dwp-boom")
                do! drained ()

                Expect.equal fcEq.Value 100 "FinishCurrent settles Ready 100 with no spurious TrailingRun"
                Expect.equal fcEqFlights.Count 1 "FinishCurrent launched 1 flight"
                Expect.equal (failedWith dwpEq.TryValue).Message "dwp-boom" "DropWhilePending surfaces error when only equal keys arrived"
                Expect.equal dwpEqFlights.Count 1 "DropWhilePending launched 1 flight (no spurious retry)"

                // 2. FinishCurrent: K1 -> K2 -> K1 during K1 flight holds Pending (`owedTrailing`) on RVal settle then surfaces K1's value without launching K2; K1 -> K2 launches K2.
                let sFc = Signal (g, 1)
                let fcKeys = ResizeArray<int>()
                let fcFlights = ResizeArray<Flight<int>>()

                let fc =
                    g.Run (fun () ->
                        createAsyncOnWithPolicy FinishCurrent (fun () -> sFc.Value % 10) (fun k _ _ ->
                            fcKeys.Add k
                            let f = Flight<int>()
                            fcFlights.Add f
                            f.Task))

                Expect.equal fc.TryValue Pending "fc flight 0 for K1=1"
                sFc.Value <- 2
                Expect.equal fc.TryValue Pending "K2=2 sets queued = 2"
                sFc.Value <- 11
                fcFlights[0].Settle 10
                do! drained ()

                // Before TrailingRun pulls `fc`, `owedTrailing` holds external Status = Status.Pending!
                Expect.equal (fc.Status &&& Status.Pending) Status.Pending "owedTrailing holds Status.Pending until TrailingRun"
                Expect.equal fc.Value 10 "TrailingRun sees equal key 11 % 10 = 1, clears owedTrailing, and surfaces 10"
                Expect.equal fc.Status Status.None "Status is now None"
                Expect.sequenceEqual fcKeys [ 1 ] "zero extra flights launched on K1 -> K2 -> K1"

                // Now test K1 -> K2 launching K2 on settle.
                sFc.Value <- 3
                Expect.equal fc.TryValue Pending "fc flight 1 for K=3"
                sFc.Value <- 4
                Expect.equal fc.TryValue Pending "K=4 deferred behind K=3"
                fcFlights[1].Settle 30
                do! drained ()
                Expect.equal fc.TryValue Pending "TrailingRun launches K=4"
                Expect.sequenceEqual fcKeys [ 1; 3; 4 ] "launched K=4 on settle"
                fcFlights[2].Settle 40
                do! drained ()
                Expect.equal fc.Value 40 "settled K=4"

                // 3. Owed flight faulting under FinishCurrent and DropWhilePending resets `hasKey <- false` so K1 -> K2 -> K1 launches a fresh flight instead of hanging in Pending.
                for policy in [ FinishCurrent; DropWhilePending ] do
                    let sFault = Signal (g, 1)
                    let faultKeys = ResizeArray<int>()
                    let faultFlights = ResizeArray<Flight<int>>()

                    let node =
                        g.Run (fun () ->
                            createAsyncOnWithPolicy policy (fun () -> sFault.Value % 10) (fun k _ _ ->
                                faultKeys.Add k
                                let f = Flight<int>()
                                faultFlights.Add f
                                f.Task))

                    Expect.equal node.TryValue Pending "initial flight for K1=1"
                    sFault.Value <- 2
                    Expect.equal node.TryValue Pending "K2=2 marks owed (queued = 2)"
                    sFault.Value <- 11
                    faultFlights[0].Fail(exn "dropped-fault")
                    do! drained ()

                    Expect.equal node.TryValue Pending "TrailingRun launches fresh flight for K1=1 instead of hanging"
                    Expect.sequenceEqual faultKeys [ 1; 1 ] "re-launched K1=1 because owed flight faulted"
                    faultFlights[1].Settle 99
                    do! drained ()
                    Expect.equal node.Value 99 "fresh flight settles Ready 99"

                // 4. Drop discards unequal K2 (pulled and unpulled) and keeps lastKey = K1.
                let sDrop = Signal (g, 1)
                let dropKeys = ResizeArray<int>()
                let dropFlights = ResizeArray<Flight<int>>()

                let dropped =
                    g.Run (fun () ->
                        createAsyncOnWithPolicy Drop (fun () -> sDrop.Value % 10) (fun k _ _ ->
                            dropKeys.Add k
                            let f = Flight<int>()
                            dropFlights.Add f
                            f.Task))

                Expect.equal dropped.TryValue Pending "drop flight 0 for K1=1"
                sDrop.Value <- 2
                Expect.equal dropped.TryValue Pending "pulled K2=2 discarded during flight"
                sDrop.Value <- 3
                dropFlights[0].Settle 10
                do! drained ()

                Expect.equal dropped.Value 10 "Drop keeps K1's settled value 10 and discards unpulled K3=3"
                Expect.sequenceEqual dropKeys [ 1 ] "only 1 flight ran"

                // Returning to equal key 11 % 10 = 1 matches retained lastKey = 1 without launching; unequal key 4 launches.
                sDrop.Value <- 11
                Expect.equal dropped.Value 10 "lastKey remained K1=1"
                Expect.sequenceEqual dropKeys [ 1 ] "still 1 flight"

                sDrop.Value <- 4
                Expect.equal dropped.TryValue Pending "unequal key 4 after settle launches flight"
                Expect.sequenceEqual dropKeys [ 1; 4 ] "flight launched for 4"
                dropFlights[1].Settle 40
                do! drained ()
                Expect.equal dropped.Value 40 "settled 40"
            }

            testCaseAsync "Queue chains unequal keys in FIFO order and treats Phase 1 Pending or Failed as overlay without appending FIFO faults"
            <| async {
                use g = new Graph ()
                let s = Signal (g, 1)
                let gate = Signal (g, 0)
                let pending = AsyncSource<int>(g)

                let upstreamFail =
                    Make.Memo (
                        g,
                        fun _ ->
                            if gate.Value = 2 then
                                failwith "upstream-queue-err"

                            0
                    )

                let flights = ResizeArray<Flight<int>>()
                let prevs = ResizeArray<Task<int voption>>()
                let keys = ResizeArray<int>()

                let q =
                    g.Run (fun () ->
                        createOwningAsyncOnWithPolicy
                            FlightPolicy.Queue
                            (fun () ->
                                if gate.Value = 1 then
                                    pending.Value |> ignore

                                upstreamFail.Value |> ignore
                                s.Value % 10)
                            (fun k prev _ ->
                                keys.Add k
                                prevs.Add prev.Settled
                                let f = Flight<int>()
                                flights.Add f
                                f.Task))

                Expect.equal q.TryValue Pending "flight 0 queued for key 1"
                s.Value <- 2
                Expect.equal q.TryValue Pending "flight 1 queued for key 2"
                s.Value <- 12
                Expect.equal q.TryValue Pending "equal key 12 % 10 = 2 adds no flight"
                Expect.sequenceEqual keys [ 1; 2 ] "two FIFO flights active"

                // Transient Phase 1 Pending and Failed set overlay WITHOUT appending a fault slot to Queue's FIFO chain.
                gate.Value <- 1
                Expect.equal q.TryValue Pending "Phase 1 Pending overlay"
                Expect.isTrue (obj.ReferenceEquals (Seq.head q.PendingSources, pending :> INode)) "PendingSources names pending"

                gate.Value <- 2
                let p1Err = failedWith q.TryValue
                Expect.equal p1Err.Message "upstream-queue-err" "Phase 1 Error overlay surfaces immediately"
                Expect.isTrue (obj.ReferenceEquals (q.ErrorOrigin, upstreamFail :> INode)) "ErrorOrigin is upstreamFail"

                gate.Value <- 0
                Expect.equal q.TryValue Pending "recovering to equal key 2 clears overlay without adding a flight"
                Expect.sequenceEqual keys [ 1; 2 ] "still two flights"

                // Out-of-order completion: flight 1 finishes before flight 0, holds Pending until flight 0 settles, then drains in FIFO start order.
                flights[1].Settle 20
                do! drained ()
                Expect.equal q.TryValue Pending "flight 1 waits behind unsettled flight 0"

                flights[0].Settle 10
                do! drained ()
                Expect.equal q.Value 20 "both queued flights applied in FIFO start order without any spurious fault slot"
                let! prev0 = Async.AwaitTask prevs[0]
                let! prev1 = Async.AwaitTask prevs[1]
                Expect.equal prev0 ValueNone "flight 0 saw ValueNone"
                Expect.equal prev1 (ValueSome 10) "flight 1 waiter received flight 0 settled value"
            }

            testCaseAsync "background flight settle or fault while Phase 1 is Pending or Failed stores into Phase 2 slot and surfaces on recovery"
            <| async {
                use g = new Graph ()

                // Case 1: Background flight completes with 42 while Phase 1 is Pending (and Failed), then Phase 1 settles back to K1.
                let key1 = Signal (g, 1)
                let gate1 = Signal (g, 0)
                let pending1 = AsyncSource<int>(g)
                let flights1 = ResizeArray<Flight<int>>()
                let seenReady = ResizeArray<int>()

                let a1 =
                    g.Run (fun () ->
                        createAsyncOn
                            (fun () ->
                                let m = gate1.Value

                                if m = 1 then
                                    pending1.Value |> ignore
                                elif m = 2 then
                                    failwith "p1-fail"

                                key1.Value % 10)
                            (fun _ _ _ ->
                                let f = Flight<int>()
                                flights1.Add f
                                f.Task))

                use _obs1 =
                    new Effect (
                        g,
                        fun () ->
                            match a1.TryValue with
                            | Ready v -> seenReady.Add v
                            | _ -> ()
                    )

                Expect.equal flights1.Count 1 "flight 0 started for key 1"
                gate1.Value <- 1
                Expect.equal (a1.Status &&& Status.Pending) Status.Pending "Phase 1 enters Pending"

                // Background flight settles 42 while Phase 1 is Pending!
                flights1[0].Settle 42
                do! drained ()

                Expect.equal a1.Status Status.Pending "Status stays Pending while Phase 1 is Pending"
                Expect.equal a1.TryValue Pending "TryValue stays Pending"
                Expect.isEmpty seenReady "observer has not seen Ready 42 while Phase 1 is Pending"

                // Switch Phase 1 to Failed -> surfaces Phase 1 failure while still holding 42 in Phase 2 slot.
                gate1.Value <- 2
                Expect.equal (failedWith a1.TryValue).Message "p1-fail" "Phase 1 Error overlay"
                Expect.isEmpty seenReady "observer still has not seen Ready 42"

                // Recover Phase 1 back to equal key 11 % 10 = 1 -> Ready 42 surfaces immediately with 1 total flight!
                key1.Value <- 11
                gate1.Value <- 0
                Expect.equal a1.Status Status.None "Status becomes None on Phase 1 recovery"
                Expect.equal a1.Value 42 "Value is 42"
                Expect.sequenceEqual seenReady [ 42 ] "downstream observer woken with 42"
                Expect.equal flights1.Count 1 "1 total flight launched"

                // Case 2: Background flight FAULTS while Phase 1 is Pending -> must NOT be dropped by FlightFailureDrop!
                let key2 = Signal (g, 1)
                let gate2 = Signal (g, 0)
                let pending2 = AsyncSource<int>(g)

                let upstreamErrNode =
                    Make.Memo (
                        g,
                        fun _ ->
                            if gate2.Value = 2 then
                                failwith "p1-origin"

                            0
                    )

                let flights2 = ResizeArray<Flight<int>>()

                let a2 =
                    g.Run (fun () ->
                        createAsyncOn
                            (fun () ->
                                if gate2.Value = 1 then
                                    pending2.Value |> ignore

                                upstreamErrNode.Value |> ignore
                                key2.Value % 10)
                            (fun _ _ _ ->
                                let f = Flight<int>()
                                flights2.Add f
                                f.Task))

                Expect.equal a2.TryValue Pending "a2 flight 0 started"
                gate2.Value <- 1
                Expect.equal a2.TryValue Pending "a2 Phase 1 Pending"

                let flightBoom = InvalidOperationException "flight-boom"
                flights2[0].Fail flightBoom
                do! drained ()

                // Still masked by Phase 1 Pending overlay:
                Expect.equal a2.Status Status.Pending "masked by Phase 1 Pending"
                Expect.equal a2.TryValue Pending "TryValue still Pending"
                Expect.isTrue (obj.ReferenceEquals (Seq.head a2.PendingSources, pending2 :> INode)) "PendingSources still names pending2"

                // Masked by Phase 1 Error overlay (preserving upstream ErrorOrigin):
                gate2.Value <- 2
                Expect.equal (failedWith a2.TryValue).Message "p1-origin" "Phase 1 Error masks Phase 2 fault"
                Expect.isTrue (obj.ReferenceEquals (a2.ErrorOrigin, upstreamErrNode :> INode)) "ErrorOrigin is upstreamErrNode"

                // Recover Phase 1 back to equal key 1 -> Phase 2's stored fault surfaces with ErrorOrigin = a2!
                gate2.Value <- 0
                let surfaced = failedWith a2.TryValue
                Expect.isTrue (obj.ReferenceEquals (surfaced, flightBoom)) "stored Phase 2 fault surfaced (not dropped)"
                Expect.isTrue (obj.ReferenceEquals (a2.ErrorOrigin, a2 :> INode)) "ErrorOrigin is a2"
                Expect.equal flights2.Count 1 "still 1 total flight launched"
            }

            testCaseAsync
                "createAsyncWithOn enforces Phase 1 Pure, preserves scope on equal or unready keys, re-reads on cleanup write, and stops on self-dispose"
            <| async {
                use g = new Graph ()
                use _ = g.Activate ()
                let expectedPureMsg = ScopeMessages.forMode ScopeMode.Pure

                // 1. Phase 1 enforces ScopeMode.Pure even on createAsyncWithOn and after Phase 2 owns a scope.
                let step = createSignal 0

                let mPureCheck =
                    createAsyncWithOn
                        (fun () ->
                            let k = step.Value

                            if k = 1 then
                                try
                                    // Asserts the runtime failure of an owned node created in a pure derivation.
                                    // fsharpanalyzer: ignore-line-next RAN010
                                    createEffect ignore
                                with _ ->
                                    ()

                            k)
                        (fun k _ _ ->
                            onCleanup ignore
                            completed (k * 10))

                mPureCheck.TryValue |> ignore
                do! drained ()
                Expect.equal mPureCheck.Value 0 "run 1 allocated Phase 2 scope"

                step.Value <- 1
                let pureEx = failedWith mPureCheck.TryValue
                Expect.isTrue (isInvalidOperation pureEx) "InvalidOperationException on Phase 1 scope creation"
                Expect.equal pureEx.Message expectedPureMsg "Phase 1 enforces ScopeMode.Pure message"

                // Pure createAsyncOn also rejects scope creation inside compute.
                let mPureCompute =
                    createAsyncOn (fun () -> 1) (fun v _ _ ->
                        // Asserts the runtime failure of an owned node created in a pure derivation.
                        // fsharpanalyzer: ignore-line-next RAN010
                        onCleanup ignore
                        completed v)

                let computePureEx = failedWith mPureCompute.TryValue
                Expect.isTrue (isInvalidOperation computePureEx) "createAsyncOn rejects onCleanup in compute"

                // 2. Re-entrant cleanup write inside createAsyncWithOn re-evaluates `read ()` before launching stale key!
                let sRe = createSignal 1
                let launchedKeys = ResizeArray<int>()
                let cleanLog = ResizeArray<int>()

                let aRe =
                    createAsyncWithOn (fun () -> sRe.Value) (fun k _ _ ->
                        launchedKeys.Add k

                        onCleanup (fun () ->
                            cleanLog.Add k

                            if k = 1 then
                                sRe.Value <- 10)

                        completed (k * 100))

                aRe.TryValue |> ignore
                do! drained ()
                Expect.equal aRe.Value 100 "initial flight for 1"

                // Writing 2 triggers discharge of key 1's scope, whose cleanup writes 10; key 2 must NEVER launch!
                sRe.Value <- 2
                aRe.TryValue |> ignore
                do! drained ()
                Expect.sequenceEqual cleanLog [ 1 ] "cleanup for 1 ran once"
                Expect.sequenceEqual launchedKeys [ 1; 10 ] "re-evaluated read () and skipped stale key 2"
                Expect.equal aRe.Value 1000 "settled key 10"

                // 3. Cleanup self-disposing root stops before launching next compute and preserves previous status.
                let sDisp = createSignal 1
                let dispGate = createSignal false
                let dispLog = ResizeArray<string>()
                let mutable root = Unchecked.defaultof<Owner>

                let aDisp =
                    createRoot (fun owner ->
                        root <- owner

                        createAsyncWithOn
                            (fun () ->
                                if dispGate.Value then
                                    failwith "prior-phase1-error"

                                sDisp.Value)
                            (fun k _ _ ->
                                dispLog.Add $"launch:{k}"

                                onCleanup (fun () ->
                                    dispLog.Add $"cleanup:{k}"
                                    root.Dispose ())

                                completed (k * 10)))

                aDisp.TryValue |> ignore
                do! drained ()
                Expect.equal aDisp.Value 10 "initial settled Ready 10"

                // Put aDisp into Phase 1 Error overlay, then transition to unequal key 2 -> cleanup disposes root!
                dispGate.Value <- true
                Expect.equal (failedWith aDisp.TryValue).Message "prior-phase1-error" "in Phase 1 Error overlay"

                dispGate.Value <- false
                sDisp.Value <- 2
                let afterDispose = failedWith aDisp.TryValue
                Expect.equal afterDispose.Message "prior-phase1-error" "previous Error status preserved across cleanup self-disposal"
                Expect.sequenceEqual dispLog [ "launch:1"; "cleanup:1" ] "stopped before launch:2"
            }

            test "synchronous NotReadyException inside compute wraps into InvalidOperationException and throwing keyEqual preserves flight" {
                use g =
                    new Graph (
                        { GraphOptions.Default with
                            Equality = StructuralPolicy ()
                        }
                    )

                use _ = g.Activate ()
                let s = createSignal 1
                let pending = createAsyncSource<int>()

                let a =
                    createAsyncOn (fun () -> s.Value) (fun x _ _ ->
                        let y = pending.Value
                        completed (x + y))

                let ex = failedWith a.TryValue
                Expect.equal a.Status Status.Error "fails with Status.Error rather than stranding in Pending"
                Expect.isTrue (obj.ReferenceEquals (a.ErrorOrigin, a :> INode)) "ErrorOrigin is the AsyncMemo itself"
                Expect.isTrue (isInvalidOperation ex) "InvalidOperationException"
                Expect.isNonEmpty ex.Message "carries descriptive message"

                let isNotReadyInner =
                    match ex.InnerException with
                    | NotReadyException src -> obj.ReferenceEquals (src, pending :> INode)
                    | _ -> false

                Expect.isTrue isNotReadyInner "InnerException preserves NotReadyException and source"

                // Throwing keyEqual surfaces Phase 1 Error overlay without cancelling active flight.
                let sKey = createSignal 1
                let armThrow = createSignal false
                let tokens = ResizeArray<CancellationToken>()
                let flights = ResizeArray<Flight<int>>()

                let aKey =
                    createAsyncOn (fun () -> GuardedKey (sKey.Value % 10, armThrow.Value)) (fun _ _ token ->
                        tokens.Add token
                        let f = Flight<int>()
                        flights.Add f
                        f.Task)

                Expect.equal aKey.TryValue Pending "initial flight running"
                armThrow.Value <- true
                sKey.Value <- 2
                let cmpEx = failedWith aKey.TryValue
                Expect.equal cmpEx.Message "key comparer" "keyEqual exception surfaced"
                Expect.isFalse tokens[0].IsCancellationRequested "active flight NOT cancelled by keyEqual throw"
                Expect.equal flights.Count 1 "no new flight launched"
            }

            testCaseAsync
                "task-wrapped NotReadyException in compute wraps into InvalidOperationException for downstream readers, and caught Phase 1 error rethrown in Phase 2 sets ErrorOrigin to self"
            <| async {
                use g = new Graph ()
                let s = Signal (g, 1)
                let pending = AsyncSource<int> g

                let a =
                    g.Run (fun () ->
                        createAsyncOn (fun () -> s.Value) (fun x _ _ ->
                            task {
                                let y = pending.Value
                                return x + y
                            }))

                let downstream = g.Run (fun () -> createMemo (fun _ -> a.Value + 10))
                downstream.TryValue |> ignore
                do! drained ()

                let exA = failedWith a.TryValue
                let exDown = failedWith downstream.TryValue
                Expect.equal a.Status Status.Error "AsyncMemo enters Status.Error"
                Expect.equal downstream.Status Status.Error "downstream Memo enters Status.Error rather than stranding in Pending"
                Expect.isTrue (isInvalidOperation exA) "wrapped into InvalidOperationException"
                Expect.equal exA.Message UntrackedPhase.asyncOnPending "carries asyncOnPending message"
                Expect.isTrue (obj.ReferenceEquals (exDown, exA)) "downstream sees wrapped InvalidOperationException"
                Expect.isTrue (obj.ReferenceEquals (a.ErrorOrigin, a :> INode)) "a.ErrorOrigin is a"
                Expect.isTrue (obj.ReferenceEquals (downstream.ErrorOrigin, a :> INode)) "downstream.ErrorOrigin is a"

                let hasPendingInner =
                    match exA.InnerException with
                    | NotReadyException src -> obj.ReferenceEquals (src, pending :> INode)
                    | _ -> false

                Expect.isTrue hasPendingInner "InnerException preserves NotReadyException(pending)"

                // Caught failed read in Phase 1 rethrown in Phase 2 attributes ErrorOrigin to self, not failedNode.
                let failedNode = Make.Memo (g, fun _ -> failwith "upstream-failed": int)
                let mutable caught: exn = null

                let aSync =
                    g.Run (fun () ->
                        createAsyncOn
                            (fun () ->
                                try
                                    failedNode.Value
                                with ex ->
                                    caught <- ex
                                    1)
                            (fun _ _ _ -> raise caught))

                let exSync = failedWith aSync.TryValue
                Expect.equal exSync.Message "upstream-failed" "rethrown message preserved"
                Expect.isTrue (obj.ReferenceEquals (aSync.ErrorOrigin, aSync :> INode)) "synchronous Phase 2 rethrow sets ErrorOrigin to self"

                let aTask =
                    g.Run (fun () ->
                        createAsyncOn
                            (fun () ->
                                try
                                    failedNode.Value
                                with ex ->
                                    caught <- ex
                                    2)
                            (fun _ _ _ -> task { return raise caught }))

                aTask.TryValue |> ignore
                do! drained ()
                let exTask = failedWith aTask.TryValue
                Expect.equal exTask.Message "upstream-failed" "task Phase 2 rethrow message preserved"
                Expect.isTrue (obj.ReferenceEquals (aTask.ErrorOrigin, aTask :> INode)) "task Phase 2 rethrow sets ErrorOrigin to self"
            }

#if RANVIER_TRACE
            testCaseAsync "Trace.reconcile holds across createAsyncOn and createAsyncWithOn lifecycles"
            <| async {
                use g = new Graph ()
                let items = Signal (g, [ 1; 2 ])
                let tick = Signal (g, 0)
                let gate = Signal (g, false)
                let pending = AsyncSource<int> g
                let sink = ResizeArray<string>()
                let flights = ResizeArray<Flight<int>>()

                let fetched =
                    g.Run (fun () ->
                        createAsyncOn
                            (fun () ->
                                if gate.Value then
                                    pending.Value |> ignore

                                List.length items.Value)
                            (fun n _ _ ->
                                let f = Flight<int>()
                                flights.Add f
                                f.Task |> ignore
                                f.Task))

                let owning =
                    g.Run (fun () ->
                        createAsyncWithOn (fun () -> fetched.Value) (fun v _ _ ->
                            createEffect (fun () -> sink.Add $"{v}:{tick.Value}")
                            onCleanup (fun () -> sink.Add $"clean:{v}")
                            completed (v * 10)))

                Expect.equal (g.Run (fun () -> owning.TryValue)) Pending "initial pending"
                flights[0].Settle 2
                do! drained ()
                g.Run (fun () -> owning.TryValue) |> ignore
                do! drained ()
                Expect.equal (g.Run (fun () -> owning.Value)) 20 "settled 20"

                items.Value <- [ 3; 4 ]
                Expect.equal (g.Run (fun () -> owning.Value)) 20 "equal key cutoff"
                tick.Value <- 1

                gate.Value <- true
                Expect.equal (g.Run (fun () -> owning.TryValue)) Pending "transient pending"
                gate.Value <- false
                Expect.equal (g.Run (fun () -> owning.Value)) 20 "recovered without restart"

                items.Value <- [ 5; 6; 7 ]
                Expect.equal (g.Run (fun () -> owning.TryValue)) Pending "unequal key starts flight 2"
                flights[1].Settle 3
                do! drained ()
                g.Run (fun () -> owning.TryValue) |> ignore
                do! drained ()
                Expect.equal (g.Run (fun () -> owning.Value)) 30 "settled 30"

                owning.Dispose ()
                fetched.Dispose ()

                Expect.isEmpty (Trace.reconcile g) "live edges and scopes match folded trace log"
            }
#endif
        ]
