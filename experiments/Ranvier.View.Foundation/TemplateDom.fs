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
