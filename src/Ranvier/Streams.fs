namespace Ranvier

open System
open System.Threading
open System.Threading.Tasks

/// <summary>The accumulator behind a <c>StreamSink</c>.</summary>
#if FABLE_COMPILER
type internal StreamInlet<'T> =
    abstract Deliver: item: 'T -> unit
#else
[<AbstractClass>]
type internal StreamInlet<'T>() =
    abstract Deliver: item: 'T -> unit
#endif

/// <summary>The receiver of a running stream's items.</summary>
[<Sealed>]
type StreamSink<'T> internal (inlet: StreamInlet<'T>) =
    internal new(deliver: 'T -> unit) =
#if FABLE_COMPILER
        StreamSink<'T>(
            { new StreamInlet<'T> with
                member _.Deliver item =
                    deliver item
            }
        )
#else
        StreamSink<'T>(
            { new StreamInlet<'T>() with
                member _.Deliver item =
                    deliver item
            }
        )
#endif

    /// <summary>Folds <c>item</c> into the stream's state. Callable from any thread. Calls from other threads may overlap each other; a call on the graph thread must not overlap any other call.</summary>
    member _.Next(item: 'T) =
        inlet.Deliver item

/// <summary>A producer of items, run once per stream start.</summary>
[<AbstractClass>]
type StreamSource<'T>() =
    /// <summary>
    /// Delivers items to <c>sink</c> until the stream ends. The task completes when the stream completes, faults when it
    /// fails, and is cancelled or faults with <c>OperationCanceledException</c> after <c>token</c> is cancelled.
    /// </summary>
    abstract Run: sink: StreamSink<'T> * token: CancellationToken -> Task

/// <summary>The node a stream run schedules a publication on.</summary>
type internal IStreamRequester =
    /// <summary>Queues the node for the current or next flush; returns true while the node remains queued.</summary>
    abstract RequestOnGraph: unit -> bool
#if !FABLE_COMPILER
    /// <summary>Posts one wake to the graph while none is outstanding. Called off the graph thread.</summary>
    abstract RequestOffGraph: unit -> unit
#endif
    /// <summary>Reports an exception thrown by the fold for run <c>gen</c>.</summary>
    abstract ReportFoldFailure: gen: int * error: exn -> unit

/// <summary>The state of one started stream, shared by its producer and the graph thread.</summary>
type internal IStreamRun<'S> =
    /// <summary>Captures the state as <c>Captured</c>; returns the number of items folded since the previous snapshot.</summary>
    abstract Snapshot: unit -> int
    /// <summary>Clears the graph-thread queue flag and captures <c>state</c> when an item has been folded; returns folded count.</summary>
    abstract SnapshotIfReceived: unit -> int
    /// <summary>Clears the cached graph-thread queue flag when the owning <c>StreamMemo</c> dequeues.</summary>
    abstract OnDequeued: unit -> unit
    /// <summary>The state captured by the latest <c>Snapshot</c>.</summary>
    abstract Captured: 'S
    /// <summary>True once an item has been folded.</summary>
    abstract Received: bool
    /// <summary>Ignores every later item.</summary>
    abstract Close: unit -> unit
    /// <summary>Starts the producer against this run's sink and token.</summary>
    abstract RunSource: unit -> Task

/// <summary>Creates a started <c>IStreamRun&lt;'S&gt;</c> without a curried multi-argument closure.</summary>
type internal IStreamStarter<'S> =
    abstract Start: requester: IStreamRequester * gen: int * token: CancellationToken -> IStreamRun<'S>

/// <summary>Outcome of evaluating Phase 1 (<c>read</c>) on a two-phase <c>StreamMemo&lt;'S&gt;</c>.</summary>
type internal StreamPhase1Result =
    | EqualKey = 0uy
    | UnequalKey = 1uy
    | Phase1Failed = 2uy

/// <summary>Type-erased Phase 1/Phase 2 starter stored on a two-phase <c>StreamMemo&lt;'S&gt;</c>.</summary>
type internal IStreamKeyStarter<'S> =
    abstract ReadAndCompare: unit -> StreamPhase1Result
    abstract CommitNextKey: unit -> unit
    abstract StartUntracked: requester: IStreamRequester * gen: int * token: CancellationToken -> IStreamRun<'S>
    abstract Phase1Exception: exn

#if FABLE_COMPILER
type internal FoldDelegate<'S, 'T> = 'S -> 'T -> 'S
type internal StreamStartDelegate<'K, 'T> = 'K -> CancellationToken -> StreamSource<'T>
#else
type internal FoldDelegate<'S, 'T> = OptimizedClosures.FSharpFunc<'S, 'T, 'S>
type internal StreamStartDelegate<'K, 'T> = OptimizedClosures.FSharpFunc<'K, CancellationToken, StreamSource<'T>>
#endif

module internal FoldFn =
#if FABLE_COMPILER
    let inline adapt (fold: 'S -> 'T -> 'S) : FoldDelegate<'S, 'T> = fold

    let inline invoke (fold: FoldDelegate<'S, 'T>) (state: 'S) (item: 'T) : 'S =
        fold state item

    let inline adaptStart (start: 'K -> CancellationToken -> StreamSource<'T>) : StreamStartDelegate<'K, 'T> = start

    let inline invokeStart (start: StreamStartDelegate<'K, 'T>) (key: 'K) (token: CancellationToken) : StreamSource<'T> =
        start key token
#else
    let inline adapt (fold: 'S -> 'T -> 'S) : FoldDelegate<'S, 'T> =
        OptimizedClosures.FSharpFunc<'S, 'T, 'S>.Adapt fold

    let inline invoke (fold: FoldDelegate<'S, 'T>) (state: 'S) (item: 'T) : 'S =
        fold.Invoke (state, item)

    let inline adaptStart (start: 'K -> CancellationToken -> StreamSource<'T>) : StreamStartDelegate<'K, 'T> =
        OptimizedClosures.FSharpFunc<'K, CancellationToken, StreamSource<'T>>.Adapt start

    let inline invokeStart (start: StreamStartDelegate<'K, 'T>) (key: 'K) (token: CancellationToken) : StreamSource<'T> =
        start.Invoke (key, token)
#endif

/// <summary>
/// A started stream's accumulator. Items fold lock-free on the graph thread; off-thread producers, <c>Snapshot</c>,
/// and <c>Close</c> synchronize on the <c>StreamRun</c> instance itself (zero separate gate object allocation).
/// </summary>
[<Sealed>]
type internal StreamRun<'S, 'T>
    (graph: Graph, seed: 'S, fold: FoldDelegate<'S, 'T>, requester: IStreamRequester, gen: int, source: StreamSource<'T>, token: CancellationToken) =
#if !FABLE_COMPILER
    inherit StreamInlet<'T>()
#endif
    let mutable state = seed
    let mutable captured = seed
    let mutable folded = 0
    let mutable received = false
    let mutable closed = false
    let mutable onGraphQueued = false
    let mutable foldError: exn = Platform.nil ()

#if !FABLE_COMPILER
    /// <summary>Folds <c>item</c> under instance lock: 1 when folded, 0 when closed, 2 when the fold threw and closed the run.</summary>
    member private _.FoldLocked(item: 'T) =
        if Volatile.Read &closed then
            0
        else
            try
                state <- FoldFn.invoke fold state item
                folded <- folded + 1
                received <- true
                1
            with error ->
                Volatile.Write (&closed, true)
                foldError <- error
                2

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.DeliverOffGraph(item: 'T) =
        System.Threading.Monitor.Enter this

        let outcome =
            try
                this.FoldLocked item
            finally
                System.Threading.Monitor.Exit this

        if outcome = 1 then
            requester.RequestOffGraph ()
        elif outcome = 2 then
            requester.ReportFoldFailure (gen, foldError)

    /// <summary>Folds <c>item</c> and requests a publication; a throwing fold closes the run and reports the exception.</summary>
    override this.Deliver(item: 'T) =
        // The graph thread folds without the lock; this depends on StreamSink.Next calls never overlapping.
        if graph.IsOnGraphThread then
            if not closed then
                let mutable raised: exn = null

                try
                    state <- FoldFn.invoke fold state item
                    folded <- folded + 1
                    received <- true
                with error ->
                    closed <- true
                    raised <- error

                if isNull raised then
                    if
                        not (
                            onGraphQueued
                            && (graph.BatchDepth > 0 || graph.IsFlushing)
                        )
                    then
                        onGraphQueued <- requester.RequestOnGraph ()
                else
                    requester.ReportFoldFailure (gen, raised)
        else
            this.DeliverOffGraph item
#else
    interface StreamInlet<'T> with
        /// <summary>Folds <c>item</c> and requests a publication; a throwing fold closes the run and reports the exception.</summary>
        member _.Deliver(item: 'T) =
            if not closed then
                let mutable raised: exn = Platform.nil ()

                try
                    state <- FoldFn.invoke fold state item
                    folded <- folded + 1
                    received <- true
                with error ->
                    closed <- true
                    raised <- error

                if Platform.isNull raised then
                    if
                        not (
                            onGraphQueued
                            && (graph.BatchDepth > 0 || graph.IsFlushing)
                        )
                    then
                        onGraphQueued <- requester.RequestOnGraph ()
                else
                    requester.ReportFoldFailure (gen, raised)
#endif

    interface IStreamRun<'S> with
        member this.Snapshot() =
            onGraphQueued <- false
#if FABLE_COMPILER
            let count = folded
            folded <- 0
            captured <- state
            count
#else
            System.Threading.Monitor.Enter this

            try
                let count = folded
                folded <- 0
                captured <- state
                count
            finally
                System.Threading.Monitor.Exit this
#endif

        member this.SnapshotIfReceived() =
            onGraphQueued <- false
#if FABLE_COMPILER
            if not received then
                0
            else
                let count = folded
                folded <- 0
                captured <- state
                count
#else
            System.Threading.Monitor.Enter this

            try
                if not received then
                    0
                else
                    let count = folded
                    folded <- 0
                    captured <- state
                    count
            finally
                System.Threading.Monitor.Exit this
#endif

        member _.OnDequeued() =
            onGraphQueued <- false

        member _.Captured = captured

        member _.Received =
#if FABLE_COMPILER
            received
#else
            Volatile.Read &received
#endif

        member this.Close() =
#if FABLE_COMPILER
            closed <- true
#else
            System.Threading.Monitor.Enter this

            try
                Volatile.Write (&closed, true)
            finally
                System.Threading.Monitor.Exit this
#endif

        member this.RunSource() =
            source.Run (StreamSink<'T> this, token)

[<Sealed>]
type internal StreamStarter<'S, 'T>(graph: Graph, seed: 'S, fold: FoldDelegate<'S, 'T>, start: CancellationToken -> StreamSource<'T>) =
    interface IStreamStarter<'S> with
        member _.Start(requester, gen, token) =
            let source = start token

            if Platform.isNull (box source) then
                raise (InvalidOperationException "The stream's start function returned null.")

            StreamRun<'S, 'T>(graph, seed, fold, requester, gen, source, token) :> IStreamRun<'S>

[<Sealed>]
type internal StreamKeyStarter<'K, 'S, 'T>(graph: Graph, seed: 'S, fold: FoldDelegate<'S, 'T>, read: unit -> 'K, start: StreamStartDelegate<'K, 'T>) =
    let keyEqual = graph.EncodedComparer<'K>()
    let mutable hasKey = false
    let mutable lastKey = Platform.zero<'K>()
    let mutable candidateKey = Platform.zero<'K>()
    let mutable phase1Ex: exn = Platform.nil ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private _.CompareKeyGuarded(nextKey: 'K) : StreamPhase1Result =
        try
            if ComparerSentinel.equals keyEqual lastKey nextKey then
                StreamPhase1Result.EqualKey
            else
                StreamPhase1Result.UnequalKey
        with ex ->
            phase1Ex <- ex
            StreamPhase1Result.Phase1Failed

    interface IStreamKeyStarter<'S> with
        member this.ReadAndCompare() =
            phase1Ex <- Platform.nil ()
            let nextKey = read ()
            candidateKey <- nextKey

            if not hasKey then
                StreamPhase1Result.UnequalKey
            elif SafeEquality.isSafe keyEqual then
                if ComparerSentinel.equals keyEqual lastKey nextKey then
                    StreamPhase1Result.EqualKey
                else
                    StreamPhase1Result.UnequalKey
            else
                this.CompareKeyGuarded nextKey

        member _.CommitNextKey() =
            lastKey <- candidateKey
            hasKey <- true

        member _.StartUntracked(requester, gen, token) =
            let source = FoldFn.invokeStart start lastKey token

            if Platform.isNull (box source) then
                raise (InvalidOperationException "The stream's start function returned null.")

            StreamRun<'S, 'T>(graph, seed, fold, requester, gen, source, token) :> IStreamRun<'S>

        member _.Phase1Exception = phase1Ex

/// <summary>How a publication reaches the stream memo's readers when its state moved.</summary>
type internal StreamNotice =
    /// <summary>Readers are not notified.</summary>
    | Silent = 0
    /// <summary>Readers are marked dirty and the graph flushes.</summary>
    | Flush = 1
    /// <summary>Readers are marked dirty; the running flush runs the effects this queues.</summary>
    | InFlush = 2

/// <summary>Why a stream ended, recorded by <c>StreamEnded</c>.</summary>
type internal StreamEnd =
    | Completed = 0
    | Failed = 1
    | Restarted = 2
    | Disposed = 3

/// <summary>
/// The latest state of a stream: a fold of its items into a seed, published at most once per graph pass.
/// </summary>
/// <remarks>
/// Starts on its first read. A change to a value read by <c>start</c> cancels the stream and starts a new one from the
/// seed; the node is pending until the new stream's first item or completion. Disposal cancels the stream, and a
/// pending node becomes failed with <c>ObjectDisposedException</c>.
/// </remarks>
[<Sealed>]
type StreamMemo<'S> internal (graph: Graph, seed: 'S, emptyFails: bool, starter: IStreamStarter<'S>, keyStarter: IStreamKeyStarter<'S>) =
    let isSplit = not (Platform.isNull (box keyStarter))
    let id = graph.NextId ()
#if !FABLE_COMPILER && !RANVIER_TRACE
    let mutable observers = ObserverList.Create ()
#else
#if FABLE_COMPILER && !RANVIER_TRACE
    let mutable observers: ObserverList = Platform.nil ()
#else
    let observers = ObserverSet ()
    do Tracer.Bind (observers, graph, id)
#endif
#endif
    let mutable sources = SourceList.Create ()
    do Tracer.Bind (sources, graph, id)
    let equal = graph.EncodedComparer<'S>()
#if !FABLE_COMPILER
    let runSource = fun (started: IStreamRun<'S>) -> started.RunSource ()
#endif
    let mutable freshness = Freshness.Dirty
    let mutable status = Status.Pending ||| Status.Uninitialized
    let mutable value = Unchecked.defaultof<'S>
    let mutable failure: Failure = Platform.nil ()
    let mutable phase2Status = Status.Pending ||| Status.Uninitialized
    let mutable phase2Failure: Failure = Platform.nil ()
    let mutable phase1Status = Status.None
    let mutable phase1Failure: Failure = Platform.nil ()
    let mutable maskedMove = false
    let mutable inLaunchOn = false
    let mutable notifiedInLaunch = false
    let mutable runs = 0
    let mutable disposed = false

    /// <summary>Set when a run ends stale, as on <c>Memo</c>. The next mark notifies the observers and clears it.</summary>
    let mutable unannounced = false
    let mutable violated = false
    let mutable link: OwnerLink = Platform.nil ()
    let mutable generation = 0
    let mutable run: IStreamRun<'S> = Platform.nil ()
    let mutable cts: CancellationTokenSource = Platform.nil ()
    let mutable running = false
    let mutable active = 0
    let mutable launching = false
    let mutable queued = false
#if !FABLE_COMPILER
    let mutable wakePosted = 0
    let mutable wakeAction: unit -> unit = Unchecked.defaultof<_>
#endif

    member inline private _.SyncVisibleOn() =
        if not disposed then
            if phase1Status = Status.Pending then
                status <-
                    Status.Pending
                    ||| (phase2Status &&& Status.Uninitialized)

                failure <- Platform.nil ()
            elif phase1Status = Status.Error then
                status <- Status.Error
                failure <- phase1Failure
            else
                status <- phase2Status
                failure <- phase2Failure

    member inline private _.NotifyOtherObservers() =
#if !FABLE_COMPILER && !RANVIER_TRACE
        graph.NotifyMovedList &observers
#else
#if FABLE_COMPILER && !RANVIER_TRACE
        graph.NotifyMovedList observers
#else
        graph.NotifyMoved observers
#endif
#endif

    member private this.Attach() =
        let owner = graph.CurrentOwner
        link <- owner.AttachLinked this
        Tracer.StreamNew (graph, id, owner)

    member inline private this.Enqueue() =
        if not queued then
            queued <- true
            graph.Schedule (this :> IScheduled)

#if !FABLE_COMPILER
    member private this.Wake() =
        Volatile.Write (&wakePosted, 0)

        if not disposed then
            this.Enqueue ()
            graph.RequestFlushFromEntered ()
#endif

    member inline private this.RequestOnGraph() : bool =
        if not launching && not disposed then
            this.Enqueue ()

            if graph.BatchDepth = 0 then
                graph.RequestFlushFromEntered ()

            queued
        else
            false

#if !FABLE_COMPILER
    member private this.RequestOffGraph() =
        if
            Volatile.Read &wakePosted = 0
            && Interlocked.Exchange (&wakePosted, 1) = 0
        then
            if Platform.isNull (box wakeAction) then
                wakeAction <- this.Wake

            graph.PostQueued wakeAction

    interface Platform.IDoneWaiter with
        member this.OnDone(outcome, gen) =
            if graph.IsOnGraphThread then
                this.Ended (gen, outcome)
            else
                graph.PostQueued (fun () -> this.Ended (gen, outcome))
#endif

    member private this.ReportFoldFailure(gen, error) =
#if FABLE_COMPILER
        this.FoldFailed (gen, error)
#else
        if graph.IsOnGraphThread then
            this.FoldFailed (gen, error)
        else
            graph.PostQueued (fun () -> this.FoldFailed (gen, error))
#endif

    member inline private _.NotifyObserversDirty() =
#if FABLE_COMPILER && !RANVIER_TRACE
        if not (Platform.isNull observers) then
            observers.NotifyDirty ()
#else
        observers.NotifyDirty ()
#endif

    member inline private _.NotifyObserversCheck() =
#if FABLE_COMPILER && !RANVIER_TRACE
        if not (Platform.isNull observers) then
            observers.NotifyCheck ()
#else
        observers.NotifyCheck ()
#endif

    member inline private _.Notify() =
        if inLaunchOn then
            notifiedInLaunch <- true
#if !FABLE_COMPILER && !RANVIER_TRACE
        graph.NotifyMovedList &observers
#else
#if FABLE_COMPILER && !RANVIER_TRACE
        graph.NotifyMovedList observers
#else
        graph.NotifyMoved observers
#endif
#endif
        Tracer.Notified graph
        graph.RequestFlush ()

    member inline private _.NotifyInFlush() =
#if !FABLE_COMPILER && !RANVIER_TRACE
        graph.NotifyMovedList &observers
#else
#if FABLE_COMPILER && !RANVIER_TRACE
        graph.NotifyMovedList observers
#else
        graph.NotifyMoved observers
#endif
#endif
        Tracer.Notified graph

    /// <summary>Closes the open stream and cancels its token.</summary>
    member private _.Stop(reason: StreamEnd) =
        if not (Platform.isNull (box run)) then
            run.Close ()

        if running then
            running <- false
            Tracer.StreamEnded (graph, id, int reason)

        if not (Platform.isNull cts) then
            let source = cts
            cts <- Platform.nil ()
            source.Cancel ()
            source.Dispose ()

    member private this.Fail(error: exn) =
        this.Stop StreamEnd.Failed

        if isSplit then
            let error = UntrackedPhase.wrapPending UntrackedPhase.streamOnPending error
            let next = graph.FailureOf (error, this, phase2Failure)

            let moved =
                phase2Status <> Status.Error
                || Failure.Moved (next, phase2Failure)

            phase2Failure <- next
            phase2Status <- Status.Error
            Tracer.StreamPublished (graph, id, int (byte Status.Error), 0, box error)
            this.SyncVisibleOn ()

            if phase1Status <> Status.None then
                if moved then
                    maskedMove <- true
            elif moved then
                this.Notify ()
        else
            let next = graph.FailureOf (error, this, failure)

            let moved =
                status <> Status.Error
                || Failure.Moved (next, failure)

            failure <- next
            status <- Status.Error
            Tracer.StreamPublished (graph, id, int (byte status), 0, box error)

            if moved then
                this.Notify ()

    member private this.Publish(state: 'S, count: int, notice: StreamNotice) =
        try
            if isSplit then
                let moved =
                    phase2Status <> Status.None
                    || not (ComparerSentinel.equals equal value state)

                value <- state
                phase2Status <- Status.None
                phase2Failure <- Platform.nil ()
                Tracer.StreamPublished (graph, id, int (byte Status.None), count, box state)
                this.SyncVisibleOn ()

                if phase1Status <> Status.None then
                    if moved then
                        maskedMove <- true
                else
                    maskedMove <- false

                    if moved then
                        if notice = StreamNotice.Flush then
                            this.Notify ()
                        elif notice = StreamNotice.InFlush then
                            this.NotifyInFlush ()
            else
                let moved =
                    status <> Status.None
                    || not (ComparerSentinel.equals equal value state)

                value <- state
                status <- Status.None
                failure <- Platform.nil ()
                Tracer.StreamPublished (graph, id, int (byte status), count, box state)

                if moved then
                    if notice = StreamNotice.Flush then
                        this.Notify ()
                    elif notice = StreamNotice.InFlush then
                        this.NotifyInFlush ()
        with error ->
            this.Fail error

    member inline private this.PublishInFlush(state: 'S, count: int) =
        try
            if isSplit then
                let moved =
                    phase2Status <> Status.None
                    || not (ComparerSentinel.equals equal value state)

                value <- state
                phase2Status <- Status.None
                phase2Failure <- Platform.nil ()
                Tracer.StreamPublished (graph, id, int (byte Status.None), count, box state)
                this.SyncVisibleOn ()

                if phase1Status <> Status.None then
                    if moved then
                        maskedMove <- true
                else
                    maskedMove <- false

                    if moved then
                        this.NotifyInFlush ()
            else
                let moved =
                    status <> Status.None
                    || not (ComparerSentinel.equals equal value state)

                value <- state
                status <- Status.None
                failure <- Platform.nil ()
                Tracer.StreamPublished (graph, id, int (byte status), count, box state)

                if moved then
                    this.NotifyInFlush ()
        with error ->
            this.Fail error

    member private this.PublishLatest(notice: StreamNotice) =
        let count = run.Snapshot ()
        this.Publish (run.Captured, count, notice)

    member private this.Ended(gen: int, outcome: Platform.FlightOutcome<unit>) =
        if not disposed && gen = generation && running then
            match outcome with
            | Platform.FlightOutcome.Completed _ ->
                let received = run.Received
                this.Stop StreamEnd.Completed

                if received then
                    this.PublishLatest StreamNotice.Flush
                elif emptyFails then
                    this.Fail (InvalidOperationException "The stream completed without an item.")
                else
                    this.Publish (seed, 0, StreamNotice.Flush)
            | Platform.FlightOutcome.Faulted error
            | Platform.FlightOutcome.Canceled error -> this.Fail error

    member private this.FoldFailed(gen: int, error: exn) =
        if not disposed && gen = generation && running then
            this.Fail error

    member private this.Launch() =
        freshness <- Freshness.Clean
        unannounced <- false
        this.Stop StreamEnd.Restarted
        generation <- generation + 1
        let gen = generation
        runs <- runs + 1
        Tracer.RunStart (graph, id, runs)
        failure <- Platform.nil ()

        status <-
            Status.Pending
            ||| (status &&& Status.Uninitialized)

        sources.BeginRun ()
        let source = new CancellationTokenSource ()
        cts <- source
        active <- 1

        try
            try
                let started =
#if FABLE_COMPILER
                    graph.RunHosted (this :> IComputation, fun () -> starter.Start (this :> IStreamRequester, gen, source.Token))
#else
                    let saved = graph.EnterHosted (this :> IComputation)

                    try
                        let s = starter.Start (this :> IStreamRequester, gen, source.Token)
                        graph.CheckRaised ()
                        s
                    finally
                        graph.LeaveHosted saved
#endif

                if violated then
                    raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                if not disposed then
                    run <- started
                    running <- true
                    Tracer.StreamStarted (graph, id, gen)
                    launching <- true

                    let work =
                        try
                            try
#if FABLE_COMPILER
                                graph.RunUntracked (fun () -> started.RunSource ())
#else
                                graph.RunUntrackedWith (runSource, started)
#endif
                            with NotReadyException _ as pending ->
                                let failure = InvalidOperationException "StreamSource.Run read a pending source."
#if FABLE_COMPILER
                                Platform.setInner failure pending
                                raise failure
#else
                                raise (InvalidOperationException (failure.Message, pending))
#endif
                        finally
                            launching <- false

                    if Platform.isNull work then
                        raise (InvalidOperationException "StreamSource.Run returned null.")

#if FABLE_COMPILER
                    Platform.whenDone work (fun outcome -> this.Ended (gen, outcome))
#else
                    if work.IsCompleted && graph.IsOnGraphThread then
                        this.Ended (gen, Platform.settledUnitOutcome work)
                    else
                        Platform.attachDone work (this :> Platform.IDoneWaiter) gen
#endif

                    if gen = generation && running && started.Received then
                        this.PublishLatest StreamNotice.Silent
            finally
                active <- 0

                if disposed then
                    sources.Clear (this :> IComputation)
                else
                    sources.EndRun (this :> IComputation)
        with
        | error when violated ->
            violated <- false
            this.Fail (ScopeMessages.failure ScopeMode.Pure error)
        | NotReadyException _ -> this.Stop StreamEnd.Restarted
        | error -> this.Fail error

        Tracer.RunEnd (graph, id, status)

        if freshness <> Freshness.Clean then
            unannounced <- true
            graph.NoteStaleTransition ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.CompareMovedGuarded(prevValue: 'S) : bool =
        try
            not (ComparerSentinel.equals equal prevValue value)
        with error ->
            this.Fail error
            false

    member private this.LaunchOn() =
        freshness <- Freshness.Clean
        unannounced <- false
        let prevStatus = status
        let prevFailure = failure
        let prevValue = value
        inLaunchOn <- true
        notifiedInLaunch <- false
        let mutable syncPublished = false
        runs <- runs + 1
        Tracer.RunStart (graph, id, runs)
        sources.BeginRun ()
        active <- 1

        let mutable p1Outcome = StreamPhase1Result.EqualKey
        let mutable p1Pending = false
        let mutable p1Error: exn = Platform.nil ()

        try
            try
                try
                    p1Outcome <-
#if FABLE_COMPILER
                        graph.RunHosted (this :> IComputation, fun () -> keyStarter.ReadAndCompare ())
#else
                        let saved = graph.EnterHosted (this :> IComputation)

                        try
                            let r = keyStarter.ReadAndCompare ()
                            graph.CheckRaised ()
                            r
                        finally
                            graph.LeaveHosted saved
#endif

                    if violated then
                        raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))
                finally
                    if disposed then
                        sources.Clear (this :> IComputation)
                    else
                        sources.EndRun (this :> IComputation)
            with
            | error when violated ->
                violated <- false
                p1Outcome <- StreamPhase1Result.Phase1Failed
                p1Error <- ScopeMessages.failure ScopeMode.Pure error
            | NotReadyException _ -> p1Pending <- true
            | error ->
                p1Outcome <- StreamPhase1Result.Phase1Failed
                p1Error <- error

            if disposed then
                active <- 0
            elif p1Pending then
                active <- 0
                phase1Status <- Status.Pending
                phase1Failure <- Platform.nil ()

                if running && not (Platform.isNull (box run)) then
                    let count = run.SnapshotIfReceived ()

                    if count > 0 then
                        this.Publish (run.Captured, count, StreamNotice.Silent)
                    else
                        this.SyncVisibleOn ()
                else
                    this.SyncVisibleOn ()
            elif p1Outcome = StreamPhase1Result.Phase1Failed then
                active <- 0

                let err =
                    if Platform.isNull p1Error then
                        keyStarter.Phase1Exception
                    else
                        p1Error

                phase1Status <- Status.Error
                phase1Failure <- graph.FailureOf (err, this, prevFailure)

                if running && not (Platform.isNull (box run)) then
                    let count = run.SnapshotIfReceived ()

                    if count > 0 then
                        this.Publish (run.Captured, count, StreamNotice.Silent)
                    else
                        this.SyncVisibleOn ()
                else
                    this.SyncVisibleOn ()
            elif p1Outcome = StreamPhase1Result.EqualKey then
                active <- 0
                let hadMaskedMove = maskedMove
                phase1Status <- Status.None
                phase1Failure <- Platform.nil ()

                if running && not (Platform.isNull (box run)) then
                    let count = run.SnapshotIfReceived ()

                    if count > 0 then
                        syncPublished <- true
                        this.Publish (run.Captured, count, StreamNotice.Silent)

                if hadMaskedMove then
                    maskedMove <- true

                this.SyncVisibleOn ()
            else
                phase1Status <- Status.None
                phase1Failure <- Platform.nil ()
                maskedMove <- false
                keyStarter.CommitNextKey ()
                this.Stop StreamEnd.Restarted
                generation <- generation + 1
                let gen = generation
                phase2Failure <- Platform.nil ()

                phase2Status <-
                    Status.Pending
                    ||| (phase2Status &&& Status.Uninitialized)

                this.SyncVisibleOn ()

                if graph.HasLastRaised then
                    graph.ClearLastRaised ()

                let source = new CancellationTokenSource ()
                cts <- source

                try
                    try
                        let started =
#if FABLE_COMPILER
                            graph.RunUntrackedHosted (
                                this :> IScopeHost,
                                fun () -> keyStarter.StartUntracked (this :> IStreamRequester, gen, source.Token)
                            )
#else
                            let hasJournal = not (Platform.isNull graph.Journal)

                            if hasJournal then
                                graph.Journal.EnterDerived ()

                            let saved = graph.EnterUntrackedHosted (this :> IScopeHost)

                            try
                                keyStarter.StartUntracked (this :> IStreamRequester, gen, source.Token)
                            finally
                                graph.LeaveUntrackedHosted saved

                                if hasJournal then
                                    graph.Journal.LeaveDerived ()
#endif

                        if violated then
                            raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                        if not disposed then
                            run <- started
                            running <- true
                            Tracer.StreamStarted (graph, id, gen)
                            launching <- true

                            let work =
                                try
                                    try
#if FABLE_COMPILER
                                        graph.RunUntracked (fun () -> started.RunSource ())
#else
                                        graph.RunUntrackedWith (runSource, started)
#endif
                                    with NotReadyException _ as pending ->
                                        let failure = InvalidOperationException "StreamSource.Run read a pending source."
#if FABLE_COMPILER
                                        Platform.setInner failure pending
                                        raise failure
#else
                                        raise (InvalidOperationException (failure.Message, pending))
#endif
                                finally
                                    launching <- false

                            if Platform.isNull work then
                                raise (InvalidOperationException "StreamSource.Run returned null.")

#if FABLE_COMPILER
                            Platform.whenDone work (fun outcome -> this.Ended (gen, outcome))
#else
                            if work.IsCompleted && graph.IsOnGraphThread then
                                this.Ended (gen, Platform.settledUnitOutcome work)
                            else
                                Platform.attachDone work (this :> Platform.IDoneWaiter) gen
#endif

                            if gen = generation && running && started.Received then
                                syncPublished <- true
                                this.PublishLatest StreamNotice.Silent
                    finally
                        active <- 0
                with
                | error when violated ->
                    violated <- false
                    this.Fail (ScopeMessages.failure ScopeMode.Pure error)
                | error -> this.Fail (UntrackedPhase.wrapPending UntrackedPhase.streamOnPending error)
        finally
            inLaunchOn <- false

        let unmaskedMove = maskedMove && phase1Status = Status.None

        if phase1Status = Status.None then
            maskedMove <- false

        if not disposed && not notifiedInLaunch then
            let curNorm = status &&& ~~~Status.Uninitialized
            let prevNorm = prevStatus &&& ~~~Status.Uninitialized

            let statusOrErrorMoved =
                curNorm <> prevNorm
                || (curNorm = Status.Error
                    && not (obj.ReferenceEquals (failure, prevFailure))
                    && Failure.Moved (failure, prevFailure))

            let moved =
                statusOrErrorMoved
                || unmaskedMove
                || (curNorm = Status.None
                    && syncPublished
                    && (if SafeEquality.isSafe equal then
                            not (ComparerSentinel.equals equal prevValue value)
                        else
                            this.CompareMovedGuarded prevValue))

            if moved && not notifiedInLaunch then
                Tracer.Unattributed graph
                this.NotifyOtherObservers ()
                Tracer.Notified graph
                Tracer.RunEnd (graph, id, status)

                if graph.QueuePending then
                    graph.OweFlush ()
            else
                Tracer.RunEnd (graph, id, status)
        else
            Tracer.RunEnd (graph, id, status)

        if freshness <> Freshness.Clean then
            unannounced <- true
            graph.NoteStaleTransition ()

    member private this.EnsureCurrent() =
        if freshness <> Freshness.Clean && not disposed then
            if graph.Deferring then
                this.Refresh ()
                graph.ClearLastRaisedIfTop ()
            else
                this.Pull ()

    /// <summary>
    /// <c>EnsureCurrent</c>, then links this stream into the running reader, as on <c>Memo</c>. <c>Dirty</c>, as
    /// <c>MarkDirty</c> tells readers.
    /// </summary>
    member private this.TrackCurrent() =
        if freshness <> Freshness.Clean && not disposed then
            if graph.Deferring then
                this.Refresh ()
                graph.ClearLastRaisedIfTop ()
            else
                this.Pull ()

            graph.Track (this :> ISource)

            if freshness <> Freshness.Clean && not disposed then
                if isSplit then
                    graph.MarkReaderCheck ()
                else
                    graph.MarkReaderDirty ()
        else
            graph.Track (this :> ISource)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.CheckActiveSource(i: int) =
        if freshness = Freshness.Check then
            let source = sources.SourceAt i

            if source.IsStale && source.IsActive then
                (this :> IComputation).MarkDirty()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ResolveCheckAgain() =
        let mutable again = sources.HasStaleSource ()

        while again do
            let before = graph.StaleTransitions
            let mutable i = 0

            while freshness = Freshness.Check && i < sources.Count do
                sources.RefreshSourceAt i

                if graph.ActiveSkips <> 0 then
                    this.CheckActiveSource i

                i <- i + 1

            again <-
                freshness = Freshness.Check
                && graph.StaleTransitions <> before
                && sources.HasStaleSource ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.RefreshActive() =
        graph.NoteActiveSkip ()

        if active = 2 then
            (this :> IComputation).MarkDirty()

    member private this.ResolveCheckOnly() =
        if active = 0 then
            Tracer.CheckStart (graph, id)
            active <- 2
            let before = graph.StaleTransitions
            let mutable i = 0

            while freshness = Freshness.Check && i < sources.Count do
                sources.RefreshSourceAt i

                if graph.ActiveSkips <> 0 then
                    this.CheckActiveSource i

                i <- i + 1

            if
                freshness = Freshness.Check
                && graph.StaleTransitions <> before
            then
                this.ResolveCheckAgain ()

            active <- 0
            Tracer.CheckResolved (graph, id, (freshness = Freshness.Dirty))

            if freshness = Freshness.Check then
                freshness <- Freshness.Clean
                unannounced <- false
        else
            this.RefreshActive ()

    member private this.Refresh() =
        if active = 0 then
            if freshness = Freshness.Check then
                this.ResolveCheckOnly ()

            if freshness = Freshness.Dirty then
                if isSplit then this.LaunchOn () else this.Launch ()
        else
            this.RefreshActive ()

    member private this.Pull() =
#if !FABLE_COMPILER || !RANVIER_TRACE
        if graph.TryEnterPullFast () then
            try
                this.Refresh ()
            with _ ->
                graph.ExitPullFastOnException ()
                reraise ()

            if graph.ExitPullFast () then
                graph.FinishPullSettle ()
        else
            graph.EnterPull ()

            try
                this.Refresh ()
            finally
                graph.ExitPull ()
#else
        graph.EnterPull ()

        try
            this.Refresh ()
        finally
            graph.ExitPull ()
#endif

    static member internal Create<'T>(graph: Graph, seed: 'S, fold: 'S -> 'T -> 'S, start: CancellationToken -> StreamSource<'T>, emptyFails: bool) =
        if Platform.isNull (box fold) then
            nullArg (nameof fold)

        if Platform.isNull (box start) then
            nullArg (nameof start)

        let adapted = FoldFn.adapt fold

        let starter =
            StreamStarter<'S, 'T>(graph, seed, adapted, start) :> IStreamStarter<'S>

        let memo = new StreamMemo<'S> (graph, seed, emptyFails, starter, Platform.nil ())
        memo.Attach ()
        memo

    static member internal CreateOn(graph: Graph, read: unit -> 'K, start: 'K -> CancellationToken -> StreamSource<'S>) : StreamMemo<'S> =
        if UntrackedPhase.isNullFn1 read then
            nullArg (nameof read)

        if UntrackedPhase.isNullFn2 start then
            nullArg (nameof start)

        let adaptedFold = FoldFn.adapt (fun (_: 'S) (item: 'S) -> item)
        let adaptedStart = FoldFn.adaptStart start

        let keyStarter =
            StreamKeyStarter<'K, 'S, 'S>(graph, Platform.zero<'S>(), adaptedFold, read, adaptedStart) :> IStreamKeyStarter<'S>

        let memo =
            new StreamMemo<'S> (graph, Platform.zero<'S>(), true, Platform.nil (), keyStarter)

        memo.Attach ()
        memo

    static member internal CreateFoldOn
        (graph: Graph, seed: 'S, fold: 'S -> 'T -> 'S, read: unit -> 'K, start: 'K -> CancellationToken -> StreamSource<'T>)
        : StreamMemo<'S> =
        if UntrackedPhase.isNullFn2 fold then
            nullArg (nameof fold)

        if UntrackedPhase.isNullFn1 read then
            nullArg (nameof read)

        if UntrackedPhase.isNullFn2 start then
            nullArg (nameof start)

        let adaptedFold = FoldFn.adapt fold
        let adaptedStart = FoldFn.adaptStart start

        let keyStarter =
            StreamKeyStarter<'K, 'S, 'T>(graph, seed, adaptedFold, read, adaptedStart) :> IStreamKeyStarter<'S>

        let memo = new StreamMemo<'S> (graph, seed, false, Platform.nil (), keyStarter)
        memo.Attach ()
        memo

#if RANVIER_TRACE
    interface ITracedNode with
        member _.TraceGraph = box graph
#endif

    interface INode with
        member _.Id = id
        member _.Status = status ||| Staleness.ofFreshness freshness

    interface ISource with
        member _.AddObserver(observer, slot) =
#if FABLE_COMPILER && !RANVIER_TRACE
            if Platform.isNull observers then
                observers <- ObserverList ()
#endif
            observers.Add (observer, slot)

        member _.TryAddObserver(observer, slot) =
#if FABLE_COMPILER && !RANVIER_TRACE
            if Platform.isNull observers then
                observers <- ObserverList ()
#endif
            observers.TryAdd (observer, slot)

        member _.RemoveObserverAt(observer, handle) =
#if FABLE_COMPILER && !RANVIER_TRACE
            if not (Platform.isNull observers) then
                observers.RemoveAt (observer, handle)
#else
            observers.RemoveAt (observer, handle)
#endif

        member this.UpdateIfNecessary() =
#if FABLE_COMPILER
            if freshness <> Freshness.Clean && not disposed then
                this.EnsureCurrent ()
#else
            this.EnsureCurrent ()
#endif

        member _.IsStale = freshness <> Freshness.Clean && not disposed

        member _.IsActive = active <> 0

    interface IComputation with
        member this.AddSource source =
            sources.Add (this :> IComputation, source)

        member _.MovedIn(slot, handle) =
            sources.MovedIn (slot, handle)

        member _.HasReadSlot slot =
            slot < sources.Cursor

        member this.MarkDirty() =
            if isSplit then
                if freshness <> Freshness.Dirty then
                    let wasClean = freshness = Freshness.Clean
                    freshness <- Freshness.Dirty

                    if wasClean then
                        graph.NoteStaleTransition ()
                        this.NotifyObserversCheck ()
                    elif unannounced then
                        unannounced <- false
                        graph.NoteStaleTransition ()
                        this.NotifyObserversCheck ()
                elif unannounced then
                    unannounced <- false
                    graph.NoteStaleTransition ()
                    this.NotifyObserversCheck ()
            elif freshness <> Freshness.Dirty || unannounced then
                if freshness = Freshness.Clean || unannounced then
                    graph.NoteStaleTransition ()

                freshness <- Freshness.Dirty
                unannounced <- false
                this.NotifyObserversDirty ()

        member this.MarkCheck() =
            if freshness = Freshness.Clean then
                freshness <- Freshness.Check
                unannounced <- false
                this.NotifyObserversCheck ()
            elif unannounced then
                unannounced <- false
                graph.NoteStaleTransition ()
                this.NotifyObserversCheck ()

    interface IScheduled with
        member this.Execute() =
            queued <- false

            if not disposed && running then
                if freshness = Freshness.Check then
                    if graph.Deferring then
                        this.ResolveCheckOnly ()
                        graph.ClearLastRaisedIfTop ()
                    else
                        graph.EnterPull ()

                        try
                            this.ResolveCheckOnly ()
                        finally
                            graph.ExitPull ()

                if
                    freshness = Freshness.Clean
                    && running
                    && not disposed
                then
                    let count = run.SnapshotIfReceived ()

                    if count > 0 then
                        this.PublishInFlush (run.Captured, count)
                elif not (Platform.isNull (box run)) then
                    run.OnDequeued ()
            elif not (Platform.isNull (box run)) then
                run.OnDequeued ()

    interface IStreamRequester with
        member this.RequestOnGraph() =
            this.RequestOnGraph ()
#if !FABLE_COMPILER
        member this.RequestOffGraph() =
            this.RequestOffGraph ()
#endif
        member this.ReportFoldFailure(gen, error) =
            this.ReportFoldFailure (gen, error)

    interface IScopeHost with
        member _.CanOwn = false

        member _.Scope =
            violated <- true
            raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

    interface IOwned with
        member this.Release() =
            link <- null
            this.Dispose ()

    /// <summary>The graph that owns this value.</summary>
    member _.Graph = graph
    /// <summary>The published state.</summary>
    member _.Status = status
    /// <summary>The number of stream starts, including starts that suspended or failed.</summary>
    member _.Runs = runs
    /// <summary>The last published state, read without tracking; never starts the stream or raises.</summary>
    member _.Peek = value
    /// <summary>The node the current failure originated in while <c>Status</c> has <c>Error</c>, otherwise null.</summary>
    member _.ErrorOrigin: INode = Failure.OriginOf failure

    member inline internal _.Failure = failure
    member inline internal _.IsSplit = isSplit

    /// <summary>Tracks and reads the published state; raises while it is pending or failed.</summary>
#if FABLE_COMPILER
    member this.Value: 'S =
        if StatusBits.isCleanAndReady freshness status then
            graph.Track (this :> ISource)
            value
        else
            this.ReadSlow ()
#else
    member this.Value: 'S = this.GetFast ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.GetFast() : 'S =
        if StatusBits.isCleanAndReady freshness status then
            graph.Track (this :> ISource)
            value
        else
            this.ReadSlow ()
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ReadSlow() : 'S =
        this.TrackCurrent ()

        if StatusBits.isUnready status then
            if StatusBits.isPending status then
                raise (graph.NotReady (this :> INode))

            graph.Raise failure

        value

    /// <summary>Tracks and returns the published ready, pending or failed state.</summary>
#if FABLE_COMPILER
    member this.TryValue: Reading<'S> =
        if StatusBits.isCleanAndReady freshness status then
            graph.Track (this :> ISource)
            Ready value
        else
            this.ReadTrySlow ()
#else
    member this.TryValue: Reading<'S> = this.GetTryFast ()

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private this.GetTryFast() : Reading<'S> =
        if StatusBits.isCleanAndReady freshness status then
            graph.Track (this :> ISource)
            Ready value
        else
            this.ReadTrySlow ()
#endif

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.ReadTrySlow() : Reading<'S> =
        this.TrackCurrent ()

        if StatusBits.isUnready status then
            if StatusBits.isPending status then
                Pending
            else
                Failed failure.Error
        else
            Ready value

    /// <summary>Cancels the stream and detaches from its sources; repeated disposal is harmless.</summary>
    member this.Dispose() =
        graph.Entered (
            "Disposing a node",
            fun () ->
                if not disposed then
                    disposed <- true
                    Tracer.NodeDispose (graph, id)

                    if not (Platform.isNull link) then
                        link.Detach ()
                        link <- null

                    sources.Clear (this :> IComputation)
                    this.Stop StreamEnd.Disposed

                    if StatusBits.isPending status then
                        phase1Status <- Status.None
                        failure <- Failure (ObjectDisposedException (this.GetType().Name), (this :> INode))
                        status <- Status.Error
                        this.Notify ()
        )

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()

/// <summary>Stream sources from common item producers.</summary>
[<RequireQualifiedAccess; CompilationRepresentation(CompilationRepresentationFlags.ModuleSuffix)>]
module StreamSource =
    /// <summary>A source whose every start calls <c>run</c>.</summary>
    /// <exception cref="T:System.ArgumentNullException"><c>run</c> is null.</exception>
    let create (run: StreamSink<'T> -> CancellationToken -> Task) : StreamSource<'T> =
        if Platform.isNull (box run) then
            nullArg (nameof run)

        { new StreamSource<'T>() with
            member _.Run(sink, token) =
                run sink token
        }

    let private cancelled (token: CancellationToken) : exn =
#if FABLE_COMPILER
        OperationCanceledException "The operation was canceled."
#else
        OperationCanceledException token
#endif

    let inline private holdObservable (gate: obj) ([<InlineIfLambda>] body: unit -> unit) =
#if FABLE_COMPILER
        ignore gate
        body ()
#else
        lock gate body
#endif

    /// <summary>Subscribes on each start: <c>OnNext</c> delivers, <c>OnCompleted</c> completes, <c>OnError</c> fails.</summary>
    /// <remarks>Cancellation disposes the subscription and faults the task with <c>OperationCanceledException</c>.</remarks>
    let ofObservable (items: IObservable<'T>) : StreamSource<'T> =
        if Platform.isNull (box items) then
            nullArg (nameof items)

        create (fun sink token ->
            let finished = Platform.completion<unit>()
            let mutable doneFlag = false
            let mutable reg = Unchecked.defaultof<CancellationTokenRegistration>
            let mutable sub: IDisposable = null
            let mutable observer = Unchecked.defaultof<IObserver<'T>>

            let disposeSub (s: IDisposable) =
                if not (Platform.isNull s) then
                    try
                        s.Dispose ()
                    with _ ->
                        ()

            let finish ok (error: exn) =
                let mutable shouldFinish = false
                let mutable toDisposeReg = Unchecked.defaultof<CancellationTokenRegistration>
                let mutable toDisposeSub: IDisposable = null

                holdObservable observer (fun () ->
                    if not doneFlag then
                        doneFlag <- true
                        toDisposeReg <- reg
                        toDisposeSub <- sub
                        sub <- null
                        shouldFinish <- true)

                if shouldFinish then
                    Platform.disposeRegistration toDisposeReg
                    disposeSub toDisposeSub

                    if ok then finished.Resolve () else finished.Reject error

            observer <-
                { new IObserver<'T> with
                    member _.OnNext item =
                        sink.Next item

                    member _.OnCompleted() =
                        finish true null

                    member _.OnError error =
                        finish false error
                }

            let subscription = items.Subscribe observer
            let mutable disposeSubNow = false

            holdObservable observer (fun () ->
                if doneFlag then
                    disposeSubNow <- true
                else
                    sub <- subscription)

            if disposeSubNow then
                disposeSub subscription
            else
                let registered =
                    token.Register (fun () ->
                        let mutable shouldCancel = false
                        let mutable toDisposeSub: IDisposable = null

                        holdObservable observer (fun () ->
                            if not doneFlag then
                                doneFlag <- true
                                toDisposeSub <- sub
                                sub <- null
                                shouldCancel <- true)

                        if shouldCancel then
                            disposeSub toDisposeSub
                            finished.Reject (cancelled token))

                let mutable disposeRegNow = false

                holdObservable observer (fun () ->
                    if doneFlag then
                        disposeRegNow <- true
                    else
                        reg <- registered)

                if disposeRegNow then
                    Platform.disposeRegistration registered

            finished.Task :> Task)

#if !FABLE_COMPILER
    [<Sealed>]
    type private AsyncEnumerablePump<'T>
        (enumerator: Collections.Generic.IAsyncEnumerator<'T>, sink: StreamSink<'T>, token: CancellationToken, finished: Platform.Completion<unit>) =
        let mutable pendingAwaiter =
            Unchecked.defaultof<Runtime.CompilerServices.ConfiguredValueTaskAwaitable<bool>.ConfiguredValueTaskAwaiter>

        let mutable onMoveNext: Action = null
        let mutable onDisposed: Action<Task> = null
        let mutable terminalError: exn = null

        member private this.ClassifyError(error: exn) : exn =
            if
                (error :? OperationCanceledException)
                && token.IsCancellationRequested
            then
                cancelled token
            else
                error

        member private _.OnDisposed(d: Task) =
            let err = terminalError

            if not (isNull err) then
                finished.Reject err
            elif d.IsFaulted then
                finished.Reject (d.Exception.GetBaseException ())
            else
                finished.Resolve ()

        member private this.Finish(error: exn) =
            let mutable disposal = Unchecked.defaultof<ValueTask>
            let mutable disposeFault: exn = null

            try
                disposal <- enumerator.DisposeAsync ()
            with ex ->
                disposeFault <- ex

            if not (isNull disposeFault) then
                if not (isNull error) then
                    finished.Reject error
                else
                    finished.Reject disposeFault
            elif disposal.IsCompletedSuccessfully then
                disposal.GetAwaiter().GetResult()

                if not (isNull error) then
                    finished.Reject error
                else
                    finished.Resolve ()
            else
                terminalError <- error

                if isNull onDisposed then
                    onDisposed <- Action<Task> this.OnDisposed

                disposal.AsTask().ContinueWith(onDisposed, TaskContinuationOptions.ExecuteSynchronously)
                |> ignore

        member private this.OnMoveNext() =
            let a = pendingAwaiter
            pendingAwaiter <- Unchecked.defaultof<_>
            let mutable hasMore = false
            let mutable fault: exn = null

            try
                hasMore <- a.GetResult ()
            with error ->
                fault <- error

            if not (isNull fault) then
                this.Finish (this.ClassifyError fault)
            elif hasMore then
                try
                    sink.Next enumerator.Current
                    this.Pump ()
                with error ->
                    this.Finish error
            else
                this.Finish null

        member this.Pump() =
            try
                let mutable next = enumerator.MoveNextAsync ()
                let mutable go = true

                while go && next.IsCompleted do
                    let mutable hasMore = false
                    let mutable fault: exn = null

                    try
                        hasMore <- next.GetAwaiter().GetResult()
                    with error ->
                        fault <- error

                    if not (isNull fault) then
                        go <- false
                        this.Finish (this.ClassifyError fault)
                    elif hasMore then
                        sink.Next enumerator.Current
                        next <- enumerator.MoveNextAsync ()
                    else
                        go <- false
                        this.Finish null

                if go then
                    let a = next.ConfigureAwait(false).GetAwaiter()
                    pendingAwaiter <- a

                    if isNull onMoveNext then
                        onMoveNext <- Action this.OnMoveNext

                    a.UnsafeOnCompleted onMoveNext
            with error ->
                this.Finish (this.ClassifyError error)

    /// <summary>
    /// Enumerates on each start with the stream's token. Items already available are delivered without an
    /// intervening continuation. <c>DisposeAsync</c> runs once, on completion, failure or cancellation.
    /// </summary>
    let ofAsyncEnumerable (items: Collections.Generic.IAsyncEnumerable<'T>) : StreamSource<'T> =
        if Platform.isNull (box items) then
            nullArg (nameof items)

        create (fun sink token ->
            let finished = Platform.completion<unit>()
            let enumerator = items.GetAsyncEnumerator token
            let pump = AsyncEnumerablePump<'T>(enumerator, sink, token, finished)
            pump.Pump ()
            finished.Task :> Task)
#endif

#if NET8_0_OR_GREATER && !FABLE_COMPILER
    [<Sealed>]
    type private ChannelPump<'T>
        (reader: Channels.ChannelReader<'T>, sink: StreamSink<'T>, token: CancellationToken, finished: Platform.Completion<unit>) =
        let mutable pendingAwaiter =
            Unchecked.defaultof<Runtime.CompilerServices.ConfiguredValueTaskAwaitable<bool>.ConfiguredValueTaskAwaiter>

        let mutable onWaitReady: Action = null

        member private _.Drain() =
            let mutable item = Unchecked.defaultof<'T>

            while not token.IsCancellationRequested
                  && reader.TryRead &item do
                sink.Next item

        member private _.FinishCompleted(wait: ValueTask<bool>) =
            let mutable fault: exn = null

            try
                wait.GetAwaiter().GetResult() |> ignore
            with error ->
                fault <- error

            if not (isNull fault) then
                if
                    (fault :? OperationCanceledException)
                    || token.IsCancellationRequested
                then
                    finished.Reject (cancelled token)
                else
                    finished.Reject fault
            elif token.IsCancellationRequested then
                finished.Reject (cancelled token)
            else
                finished.Resolve ()

        member private this.OnWaitReady() =
            let a = pendingAwaiter
            pendingAwaiter <- Unchecked.defaultof<_>
            let mutable hasMore = false
            let mutable fault: exn = null

            try
                hasMore <- a.GetResult ()
            with error ->
                fault <- error

            if not (isNull fault) then
                if
                    (fault :? OperationCanceledException)
                    || token.IsCancellationRequested
                then
                    finished.Reject (cancelled token)
                else
                    finished.Reject fault
            elif token.IsCancellationRequested then
                finished.Reject (cancelled token)
            elif hasMore then
                try
                    this.Drain ()
                    this.Pump ()
                with error ->
                    finished.Reject error
            else
                finished.Resolve ()

        member this.Pump() =
            try
                let mutable wait = reader.WaitToReadAsync token

                while wait.IsCompletedSuccessfully
                      && wait.Result
                      && not token.IsCancellationRequested do
                    this.Drain ()
                    wait <- reader.WaitToReadAsync token

                if wait.IsCompleted then
                    this.FinishCompleted wait
                else
                    let a = wait.ConfigureAwait(false).GetAwaiter()
                    pendingAwaiter <- a

                    if isNull onWaitReady then
                        onWaitReady <- Action this.OnWaitReady

                    a.UnsafeOnCompleted onWaitReady
            with error ->
                if
                    (error :? OperationCanceledException)
                    && token.IsCancellationRequested
                then
                    finished.Reject (cancelled token)
                else
                    finished.Reject error

    /// <summary>
    /// Reads on each start: waits with the stream's token, then delivers every buffered item before waiting again, and
    /// reads nothing once the token is cancelled. The stream completes when the channel completes, and fails with the
    /// exception passed to the writer's <c>Complete</c>.
    /// </summary>
    let ofChannel (reader: Channels.ChannelReader<'T>) : StreamSource<'T> =
        if Platform.isNull (box reader) then
            nullArg (nameof reader)

        create (fun sink token ->
            let finished = Platform.completion<unit>()
            let pump = ChannelPump<'T>(reader, sink, token, finished)
            pump.Pump ()
            finished.Task :> Task)
#endif

#if FABLE_COMPILER
    [<Fable.Core.Emit("$0[Symbol.asyncIterator]()")>]
    let private iteratorOf (items: Fable.Core.JS.AsyncIterable<'T>) : obj =
        Fable.Core.Util.jsNative

    [<Fable.Core.Emit("$0.next()")>]
    let private nextOf (iterator: obj) : Task<obj> =
        Fable.Core.Util.jsNative

    [<Fable.Core.Emit("($0.return ? Promise.resolve($0.return()).catch(() => {}) : undefined)")>]
    let private returnOf (iterator: obj) : unit =
        Fable.Core.Util.jsNative

    [<Fable.Core.Emit("$0.done")>]
    let private isDone (result: obj) : bool =
        Fable.Core.Util.jsNative

    [<Fable.Core.Emit("$0.value")>]
    let private valueOf (result: obj) : 'T =
        Fable.Core.Util.jsNative

    /// <summary>
    /// Iterates on each start. Cancellation calls the iterator's <c>return ()</c> and faults the task with
    /// <c>OperationCanceledException</c>.
    /// </summary>
    let ofAsyncIterable (items: Fable.Core.JS.AsyncIterable<'T>) : StreamSource<'T> =
        if Platform.isNull (box items) then
            nullArg (nameof items)

        create (fun sink token ->
            let finished = Platform.completion<unit>()
            let iterator = iteratorOf items
            let mutable stopped = false
            let mutable reg = Unchecked.defaultof<CancellationTokenRegistration>

            reg <-
                token.Register (fun () ->
                    stopped <- true

                    try
                        returnOf iterator
                    with _ ->
                        ()

                    finished.Reject (cancelled token))

            let rec step () =
                Platform.whenSettled (nextOf iterator) (fun outcome ->
                    if not stopped then
                        match outcome with
                        | Platform.FlightOutcome.Completed result when isDone result ->
                            Platform.disposeRegistration reg
                            finished.Resolve ()
                        | Platform.FlightOutcome.Completed result ->
                            sink.Next (valueOf<'T> result)
                            step ()
                        | Platform.FlightOutcome.Faulted error
                        | Platform.FlightOutcome.Canceled error ->
                            Platform.disposeRegistration reg
                            finished.Reject error)
                |> ignore

            step ()
            finished.Task :> Task)
#endif
