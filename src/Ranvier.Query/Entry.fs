namespace Ranvier.Query

open System
open System.Threading
open System.Threading.Tasks
open Ranvier

/// <summary>Cached data, request state, and lazy initial-load suspension source for an entry.</summary>
[<AllowNullLiteral; AbstractClass>]
type internal QueryEntry<'T>(session: QuerySession) =
    let graph = session.Graph

    let data =
        Signal<'T>.OfEncoded(graph, Unchecked.defaultof<'T>, PublicationComparer<_>.Instance)

    let mutable hasData = false

    let metadata =
        Signal<QueryMeta>
            .OfEncoded(
                graph,
                {
                    FetchStatus = FetchStatus.Idle
                    Error = None
                    IsStale = true
                },
                PublicationComparer<_>.Instance
            )

    let mutable gate: Signal<AsyncSource<'T>> = Unchecked.defaultof<_>
    let equal = QueryValueComparer.encode<'T> graph
    let mutable generation = 0L
    let mutable disposed = false
    let mutable leaseCount = 0
    let mutable cancellation: CancellationTokenSource = null
    let mutable waiters: ResizeArray<QueryAwaiter<'T>> = null

    let mutable invokeFetchDelegate: CancellationToken -> Task<'T> =
        Unchecked.defaultof<_>

    let mutable beginRequestBatchAction: unit -> unit = Unchecked.defaultof<_>
    let mutable publishOkBatchAction: unit -> unit = Unchecked.defaultof<_>
    let mutable publishErrorBatchAction: unit -> unit = Unchecked.defaultof<_>
    let mutable pendingValue: 'T = Unchecked.defaultof<'T>
    let mutable pendingChanged = false
    let mutable pendingSettleGate = false
    let mutable pendingError: exn = null
    let mutable draftStaged = false
    let mutable draftHasValue = false
    let mutable draftValue: 'T = Unchecked.defaultof<'T>
    let mutable draftStale = true
    let mutable draftUpdated = false
    let mutable draftChanged = false

    let takeWaiters () : QueryAwaiter<'T>[] =
        if isNull waiters || waiters.Count = 0 then
            null
        else
            let current = waiters.ToArray ()
            waiters.Clear ()
            current

    let cancelWaiters (pending: QueryAwaiter<'T>[]) =
        if not (isNull pending) then
            for i = 0 to pending.Length - 1 do
                pending[i].Cancel()

    let cancelCancellation (cts: CancellationTokenSource) =
        if not (isNull cts) then
            try
                cts.Cancel ()
            with error ->
                graph.Root.AddCleanup (fun () -> raise error)

            cts.Dispose ()

    let cancel () =
        let old = cancellation
        cancellation <- null
        cancelCancellation old

    let disposeEntry () =
        if not disposed then
            disposed <- true
            generation <- generation + 1L
            let pending = takeWaiters ()
            cancel ()
            cancelWaiters pending

    abstract InvokeFetch: CancellationToken -> Task<'T>
    abstract OnEvict: unit -> unit

    member internal _.Graph = graph

    member private _.EnsureGate() : Signal<AsyncSource<'T>> =
        if isNull (box gate) then
            let source = AsyncSource<'T>(graph)

            match metadata.Peek.Error with
            | Some error ->
                if graph.Options.Journal.IsNone then
                    source.Fail error
                else
                    Journal.derived graph (fun () -> source.Fail error)
            | None -> ()

            gate <- Signal<AsyncSource<'T>>.OfEncoded(graph, source, PublicationComparer<_>.Instance)

        gate

    member private _.PublishErrorBatch() =
        metadata.Value <-
            {
                FetchStatus = FetchStatus.Idle
                Error = Some pendingError
                IsStale = true
            }

        gate.Peek.Fail pendingError

    member private _.PublishOkBatch() =
        let value = pendingValue
        pendingValue <- Unchecked.defaultof<'T>

        if pendingChanged then
            hasData <- true
            data.Value <- value

        metadata.Value <-
            {
                FetchStatus = FetchStatus.Idle
                Error = None
                IsStale = false
            }

        if pendingSettleGate then
            gate.Peek.Settle value

    member private _.BeginRequestBatch() =
        gate.Value <- AsyncSource<'T>(graph)

        metadata.Value <-
            {
                FetchStatus = FetchStatus.Fetching
                Error = None
                IsStale = true
            }

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member private this.InvokeFetchUntracked(token: CancellationToken) : Task<'T> =
        let mutable fn = invokeFetchDelegate

        if isNull (box fn) then
            fn <- this.InvokeFetch
            invokeFetchDelegate <- fn

        graph.RunUntrackedWith (fn, token)

    member internal this.PublishError(current: int64, error: exn) =
        if not disposed && generation = current then
            let cts = cancellation
            cancellation <- null

            if not (isNull cts) then
                cts.Dispose ()

            let pending = takeWaiters ()

            if hasData || isNull (box gate) then
                metadata.Value <-
                    {
                        FetchStatus = FetchStatus.Idle
                        Error = Some error
                        IsStale = true
                    }
            else
                pendingError <- error

                if isNull (box publishErrorBatchAction) then
                    publishErrorBatchAction <- this.PublishErrorBatch

                try
                    graph.RunBatch publishErrorBatchAction
                finally
                    pendingError <- null

            if disposed || generation <> current then
                cancelWaiters pending
            elif not (isNull pending) then
                for i = 0 to pending.Length - 1 do
                    pending[i].Fail error

    member internal this.PublishOk(current: int64, value: 'T) =
        if not disposed && generation = current then
            let cts = cancellation
            cancellation <- null

            if not (isNull cts) then
                cts.Dispose ()

            try
                let initial = not hasData

                let changed =
                    if initial then
                        true
                    else
                        not (QueryValueComparer.equals equal data.Peek value)

                if not disposed && generation = current then
                    let pending = takeWaiters ()
                    let hasGate = not (isNull (box gate))

                    if not changed && (not initial || not hasGate) then
                        metadata.Value <-
                            {
                                FetchStatus = FetchStatus.Idle
                                Error = None
                                IsStale = false
                            }
                    else
                        pendingValue <- value
                        pendingChanged <- changed
                        pendingSettleGate <- initial && hasGate

                        if isNull (box publishOkBatchAction) then
                            publishOkBatchAction <- this.PublishOkBatch

                        try
                            graph.RunBatch publishOkBatchAction
                        finally
                            pendingValue <- Unchecked.defaultof<'T>

                    if disposed || generation <> current then
                        cancelWaiters pending
                    elif not (isNull pending) then
                        for i = 0 to pending.Length - 1 do
                            pending[i].Settle value
            with error ->
                this.PublishError (current, error)

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member internal this.PostPublishOk(current: int64, value: 'T) =
        graph.Post (fun () -> this.PublishOk (current, value))

    [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
    member internal this.PostPublishError(current: int64, error: exn) =
        graph.Post (fun () -> this.PublishError (current, error))

    member private this.DispatchRequest(current: int64, request: Task<'T>) =
#if !FABLE_COMPILER
        if request.IsCompleted then
            if request.IsCompletedSuccessfully then
                let value = request.Result

                if graph.IsOnGraphThread then
                    this.PublishOk (current, value)
                else
                    this.PostPublishOk (current, value)
            else
                let error = QueryTask.extractError request

                if graph.IsOnGraphThread then
                    this.PublishError (current, error)
                else
                    this.PostPublishError (current, error)
        else
            QueryTask.observe request (QueryFlight<'T>(this, current))
#else
        let flight = QueryFlight<'T>(this, current)

        Platform.whenSettled request (fun outcome ->
            match outcome with
            | Platform.FlightOutcome.Completed value -> flight.HandleOk value
            | Platform.FlightOutcome.Faulted error
            | Platform.FlightOutcome.Canceled error -> flight.HandleError error)
        |> ignore
#endif

    member private this.BeginRequest(current: int64) =
        let cts = new CancellationTokenSource ()
        cancellation <- cts

        if hasData || isNull (box gate) then
            metadata.Value <-
                {
                    FetchStatus = FetchStatus.Fetching
                    Error = None
                    IsStale = true
                }
        else
            if isNull (box beginRequestBatchAction) then
                beginRequestBatchAction <- this.BeginRequestBatch

            graph.RunBatch beginRequestBatchAction

        let mutable request: Task<'T> = null
        let mutable syncError: exn = null

        try
            request <-
                if not (graph.IsTracking ()) then
                    this.InvokeFetch cts.Token
                else
                    this.InvokeFetchUntracked cts.Token

            if isNull request then
                syncError <- InvalidOperationException "The query fetch returned a null task."
        with error ->
            syncError <- error

        if not (isNull syncError) then
            this.PublishError (current, syncError)
        else
            this.DispatchRequest (current, request)

    member private this.Start() =
        if disposed then
            raise (ObjectDisposedException "QueryEntry")

        generation <- generation + 1L
        let current = generation
        let pending = takeWaiters ()
        cancel ()
        cancelWaiters pending

        if not disposed && generation = current then
            this.BeginRequest current

    member private this.EnsureStaged(context: QueryStaging) =
        if not draftStaged then
            draftStaged <- true
            draftHasValue <- hasData

            draftValue <- if hasData then data.Peek else Unchecked.defaultof<'T>

            draftStale <- true
            draftUpdated <- false
            draftChanged <- false
            context.Drafts.Add (this :> IQueryDraft)

    /// <summary>Increments the active lease count for this entry.</summary>
    member internal _.AcquireLease() =
        leaseCount <- leaseCount + 1

    /// <summary>Decrements the active lease count, evicting and disposing when zero.</summary>
    member internal this.ReleaseLease() =
        leaseCount <- leaseCount - 1

        if leaseCount = 0 then
            this.OnEvict ()
            disposeEntry ()

    /// <summary>A tracked value read after caller session/lease checks; suspends on initial load.</summary>
    member this.ValueUnchecked =
        if disposed then
            raise (ObjectDisposedException "QueryEntry")

        if hasData then
            data.Value
        else
            this.EnsureGate().Value.Value

    /// <summary>A tracked state read after caller session/lease checks.</summary>
    member _.StateUnchecked =
        if disposed then
            raise (ObjectDisposedException "QueryEntry")

        let meta = metadata.Value
        let value = data.Value

        {
            Data = if hasData then Some value else None
            FetchStatus = meta.FetchStatus
            Error = meta.Error
            IsStale = meta.IsStale
        }

    /// <summary>Starts a missing or stale query and joins an existing request without re-running guards.</summary>
    member this.EnsureUnchecked() =
        let meta = metadata.Peek

        if
            meta.FetchStatus <> FetchStatus.Fetching
            && (not hasData || meta.IsStale)
        then
            this.Start ()

    /// <summary>Supersedes the current request and starts another without re-running guards.</summary>
    member this.RefreshUnchecked() =
        this.Start ()

    /// <summary>True when a request is currently in flight.</summary>
    member _.IsFetching = metadata.Peek.FetchStatus = FetchStatus.Fetching

    /// <summary>Returns a settled, faulted, or canceled task without allocating a waiter when not fetching.</summary>
    member _.AwaitImmediate() : Task<'T> =
        let meta = metadata.Peek

        match meta.Error with
        | Some failure -> QueryTask.faulted failure
        | None ->
            if hasData && not meta.IsStale then
                QueryTask.settled data.Peek
            else
                QueryTask.cancelled ()

    /// <summary>Attaches a detachable waiter to the active fetch without per-waiter closures.</summary>
    member _.AttachWaiter(leaseWaiters: ResizeArray<QueryAwaiter<'T>>) : Task<'T> =
        if isNull waiters then
            waiters <- ResizeArray<QueryAwaiter<'T>>()

        let waiter = QueryAwaiter<'T>(waiters, leaseWaiters)
        waiters.Add waiter
        leaseWaiters.Add waiter
        waiter.Task

    /// <summary>Stages an updater edit directly on this entry inside a commit staging pass.</summary>
    member this.StageUpdate(context: QueryStaging, updater: 'T -> 'T) =
        this.EnsureStaged context

        if draftHasValue then
            draftValue <- updater draftValue
            draftStale <- false
            draftUpdated <- true

    /// <summary>Stages a replacement value directly on this entry inside a commit staging pass.</summary>
    member this.StageSet(context: QueryStaging, value: 'T) =
        this.EnsureStaged context

        if draftHasValue then
            draftValue <- value
            draftStale <- false
            draftUpdated <- true

    /// <summary>Stages invalidation directly on this entry inside a commit staging pass.</summary>
    member this.StageInvalidate(context: QueryStaging) =
        this.EnsureStaged context
        draftStale <- true

    interface IQueryDraft with
        member _.Prepare() =
            draftChanged <-
                if not draftUpdated || not draftHasValue then
                    false
                elif not hasData then
                    true
                else
                    not (QueryValueComparer.equals equal data.Peek draftValue)

        member this.Publish(context) =
            let nextHasValue = draftHasValue
            let nextVal = draftValue
            let changed = draftChanged
            let stale = draftStale
            draftStaged <- false
            draftValue <- Unchecked.defaultof<'T>
            draftUpdated <- false
            draftChanged <- false

            let priorError = metadata.Peek.Error
            generation <- generation + 1L
            let retiredWaiters = takeWaiters ()
            let retiredCts = cancellation
            cancellation <- null

            if
                not (isNull retiredWaiters)
                || not (isNull retiredCts)
            then
                context.AddRetired (QueryRetired (this, box retiredWaiters, retiredCts))

            if changed && nextHasValue then
                hasData <- true
                data.Value <- nextVal

            metadata.Value <-
                {
                    FetchStatus = FetchStatus.Idle
                    Error = (if stale then priorError else None)
                    IsStale = stale
                }

            if not nextHasValue then
                if not (isNull (box gate)) then
                    gate.Value <- AsyncSource<'T>(graph)

        member _.CancelRetired(retiredWaiters, retiredCts) =
            cancelWaiters (unbox<QueryAwaiter<'T>[]> retiredWaiters)
            cancelCancellation retiredCts

        member _.ResetDraft() =
            if draftStaged then
                draftStaged <- false
                draftValue <- Unchecked.defaultof<'T>
                draftUpdated <- false
                draftChanged <- false

    /// <summary>Retires outstanding completion and requests cancellation.</summary>
    member _.Dispose() =
        disposeEntry ()

and [<Sealed>] internal QueryFlight<'T>(entry: QueryEntry<'T>, current: int64) =
    member _.HandleOk(value: 'T) =
        if entry.Graph.IsOnGraphThread then
            entry.PublishOk (current, value)
        else
            entry.PostPublishOk (current, value)

    member _.HandleError(error: exn) =
        if entry.Graph.IsOnGraphThread then
            entry.PublishError (current, error)
        else
            entry.PostPublishError (current, error)

#if !FABLE_COMPILER
    interface QueryTask.IQueryTaskObserver<'T> with
        member this.OnCompleted(request: Task<'T>) =
            if request.IsCompletedSuccessfully then
                this.HandleOk request.Result
            else
                this.HandleError (QueryTask.extractError request)
#endif
