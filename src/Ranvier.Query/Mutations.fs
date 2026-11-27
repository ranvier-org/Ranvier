namespace Ranvier.Query

open System
open System.Threading
open System.Threading.Tasks
#if FABLE_COMPILER
open Fable.Core
#endif
open Ranvier

/// <summary>Client-owned remote writes and their atomic query reconciliation.</summary>
[<AutoOpen>]
module QueryMutationExtensions =
    /// <summary>Settles cancellation using each target's completion-source spelling.</summary>
#if FABLE_COMPILER
    [<Emit("$0.SetCancelled()")>]
#endif
    let private cancelResult (source: TaskCompletionSource<'T>) =
        source.SetCanceled ()

    [<Sealed>]
    type private QueryMutation<'Input, 'Result>
        (
            client: QueryClient,
            input: 'Input,
            execute: 'Input -> CancellationToken -> Task<'Result>,
            reconcile: 'Result -> QueryEdit list,
            result: TaskCompletionSource<MutationOutcome<'Result>>,
            cancellation: CancellationTokenSource
        ) =
        let graph = client.Graph
#if !FABLE_COMPILER
        let executeFn =
            OptimizedClosures.FSharpFunc<'Input, CancellationToken, Task<'Result>>.Adapt execute
#endif
        let mutable cancelled = false
        let mutable terminal = false
        let mutable onFinished: unit -> unit = Unchecked.defaultof<_>

        let mutable invokeExecuteDelegate: CancellationToken -> Task<'Result> =
            Unchecked.defaultof<_>

        member private _.FinishApply(accepted: MutationOutcome<'Result>) =
            if not cancelled then
                terminal <- true
                result.SetResult accepted

            cancellation.Dispose ()
            onFinished ()

        member private this.ApplyError(error: exn) =
            if not cancelled && not client.IsDisposed then
                this.FinishApply (MutationOutcome.RequestFailed error)

        member private this.ApplyOk(value: 'Result) =
            if not cancelled && not client.IsDisposed then
                let accepted =
                    try
                        client.ReconcileWith (reconcile, value)
                        MutationOutcome.Applied value
                    with error ->
                        if not client.IsDisposed then
                            client.InvalidateAll ()

                        MutationOutcome.ReconciliationFailed (value, error)

                this.FinishApply accepted

        [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
        member private this.PostApplyOk(value: 'Result) =
            graph.Post (fun () -> this.ApplyOk value)

        [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
        member private this.PostApplyError(error: exn) =
            graph.Post (fun () -> this.ApplyError error)

        member private _.InvokeExecute(token: CancellationToken) : Task<'Result> =
#if FABLE_COMPILER
            execute input token
#else
            executeFn.Invoke (input, token)
#endif

        [<System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)>]
        member private this.InvokeExecuteUntracked(token: CancellationToken) : Task<'Result> =
            let mutable fn = invokeExecuteDelegate

            if isNull (box fn) then
                fn <- this.InvokeExecute
                invokeExecuteDelegate <- fn

            graph.RunUntrackedWith (fn, token)

        member private this.ObserveRequest(remote: Task<'Result>) =
#if !FABLE_COMPILER
            if remote.IsCompleted then
                if remote.IsCompletedSuccessfully then
                    let value = remote.Result

                    if graph.IsOnGraphThread then
                        this.ApplyOk value
                    else
                        this.PostApplyOk value
                else
                    let error = QueryTask.extractError remote

                    if graph.IsOnGraphThread then
                        this.ApplyError error
                    else
                        this.PostApplyError error
            else
                QueryTask.observe remote (this :> QueryTask.IQueryTaskObserver<'Result>)
#else
            Platform.whenSettled remote (fun outcome ->
                match outcome with
                | Platform.FlightOutcome.Completed value ->
                    if graph.IsOnGraphThread then
                        this.ApplyOk value
                    else
                        this.PostApplyOk value
                | Platform.FlightOutcome.Faulted error
                | Platform.FlightOutcome.Canceled error ->
                    if graph.IsOnGraphThread then
                        this.ApplyError error
                    else
                        this.PostApplyError error)
            |> ignore
#endif

#if !FABLE_COMPILER
        interface QueryTask.IQueryTaskObserver<'Result> with
            member this.OnCompleted(remote: Task<'Result>) =
                if remote.IsCompletedSuccessfully then
                    let value = remote.Result

                    if graph.IsOnGraphThread then
                        this.ApplyOk value
                    else
                        this.PostApplyOk value
                else
                    let error = QueryTask.extractError remote

                    if graph.IsOnGraphThread then
                        this.ApplyError error
                    else
                        this.PostApplyError error
#endif

        interface IQueryMutation with
            member _.Cancel() =
                if not terminal then
                    terminal <- true
                    cancelled <- true
                    cancelResult result

                    try
                        cancellation.Cancel ()
                    with error ->
                        graph.Root.AddCleanup (fun () -> raise error)

                    cancellation.Dispose ()

            member this.Start(finished) =
                onFinished <- finished
                let mutable remote: Task<'Result> = null
                let mutable syncError: exn = null

                try
                    remote <-
                        if not (graph.IsTracking ()) then
                            this.InvokeExecute cancellation.Token
                        else
                            this.InvokeExecuteUntracked cancellation.Token

                    if isNull remote then
                        syncError <- InvalidOperationException "The mutation execute function returned a null task."
                with error ->
                    syncError <- error

                if not (isNull syncError) then
                    this.ApplyError syncError
                else
                    this.ObserveRequest remote

    type QueryClient with
        /// <summary>Queues one remote write, then publishes its edits before the next write starts.</summary>
        /// <remarks>A reconciliation failure retains the saved receipt and invalidates cached queries. No write is retried.</remarks>
        /// <returns>A task with a mutation outcome; client disposal cancels the task instead of returning an outcome.</returns>
        member client.Mutate(input: 'Input, execute: 'Input -> CancellationToken -> Task<'Result>, reconcile: 'Result -> QueryEdit list) =
            let result = TaskCompletionSource<MutationOutcome<'Result>>()
            let cancellation = new CancellationTokenSource ()

            let operation =
                QueryMutation<'Input, 'Result>(client, input, execute, reconcile, result, cancellation)

            try
                client.Enqueue (operation :> IQueryMutation)
            with error ->
                cancellation.Dispose ()
                raise error

            result.Task
