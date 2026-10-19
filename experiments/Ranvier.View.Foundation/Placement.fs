namespace Ranvier.View.Foundation

open Ranvier
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
/// <c>Place</c> has run and its parent is placed; hooks added before then are held, and every hook runs in the order
/// added.
/// </summary>
/// <remarks>
/// A hook runs as an untracked core effect created under its instance's owner: a batch or an enclosing computation body
/// defers it.
/// </remarks>
[<Sealed; AllowNullLiteral>]
type internal Placement(parent: Placement) =
    let mutable laidOut = false
    let mutable discarded = false
    // Null until the first hook is held.
    let mutable held: ResizeArray<Owner * (unit -> unit)> = null
    let mutable failure: exn = null

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

    /// <summary>Runs <c>hook</c> under <c>owner</c> once the container is placed: at once when it is placed, otherwise after its <c>Place</c>.</summary>
    member this.Add(owner: Owner, hook: unit -> unit) =
        if discarded then
            ()
        elif not laidOut then
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
            parent.Add (owner, hook)

    /// <summary>Marks the container's nodes laid out, then starts each held hook, or passes it to the parent while the parent is unplaced.</summary>
    member this.Place() =
        if not laidOut && not discarded then
            laidOut <- true

            if not (isNull held) then
                let hooks = held
                held <- null
#if RANVIER_VIEW_COUNTERS
                PlacementCounters.held.Value <- PlacementCounters.held.Value - hooks.Count
#endif

                for owner, hook in hooks do
                    this.Add (owner, hook)

    /// <summary>Drops the held hooks and every later one.</summary>
    member _.Discard() =
        discarded <- true
#if RANVIER_VIEW_COUNTERS
        if not (isNull held) then
            PlacementCounters.held.Value <- PlacementCounters.held.Value - held.Count
#endif
        held <- null

/// <summary>Element hooks registered with a placement container.</summary>
module internal PlacedHook =
    /// <summary>
    /// Adds <c>hook</c> for <c>node</c> to <c>container</c>; its returned cleanup runs when <c>owner</c> is disposed. A
    /// failure during the root's first placement, outside every boundary, unwinds <c>mountWith</c>; every other failure
    /// goes to <c>scope</c>.
    /// </summary>
    let register (container: Placement) (scope: ViewScope) (owner: Owner) (kind: PlacedKind) (node: Node) (hook: unit -> (unit -> unit)) =
        container.Add (
            owner,
            fun () ->
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

                if not failed && not (isNull (box cleanup)) then
                    Api.onCleanup cleanup
        )
