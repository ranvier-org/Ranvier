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

[<Emit("$0.remove()")>]
let private removeNode (node: Node) : unit = jsNative

/// <summary>Removes the siblings after <c>start</c> up to <c>finish</c>, or to the last sibling when <c>finish</c> is null.</summary>
let private clearAfter (start: Node) (finish: Node) =
    let mutable node = following start

    while not (isNull (box node))
          && not (obj.ReferenceEquals (node, finish)) do
        let next = following node
        removeNode node
        node <- next

/// <summary>
/// Hydrates the root of <c>host</c> as <c>hydrateWith</c> does, the document's head aside; before a re-rendered root
/// mounts fresh, <c>served</c> sets the head back as the server wrote it.
/// </summary>
let private adoptHost
    (served: unit -> unit)
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
        let owner =
            match parentOwner with
            | Some owner -> owner
            | None -> Unchecked.defaultof<_>

        let build (context: BuildContext) =
            context.Backend.Template (definition, box props)

        let state (cursor: TemplateDom.HydrationCursor) (rootStart: Node) (rootEnd: Node) =
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

        let fresh (before: Node option) =
            (state null Unchecked.defaultof<_> Unchecked.defaultof<_>).Run(before, build)

        let rootStart = rootMarker host

        if isNull (box rootStart) then
            let error = TemplateDom.mismatch "root" "<!--[r-->" "absent"

            if options.Strict then
                raise error

            report [ error ]
            fresh None
        else
            let rootEnd =
                try
                    TemplateDom.rootEnd rootStart
                with TemplateDom.HydrationMismatchException _ as error when not options.Strict ->
                    report [ error ]
                    clearAfter rootStart Unchecked.defaultof<_>
                    Unchecked.defaultof<_>

            if isNull (box rootEnd) then
                fresh None
            else
                let first = following rootStart
                let cursor = TemplateDom.HydrationCursor (first, options.Strict)

                try
                    (state cursor rootStart rootEnd)
                        .Run(
                            None,
                            fun context ->
                                build context

                                if not (obj.ReferenceEquals (cursor.Next, rootEnd)) then
                                    let mutable position = 0
                                    let mutable node = first

                                    while not (obj.ReferenceEquals (node, cursor.Next)) do
                                        node <- TemplateDom.logicalNext node
                                        position <- position + 1

                                    raise (TemplateDom.mismatch ("#" + string position) "<!--]-->" (TemplateDom.describe cursor.Next))
                        )
                with error when not options.Strict ->
                    match TemplateDom.findMismatch error with
                    | Some found ->
                        served ()
                        report [ found :> exn ]
                        clearAfter rootStart rootEnd
                        fresh (Some rootEnd)
                    | None -> reraise ()

/// <summary>
/// Mounts <c>definition</c> for <c>props</c> over the server HTML between the first <c>&lt;!--[r--&gt;</c> child of
/// <c>host</c> and its matching end marker, adopting the server's nodes. The result behaves as a mount by
/// <c>mountWith SynchronousCommits</c>: its slots compare their first value with the server's nodes and write only a
/// difference, and its regions mount their rows fresh between the server's region markers.
/// </summary>
/// <remarks>
/// <para>Nodes outside the root marker pair stay in place and outside the mount's range.</para>
/// <para>
/// Document entries adopt the head elements marked with their key, as in every mount, and a title entry writes the
/// document's title only when it differs. Once the mount is built, every unadopted marked element of the document's
/// head is removed, those served for other roots included; an entry under pending content keeps its adopted element.
/// </para>
/// <para>
/// On a structural mismatch, the head is set back as the server wrote it and the mismatch is passed to <c>report</c>
/// once, as a one-element list. The server nodes between the root markers are then removed and the root mounts fresh
/// before the end marker. Without a root marker, the root mounts fresh at the end of <c>host</c>, after its children; without the end
/// marker, the nodes after the root marker are removed first. Ids issued by the fresh mount start again under
/// <c>options.IdPrefix</c>.
/// </para>
/// <para>
/// Under <c>options.Strict</c>, and for every other failure, the call disposes what it created and raises; the host and
/// the document's head are then as the server wrote them. A failure of <c>report</c> also propagates with the host and
/// the head as the server wrote them.
/// </para>
/// </remarks>
/// <exception cref="T:System.NotSupportedException"><c>mode</c> is <c>QueuedCommits</c>.</exception>
/// <exception cref="T:Ranvier.View.Foundation.TemplateDom.HydrationMismatchException">
/// <c>options.Strict</c> is set, and the host has no root marker or the server HTML differs from the template's structure.
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
    let document = DocumentHead.documentOf host
    let served = DocumentHead.keep document

    try
        let mounted =
            adoptHost served mode graph parentOwner report options host definition props

        DocumentHead.removeUnadopted document
        mounted
    with _ ->
        served ()
        reraise ()

/// <summary>Hydrates as <c>hydrateWith SynchronousCommits</c> does.</summary>
let hydrate graph parentOwner report options host (definition: Component<'props>) (props: 'props) : Mounted =
    hydrateWith SynchronousCommits graph parentOwner report options host definition props
