module Ranvier.View.Foundation.TemplateDom

open System
open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.TS.Dom
open Ranvier
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template

[<Global "document">]
let private document: Document = jsNative

[<Literal>]
let FirstChild = 0

[<Literal>]
let NextSibling = 1

[<Literal>]
let Parent = 2

[<Literal>]
let Collect = 3

[<Literal>]
let StateCompiled = 0

[<Literal>]
let StateInstance = 1

[<Literal>]
let StateOwner = 2

[<Literal>]
let StateGraph = 3

[<Literal>]
let StateNodes = 4

/// <summary>A definition compiled for the DOM: a skeleton, the walk to its dynamic nodes, and its slot tables.</summary>
[<NoEquality; NoComparison>]
type Compiled =
    {
        Skeleton: DocumentFragment
        Walk: int array
        NodeCount: int
        RenderSlots: (int * Slot) array
        IsolatedSlots: (int * Slot) array
        Properties: (int * Func<obj, obj, unit> * obj) array
        Events: (int * string * Func<obj, obj, unit>) array
        Dispatch: Func<obj array, Node, obj, unit>
    }

/// <summary>Compiles child-index paths, in document order, into walk opcodes ending each path with <c>Collect</c>.</summary>
let walkProgram (paths: int array array) : int array =
    let ops = ResizeArray<int>()
    let mutable cursor: int array = [||]

    for path in paths do
        let mutable shared = 0

        while shared < cursor.Length
              && shared < path.Length
              && cursor[shared] = path[shared] do
            shared <- shared + 1

        if shared = cursor.Length then
            for level in shared .. path.Length - 1 do
                ops.Add FirstChild

                for _ in 1 .. path[level] do
                    ops.Add NextSibling
        else
            for _ in shared + 1 .. cursor.Length - 1 do
                ops.Add Parent

            for _ in 1 .. path[shared] - cursor[shared] do
                ops.Add NextSibling

            for level in shared + 1 .. path.Length - 1 do
                ops.Add FirstChild

                for _ in 1 .. path[level] do
                    ops.Add NextSibling

        ops.Add Collect
        cursor <- path

    ops.ToArray ()

let private dispatchFor (nodeCount: int) (events: (int * string * Func<obj, obj, unit>) array) =
    Func<obj array, Node, obj, unit>(fun state node event ->
        let mutable index = -1

        for i in 0 .. nodeCount - 1 do
            if obj.ReferenceEquals (state[StateNodes + i], node) then
                index <- i

        if index >= 0 then
            let owner = state[StateOwner] :?> Owner

            if not owner.IsDisposed then
                let graph = state[StateGraph] :?> Graph
                let eventType: string = event?``type``

                for target, eventName, handler in events do
                    if target = index && eventName = eventType then
                        graph.Run (fun () -> Api.runWithOwner owner (fun () -> Api.untrack (fun () -> handler.Invoke (state[StateInstance], event)))))

/// <summary>Compiles a definition once: parses its skeleton markup and builds its walk and slot tables.</summary>
let compile (definition: TemplateComponent) : Compiled =
    let markup, targets = skeletonMarkup definition.Root
    let template = document.createElement "template" :?> HTMLTemplateElement
    template.innerHTML <- markup
    let renderSlots = ResizeArray ()
    let isolatedSlots = ResizeArray ()
    let properties = ResizeArray ()
    let events = ResizeArray ()

    let add index (slot: Slot) =
        if slot.Mode = IsolatedMode then
            isolatedSlots.Add (index, slot)
        else
            renderSlots.Add (index, slot)

    targets
    |> Array.iteri (fun index (node, _) ->
        match node with
        | TextSlotNode slot -> add index slot
        | ElementNode (_, _, elementProperties, slots, elementEvents, _) ->
            for write, value in elementProperties do
                properties.Add (index, write, value)

            for slot in slots do
                add index slot

            for eventName, handler in elementEvents do
                events.Add (index, eventName, handler)
        | _ -> ())

    let eventArray = events.ToArray ()

    {
        Skeleton = template.content
        Walk = walkProgram (Array.map snd targets)
        NodeCount = targets.Length
        RenderSlots = renderSlots.ToArray ()
        IsolatedSlots = isolatedSlots.ToArray ()
        Properties = properties.ToArray ()
        Events = eventArray
        Dispatch = dispatchFor targets.Length eventArray
    }

/// <summary>The mount-owned operations a template instance uses.</summary>
[<NoEquality; NoComparison>]
type MountServices =
    {
        Append: Node -> unit
        Setup: Setup
        SettingUp: unit -> bool
        SetupFailed: exn -> unit
        RaiseSetupFailure: unit -> unit
        IsDelegated: string -> bool
        Delegate: string -> unit
        ListenDirect: Node -> string -> (obj -> unit) -> unit
    }

let private unset = obj ()

[<Emit("$0.data = $1 == null ? '' : $1")>]
let private setData (node: obj) (value: obj) : unit = jsNative

[<Emit("$1 == null ? $0.removeAttribute($2) : $0.setAttribute($2, $1)")>]
let private setAttribute (node: obj) (value: obj) (name: string) : unit = jsNative

/// <summary>Marks a node as an event node of a template instance.</summary>
[<Emit("$0.$$ranvier = $1")>]
let attachState (node: obj) (state: obj array) : unit = jsNative

let private write (slot: Slot) (node: obj) (value: obj) =
    match slot.Target with
    | AttributeTarget name -> setAttribute node value name
    | PropertyTarget writer -> writer.Invoke (node, value)
    | TextTarget -> setData node value

let private read (slot: Slot) (instance: obj) =
    try
        slot.Read.Invoke instance
    with _ ->
        unset

let private commit (services: MountServices) (state: obj array) (slot: Slot) (nodeIndex: int) (previousIndex: int) (value: obj) =
    if not (obj.ReferenceEquals (value, unset)) then
        let previous = state[previousIndex]

        if
            obj.ReferenceEquals (previous, unset)
            || not (slot.Same.Invoke (previous, value))
        then
            try
                write slot state[StateNodes + nodeIndex] value
                state[previousIndex] <- value
            with error when services.SettingUp () ->
                services.SetupFailed error
                reraise ()

let private collect (root: Node) (walk: int array) (state: obj array) =
    let mutable cursor = root
    let mutable count = 0

    for op in walk do
        if op = FirstChild then
            cursor <- unbox cursor.firstChild
        elif op = NextSibling then
            cursor <- unbox cursor.nextSibling
        elif op = Parent then
            cursor <- unbox cursor.parentNode
        else
            state[StateNodes + count] <- box cursor
            count <- count + 1

/// <summary>Materialises one instance: runs setup, clones the skeleton, binds its slots and events, and appends it.</summary>
let materialise (definition: TemplateDefinition) (props: obj) (services: MountServices) =
    let template = definition :?> TemplateComponent

    if isNull definition.Compiled then
        definition.Compiled <- box (compile template)

    let compiled = definition.Compiled :?> Compiled
    let instance = Api.untrack (fun () -> template.Setup.Invoke (services.Setup, props))
    let fragment = compiled.Skeleton.cloneNode true
    let renderCount = compiled.RenderSlots.Length
    let isolatedCount = compiled.IsolatedSlots.Length
    let previousBase = StateNodes + compiled.NodeCount
    let pendingBase = previousBase + renderCount + isolatedCount
    let state: obj array = Array.zeroCreate (pendingBase + renderCount)
    state[StateCompiled] <- box compiled
    state[StateInstance] <- instance
    state[StateOwner] <- box (Api.getOwner ())
    state[StateGraph] <- box Graph.Current

    for index in previousBase .. pendingBase - 1 do
        state[index] <- unset

    collect fragment compiled.Walk state

    for nodeIndex, writer, value in compiled.Properties do
        try
            writer.Invoke (state[StateNodes + nodeIndex], value)
        with error when services.SettingUp () ->
            services.SetupFailed error
            reraise ()

    if renderCount > 0 then
        let commitAll () =
            for k in 0 .. renderCount - 1 do
                let nodeIndex, slot = compiled.RenderSlots[k]
                commit services state slot nodeIndex (previousBase + k) state[pendingBase + k]

        Api.createEffect (fun () ->
            for k in 0 .. renderCount - 1 do
                let _, slot = compiled.RenderSlots[k]
                state[pendingBase + k] <- read slot instance

            Api.untrack commitAll)

        services.RaiseSetupFailure ()

    for k in 0 .. isolatedCount - 1 do
        let nodeIndex, slot = compiled.IsolatedSlots[k]
        let previousIndex = previousBase + renderCount + k

        Api.createEffect (fun () ->
            let value = read slot instance
            Api.untrack (fun () -> commit services state slot nodeIndex previousIndex value))

        services.RaiseSetupFailure ()

    for nodeIndex, eventName, handler in compiled.Events do
        let node = state[StateNodes + nodeIndex]

        if services.IsDelegated eventName then
            attachState node state
            services.Delegate eventName
        else
            let owner = state[StateOwner] :?> Owner
            let graph = state[StateGraph] :?> Graph

            services.ListenDirect (unbox node) eventName (fun event ->
                if not owner.IsDisposed then
                    graph.Run (fun () -> Api.runWithOwner owner (fun () -> Api.untrack (fun () -> handler.Invoke (instance, event)))))

    let rootCount = int fragment.childNodes.length
    let roots: Node array = Array.zeroCreate rootCount

    for index in 0 .. rootCount - 1 do
        roots[index] <- fragment.childNodes.item (float index)

    for root in roots do
        services.Append root
