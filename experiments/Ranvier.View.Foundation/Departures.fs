namespace Ranvier.View.Foundation

open System
open Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation.Representation

/// <summary>A retired row's root element held in place while its exit runs.</summary>
[<Sealed; AllowNullLiteral>]
type internal Departure(node: Node) =
    member _.Node = node

    /// <summary>Ends the exit at once: cancels it, then removes the node. Does nothing once the exit has ended.</summary>
    member val End: unit -> unit = ignore with get, set

/// <summary>A reflow hook's measurements and whether its element has an open measurement in the current flush.</summary>
[<Sealed; AllowNullLiteral>]
type internal ReflowEntry(reflow: Reflow) =
    member _.Reflow = reflow
    /// <summary>Set by <c>Before</c>, cleared when the posted <c>After</c> runs.</summary>
    member val Open = false with get, set
    /// <summary>The number of <c>After</c> posts; only the latest post of an open measurement runs <c>After</c>.</summary>
    member val Posts = 0 with get, set

/// <summary>
/// The exit and reflow hooks of placed elements, and the retired row roots held in place while their exits run. Until
/// the first registration of each kind the mount paths test only <c>enabled</c> or <c>reflowEnabled</c>.
/// </summary>
module internal Departures =
    [<Emit("new WeakMap()")>]
    let private weakMap () : obj = jsNative

    [<Emit("new WeakSet()")>]
    let private weakSet () : obj = jsNative

    [<Emit("$0.get($1)")>]
    let private lookup (table: obj) (node: Node) : 'T = jsNative

    [<Emit("$0.set($1, $2)")>]
    let private store (table: obj) (node: Node) (value: 'T) : unit = jsNative

    [<Emit("$0.has($1)")>]
    let private holds (table: obj) (node: Node) : bool = jsNative

    [<Emit("$0.delete($1)")>]
    let private forget (table: obj) (node: Node) : unit = jsNative

    [<Emit("$0.add($1)")>]
    let private mark (table: obj) (node: Node) : unit = jsNative

    [<Emit("$0 == null")>]
    let private isNullish (value: 'T) : bool = jsNative

    [<Emit("$0.nextSibling")>]
    let private nextOrNull (node: Node) : Node = jsNative

    [<Emit("$0.parentNode != null && $0.parentNode.removeChild($0)")>]
    let private detach (node: Node) : unit = jsNative

    /// <summary>Whether the node is detached or a child of a document fragment.</summary>
    [<Emit("($0.parentNode == null || $0.parentNode.nodeType === 11)")>]
    let private atRoot (node: Node) : bool = jsNative

    [<Emit("setTimeout($0, $1)")>]
    let private startTimer (action: unit -> unit) (milliseconds: int) : obj = jsNative

    [<Emit("clearTimeout($0)")>]
    let private stopTimer (timer: obj) : unit = jsNative

    /// <summary>The milliseconds after which a held exit is finished.</summary>
    let safetyTimeout = 10000

    // Each registered exit, by element: given `finish`, starts the exit and returns its cancel.
    let private exits = weakMap ()
    let private leaving = weakSet ()
    let private reflows = weakMap ()

    /// <summary>Set by the first exit registration.</summary>
    let mutable enabled = false

    /// <summary>Set by the first reflow registration.</summary>
    let mutable reflowEnabled = false

    /// <summary>Registers <c>exit</c> for <c>node</c>, replacing an earlier registration.</summary>
    let register (node: Node) (exit: (unit -> unit) -> (unit -> unit)) =
        enabled <- true
        store exits node exit

    /// <summary>Registers <c>reflow</c> for <c>node</c> and returns the function that withdraws this registration.</summary>
    let registerReflow (node: Node) (reflow: Reflow) : unit -> unit =
        reflowEnabled <- true
        let entry = ReflowEntry reflow
        store reflows node entry

        fun () ->
            if obj.ReferenceEquals (lookup reflows node, entry) then
                forget reflows node

    /// <summary>Whether the node is a retired root held in place by its exit.</summary>
    let isLeaving (node: Node) : bool =
        holds leaving node

    /// <summary>The node's next sibling that is not leaving, or <c>null</c>.</summary>
    let nextLive (node: Node) : Node =
        let mutable next = nextOrNull node

        while not (isNullish next) && holds leaving next do
            next <- nextOrNull next

        next

    /// <summary>Whether a sibling from <c>first</c> to <c>last</c> has a registered exit.</summary>
    let anyExit (first: Node) (last: Node) : bool =
        let mutable cursor = first
        let mutable found = false
        let mutable going = true

        while going && not found do
            found <- holds exits cursor

            if
                obj.ReferenceEquals (cursor, last)
                || isNullish cursor
            then
                going <- false
            else
                cursor <- nextOrNull cursor

        found

    /// <summary>Whether a node strictly between <c>first</c> and <c>last</c> is leaving.</summary>
    let anyLeaving (first: Node) (last: Node) : bool =
        let mutable cursor = nextOrNull first
        let mutable found = false

        while not found
              && not (isNullish cursor)
              && not (obj.ReferenceEquals (cursor, last)) do
            found <- holds leaving cursor
            cursor <- nextOrNull cursor

        found

    let private drop (held: ResizeArray<Departure>) (departure: Departure) =
        let mutable index = held.Count - 1

        while index >= 0 do
            if obj.ReferenceEquals (held[index], departure) then
                held.RemoveAt index
                index <- -1
            else
                index <- index - 1

    /// <summary>
    /// Retires a row root. Returns false, and withdraws the node's exit, when the node has no exit or the removal is
    /// <c>forced</c>. Otherwise marks the node leaving, adds it to <c>held</c> and starts its exit with a <c>finish</c>
    /// that removes the node and drops it from <c>held</c>; <c>safetyTimeout</c> milliseconds later <c>finish</c> runs
    /// unless it ran earlier. A failure of the exit finishes it and is raised.
    /// </summary>
    let retire (node: Node) (forced: bool) (held: ResizeArray<Departure>) : bool =
        let exit: (unit -> unit) -> (unit -> unit) = lookup exits node

        if isNullish exit then
            false
        else
            forget exits node

            if forced then
                false
            else
                mark leaving node
                let departure = Departure node
                let mutable ended = false
                let mutable timer: obj = null
                let mutable cancel: unit -> unit = Unchecked.defaultof<_>

                let leave () =
                    ended <- true

                    if not (isNull timer) then
                        stopTimer timer

                    forget leaving node
                    drop held departure
                    detach node

                let finish () =
                    if not ended then
                        leave ()

                departure.End <-
                    fun () ->
                        if not ended then
                            let running = cancel
                            ended <- true

                            try
                                if not (isNullish running) then
                                    running ()
                            finally
                                leave ()

                held.Add departure
                timer <- startTimer finish safetyTimeout

                try
                    cancel <- exit finish
                with _ ->
                    finish ()
                    reraise ()

                true

    /// <summary>Runs the <c>Before</c> of the reflow registered on <c>parent</c> unless its measurement is open.</summary>
    let before (parent: Node) =
        let entry: ReflowEntry = lookup reflows parent

        if not (isNullish entry) && not entry.Open then
            entry.Open <- true
            entry.Reflow.Before ()

    /// <summary>
    /// Posts the <c>After</c> of the reflow registered on <c>parent</c> when its measurement is open. Of the posts made
    /// while the measurement is open, the one posted last closes it and runs <c>After</c>; the earlier ones do nothing.
    /// </summary>
    let after (parent: Node) (post: (unit -> unit) -> unit) =
        let entry: ReflowEntry = lookup reflows parent

        if not (isNullish entry) && entry.Open then
            let mutable posted = entry.Posts + 1
            entry.Posts <- posted

            post (fun () ->
                if entry.Open && entry.Posts = posted then
                    entry.Open <- false
                    entry.Reflow.After ())

    /// <summary>
    /// Registers a motion, exit or reflow hook for <c>node</c>. <c>invoke</c> calls the hook with its argument: the
    /// element's <c>MotionContext</c>, the exit's <c>finish</c>, or null. A motion or reflow hook runs once
    /// <c>container</c> is placed, as <c>PlacedHook.register</c> describes; an exit is registered at once. A Debug build
    /// raises "An exit hook requires a row or arm root element" for an exit on a node that is not a root.
    /// </summary>
    let place
        (container: Placement)
        (scope: ViewScope)
        (owner: Owner)
        (kind: PlacedKind)
        (node: Node)
        (root: bool)
        (hydrated: bool)
        (invoke: obj -> obj)
        =
        match kind with
        | PlacedKind.Exit ->
#if DEBUG
            if not root && not (atRoot node) then
                raise (InvalidOperationException "An exit hook requires a row or arm root element")
#endif
            register node (fun finish -> unbox (invoke (box finish)))
        | PlacedKind.Reflow -> PlacedHook.register container scope owner kind node root (fun () -> registerReflow node (unbox (invoke null)))
        | _ ->
            let context =
                box
                    {
                        Hydrated = hydrated
                        Inserted = container.Inserted
                    }

            PlacedHook.register container scope owner kind node root (fun () -> unbox (invoke context))
