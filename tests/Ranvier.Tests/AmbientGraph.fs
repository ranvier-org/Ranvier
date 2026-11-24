module Ranvier.Tests.AmbientGraph

open System.Threading.Tasks
open Expecto
open Ranvier

/// <summary>
/// The graph the module-level functions resolve against while the engine runs a body or a cleanup.
/// </summary>
[<Tests>]
let tests =
    testList
        "AmbientGraph"
        [
            test "an effect woken by a write outside the graph registers its cleanups" {
                let g = new Graph ()
                let s = Signal (g, 1)
                let log = ResizeArray ()

                let e =
                    g.Run (fun () ->
                        new Effect (
                            g,
                            fun () ->
                                let v = s.Value
                                log.Add $"show {v}"
                                onCleanup (fun () -> log.Add $"hide {v}")
                        ))

                s.Value <- 2
                s.Value <- 3

                Expect.isNull e.Error "the run raised nothing"
                Expect.sequenceEqual log [ "show 1"; "hide 1"; "show 2"; "hide 2"; "show 3" ] "every run's cleanup ran"
            }

            test "an effect woken by a settle outside the graph creates nodes" {
                let g = new Graph ()
                let a = AsyncSource<int>(g)
                let seen = ResizeArray ()

                let e =
                    g.Run (fun () ->
                        new Effect (
                            g,
                            fun () ->
                                let v = a.Value
                                let doubled = createMemo (fun _ -> v * 2)
                                seen.Add doubled.Value
                        ))

                a.Settle 4

                Expect.isNull e.Error "the run raised nothing"
                Expect.sequenceEqual seen [ 8 ] "the node was created in the run"
            }

            test "an owning memo pulled outside the graph creates nodes" {
                let g = new Graph ()
                let s = Signal (g, 1)

                let m =
                    g.Run (fun () -> createMemoWith (fun _ -> (createMemo (fun _ -> s.Value + 1)).Value))

                s.Value <- 5

                Expect.equal m.Value 6 "the pull ran the body with its graph ambient"
            }

            test "a cleanup run outside the graph reaches its graph" {
                let g = new Graph ()
                let s = Signal (g, 1)
                let inner = ref 0

                g.Run (fun () ->
                    createEffect (fun () ->
                        s.Value |> ignore
                        onCleanup (fun () -> untrack (fun () -> inner.Value <- inner.Value + 1))))

                s.Value <- 2

                Expect.equal inner.Value 1 "the cleanup's untrack ran"
            }

            test "a body sees its own graph while another is ambient" {
                let g = new Graph ()
                let other = new Graph ()
                let s = Signal (g, 1)
                let seen = ResizeArray ()

                g.Run (fun () -> createEffect (fun () -> seen.Add (obj.ReferenceEquals (Graph.Current, g), s.Value)))

                use _ = other.Activate ()
                s.Value <- 2

                Expect.sequenceEqual seen [ (true, 1); (true, 2) ] "the effect ran against g"
                Expect.isTrue (obj.ReferenceEquals (Graph.Current, other)) "the caller's graph is restored"
            }

            test "a body that pulls another graph's memo keeps its own graph" {
                let a = new Graph ()
                let b = new Graph ()
                let sa = Signal (a, 0)
                let sb = Signal (b, 1)

                let m =
                    b.Run (fun () -> createMemoWith (fun _ -> (createMemo (fun _ -> sb.Value * 10)).Value))

                let seen = ResizeArray ()

                a.Run (fun () ->
                    createEffect (fun () ->
                        ignore sa.Value
                        let v = m.Value
                        seen.Add (v, obj.ReferenceEquals (Graph.Current, a))))

                sb.Value <- 2
                sa.Value <- 1

                Expect.sequenceEqual seen [ (10, true); (20, true) ] "a is ambient again after b's memo recomputed"
            }

            test "a fused memo-effect flush clears a caught upstream failure before running the effect" {
                use g = new Graph ()
                let s = Signal (g, 0)
                let shared = System.Exception "shared"
                let bad = g.Run (fun () -> createMemo (fun _ -> raise shared))
                ignore bad.TryValue

                let m =
                    g.Run (fun () ->
                        createMemo (fun _ ->
                            let v = s.Value

                            if v > 0 then
                                try
                                    ignore bad.Value
                                with _ ->
                                    ()

                            v))

                ignore m.Value

                let e =
                    g.Run (fun () ->
                        new Effect (
                            g,
                            fun () ->
                                if m.Value > 0 then
                                    raise shared
                        ))

                s.Value <- 1

                Expect.isTrue
                    (obj.ReferenceEquals (e.ErrorOrigin, e))
                    "the effect's own throw is attributed to the effect, not the caught upstream memo"
            }

            test "a stale memo pulled under another graph's activation during a flush sees its own graph" {
                use g = new Graph ()
                use active = g.Activate ()
                use other = new Graph ()
                let source = Signal (g, 0)
                let tick = Signal (g, 0)
                let mutable sawOwn = false
                let mutable sawOther = true
                let mutable beforeIsOther = false
                let mutable pulledValue = 0
                let mutable afterIsOther = false

                let m =
                    createMemo (fun _ ->
                        sawOwn <-
                            Graph.TryCurrent
                            |> ValueOption.exists (fun x -> obj.ReferenceEquals (x, g))

                        sawOther <-
                            Graph.TryCurrent
                            |> ValueOption.exists (fun x -> obj.ReferenceEquals (x, other))

                        source.Value)

                ignore m.Value
                sawOwn <- false
                sawOther <- true

                createEffectOn (fun () -> tick.Value) (fun t ->
                    if t > 0 then
                        use _ = other.Activate ()
                        beforeIsOther <- obj.ReferenceEquals (Graph.Current, other)
                        pulledValue <- m.Value
                        afterIsOther <- obj.ReferenceEquals (Graph.Current, other))

                source.Value <- 1
                tick.Value <- 1
                Expect.isTrue beforeIsOther "other is ambient before the pull"
                Expect.equal pulledValue 1 "the stale memo recomputed"
                Expect.isTrue afterIsOther "other is ambient again after the pull"
                Expect.isTrue sawOwn "the stale memo saw its own graph"
                Expect.isFalse sawOther "and did not see the outer activated graph"
            }

#if !FABLE_COMPILER
            // .NET only: JavaScript is single-threaded and has no AsyncLocal, ThreadId recycling, or WeakReference GC.
            test "an off-thread stale read during a guarded flush raises before recomputing" {
                use g = new Graph ()
                use _ = g.Activate ()
                let source = Signal (g, 0)
                let tick = Signal (g, 0)
                let mutable runs = 0
                let mutable outcome = ""

                let m =
                    Memo<int>(
                        g,
                        fun _ ->
                            runs <- runs + 1
                            source.Value
                    )

                ignore m.Value

                createEffectOn (fun () -> tick.Value) (fun t ->
                    if t > 0 then
                        let worker =
                            System.Threading.Thread (fun () ->
                                try
                                    outcome <- string m.Value
                                with ex ->
                                    outcome <- ex.GetType().Name)

                        worker.Start ()
                        worker.Join ())

                source.Value <- 1
                tick.Value <- 1
                Expect.equal outcome (nameof System.InvalidOperationException) "the worker was rejected by the guarded graph"
                Expect.equal runs 1 "the memo body never ran off-thread"
            }

            test "a child task activating another graph and suspending leaves the outer host ambient" {
                use a = new Graph ()
                use b = new Graph (GraphOptions.Default.WithThreadAffinity Unchecked)

                let gate =
                    TaskCompletionSource<unit>(TaskCreationOptions.RunContinuationsAsynchronously)

                let mutable outerBefore = false
                let mutable outerAfter = false
                let mutable childAfter = false
                let mutable child = Task.CompletedTask

                use _ =
                    new Effect (
                        a,
                        fun () ->
                            outerBefore <- obj.ReferenceEquals (Graph.Current, a)

                            child <-
                                task {
                                    use _ = b.Activate ()
                                    do! gate.Task
                                    childAfter <- obj.ReferenceEquals (Graph.Current, b)
                                }

                            outerAfter <-
                                Graph.TryCurrent
                                |> ValueOption.exists (fun x -> obj.ReferenceEquals (x, a))
                    )

                gate.SetResult ()
                child.GetAwaiter().GetResult()
                Expect.isTrue outerBefore "outer saw a before starting the child task"
                Expect.isTrue outerAfter "outer still saw a after the child task suspended"
                Expect.isTrue childAfter "the resumed child task saw b"
            }

            test "an outlived activation handle does not expose a worker graph to an idle starter thread" {
                for affinity in [ Unchecked; Serialised ] do
                    use g = new Graph (GraphOptions.Default.WithThreadAffinity affinity)
                    use other = new Graph ()
                    let tick = Signal (g, 0)
                    use entered = new System.Threading.ManualResetEventSlim ()
                    use finish = new System.Threading.ManualResetEventSlim ()
                    let mutable scope: System.IDisposable = null
                    let mutable failure: exn = null

                    use _ =
                        new Effect (
                            g,
                            fun () ->
                                if tick.Value = 0 then
                                    scope <- other.Activate ()
                                else
                                    entered.Set ()
                                    finish.Wait ()
                        )

                    scope.Dispose ()
                    Expect.isTrue Graph.TryCurrent.IsNone "starter thread is idle after disposing the outlived activation"

                    let worker =
                        System.Threading.Thread (fun () ->
                            try
                                // fsharpanalyzer: ignore-line-next RAN013
                                tick.Value <- 1
                            with ex ->
                                failure <- ex
                                entered.Set ())

                    do
                        use _ = System.Threading.ExecutionContext.SuppressFlow ()
                        worker.Start ()

                    try
                        Expect.isTrue (entered.Wait 5000) "worker entered the effect"

                        if not (isNull failure) then
                            raise failure

                        Expect.isTrue Graph.TryCurrent.IsNone "idle starter thread does not see the worker's running graph"
                    finally
                        finish.Set ()
                        worker.Join ()
            }

            test "a completed activation does not retain an unreachable unguarded graph on its thread" {
                let makeWeak affinity =
                    let alloc () =
                        let g = new Graph (GraphOptions.Default.WithThreadAffinity affinity)
                        use _ = g.Activate ()
                        let m = Memo<int>(g, fun _ -> 42)
                        ignore m.Value
                        System.WeakReference g

                    alloc ()

                for affinity in [ Unchecked; Serialised ] do
                    let weak = makeWeak affinity
                    System.GC.Collect ()
                    System.GC.WaitForPendingFinalizers ()
                    System.GC.Collect ()
                    Expect.isFalse weak.IsAlive $"completed {affinity} graph was collected"
            }

            test "an inline async continuation inside an effect or memo body leaves subsequent flushes and top-level reads clean" {
                use g = new Graph ()
                use _ = g.Activate ()
                let tcs = TaskCompletionSource<int>()
                let am = createAsync (fun _ _ -> task { let! v = tcs.Task in return v + 1 })
                ignore am.TryValue

                let s = createSignal 0
                let other = createSignal 0
                let fresh = createSignal 7
                let mutable firstRuns = 0
                let mutable otherRuns = 0

                createEffect (fun () ->
                    firstRuns <- firstRuns + 1

                    if s.Value = 1 then
                        tcs.SetResult 41)

                createEffect (fun () ->
                    ignore other.Value
                    otherRuns <- otherRuns + 1)

                s.Value <- 1
                s.Value <- 2
                s.Value <- 3
                other.Value <- 1
                other.Value <- 2
                Expect.equal fresh.Value 7 "fresh signal value"
                Expect.equal firstRuns 4 "the effect that resumed the continuation still ran on every write"
                Expect.equal otherRuns 3 "an unrelated effect continued flushing"
                Expect.equal fresh.ObserverCount 0 "a top-level read after the continuation linked no observer"

                // Memo-body variant (`RunTopCore`):
                let tcs2 = TaskCompletionSource<int>()
                let am2 = createAsync (fun _ _ -> task { let! v = tcs2.Task in return v + 10 })
                ignore am2.TryValue
                let sm = createSignal 0

                let m =
                    createMemo (fun _ ->
                        let v = sm.Value

                        if v = 1 then
                            tcs2.SetResult 5

                        v * 2)

                Expect.equal m.Value 0 "initial memo read"
                sm.Value <- 1
                Expect.equal m.Value 2 "memo recompute resumed inline continuation cleanly"
                other.Value <- 3
                Expect.equal otherRuns 4 "flushes still run after inline continuation inside Memo.RunTopCore"
            }

            test "an unguarded graph constructed on a dead thread remains ambient when a later thread reuses its managed thread id" {
                let mutable g = Unchecked.defaultof<Graph>
                let mutable creatorTid = 0

                let creator =
                    System.Threading.Thread (fun () ->
                        creatorTid <- System.Environment.CurrentManagedThreadId
                        g <- new Graph (GraphOptions.Default.WithThreadAffinity Unchecked))

                creator.Start ()
                creator.Join ()

                try
                    let s = Signal (g, 0)
                    let sawSelf = ResizeArray<bool>()

                    g.Run (fun () ->
                        createEffect (fun () ->
                            ignore s.Value

                            sawSelf.Add (
                                Graph.TryCurrent
                                |> ValueOption.exists (fun x -> obj.ReferenceEquals (x, g))
                            )))

                    let mutable tries = 0
                    let mutable reused = false

                    while not reused && tries < 512 do
                        tries <- tries + 1

                        let t =
                            System.Threading.Thread (fun () ->
                                if System.Environment.CurrentManagedThreadId = creatorTid then
                                    reused <- true
                                    // fsharpanalyzer: ignore-line-next RAN013
                                    s.Value <- tries)

                        t.Start ()
                        t.Join ()

                    if not reused then
                        s.Value <- 999

                    Expect.isTrue
                        (sawSelf |> Seq.forall id)
                        "effect body saw its unguarded graph on every thread including a recycled creator ThreadId"
                finally
                    g.Dispose ()
            }
#endif

            test "check-chain unwinding during a flush clears a caught upstream failure before recomputing a downstream memo" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal 0
                let tick = createSignal 0
                let shared = System.Exception "chain-shared"
                let bad = createMemo (fun _ -> raise shared)
                ignore bad.TryValue

                let m1 = createMemo (fun _ -> s.Value)

                let m2 =
                    createMemo (fun _ ->
                        let v = m1.Value

                        if v > 0 then
                            try
                                ignore bad.Value
                            with _ ->
                                ()

                        v)

                let m3 = createMemo (fun _ -> if m2.Value > 0 then raise shared else 0)

                Expect.equal m3.Value 0 "warmed chain"

                createEffectOn (fun () -> tick.Value) (fun t ->
                    if t > 0 then
                        ignore m3.TryValue)

                s.Value <- 1
                tick.Value <- 1
                Expect.isTrue (obj.ReferenceEquals (m3.ErrorOrigin, m3)) "m3's own throw is attributed to m3, not the upstream memo caught in m2"
            }

            test "an idle signal write marks both node and projection observers without throwing" {
                use g = new Graph ()
                use _ = g.Activate ()
                let s = createSignal [ 1; 2 ]
                let directSeen = ResizeArray<int list>()
                let keysSeen = ResizeArray<int[]>()
                createEffect (fun () -> directSeen.Add s.Value)
                let rows = createProjection id (fun x -> x * 10) (fun () -> s.Value)
                createEffect (fun () -> keysSeen.Add rows.Keys)
                s.Value <- [ 1; 2; 3 ]
                Expect.equal directSeen.Count 2 "direct effect ran on idle write"
                Expect.equal keysSeen[keysSeen.Count - 1] [| 1; 2; 3 |] "projection pass observer also ran on idle write"
            }

            test "nested activations, flushes, stale pulls, and out-of-order disposal restore every ambient graph" {
                use a = new Graph ()
                use b = new Graph ()
                use c = new Graph ()
                let sa = Signal (a, 0)
                let sb = Signal (b, 10)
                let sc = Signal (c, 0)
                let ma = a.Run (fun () -> createMemo (fun _ -> sa.Value * 2))
                let mb = b.Run (fun () -> createMemo (fun _ -> sb.Value + 1))
                let log = ResizeArray<string>()

                a.Run (fun () ->
                    createEffect (fun () ->
                        let v = ma.Value

                        if v > 0 then
                            let isA = if obj.ReferenceEquals (Graph.Current, a) then 1 else 0
                            log.Add $"a:{v}:{isA}"))

                c.Run (fun () ->
                    createEffect (fun () ->
                        let v = sc.Value

                        if v > 0 then
                            let beforeC = if obj.ReferenceEquals (Graph.Current, c) then 1 else 0
                            // Activate a inside c's flush, pull stale b memo, flush a, then dispose a's activation.
                            let innerA = a.Activate ()
                            let duringInnerA = if obj.ReferenceEquals (Graph.Current, a) then 1 else 0
                            let bv = mb.Value
                            sa.Value <- v
                            let afterNestedA = if obj.ReferenceEquals (Graph.Current, a) then 1 else 0
                            innerA.Dispose ()
                            let restoredC = if obj.ReferenceEquals (Graph.Current, c) then 1 else 0
                            log.Add $"c:{v}:{beforeC}:{duringInnerA}:{bv}:{afterNestedA}:{restoredC}"))

                let actA = a.Activate ()
                Expect.isTrue (obj.ReferenceEquals (Graph.Current, a)) "a active"
                // Repeated flushes under the same epoch stay on the lazy-epoch fast path.
                sa.Value <- 1
                sa.Value <- 2
                Expect.isTrue (obj.ReferenceEquals (Graph.Current, a)) "a still active after repeated flushes"

                let actB = b.Activate ()
                Expect.isTrue (obj.ReferenceEquals (Graph.Current, b)) "b shadows a"

                sb.Value <- 20
                sc.Value <- 3
                Expect.isTrue (obj.ReferenceEquals (Graph.Current, b)) "b restored after nested cross-graph flush"

                // Disposing b restores shadowed a; a subsequent flush on a runs cleanly under the updated lazy epoch.
                actB.Dispose ()
                Expect.isTrue (obj.ReferenceEquals (Graph.Current, a)) "a restored when inner b activation is disposed"

                sa.Value <- 4
                Expect.isTrue (obj.ReferenceEquals (Graph.Current, a)) "a stays ambient after post-restore flush"

                actA.Dispose ()
                Expect.isTrue Graph.TryCurrent.IsNone "no ambient graph after all activations disposed"

                Expect.sequenceEqual
                    log
                    [ "a:2:1"; "a:4:1"; "a:6:1"; "c:3:1:1:21:1:1"; "a:8:1" ]
                    "every nested host switch saw its own graph and restored the outer host"
            }
        ]
