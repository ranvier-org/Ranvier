namespace Ranvier.Query

open System
open System.Threading
open System.Threading.Tasks
open Ranvier

/// <summary>Cached data, request state, and the initial-load suspension source for an entry.</summary>
[<AllowNullLiteral>]
type internal QueryEntry<'T>(graph: Graph, fetch: CancellationToken -> Task<'T>, guard: unit -> unit, onEvict: unit -> unit) =
    let data = Signal<'T option>(graph, None, PublicationComparer<_>.Instance)

    let metadata =
        Signal<QueryMeta>(
            graph,
            {
                FetchStatus = FetchStatus.Idle
                Error = None
                IsStale = true
            },
            PublicationComparer<_>.Instance
        )

    let gate =
        Signal<AsyncSource<'T>>(graph, AsyncSource<'T>(graph), PublicationComparer<_>.Instance)

    let comparer = graph.Options.Equality.Comparer<'T>()
    let mutable generation = 0L
    let mutable disposed = false
    let mutable freshGate = true
    let mutable leaseCount = 0
    let mutable cancellation: CancellationTokenSource = null
    let mutable waiters: ResizeArray<QueryAwaiter<'T>> = null

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

    let publishError current (error: exn) =
        if not disposed && generation = current then
            let pending = takeWaiters ()

            graph.RunBatch (fun () ->
                metadata.Value <-
                    {
                        FetchStatus = FetchStatus.Idle
                        Error = Some error
                        IsStale = true
                    }

                if data.Peek.IsNone then
                    gate.Peek.Fail error)

            if disposed || generation <> current then
                cancelWaiters pending
            elif not (isNull pending) then
                for i = 0 to pending.Length - 1 do
                    pending[i].Fail error

    let publishOk current (value: 'T) =
        if not disposed && generation = current then
            try
                let initial = data.Peek.IsNone

                let changed =
                    match data.Peek with
                    | None -> true
                    | Some old -> not (comparer.Equals (old, value))

                if not disposed && generation = current then
                    let pending = takeWaiters ()

                    graph.RunBatch (fun () ->
                        if changed then
                            data.Value <- Some value

                        metadata.Value <-
                            {
                                FetchStatus = FetchStatus.Idle
                                Error = None
                                IsStale = false
                            }

                        if initial then
                            gate.Peek.Settle value)

                    if disposed || generation <> current then
                        cancelWaiters pending
                    elif not (isNull pending) then
                        for i = 0 to pending.Length - 1 do
                            pending[i].Settle value
            with error ->
                publishError current error

    let observeRequest current (request: Task<'T>) =
        task {
            try
                let! value = request

                if graph.IsOnGraphThread then
                    publishOk current value
                else
                    graph.Post (fun () -> publishOk current value)
            with error ->
                if graph.IsOnGraphThread then
                    publishError current error
                else
                    graph.Post (fun () -> publishError current error)
        }
        |> ignore

    let dispatchRequest current (request: Task<'T>) =
#if !FABLE_COMPILER
        if request.IsCompleted then
            if request.IsCompletedSuccessfully then
                let value = request.GetAwaiter().GetResult()

                if graph.IsOnGraphThread then
                    publishOk current value
                else
                    graph.Post (fun () -> publishOk current value)
            else
                try
                    request.GetAwaiter().GetResult() |> ignore
                with error ->
                    if graph.IsOnGraphThread then
                        publishError current error
                    else
                        graph.Post (fun () -> publishError current error)
        else
            observeRequest current request
#else
        observeRequest current request
#endif

    let beginRequest current =
        let cts = new CancellationTokenSource ()
        cancellation <- cts

        graph.RunBatch (fun () ->
            if data.Peek.IsNone then
                if freshGate then
                    freshGate <- false
                else
                    gate.Value <- AsyncSource<'T>(graph)

            metadata.Value <-
                {
                    FetchStatus = FetchStatus.Fetching
                    Error = None
                    IsStale = true
                })

        let mutable request: Task<'T> = null
        let mutable syncError: exn = null

        try
            request <- graph.RunUntracked (fun () -> fetch cts.Token)

            if isNull request then
                syncError <- InvalidOperationException "The query fetch returned a null task."
        with error ->
            syncError <- error

        if not (isNull syncError) then
            publishError current syncError
        else
            dispatchRequest current request

    let disposeEntry () =
        if not disposed then
            disposed <- true
            generation <- generation + 1L
            let pending = takeWaiters ()
            cancel ()
            cancelWaiters pending

    let start () =
        guard ()

        if disposed then
            raise (ObjectDisposedException "QueryEntry")

        generation <- generation + 1L
        let current = generation
        let pending = takeWaiters ()
        cancel ()
        cancelWaiters pending

        if not disposed && generation = current then
            beginRequest current

    /// <summary>Increments the active lease count for this entry.</summary>
    member internal _.AcquireLease() =
        leaseCount <- leaseCount + 1

    /// <summary>Decrements the active lease count, evicting and disposing when zero.</summary>
    member internal _.ReleaseLease() =
        leaseCount <- leaseCount - 1

        if leaseCount = 0 then
            onEvict ()
            disposeEntry ()

    /// <summary>A tracked value read; suspends until initial data arrives and retains data during refresh.</summary>
    member _.Value =
        guard ()

        if disposed then
            raise (ObjectDisposedException "QueryEntry")

        match data.Value with
        | Some value -> value
        | None -> gate.Value.Value

    /// <summary>A tracked state read that remains available during initial loading.</summary>
    member _.State =
        guard ()

        if disposed then
            raise (ObjectDisposedException "QueryEntry")

        let meta = metadata.Value

        {
            Data = data.Value
            FetchStatus = meta.FetchStatus
            Error = meta.Error
            IsStale = meta.IsStale
        }

    /// <summary>Starts a missing or stale query and joins an existing request.</summary>
    member _.Ensure() =
        guard ()
        let meta = metadata.Peek

        if
            meta.FetchStatus <> FetchStatus.Fetching
            && (data.Peek.IsNone || meta.IsStale)
        then
            start ()

    /// <summary>Supersedes the current request and starts another.</summary>
    member _.Refresh() = start ()

    /// <summary>Attaches a lease waiter to the current request, or returns settled state immediately.</summary>
    member _.Await(completed: QueryAwaiter<'T> -> unit) =
        guard ()

        let waiter =
            QueryAwaiter<'T>(fun current ->
                if not (isNull waiters) then
                    waiters.Remove current |> ignore

                completed current)

        let meta = metadata.Peek

        match meta.FetchStatus, meta.Error, data.Peek with
        | FetchStatus.Fetching, _, _ ->
            if isNull waiters then
                waiters <- ResizeArray<QueryAwaiter<'T>>()

            waiters.Add waiter
        | _, Some failure, _ -> waiter.Fail failure
        | _, _, Some value when not meta.IsStale -> waiter.Settle value
        | _ -> waiter.Cancel ()

        waiter

    /// <summary>Creates a typed draft without modifying requests or sources.</summary>
    member this.Draft(context: QueryStaging) =
        match context.TryGet this with
        | null ->
            let draft = QueryDraft<'T>(this, data.Peek)
            context.Add (this, draft)
            draft
        | existing -> unbox<QueryDraft<'T>> existing

    interface IQueryEntry<'T> with
        member _.PrepareDraft(initial, next, updated) =
            match updated, initial, next with
            | false, _, _ -> false
            | true, Some old, Some value -> not (comparer.Equals (old, value))
            | true, None, None -> false
            | _ -> true

        member _.PublishDraft(next, changed, stale, draft) =
            let priorError = metadata.Peek.Error
            generation <- generation + 1L
            let retiredWaiters = takeWaiters ()
            let retiredCts = cancellation
            cancellation <- null
            draft.SetRetired (retiredWaiters, retiredCts)

            if changed then
                data.Value <- next

            metadata.Value <-
                {
                    FetchStatus = FetchStatus.Idle
                    Error = (if stale then priorError else None)
                    IsStale = stale
                }

            if next.IsNone then
                freshGate <- false
                gate.Value <- AsyncSource<'T>(graph)

        member _.CancelDraft(retiredWaiters, retiredCts) =
            cancelWaiters retiredWaiters
            cancelCancellation retiredCts

    /// <summary>Retires outstanding completion and requests cancellation.</summary>
    member _.Dispose() =
        disposeEntry ()
