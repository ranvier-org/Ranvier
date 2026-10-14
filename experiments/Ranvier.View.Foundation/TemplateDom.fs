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

/// <summary>A region of a compiled template.</summary>
[<NoEquality; NoComparison>]
type CompiledRegion =
    {
        /// <summary>The node index of the region's start marker.</summary>
        Marker: int
        /// <summary>True when the markers are roots of a multi-root skeleton.</summary>
        RootLevel: bool
        Same: obj -> obj -> bool
        Items: Func<obj, (obj * obj) array>
        /// <summary>True when the items are read once, as the instance is created.</summary>
        Once: bool
        Render: (unit -> obj) -> Backend -> unit
    }

/// <summary>A definition compiled for the DOM: a skeleton, the walk to its dynamic nodes, and its slot tables.</summary>
/// <remarks>A single-root skeleton is the root node itself and its walk starts at the root; otherwise the skeleton is a fragment of <c>RootCount</c> roots.</remarks>
[<NoEquality; NoComparison>]
type Compiled =
    {
        Skeleton: Node
        RootCount: int
        Walk: int array
        NodeCount: int
        RenderSlots: (int * Slot) array
        IsolatedSlots: (int * Slot) array
        Properties: (int * Func<obj, obj, unit> * obj) array
        Events: (int * string * Func<obj, obj, unit>) array
        EventNames: string array
        Dispatch: Func<obj array, Node, obj, unit>
        Regions: CompiledRegion array
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

let private dispatchFor (nodeCount: int) (events: (int * string * Func<obj, obj, unit>) array) (eventNames: string array) =
    Func<obj array, Node, obj, unit>(fun state node event ->
        let eventType: string = event?``type``
        let mutable handled = false

        for eventName in eventNames do
            if eventName = eventType then
                handled <- true

        if handled then
            let mutable index = -1
            let mutable i = 0

            while index < 0 && i < nodeCount do
                if obj.ReferenceEquals (state[StateNodes + i], node) then
                    index <- i

                i <- i + 1

            if index >= 0 then
                let owner = state[StateOwner] :?> Owner

                if not owner.IsDisposed then
                    let graph = state[StateGraph] :?> Graph

                    for target, eventName, handler in events do
                        if target = index && eventName = eventType then
                            graph.Run (fun () ->
                                Api.runWithOwner owner (fun () -> Api.untrack (fun () -> handler.Invoke (state[StateInstance], event)))))

/// <summary>Dispatches a delegated event to the template instance whose state is attached to the node.</summary>
let dispatch =
    Func<obj array, Node, obj, unit>(fun state node event -> (state[StateCompiled] :?> Compiled).Dispatch.Invoke(state, node, event))

let private restructured (detail: string) =
    InvalidOperationException (
        "The HTML parser restructured the template markup: "
        + detail
    )

[<Emit("$0 != null && $0.nodeType === 8 && $0.data === '[' && $0.nextSibling != null && $0.nextSibling.nodeType === 8 && $0.nextSibling.data === ']'")>]
let private isRegionStart (node: Node) : bool = jsNative

[<Emit("document.importNode($0, true)")>]
let private importDeep (node: Node) : Node = jsNative

let private validateParsed (content: DocumentFragment) (walk: int array) (targets: (TemplateNode * int array) array) (rootCount: int) =
    let parsedRoots = int content.childNodes.length

    if parsedRoots <> rootCount then
        raise (restructured $"expected %d{rootCount} root nodes, parsed %d{parsedRoots}")

    let mutable cursor: Node = content :> Node
    let mutable count = 0

    for op in walk do
        if op = Collect then
            let target, path = targets[count]

            let expected, matches =
                match target with
                | ElementNode (tag, _, _, _, _, _) ->
                    let tagName = name tag

                    "<" + tagName + ">",
                    not (isNull (box cursor))
                    && cursor.nodeType = 1.
                    && cursor.nodeName.ToLowerInvariant () = tagName.ToLowerInvariant ()
                | TextSlotNode _ -> "a text node", not (isNull (box cursor)) && cursor.nodeType = 3.
                | RegionNode _ -> "a region marker pair", isRegionStart cursor
                | TextNode _
                | FragmentNode _ -> invalidOp "Static nodes are not dynamic targets"

            if not matches then
                let found =
                    if isNull (box cursor) then
                        "absent"
                    else
                        cursor.nodeName.ToLowerInvariant ()

                let at = path |> Array.map string |> String.concat "/"
                raise (restructured $"the dynamic node at path {at} should be {expected} but is {found}")

            count <- count + 1
        elif not (isNull (box cursor)) then
            if op = FirstChild then
                cursor <- unbox cursor.firstChild
            elif op = NextSibling then
                cursor <- unbox cursor.nextSibling
            else
                cursor <- unbox cursor.parentNode

let private readsOnce (row: RegionRow) =
    match row with
    | ViewRow -> true

/// <summary>Compiles a definition once: parses its skeleton markup and builds its walk, slot and region tables.</summary>
/// <remarks>The skeleton belongs to the main document, as do its clones.</remarks>
/// <exception cref="T:System.InvalidOperationException">The HTML parser restructures the markup, moving a dynamic node or changing the root count.</exception>
let compile (definition: TemplateComponent) : Compiled =
    let markup, targets, rootCount = skeletonMarkup definition.Root
    let template = document.createElement "template" :?> HTMLTemplateElement
    template.innerHTML <- markup
    let content = importDeep template.content :?> DocumentFragment
    let paths = Array.map snd targets
    let walk = walkProgram paths
    validateParsed content walk targets rootCount
    let renderSlots = ResizeArray ()
    let isolatedSlots = ResizeArray ()
    let properties = ResizeArray ()
    let events = ResizeArray ()
    let regions = ResizeArray ()

    let add index (slot: Slot) =
        if slot.Mode = IsolatedMode then
            isolatedSlots.Add (index, slot)
        else
            renderSlots.Add (index, slot)

    targets
    |> Array.iteri (fun index (node, path) ->
        match node with
        | TextSlotNode slot -> add index slot
        | ElementNode (_, _, elementProperties, slots, elementEvents, _) ->
            for write, value in elementProperties do
                properties.Add (index, write, value)

            for slot in slots do
                add index slot

            for eventName, handler in elementEvents do
                events.Add (index, eventName, handler)
        | RegionNode region ->
            let same = region.Same

            regions.Add
                {
                    CompiledRegion.Marker = index
                    RootLevel = rootCount > 1 && path.Length = 1
                    Same = fun left right -> same.Invoke (left, right)
                    Items = region.Items
                    Once = readsOnce region.Row
                    Render = rowRenderer region.Row
                }
        | TextNode _
        | FragmentNode _ -> invalidOp "Static nodes are not dynamic targets")

    let eventArray = events.ToArray ()

    let eventNames =
        eventArray
        |> Array.map (fun (_, eventName, _) -> eventName)
        |> Array.distinct

    {
        Skeleton =
            if rootCount = 1 then
                unbox content.firstChild
            else
                content :> Node
        RootCount = rootCount
        Walk =
            if rootCount = 1 then
                walkProgram (paths |> Array.map (fun path -> path[1..]))
            else
                walk
        NodeCount = targets.Length
        RenderSlots = renderSlots.ToArray ()
        IsolatedSlots = isolatedSlots.ToArray ()
        Properties = properties.ToArray ()
        Events = eventArray
        EventNames = eventNames
        Dispatch = dispatchFor targets.Length eventArray eventNames
        Regions = regions.ToArray ()
    }

/// <summary>The mount-owned operations a template instance uses.</summary>
type internal MountServices =
    abstract Append: Node -> unit
    abstract Setup: Setup
    abstract SettingUp: unit -> bool
    abstract SetupFailed: exn -> unit
    abstract RaiseSetupFailure: unit -> unit
    abstract IsDelegated: string -> bool
    abstract Delegate: string -> unit
    abstract ListenDirect: Node * string * (obj -> unit) -> unit
    abstract ReportFailure: exn -> unit
    abstract ClearFailure: unit -> unit
    /// <summary>Mounts keyed rows between a region's start and end markers; <c>true</c> marks markers that are roots of the appended clone.</summary>
    abstract Region: Node * Node * bool * (obj -> obj -> bool) * (unit -> (obj * obj) array) * ((unit -> obj) -> Backend -> unit) -> unit

let private unset = obj ()

/// <summary>An array of <c>length</c> unassigned elements.</summary>
[<Emit("new Array($0)")>]
let private allocate (length: int) : obj array = jsNative

/// <summary>Reads the element at an index; an index past the end reads <c>undefined</c>.</summary>
[<Emit("$0[$1]")>]
let private at (array: 'T array) (index: int) : 'T = jsNative

/// <summary>Writes the element at an index; an index past the end grows the array.</summary>
[<Emit("$0[$1] = $2")>]
let private store (array: obj array) (index: int) (value: obj) : unit = jsNative

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

// A failed write keeps the slot's previous value. During setup the failure aborts the mount; afterwards it is reported.
let private commit (services: MountServices) (state: obj array) (slot: Slot) (nodeIndex: int) (previousIndex: int) (value: obj) =
    let previous = at state previousIndex

    if obj.ReferenceEquals (value, unset) then
        match slot.Target with
        | TextTarget when obj.ReferenceEquals (previous, unset) ->
            setData (at state (StateNodes + nodeIndex)) ""
            store state previousIndex (box "")
        | _ -> ()
    elif
        obj.ReferenceEquals (previous, unset)
        || not (slot.Same.Invoke (previous, value))
    then
        try
            write slot (at state (StateNodes + nodeIndex)) value
            store state previousIndex value
        with error ->
            if services.SettingUp () then
                services.SetupFailed error
                reraise ()
            else
                services.ReportFailure error

let private collect (root: Node) (walk: int array) (state: obj array) =
    let mutable cursor = root
    let mutable count = 0

    for i in 0 .. walk.Length - 1 do
        let op = at walk i

        if op = FirstChild then
            cursor <- unbox cursor.firstChild
        elif op = NextSibling then
            cursor <- unbox cursor.nextSibling
        elif op = Parent then
            cursor <- unbox cursor.parentNode
        else
            store state (StateNodes + count) (box cursor)
            count <- count + 1

/// <summary>Materialises one instance: runs setup, clones the skeleton, binds its slots and events, appends it and mounts its regions.</summary>
let internal materialise (definition: TemplateDefinition) (props: obj) (services: MountServices) =
    let template = definition :?> TemplateComponent

    if isNull definition.Compiled then
        definition.Compiled <- box (compile template)

    let compiled = definition.Compiled :?> Compiled
    let instance = Api.untrack (fun () -> template.Setup.Invoke (services.Setup, props))
    let clone = compiled.Skeleton.cloneNode true
    let renderCount = compiled.RenderSlots.Length
    let isolatedCount = compiled.IsolatedSlots.Length
    let previousBase = StateNodes + compiled.NodeCount
    let pendingBase = previousBase + renderCount + isolatedCount
    let state = allocate (pendingBase + renderCount)
    store state StateCompiled (box compiled)
    store state StateInstance instance
    store state StateOwner (box (Api.getOwner ()))
    store state StateGraph (box Graph.Current)

    for index in previousBase .. pendingBase - 1 do
        store state index unset

    collect clone compiled.Walk state

    let properties = compiled.Properties

    for i in 0 .. properties.Length - 1 do
        let nodeIndex, writer, value = at properties i

        try
            writer.Invoke (at state (StateNodes + nodeIndex), value)
        with error when services.SettingUp () ->
            services.SetupFailed error
            reraise ()

    if renderCount > 0 then
        let commitAll () =
            for k in 0 .. renderCount - 1 do
                let nodeIndex, slot = at compiled.RenderSlots k
                commit services state slot nodeIndex (previousBase + k) (at state (pendingBase + k))

        let mutable failed = false

        Api.createEffect (fun () ->
            let mutable failure: exn = null

            for k in 0 .. renderCount - 1 do
                let _, slot = at compiled.RenderSlots k

                let value =
                    try
                        slot.Read.Invoke instance
                    with
                    | NotReadyException _ -> unset
                    | error ->
                        failed <- true
                        services.ReportFailure error

                        if isNull failure then
                            failure <- error

                        unset

                store state (pendingBase + k) value

            Api.untrack commitAll

            if not (isNull failure) then
                failed <- true
                raise failure
            elif failed then
                failed <- false
                services.ClearFailure ())

        services.RaiseSetupFailure ()

    for k in 0 .. isolatedCount - 1 do
        let nodeIndex, slot = at compiled.IsolatedSlots k
        let previousIndex = previousBase + renderCount + k

        let mutable failed = false

        Api.createEffect (fun () ->
            let mutable failure: exn = null

            let value =
                try
                    slot.Read.Invoke instance
                with
                | NotReadyException _ -> unset
                | error ->
                    failed <- true
                    services.ReportFailure error
                    failure <- error
                    unset

            Api.untrack (fun () -> commit services state slot nodeIndex previousIndex value)

            if not (isNull failure) then
                failed <- true
                raise failure
            elif failed then
                failed <- false
                services.ClearFailure ())

        services.RaiseSetupFailure ()

    let mutable attached = -1

    let events = compiled.Events

    for i in 0 .. events.Length - 1 do
        let nodeIndex, eventName, handler = at events i
        let node = at state (StateNodes + nodeIndex)

        if services.IsDelegated eventName then
            if attached <> nodeIndex then
                attachState node state
                attached <- nodeIndex

            services.Delegate eventName
        else
            let owner = at state StateOwner :?> Owner
            let graph = at state StateGraph :?> Graph

            services.ListenDirect (
                unbox node,
                eventName,
                fun event ->
                    if not owner.IsDisposed then
                        graph.Run (fun () -> Api.runWithOwner owner (fun () -> Api.untrack (fun () -> handler.Invoke (instance, event))))
            )

    if compiled.RootCount = 1 then
        services.Append clone
    else
        let mutable root: Node = unbox clone.firstChild

        while not (isNull (box root)) do
            let next: Node = unbox root.nextSibling
            services.Append root
            root <- next

    let regions = compiled.Regions

    for i in 0 .. regions.Length - 1 do
        let region = at regions i
        let start: Node = unbox (at state (StateNodes + region.Marker))
        let finish: Node = unbox start.nextSibling
        let items = region.Items

        let read =
            if region.Once then
                let snapshot = items.Invoke instance
                fun () -> snapshot
            else
                fun () -> items.Invoke instance

        services.Region (start, finish, region.RootLevel, region.Same, read, region.Render)
