namespace Ranvier

open System
open System.Threading
open System.Threading.Tasks

/// <summary>The receiver of a running stream's items.</summary>
[<Sealed>]
type StreamSink<'T> internal (deliver: 'T -> unit) =
    /// <summary>Folds <c>item</c> into the stream's state. Callable from any thread; calls must not overlap.</summary>
    member _.Next(item: 'T) =
        deliver item

/// <summary>A producer of items, run once per stream start.</summary>
[<AbstractClass>]
type StreamSource<'T>() =
    /// <summary>
    /// Delivers items to <c>sink</c> until the stream ends. The task completes when the stream completes, faults when it
    /// fails, and is cancelled or faults with <c>OperationCanceledException</c> after <c>token</c> is cancelled.
    /// </summary>
    abstract Run: sink: StreamSink<'T> * token: CancellationToken -> Task

/// <summary>The state of one started stream, shared by its producer and the graph thread.</summary>
type internal IStreamRun<'S> =
    /// <summary>The state, and the number of items folded since the previous snapshot.</summary>
    abstract Snapshot: unit -> struct ('S * int)
    /// <summary>True once an item has been folded.</summary>
    abstract Received: bool
    /// <summary>Ignores every later item.</summary>
    abstract Close: unit -> unit

/// <summary>A started stream's accumulator. Items fold under a lock on the delivering thread.</summary>
[<Sealed>]
type internal StreamRun<'S, 'T>(seed: 'S, fold: 'S -> 'T -> 'S, wake: unit -> unit, failed: exn -> unit) =
    let gate = obj ()
    let mutable state = seed
    let mutable folded = 0
    let mutable received = false
    let mutable closed = false
    let mutable foldError: exn = null

    /// <summary>Folds <c>item</c> and requests a wake; a throwing fold closes the run and reports the exception.</summary>
    member _.Deliver(item: 'T) =
        let outcome =
            lock gate (fun () ->
                if closed then
                    0
                else
                    try
                        state <- fold state item
                        folded <- folded + 1
                        received <- true
                        1
                    with error ->
                        closed <- true
                        foldError <- error
                        2)

        if outcome = 1 then
            wake ()
        elif outcome = 2 then
            failed foldError

    interface IStreamRun<'S> with
        member _.Snapshot() =
            lock gate (fun () ->
                let count = folded
                folded <- 0
                struct (state, count))

        member _.Received = lock gate (fun () -> received)

        member _.Close() =
            lock gate (fun () -> closed <- true)

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
type StreamMemo<'S>
    internal
    (
        graph: Graph,
        seed: 'S,
        emptyFails: bool,
        launch: (unit -> unit) -> (exn -> unit) -> CancellationToken -> struct (IStreamRun<'S> * (unit -> Task))
    ) as this =
    let id = graph.NextId ()
    let observers = ObserverSet ()
    do Tracer.Bind (observers, graph, id)
    let sources = SourceList ()
    do Tracer.Bind (sources, graph, id)
    let equal = graph.Options.Equality.Comparer<'S>()
    let mutable freshness = Freshness.Dirty
    let mutable status = Status.Pending ||| Status.Uninitialized
    let mutable value = Unchecked.defaultof<'S>
    let mutable failure: Failure = null
    let mutable runs = 0
    let mutable disposed = false
    let mutable violated = false
    let mutable link: OwnerLink = null
    let mutable generation = 0
    let mutable run: IStreamRun<'S> = Unchecked.defaultof<_>
    let mutable cts: CancellationTokenSource = null
    let mutable running = false
    let mutable launching = false
    let mutable queued = false
    let mutable wakePosted = 0

    let wake (gen: int) () =
#if FABLE_COMPILER
        wakePosted <- 0
#else
        Interlocked.Exchange (&wakePosted, 0) |> ignore
#endif
        if not disposed && gen = generation then
            this.Enqueue ()
            graph.RequestFlush ()

    member private this.Attach() =
        link <- graph.CurrentOwner.AttachLinked this
        Tracer.StreamNew (graph, id, link.Owner)

    member private this.Enqueue() =
        if not queued then
            queued <- true
            graph.Schedule (this :> IScheduled)

    /// <summary>Requests one wake while none is outstanding; items folded during <c>Run</c>'s synchronous part need none.</summary>
    member private _.Request(gen: int) =
        if not (launching && graph.IsOnGraphThread) then
#if FABLE_COMPILER
            let previous = wakePosted
            wakePosted <- 1
#else
            let previous = Interlocked.Exchange (&wakePosted, 1)
#endif
            if previous = 0 then
                graph.Post (wake gen)

    member private _.Notify() =
        observers.NotifyDirtyExcept graph.CurrentComputation
        Tracer.Notified graph
        graph.RequestFlush ()

    /// <summary>Closes the open stream and cancels its token.</summary>
    member private _.Stop(reason: StreamEnd) =
        if not (Platform.isNull (box run)) then
            run.Close ()

        if running then
            running <- false
            Tracer.StreamEnded (graph, id, int reason)

        if not (Platform.isNull cts) then
            let source = cts
            cts <- null
            source.Cancel ()
            source.Dispose ()

    member private this.Fail(error: exn) =
        this.Stop StreamEnd.Failed
        let next = graph.FailureOf (error, this, failure)

        let moved =
            status <> Status.Error
            || Failure.Moved (next, failure)

        failure <- next
        status <- Status.Error
        Tracer.StreamPublished (graph, id, int (byte status), 0, box error)

        if moved then
            this.Notify ()

    member private this.Publish(state: 'S, count: int, notify: bool) =
        try
            let moved =
                status <> Status.None
                || not (equal.Equals (value, state))

            value <- state
            status <- Status.None
            failure <- null
            Tracer.StreamPublished (graph, id, int (byte status), count, box state)

            if moved && notify then
                this.Notify ()
        with error ->
            this.Fail error

    member private this.PublishLatest(notify: bool) =
        let struct (state, count) = run.Snapshot ()
        this.Publish (state, count, notify)

    member private this.Ended(gen: int, outcome: Platform.FlightOutcome<unit>) =
        if not disposed && gen = generation && running then
            match outcome with
            | Platform.FlightOutcome.Completed _ ->
                let received = run.Received
                this.Stop StreamEnd.Completed

                if received then
                    this.PublishLatest true
                elif emptyFails then
                    this.Fail (InvalidOperationException "The stream completed without an item.")
                else
                    this.Publish (seed, 0, true)
            | Platform.FlightOutcome.Faulted error
            | Platform.FlightOutcome.Canceled error -> this.Fail error

    member private this.FoldFailed(gen: int, error: exn) =
        if not disposed && gen = generation && running then
            this.Fail error

    member private this.Launch() =
        freshness <- Freshness.Clean
        this.Stop StreamEnd.Restarted
        generation <- generation + 1
        let gen = generation
        runs <- runs + 1
        Tracer.RunStart (graph, id, runs)
        failure <- null

        status <-
            Status.Pending
            ||| (status &&& Status.Uninitialized)

        sources.BeginRun ()
        let source = new CancellationTokenSource ()
        cts <- source

        try
            try
                let struct (started, body) =
                    graph.RunHosted (
                        this :> IComputation,
                        fun () -> launch (fun () -> this.Request gen) (fun error -> graph.Post (fun () -> this.FoldFailed (gen, error))) source.Token
                    )

                if violated then
                    raise (InvalidOperationException (ScopeMessages.forMode ScopeMode.Pure))

                run <- started
                running <- true
                Tracer.StreamStarted (graph, id, gen)
                launching <- true

                let work =
                    try
                        graph.RunUntracked body
                    finally
                        launching <- false

                if Platform.isNull work then
                    raise (InvalidOperationException "StreamSource.Run returned null.")

                Platform.whenDone work (fun outcome -> graph.Post (fun () -> this.Ended (gen, outcome)))

                if gen = generation && running && started.Received then
                    this.PublishLatest false
            finally
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

    member private this.EnsureCurrent() =
        if freshness <> Freshness.Clean && not disposed then
            this.Pull ()

    member private this.Pull() =
        graph.EnterPull ()

        try
            if freshness = Freshness.Check then
                Tracer.CheckStart (graph, id)
                let mutable i = 0

                while freshness = Freshness.Check && i < sources.Count do
                    sources.SourceAt(i).UpdateIfNecessary()
                    i <- i + 1

                Tracer.CheckResolved (graph, id, (freshness = Freshness.Dirty))

                if freshness = Freshness.Check then
                    freshness <- Freshness.Clean

            if freshness = Freshness.Dirty then
                this.Launch ()
        finally
            graph.ExitPull ()

    static member internal Create<'T>(graph: Graph, seed: 'S, fold: 'S -> 'T -> 'S, start: CancellationToken -> StreamSource<'T>, emptyFails: bool) =
        if Platform.isNull (box fold) then
            nullArg (nameof fold)

        if Platform.isNull (box start) then
            nullArg (nameof start)

        let launch wake failed (token: CancellationToken) =
            let source = start token

            if Platform.isNull (box source) then
                raise (InvalidOperationException "The stream's start function returned null.")

            let started = StreamRun<'S, 'T>(seed, fold, wake, failed)
            struct (started :> IStreamRun<'S>, (fun () -> source.Run (StreamSink<'T> started.Deliver, token)))

        let memo = new StreamMemo<'S> (graph, seed, emptyFails, launch)
        memo.Attach ()
        memo

    interface INode with
        member _.Id = id
        member _.Status = status

    interface ISource with
        member _.AddObserver observer =
            observers.Add observer

        member _.RemoveObserver observer =
            observers.Remove observer

        member this.UpdateIfNecessary() =
            this.EnsureCurrent ()

    interface IComputation with
        member this.AddSource source =
            sources.Add (this :> IComputation, source)

        member _.MarkDirty() =
            if freshness <> Freshness.Dirty then
                freshness <- Freshness.Dirty
                observers.NotifyDirty ()

        member _.MarkCheck() =
            if freshness = Freshness.Clean then
                freshness <- Freshness.Check
                observers.NotifyCheck ()

    interface IScheduled with
        member this.Execute() =
            queued <- false

            if
                not disposed
                && running
                && freshness = Freshness.Clean
            then
                this.PublishLatest true

    interface IScopeHost with
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
    /// <summary>The last published state, read without tracking, starting the stream or raising.</summary>
    member _.Peek = value
    /// <summary>The node the current failure originated in while <c>Status</c> has <c>Error</c>, otherwise null.</summary>
    member _.ErrorOrigin: INode = Failure.OriginOf failure

    /// <summary>Tracks and reads the published state; raises while it is pending or failed.</summary>
    member this.Value: 'S =
        this.EnsureCurrent ()
        graph.Track (this :> ISource)

        if status.HasFlag Status.Pending then
            raise (graph.NotReady (this :> INode))

        if status.HasFlag Status.Error then
            graph.Raise failure

        value

    /// <summary>Tracks and returns the published ready, pending or failed state.</summary>
    member this.TryValue: Reading<'S> =
        this.EnsureCurrent ()
        graph.Track (this :> ISource)

        if status.HasFlag Status.Pending then Pending
        elif status.HasFlag Status.Error then Failed failure.Error
        else Ready value

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

                    if status.HasFlag Status.Pending then
                        failure <- Failure (ObjectDisposedException (this.GetType().Name), (this :> INode))
                        status <- Status.Error
                        this.Notify ()
        )

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()
