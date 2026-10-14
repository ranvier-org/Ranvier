module Ranvier.View.Foundation.Mounting

open System
open Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Fable.Core.TS
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Scheduling

type NativeOwnership =
    | Owned
    | Borrowed

type BuildContext =
    {
        Backend: Backend
        Native: Node * NativeOwnership -> unit
        OnCleanup: (unit -> unit) -> unit
        Phase: unit -> ScopePhase
    }

[<Global>]
let private globalThis: GlobalThis = jsNative

[<Global "document">]
let private document: Document = jsNative

let private fragment () =
    document.createDocumentFragment ()

let private comment (value: string) =
    document.createComment (value)

let private text (value: string) =
    document.createTextNode (value)

let private propertyEquals = Func<obj, obj, bool>(fun left right -> left = right)

let private attributeEquals =
    Func<string option, string option, bool>(fun left right -> left = right)

let inline private insert (parent: #Node) (node: #Node) (before: #Node option) : unit =
    let before: Node option = before |> Option.map (fun x -> x :> Node)

    parent.insertBefore (node, ?child = before)
    |> ignore

let inline private nextSiblingNode (node: #Node) : Node option =
    node.nextSibling
    |> Option.map (fun x -> x :> Node)

let private remove (node: ChildNode) : unit =
    node.remove ()

let private contains (parent: Node) (node: Node) : bool =
    parent.contains node

let private sameDocument (a: Node) (b: Node) : bool =
    match a.ownerDocument, b.ownerDocument with
    | None, None -> true
    | Some left, Some right -> obj.ReferenceEquals (left, right)
    | _ -> false

let private inputValue (node: HTMLInputElement) (value: string) : unit =
    node.value <- value

let private attributeValue (node: Element) name =
    function
    | Some value -> node.setAttribute (name, value)
    | None -> node.removeAttribute name

let private isDocumentFragment (node: Node) : bool =
    node.nodeType = 11.

let private invalid message =
    raise (InvalidOperationException message)

let private same a b =
    obj.ReferenceEquals (a, b)

let rec private walkPrepared visit (node: Node) =
    visit node

    let children =
        if
            node.nodeType = 1.
            && (node :?> Element).tagName = "TEMPLATE"
        then
            (node :?> HTMLTemplateElement).content :> Node
        else
            node

    let mutable child = children.firstChild

    while child.IsSome do
        let current = child.Value
        child <- current.nextSibling
        walkPrepared visit current

let private createClaims () : WeakSet<Node> =
    globalThis.WeakSet.Create ()

let private claims = createClaims ()

[<Emit("new Set(['beforeinput', 'click', 'contextmenu', 'dblclick', 'focusin', 'focusout', 'input', 'keydown', 'keyup', 'mousedown', 'mousemove', 'mouseout', 'mouseover', 'mouseup', 'pointerdown', 'pointermove', 'pointerout', 'pointerover', 'pointerup', 'touchend', 'touchmove', 'touchstart'])")>]
let private createDelegatedEvents () : obj = jsNative

/// <summary>Bubbling events dispatched from one listener per mount host.</summary>
let private delegatedEvents = createDelegatedEvents ()

[<Emit("$0.has($1)")>]
let private isDelegated (events: obj) (eventName: string) : bool = jsNative

[<Emit("$0['$$ranvier:' + $1] = $2")>]
let private setDelegatedHandler (node: Node) (eventName: string) (handler: obj -> unit) : unit = jsNative

[<Emit("new Map()")>]
let private createSharedListeners () : obj = jsNative

/// <summary>Per event name: the listener shared by every host.</summary>
let private sharedListeners = createSharedListeners ()

[<Emit("new WeakMap()")>]
let private createHostCounts () : obj = jsNative

/// <summary>Per host: the number of mounts using each shared listener.</summary>
let private hostCounts = createHostCounts ()

// The listener runs with its host as `this` and walks from the target to that host, presenting each handled node as currentTarget. A node with a handler runs it; for a template event node, `TemplateDom.dispatch` receives its instance state. An inner host records itself on the event, so an outer host resumes above it.
let private delegateFrom: Func<Node, string, unit> =
    JsInterop.emitJsExpr
        (sharedListeners, TemplateDom.dispatch, hostCounts)
        """(host, eventName) => { let listener = $0.get(eventName); if (!listener) { const key = '$$ranvier:' + eventName; listener = function (event) { const host = this; let node = event.$$ranvierHost ? event.$$ranvierHost.parentNode : event.target; Object.defineProperty(event, 'currentTarget', { configurable: true, get: () => node }); while (node) { const handler = node[key]; if (handler) { handler(event); if (event.cancelBubble) break; } else if (node.$$ranvier !== undefined) { $1(node.$$ranvier, node, event); if (event.cancelBubble) break; } if (node === host) break; node = node.parentNode; } event.$$ranvierHost = host; }; $0.set(eventName, listener); } let counts = $2.get(host); if (!counts) $2.set(host, counts = new Map()); const count = counts.get(eventName) | 0; if (count === 0) host.addEventListener(eventName, listener); counts.set(eventName, count + 1); }"""

let private undelegateFrom: Func<Node, string, unit> =
    JsInterop.emitJsExpr
        (sharedListeners, hostCounts)
        """(host, eventName) => { const counts = $1.get(host); const count = counts ? counts.get(eventName) | 0 : 0; if (count === 1) host.removeEventListener(eventName, $0.get(eventName)); if (count > 0) counts.set(eventName, count - 1); }"""

let private claim (registry: WeakSet<Node>) (node: Node) : unit =
    registry.add node |> ignore

let private unclaim (registry: WeakSet<Node>) (node: Node) : unit =
    registry.delete node |> ignore

[<Emit("(() => { for (let p = $1; p; p = p.parentNode) { if ($0.has(p)) return true; } if (!$1.firstChild) return false; const pending = Array.from($1.childNodes); while (pending.length) { const n = pending.pop(); if ($0.has(n)) return true; for (let c = n.firstChild; c; c = c.nextSibling) pending.push(c); } return false; })()")>]
let private overlaps (registry: WeakSet<Node>) (node: Node) : bool = jsNative

[<Emit("$0.parentNode === $1")>]
let private isChildOf (node: Node) (parent: Node) : bool = jsNative

[<Emit("$0.parentNode == null")>]
let private isDetached (node: Node) : bool = jsNative

/// <summary>The node's next sibling, or <c>null</c> after the last child.</summary>
[<Emit("$0.nextSibling")>]
let private nextOrNull (node: Node) : Node = jsNative

/// <summary>Reads an element without a bounds check.</summary>
[<Emit("$0[$1]")>]
let private at (items: ResizeArray<'T>) (index: int) : 'T = jsNative

let private skip: unit -> unit = ignore

type private Entry =
    {
        Node: Node
        EndNode: Node
        Validate: unit -> unit
        Relocate: unit -> unit
        Restore: (unit -> unit) option
    }

type private OwnedRange(host: Node) =
    let staging = fragment ()
    let first, last = comment "ranvier:start", comment "ranvier:end"
    let entries = ResizeArray<Entry>()
    let generatedDescendants = ResizeArray<Node>()
    let mutable placement: Node = staging
    // False while every root is a detached node created by the mount; `Attach` then inserts each root individually. A keyed region, a native node or a cloned-fragment root moves all roots into `staging`.
    let mutable staged = false
#if LADDER_NAIVE_RANGE
    let naiveRoots = ResizeArray<Node>()
#endif

    let intactAt (parent: Node) =
        let mutable cursor: Node = first
        let mutable intact = true

        for index in 0 .. entries.Count - 1 do
            let entry = at entries index
            entry.Validate ()

            if
                not (isChildOf entry.Node parent)
                || not (same cursor entry.Node)
            then
                intact <- false

            let mutable spanCursor = entry.Node

            while not (isNull (box spanCursor))
                  && not (same spanCursor entry.EndNode) do
                spanCursor <- nextOrNull spanCursor

            if
                isNull (box spanCursor)
                || not (isChildOf entry.EndNode parent)
            then
                intact <- false

            cursor <- nextOrNull entry.EndNode

        intact

    let stage () =
        if not staged then
            staged <- true

            for index in 0 .. entries.Count - 1 do
                let node = (at entries index).Node

                if not (isDetached node || isChildOf node staging) then
                    invalid "Range boundaries or owned roots were externally mutated"

                staging.appendChild node |> ignore

    do
        entries.Add
            {
                Node = first
                EndNode = first
                Validate = skip
                Relocate = skip
                Restore = None
            }

        entries.Add
            {
                Node = last
                EndNode = last
                Validate = skip
                Relocate = skip
                Restore = None
            }

        claim claims first
        claim claims last

    member _.Staging = staging

    member _.AddRegion(start: Node, finish: Node, validate: unit -> unit, relocate: unit -> unit) =
        stage ()

        entries.Insert (
            entries.Count - 1,
            {
                Node = start
                EndNode = finish
                Validate = validate
                Relocate = relocate
                Restore = None
            }
        )

        claim claims start
        claim claims finish
        insert staging start (Some last)
        insert staging finish (Some last)

    member this.AppendGenerated(parent: Node, node: Node) =
        generatedDescendants.Add node
        claim claims node
        parent.appendChild node |> ignore

#if LADDER_NAIVE_RANGE
    member _.AddNaive(node: Node) =
        stage ()
        naiveRoots.Add node
        insert staging node (Some last)

#endif
    /// <summary>Adds a root node created by the mount, either detached or a child of a cloned fragment.</summary>
    /// <remarks>The node and its subtree are unclaimed and contain neither the host nor the range.</remarks>
    member _.AddCreated(node: Node) =
        let attachNow = staged || not (isDetached node)

        if attachNow then
            stage ()

        entries.Insert (
            entries.Count - 1,
            {
                Node = node
                EndNode = node
                Validate = skip
                Relocate = skip
                Restore = None
            }
        )

        claim claims node

        if attachNow then
            insert staging node (Some last)

    member _.Add(node: ChildNode, ownership) =
        if contains node host || contains node staging then
            invalid "A native node cannot contain its mount host or range"

        if overlaps claims node then
            invalid "Native ownership overlaps a live mount"

        let restore =
            match ownership with
            | Owned -> None
            | Borrowed ->
                let originalParent, originalNext = node.parentNode, node.nextSibling

                Some (fun () ->
                    match originalParent with
                    | None -> remove node
                    | Some parent ->
                        let before =
                            originalNext
                            |> Option.filter (fun next -> next.parentNode |> Option.exists (same parent))

                        insert parent node before)

        stage ()

        entries.Insert (
            entries.Count - 1,
            {
                Node = node
                EndNode = node
                Validate = skip
                Relocate = skip
                Restore = restore
            }
        )

        claim claims node
        insert staging node (Some last)

    member _.Validate() =
        if not staged && same placement staging then
            for index in 0 .. entries.Count - 1 do
                if not (isDetached (at entries index).Node) then
                    invalid "Range boundaries or owned roots were externally mutated"
        elif intactAt placement then
            ()
        elif
            isDocumentFragment placement
            && placement.firstChild.IsNone
        then
            let relocatedParent =
                first.parentNode
                |> Option.defaultWith (fun () -> invalid "Range boundaries or owned roots were externally mutated")

            if not (intactAt relocatedParent) then
                invalid "Range roots were not contiguous after their staging fragment was attached"

            placement <- relocatedParent
        else
            invalid "Range boundaries or owned roots were externally mutated"

    member private _.ValidateDestination(parent: Node, before: Node option) =
        if not (sameDocument first parent) then
            invalid "Cross-document movement requires an explicit adoption policy"

        let anchor: Node =
            match before with
            | Some anchor when not (isChildOf anchor parent) -> invalid "Insertion anchor does not belong to the destination"
            | Some anchor -> anchor
            | None -> Unchecked.defaultof<_>

        for index in 0 .. entries.Count - 1 do
            let entry = at entries index
            let mutable cursor = entry.Node

            while not (isNull (box cursor)) do
                let node = cursor

                if contains node parent then
                    invalid "Cannot move a range into itself"

                if same node anchor && not (same first anchor) then
                    invalid "Cannot insert a range inside its own boundaries"

                cursor <-
                    if same node entry.EndNode then
                        Unchecked.defaultof<_>
                    else
                        nextOrNull node

    member this.Attach(parent, before) =
#if !LADDER_NAIVE_RANGE
        this.Validate ()
        this.ValidateDestination (parent, before)
#endif
        if staged then
            insert parent staging before
        else
            let mutable index = 0

            // An insertion that disposes the mount empties `entries`, ending the loop.
            while index < entries.Count do
                insert parent (at entries index).Node before
                index <- index + 1

        placement <- parent

    member _.Relocate() =
        placement <-
            first.parentNode
            |> Option.map (fun node -> node :> Node)
            |> Option.defaultWith (fun () -> invalid "Range has no parent")

        for entry in entries do
            entry.Relocate ()

    member this.Move(parent, before, rollbackFailed: unit -> unit) =
        this.Validate ()
        this.ValidateDestination (parent, before)

        if not (before |> Option.exists (same first)) then
            let originalParent, originalNext = placement, last.nextSibling

            try
                for entry in entries do
                    let mutable cursor = Some entry.Node

                    while cursor.IsSome do
                        let node = cursor.Value

                        cursor <-
                            if same node entry.EndNode then
                                None
                            else
                                nextSiblingNode node

                        staging.appendChild node |> ignore

                insert parent staging before
                placement <- parent

                for entry in entries do
                    entry.Relocate ()
            with original ->
                try
                    for entry in entries do
                        let mutable cursor = Some entry.Node

                        while cursor.IsSome do
                            let node = cursor.Value

                            cursor <-
                                if same node entry.EndNode then
                                    None
                                else
                                    nextSiblingNode node

                            staging.appendChild node |> ignore

                    insert originalParent staging originalNext
                    placement <- originalParent

                    for entry in entries do
                        entry.Relocate ()
                with rollback ->
                    rollbackFailed ()
                    raise (AggregateException ("Range movement and rollback failed", [| original; rollback |]))

                raise original

    member _.Release() =
        let errors = ResizeArray<exn>()

#if LADDER_NAIVE_RANGE
        for index in naiveRoots.Count - 1 .. -1 .. 0 do
            remove (naiveRoots[index] :?> ChildNode)

        naiveRoots.Clear ()

#endif
        for index in generatedDescendants.Count - 1 .. -1 .. 0 do
            let node = at generatedDescendants index

            try
                remove (node :?> ChildNode)
            with error ->
                errors.Add error

            unclaim claims node

        generatedDescendants.Clear ()

        for index in entries.Count - 1 .. -1 .. 0 do
            let entry = at entries index
            let spans = not (same entry.Node entry.EndNode)

            match entry.Restore with
            | Some restore ->
                try
                    restore ()
                with error ->
                    errors.Add error
            | None ->
                try
                    remove (entry.Node :?> ChildNode)
                with error ->
                    errors.Add error

                if spans then
                    try
                        remove (entry.EndNode :?> ChildNode)
                    with error ->
                        errors.Add error

            unclaim claims entry.Node

            if spans then
                unclaim claims entry.EndNode

        entries.Clear ()

        if errors.Count = 1 then
            raise errors[0]
        elif errors.Count > 1 then
            raise (AggregateException ("Range release failed", errors))

type Mounted
    internal
    (
        scope: Scope,
        move: Node * Node option -> unit,
        validate: unit -> unit,
        relocate: unit -> unit,
        flush: unit -> unit,
        commitErrors: unit -> exn array
    ) =
    member _.Phase = scope.Phase
    member _.Failures = scope.Failures
    member _.CommitErrors = commitErrors ()

    member _.Flush() =
        if not scope.IsDisposed then
            flush ()

    /// <summary>Checks that the mount's recorded DOM range remains intact.</summary>
    member internal _.Validate() =
        validate ()

    member internal _.Relocate() =
        relocate ()

    member _.Dispose() =
        scope.Dispose ()

    member _.MoveBefore(parent, before) =
        if scope.Phase <> Attached then
            invalid "Only an attached mount can move"

        move (parent, before)

type private KeyedEntry =
    {
        Key: obj
        Value: Signal<obj>
        First: Node
        Last: Node
        Mount: Mounted
    }

exception private KeyedDisposed
exception private KeyedSetupFailed of exn list

/// <summary>The state of a mounted view and the backend that builds it.</summary>
type private MountState(scheduling: Scheduling, graph: Graph, parentScope: Scope option, report: exn list -> unit, host: Node) =
    inherit Prepared.Backend()

    let range = OwnedRange host

    let cleanupErrors = ResizeArray<exn>()

    let release () =
        range.Release ()

        if cleanupErrors.Count > 0 then
            let errors = cleanupErrors.ToArray ()
            cleanupErrors.Clear ()

            if errors.Length = 1 then
                raise errors[0]
            else
                raise (AggregateException ("Component cleanup failed", errors))

    let reporting (callback: unit -> unit) () =
        try
            callback ()
        with error ->
            cleanupErrors.Add error

    let ownedCleanup (owner: Owner) callback =
        if owner.IsDisposed then
            invalid "Component cleanup registration requires a live component"

        owner.OnCleanup (Action (reporting callback))

    let root =
        match parentScope with
        | Some parent -> parent.Child (report, release)
        | None -> Scope (report, release)

    let mutable parent: Node = range.Staging
    let mutable bindingSetupFailure: exn option = None
    let mutable modifierNode: HTMLElement = Unchecked.defaultof<_>
    let mutable modifierTag = Div

    let ensureSetup () =
#if LADDER_NAIVE_CHECKS
        ()
#else
        if root.Phase <> SettingUp then
            invalid "View construction is only valid during live setup"
#endif

    let append node =
        ensureSetup ()

#if LADDER_NAIVE_RANGE
        if same parent range.Staging then
            range.AddNaive node
        else
            parent.appendChild node |> ignore
#else
        if same parent range.Staging then
            range.AddCreated node
        else
            parent.appendChild node |> ignore
#endif

    let currentElement () =
#if !LADDER_NAIVE_CHECKS
        ensureSetup ()

        if isNull (box modifierNode) then
            invalid "Element modifiers require an active element"

#endif
        modifierNode

    let applyModifiers (backend: Backend) tag node (modifiers: ModifierAction array) =
        let previousNode = modifierNode
        let previousTag = modifierTag
        modifierNode <- node
        modifierTag <- tag

        try
            for index in 0 .. modifiers.Length - 1 do
                ensureSetup ()
                modifiers[index] backend
                ensureSetup ()
        finally
            modifierNode <- previousNode
            modifierTag <- previousTag

    let openElement (backend: Backend) tag modifiers =
        ensureSetup ()
        let node = document.createElement (name tag)
        append node

        if tag <> Select then
            applyModifiers backend tag node modifiers

        node

    let childParent tag (node: HTMLElement) : Node =
        if name tag = "template" then
            (node :?> HTMLTemplateElement).content :> Node
        else
            node

    let mutable lastReported: exn = null

    // Sends a reader failure to `report`, untracked, once per exception object until `clearReported` runs.
    // A pending read (`NotReadyException`) is not reported.
    let reportReaderFailure (error: exn) =
        match error with
        | NotReadyException _ -> ()
        | _ when obj.ReferenceEquals (error, lastReported) -> ()
        | _ ->
            lastReported <- error

            try
                Api.untrack (fun () -> report [ error ])
            with reporterError ->
                raise (AggregateException ("Binding reader and reporter failed", [| error; reporterError |]))

    // Called when a failed reader recovers; the same exception object is reported again if it recurs.
    let clearReported () =
        lastReported <- null

    let startBinding (equals: Func<_, _, bool>) read write =
#if LADDER_NAIVE_BINDINGS
        Api.createEffect (fun () -> write (read ()))
#else
        match scheduling with
        | Synchronous ->
            let mutable hasCommitted = false
            let mutable committed = Unchecked.defaultof<_>
            let mutable pending = Unchecked.defaultof<_>

            let commit () =
                let changed =
                    not hasCommitted
                    || not (equals.Invoke (committed, pending))

                if changed && not root.IsDisposed then
                    try
                        write pending
                        committed <- pending
                        hasCommitted <- true
                    with error when root.Phase = SettingUp ->
                        bindingSetupFailure <- Some error
                        reraise ()

            let mutable failed = false

            Api.createEffect (fun () ->
                let value =
                    try
                        read ()
                    with error ->
                        failed <- true
                        reportReaderFailure error
                        reraise ()

                if failed then
                    failed <- false
                    clearReported ()

                if not root.IsDisposed then
                    pending <- value
                    Api.untrack commit)
        | Queued queue ->
            let live () =
                not root.IsDisposed

            let guardedWrite value =
                try
                    write value
                with error when root.Phase = SettingUp ->
                    bindingSetupFailure <- Some error
                    reraise ()

            let mutable failed = false

            let reportingRead () =
                let value =
                    try
                        read ()
                    with error ->
                        failed <- true
                        reportReaderFailure error
                        reraise ()

                if failed then
                    failed <- false
                    clearReported ()

                value

            Binding.startQueued root.OnCleanup live queue (fun left right -> equals.Invoke (left, right)) reportingRead guardedWrite

        bindingSetupFailure |> Option.iter raise
#endif

    let delegatedNames = ResizeArray<string>()
    let mutable delegatedHost: Node = Unchecked.defaultof<_>

    let delegateAt (target: Node) =
        for index in 0 .. delegatedNames.Count - 1 do
            let eventName = delegatedNames[index]
            delegateFrom.Invoke (target, eventName)

        delegatedHost <- target

    let releaseDelegation () =
        if not (isNull (box delegatedHost)) then
            for index in 0 .. delegatedNames.Count - 1 do
                let eventName = delegatedNames[index]
                undelegateFrom.Invoke (delegatedHost, eventName)

            delegatedHost <- Unchecked.defaultof<_>

    let mutable preparedRoot: Node = Unchecked.defaultof<_>

    let ensurePrepared (node: Node) =
        ensureSetup ()

        if isNull (box preparedRoot) then
            invalid "Prepared node operations require active clone setup"

        let mutable belongs = contains preparedRoot node

        if not belongs then
            walkPrepared
                (fun current ->
                    if same current node then
                        belongs <- true)
                preparedRoot

        if not belongs then
            invalid "Prepared node operations require a node from the current clone"

    let registerDelegated (eventName: string) =
        let mutable known = false

        for index in 0 .. delegatedNames.Count - 1 do
            if delegatedNames[index] = eventName then
                known <- true

        if not known then
            delegatedNames.Add eventName

    let listenDirect (node: Node) eventName (invoke: obj -> unit) =
        let cleanup = Typed.listenDirect (box node) eventName invoke

        if root.IsDisposed then
            cleanup ()
            ensureSetup ()

        ownedCleanup (Api.getOwner ()) cleanup

    let mutable templateServices: TemplateDom.MountServices = Unchecked.defaultof<_>

    let getTemplateServices () =
        if isNull (box templateServices) then
            templateServices <-
                {
                    Append = fun node -> append (node :?> ChildNode)
                    Setup =
                        {
                            OnCleanup = fun callback -> ownedCleanup (Api.getOwner ()) callback
                        }
                    SettingUp = fun () -> root.Phase = SettingUp
                    SetupFailed = fun error -> bindingSetupFailure <- Some error
                    RaiseSetupFailure = fun () -> bindingSetupFailure |> Option.iter raise
                    IsDelegated = fun eventName -> isDelegated delegatedEvents eventName
                    Delegate = registerDelegated
                    ListenDirect = listenDirect
                    ReportFailure = reportReaderFailure
                    ClearFailure = clearReported
                }

        templateServices

    member _.Root = root

    /// <summary>Builds the view, attaches it before <c>before</c> in the host, and returns its handle.</summary>
    /// <remarks>A build or attachment failure disposes the root scope and rethrows.</remarks>
    member this.Run(before: Node option, build: BuildContext -> unit) =
        let context =
            {
                Backend = this
                Native =
                    fun (node, ownership) ->
                        ensureSetup ()

                        if not (same parent range.Staging) then
                            invalid "Native roots require top-level placement"

                        range.Add (node :?> ChildNode, ownership)
                OnCleanup = root.OnCleanup
                Phase = fun () -> root.Phase
            }

        try
            graph.Run (fun () ->
                Api.createRoot (fun owner ->
                    Api.onCleanup root.Dispose
                    root.OnCleanup owner.Dispose
                    Api.untrack (fun () -> build context)))

            ensureSetup ()
            range.Attach (host, before)
            root.MarkAttached ()
            delegateAt host
            root.OnCleanup releaseDelegation

            Mounted (
                root,
                (fun (parent, before) ->
                    range.Move (parent, before, root.Dispose)

                    if not (same parent delegatedHost) then
                        releaseDelegation ()
                        delegateAt parent),
                (fun () -> range.Validate ()),
                (fun () -> range.Relocate ()),
                (fun () ->
                    match scheduling with
                    | Synchronous -> graph.Run Api.flush
                    | Queued queue -> queue.Flush ()),
                (fun () ->
                    match scheduling with
                    | Synchronous -> [||]
                    | Queued queue -> queue.Errors)
            )
        with _ ->
            root.Dispose ()
            reraise ()

    override _.Clone(template, setup) =
        ensureSetup ()
        let node = template.cloneNode true :?> HTMLElement
        append node
        let previous = preparedRoot
        preparedRoot <- node

        try
            setup node
            ensureSetup ()
        finally
            preparedRoot <- previous

    override this.Apply(node, modifiers) =
        ensurePrepared node

        let tag =
            if node.tagName = "INPUT" then
                Input
            else
                HtmlTag node.localName

        applyModifiers this tag node modifiers

    override _.BindText(node, read) =
        ensurePrepared node

        if node.nodeType <> 3. then
            raise (ArgumentException ("Prepared text bindings require a text node", "node"))

        startBinding (Func<_, _, _>(fun left right -> left = right)) read (fun value -> node.textContent <- Some value)
        ensureSetup ()

    override _.Text(value) =
        ensureSetup ()
        append (text value)

    override _.Attribute(key, value) =
        let node = currentElement ()
#if !LADDER_NAIVE_CHECKS && !LADDER_UNCHECKED_ATTRIBUTES
        validateAttribute key
#endif
        node.setAttribute (key, value)

    override _.OptionalAttribute(key, value) =
        let node = currentElement ()
#if !LADDER_NAIVE_CHECKS && !LADDER_UNCHECKED_ATTRIBUTES
        validateAttribute key
#endif
        attributeValue node key value

    override _.ReactiveAttribute(key, read) =
        let node = currentElement ()
#if !LADDER_NAIVE_CHECKS && !LADDER_UNCHECKED_ATTRIBUTES
        validateAttribute key
#endif
        startBinding attributeEquals read (attributeValue node key)

    override _.InputValue(value) =
        let node = currentElement ()

        if modifierTag <> Input then
            invalidArg "modifiers" "Input value requires input"

        inputValue (node :?> HTMLInputElement) value

    override _.Browser(hook) =
        let cleanup = hook (box (currentElement ()))

        if root.IsDisposed then
            cleanup ()
            ensureSetup ()

        ownedCleanup (Api.getOwner ()) cleanup

    override _.Listen(eventName, handler) =
        let node = currentElement ()
        let invoke = Typed.ownedHandler handler

        if isDelegated delegatedEvents eventName then
            setDelegatedHandler node eventName invoke
            registerDelegated eventName
        else
            listenDirect node eventName invoke

    override _.Property(write) =
        write (box (currentElement ()))

    override _.ReactiveProperty(read, write) =
        let node = currentElement ()
        startBinding propertyEquals read (fun value -> write.Invoke (box node, value))

    override this.Element(tag, modifiers, children) =
        let node = openElement this tag modifiers

        if not (isVoid tag) then
            let previous = parent
            parent <- childParent tag node

            try
                children ()
            finally
                parent <- previous

        if tag = Select then
            applyModifiers this tag node modifiers

    override this.ElementWith(tag, modifiers, children) =
        let node = openElement this tag modifiers

        if not (isVoid tag) then
            let previous = parent
            parent <- childParent tag node

            try
                for index in 0 .. children.Length - 1 do
                    children[index] this
            finally
                parent <- previous

        if tag = Select then
            applyModifiers this tag node modifiers

    override _.Bind(equals, read) =
        ensureSetup ()
        let node = text ""
        append node
        startBinding (Func<_, _, _>(fun left right -> equals left right)) read (fun value -> node.textContent <- value)

    override _.Keyed(equals, read, render) =
        ensureSetup ()

        let regionStart, regionEnd =
            comment "ranvier:list:start", comment "ranvier:list:end"

        if not (same parent range.Staging) then
            range.AppendGenerated (parent, regionStart)
            range.AppendGenerated (parent, regionEnd)

        let regionScope = root.Child (report, ignore)
        let mutable entries: KeyedEntry array = [||]
        let mutable regionOwner: Owner option = None
        let mutable initializing = true
        let mutable setupFailure: exn option = None
        let mutable rowSetupErrors: ResizeArray<exn> option = None

        let reportRow errors =
            match rowSetupErrors with
            | Some collected -> collected.AddRange errors
            | None -> report errors

        let ensureRegionLive () =
            if root.IsDisposed || regionScope.IsDisposed then
                raise KeyedDisposed

        let liveEquals left right =
            ensureRegionLive ()
            let result = equals left right
            ensureRegionLive ()
            result

        let getRegionParent () =
            let regionParent =
                regionStart.parentNode
                |> Option.defaultWith (fun () -> invalid "Keyed-list boundaries have no parent")

            if
                not (
                    regionEnd.parentNode
                    |> Option.exists (same regionParent)
                )
            then
                invalid "Keyed-list boundaries were externally mutated"

            regionParent

        let validateRegion regionParent =
            if
                not (
                    regionStart.parentNode
                    |> Option.exists (same regionParent)
                )
                || not (
                    regionEnd.parentNode
                    |> Option.exists (same regionParent)
                )
            then
                invalid "Keyed-list boundaries were externally mutated"

            let mutable cursor = nextSiblingNode regionStart

            for entry in entries do
                entry.Mount.Validate ()

                if not (cursor |> Option.exists (same entry.First)) then
                    invalid "Keyed-list rows or contents were externally mutated"

                cursor <- nextSiblingNode entry.Last

            if not (cursor |> Option.exists (same regionEnd)) then
                invalid "Keyed-list boundaries were externally reordered"

        if same parent range.Staging then
            range.AddRegion (
                regionStart,
                regionEnd,
                (fun () -> validateRegion (getRegionParent ())),
                (fun () ->
                    for entry in entries do
                        entry.Mount.Relocate ())
            )

        let moveToOrder regionParent (ordered: KeyedEntry array) =
            let mutable anchor: Node option = Some regionEnd

            for index in ordered.Length - 1 .. -1 .. 0 do
                let entry = ordered[index]
                ensureRegionLive ()

                let alreadyPlaced =
                    entry.Last.parentNode
                    |> Option.exists (same regionParent)
                    && (nextSiblingNode entry.Last
                        |> Option.exists (fun next -> anchor |> Option.exists (same next)))

                if not alreadyPlaced then
                    entry.Mount.MoveBefore (regionParent, anchor)

                ensureRegionLive ()

                anchor <- Some entry.First

        let createEntry key value =
            ensureRegionLive ()
            let item = graph.Run (fun () -> Api.createSignal value)
            let staging = fragment ()
            let setupErrors = ResizeArray<exn>()
            let previousErrors = rowSetupErrors

            let owner =
                regionOwner
                |> Option.defaultWith (fun () -> invalid "Keyed-list owner is unavailable")

            let mounted: Mounted =
                rowSetupErrors <- Some setupErrors

                try
                    try
                        Api.runWithOwner owner (fun () ->
                            MountState(scheduling, graph, Some regionScope, reportRow, staging)
                                .Run(None, (fun context -> render (fun () -> item.Value) context.Backend)))
                    with original ->
                        if setupErrors.Count > 0 then
                            raise (KeyedSetupFailed (original :: List.ofSeq setupErrors))

                        ensureRegionLive ()
                        reraise ()
                finally
                    rowSetupErrors <- previousErrors

            if setupErrors.Count > 0 then
                let errors = List.ofSeq setupErrors

                try
                    Api.untrack (fun () -> report errors)
                with reporterError ->
                    let failures = ResizeArray<exn>(errors)
                    failures.Add reporterError

                    try
                        mounted.Dispose ()
                    with cleanupError ->
                        failures.Add cleanupError

                    raise (AggregateException ("Keyed row reader and reporter failed", failures))

            let first =
                staging.firstChild
                |> Option.defaultWith (fun () -> invalid "A keyed row has no start boundary")

            let last =
                staging.lastChild
                |> Option.defaultWith (fun () -> invalid "A keyed row has no end boundary")

            {
                Key = key
                Value = item
                First = first
                Last = last
                Mount = mounted
            }

        let reconcileItems items =
            ensureRegionLive ()
            let regionParent = getRegionParent ()
            validateRegion regionParent
            Keyed.validateUniqueKeys liveEquals items
            ensureRegionLive ()

            let matches =
                items
                |> Array.map (fun (key, value) ->
                    let matched =
                        entries
                        |> Array.tryFind (fun entry -> liveEquals entry.Key key)

                    ensureRegionLive ()

                    match matched with
                    | Some entry -> Choice1Of2 (entry, value)
                    | None -> Choice2Of2 (key, value))

            let created = ResizeArray<KeyedEntry>()
            let desired = ResizeArray<KeyedEntry * obj>()

            try
                for matchResult in matches do
                    match matchResult with
                    | Choice1Of2 (entry, value) -> desired.Add (entry, value)
                    | Choice2Of2 (key, value) ->
                        let entry = createEntry key value
                        created.Add entry
                        ensureRegionLive ()
                        desired.Add (entry, value)
            with original ->
                let errors = ResizeArray<exn>()
                errors.Add original

                for index in created.Count - 1 .. -1 .. 0 do
                    try
                        created[index].Mount.Dispose()
                    with error ->
                        errors.Add error

                if errors.Count = 1 then
                    reraise ()
                else
                    raise (AggregateException ("Keyed row setup and cleanup failed", errors))

            let nextEntries = Array.init desired.Count (fun index -> fst desired[index])

            try
                moveToOrder regionParent nextEntries
            with movementError ->
                ensureRegionLive ()

                try
                    moveToOrder regionParent entries
                with rollbackError ->
                    root.Dispose ()
                    raise (AggregateException ("Keyed reconciliation and rollback failed", [| movementError; rollbackError |]))

                let errors = ResizeArray<exn>()
                errors.Add movementError

                for entry in created do
                    try
                        entry.Mount.Dispose ()
                    with error ->
                        errors.Add error

                if errors.Count = 1 then
                    reraise ()
                else
                    raise (AggregateException ("Keyed reconciliation and cleanup failed", errors))

            let removedEntries =
                entries
                |> Array.filter (fun entry ->
                    nextEntries
                    |> Array.exists (fun next -> obj.ReferenceEquals (entry, next))
                    |> not)

            Api.batch (fun () ->
                for entry, value in desired do
                    ensureRegionLive ()
                    entry.Value.Value <- value

                entries <- nextEntries

                for removed in removedEntries do
                    removed.Mount.Dispose ())

        let reconcile () =
            try
                let items = Array.copy (read ())
                Api.untrack (fun () -> reconcileItems items)
            with
            | KeyedDisposed -> ()
            | error ->
                let errors =
                    match error with
                    | KeyedSetupFailed errors -> errors
                    | _ -> [ error ]

                if initializing then
                    setupFailure <-
                        Some (
                            match errors with
                            | [ error ] -> error
                            | _ ->
                                AggregateException (
                                    "Keyed row setup and cleanup failed: "
                                    + String.concat "; " (errors |> List.map _.Message),
                                    errors
                                )
                        )
                else
                    try
                        Api.untrack (fun () -> report errors)
                    with reporterError ->
                        raise (AggregateException ("Keyed update and reporter failed", [| error; reporterError |]))

                    reraise ()

        Api.createRoot (fun owner ->
            regionOwner <- Some owner
            Api.onCleanup regionScope.Dispose
            regionScope.OnCleanup owner.Dispose
            Api.untrack (fun () -> Api.createEffect reconcile))

        initializing <- false

        match setupFailure with
        | Some error -> raise error
        | None -> ()

    override _.Component(setup) =
        ensureSetup ()
#if LADDER_NAIVE_COMPONENTS
        setup
            {
                OnCleanup = ownedCleanup (Api.getOwner ())
            }
#else
        Api.createRoot (fun owner ->
            Api.untrack (fun () ->
                setup
                    {
                        OnCleanup = fun callback -> ownedCleanup owner callback
                    })

            if owner.IsDisposed then
                invalid "View construction is only valid during live setup")
#endif

    override this.Template(definition, props) =
        ensureSetup ()

        match scheduling with
        | Synchronous -> TemplateDom.materialise definition props (getTemplateServices ())
        | Queued _ -> definition.Expand (this, props)

let private mountCore scheduling onRoot graph parentScope report host before build =
    let state = MountState (scheduling, graph, parentScope, report, host)
    onRoot state.Root
    state.Run (before, build)

/// <summary>Mounts a view with an owned commit queue when deferred scheduling is selected.</summary>
/// <remarks>Nested keyed mounts share the containing mount's scheduler.</remarks>
let mountWith mode graph parentScope report host before build =
    match mode with
    | SynchronousCommits -> mountCore Synchronous ignore graph parentScope report host before build
    | QueuedCommits (failure, post) ->
        let queue = Queue (graph, failure, post)

        try
            mountCore (Queued queue) (fun root -> root.OnCleanup queue.Dispose) graph parentScope report host before build
        with _ ->
            queue.Dispose ()
            reraise ()

let mount graph parentScope report host before build =
    mountWith SynchronousCommits graph parentScope report host before build
