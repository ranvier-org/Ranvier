module Ranvier.View.Foundation.Binding

open System
open Ranvier
open Ranvier.View.Foundation.Scheduling

type InitialWrite =
    | Immediate
    | Scheduled

type Policy<'T> =
    {
        Equals: 'T -> 'T -> bool
        Initial: InitialWrite
        Pending: unit -> 'T option
        Failed: exn -> 'T option
        Status: Reading<'T> -> unit
        Report: exn -> unit
    }

let private startCore rejectInitial registerCleanup isLive scheduling (policy: Policy<'T>) (read: unit -> Reading<'T>) (write: 'T -> unit) =
    if not (isLive ()) then
        raise (InvalidOperationException "Binding destination is disposed")

    let slot =
        match scheduling with
        | Synchronous -> None
        | Queued queue -> Some (queue, queue.Reserve ())

    let mutable owner: Owner option = None
    let mutable disposed = false
    let mutable first = true
    let mutable version = 0
    let mutable committed: 'T option = None
    let mutable constructing = rejectInitial
    let mutable setupFailure: exn option = None

    let live () =
        not disposed && isLive ()

    let cancel () =
        slot
        |> Option.iter (fun (queue, id) -> queue.Cancel id)

    let dispose () =
        if not disposed then
            disposed <- true
            version <- version + 1
            cancel ()
            owner |> Option.iter _.Dispose()

    registerCleanup dispose

    let report error =
        try
            Api.untrack (fun () -> policy.Report error)
        with reporterError ->
            raise (
                AggregateException (
                    "Binding error reporter failed: "
                    + error.Message
                    + "; "
                    + reporterError.Message,
                    [| error; reporterError |]
                )
            )

    let guarded action =
        try
            action ()
        with error ->
            report error
            reraise ()

    let createEffect body =
        if rejectInitial then
            Api.createEffect (fun () ->
                try
                    body ()
                with error ->
                    if constructing then
                        setupFailure <- Some error

                    reraise ())
        else
            Api.createEffect body

    try
        Api.createRoot (fun bindingOwner ->
            owner <- Some bindingOwner

            if disposed || not (isLive ()) then
                bindingOwner.Dispose ()
                raise (InvalidOperationException "Binding destination was disposed during registration")

            Api.onCleanup dispose

            createEffect (fun () ->
                if live () then
                    let reading =
                        try
                            read ()
                        with error ->
                            Reading.Failed error

                    let initial = first
                    first <- false
                    version <- version + 1
                    let ticket = version
                    cancel ()
                    Api.onCleanup cancel
                    let actionOwner = Api.getOwner ()

                    Api.untrack (fun () ->
                        if live () then
                            guarded (fun () -> policy.Status reading)

                            if live () then
                                let value =
                                    match reading with
                                    | Reading.Ready value -> Some value
                                    | Reading.Pending -> guarded policy.Pending
                                    | Reading.Failed error ->
                                        report error

                                        if live () then
                                            guarded (fun () -> policy.Failed error)
                                        else
                                            None

                                match value with
                                | None -> ()
                                | Some value when live () ->
                                    let commit () =
                                        if
                                            live ()
                                            && not actionOwner.IsDisposed
                                            && version = ticket
                                        then
                                            Api.runWithOwner actionOwner (fun () ->
                                                Api.untrack (fun () ->
                                                    guarded (fun () ->
                                                        let changed =
                                                            committed
                                                            |> Option.forall (fun old -> not (policy.Equals old value))

                                                        if changed && live () && version = ticket then
                                                            write value

                                                            if version = ticket then
                                                                committed <- Some value)))

                                    match scheduling with
                                    | Synchronous -> commit ()
                                    | Queued _ when initial && policy.Initial = Immediate -> commit ()
                                    | Queued queue -> queue.Enqueue (snd slot.Value, commit)
                                | _ -> ())))

        constructing <- false

        match setupFailure with
        | Some error ->
            dispose ()
            raise error
        | None -> ()
    with _ ->
        dispose ()
        reraise ()

let start registerCleanup isLive scheduling policy read write =
    startCore false registerCleanup isLive scheduling policy read write

/// <summary>Starts a binding and rejects setup when an initial policy callback or immediate setter fails.</summary>
/// <remarks>Failed readings use the selected outcome policy; deferred commit errors are reported by the queue.</remarks>
let startChecked registerCleanup isLive scheduling policy read write =
    startCore true registerCleanup isLive scheduling policy read write
