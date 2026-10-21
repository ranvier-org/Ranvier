namespace Ranvier.View.Twin

open System
open System.Collections.Generic
open System.Runtime.CompilerServices
open Ranvier.View.Testing

/// <summary>A listener raised during a dispatch; the dispatch ran every other listener first.</summary>
exception TwinListenerException of eventType: string * inner: exn with
    override this.Message = $"A '{this.eventType}' listener raised: {this.inner}"

[<AllowNullLiteral>]
type TwinEvent(eventType: string, kind: EventKind, bubbles: bool, cancelable: bool, init: IDictionary<string, obj>) =
    member _.Type = eventType
    member _.Kind = kind
    member _.Bubbles = bubbles
    member _.Cancelable = cancelable
    member _.Init = init
    member val Target: TwinNode = null with get, set
    member val CurrentTarget: TwinNode = null with get, set
    member val CurrentTargetOverride: unit -> obj = Unchecked.defaultof<_> with get, set
    member val Phase = 0 with get, set
    member val PropagationStopped = false with get, set
    member val ImmediateStopped = false with get, set
    member val DefaultPrevented = false with get, set

    member this.StopPropagation() =
        this.PropagationStopped <- true

    member this.StopImmediatePropagation() =
        this.PropagationStopped <- true
        this.ImmediateStopped <- true

    member this.PreventDefault() =
        if cancelable then
            this.DefaultPrevented <- true

type EventProxy() =
    inherit System.Reflection.DispatchProxy()
    member val Event: TwinEvent = null with get, set

    override this.Invoke(targetMethod, args) =
        let e = this.Event
        let name = targetMethod.Name

        let display =
            if name.StartsWith "get_" || name.StartsWith "set_" then
                name.Substring 4
            else
                name

        let value: obj =
            match name with
            | "get_type" -> box e.Type
            | "get_target" -> BridgeCore.ProxyOf e.Target
            | "get_currentTarget" ->
                if isNull (box e.CurrentTargetOverride) then
                    BridgeCore.ProxyOf e.CurrentTarget
                else
                    e.CurrentTargetOverride ()
            | "get_eventPhase" -> box (float e.Phase)
            | "get_bubbles" -> box e.Bubbles
            | "get_cancelable" -> box e.Cancelable
            | "get_cancelBubble" -> box e.PropagationStopped
            | "set_cancelBubble" ->
                (if (args[0] :?> bool) then
                     e.StopPropagation ())

                null
            | "get_defaultPrevented" -> box e.DefaultPrevented
            | "get_isTrusted" -> box false
            | "preventDefault" ->
                e.PreventDefault ()
                null
            | "stopPropagation" ->
                e.StopPropagation ()
                null
            | "stopImmediatePropagation" ->
                e.StopImmediatePropagation ()
                null
            | _ when
                name.StartsWith "get_"
                && e.Init.ContainsKey display
                ->
                match e.Init[display] with
                | :? TwinNode as node -> BridgeCore.ProxyOf node
                | value -> value
            | _ ->
                raise (
                    NotSupportedException (
                        "The DOM twin does not implement "
                        + targetMethod.DeclaringType.Name
                        + "."
                        + display
                    )
                )

        BridgeCore.Serve (targetMethod.DeclaringType.Name + "." + display)
        BridgeCore.Wrap (targetMethod.ReturnType, value)

[<RequireQualifiedAccess>]
module Events =
    type private Listener =
        {
            Type: string
            Capture: bool
            Callback: obj -> unit
            mutable Removed: bool
        }

    let private listeners = ConditionalWeakTable<TwinNode, ResizeArray<Listener>>()
    let private proxies = ConditionalWeakTable<TwinEvent, obj>()

    let private kindOf (eventType: string) =
        match eventType with
        | "click"
        | "dblclick"
        | "auxclick"
        | "contextmenu" -> Pointer
        | t when t.StartsWith "pointer" -> Pointer
        | t when t.StartsWith "mouse" -> Mouse
        | t when t.StartsWith "key" -> Keyboard
        | "input"
        | "beforeinput" -> Input
        | t when t.StartsWith "focus" || t = "blur" -> Focus
        | _ -> Plain

    let create (spec: EventSpec) : TwinEvent =
        let init = Dictionary<string, obj>()

        for key, value in spec.Init do
            init[key] <- value

        TwinEvent (spec.Type, spec.Kind, spec.Bubbles, spec.Cancelable, init)

    let proxyOf (event: TwinEvent) : obj =
        proxies.GetValue (
            event,
            fun e ->
                let iface = BridgeCore.Interface (EventSpec.constructorName e.Kind)

                let proxy =
                    System.Reflection.DispatchProxy.Create (iface, typeof<EventProxy>) :?> EventProxy

                proxy.Event <- e
                box proxy
        )

    let eventOf (value: obj) : TwinEvent =
        (value :?> EventProxy).Event

    let listen (node: TwinNode) (eventType: string) (capture: bool) (callback: obj -> unit) : unit -> unit =
        let list = listeners.GetValue (node, fun _ -> ResizeArray ())

        let entry =
            {
                Type = eventType
                Capture = capture
                Callback = callback
                Removed = false
            }

        list.Add entry

        fun () ->
            entry.Removed <- true
            list.Remove entry |> ignore

    let private invoke (node: TwinNode) (event: TwinEvent) (phase: int) (failures: ResizeArray<exn>) =
        match listeners.TryGetValue node with
        | true, list ->
            event.CurrentTarget <- node
            event.Phase <- phase

            for entry in List.ofSeq list do
                let applies =
                    entry.Type = event.Type
                    && not entry.Removed
                    && (phase = 2 || (phase = 1) = entry.Capture)

                if applies && not event.ImmediateStopped then
                    try
                        entry.Callback (proxyOf event)
                    with error ->
                        failures.Add error
        | false, _ -> ()

    /// <summary>
    /// Runs an interaction: once the outermost dispatch, click, focus or blur of the document returns, the document's
    /// <c>AfterDispatch</c> runs.
    /// </summary>
    let private outermost (document: TwinDocument) (action: unit -> 'T) : 'T =
        document.DispatchDepth <- document.DispatchDepth + 1

        let result =
            try
                action ()
            finally
                document.DispatchDepth <- document.DispatchDepth - 1

        if document.DispatchDepth = 0 then
            document.AfterDispatch ()

        result

    let private dispatchNow (target: TwinNode) (event: TwinEvent) : bool =
        target.Document.Check ("dispatch " + event.Type)
        event.Target <- target
        let path = ResizeArray<TwinNode>()
        let mutable cursor = target.Parent

        while not (isNull cursor) do
            path.Add cursor
            cursor <- cursor.Parent

        let failures = ResizeArray<exn>()

        for i in path.Count - 1 .. -1 .. 0 do
            if not event.PropagationStopped then
                invoke path[i] event 1 failures

        if not event.PropagationStopped then
            invoke target event 2 failures

        if event.Bubbles then
            for node in path do
                if not event.PropagationStopped then
                    invoke node event 3 failures

        event.Phase <- 0
        event.CurrentTarget <- null
        event.CurrentTargetOverride <- Unchecked.defaultof<_>

        target.Document.ListenerErrors.AddRange failures

        if
            failures.Count > 0
            && target.Document.RaiseListenerErrors
        then
            raise (TwinListenerException (event.Type, failures[0]))

        not event.DefaultPrevented

    /// <summary>Dispatches <c>event</c> at <c>target</c> through the capture, target and bubble phases; false when cancelled.</summary>
    let dispatch (target: TwinNode) (event: TwinEvent) : bool =
        outermost target.Document (fun () -> dispatchNow target event)

    let private fire (target: TwinNode) (eventType: string) (bubbles: bool) (cancelable: bool) =
        dispatch
            target
            (create
                {
                    Type = eventType
                    Kind = kindOf eventType
                    Bubbles = bubbles
                    Cancelable = cancelable
                    Init = []
                })

    let private isDisabled (e: TwinElement) =
        e.HasAttribute "disabled"
        && (e.LocalName = "button"
            || e.LocalName = "input"
            || e.LocalName = "select"
            || e.LocalName = "textarea")

    let private formOf (e: TwinElement) =
        let mutable cursor = e.Parent

        while not (isNull cursor)
              && not (
                  match cursor with
                  | :? TwinElement as f -> f.LocalName = "form"
                  | _ -> false
              ) do
            cursor <- cursor.Parent

        cursor

    let private labelled (label: TwinElement) : TwinElement =
        match label.GetAttribute "for" with
        | null ->
            let rec find (node: TwinNode) =
                node.Children
                |> List.tryPick (fun c ->
                    match c with
                    | :? TwinElement as e when List.contains e.LocalName [ "input"; "select"; "textarea"; "button" ] -> Some e
                    | _ -> find c)

            find label |> Option.toObj
        | id -> label.Document.ElementById id

    /// <summary>The <c>click ()</c> of HTML: activation behaviour around a bubbling, cancelable click.</summary>
    let rec private clickNow (element: TwinElement) : unit =
        if not (isDisabled element) then
            let kind =
                if element.LocalName = "input" then
                    match element.GetAttribute "type" with
                    | null -> "text"
                    | t -> t.ToLowerInvariant ()
                else
                    ""

            let toggles = kind = "checkbox" || kind = "radio"
            let before = element.Checked

            let group =
                if kind = "radio" then
                    let mutable root: TwinNode = element

                    while not (isNull root.Parent) do
                        root <- root.Parent

                    let rec radios (node: TwinNode) =
                        node.Children
                        |> List.collect (fun c ->
                            match c with
                            | :? TwinElement as e when
                                e.LocalName = "input"
                                && e.GetAttribute "type" = "radio"
                                && e.GetAttribute "name" = element.GetAttribute "name"
                                ->
                                e :: radios c
                            | _ -> radios c)

                    radios root |> List.map (fun r -> r, r.Checked)
                else
                    []

            if kind = "checkbox" then
                element.Checked <- not before

            if kind = "radio" then
                element.Checked <- true

            let accepted = fire element "click" true true

            if toggles then
                if not accepted then
                    if kind = "checkbox" then
                        element.Checked <- before

                    for radio, state in group do
                        radio.Checked <- state
                elif element.Checked <> before then
                    fire element "input" true false |> ignore
                    fire element "change" true false |> ignore
            elif accepted then
                let submits =
                    (element.LocalName = "button"
                     && (match element.GetAttribute "type" with
                         | null
                         | "submit" -> true
                         | _ -> false))
                    || (element.LocalName = "input" && kind = "submit")

                match formOf element with
                | :? TwinElement as form when submits -> fire form "submit" true true |> ignore
                | _ -> ()

                if element.LocalName = "label" then
                    match labelled element with
                    | null -> ()
                    | control -> clickNow control

    let private focusable (e: TwinElement) =
        not (isDisabled e)
        && (List.contains e.LocalName [ "input"; "select"; "textarea"; "button" ]
            || (e.LocalName = "a" && e.HasAttribute "href")
            || e.HasAttribute "tabindex")

    let private blurNow (element: TwinElement) : unit =
        let document = element.Document

        if obj.ReferenceEquals (document.ActiveElement, element) then
            document.ActiveElement <- null
            fire element "blur" false false |> ignore
            fire element "focusout" true false |> ignore

    let private focusNow (element: TwinElement) : unit =
        let document = element.Document

        if
            focusable element
            && not (obj.ReferenceEquals (document.ActiveElement, element))
        then
            match document.ActiveElement with
            | null -> ()
            | previous -> blurNow previous

            document.ActiveElement <- element
            fire element "focus" false false |> ignore
            fire element "focusin" true false |> ignore

    /// <summary>The <c>click ()</c> of HTML: activation behaviour around a bubbling, cancelable click.</summary>
    let click (element: TwinElement) : unit =
        outermost element.Document (fun () -> clickNow element)

    let focus (element: TwinElement) : unit =
        outermost element.Document (fun () -> focusNow element)

    let blur (element: TwinElement) : unit =
        outermost element.Document (fun () -> blurNow element)

    // Per node: the listeners added through addEventListener, by type and callback object, with their removal.
    let private added =
        ConditionalWeakTable<TwinNode, ResizeArray<string * obj * (unit -> unit)>>()

    let private registered =
        lazy
            (Bridge.ensure ()

             Bridge.registerMember "addEventListener" 3 (fun node args ->
                 let eventType, callback = string args[0], args[1]

                 let capture =
                     match args[2] with
                     | :? bool as b -> b
                     | _ -> false

                 let invoke =
                     match callback with
                     | :? (Fable.Core.TS.Dom.Event -> unit) as f -> fun (e: obj) -> f (e :?> Fable.Core.TS.Dom.Event)
                     | :? (obj -> unit) as f -> f
                     | other -> raise (NotSupportedException $"The DOM twin does not accept a {other.GetType().Name} listener")

                 let list = added.GetValue (node, fun _ -> ResizeArray ())

                 if not (list.Exists (fun (t, c, _) -> t = eventType && obj.ReferenceEquals (c, callback))) then
                     list.Add ((eventType, callback, listen node eventType capture invoke))

                 null)

             Bridge.registerMember "removeEventListener" 3 (fun node args ->
                 match added.TryGetValue node with
                 | true, list ->
                     match
                         list.FindIndex (fun (t, c, _) ->
                             t = string args[0]
                             && obj.ReferenceEquals (c, args[1]))
                     with
                     | -1 -> ()
                     | index ->
                         let _, _, remove = list[index]
                         remove ()
                         list.RemoveAt index
                 | false, _ -> ()

                 null)

             Bridge.registerMember "dispatchEvent" 1 (fun node args -> box (dispatch node (eventOf args[0])))

             Bridge.registerMember "click" 0 (fun node _ ->
                 click (node :?> TwinElement)
                 null)

             Bridge.registerMember "focus" 1 (fun node _ ->
                 focus (node :?> TwinElement)
                 null)

             Bridge.registerMember "focus" 0 (fun node _ ->
                 focus (node :?> TwinElement)
                 null)

             Bridge.registerMember "blur" 0 (fun node _ ->
                 blur (node :?> TwinElement)
                 null))

    /// <summary>Registers the event members once per process, after the core members.</summary>
    let ensure () =
        registered.Force ()
