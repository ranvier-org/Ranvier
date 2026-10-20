/// <summary>Hydration: a client mount that adopts the nodes of a template component's server HTML.</summary>
module Ranvier.View.Foundation.Hydration

open System
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Scheduling
open Ranvier.View.Foundation.Mounting

/// <summary>The client's settings for hydrating server HTML.</summary>
type HydrationOptions =
    {
        /// <summary>The root prefix of the ids <c>Setup.CreateId</c> returns; must match the server's.</summary>
        IdPrefix: string
        /// <summary>Whether a structural mismatch raises from <c>hydrate</c>.</summary>
        Strict: bool
    }

/// <summary>The first <c>&lt;!--[r--&gt;</c> child of <c>host</c>, or null.</summary>
[<Emit("(() => { for (let node = $0.firstChild; node; node = node.nextSibling) if (node.nodeType === 8 && node.data === '[r') return node; return null; })()")>]
let private rootMarker (host: Node) : Node = jsNative

[<Emit("$0.nextSibling")>]
let private following (node: Node) : Node = jsNative

/// <summary>
/// Mounts <c>definition</c> for <c>props</c> over the server HTML between the first <c>&lt;!--[r--&gt;</c> child of
/// <c>host</c> and its matching end marker, adopting the server's nodes. The result behaves as a mount by
/// <c>mountWith SynchronousCommits</c>: its slots compare their first value with the server's nodes and write only a
/// difference, and its regions mount their rows fresh between the server's region markers.
/// </summary>
/// <remarks>
/// Nodes outside the root marker pair stay in place and are never owned by the mount. A failure disposes what the call
/// created, leaves the adopted nodes in place with their structural edits undone, and propagates.
/// </remarks>
/// <exception cref="T:System.NotSupportedException"><c>mode</c> is <c>QueuedCommits</c>.</exception>
/// <exception cref="T:Ranvier.View.Foundation.TemplateDom.HydrationMismatchException">
/// The host has no root marker, or the server HTML differs from the template's structure.
/// </exception>
let hydrateWith
    (mode: CommitMode)
    (graph: Graph)
    (parentOwner: Owner option)
    (report: exn list -> unit)
    (options: HydrationOptions)
    (host: Node)
    (definition: Component<'props>)
    (props: 'props)
    : Mounted =
    match mode with
    | QueuedCommits _ -> raise (NotSupportedException "Hydration supports synchronous commits only")
    | SynchronousCommits ->
        let rootStart = rootMarker host

        if isNull (box rootStart) then
            raise (TemplateDom.mismatch "root" "<!--[r-->" "absent")

        let rootEnd = TemplateDom.matchingEnd rootStart
        let first = following rootStart
        let cursor = TemplateDom.HydrationCursor (first, options.Strict)

        let owner =
            match parentOwner with
            | Some owner -> owner
            | None -> Unchecked.defaultof<_>

        let state =
            MountState (
                Synchronous,
                graph,
                null,
                owner,
                report,
                host,
                Unchecked.defaultof<_>,
                Unchecked.defaultof<_>,
                IdentityScope options.IdPrefix,
                null,
                InstanceStep.Root,
                0,
                0,
                cursor,
                rootStart,
                rootEnd
            )

        state.Run (
            None,
            fun context ->
                context.Backend.Template (definition, box props)

                if not (obj.ReferenceEquals (cursor.Next, rootEnd)) then
                    let mutable position = 0
                    let mutable node = first

                    while not (obj.ReferenceEquals (node, cursor.Next)) do
                        node <- TemplateDom.logicalNext node
                        position <- position + 1

                    raise (TemplateDom.mismatch ("#" + string position) "<!--]-->" (TemplateDom.describe cursor.Next))
        )

/// <summary>Hydrates as <c>hydrateWith SynchronousCommits</c> does.</summary>
let hydrate graph parentOwner report options host (definition: Component<'props>) (props: 'props) : Mounted =
    hydrateWith SynchronousCommits graph parentOwner report options host definition props
