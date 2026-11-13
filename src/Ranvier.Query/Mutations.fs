namespace Ranvier.Query

open System
open System.Threading
open System.Threading.Tasks
open Fable.Core

/// <summary>Client-owned remote writes and their atomic query reconciliation.</summary>
[<AutoOpen>]
module QueryMutationExtensions =
    let private observeAsync (graph: Ranvier.Graph) (remote: Task<'T>) (applyOk: 'T -> unit) (applyError: exn -> unit) =
        task {
            try
                let! value = remote

                if graph.IsOnGraphThread then
                    applyOk value
                else
                    graph.Post (fun () -> applyOk value)
            with error ->
                if graph.IsOnGraphThread then
                    applyError error
                else
                    graph.Post (fun () -> applyError error)
        }
        |> ignore

    let private observeRequest (graph: Ranvier.Graph) (remote: Task<'T>) (applyOk: 'T -> unit) (applyError: exn -> unit) =
#if !FABLE_COMPILER
        if remote.IsCompleted then
            if remote.IsCompletedSuccessfully then
                let value = remote.GetAwaiter().GetResult()

                if graph.IsOnGraphThread then
                    applyOk value
                else
                    graph.Post (fun () -> applyOk value)
            else
                try
                    remote.GetAwaiter().GetResult() |> ignore
                with error ->
                    if graph.IsOnGraphThread then
                        applyError error
                    else
                        graph.Post (fun () -> applyError error)
        else
            observeAsync graph remote applyOk applyError
#else
        observeAsync graph remote applyOk applyError
#endif

    /// <summary>Settles cancellation using each target's completion-source spelling.</summary>
    [<Emit("$0.SetCancelled()")>]
    let private cancelResult (source: TaskCompletionSource<'T>) =
        source.SetCanceled ()

    type QueryClient with
        /// <summary>Queues one remote write, then publishes its edits before the next write starts.</summary>
        /// <remarks>A reconciliation failure retains the saved receipt and invalidates cached queries. No write is retried.</remarks>
        /// <returns>A task with a mutation outcome; client disposal cancels the task instead of returning an outcome.</returns>
        member client.Mutate(input: 'Input, execute: 'Input -> CancellationToken -> Task<'Result>, reconcile: 'Result -> QueryEdit list) =
            let graph = client.Graph
            let result = TaskCompletionSource<MutationOutcome<'Result>>()
            let cancellation = new CancellationTokenSource ()
            let mutable cancelled = false
            let mutable terminal = false

            let operation =
                { new IQueryMutation with
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

                    member _.Start(finished) =
                        let finishApply (accepted: MutationOutcome<'Result>) =
                            if not cancelled then
                                terminal <- true
                                result.SetResult accepted

                            cancellation.Dispose ()
                            finished ()

                        let applyError (error: exn) =
                            if not cancelled && not client.IsDisposed then
                                finishApply (MutationOutcome.RequestFailed error)

                        let applyOk (value: 'Result) =
                            if not cancelled && not client.IsDisposed then
                                let accepted =
                                    try
                                        client.Reconcile (fun () -> reconcile value)
                                        MutationOutcome.Applied value
                                    with error ->
                                        if not client.IsDisposed then
                                            client.InvalidateAll ()

                                        MutationOutcome.ReconciliationFailed (value, error)

                                finishApply accepted

                        let mutable remote: Task<'Result> = null
                        let mutable syncError: exn = null

                        try
                            remote <- graph.RunUntracked (fun () -> execute input cancellation.Token)

                            if isNull remote then
                                syncError <- InvalidOperationException "The mutation execute function returned a null task."
                        with error ->
                            syncError <- error

                        if not (isNull syncError) then
                            applyError syncError
                        else
                            observeRequest graph remote applyOk applyError
                }

            try
                client.Enqueue operation
            with error ->
                cancellation.Dispose ()
                raise error

            result.Task
