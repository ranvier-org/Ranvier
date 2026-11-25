namespace Ranvier.CSharp

open System
open System.Collections
open System.Collections.Generic
open System.ComponentModel
open System.Runtime.ExceptionServices
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open System.Windows.Input
open Ranvier

/// <summary>Sentinel-encoded equality comparison avoiding interface dispatch under default policies.</summary>
module internal CSharpValueComparer =
    let encode<'T> (graph: Graph) : IEqualityComparer<'T> =
        ComparerSentinel.encode (graph.Options.Equality.Comparer<'T>())

    let inline equals (encoded: IEqualityComparer<'T>) (a: 'T) (b: 'T) : bool =
        if typeof<'T>.IsValueType then
            if obj.ReferenceEquals (encoded, null) then
                EqualityComparer<'T>.Default.Equals(a, b)
            else
                encoded.Equals (a, b)
        elif obj.ReferenceEquals (encoded, null) then
            obj.ReferenceEquals (box a, box b)
        else
            encoded.Equals (a, b)

module private HandlerDispatch =
    let invokePropertyChanged (handler: PropertyChangedEventHandler) (sender: obj) (args: PropertyChangedEventArgs) =
        handler.Invoke (sender, args)

    let invokeErrorsChanged (handler: EventHandler<DataErrorsChangedEventArgs>) (sender: obj) (args: DataErrorsChangedEventArgs) =
        handler.Invoke (sender, args)

    let invokeEvent (handler: EventHandler) (sender: obj) (args: EventArgs) =
        handler.Invoke (sender, args)

/// <summary>A handler invocation posted to a <c>SynchronizationContext</c>.</summary>
type internal IPostedDelivery =
    abstract Run: unit -> unit

/// <summary>One handler's invocation with the sender and arguments of the raise that posted it.</summary>
/// <remarks><c>Run</c> invokes the handler only while <c>Live ()</c> is true.</remarks>
[<NoEquality; NoComparison>]
type internal PostedDelivery<'H, 'Args> =
    {
        Invoke: 'H -> obj -> 'Args -> unit
        Handler: 'H
        Sender: obj
        Args: 'Args
        Live: unit -> bool
    }

    interface IPostedDelivery with
        member this.Run() =
            if this.Live () then
                this.Invoke this.Handler this.Sender this.Args

module private PostedCallback =
    /// <summary>Runs the <c>IPostedDelivery</c> passed as the post's state.</summary>
    let run = SendOrPostCallback (fun state -> (state :?> IPostedDelivery).Run())

/// <summary>
/// The handlers of one event, each paired with the <c>SynchronizationContext</c> current when it subscribed.
/// </summary>
/// <remarks>Safe to subscribe and unsubscribe from any thread.</remarks>
[<Sealed>]
type internal ContextHandlers<'H, 'Args when 'H: null and 'H :> Delegate>(invoke: 'H -> obj -> 'Args -> unit) =
    let mutable items: struct ('H * SynchronizationContext)[] = Array.Empty ()

    member _.IsEmpty = items.Length = 0

    member this.Add(handler: 'H) =
        if not (isNull handler) then
            let entry = struct (handler, SynchronizationContext.Current)

            lock this (fun () ->
                let prev = items
                let next = Array.zeroCreate (prev.Length + 1)

                if prev.Length > 0 then
                    Array.Copy (prev, next, prev.Length)

                next[prev.Length] <- entry
                items <- next)

    /// <summary>Removes the latest subscription of <c>handler</c>.</summary>
    member this.Remove(handler: 'H) =
        if not (isNull handler) then
            lock this (fun () ->
                let prev = items
                let mutable index = prev.Length - 1
                let mutable found = -1

                while found < 0 && index >= 0 do
                    let struct (h, _) = prev[index]

                    if obj.Equals (h, handler) then
                        found <- index
                    else
                        index <- index - 1

                if found >= 0 then
                    if prev.Length = 1 then
                        items <- Array.Empty ()
                    else
                        let next = Array.zeroCreate (prev.Length - 1)

                        if found > 0 then
                            Array.Copy (prev, 0, next, 0, found)

                        if found < prev.Length - 1 then
                            Array.Copy (prev, found + 1, next, found, prev.Length - found - 1)

                        items <- next)

    member this.Clear() =
        lock this (fun () -> items <- Array.Empty ())

    /// <summary>
    /// Invokes each handler inline when it captured no context or the current context is the captured one, and posts it
    /// to the captured context otherwise.
    /// </summary>
    /// <remarks>
    /// An exception from an inline handler is added to <c>errors</c>, and the remaining handlers still run. A posted
    /// invocation runs only while <c>live ()</c> is true.
    /// </remarks>
    member _.Raise(sender: obj, args: 'Args, live: unit -> bool, errors: byref<ResizeArray<exn>>) =
        let snap = items

        if snap.Length > 0 then
            let current = SynchronizationContext.Current

            for i = 0 to snap.Length - 1 do
                let struct (handler, context) = snap[i]

                if
                    isNull context
                    || obj.ReferenceEquals (context, current)
                then
                    try
                        invoke handler sender args
                    with ex ->
                        if isNull errors then
                            errors <- ResizeArray<exn>()

                        errors.Add ex
                else
                    context.Post (
                        PostedCallback.run,
                        {
                            Invoke = invoke
                            Handler = handler
                            Sender = sender
                            Args = args
                            Live = live
                        }
                    )

/// <summary>A bound property's state as last notified.</summary>
[<Sealed; AllowNullLiteral>]
type internal BoundState<'T>(value: 'T, hasValue: bool, loading: bool, error: exn) =
    member _.Value = value
    member _.HasValue = hasValue
    member _.Loading = loading
    member _.Error = error

/// <summary>A property registered with a <c>ReactiveBindings</c>.</summary>
type internal IBoundSlot =
    abstract Name: string
    abstract Loading: bool
    abstract Error: exn

    /// <summary>
    /// Reads the node, tracked, stores the new state, and adds the notifications the change requires.
    /// </summary>
    abstract Refresh: Notifications -> unit

/// <summary>Host contract allowing hosted commands to unregister on disposal without per-command closures.</summary>
and [<AllowNullLiteral>] internal ICommandHost =
    abstract RemoveCommand: ReactiveCommand -> unit

/// <summary>The notifications of one pass over the bound properties and hosted commands.</summary>
and [<Sealed>] internal Notifications() =
    let changed = ResizeArray<PropertyChangedEventArgs>()
    let errorsChanged = ResizeArray<DataErrorsChangedEventArgs>()
    let commandChanges = ResizeArray<struct (ReactiveCommand * int)>()

    member _.Changed = changed
    member _.ErrorsChanged = errorsChanged
    member _.CommandChanges = commandChanges

    member _.Any =
        changed.Count > 0
        || errorsChanged.Count > 0
        || commandChanges.Count > 0

    member _.Clear() =
        if changed.Count > 0 then
            changed.Clear ()

        if errorsChanged.Count > 0 then
            errorsChanged.Clear ()

        if commandChanges.Count > 0 then
            commandChanges.Clear ()

/// <summary>
/// A read-only view-model property backed by a memo. <c>Value</c> is the last settled value; <c>IsLoading</c> and
/// <c>Error</c> describe the current reading.
/// </summary>
/// <remarks>
/// <c>Value</c> is <c>default</c> before the first settled value. <c>IsLoading</c> and <c>Error</c> are safe to read from
/// any thread.
/// </remarks>
and [<Sealed>] BoundValue<'T> internal (graph: Graph, owner: Owner, name: string, loadingName: string, memo: Memo<'T>) =
    let equal = CSharpValueComparer.encode<'T> graph
    let changedArgs = PropertyChangedEventArgs name
    let errorsArgs = DataErrorsChangedEventArgs name

    let loadingArgs =
        if String.IsNullOrEmpty loadingName then
            null
        else
            PropertyChangedEventArgs loadingName

    [<VolatileField>]
    let mutable state =
        let struct (st, v, err) = memo.TryReadUntrackedStruct ()

        if st = Status.None then
            BoundState (v, true, false, null)
        elif st = Status.Pending then
            BoundState (Unchecked.defaultof<'T>, false, true, null)
        else
            BoundState (Unchecked.defaultof<'T>, false, false, err)

    /// <summary>The property name raised with <c>PropertyChanged</c> and <c>ErrorsChanged</c>.</summary>
    member _.Name = name

    /// <summary>The memo behind the property, disposed with the bindings.</summary>
    member _.Memo = memo

    /// <summary>
    /// The memo's value when it is ready, otherwise the last settled value. On the graph's thread the read is tracked.
    /// </summary>
    /// <remarks>
    /// Off the graph's thread, or after disposal, <c>Value</c> is the value last notified. A <c>Computed</c> body that
    /// reads it tracks the memo, and sees the last settled value in place of a pending or failed reading; reading
    /// <c>Memo.Value</c> propagates the pending or failed reading instead.
    /// </remarks>
    member _.Value: 'T =
        if graph.IsOnGraphThread && not owner.IsDisposed then
            let struct (st, v, _) = memo.TryReadStruct ()
            if st = Status.None then v else state.Value
        else
            state.Value

    /// <summary>True while the memo is pending.</summary>
    member _.IsLoading = state.Loading

    /// <summary>The memo's error while it is failed, otherwise null.</summary>
    member _.Error: exn = state.Error

    interface IBoundSlot with
        member _.Name = name
        member _.Loading = state.Loading
        member _.Error = state.Error

        member _.Refresh(notes) =
            let old = state
            let struct (st, v, err) = memo.TryReadStruct ()

            if st = Status.None then
                let valueChanged =
                    not old.HasValue
                    || not (CSharpValueComparer.equals equal old.Value v)

                let loadingChanged = old.Loading
                let errorChanged = not (isNull old.Error)

                if valueChanged || loadingChanged || errorChanged then
                    if valueChanged then
                        notes.Changed.Add changedArgs

                    if loadingChanged && not (isNull loadingArgs) then
                        notes.Changed.Add loadingArgs

                    if errorChanged then
                        notes.ErrorsChanged.Add errorsArgs

                    state <- BoundState (v, true, false, null)
                elif not (isNull (box equal)) then
                    state <- BoundState (v, true, false, null)
            elif st = Status.Pending then
                if not old.Loading || not (isNull old.Error) then
                    if not old.Loading && not (isNull loadingArgs) then
                        notes.Changed.Add loadingArgs

                    if not (isNull old.Error) then
                        notes.ErrorsChanged.Add errorsArgs

                    state <- BoundState (old.Value, old.HasValue, true, null)
            else
                let loadingChanged = old.Loading
                let errorChanged = not (obj.ReferenceEquals (err, old.Error))

                if loadingChanged || errorChanged then
                    if loadingChanged && not (isNull loadingArgs) then
                        notes.Changed.Add loadingArgs

                    if errorChanged then
                        notes.ErrorsChanged.Add errorsArgs

                    state <- BoundState (old.Value, old.HasValue, false, err)

/// <summary>A two-way view-model property backed by a signal.</summary>
and [<Sealed>] BoundSignal<'T> internal (graph: Graph, owner: Owner, name: string, signal: Signal<'T>) =
    let equal = CSharpValueComparer.encode<'T> graph
    let changedArgs = PropertyChangedEventArgs name

    [<VolatileField>]
    let mutable state = BoundState (signal.Peek, true, false, null)

    /// <summary>The property name raised with <c>PropertyChanged</c>.</summary>
    member _.Name = name

    member _.Signal = signal

    /// <summary>
    /// Reads the signal, tracked, on the graph's thread, and the value last notified elsewhere. Setting it writes the
    /// signal through <c>Graph.Dispatch</c>: inline on the graph's thread, marshalled from any other.
    /// </summary>
    /// <remarks>
    /// Under <c>ThreadAffinity.Serialised</c>, a set from outside the graph applies at the next drain, so a read right
    /// after it returns the previous value.
    /// </remarks>
    member _.Value
        with get (): 'T =
            if graph.IsOnGraphThread && not owner.IsDisposed then
                signal.Value
            else
                state.Value
        and set (v: 'T) =
            if graph.IsOnGraphThread then
                signal.Value <- v
            else
                graph.PostQueued (fun () -> signal.Value <- v)

    interface IBoundSlot with
        member _.Name = name
        member _.Loading = false
        member _.Error = null

        member _.Refresh(notes) =
            let old = state
            let v = signal.Value

            if not (CSharpValueComparer.equals equal old.Value v) then
                state <- BoundState (v, true, false, null)
                notes.Changed.Add changedArgs
            elif not (isNull (box equal)) then
                state <- BoundState (v, true, false, null)

/// <summary>What a <c>ReactiveCommand</c> does when an execution is requested while another runs.</summary>
and CommandPolicy =
    /// <summary>The command is disabled while an execution runs, and a request made meanwhile is ignored.</summary>
    | Disable = 0
    /// <summary>The request cancels the token of every execution in flight, then starts a new execution.</summary>
    | CancelPrevious = 1

/// <summary>
/// An <c>ICommand</c> whose <c>CanExecute</c> is derived from graph nodes and whose busy state is observable. Create one
/// with <c>ReactiveBindings.Command</c> or <c>Reactive.Command</c>.
/// </summary>
/// <remarks>
/// <para>
/// <c>CanExecute</c> is true while <c>Enabled</c> is ready and true: false while the predicate reads a pending or failed
/// node, and, under <c>CommandPolicy.Disable</c>, false while an execution runs. <c>CanExecuteChanged</c> and
/// <c>PropertyChanged</c> (<c>"CanRun"</c>, <c>"IsRunning"</c>, <c>"Error"</c>) are raised on the graph's thread, or
/// posted to the <c>SynchronizationContext</c> current when the handler subscribed.
/// </para>
/// <para>
/// The predicate is first evaluated at the first <c>CanExecute</c> call, event subscription or execution, so it can
/// read commands created after this one.
/// </para>
/// <para>
/// An execution runs the body untracked on the graph's thread, with a token that <c>Cancel</c>, <c>Dispose</c> and
/// <c>CommandPolicy.CancelPrevious</c> cancel. Its failure is stored in <c>Error</c>; an
/// <c>OperationCanceledException</c> after its token is cancelled clears <c>Error</c> instead. <c>ICommand.Execute</c>
/// discards the execution's task, so a failure reaches only <c>Error</c>.
/// </para>
/// </remarks>
/// <example>
/// <code lang="csharp">
/// save = bindings.Command((_, token) =&gt; repo.SaveAsync(draft.Value, token), () =&gt; IsValid &amp;&amp; !load.IsRunning);
/// load = bindings.Command((_, token) =&gt; repo.LoadAsync(token), () =&gt; !save.IsRunning);
/// busy = bindings.Computed("IsBusy", () =&gt; save.IsRunning || load.IsRunning);
/// </code>
/// </example>
and [<Sealed>] ReactiveCommand
    internal
    (
        graph: Graph,
        asyncExecute: Func<obj, CancellationToken, Task>,
        syncExecute: Action<obj>,
        canExecute: Func<bool>,
        policy: CommandPolicy,
        host: ICommandHost
    ) as self =
    static let canRunArgs = PropertyChangedEventArgs "CanRun"
    static let isRunningArgs = PropertyChangedEventArgs "IsRunning"
    static let errorArgs = PropertyChangedEventArgs "Error"
    static let cancelledTask: Task = Task.FromCanceled (CancellationToken true)

    let canExecuteHandlers =
        ContextHandlers<EventHandler, EventArgs>(HandlerDispatch.invokeEvent)

    let changedHandlers =
        ContextHandlers<PropertyChangedEventHandler, PropertyChangedEventArgs>(HandlerDispatch.invokePropertyChanged)

    /// <summary>The token sources of the executions in flight. Graph thread only, allocated lazily.</summary>
    let mutable flights: ResizeArray<CancellationTokenSource> = null

    /// <summary>The token source of the latest execution; only that execution writes <c>error</c>.</summary>
    let mutable latest: CancellationTokenSource = null

    [<VolatileField>]
    let mutable canRun = false

    [<VolatileField>]
    let mutable isRunning = false

    [<VolatileField>]
    let mutable lastError: exn = null

    [<VolatileField>]
    let mutable armRequested = false

    [<VolatileField>]
    let mutable disposed = false

    let mutable pendingError: exn = null
    let mutable pendingSyncParam: obj = null
    let mutable syncFailure: exn = null

    let struct (owner, armed, running, error, enabled) =
        graph.RunRoot (fun owner ->
            let armed = Signal<bool>(graph, false)
            let running = Signal<int>(graph, 0)
            let error = Signal<exn>(graph, null)

            let enabled =
                Memo<bool>
                    .Create(
                        graph,
                        (fun _ ->
                            (isNull canExecute || canExecute.Invoke ())
                            && (policy <> CommandPolicy.Disable
                                || running.Value = 0)),
                        ScopeMode.Pure
                    )

            struct (owner, armed, running, error, enabled))

    let readEnabled () =
        let struct (st, v, _) = enabled.TryReadStruct ()
        st = Status.None && v

    let readEnabledUntracked () =
        let struct (st, v, _) = enabled.TryReadUntrackedStruct ()
        st = Status.None && v

    let liveFn () =
        not disposed

    let armFn () =
        if not disposed then
            armed.Value <- true

    let arm () =
        if not armRequested && not disposed then
            armRequested <- true

            if graph.IsOnGraphThread then
                if not disposed then
                    armed.Value <- true
            else
                graph.PostQueued armFn

    let cancelAll () =
        if not (isNull flights) && flights.Count > 0 then
            let active = flights.ToArray ()

            for i = 0 to active.Length - 1 do
                try
                    active[i].Cancel()
                with _ ->
                    ()

    let release () =
        disposed <- true
        changedHandlers.Clear ()
        canExecuteHandlers.Clear ()

        if not (isNull host) then
            host.RemoveCommand self

    let disposeOwnerFn () =
        owner.Dispose ()

    do
        owner.AddCleanup (fun () ->
            release ()
            cancelAll ())

    /// <summary>The failure recorded for an execution of <c>task</c> under <c>cts</c>, or null.</summary>
    let failureOf (task: Task) (cts: CancellationTokenSource) : exn =
        let failure =
            if task.IsFaulted then
                let inner = task.Exception.InnerExceptions

                if inner.Count = 1 then inner[0] else task.Exception :> exn
            elif task.IsCanceled then
                try
                    task.GetAwaiter().GetResult()
                    null
                with ex ->
                    ex
            else
                null

        match failure with
        | :? OperationCanceledException when cts.IsCancellationRequested -> null
        | _ -> failure

    let finishBatchFn () =
        running.Value <- running.Peek - 1
        error.Value <- pendingError

    /// <summary>Applies the outcome of <c>task</c>, the execution under <c>cts</c>. Graph thread only.</summary>
    let finish (cts: CancellationTokenSource) (completion: TaskCompletionSource<obj>) (task: Task) =
        if not (isNull flights) && flights.Count > 0 then
            let last = flights.Count - 1

            if obj.ReferenceEquals (flights[last], cts) then
                flights.RemoveAt last
            else
                flights.Remove cts |> ignore

        if not disposed then
            let failure = failureOf task cts

            if
                obj.ReferenceEquals (latest, cts)
                && (not (obj.ReferenceEquals (error.Peek, failure))
                    || not graph.Options.Journal.IsNone)
            then
                pendingError <- failure
                graph.RunBatch finishBatchFn
            else
                running.Value <- running.Peek - 1
        else
            // Observes the exception, so it reaches no UnobservedTaskException handler.
            task.Exception |> ignore

        if obj.ReferenceEquals (latest, cts) then
            latest <- null

        cts.Dispose ()

        if not (isNull completion) then
            if task.IsFaulted then
                completion.TrySetException task.Exception.InnerExceptions
                |> ignore
            elif task.IsCanceled then
                completion.TrySetCanceled () |> ignore
            else
                completion.TrySetResult null |> ignore

    let syncInvokeFn (p: obj) =
        syncExecute.Invoke p

    let syncBatchFn () =
        let parameter = pendingSyncParam
        pendingSyncParam <- null
        latest <- null
        running.Value <- running.Peek + 1

        let failure =
            try
                if not (graph.IsTracking ()) then
                    syncExecute.Invoke parameter
                else
                    graph.RunUntrackedWith (syncInvokeFn, parameter)

                null
            with ex ->
                ex

        syncFailure <- failure

        if not disposed then
            if
                not (obj.ReferenceEquals (error.Peek, failure))
                || not graph.Options.Journal.IsNone
            then
                running.Value <- running.Peek - 1
                error.Value <- failure
            else
                running.Value <- running.Peek - 1

    let runSync (parameter: obj) (completion: TaskCompletionSource<obj>) : Task =
        pendingSyncParam <- parameter
        syncFailure <- null
        graph.RunBatch syncBatchFn
        let failure = syncFailure
        syncFailure <- null

        if not (isNull completion) then
            if not (isNull failure) then
                completion.TrySetException failure |> ignore
            else
                completion.TrySetResult null |> ignore

            completion.Task :> Task
        elif not (isNull failure) then
            Task.FromException failure
        else
            Task.CompletedTask

    /// <summary>Starts an async execution when the command is enabled. Graph thread only.</summary>
    let launchAsync (parameter: obj) (completion: TaskCompletionSource<obj>) (needTask: bool) : Task =
        if policy = CommandPolicy.CancelPrevious then
            cancelAll ()

        let cts = new CancellationTokenSource ()

        if isNull flights then
            flights <- ResizeArray<CancellationTokenSource>()

        flights.Add cts
        latest <- cts
        running.Value <- running.Peek + 1

        let task =
            try
                let raw =
                    if not (graph.IsTracking ()) then
                        asyncExecute.Invoke (parameter, cts.Token)
                    else
                        graph.RunUntracked (fun () -> asyncExecute.Invoke (parameter, cts.Token))

                if isNull raw then Task.CompletedTask else raw
            with ex ->
                Task.FromException ex

        if task.IsCompleted then
            let faulted = task.IsFaulted
            let canceled = task.IsCanceled
            let agg = if faulted then task.Exception else null
            finish cts completion task

            if not (isNull completion) then
                completion.Task :> Task
            elif not needTask then
                Task.CompletedTask
            elif faulted then
                if agg.InnerExceptions.Count = 1 then
                    Task.FromException agg.InnerExceptions[0]
                else
                    let tcs =
                        TaskCompletionSource<obj>(TaskCreationOptions.RunContinuationsAsynchronously)

                    tcs.TrySetException agg.InnerExceptions |> ignore
                    tcs.Task :> Task
            elif canceled then
                cancelledTask
            else
                Task.CompletedTask
        else
            let target =
                if not (isNull completion) then
                    completion
                elif needTask then
                    TaskCompletionSource<obj>(TaskCreationOptions.RunContinuationsAsynchronously)
                else
                    null

            task.ContinueWith (
                Action<Task>(fun t ->
                    if graph.IsOnGraphThread then
                        finish cts target t
                    else
                        graph.PostQueued (fun () -> finish cts target t)),
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            )
            |> ignore

            if isNull target then
                Task.CompletedTask
            else
                target.Task :> Task

    let startWithResult (parameter: obj) (completion: TaskCompletionSource<obj>) (needTask: bool) : Task =
        try
            arm ()

            if disposed || not (readEnabledUntracked ()) then
                if not (isNull completion) then
                    completion.TrySetResult null |> ignore
                    completion.Task :> Task
                else
                    Task.CompletedTask
            elif not (isNull syncExecute) then
                runSync parameter completion
            else
                launchAsync parameter completion needTask
        with ex ->
            if not (isNull completion) then
                completion.TrySetException ex |> ignore
                completion.Task :> Task
            elif needTask then
                Task.FromException ex
            else
                Task.CompletedTask

    /// <summary>
    /// Reads the command's nodes, tracked, stores the new state, and returns the changed properties as flags. Graph
    /// thread only.
    /// </summary>
    let refresh () =
        if disposed || not armed.Value then
            0
        else
            let nextCanRun = readEnabled ()
            let nextRunning = running.Value > 0
            let nextError = error.Value
            let mutable changes = 0

            if nextCanRun <> canRun then
                canRun <- nextCanRun
                changes <- changes ||| 1

            if nextRunning <> isRunning then
                isRunning <- nextRunning
                changes <- changes ||| 2

            if not (obj.ReferenceEquals (nextError, lastError)) then
                lastError <- nextError
                changes <- changes ||| 4

            changes

    let raiseChanges (changes: int) (errors: byref<ResizeArray<exn>>) =
        let from = box self

        if changes &&& 1 <> 0 then
            canExecuteHandlers.Raise (from, EventArgs.Empty, liveFn, &errors)
            changedHandlers.Raise (from, canRunArgs, liveFn, &errors)

        if changes &&& 2 <> 0 then
            changedHandlers.Raise (from, isRunningArgs, liveFn, &errors)

        if changes &&& 4 <> 0 then
            changedHandlers.Raise (from, errorArgs, liveFn, &errors)

    let raiseStandalone (changes: int) =
        let mutable errors: ResizeArray<exn> = null
        raiseChanges changes &errors

        if not (isNull errors) && errors.Count > 0 then
            ExceptionDispatchInfo.Capture(errors[0]).Throw()

    let notify () =
        let changes = refresh ()

        if
            changes <> 0
            && (not canExecuteHandlers.IsEmpty
                || not changedHandlers.IsEmpty)
        then
            graph.RunUntrackedWith (raiseStandalone, changes)

    let canExecuteChangedEvent =
        { new IDelegateEvent<EventHandler> with
            member _.AddHandler h =
                canExecuteHandlers.Add h
                arm ()

            member _.RemoveHandler h =
                canExecuteHandlers.Remove h
        }

    let propertyChangedEvent =
        { new IDelegateEvent<PropertyChangedEventHandler> with
            member _.AddHandler h =
                changedHandlers.Add h
                arm ()

            member _.RemoveHandler h =
                changedHandlers.Remove h
        }

    do
        if isNull host then
            let entered = graph.Enter "Running with an owner"

            try
                graph.CurrentOwner |> ignore
                graph.RunOwned (owner, fun () -> Effect.Create (graph, notify) |> ignore)
            finally
                if entered then
                    graph.Release ()

    member internal _.Refresh() =
        refresh ()

    member internal _.HasHandlers =
        not canExecuteHandlers.IsEmpty
        || not changedHandlers.IsEmpty

    member internal _.RaiseChanges(changes: int, errors: byref<ResizeArray<exn>>) =
        raiseChanges changes &errors

    member _.Graph = graph

    /// <summary>The root scope of the command's nodes; disposed by <c>Dispose</c>.</summary>
    member _.Owner = owner

    /// <summary>
    /// The derived predicate: <c>canExecute ()</c>, and under <c>CommandPolicy.Disable</c> no execution in flight.
    /// </summary>
    member _.Enabled = enabled

    /// <summary>
    /// <c>Enabled</c> when it is ready, and false while it is pending or failed. On the graph's thread the read is
    /// tracked.
    /// </summary>
    /// <remarks>Off the graph's thread, or after disposal, <c>CanRun</c> is the value last notified.</remarks>
    member _.CanRun: bool =
        if graph.IsOnGraphThread && not disposed then
            readEnabled ()
        else
            canRun

    /// <summary>True while an execution is in flight. On the graph's thread the read is tracked.</summary>
    /// <remarks>Off the graph's thread, or after disposal, <c>IsRunning</c> is the value last notified.</remarks>
    member _.IsRunning: bool =
        if graph.IsOnGraphThread && not disposed then
            running.Value > 0
        else
            isRunning

    /// <summary>
    /// The latest execution's failure, or null after it succeeds or is cancelled. On the graph's thread the read is
    /// tracked.
    /// </summary>
    /// <remarks>Off the graph's thread, or after disposal, <c>Error</c> is the value last notified.</remarks>
    member _.Error: exn =
        if graph.IsOnGraphThread && not disposed then
            error.Value
        else
            lastError

    /// <summary>
    /// Starts an execution with <c>parameter</c> when the command is enabled, through <c>Graph.Dispatch</c>. The task
    /// completes with the execution, after <c>IsRunning</c> and <c>Error</c> are updated.
    /// </summary>
    /// <remarks>
    /// A disabled or disposed command returns a completed task. A failed execution faults the task, and a cancelled one
    /// cancels it.
    /// </remarks>
    member _.ExecuteAsync([<Optional; DefaultParameterValue(null: obj)>] parameter: obj) : Task =
        if disposed then
            Task.CompletedTask
        elif graph.IsOnGraphThread then
            startWithResult parameter null true
        else
            let completion =
                TaskCompletionSource<obj>(TaskCreationOptions.RunContinuationsAsynchronously)

            graph.PostQueued (fun () ->
                startWithResult parameter completion true
                |> ignore)

            completion.Task :> Task

    /// <summary>
    /// Starts an execution with <c>parameter</c> as <c>ExecuteAsync</c> does, and discards its task. A failure is stored
    /// in <c>Error</c> only.
    /// </summary>
    member _.Execute([<Optional; DefaultParameterValue(null: obj)>] parameter: obj) : unit =
        if not disposed then
            if graph.IsOnGraphThread then
                startWithResult parameter null false |> ignore
            else
                graph.PostQueued (fun () -> startWithResult parameter null false |> ignore)

    /// <summary>Cancels the token of every execution in flight, through <c>Graph.Dispatch</c>.</summary>
    member _.Cancel() =
        if graph.IsOnGraphThread then
            cancelAll ()
        else
            graph.PostQueued cancelAll

    /// <summary>The value of <c>CanRun</c> last notified. Safe to call from any thread.</summary>
    member _.CanExecute(_parameter: obj) : bool =
        arm ()
        canRun

    /// <summary>
    /// Raised when <c>CanExecute</c> changes, on the graph's thread or posted to the context current when the handler
    /// subscribed.
    /// </summary>
    [<CLIEvent>]
    member _.CanExecuteChanged = canExecuteChangedEvent

    /// <summary>
    /// Raised as <c>"CanRun"</c>, <c>"IsRunning"</c> and <c>"Error"</c>, on the graph's thread or posted to the context
    /// current when the handler subscribed.
    /// </summary>
    [<CLIEvent>]
    member _.PropertyChanged = propertyChangedEvent

    /// <summary>
    /// Cancels the executions in flight, disposes the command's nodes and drops every handler. Idempotent; dispatched to
    /// the graph's thread when called from another.
    /// </summary>
    member _.Dispose() =
        if not disposed then
            release ()

            if graph.IsOnGraphThread then
                owner.Dispose ()
            else
                graph.PostQueued disposeOwnerFn

    interface ICommand with
        member this.CanExecute(parameter) =
            this.CanExecute parameter

        member this.Execute(parameter) =
            this.Execute parameter

        [<CLIEvent>]
        member this.CanExecuteChanged = canExecuteChangedEvent

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member _.PropertyChanged = propertyChangedEvent

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()

/// <summary>
/// <c>INotifyPropertyChanged</c> and <c>INotifyDataErrorInfo</c> over graph nodes, for XAML view models. Each registered
/// property raises <c>PropertyChanged</c> once per settled change, and errors of failed properties surface through
/// <c>GetErrors</c>.
/// </summary>
/// <remarks>
/// <para>
/// Events carry <c>sender</c>, or the bindings when <c>sender</c> is null. Each handler runs on the
/// <c>SynchronizationContext</c> current when it subscribed: inline when it subscribed without one or when the raise
/// already runs on it, posted otherwise. The notifying effect runs on the graph's thread.
/// </para>
/// <para>
/// The bindings own a root scope created under the scope current at construction. <c>Dispose</c>, or disposing that
/// scope, disposes every node the bindings created and drops every handler. Register properties on the graph's thread.
/// </para>
/// </remarks>
/// <example>
/// <code lang="csharp">
/// public sealed class OrderViewModel : ObservableObject, IDisposable
/// {
///     readonly ReactiveBindings bindings;
///     readonly BoundSignal&lt;int&gt; quantity;
///     readonly BoundValue&lt;decimal&gt; total;
///
///     public OrderViewModel(Graph graph, decimal price)
///     {
///         bindings = new ReactiveBindings(this, graph);
///         bindings.PropertyChanged += (_, e) =&gt; OnPropertyChanged(e);
///         quantity = bindings.Writable(nameof(Quantity), 1);
///         total = bindings.Computed(nameof(Total), () =&gt; Quantity * price);
///     }
///
///     public int Quantity { get =&gt; quantity.Value; set =&gt; quantity.Value = value; }
///     public decimal Total =&gt; total.Value;
///     public void Dispose() =&gt; bindings.Dispose();
/// }
/// </code>
/// </example>
[<Sealed>]
type ReactiveBindings(sender: obj, graph: Graph) as self =
    static let loadingArgs = PropertyChangedEventArgs "IsLoading"
    static let hasErrorsArgs = PropertyChangedEventArgs "HasErrors"

    let changedHandlers =
        ContextHandlers<PropertyChangedEventHandler, PropertyChangedEventArgs>(HandlerDispatch.invokePropertyChanged)

    let errorsHandlers =
        ContextHandlers<EventHandler<DataErrorsChangedEventArgs>, DataErrorsChangedEventArgs>(HandlerDispatch.invokeErrorsChanged)

    let gate = obj ()
    let mutable slots: IBoundSlot[] = Array.Empty ()
    let mutable commands: ReactiveCommand[] = Array.Empty ()

    [<VolatileField>]
    let mutable loading = false

    [<VolatileField>]
    let mutable hasErrors = false

    [<VolatileField>]
    let mutable disposed = false

    let version = Signal<int>(graph, 0)
    let owner = graph.RunRoot id
    let cachedNotes = Notifications ()
    let mutable notesInUse = false

    let source () =
        if isNull sender then box self else sender

    let liveFn () =
        not disposed

    let release () =
        disposed <- true
        changedHandlers.Clear ()
        errorsHandlers.Clear ()

    let disposeOwnerFn () =
        owner.Dispose ()

    do owner.AddCleanup release

    /// <summary>Runs <c>body</c> with <c>graph</c> active and <c>owner</c> as the scope new nodes attach to.</summary>
    let inScope (body: unit -> 'R) : 'R =
        let activation =
            match Graph.TryCurrent with
            | ValueSome current when obj.ReferenceEquals (current, graph) -> null
            | _ -> graph.Activate ()

        try
            Api.runWithOwner owner body
        finally
            if not (isNull activation) then
                activation.Dispose ()

    let inOwner (body: unit -> 'R) : 'R =
        let entered = graph.Enter "Running with an owner"

        try
            graph.CurrentOwner |> ignore
            graph.RunOwned (owner, body)
        finally
            if entered then
                graph.Release ()

    let aggregateSlots (current: IBoundSlot[]) =
        let mutable anyLoading = false
        let mutable anyErrors = false

        for i = 0 to current.Length - 1 do
            let slot = current[i]

            if slot.Loading then
                anyLoading <- true

            if not (isNull slot.Error) then
                anyErrors <- true

        struct (anyLoading, anyErrors)

    let hasCommandHandlers (cmdChanges: ResizeArray<struct (ReactiveCommand * int)>) =
        let mutable any = false
        let mutable i = 0

        while not any && i < cmdChanges.Count do
            let struct (cmd, _) = cmdChanges[i]

            if cmd.HasHandlers then any <- true else i <- i + 1

        any

    let raiseAll (notes: Notifications) =
        let mutable errors: ResizeArray<exn> = null
        let from = source ()

        if not changedHandlers.IsEmpty then
            let changed = notes.Changed

            for i = 0 to changed.Count - 1 do
                changedHandlers.Raise (from, changed[i], liveFn, &errors)

        if not errorsHandlers.IsEmpty then
            let errorsChanged = notes.ErrorsChanged

            for i = 0 to errorsChanged.Count - 1 do
                errorsHandlers.Raise (from, errorsChanged[i], liveFn, &errors)

        let cmdChanges = notes.CommandChanges

        for i = 0 to cmdChanges.Count - 1 do
            let struct (command, changes) = cmdChanges[i]
            command.RaiseChanges (changes, &errors)

        if not (isNull errors) && errors.Count > 0 then
            ExceptionDispatchInfo.Capture(errors[0]).Throw()

    let notify () =
        version.Value |> ignore

        let notes =
            if not notesInUse then
                notesInUse <- true
                cachedNotes
            else
                Notifications ()

        try
            let current = slots
            let mutable nextLoading = false
            let mutable nextErrors = false

            for i = 0 to current.Length - 1 do
                let slot = current[i]
                slot.Refresh notes

                if slot.Loading then
                    nextLoading <- true

                if not (isNull slot.Error) then
                    nextErrors <- true

            if nextLoading <> loading then
                loading <- nextLoading
                notes.Changed.Add loadingArgs

            if nextErrors <> hasErrors then
                hasErrors <- nextErrors
                notes.Changed.Add hasErrorsArgs

            let currentCommands = commands

            for i = 0 to currentCommands.Length - 1 do
                let command = currentCommands[i]
                let changes = command.Refresh ()

                if changes <> 0 then
                    notes.CommandChanges.Add (struct (command, changes))

            if not disposed && notes.Any then
                let raiseNeeded =
                    (notes.Changed.Count > 0
                     && not changedHandlers.IsEmpty)
                    || (notes.ErrorsChanged.Count > 0
                        && not errorsHandlers.IsEmpty)
                    || hasCommandHandlers notes.CommandChanges

                if raiseNeeded then
                    if not (graph.IsTracking ()) then
                        raiseAll notes
                    else
                        graph.RunUntrackedWith (raiseAll, notes)
        finally
            if obj.ReferenceEquals (notes, cachedNotes) then
                cachedNotes.Clear ()
                notesInUse <- false

    do inOwner (fun () -> Effect.Create (graph, notify) |> ignore)

    let register (slot: IBoundSlot) =
        if disposed then
            raise (ObjectDisposedException (nameof ReactiveBindings))

        lock gate (fun () ->
            for i = 0 to slots.Length - 1 do
                if slots[i].Name = slot.Name then
                    raise (ArgumentException ($"A property named '%s{slot.Name}' is already bound.", "name"))

            let prev = slots
            let next = Array.zeroCreate (prev.Length + 1)

            if prev.Length > 0 then
                Array.Copy (prev, next, prev.Length)

            next[prev.Length] <- slot
            slots <- next)

        let struct (nextLoading, nextErrors) = aggregateSlots slots
        loading <- nextLoading
        hasErrors <- nextErrors
        version.Value <- version.Peek + 1

    let registerCommand (command: ReactiveCommand) =
        lock gate (fun () ->
            let prev = commands
            let next = Array.zeroCreate (prev.Length + 1)

            if prev.Length > 0 then
                Array.Copy (prev, next, prev.Length)

            next[prev.Length] <- command
            commands <- next)

        version.Value <- version.Peek + 1
        command

    let createCommand (asyncExecute: Func<obj, CancellationToken, Task>) (syncExecute: Action<obj>) (canExecute: Func<bool>) policy =
        if disposed then
            raise (ObjectDisposedException (nameof ReactiveBindings))

        inOwner (fun () -> new ReactiveCommand (graph, asyncExecute, syncExecute, canExecute, policy, self :> ICommandHost))
        |> registerCommand

    let bindSignal (name: string) (signal: Signal<'T>) =
        let bound = BoundSignal<'T>(graph, owner, name, signal)
        register bound
        bound

    let propertyChangedEvent =
        { new IDelegateEvent<PropertyChangedEventHandler> with
            member _.AddHandler h =
                changedHandlers.Add h

            member _.RemoveHandler h =
                changedHandlers.Remove h
        }

    let errorsChangedEvent =
        { new IDelegateEvent<EventHandler<DataErrorsChangedEventArgs>> with
            member _.AddHandler h =
                errorsHandlers.Add h

            member _.RemoveHandler h =
                errorsHandlers.Remove h
        }

    interface ICommandHost with
        member _.RemoveCommand(command: ReactiveCommand) =
            lock gate (fun () ->
                let prev = commands
                let mutable index = prev.Length - 1
                let mutable found = -1

                while found < 0 && index >= 0 do
                    if obj.ReferenceEquals (prev[index], command) then
                        found <- index
                    else
                        index <- index - 1

                if found >= 0 then
                    if prev.Length = 1 then
                        commands <- Array.Empty ()
                    else
                        let next = Array.zeroCreate (prev.Length - 1)

                        if found > 0 then
                            Array.Copy (prev, 0, next, 0, found)

                        if found < prev.Length - 1 then
                            Array.Copy (prev, found + 1, next, found, prev.Length - found - 1)

                        commands <- next)

    /// <summary>Bindings on <c>Graph.Current</c> whose events carry <c>sender</c>.</summary>
    /// <exception cref="T:System.InvalidOperationException">No graph is active on the calling thread.</exception>
    new(sender: obj) = new ReactiveBindings (sender, Graph.Current)

    /// <summary>Bindings on <c>Graph.Current</c> whose events carry the bindings themselves.</summary>
    /// <exception cref="T:System.InvalidOperationException">No graph is active on the calling thread.</exception>
    new() = new ReactiveBindings (null, Graph.Current)

    member _.Graph = graph

    /// <summary>The root scope of every node the bindings create; disposed by <c>Dispose</c>.</summary>
    member _.Owner = owner

    /// <summary>
    /// A read-only property <c>name</c> holding <c>compute ()</c>. <c>compute</c> re-runs when a value it read changes,
    /// as a memo's body does.
    /// </summary>
    /// <remarks>
    /// <c>compute</c> is pure: creating a node in it fails the property. A pending or failed read in <c>compute</c>
    /// makes the property loading or failed, and it keeps showing its last settled value.
    /// </remarks>
    /// <exception cref="T:System.ArgumentException">A property named <c>name</c> is already bound.</exception>
    /// <exception cref="T:System.ObjectDisposedException">The bindings are disposed.</exception>
    member this.Computed<'T>(name: string, compute: Func<'T>) : BoundValue<'T> =
        this.Computed (name, compute, null)

    /// <summary>
    /// <c>Computed</c>, also raising <c>PropertyChanged</c> for <c>loadingName</c> when the property starts or stops
    /// loading.
    /// </summary>
    /// <exception cref="T:System.ArgumentException">A property named <c>name</c> is already bound.</exception>
    /// <exception cref="T:System.ObjectDisposedException">The bindings are disposed.</exception>
    member _.Computed<'T>(name: string, compute: Func<'T>, loadingName: string) : BoundValue<'T> =
        if disposed then
            raise (ObjectDisposedException (nameof ReactiveBindings))

        let memo =
            inOwner (fun () -> Memo<'T>.Create(graph, (fun _ -> compute.Invoke ()), ScopeMode.Pure))

        let bound = BoundValue<'T>(graph, owner, name, loadingName, memo)
        register bound
        bound

    /// <summary>A two-way property <c>name</c> over a new signal holding <c>initial</c>.</summary>
    /// <exception cref="T:System.ArgumentException">A property named <c>name</c> is already bound.</exception>
    /// <exception cref="T:System.ObjectDisposedException">The bindings are disposed.</exception>
    member _.Writable<'T>(name: string, initial: 'T) : BoundSignal<'T> =
        bindSignal name (Signal<'T>(graph, initial))

    /// <summary>A two-way property <c>name</c> over <c>signal</c>, which stays usable after the bindings are disposed.</summary>
    /// <exception cref="T:System.ArgumentException">A property named <c>name</c> is already bound.</exception>
    /// <exception cref="T:System.ObjectDisposedException">The bindings are disposed.</exception>
    member _.Writable<'T>(name: string, signal: Signal<'T>) : BoundSignal<'T> =
        bindSignal name signal

    /// <summary>
    /// A command running <c>execute</c>, enabled while <c>canExecute ()</c> is true and <c>policy</c> allows. The
    /// bindings' notifying effect raises its events, and <c>Dispose</c> disposes it.
    /// </summary>
    /// <remarks>
    /// <c>canExecute</c> re-runs when a value it read changes, as a memo's body does; a null <c>canExecute</c> is always
    /// true. <c>execute</c> receives the parameter and a token cancelled by <c>Cancel</c>, <c>Dispose</c> and
    /// <c>CommandPolicy.CancelPrevious</c>.
    /// </remarks>
    /// <exception cref="T:System.ArgumentNullException"><c>execute</c> is null.</exception>
    /// <exception cref="T:System.ObjectDisposedException">The bindings are disposed.</exception>
    member _.Command
        (
            execute: Func<obj, CancellationToken, Task>,
            [<Optional; DefaultParameterValue(null: Func<bool>)>] canExecute: Func<bool>,
            [<Optional; DefaultParameterValue(CommandPolicy.Disable)>] policy: CommandPolicy
        ) : ReactiveCommand =
        if isNull execute then
            nullArg "execute"

        createCommand execute null canExecute policy

    /// <summary>
    /// A command running <c>execute</c> synchronously, enabled while <c>canExecute ()</c> is true. The writes
    /// <c>execute</c> makes are batched.
    /// </summary>
    /// <exception cref="T:System.ArgumentNullException"><c>execute</c> is null.</exception>
    /// <exception cref="T:System.ObjectDisposedException">The bindings are disposed.</exception>
    member _.Command(execute: Action<obj>, [<Optional; DefaultParameterValue(null: Func<bool>)>] canExecute: Func<bool>) : ReactiveCommand =
        if isNull execute then
            nullArg "execute"

        createCommand null execute canExecute CommandPolicy.Disable

    /// <summary>
    /// Runs <c>body</c> with the graph active and <c>Owner</c> as its scope, so the nodes it creates are disposed with
    /// the bindings.
    /// </summary>
    member _.Run<'T>(body: Func<'T>) : 'T =
        inScope body.Invoke

    /// <summary>
    /// Runs <c>body</c> with the graph active and <c>Owner</c> as its scope, so the nodes it creates are disposed with
    /// the bindings.
    /// </summary>
    member _.Run(body: Action) : unit =
        inScope body.Invoke

    /// <summary>True while any bound property is loading. Raises <c>PropertyChanged</c> as <c>"IsLoading"</c>.</summary>
    member _.IsLoading = loading

    /// <summary>True while any bound property is failed. Raises <c>PropertyChanged</c> as <c>"HasErrors"</c>.</summary>
    member _.HasErrors = hasErrors

    /// <summary>
    /// The error message of property <c>propertyName</c> while it is failed, or of every failed property for a null or
    /// empty name.
    /// </summary>
    member _.GetErrors(propertyName: string) : IEnumerable =
        if not hasErrors then
            Array.Empty<string>() :> IEnumerable
        else
            let current = slots
            let all = String.IsNullOrEmpty propertyName
            let messages = ResizeArray<string>()

            for i = 0 to current.Length - 1 do
                let slot = current[i]

                if
                    not (isNull slot.Error)
                    && (all || slot.Name = propertyName)
                then
                    messages.Add slot.Error.Message

            messages :> IEnumerable

    /// <summary>Raised on the graph's thread, or posted to the context current when the handler subscribed.</summary>
    [<CLIEvent>]
    member _.PropertyChanged = propertyChangedEvent

    /// <summary>Raised on the graph's thread, or posted to the context current when the handler subscribed.</summary>
    [<CLIEvent>]
    member _.ErrorsChanged = errorsChangedEvent

    /// <summary>
    /// Disposes every node the bindings created and drops every handler. Idempotent; dispatched to the graph's thread
    /// when called from another.
    /// </summary>
    /// <remarks>Signals passed to <c>Writable</c> stay usable.</remarks>
    member _.Dispose() =
        if not disposed then
            release ()

            if graph.IsOnGraphThread then
                owner.Dispose ()
            else
                graph.PostQueued disposeOwnerFn

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member _.PropertyChanged = propertyChangedEvent

    interface INotifyDataErrorInfo with
        member _.HasErrors = hasErrors

        member this.GetErrors(propertyName) =
            this.GetErrors propertyName

        [<CLIEvent>]
        member _.ErrorsChanged = errorsChangedEvent

    interface IDisposable with
        member this.Dispose() =
            this.Dispose ()

/// <summary>
/// A view model whose <c>PropertyChanged</c>, <c>ErrorsChanged</c>, <c>HasErrors</c>, <c>GetErrors</c> and
/// <c>Dispose</c> come from <c>Bindings</c>. Register properties with <c>Bindings.Computed</c> and
/// <c>Bindings.Writable</c>.
/// </summary>
/// <example>
/// <code lang="csharp">
/// public sealed class Cart : ReactiveObject
/// {
///     readonly BoundSignal&lt;int&gt; count;
///     readonly BoundValue&lt;int&gt; total;
///
///     public Cart(Graph graph) : base(graph)
///     {
///         count = Bindings.Writable(nameof(Count), 1);
///         total = Bindings.Computed(nameof(Total), () =&gt; Count * 5);
///     }
///
///     public int Count { get =&gt; count.Value; set =&gt; count.Value = value; }
///     public int Total =&gt; total.Value;
/// }
/// </code>
/// </example>
[<AbstractClass>]
type ReactiveObject(graph: Graph) as self =
    let bindings = new ReactiveBindings (self, graph)

    /// <summary>A view model on <c>Graph.Current</c>.</summary>
    /// <exception cref="T:System.InvalidOperationException">No graph is active on the calling thread.</exception>
    new() = new ReactiveObject (Graph.Current)

    member _.Bindings = bindings

    /// <summary>True while any bound property is loading.</summary>
    member _.IsLoading = bindings.IsLoading

    /// <summary>True while any bound property is failed.</summary>
    member _.HasErrors = bindings.HasErrors

    /// <summary>
    /// The error message of property <c>propertyName</c> while it is failed, or of every failed property for a null or
    /// empty name.
    /// </summary>
    member _.GetErrors(propertyName: string) =
        bindings.GetErrors propertyName

    [<CLIEvent>]
    member _.PropertyChanged = bindings.PropertyChanged

    [<CLIEvent>]
    member _.ErrorsChanged = bindings.ErrorsChanged

    /// <summary>Disposes <c>Bindings</c>.</summary>
    member _.Dispose() =
        bindings.Dispose ()

    interface INotifyPropertyChanged with
        [<CLIEvent>]
        member _.PropertyChanged = bindings.PropertyChanged

    interface INotifyDataErrorInfo with
        member _.HasErrors = bindings.HasErrors

        member _.GetErrors(propertyName) =
            bindings.GetErrors propertyName

        [<CLIEvent>]
        member _.ErrorsChanged = bindings.ErrorsChanged

    interface IDisposable with
        member _.Dispose() =
            bindings.Dispose ()
