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

/// <summary>How a region mounts its rows.</summary>
type RegionKind =
    /// <summary>One row per keyed item; new rows are created before removed rows retire.</summary>
    | ListRows
    /// <summary>The row of the selected arm; the previous arm is disposed before the next is created.</summary>
    | SelectedArm
    /// <summary>One view, read and mounted once.</summary>
    | ViewHole
    /// <summary>A boundary: the region's single item's value is its <c>BoundaryViews</c>, read once.</summary>
    | BoundaryContent

[<NoEquality; NoComparison>]
type CompiledRegion =
    {
        /// <summary>The node index of the region's start marker.</summary>
        Marker: int
        /// <summary>The part whose instance the region reads: 0 for the template, k for the k-th inlined child.</summary>
        Owner: int
        /// <summary>The region's position among its owning part's regions.</summary>
        Index: int
        /// <summary>True when the markers are roots of a multi-root skeleton.</summary>
        RootLevel: bool
        Kind: RegionKind
        Items: Func<obj, obj array>
        Key: Func<obj, obj>
        Render: (unit -> obj) -> Backend -> unit
        Row: RegionRow
    }

/// <summary>A definition compiled for the DOM: a skeleton, the walk to its dynamic nodes, and its slot tables.</summary>
/// <remarks>A single-root skeleton is the root node itself and its walk starts at the root; otherwise the skeleton is a fragment of <c>RootCount</c> roots. The root compiled form lists the regions and inlined children of every depth.</remarks>
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
        /// <summary>The placed hooks of the part's nodes: node index, kind and hook.</summary>
        Placed: (int * PlacedKind * Func<obj, obj, (unit -> unit)>) array
        /// <summary>The document head entries of the part.</summary>
        Heads: HeadTemplate array
        EventNames: string array
        Dispatch: Func<obj array, Node, obj, unit>
        Regions: CompiledRegion array
        Children: CompiledChild array
    }

and [<NoEquality; NoComparison>] CompiledChild =
    {
        Definition: TemplateComponent
        Props: Func<obj, obj>
        /// <summary>The part whose instance the props reader receives.</summary>
        Owner: int
        /// <summary>The template's node index of the child's first dynamic node.</summary>
        Offset: int
        /// <summary>The child's slot and event tables, indexed from <c>Offset</c>; its skeleton is null and its walk empty.</summary>
        Tables: Compiled
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

/// <summary>Marks an event node of a template instance under a boundary with the boundary.</summary>
[<Emit("$0.$$ranvierScope = $1")>]
let private attachScope (node: obj) (scope: ViewScope) : unit = jsNative

/// <summary>The boundary of an event node, or null outside every boundary.</summary>
[<Emit("$0.$$ranvierScope")>]
let private scopeOf (node: Node) : ViewScope = jsNative

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
                let graph = state[StateGraph] :?> Graph

                for target, eventName, handler in events do
                    if
                        target = index
                        && eventName = eventType
                        && not owner.IsDisposed
                    then
                        try
                            graph.Run (fun () ->
                                Api.runWithOwner owner (fun () -> Api.untrack (fun () -> handler.Invoke (state[StateInstance], event))))
                        with error ->
                            let scope = scopeOf node

                            if isNull (box scope) || not scope.Tracks then
                                reraise ()

                            scope.FailAll [ error ])

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
                | ElementNode (tag, _, _, _, _, _, _) ->
                    let tagName = name tag

                    "<" + tagName + ">",
                    not (isNull (box cursor))
                    && cursor.nodeType = 1.
                    && cursor.nodeName.ToLowerInvariant () = tagName.ToLowerInvariant ()
                | TextSlotNode _ -> "a text node", not (isNull (box cursor)) && cursor.nodeType = 3.
                | RegionNode _ -> "a region marker pair", isRegionStart cursor
                | TextNode _
                | FragmentNode _
                | ChildNode _
                | HeadNode _ -> invalidOp "Static nodes are not dynamic targets"

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

let private tables (targets: (TemplateNode * int array) array) (owners: int array) (part: int) (first: int) (count: int) =
    let renderSlots = ResizeArray<int * Slot>()
    let isolatedSlots = ResizeArray<int * Slot>()
    let properties = ResizeArray<int * Func<obj, obj, unit> * obj>()
    let events = ResizeArray<int * string * Func<obj, obj, unit>>()
    let placed = ResizeArray<int * PlacedKind * Func<obj, obj, (unit -> unit)>>()

    let add index (slot: Slot) =
        if slot.Mode = IsolatedMode then
            isolatedSlots.Add (index, slot)
        else
            renderSlots.Add (index, slot)

    for absolute in first .. first + count - 1 do
        if owners[absolute] = part then
            let index = absolute - first

            match fst (targets[absolute]) with
            | TextSlotNode slot -> add index slot
            | ElementNode (_, _, elementProperties, slots, elementEvents, elementPlaced, _) ->
                for write, value in elementProperties do
                    properties.Add (index, write, value)

                for slot in slots do
                    add index slot

                for eventName, handler in elementEvents do
                    events.Add (index, eventName, handler)

                for kind, hook in elementPlaced do
                    placed.Add (index, kind, hook)
            | RegionNode _ -> ()
            | TextNode _
            | FragmentNode _
            | ChildNode _
            | HeadNode _ -> invalidOp "Static nodes are not dynamic targets"

    renderSlots.ToArray (), isolatedSlots.ToArray (), properties.ToArray (), events.ToArray (), placed.ToArray ()

let private compilePart
    (layout: SkeletonLayout)
    (part: int)
    (first: int)
    (count: int)
    (skeleton: Node)
    (rootCount: int)
    (walk: int array)
    (regions: CompiledRegion array)
    (children: CompiledChild array)
    : Compiled =
    let renderSlots, isolatedSlots, properties, events, placed =
        tables layout.Targets layout.Owners part first count

    let eventNames =
        events
        |> Array.map (fun (_, eventName, _) -> eventName)
        |> Array.distinct

    {
        Skeleton = skeleton
        RootCount = rootCount
        Walk = walk
        NodeCount = count
        RenderSlots = renderSlots
        IsolatedSlots = isolatedSlots
        Properties = properties
        Events = events
        Placed = placed
        Heads =
            layout.Heads
            |> Array.filter (fun (_, owner) -> owner = part)
            |> Array.map fst
        EventNames = eventNames
        Dispatch = dispatchFor count events eventNames
        Regions = regions
        Children = children
    }

/// <summary>Compiles a definition once: parses its skeleton markup and builds its walk, its slot tables, its regions and its inlined children's slot tables.</summary>
/// <remarks>A skeleton with regions belongs to the main document, as do its clones; a skeleton without regions belongs to the template element's inert document.</remarks>
/// <exception cref="T:System.InvalidOperationException">The HTML parser restructures the markup, moving a dynamic node or changing the root count.</exception>
let compile (definition: TemplateComponent) : Compiled =
    let layout = skeletonLayout definition.Root
    let targets = layout.Targets
    let rootCount = layout.RootCount

    // The number of regions compiled so far, per part.
    let partRegions = Array.zeroCreate<int>(layout.Children.Length + 1)

    let regions =
        [|
            for index in 0 .. targets.Length - 1 do
                match targets[index] with
                | RegionNode region, path ->
                    let owner = layout.Owners[index]
                    let position = partRegions[owner]
                    partRegions[owner] <- position + 1

                    {
                        CompiledRegion.Marker = index
                        Owner = owner
                        Index = position
                        RootLevel = rootCount > 1 && path.Length = 1
                        Items = region.Items
                        Key = region.Key
                        Kind =
                            match region.Row with
                            | ItemRow _ -> ListRows
                            | ArmRow -> SelectedArm
                            | ViewRow -> ViewHole
                            | BoundaryRow -> BoundaryContent
                        Render = rowRenderer region.Row
                        Row = region.Row
                    }
                | _ -> ()
        |]

    let template = document.createElement "template" :?> HTMLTemplateElement
    template.innerHTML <- layout.Markup

    let content =
        if regions.Length = 0 then
            template.content
        else
            importDeep template.content :?> DocumentFragment

    let paths = Array.map snd targets
    let walk = walkProgram paths
    validateParsed content walk targets rootCount

    let children =
        layout.Children
        |> Array.mapi (fun index child ->
            {
                CompiledChild.Definition = child.Definition
                Props = child.Props
                Owner = child.Owner
                Offset = child.First
                Tables = compilePart layout (index + 1) child.First child.Count Unchecked.defaultof<Node> 0 [||] [||] [||]
            })

    compilePart
        layout
        0
        0
        targets.Length
        (if rootCount = 1 then
             unbox content.firstChild
         else
             content :> Node)
        rootCount
        (if rootCount = 1 then
             walkProgram (paths |> Array.map (fun path -> path[1..]))
         else
             walk)
        regions
        children

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
    /// <summary>The receiver of the pending reads and failures of the instance's bindings.</summary>
    abstract Scope: ViewScope
    /// <summary>The container that holds the instance's hooks until its nodes are placed.</summary>
    abstract Placement: Placement
    /// <summary>Binds a document head entry for the instance being materialised.</summary>
    abstract Head: HeadEntry -> unit
    /// <summary>The ids of the mount; an instance's setup runs in its window.</summary>
    abstract Identity: IdentityScope

    /// <summary>
    /// Mounts a region between its start and end markers. The flag marks markers that are roots of the appended clone;
    /// the materialiser, null for a view hole or a boundary, receives a row's current item, its services and its
    /// creation index. The region's rows sit under the owning part's path at the region's index.
    /// </summary>
    abstract Region:
        Node *
        Node *
        bool *
        RegionKind *
        KeyedItems *
        ((unit -> obj) -> Backend -> unit) *
        Func<(unit -> obj), MountServices, int, unit> *
        InstancePath *
        int ->
            unit

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
    | ContentTarget -> (unbox<HTMLElement> node).innerHTML <- markupOf value

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

/// <summary>A state array for an instance of <c>compiled</c>, with <c>extra</c> trailing elements and every previous value unset.</summary>
let private stateFor (compiled: Compiled) (instance: obj) (extra: int) =
    let renderCount = compiled.RenderSlots.Length
    let previousBase = StateNodes + compiled.NodeCount

    let pendingBase =
        previousBase
        + renderCount
        + compiled.IsolatedSlots.Length

    let state = allocate (pendingBase + renderCount + extra)
    store state StateCompiled (box compiled)
    store state StateInstance instance
    store state StateOwner (box (Api.getOwner ()))
    store state StateGraph (box Graph.Current)

    for index in previousBase .. pendingBase - 1 do
        store state index unset

    state

/// <summary>Starts the effect that reads every render slot of a part and commits them together.</summary>
let private startRender (services: MountServices) (state: obj array) =
    let scope = services.Scope
    let mutable flags = BindingState.started scope

    if scope.Tracks then
        Api.onCleanup (fun () ->
            let previous = flags
            flags <- BindingState.released previous
            BindingState.ready scope previous)

    Api.createEffect (fun () ->
        let compiled = at state StateCompiled :?> Compiled
        let instance = at state StateInstance
        let renderCount = compiled.RenderSlots.Length
        let previousBase = StateNodes + compiled.NodeCount

        let pendingBase =
            previousBase
            + renderCount
            + compiled.IsolatedSlots.Length

        let mutable failure: exn = null
        let mutable waiting = false

        for k in 0 .. renderCount - 1 do
            let _, slot = at compiled.RenderSlots k

            let value =
                try
                    slot.Read.Invoke instance
                with error ->
                    flags <- BindingState.rejected scope flags error
                    BindingState.report scope error

                    match error with
                    | NotReadyException _ -> waiting <- true
                    | _ when isNull failure -> failure <- error
                    | _ -> ()

                    unset

            store state (pendingBase + k) value

        Api.untrack (fun () ->
            for k in 0 .. renderCount - 1 do
                let nodeIndex, slot = at compiled.RenderSlots k
                commit services state slot nodeIndex (previousBase + k) (at state (pendingBase + k)))

        if flags <> 0 then
            if isNull failure then
                flags <- BindingState.recovered services.ClearFailure flags

            if not waiting then
                let previous = flags
                flags <- BindingState.released previous
                BindingState.ready scope previous

        if not (isNull failure) then
            raise failure)

    services.RaiseSetupFailure ()

/// <summary>Starts the effect of the isolated slot at index <c>k</c> of a part.</summary>
let private startIsolated (services: MountServices) (state: obj array) (k: int) =
    let scope = services.Scope
    let mutable flags = BindingState.started scope

    if scope.Tracks then
        Api.onCleanup (fun () ->
            let previous = flags
            flags <- BindingState.released previous
            BindingState.ready scope previous)

    Api.createEffect (fun () ->
        let compiled = at state StateCompiled :?> Compiled
        let nodeIndex, slot = at compiled.IsolatedSlots k

        let previousIndex =
            StateNodes
            + compiled.NodeCount
            + compiled.RenderSlots.Length
            + k

        let mutable failure: exn = null
        let mutable waiting = false

        let value =
            try
                slot.Read.Invoke (at state StateInstance)
            with error ->
                flags <- BindingState.rejected scope flags error
                BindingState.report scope error

                match error with
                | NotReadyException _ -> waiting <- true
                | _ -> failure <- error

                unset

        Api.untrack (fun () -> commit services state slot nodeIndex previousIndex value)

        if flags <> 0 then
            if isNull failure then
                flags <- BindingState.recovered services.ClearFailure flags

            if not waiting then
                let previous = flags
                flags <- BindingState.released previous
                BindingState.ready scope previous

        if not (isNull failure) then
            raise failure)

    services.RaiseSetupFailure ()

/// <summary>Attaches a direct listener for an event of a part's node.</summary>
let private listenDirect (services: MountServices) (state: obj array) (node: obj) (eventName: string) (handler: Func<obj, obj, unit>) =
    let owner = at state StateOwner :?> Owner
    let graph = at state StateGraph :?> Graph
    let instance = at state StateInstance
    let scope = services.Scope

    services.ListenDirect (
        unbox node,
        eventName,
        fun event ->
            if not owner.IsDisposed then
                try
                    graph.Run (fun () -> Api.runWithOwner owner (fun () -> Api.untrack (fun () -> handler.Invoke (instance, event))))
                with error ->
                    if not scope.Tracks then
                        reraise ()

                    scope.FailAll [ error ]
    )

/// <summary>Writes a part's static properties, starts its render and isolated effects, attaches its events and registers its placed hooks.</summary>
/// <remarks>The effects and listeners retain the state array and the services.</remarks>
let private bind (services: MountServices) (compiled: Compiled) (state: obj array) =
    let properties = compiled.Properties

    for i in 0 .. properties.Length - 1 do
        let nodeIndex, writer, value = at properties i

        try
            writer.Invoke (at state (StateNodes + nodeIndex), value)
        with error when services.SettingUp () ->
            services.SetupFailed error
            reraise ()

    if compiled.RenderSlots.Length > 0 then
        startRender services state

    for k in 0 .. compiled.IsolatedSlots.Length - 1 do
        startIsolated services state k

    let mutable attached = -1

    let events = compiled.Events

    let scope =
        if events.Length > 0 then
            services.Scope
        else
            Unchecked.defaultof<_>

    for i in 0 .. events.Length - 1 do
        let nodeIndex, eventName, handler = at events i
        let node = at state (StateNodes + nodeIndex)

        if services.IsDelegated eventName then
            if attached <> nodeIndex then
                attachState node state

                if scope.Tracks then
                    attachScope node scope

                attached <- nodeIndex

            services.Delegate eventName
        else
            listenDirect services state node eventName handler

    let placed = compiled.Placed

    if placed.Length > 0 then
        let owner = at state StateOwner :?> Owner
        let instance = at state StateInstance

        for i in 0 .. placed.Length - 1 do
            let nodeIndex, kind, hook = at placed i
            let node: Node = unbox (at state (StateNodes + nodeIndex))
            PlacedHook.register services.Placement services.Scope owner kind node (fun () -> hook.Invoke (instance, box node))

/// <summary>The instance of a part: the template's own for 0, an inlined child's from its state array otherwise.</summary>
let private partInstance (state: obj array) (childBase: int) (part: int) : obj =
    if part = 0 then
        at state StateInstance
    else
        at (unbox<obj array>(at state (childBase + part - 1))) StateInstance

/// <summary>Runs a template's setup on <c>props</c>, untracked, inside the instance's open identity window.</summary>
let private setUp (template: TemplateComponent) (services: MountServices) (props: obj) =
    let setup = ownSetup services.Identity services.Setup
    Api.untrack (fun () -> template.Setup.Invoke (setup, props))

/// <summary>
/// Runs an inlined child's setup on the props read from <c>owner</c>, the instance of its owning part, untracked,
/// inside the child's open identity window.
/// </summary>
let private setUpChild (services: MountServices) (child: CompiledChild) (owner: obj) =
    let setup = ownSetup services.Identity services.Setup
    Api.untrack (fun () -> child.Definition.Setup.Invoke (setup, child.Props.Invoke owner))

/// <summary>
/// Materialises one instance at the position one step of <c>kind</c> below <c>parent</c>: runs setup, clones the
/// skeleton, binds its slots, events and placed hooks and those of its inlined children, appends it and mounts its
/// regions.
/// </summary>
let rec internal materialise
    (definition: TemplateDefinition)
    (props: obj)
    (services: MountServices)
    (parent: InstancePath)
    (kind: int)
    (a: int)
    (b: int)
    =
    let template = definition :?> TemplateComponent

    if isNull definition.Compiled then
        definition.Compiled <- box (compile template)

    let compiled = definition.Compiled :?> Compiled
    let children = compiled.Children
    let identity = services.Identity
    // The instance's path, taken only when a region or an inlined child sits under it.
    let mutable path: InstancePath = null
    identity.Enter (parent, kind, a, b)

    let instance =
        try
            let instance = setUp template services props

            if compiled.Regions.Length > 0 || children.Length > 0 then
                path <- identity.Path ()

            instance
        finally
            identity.Leave ()

    let clone = compiled.Skeleton.cloneNode true
    let state = stateFor compiled instance children.Length
    collect clone compiled.Walk state
    bind services compiled state

    for i in 0 .. compiled.Heads.Length - 1 do
        services.Head (headEntry (at compiled.Heads i) instance)

    let childBase =
        StateNodes
        + compiled.NodeCount
        + 2 * compiled.RenderSlots.Length
        + compiled.IsolatedSlots.Length

    for k in 0 .. children.Length - 1 do
        let child = at children k

        identity.Enter (path, InstanceStep.Child, k, 0)

        let childInstance =
            try
                setUpChild services child (partInstance state childBase child.Owner)
            finally
                identity.Leave ()

        let tables = child.Tables
        let childState = stateFor tables childInstance 0

        for i in 0 .. tables.NodeCount - 1 do
            store childState (StateNodes + i) (at state (StateNodes + child.Offset + i))

        store state (childBase + k) (box childState)
        bind services tables childState

        for h in 0 .. tables.Heads.Length - 1 do
            services.Head (headEntry (at tables.Heads h) childInstance)

    if compiled.RootCount = 1 then
        services.Append clone
    else
        let mutable root: Node = unbox clone.firstChild

        while not (isNull (box root)) do
            let next: Node = unbox root.nextSibling
            services.Append root
            root <- next

    let regions = compiled.Regions
    // The path of the part owning the latest region, kept for the regions of the same part that follow it.
    let mutable pathOwner = -1
    let mutable partPath: InstancePath = null

    for i in 0 .. regions.Length - 1 do
        let region = at regions i
        let owner = partInstance state childBase region.Owner

        if region.Owner <> pathOwner then
            pathOwner <- region.Owner

            partPath <-
                if region.Owner = 0 then
                    path
                else
                    path.Child (region.Owner - 1)

        let regionPath = partPath
        let regionIndex = region.Index
        let start: Node = unbox (at state (StateNodes + region.Marker))
        let finish: Node = unbox start.nextSibling
        let items = region.Items

        let read =
            match region.Kind with
            | ViewHole
            | BoundaryContent ->
                let snapshot = items.Invoke owner
                fun () -> snapshot
            | ListRows
            | SelectedArm -> fun () -> items.Invoke owner

        let rows =
            match region.Row with
            | ItemRow (rowDefinition, rowProps) ->
                Func<(unit -> obj), MountServices, int, unit>(fun current rowServices row ->
                    materialise rowDefinition (rowProps.Invoke current) rowServices regionPath InstanceStep.Row regionIndex row)
            | ArmRow ->
                Func<(unit -> obj), MountServices, int, unit>(fun current rowServices row ->
                    let arm = unbox<Arm>(current ())
                    materialise arm.Definition (arm.Reader.Invoke current) rowServices regionPath InstanceStep.Row regionIndex row)
            | ViewRow
            | BoundaryRow -> null

        services.Region (
            start,
            finish,
            region.RootLevel,
            region.Kind,
            { Read = read; Key = region.Key },
            region.Render,
            rows,
            regionPath,
            regionIndex
        )
