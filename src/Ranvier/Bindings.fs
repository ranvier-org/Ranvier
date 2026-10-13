namespace Ranvier

open System

/// <summary>
/// Writes one source's value to a target whenever the source changes, without dependency tracking.
/// </summary>
/// <remarks>
/// The source is fixed at creation. Writes run in the flush, untracked, and must not create reactive nodes. A pending
/// or failed memo source leaves the target unchanged. Disposed with the owner current at creation, or by
/// <c>Dispose</c>.
/// </remarks>
[<Sealed>]
type Binding<'Target, 'T> private (graph: Graph, source: ISource, signal: Signal<'T>, memo: Memo<'T>, target: 'Target, write: 'Target -> 'T -> unit) =
    let id = graph.NextId ()
    let mutable freshness = Freshness.Clean
    let mutable status = Status.Uninitialized
    let mutable failure: Failure = null
    let mutable queued = false
    let mutable disposed = false
    let mutable link: OwnerLink = null
    let mutable runs = 0

    static member internal OfSignal(graph: Graph, source: Signal<'T>, target: 'Target, write: 'Target -> 'T -> unit) =
        let binding =
            new Binding<'Target, 'T> (graph, source :> ISource, source, Unchecked.defaultof<_>, target, write)

        binding.Start ()
        binding

    static member internal OfMemo(graph: Graph, source: Memo<'T>, target: 'Target, write: 'Target -> 'T -> unit) =
        let binding =
            new Binding<'Target, 'T> (graph, source :> ISource, Unchecked.defaultof<_>, source, target, write)

        binding.Start ()
        binding

    member private this.Start() =
        source.AddObserver (this :> IComputation)
        link <- graph.CurrentOwner.AttachLinked this
        Tracer.EffectNew (graph, id, link.Owner)
        (this :> IComputation).MarkDirty()
        graph.RequestFlush ()

    member private this.Run() =
        runs <- runs + 1
        Tracer.RunStart (graph, id, runs)

        try
            if isNull (box memo) then
                status <- Status.None
                failure <- null
                write target signal.Peek
            else
                match memo.ReadUntracked () with
                | Ready value ->
                    status <- Status.None
                    failure <- null
                    write target value
                | Pending -> status <- Status.Pending
                | Failed _ ->
                    status <- Status.Error
                    failure <- memo.Failure
        with ex ->
            failure <- graph.FailureOf (ex, this, null)
            status <- Status.Error

        Tracer.RunEnd (graph, id, status)

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
                    disposed <- true
                    Tracer.NodeDispose (graph, id)

                    if not (isNull link) then
                        link.Detach ()
                        link <- null

                    source.RemoveObserver (this :> IComputation)
        )

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()

    interface IOwned with
        member this.Release() =
            link <- null
            this.Dispose ()

    interface INode with
        member _.Id = id
        member _.Status = status

    interface IComputation with
        member _.AddSource _ = ()

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
                // The source is brought current while `queued` is still set, so a memo source that recomputes to a
                // new value marks this binding Dirty without queueing it again. A Dirty binding updates its source
                // too: the memo's first computation notifies its observers.
                if freshness = Freshness.Check then
                    Tracer.CheckStart (graph, id)
                    source.UpdateIfNecessary ()
                    Tracer.CheckResolved (graph, id, (freshness = Freshness.Dirty))
                else
                    source.UpdateIfNecessary ()

                let shouldRun = freshness = Freshness.Dirty
                queued <- false
                freshness <- Freshness.Clean

                if shouldRun then
                    this.Run ()
