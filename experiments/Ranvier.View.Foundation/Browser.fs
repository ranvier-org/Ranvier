module Ranvier.View.Foundation.Browser

open Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation

type Metrics =
    {
        mutable Owners: int
        mutable Effects: int
        mutable Setups: int
        mutable Writes: int
    }

let metrics () =
    {
        Owners = 0
        Effects = 0
        Setups = 0
        Writes = 0
    }

[<Emit("document.createElement($0)")>]
let private element (tag: string) : HTMLElement = jsNative

[<Emit("document.createTextNode($0)")>]
let private text (value: string) : Node = jsNative

[<Emit("$0.remove()")>]
let private remove (node: Node) : unit = jsNative

[<Emit("$0.value = $1")>]
let private inputValue (node: HTMLElement) (value: string) : unit = jsNative

let mount (graph: Graph) (host: HTMLElement) (report: exn list -> unit) (counts: Metrics option) (build: Backend -> unit) : unit -> unit =
    let roots = ResizeArray<Node>()

    let root =
        Scope (
            report,
            fun () ->
                for node in roots do
                    remove node

                roots.Clear ()
        )

    let mutable parent: Node = host
    let mutable scope = root
    let mutable rootOwner: Owner option = None

    let count action =
        counts |> Option.iter action

    let append node =
        if obj.ReferenceEquals (parent, host) then
            roots.Add node

        parent.appendChild node |> ignore

    let backend =
        { new Backend() with
            member _.Text value =
                append (text value)

            member _.Element(tag, tokens, children) =
                let node = element (name tag)
                append node

                for token in tokens do
                    match token with
                    | Attribute (key, value) ->
                        validateAttribute key
                        node.setAttribute (key, value)
                    | InputValue value ->
                        if tag <> Input then
                            invalidArg "tokens" "Input value requires input"

                        inputValue node value
                    | Browser hook -> scope.OnCleanup (hook (box node))
                    | ReactiveProperty _ -> invalidOp "Reactive properties require the owned-range interpreter"

                if tag <> Input then
                    let previous = parent
                    parent <- node

                    try
                        children ()
                    finally
                        parent <- previous

            member _.Bind(equals, read) =
                let node = text ""
                append node
                let mutable committed: string option = None
                count (fun c -> c.Effects <- c.Effects + 1)

                Api.createEffect (fun () ->
                    let value = read ()

                    let changed =
                        match committed with
                        | None -> true
                        | Some old -> not (equals old value)

                    if changed then
                        Api.untrack (fun () -> node.textContent <- Some value)
                        committed <- Some value
                        count (fun c -> c.Writes <- c.Writes + 1))

            member _.Keyed(_, _, _) =
                invalidOp "The baseline browser interpreter does not support keyed dynamic lists"

            member _.Component setup =
                let previous = scope
                let child = scope.Child ignore
                scope <- child

                try
                    Api.createRoot (fun owner ->
                        count (fun c ->
                            c.Owners <- c.Owners + 1
                            c.Setups <- c.Setups + 1)

                        Api.onCleanup child.Dispose
                        child.OnCleanup owner.Dispose
                        Api.untrack (fun () -> setup { OnCleanup = child.OnCleanup }))
                finally
                    scope <- previous
        }

    try
        graph.Run (fun () ->
            Api.createRoot (fun owner ->
                rootOwner <- Some owner
                count (fun c -> c.Owners <- c.Owners + 1)
                Api.onCleanup root.Dispose
                Api.untrack (fun () -> build backend)))
    with _ ->
        root.Dispose ()
        rootOwner |> Option.iter _.Dispose()
        reraise ()

    fun () ->
        root.Dispose ()
        rootOwner |> Option.iter _.Dispose()
