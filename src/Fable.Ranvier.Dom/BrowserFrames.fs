namespace Ranvier.Dom

open System
open System.Collections.Generic
open Fable.Core

/// <summary>Animation frames, timers and microtasks of a browser document's Window.</summary>
module BrowserFrames =
    [<Emit("$0.defaultView")>]
    let private windowOf (document: Document) : obj = jsNative

    [<Emit("$0.performance.now()")>]
    let private now (window: obj) : float = jsNative

    [<Emit("$0.requestAnimationFrame($1)")>]
    let private requestFrame (window: obj) (callback: float -> unit) : int = jsNative

    [<Emit("$0.cancelAnimationFrame($1)")>]
    let private cancelFrame (window: obj) (id: int) : unit = jsNative

    [<Emit("$0.setTimeout($1, $2)")>]
    let private setTimeout (window: obj) (callback: unit -> unit) (milliseconds: float) : int = jsNative

    [<Emit("$0.clearTimeout($1)")>]
    let private clearTimeout (window: obj) (id: int) : unit = jsNative

    [<Emit("$0.queueMicrotask($1)")>]
    let private queueMicrotask (window: obj) (callback: unit -> unit) : unit = jsNative

    /// <summary>A pending animation frame request; disposal cancels it.</summary>
    /// <remarks>Each ticket guards its own active state so browser handle reuse cannot cancel a later frame.</remarks>
    [<Sealed>]
    type private FrameRequest(window: obj, frames: HashSet<int>, callback: float -> unit) =
        let mutable handle = 0
        let mutable active = false

        member _.Start() =
            active <- true

            handle <-
                requestFrame window (fun time ->
                    if active then
                        active <- false
                        frames.Remove handle |> ignore
                        callback time)

            if active then
                frames.Add handle |> ignore

        interface IDisposable with
            member _.Dispose() =
                if active then
                    active <- false
                    frames.Remove handle |> ignore
                    cancelFrame window handle

    /// <summary>A scheduler over <c>document</c>'s Window; <c>Now</c> is <c>performance.now()</c>.</summary>
    let forDocument (document: Document) : FrameScheduler =
        if isNull (box document) then
            nullArg "document"

        let window = windowOf document

        if isNull window then
            raise (NotSupportedException "Frame scheduling requires a document with a Window")

        let mutable disposed = false
        let mutable next = 0
        let pending = Dictionary<int, unit -> unit>()
        let frames = HashSet<int>()

        let schedule (start: (unit -> unit) -> (unit -> unit)) (run: unit -> unit) =
            if disposed then
                raise (ObjectDisposedException (nameof FrameScheduler))

            next <- next + 1
            let id = next

            let invoke () =
                if pending.Remove id then
                    run ()

            pending[id] <- start invoke

            { new IDisposable with
                member _.Dispose() =
                    match pending.TryGetValue id with
                    | true, cancel ->
                        pending.Remove id |> ignore
                        cancel ()
                    | _ -> ()
            }

        { new FrameScheduler with
            member _.Now() =
                now window

            member _.Frame callback =
                if disposed then
                    raise (ObjectDisposedException (nameof FrameScheduler))

                let request = new FrameRequest (window, frames, callback)
                request.Start ()
                request :> IDisposable

            member _.Timeout(milliseconds, callback) =
                schedule
                    (fun invoke ->
                        let id = setTimeout window invoke (max 0.0 milliseconds)
                        fun () -> clearTimeout window id)
                    callback

            member _.Microtask callback =
                schedule
                    (fun invoke ->
                        queueMicrotask window invoke
                        ignore)
                    callback

            member _.Dispose() =
                if not disposed then
                    disposed <- true

                    for cancel in Seq.toArray pending.Values do
                        cancel ()

                    pending.Clear ()

                    for handle in frames do
                        cancelFrame window handle

                    frames.Clear ()
        }
