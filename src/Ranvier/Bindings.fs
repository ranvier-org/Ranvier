namespace Ranvier

open System

/// <summary>
/// Writes a source's value to a target on the next flush and after every change of the source.
/// </summary>
/// <remarks>
/// The source is fixed at creation. Writes run in the flush, untracked, and must not create reactive nodes. A pending
/// or failed memo source leaves the target unchanged. Disposed with the owner current at creation, or by
/// <c>Dispose</c>.
/// </remarks>
[<Sealed>]
type internal Binding<'Target, 'T>
    private (graph: Graph, source: ISource, signal: Signal<'T>, memo: Memo<'T>, target: 'Target, write: 'Target -> 'T -> unit) =
    let id = graph.NextId ()
    let mutable freshness = Freshness.Clean
    let mutable status = Status.Uninitialized
    let mutable failure: Failure = Platform.nil ()
    let mutable queued = false
    let mutable disposed = false
    let mutable link: OwnerLink = Platform.nil ()
    let mutable runs = 0
    let mutable handle = EdgeHandle.Unlinked
#if RANVIER_TRACE
    let mutable traceLog: TraceLog = null
    let mutable traceId = 0
#endif

#if !FABLE_COMPILER
    let writeFn = OptimizedClosures.FSharpFunc<'Target, 'T, unit>.Adapt write

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)>]
    member private _.WriteTarget(value: 'T) =
        writeFn.Invoke (target, value)
#endif

    static member internal OfSignal(graph: Graph, source: Signal<'T>, target: 'Target, write: 'Target -> 'T -> unit) =
        let binding =
            new Binding<'Target, 'T> (graph, source :> ISource, source, Platform.nil (), target, write)

        binding.Start ()
        binding

    static member internal OfMemo(graph: Graph, source: Memo<'T>, target: 'Target, write: 'Target -> 'T -> unit) =
        let binding =
            new Binding<'Target, 'T> (graph, source.Node :> ISource, Platform.nil (), source, target, write)

        binding.Start ()
        binding

    member private this.Start() =
        let owner = graph.CurrentOwner
        link <- owner.AttachLinked this
        Tracer.BindingNew (graph, id, owner)

        if not disposed then
            Tracer.Bind (this, graph, id)
            Tracer.EdgeAdd (this, source, 0)
            handle <- source.AddObserver (this :> IComputation, 0)
            (this :> IComputation).MarkDirty()
            graph.RequestFlush ()

#if !FABLE_COMPILER
    member private this.Run() =
        runs <- runs + 1
        Tracer.RunStart (graph, id, runs)

        try
            if Platform.isNull (box memo) then
                status <- Status.None

                if not (Platform.isNull failure) then
                    failure <- Platform.nil ()

                this.WriteTarget signal.Peek
            elif memo.IsCleanAndReady then
                status <- Status.None

                if not (Platform.isNull failure) then
                    failure <- Platform.nil ()

                this.WriteTarget memo.Peek
            else
                match memo.ReadUntracked () with
                | Ready value ->
                    status <- Status.None

                    if not (Platform.isNull failure) then
                        failure <- Platform.nil ()

                    this.WriteTarget value
                | Pending -> status <- Status.Pending
                | Failed _ ->
                    status <- Status.Error
                    failure <- memo.Failure
        with ex ->
            failure <- graph.FailureOf (ex, this, Platform.nil ())
            status <- Status.Error

        Tracer.RunEnd (graph, id, status)
#endif

    /// <summary>The state after the latest run.</summary>
    member _.Status = status

    /// <summary>
    /// The number of runs, counting runs that left the target unchanged because the source was pending or failed.
    /// </summary>
    member _.Runs = runs

    /// <summary>The exception from the latest failed write or failed source, or null.</summary>
    member _.Error: exn = Failure.ErrorOf failure

    /// <summary>Stops the binding and releases its source edge. Idempotent.</summary>
    member this.Dispose() =
        graph.Entered (
            "Disposing a node",
            fun () ->
                if not disposed then
                    Tracer.NodeDispose (graph, id)
                    Tracer.EdgesRemove (this, 0)
                    disposed <- true

                    if not (Platform.isNull link) then
                        link.Detach ()
                        link <- Platform.nil ()

                    source.RemoveObserverAt (this :> IComputation, handle)
                    handle <- EdgeHandle.Unlinked
        )

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()

    interface IOwned with
        member this.Release() =
            link <- Platform.nil ()
            this.Dispose ()

#if RANVIER_TRACE
    interface ITracedNode with
        member _.TraceGraph = box graph
#endif

    interface INode with
        member _.Id = id
        member _.Status = status ||| Staleness.ofFreshness freshness

    interface IComputation with
        member _.AddSource _ = ()

        member _.MovedIn(_, moved) =
            handle <- moved

        member _.HasReadSlot _ = false

        member this.MarkDirty() =
            if not disposed then
                freshness <- Freshness.Dirty

                if not queued then
                    queued <- true
                    graph.Schedule (this :> IScheduled)

        member this.MarkCheck() =
            if not disposed then
                if freshness = Freshness.Clean then
                    freshness <- Freshness.Check

                if not queued then
                    queued <- true
                    graph.Schedule (this :> IScheduled)

    interface IScheduled with
        member this.Execute() =
            if disposed then
                queued <- false
            else
                // `queued` stays set until the source is current: a memo recomputing during `UpdateIfNecessary`
                // marks this binding Dirty without re-queueing it.
                if freshness = Freshness.Check then
                    Tracer.CheckStart (graph, id)

                    if not (Platform.isNull (box memo)) then
                        while freshness = Freshness.Check && memo.IsStaleNode do
                            memo.EnsureCurrent ()

                            if
                                freshness = Freshness.Check
                                && memo.IsStaleNode
                                && memo.IsActiveNode
                            then
                                (this :> IComputation).MarkDirty()

                    Tracer.CheckResolved (graph, id, (freshness = Freshness.Dirty))
                elif not (Platform.isNull (box memo)) then
                    memo.EnsureCurrent ()

                let shouldRun = freshness = Freshness.Dirty
                queued <- false
                freshness <- Freshness.Clean

                if shouldRun && not disposed then
#if FABLE_COMPILER
                    runs <- runs + 1
                    Tracer.RunStart (graph, id, runs)

                    try
                        if Platform.isNull (box memo) then
                            status <- Status.None

                            if not (Platform.isNull failure) then
                                failure <- Platform.nil ()

                            write target signal.Peek
                        elif memo.IsCleanAndReady then
                            status <- Status.None

                            if not (Platform.isNull failure) then
                                failure <- Platform.nil ()

                            write target memo.Peek
                        else
                            match memo.ReadUntracked () with
                            | Ready value ->
                                status <- Status.None

                                if not (Platform.isNull failure) then
                                    failure <- Platform.nil ()

                                write target value
                            | Pending -> status <- Status.Pending
                            | Failed _ ->
                                status <- Status.Error
                                failure <- memo.Failure
                    with ex ->
                        failure <- graph.FailureOf (ex, this, Platform.nil ())
                        status <- Status.Error

                    Tracer.RunEnd (graph, id, status)
#else
                    this.Run ()
#endif

#if RANVIER_TRACE
    interface ITraced with
        member _.TraceLog
            with get () = traceLog
            and set log = traceLog <- log

        member _.TraceId
            with get () = traceId
            and set id = traceId <- id

    interface ITracedEdges with
        member _.Owner = traceId
        member _.IsSources = true
        member _.Ids = if disposed then [||] else [| source.Id |]
#endif
