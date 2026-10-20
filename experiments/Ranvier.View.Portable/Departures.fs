/// <summary>
/// The exit and reflow hooks of placed elements, and the retired row roots held in place while their exits run. The mount
/// paths reach the registry only through <c>seam</c>, which the first registration fills: a bundle with no exit or reflow
/// hook holds the seam's flags alone.
/// </summary>
module internal Ranvier.View.Foundation.Departures

open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation.Representation

/// <summary>
/// The registry operations the mount paths call. The operations are null until <c>Enabled</c> (exits) or
/// <c>ReflowEnabled</c> (reflows) is set.
/// </summary>
[<AllowNullLiteral>]
type Seam =
    abstract Enabled: bool with get, set
    abstract ReflowEnabled: bool with get, set

    /// <summary>
    /// Retires a row root: true when the node is held, and its end action joins the held list; false when the node has no
    /// exit.
    /// </summary>
    abstract Retire: System.Func<Node, ResizeArray<unit -> unit>, bool> with get, set

    /// <summary>The node's next sibling that is not leaving, or null.</summary>
    abstract NextLive: System.Func<Node, Node> with get, set

    /// <summary>Whether a sibling from the first node to the second has a registered exit.</summary>
    abstract AnyExit: System.Func<Node, Node, bool> with get, set

    /// <summary>Whether a node strictly between the two nodes is leaving.</summary>
    abstract AnyLeaving: System.Func<Node, Node, bool> with get, set

    /// <summary>Runs the <c>Before</c> of the reflow registered on the node unless its measurement is open.</summary>
    abstract Before: System.Action<Node> with get, set

    /// <summary>
    /// Posts the <c>After</c> of the reflow registered on the node when its measurement is open. Of the posts made while
    /// the measurement is open, the one posted last closes it and runs <c>After</c>.
    /// </summary>
    abstract After: System.Action<Node, (unit -> unit) -> unit> with get, set

/// <summary>The seam, an empty object until the first registration; null on .NET, which mounts no DOM.</summary>
#if FABLE_COMPILER
let seam: Seam = Fable.Core.JsInterop.emitJsExpr () "{}"
#else
let seam: Seam = null
#endif

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

[<NoEquality; NoComparison>]
type private ReflowEntry =
    {
        Reflow: Reflow
        /// <summary>Set by <c>Before</c>, cleared when the latest posted <c>After</c> runs.</summary>
        mutable Open: bool
        /// <summary>The number of <c>After</c> posts.</summary>
        mutable Posts: int
    }

let private nextLive (node: Node) : Node =
    let mutable next = nextOrNull node

    while not (isNullish next) && holds leaving next do
        next <- nextOrNull next

    next

let private anyExit (first: Node) (last: Node) : bool =
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

let private anyLeaving (first: Node) (last: Node) : bool =
    let mutable cursor = nextOrNull first
    let mutable found = false

    while not found
          && not (isNullish cursor)
          && not (obj.ReferenceEquals (cursor, last)) do
        found <- holds leaving cursor
        cursor <- nextOrNull cursor

    found

let private drop (held: ResizeArray<unit -> unit>) (ending: unit -> unit) =
    let mutable index = held.Count - 1

    while index >= 0 do
        if obj.ReferenceEquals (held[index], ending) then
            held.RemoveAt index
            index <- -1
        else
            index <- index - 1

// Withdraws the node's exit, marks the node leaving, adds its end action to `held` and starts its exit with a `finish`
// that removes the node and drops the end action; `safetyTimeout` milliseconds later `finish` runs unless it ran earlier.
// The end action cancels the exit, then removes the node. A failure of the exit finishes it and is raised.
let private retire (node: Node) (held: ResizeArray<unit -> unit>) : bool =
    let exit: (unit -> unit) -> (unit -> unit) = lookup exits node

    if isNullish exit then
        false
    else
        forget exits node
        mark leaving node
        let mutable ended = false
        let mutable timer: obj = null
        let mutable cancel: unit -> unit = Unchecked.defaultof<_>
        let mutable ending: unit -> unit = Unchecked.defaultof<_>

        let leave () =
            ended <- true

            if not (isNull timer) then
                stopTimer timer

            forget leaving node
            drop held ending
            detach node

        let finish () =
            if not ended then
                leave ()

        ending <-
            fun () ->
                if not ended then
                    let running = cancel
                    ended <- true

                    try
                        if not (isNullish running) then
                            running ()
                    finally
                        leave ()

        held.Add ending
        timer <- startTimer finish safetyTimeout

        try
            cancel <- exit finish
        with _ ->
            finish ()
            reraise ()

        true

let private before (parent: Node) =
    let entry: ReflowEntry = lookup reflows parent

    if not (isNullish entry) && not entry.Open then
        entry.Open <- true
        entry.Reflow.Before ()

let private after (parent: Node) (post: (unit -> unit) -> unit) =
    let entry: ReflowEntry = lookup reflows parent

    if not (isNullish entry) && entry.Open then
        let mutable posted = entry.Posts + 1
        entry.Posts <- posted

        post (fun () ->
            if entry.Open && entry.Posts = posted then
                entry.Open <- false
                entry.Reflow.After ())

/// <summary>Registers <c>exit</c> for <c>node</c>, replacing an earlier registration, and enables exits.</summary>
let register (node: Node) (exit: (unit -> unit) -> (unit -> unit)) =
    if not seam.Enabled then
        seam.Retire <- System.Func<_, _, _> retire
        seam.NextLive <- System.Func<_, _> nextLive
        seam.AnyExit <- System.Func<_, _, _> anyExit
        seam.AnyLeaving <- System.Func<_, _, _> anyLeaving
        seam.Enabled <- true

    store exits node exit

/// <summary>
/// Registers <c>reflow</c> for <c>node</c>, replacing an earlier registration, enables reflows, and returns the function
/// that withdraws this registration.
/// </summary>
let registerReflow (node: Node) (reflow: Reflow) : unit -> unit =
    if not seam.ReflowEnabled then
        seam.Before <- System.Action<_> before
        seam.After <- System.Action<_, _> after
        seam.ReflowEnabled <- true

    let entry =
        {
            Reflow = reflow
            Open = false
            Posts = 0
        }

    store reflows node entry

    fun () ->
        if obj.ReferenceEquals (lookup reflows node, entry) then
            forget reflows node
