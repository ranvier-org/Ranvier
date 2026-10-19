namespace Ranvier.View.Foundation

open System
open Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation.Representation

#if RANVIER_VIEW_COUNTERS
/// <summary>Counts of placement work, defined only in the DOM test build.</summary>
module internal PlacementCounters =
    /// <summary>Hooks currently held by unplaced containers, across every container.</summary>
    let held = ref 0
#endif

/// <summary>
/// The hooks of instances whose nodes are not yet placed in their mount's host. A container is placed once its own
/// <c>Place</c> has run and its parent is placed; hooks added before then are held. The held hooks run in the order
/// added, followed by the hooks that child containers released into the container while it was unplaced, in the order
/// released: an instance's hooks run before its regions' rows.
/// </summary>
/// <remarks>
/// A hook runs as an untracked core effect created under its instance's owner: a batch or an enclosing computation body
/// defers it.
/// </remarks>
[<Sealed; AllowNullLiteral>]
type internal Placement(parent: Placement) =
    let mutable laidOut = false
    let mutable discarded = false
    // The container's own hooks; null until the first is held.
    let mutable held: ResizeArray<Owner * (unit -> unit)> = null
    // The hooks released by child containers, run after `held`; null until the first is held.
    let mutable forwarded: ResizeArray<Owner * (unit -> unit)> = null
    let mutable failure: exn = null

#if RANVIER_VIEW_COUNTERS
    let count (hooks: ResizeArray<Owner * (unit -> unit)>) =
        if isNull hooks then 0 else hooks.Count
#endif

    /// <summary>True while a root mount's first placement runs inside <c>mountWith</c>.</summary>
    member val Placing = false with get, set

    member _.IsPlaced: bool = laidOut && (isNull parent || parent.IsPlaced)

    /// <summary>The container of the root mount.</summary>
    member this.Root: Placement = if isNull parent then this else parent.Root

    /// <summary>Records the first hook failure of the root's first placement.</summary>
    member _.Hold(error: exn) =
        if isNull failure then
            failure <- error

    /// <summary>Raises the failure recorded by <c>Hold</c>, once.</summary>
    member _.RaiseFailure() =
        if not (isNull failure) then
            let error = failure
            failure <- null
            raise error

    // Runs the hook when the container is placed, holds it while the container is not laid out (after the own hooks when
    // `released` is set), and otherwise releases it to the parent.
    member private this.Enqueue(owner: Owner, hook: unit -> unit, released: bool) =
        if discarded then
            ()
        elif not laidOut then
            if released then
                if isNull forwarded then
                    forwarded <- ResizeArray ()

                forwarded.Add ((owner, hook))
            else
                if isNull held then
                    held <- ResizeArray ()

                held.Add ((owner, hook))
#if RANVIER_VIEW_COUNTERS
            PlacementCounters.held.Value <- PlacementCounters.held.Value + 1
#endif
        elif this.IsPlaced then
            if not owner.IsDisposed then
                Api.runWithOwner owner (fun () -> Api.createEffect (fun () -> Api.untrack hook))
        else
            parent.Enqueue (owner, hook, true)

    /// <summary>Runs <c>hook</c> under <c>owner</c> once the container is placed: at once when it is placed, otherwise after its <c>Place</c>.</summary>
    member this.Add(owner: Owner, hook: unit -> unit) =
        this.Enqueue (owner, hook, false)

    /// <summary>
    /// Marks the container's nodes laid out, then starts its own held hooks followed by the hooks released into it. While
    /// the parent is unplaced, the hooks are released to the parent instead, which runs them after its own.
    /// </summary>
    member this.Place() =
        if not laidOut && not discarded then
            laidOut <- true
            let own = held
            let rows = forwarded
            held <- null
            forwarded <- null
#if RANVIER_VIEW_COUNTERS
            PlacementCounters.held.Value <-
                PlacementCounters.held.Value
                - count own
                - count rows
#endif

            if not (isNull own) then
                for owner, hook in own do
                    this.Enqueue (owner, hook, false)

            if not (isNull rows) then
                for owner, hook in rows do
                    this.Enqueue (owner, hook, false)

    /// <summary>Drops the held hooks and every later one.</summary>
    member _.Discard() =
        discarded <- true
#if RANVIER_VIEW_COUNTERS
        PlacementCounters.held.Value <-
            PlacementCounters.held.Value
            - count held
            - count forwarded
#endif
        held <- null
        forwarded <- null

/// <summary>
/// The passes of a list or arm region under <c>parent</c>. A pass runs from <c>Begin</c> to <c>Place</c>; its placement
/// container is allocated by the pass's first request and shared by every later request of the pass.
/// </summary>
[<Sealed; AllowNullLiteral>]
type internal PlacementPass(parent: Placement) =
    let mutable current: Placement = null
    let mutable placed = false

    /// <summary>Starts a pass: the next request allocates a new container.</summary>
    member _.Begin() =
        current <- null
        placed <- false

    /// <summary>
    /// The container of the current pass. A request after the pass's <c>Place</c> and before the next <c>Begin</c> raises
    /// "A pass placement container was requested after its Place".
    /// </summary>
    member _.Container: Placement =
        if placed then
            raise (InvalidOperationException "A pass placement container was requested after its Place")

        if isNull current then
            current <- Placement parent

        current

    /// <summary>Ends the pass and places its container, when the pass requested one.</summary>
    member _.Place() =
        placed <- true

        if not (isNull current) then
            current.Place ()

/// <summary>Element hooks registered with a placement container.</summary>
module internal PlacedHook =
    [<Emit("$0.parentNode")>]
    let private parentOf (node: Node) : Node = jsNative

    /// <summary>Whether the node is a root of its row or mount: detached, or a child of a document fragment.</summary>
    [<Emit("($0.parentNode == null || $0.parentNode.nodeType === 11)")>]
    let private atRoot (node: Node) : bool = jsNative

    /// <summary>
    /// Adds <c>hook</c> for <c>node</c> to <c>container</c>; its returned cleanup runs when <c>owner</c> is disposed. A
    /// failure during the root's first placement, outside every boundary, unwinds <c>mountWith</c> after the placement's
    /// remaining hooks run; every other failure goes to <c>scope</c>. After the cleanup of a lent or widget hook on an
    /// element that is not a row or mount root, a parent different from the one the hook ran under raises "An integration
    /// moved its element", also when the cleanup fails.
    /// </summary>
    let register (container: Placement) (scope: ViewScope) (owner: Owner) (kind: PlacedKind) (node: Node) (hook: unit -> (unit -> unit)) =
        let checksParent = kind <> PlacedKind.Mounted && not (atRoot node)

        container.Add (
            owner,
            fun () ->
                let parent = parentOf node
                let mutable failed = false

                let cleanup =
                    try
                        hook ()
                    with error ->
                        failed <- true
                        let root = container.Root

                        if not scope.Tracks && root.Placing then
                            root.Hold error
                        else
                            scope.FailAll [ error ]

                        Unchecked.defaultof<_>

                if not failed then
                    // The check runs after the cleanup, and also when the cleanup fails.
                    if checksParent then
                        Api.onCleanup (fun () ->
                            if not (obj.ReferenceEquals (parentOf node, parent)) then
                                raise (InvalidOperationException "An integration moved its element"))

                    if not (isNull (box cleanup)) then
                        Api.onCleanup cleanup
        )
