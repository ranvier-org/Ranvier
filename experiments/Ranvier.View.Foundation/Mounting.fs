module Ranvier.View.Foundation.Mounting

open System
open Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation

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

[<Emit("document.createDocumentFragment()")>]
let private fragment () : Node = jsNative

[<Emit("document.createComment($0)")>]
let private comment (value: string) : Node = jsNative

[<Emit("document.createTextNode($0)")>]
let private text (value: string) : Node = jsNative

[<Emit("document.createElement($0)")>]
let private element (tag: string) : HTMLElement = jsNative

[<Emit("$0.insertBefore($1, $2 == null ? null : $2)")>]
let private insert (parent: Node) (node: Node) (before: Node option) : unit = jsNative

[<Emit("$0.remove()")>]
let private remove (node: Node) : unit = jsNative

[<Emit("$0.contains($1)")>]
let private contains (parent: Node) (node: Node) : bool = jsNative

[<Emit("$0.ownerDocument === $1.ownerDocument")>]
let private sameDocument (a: Node) (b: Node) : bool = jsNative

[<Emit("$0.value = $1")>]
let private inputValue (node: HTMLElement) (value: string) : unit = jsNative

[<Emit("$0.nextSibling")>]
let private nextSibling (node: Node) : Node option = jsNative

let private invalid message =
    raise (InvalidOperationException message)

let private same a b =
    obj.ReferenceEquals (a, b)

type private Entry =
    {
        Node: Node
        Restore: (unit -> unit) option
    }

type private OwnedRange(host: Node) =
    let staging = fragment ()
    let first, last = comment "ranvier:start", comment "ranvier:end"
    let entries = ResizeArray<Entry>()
    let mutable placement: Node = staging

    do
        staging.appendChild first |> ignore
        staging.appendChild last |> ignore
        entries.Add { Node = first; Restore = None }
        entries.Add { Node = last; Restore = None }

    member _.Staging = staging

    member _.Add(node: Node, ownership) =
        if contains node host || contains node staging then
            invalid "A native node cannot contain its mount host or range"

        for entry in entries do
            if
                contains entry.Node node
                || contains node entry.Node
            then
                invalid "Native ownership overlaps an existing range entry"

        let restore =
            match ownership with
            | Owned -> None
            | Borrowed ->
                let originalParent, originalNext = node.parentNode, nextSibling node

                Some (fun () ->
                    match originalParent with
                    | None -> remove node
                    | Some parent ->
                        let before =
                            originalNext
                            |> Option.filter (fun next -> next.parentNode |> Option.exists (same parent))

                        insert parent node before)

        entries.Insert (entries.Count - 1, { Node = node; Restore = restore })
        insert staging node (Some last)

    member private _.Validate() =
        let mutable cursor = Some first

        for entry in entries do
            if
                not (
                    entry.Node.parentNode
                    |> Option.exists (same placement)
                )
                || not (cursor |> Option.exists (same entry.Node))
            then
                invalid "Range boundaries or owned roots were externally mutated"

            cursor <- nextSibling entry.Node

    member private _.ValidateDestination(parent: Node, before: Node option) =
        if not (sameDocument first parent) then
            invalid "Cross-document movement requires an explicit adoption policy"

        match before with
        | Some anchor when not (anchor.parentNode |> Option.exists (same parent)) -> invalid "Insertion anchor does not belong to the destination"
        | _ -> ()

        for entry in entries do
            if contains entry.Node parent then
                invalid "Cannot move a range into itself"

            if
                before
                |> Option.exists (fun anchor -> same entry.Node anchor && not (same first anchor))
            then
                invalid "Cannot insert a range inside its own boundaries"

    member this.Attach(parent, before) =
        this.Validate ()
        this.ValidateDestination (parent, before)
        insert parent staging before
        placement <- parent

    member this.Move(parent, before) =
        this.Validate ()
        this.ValidateDestination (parent, before)

        if not (before |> Option.exists (same first)) then
            let originalParent, originalNext = placement, nextSibling last

            try
                for entry in entries do
                    staging.appendChild entry.Node |> ignore

                insert parent staging before
                placement <- parent
            with original ->
                try
                    for entry in entries do
                        staging.appendChild entry.Node |> ignore

                    insert originalParent staging originalNext
                with rollback ->
                    raise (AggregateException ("Range movement and rollback failed", [| original; rollback |]))

                raise original

    member _.Release() =
        let errors = ResizeArray<exn>()

        for index in entries.Count - 1 .. -1 .. 0 do
            let entry = entries[index]

            try
                match entry.Restore with
                | Some restore -> restore ()
                | None -> remove entry.Node
            with error ->
                errors.Add error

        entries.Clear ()

        if errors.Count = 1 then
            raise errors[0]
        elif errors.Count > 1 then
            raise (AggregateException ("Range release failed", errors))

type Mounted internal (scope: Scope, move: Node * Node option -> unit) =
    member _.Phase = scope.Phase
    member _.Failures = scope.Failures

    member _.Dispose() =
        scope.Dispose ()

    member _.MoveBefore(parent, before) =
        if scope.Phase <> Attached then
            invalid "Only an attached mount can move"

        move (parent, before)

let mount (graph: Graph) (parentScope: Scope option) (report: exn list -> unit) (host: Node) (before: Node option) (build: BuildContext -> unit) =
    let range = OwnedRange host

    let root =
        match parentScope with
        | Some parent -> parent.Child range.Release
        | None -> Scope (report, range.Release)

    let mutable parent = range.Staging
    let mutable scope = root

    let ensureSetup () =
        if
            root.Phase <> SettingUp
            || scope.Phase <> SettingUp
        then
            invalid "View construction is only valid during live setup"

    let append node =
        ensureSetup ()

        if same parent range.Staging then
            range.Add (node, Owned)
        else
            parent.appendChild node |> ignore

    let backend =
        { new Backend() with
            member _.Text value =
                ensureSetup ()
                append (text value)

            member _.Element(tag, tokens, children) =
                ensureSetup ()
                let node = element (name tag)
                append node

                for token in tokens do
                    ensureSetup ()

                    match token with
                    | Attribute (key, value) ->
                        validateAttribute key
                        node.setAttribute (key, value)
                    | InputValue value ->
                        if tag <> Input then
                            invalidArg "tokens" "Input value requires input"

                        inputValue node value
                    | Browser hook ->
                        let cleanup = hook (box node)

                        if scope.IsDisposed then
                            cleanup ()
                            ensureSetup ()

                        scope.OnCleanup cleanup

                if tag <> Input then
                    let previous = parent
                    parent <- node

                    try
                        children ()
                    finally
                        parent <- previous

            member _.Bind(equals, read) =
                ensureSetup ()
                let node = text ""
                append node
                let mutable committed: string option = None

                Api.createEffect (fun () ->
                    let value = read ()

                    if not root.IsDisposed && not scope.IsDisposed then
                        let changed =
                            committed
                            |> Option.forall (fun old -> not (equals old value))

                        if changed then
                            Api.untrack (fun () -> node.textContent <- Some value)
                            committed <- Some value)

            member _.Component setup =
                ensureSetup ()
                let previous = scope
                let child = scope.Child ignore
                scope <- child

                try
                    Api.createRoot (fun owner ->
                        Api.onCleanup child.Dispose
                        child.OnCleanup owner.Dispose
                        Api.untrack (fun () -> setup { OnCleanup = child.OnCleanup }))
                finally
                    scope <- previous
        }

    let context =
        {
            Backend = backend
            Native =
                fun (node, ownership) ->
                    ensureSetup ()

                    if not (same parent range.Staging) then
                        invalid "Native roots require top-level placement"

                    range.Add (node, ownership)
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
        Mounted (root, fun (parent, before) -> range.Move (parent, before))
    with _ ->
        root.Dispose ()
        reraise ()
