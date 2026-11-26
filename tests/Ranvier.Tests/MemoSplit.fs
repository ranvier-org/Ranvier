module Ranvier.Tests.MemoSplit

open System
open System.Collections.Generic
open System.Threading.Tasks
open Expecto
open Ranvier

/// <summary>
/// A flight the test settles by hand, on the test's own thread.
/// </summary>
type private Flight<'T>() =
    let source = TaskCompletionSource<'T>()
    member _.Task = source.Task

    member _.Settle(v: 'T) =
        source.SetResult v

    member _.Fail(e: exn) =
        source.SetException e

let private comparer (equals: 'T -> 'T -> bool) =
    { new IEqualityComparer<'T> with
        member _.Equals(a, b) =
            equals a b

        member _.GetHashCode _ = 0
    }

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
/// Closed-window two-phase semantics of <c>createMemoOn</c>, <c>createMemoOnWithComparer</c>,
/// <c>createMemoWithOn</c>, and <c>createOwningMemoOnWithComparer</c>.
/// </summary>
[<Tests>]
let tests =
    testList
        "MemoSplit"
        [
            test "createMemoOn cuts off on equal Phase 1 key and closes tracking in Phase 2" {
                use g = new Graph ()
                use _ = g.Activate ()
                let items = createSignal [ 1; 2 ]
                let theme = createSignal "light"
                let computeRuns = ref 0

                let summary =
                    createMemoOn (fun () -> List.length items.Value) (fun len ->
                        computeRuns.Value <- computeRuns.Value + 1
                        $"{len}:{theme.Value}")

                Expect.equal summary.Value "2:light" "initial compute"
                Expect.equal computeRuns.Value 1 "compute ran once"

                // Phase 2 read of theme was untracked.
                theme.Value <- "dark"
                Expect.equal summary.Value "2:light" "theme change does not invalidate memo"
                Expect.equal computeRuns.Value 1 "compute still ran once"
                Expect.equal theme.ObserverCount 0 "theme has zero observers"

                // Equal Phase 1 key (length 2 -> length 2) cuts off before Phase 2.
                items.Value <- [ 3; 4 ]
                Expect.equal summary.Value "2:light" "equal length keeps previous Phase 2 output"
                Expect.equal computeRuns.Value 1 "compute skipped on equal key"

                // Unequal Phase 1 key (length 2 -> length 3) runs Phase 2 and picks up latest theme.
                items.Value <- [ 5; 6; 7 ]
                Expect.equal summary.Value "3:dark" "unequal length runs compute with latest untracked state"
                Expect.equal computeRuns.Value 2 "compute ran twice"
            }

            test "createMemoOnWithComparer and createOwningMemoOnWithComparer cut off downstream on equal output" {
                use g = new Graph ()
                use _ = g.Activate ()
                let parity = comparer (fun (a: int) b -> a % 2 = b % 2)
                let s = createSignal 1
                let pureComputeRuns = ref 0
                let owningComputeRuns = ref 0
                let cleanups = ResizeArray<int>()
                let pureSeen = ResizeArray<int>()
                let owningSeen = ResizeArray<int>()

                let pureMemo =
                    createMemoOnWithComparer parity (fun () -> s.Value) (fun v ->
                        pureComputeRuns.Value <- pureComputeRuns.Value + 1
                        v)

                let owningMemo =
                    createOwningMemoOnWithComparer parity (fun () -> s.Value) (fun v ->
                        owningComputeRuns.Value <- owningComputeRuns.Value + 1
                        onCleanup (fun () -> cleanups.Add v)
                        v)

                createEffect (fun () -> pureSeen.Add pureMemo.Value)
                createEffect (fun () -> owningSeen.Add owningMemo.Value)

                Expect.sequenceEqual pureSeen [ 1 ] "initial pure downstream"
                Expect.sequenceEqual owningSeen [ 1 ] "initial owning downstream"

                // Unequal key (1 -> 3) runs Phase 2 (discharging owning scope), but parity output cutoff suppresses downstream.
                s.Value <- 3
                Expect.equal pureComputeRuns.Value 2 "pure compute ran for unequal key"
                Expect.equal owningComputeRuns.Value 2 "owning compute ran for unequal key"
                Expect.sequenceEqual cleanups [ 1 ] "owning scope discharged on unequal key"
                Expect.sequenceEqual pureSeen [ 1 ] "downstream suppressed by custom comparer"
                Expect.sequenceEqual owningSeen [ 1 ] "owning downstream suppressed by custom comparer"

                // Unequal parity (3 -> 4) wakes downstream.
                s.Value <- 4
                Expect.sequenceEqual pureSeen [ 1; 4 ] "downstream woken on unequal custom output"
                Expect.sequenceEqual owningSeen [ 1; 4 ] "owning downstream woken on unequal custom output"
                Expect.sequenceEqual cleanups [ 1; 3 ] "second owning scope discharged"

                owningMemo.Dispose ()
                Expect.sequenceEqual cleanups [ 1; 3; 4 ] "disposal cleans active Phase 2 scope"
            }

            test "Phase 1 enforces ScopeMode.Pure even in createMemoWithOn and after Phase 2 owns a scope" {
                use g = new Graph ()
                use _ = g.Activate ()
                let expectedMessage = ScopeMessages.forMode ScopeMode.Pure

                // 1. onCleanup in read of createMemoWithOn fails with Pure message.
                let mCleanup =
                    createMemoWithOn
                        (fun () ->
                            // Asserts the runtime failure of an owned node created in a pure derivation.
                            // fsharpanalyzer: ignore-line-next RAN010
                            onCleanup ignore
                            1)
                        id

                let ex1 = failedWith mCleanup.TryValue
                Expect.isTrue (isInvalidOperation ex1) "InvalidOperationException"
                Expect.equal ex1.Message expectedMessage "Pure scope violation message on onCleanup"

                // 2. createEffect in read (even if caught inside read, and even on run 2 after Phase 2 allocated ScopeField).
                let step = createSignal 0

                let mAfterScope =
                    createMemoWithOn
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
                        (fun k ->
                            onCleanup ignore
                            k * 10)

                Expect.equal mAfterScope.Value 0 "run 1 populates ScopeField in Phase 2"
                step.Value <- 1
                let ex2 = failedWith mAfterScope.TryValue
                Expect.isTrue (isInvalidOperation ex2) "InvalidOperationException on run 2"
                Expect.equal ex2.Message expectedMessage "Phase 1 still enforces ScopeMode.Pure when ScopeField is non-null"

                // 3. Pure createMemoOn also enforces ScopeMode.Pure in Phase 2 compute.
                let mPurePhase2 =
                    createMemoOn (fun () -> 1) (fun v ->
                        // Asserts the runtime failure of an owned node created in a pure derivation.
                        // fsharpanalyzer: ignore-line-next RAN010
                        onCleanup ignore
                        v)

                let ex3 = failedWith mPurePhase2.TryValue
                Expect.isTrue (isInvalidOperation ex3) "Phase 2 in createMemoOn is also Pure"
                Expect.equal ex3.Message expectedMessage "Phase 2 Pure violation message"
            }

            testCaseAsync "createMemoWithOn preserves Phase 2 scope and value across transient Pending and Failed Phase 1 excursions"
            <| async {
                use g = new Graph ()
                use _ = g.Activate ()
                let key = createSignal 1
                let tick = createSignal 0
                let flights = Dictionary<int, Flight<string>>()

                let user =
                    Make.AsyncMemo<string>(
                        g,
                        fun _ _ ->
                            let f = Flight<string>()
                            flights[key.Value] <- f
                            f.Task
                    )

                let computeRuns = ref 0
                let innerLog = ResizeArray<string>()

                let formatted =
                    createMemoWithOn (fun () -> user.Value) (fun name ->
                        computeRuns.Value <- computeRuns.Value + 1
                        createEffect (fun () -> innerLog.Add $"effect:{name}:{tick.Value}")
                        onCleanup (fun () -> innerLog.Add $"cleanup:{name}")
                        $"user:{name}")

                // Initial pull starts flight 1; memo is Pending.
                Expect.equal formatted.TryValue Pending "initial pending"
                Expect.equal computeRuns.Value 0 "Phase 2 has not run while Phase 1 is pending"

                flights[1].Settle "ada"
                do! drained ()

                Expect.equal formatted.Value "user:ada" "settled ada"
                Expect.equal computeRuns.Value 1 "compute ran once for ada"
                Expect.sequenceEqual innerLog [ "effect:ada:0" ] "inner effect started"

                // Re-flight 2 -> transient Pending, then settles back to equal "ada".
                key.Value <- 2
                Expect.equal formatted.TryValue Pending "Phase 1 re-flight surfaces Pending"
                Expect.equal (formatted.Status &&& Status.Pending) Status.Pending "Status is Pending during re-flight"
                tick.Value <- 1
                Expect.sequenceEqual innerLog [ "effect:ada:0"; "effect:ada:1" ] "inner scope stays live while Phase 1 is Pending"

                flights[2].Settle "ada"
                do! drained ()

                Expect.equal formatted.Value "user:ada" "restored user:ada after Pending excursion"
                Expect.equal computeRuns.Value 1 "compute did not re-run when settling back to ada"
                Expect.sequenceEqual innerLog [ "effect:ada:0"; "effect:ada:1" ] "inner scope was not discharged"

                // Re-flight 3 -> transient Failed, then re-flight 4 recovering to "ada".
                key.Value <- 3
                Expect.equal formatted.TryValue Pending "flight 3 pending"
                let networkErr = InvalidOperationException "offline"
                flights[3].Fail networkErr
                do! drained ()

                let seenErr = failedWith formatted.TryValue
                Expect.isTrue (obj.ReferenceEquals (seenErr, networkErr)) "Phase 1 failure surfaces"
                Expect.isTrue (obj.ReferenceEquals (formatted.ErrorOrigin, user)) "upstream ErrorOrigin preserved"
                tick.Value <- 2
                Expect.sequenceEqual innerLog [ "effect:ada:0"; "effect:ada:1"; "effect:ada:2" ] "inner scope stays live while Phase 1 is Failed"

                key.Value <- 4
                Expect.equal formatted.TryValue Pending "flight 4 pending"
                flights[4].Settle "ada"
                do! drained ()

                Expect.equal formatted.Value "user:ada" "restored user:ada after Failed excursion"
                Expect.equal computeRuns.Value 1 "compute still ran only once"

                // Re-flight 5 -> settles unequal "grace", discharging ada's scope and running compute a second time.
                key.Value <- 5
                Expect.equal formatted.TryValue Pending "flight 5 pending"
                flights[5].Settle "grace"
                do! drained ()

                Expect.equal formatted.Value "user:grace" "updated to grace"
                Expect.equal computeRuns.Value 2 "compute ran second time on unequal key"

                Expect.sequenceEqual
                    innerLog
                    [
                        "effect:ada:0"
                        "effect:ada:1"
                        "effect:ada:2"
                        "cleanup:ada"
                        "effect:grace:2"
                    ]
                    "ada scope discharged before grace compute"
            }

            test "Phase 2 untracked NotReadyException wraps into InvalidOperationException originating at the memo" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 1
                let pending = createAsyncSource<int>()

                let m = createMemoOn (fun () -> s.Value) (fun x -> x + pending.Value)

                let ex = failedWith m.TryValue
                Expect.equal m.Status Status.Error "memo fails rather than stranding in Pending"
                Expect.isTrue (obj.ReferenceEquals (m.ErrorOrigin, m)) "ErrorOrigin is the memo itself"
                Expect.isTrue (isInvalidOperation ex) "wrapped in InvalidOperationException"

                let isNotReadyInner =
                    match ex.InnerException with
                    | NotReadyException src -> obj.ReferenceEquals (src, pending)
                    | _ -> false

                Expect.isTrue isNotReadyInner "InnerException preserves the NotReadyException and source"
            }

            test "createMemoWithOn cleanup mutating a Phase 1 source schedules a clean follow-up refresh" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 1
                let computed = ResizeArray<int>()
                let seen = ResizeArray<int>()

                let m =
                    createMemoWithOn (fun () -> s.Value) (fun v ->
                        computed.Add v

                        onCleanup (fun () ->
                            if v = 2 then
                                s.Value <- 10)

                        v * 10)

                createEffect (fun () -> seen.Add m.Value)

                s.Value <- 2
                s.Value <- 3

                Expect.sequenceEqual computed [ 1; 2; 3; 10 ] "cleanup write runs follow-up compute for 10"
                Expect.equal m.Value 100 "memo settles on cleanup's written value"
                Expect.equal (Seq.last seen) 100 "downstream effect converges to cleanup's written value"
            }

            test "createMemoWithOn cleanup disposing its enclosing root stops before running the next compute" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 1
                let log = ResizeArray<string>()
                let mutable root = Unchecked.defaultof<Owner>

                let m =
                    createRoot (fun owner ->
                        root <- owner

                        createMemoWithOn (fun () -> s.Value) (fun v ->
                            log.Add $"compute:{v}"

                            onCleanup (fun () ->
                                log.Add $"cleanup:{v}"
                                root.Dispose ())

                            v))

                Expect.equal m.Value 1 "initial run"
                s.Value <- 2
                m.TryValue |> ignore
                s.Value <- 3
                m.TryValue |> ignore

                Expect.sequenceEqual log [ "compute:1"; "cleanup:1" ] "self-disposal during cleanup stops before compute:2"
            }

            test "Phase 2 throw counts as keyed on 'T across equal keys and transient Phase 1 Pending or Error" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 1
                let gate = createSignal 0
                let pending = createAsyncSource<int>()
                let computeLog = ResizeArray<int>()

                let m =
                    createMemoOn
                        (fun () ->
                            let mode = gate.Value

                            if mode = 1 then
                                pending.Value |> ignore
                            elif mode = 2 then
                                failwith "phase1"

                            s.Value % 10)
                        (fun k ->
                            computeLog.Add k

                            if k = 2 then
                                failwith "boom"

                            k * 100)

                Expect.equal m.Value 100 "initial Ready for key 1"
                Expect.sequenceEqual computeLog [ 1 ] "compute ran for 1"

                // Key 2 throws in Phase 2.
                s.Value <- 2
                let boomEx = failedWith m.TryValue
                Expect.equal boomEx.Message "boom" "Phase 2 failure surfaced"
                Expect.isTrue (obj.ReferenceEquals (m.ErrorOrigin, m)) "ErrorOrigin is memo"
                Expect.sequenceEqual computeLog [ 1; 2 ] "compute ran once for key 2"

                // Writing s <- 12 produces equal key 2: keeps Status.Error without re-running compute.
                s.Value <- 12
                let boomEx2 = failedWith m.TryValue
                Expect.isTrue (obj.ReferenceEquals (boomEx2, boomEx)) "same Phase 2 exception preserved on equal key"
                Expect.isTrue (obj.ReferenceEquals (m.ErrorOrigin, m)) "ErrorOrigin remains memo"
                Expect.sequenceEqual computeLog [ 1; 2 ] "compute did not re-run for 12"

                // Transient Phase 1 Pending -> back to 12 keeps Phase 2 failure without re-running compute.
                gate.Value <- 1
                Expect.equal m.TryValue Pending "transient Phase 1 Pending"
                gate.Value <- 0
                let boomEx3 = failedWith m.TryValue
                Expect.isTrue (obj.ReferenceEquals (boomEx3, boomEx)) "Phase 2 failure restored after Pending"
                Expect.sequenceEqual computeLog [ 1; 2 ] "compute still did not re-run"

                // Transient Phase 1 Error -> back to 12 keeps Phase 2 failure without re-running compute.
                gate.Value <- 2
                let p1Ex = failedWith m.TryValue
                Expect.equal p1Ex.Message "phase1" "transient Phase 1 Error"
                gate.Value <- 0
                let boomEx4 = failedWith m.TryValue
                Expect.isTrue (obj.ReferenceEquals (boomEx4, boomEx)) "Phase 2 failure restored after Phase 1 Error"
                Expect.sequenceEqual computeLog [ 1; 2 ] "compute still did not re-run"

                // Unequal key 3 runs compute and recovers.
                s.Value <- 3
                Expect.equal m.Value 300 "recovered on key 3"
                Expect.sequenceEqual computeLog [ 1; 2; 3 ] "compute ran for key 3"
            }

            test "createMemoWithOn cleanup self-disposal after Pending restores Status.Pending and PendingSources" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 1
                let gate = createSignal false
                let pending = createAsyncSource<int>()
                let log = ResizeArray<string>()
                let mutable root = Unchecked.defaultof<Owner>

                let m =
                    createRoot (fun owner ->
                        root <- owner

                        createMemoWithOn
                            (fun () ->
                                if gate.Value then
                                    pending.Value |> ignore

                                s.Value)
                            (fun v ->
                                log.Add $"compute:{v}"

                                onCleanup (fun () ->
                                    log.Add $"cleanup:{v}"
                                    root.Dispose ())

                                v * 10))

                Expect.equal m.Value 10 "initial Ready state"

                // Enter transient Phase 1 Pending while Phase 2 scope remains active.
                gate.Value <- true
                Expect.equal m.TryValue Pending "Phase 1 is Pending"
                Expect.equal m.Status Status.Pending "Status is Pending before cleanup self-disposal"
                Expect.equal (Seq.length m.PendingSources) 1 "one pending source recorded"
                Expect.isTrue (obj.ReferenceEquals (Seq.head m.PendingSources, pending :> INode)) "PendingSources names pending"

                // Settle Phase 1 on unequal key 2 -> scope discharge disposes root before compute:2 runs.
                gate.Value <- false
                s.Value <- 2
                Expect.equal m.TryValue Pending "disposed memo retains prior Pending reading"
                Expect.equal m.Status Status.Pending "Status restored to Pending"
                Expect.sequenceEqual log [ "compute:1"; "cleanup:1" ] "stopped before compute:2"
                Expect.equal (Seq.length m.PendingSources) 1 "PendingSources preserved across self-disposal"
                Expect.isTrue (obj.ReferenceEquals (Seq.head m.PendingSources, pending :> INode)) "PendingSources still names pending"
            }

            test "Memo chain through createMemoWithOn preserves Phase 2 scope on equal key and propagates on unequal key" {
                use g = new Graph ()
                use _ = g.Activate ()

                // 1. Single-observer chain (UnwindChain fast path: Signal -> Memo -> createMemoWithOn -> Memo -> Effect).
                let items = createSignal [ 1; 2 ]
                let tick = createSignal 0
                let computeRuns = ref 0
                let innerLog = ResizeArray<string>()
                let downstreamRuns = ref 0
                let seen = ResizeArray<int>()

                let upstream = createMemo (fun _ -> List.map ((+) 1) items.Value)

                let split =
                    createMemoWithOn (fun () -> List.length upstream.Value) (fun len ->
                        computeRuns.Value <- computeRuns.Value + 1
                        createEffect (fun () -> innerLog.Add $"inner:{len}:{tick.Value}")
                        onCleanup (fun () -> innerLog.Add $"clean:{len}")
                        len * 100)

                let downstream =
                    createMemo (fun _ ->
                        downstreamRuns.Value <- downstreamRuns.Value + 1
                        split.Value + 1)

                createEffect (fun () -> seen.Add downstream.Value)

                Expect.sequenceEqual seen [ 201 ] "initial chain propagation"
                Expect.equal computeRuns.Value 1 "initial Phase 2 run"
                Expect.equal downstreamRuns.Value 1 "initial downstream memo run"
                Expect.sequenceEqual innerLog [ "inner:2:0" ] "inner effect live"

                // Upstream Memo moves ([2; 3] -> [4; 5]), entering UnwindChain on split with equal Phase 1 key (length 2).
                items.Value <- [ 3; 4 ]
                Expect.sequenceEqual seen [ 201 ] "equal key cuts off downstream effect"
                Expect.equal downstream.Value 201 "downstream memo keeps 201"
                Expect.equal computeRuns.Value 1 "Phase 2 compute skipped"
                Expect.equal downstreamRuns.Value 1 "downstream memo not dirtied"
                tick.Value <- 1
                Expect.sequenceEqual innerLog [ "inner:2:0"; "inner:2:1" ] "Phase 2 scope was NOT discharged by UnwindChain"

                // Upstream Memo moves to unequal Phase 1 key ([5; 6; 7] -> length 3).
                items.Value <- [ 5; 6; 7 ]
                Expect.sequenceEqual seen [ 201; 301 ] "unequal key wakes downstream chain"
                Expect.equal computeRuns.Value 2 "Phase 2 compute ran for length 3"
                Expect.equal downstreamRuns.Value 2 "downstream memo recomputed once"
                Expect.sequenceEqual innerLog [ "inner:2:0"; "inner:2:1"; "clean:2"; "inner:3:1" ] "old scope discharged before new compute"

                // 2. Multi-observer upstream Memo (UnwindChainSlow path: upstream has 2 observers).
                let itemsSlow = createSignal [ 10; 20 ]
                let tickSlow = createSignal 0
                let slowLog = ResizeArray<string>()
                let slowSeen = ResizeArray<int>()
                let siblingSeen = ResizeArray<int>()

                let upstreamSlow = createMemo (fun _ -> List.map ((+) 1) itemsSlow.Value)
                createEffect (fun () -> siblingSeen.Add (List.head upstreamSlow.Value))

                let splitSlow =
                    createMemoWithOn (fun () -> List.length upstreamSlow.Value) (fun len ->
                        createEffect (fun () -> slowLog.Add $"slow:{len}:{tickSlow.Value}")
                        onCleanup (fun () -> slowLog.Add $"slowClean:{len}")
                        len * 10)

                let downstreamSlow = createMemo (fun _ -> splitSlow.Value + 5)
                createEffect (fun () -> slowSeen.Add downstreamSlow.Value)

                itemsSlow.Value <- [ 30; 40 ]
                tickSlow.Value <- 1
                Expect.sequenceEqual siblingSeen [ 11; 31 ] "sibling observer saw upstream move"
                Expect.sequenceEqual slowSeen [ 25 ] "equal key suppressed downstream under UnwindChainSlow"
                Expect.sequenceEqual slowLog [ "slow:2:0"; "slow:2:1" ] "Phase 2 scope preserved under UnwindChainSlow"

                itemsSlow.Value <- [ 50; 60; 70 ]
                Expect.sequenceEqual slowSeen [ 25; 35 ] "unequal key propagated under UnwindChainSlow"
                Expect.sequenceEqual slowLog [ "slow:2:0"; "slow:2:1"; "slowClean:2"; "slow:3:1" ] "scope cycled on unequal key"
            }

            test "throwing keyEqual and outputEqual comparers fail the memo, preserve scope on key throw, and latch phase2Failure" {
                // Part 1: Throwing keyEqual (CompareKeyGuarded) under StructuralPolicy.
                use gKey =
                    new Graph (
                        { GraphOptions.Default with
                            Equality = StructuralPolicy ()
                        }
                    )

                gKey.Run (fun () ->
                    let s = createSignal 1
                    let armKeyThrow = createSignal false
                    let tick = createSignal 0
                    let keyComputeRuns = ref 0
                    let keyScopeLog = ResizeArray<string>()

                    let mKey =
                        createMemoWithOn (fun () -> GuardedKey (s.Value % 10, armKeyThrow.Value)) (fun (GuardedKey (k, _)) ->
                            keyComputeRuns.Value <- keyComputeRuns.Value + 1
                            createEffect (fun () -> keyScopeLog.Add $"inner:{k}:{tick.Value}")
                            onCleanup (fun () -> keyScopeLog.Add $"clean:{k}")
                            k * 10)

                    Expect.equal mKey.Value 10 "initial GuardedKey(1) succeeds"
                    Expect.equal keyComputeRuns.Value 1 "compute ran once"
                    Expect.sequenceEqual keyScopeLog [ "inner:1:0" ] "Phase 2 scope created"

                    // Arm key comparer throw -> CompareKeyGuarded records Status.Error, preserves Peek and ScopeField.
                    armKeyThrow.Value <- true
                    s.Value <- 2
                    let keyEx = failedWith mKey.TryValue
                    Expect.equal mKey.Status Status.Error "keyEqual throw puts memo in Status.Error"
                    Expect.equal keyEx.Message "key comparer" "surfaces keyEqual exception"
                    Expect.isTrue (obj.ReferenceEquals (mKey.ErrorOrigin, mKey)) "ErrorOrigin is memo"
                    Expect.equal mKey.Peek 10 "previous published value preserved"
                    Expect.equal keyComputeRuns.Value 1 "compute did not run when keyEqual threw"
                    tick.Value <- 1
                    Expect.sequenceEqual keyScopeLog [ "inner:1:0"; "inner:1:1" ] "Phase 2 scope preserved across keyEqual throw"

                    // Disarm and return to equal key 11 % 10 = 1 -> recovers without discharging scope or re-running compute.
                    armKeyThrow.Value <- false
                    s.Value <- 11
                    Expect.equal mKey.Value 10 "recovered Ready 10 on equal key"
                    Expect.equal keyComputeRuns.Value 1 "compute still ran once"
                    Expect.sequenceEqual keyScopeLog [ "inner:1:0"; "inner:1:1" ] "scope still intact"

                    // Unequal key 2 discharges old scope and runs compute.
                    s.Value <- 2
                    Expect.equal mKey.Value 20 "updated to 20 on unequal key"
                    Expect.equal keyComputeRuns.Value 2 "compute ran second time"
                    Expect.sequenceEqual keyScopeLog [ "inner:1:0"; "inner:1:1"; "clean:1"; "inner:2:1" ] "old scope discharged")

                // Part 2: Throwing outputEqual (CompareOutputGuarded) on pure and owning comparer variants.
                use gOut = new Graph ()
                use _ = gOut.Activate ()
                let sOut = createSignal 1
                let armOutThrow = ref false

                let guardedOutComparer =
                    comparer (fun (a: int) b ->
                        if armOutThrow.Value then
                            raise (InvalidOperationException "output comparer")

                        a = b)

                let pureRuns = ref 0
                let owningRuns = ref 0
                let owningCleanups = ResizeArray<int>()

                let pureOut =
                    createMemoOnWithComparer guardedOutComparer (fun () -> sOut.Value % 10) (fun k ->
                        pureRuns.Value <- pureRuns.Value + 1
                        k * 100)

                let owningOut =
                    createOwningMemoOnWithComparer guardedOutComparer (fun () -> sOut.Value % 10) (fun k ->
                        owningRuns.Value <- owningRuns.Value + 1
                        onCleanup (fun () -> owningCleanups.Add k)
                        k * 100)

                Expect.equal pureOut.Value 100 "initial pure output"
                Expect.equal owningOut.Value 100 "initial owning output"

                // Arm outputEqual throw on unequal key 2 -> CompareOutputGuarded fails memo and latches phase2Failure.
                armOutThrow.Value <- true
                sOut.Value <- 2
                let pureEx1 = failedWith pureOut.TryValue
                let owningEx1 = failedWith owningOut.TryValue
                Expect.equal pureOut.Status Status.Error "pure Status.Error on outputEqual throw"
                Expect.equal owningOut.Status Status.Error "owning Status.Error on outputEqual throw"
                Expect.equal pureEx1.Message "output comparer" "pure output comparer exception"
                Expect.equal owningEx1.Message "output comparer" "owning output comparer exception"
                Expect.isTrue (obj.ReferenceEquals (pureOut.ErrorOrigin, pureOut)) "pure ErrorOrigin is memo"
                Expect.isTrue (obj.ReferenceEquals (owningOut.ErrorOrigin, owningOut)) "owning ErrorOrigin is memo"
                Expect.equal pureOut.Peek 100 "pure Peek preserved"
                Expect.equal owningOut.Peek 100 "owning Peek preserved"
                Expect.equal pureRuns.Value 2 "pure compute ran for key 2"
                Expect.equal owningRuns.Value 2 "owning compute ran for key 2"
                Expect.sequenceEqual owningCleanups [ 1 ] "key 1 scope discharged before key 2 compute"

                // Disarm outputEqual and feed equal key 12 % 10 = 2 -> latched phase2Failure stays Error without re-running compute.
                armOutThrow.Value <- false
                sOut.Value <- 12
                let pureEx2 = failedWith pureOut.TryValue
                let owningEx2 = failedWith owningOut.TryValue
                Expect.isTrue (obj.ReferenceEquals (pureEx2, pureEx1)) "pure latched phase2Failure preserved on equal key"
                Expect.isTrue (obj.ReferenceEquals (owningEx2, owningEx1)) "owning latched phase2Failure preserved on equal key"
                Expect.equal pureRuns.Value 2 "pure compute skipped on equal key 12"
                Expect.equal owningRuns.Value 2 "owning compute skipped on equal key 12"
                Expect.sequenceEqual owningCleanups [ 1 ] "key 2 scope not discharged on equal key 12"

                // Unequal key 3 runs compute and recovers cleanly.
                sOut.Value <- 3
                Expect.equal pureOut.Value 300 "pure recovered on key 3"
                Expect.equal owningOut.Value 300 "owning recovered on key 3"
                Expect.equal pureRuns.Value 3 "pure compute ran for key 3"
                Expect.equal owningRuns.Value 3 "owning compute ran for key 3"
                Expect.sequenceEqual owningCleanups [ 1; 2 ] "key 2 scope discharged on key 3"
            }

#if RANVIER_TRACE
            test "Trace.reconcile holds across createMemoOn and createMemoWithOn lifecycles" {
                use g = new Graph ()
                use _ = g.Activate ()
                let items = createSignal [ 1; 2 ]
                let tick = createSignal 0
                let pending = createAsyncSource<int>()
                let gate = createSignal false
                let sink = ResizeArray<string>()

                let pureLen =
                    createMemoOn
                        (fun () ->
                            if gate.Value then
                                pending.Value |> ignore

                            List.length items.Value)
                        (fun n -> n * 10)

                let owning =
                    createMemoWithOn (fun () -> pureLen.Value) (fun scaled ->
                        let inner = createMemo (fun _ -> scaled + tick.Value)
                        createEffect (fun () -> sink.Add $"{inner.Value}")
                        onCleanup (fun () -> sink.Add $"clean:{scaled}")
                        scaled)

                Expect.equal owning.Value 20 "initial pull"
                items.Value <- [ 3; 4 ]
                Expect.equal owning.Value 20 "equal key cutoff"
                tick.Value <- 5
                items.Value <- [ 7; 8; 9 ]
                Expect.equal owning.Value 30 "unequal key discharges old inner scope and builds new"
                gate.Value <- true
                Expect.equal owning.TryValue Pending "pending excursion"
                gate.Value <- false
                Expect.equal owning.Value 30 "restored after pending"
                owning.Dispose ()

                Expect.isEmpty (Trace.reconcile g) "live edges and scopes match folded trace log"
            }
#endif
        ]
