namespace Ranvier.View.Anime

open System
open Fable.Core
open global.Ranvier
open Ranvier.Anime
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar

/// <summary>
/// Template parts that animate elements with anime.js: <c>on</c>, <c>appear</c>, <c>enter</c>, <c>exit</c>,
/// <c>presence</c> and its presets, and <c>flip</c>.
/// </summary>
/// <remarks>
/// Every part creates its anime objects once the element is placed and reverts them when the instance is disposed. On an
/// element whose exit follows the disposal, the part pauses its animations instead, so the exit starts from the current
/// values; a paused animation leaves anime's engine on its next tick. The server omits every part.
/// Under <c>Reactive.policy</c> <c>Reduced</c>, enter and flip do not play and an exit finishes at once.
/// </remarks>
[<RequireQualifiedAccess>]
module Motion =
#if FABLE_COMPILER
    [<Emit("new WeakSet()")>]
    let private weakSet () : obj = jsNative

    // Elements whose Motion exit runs.
    let private departing = weakSet ()
#else
    let private departing: obj = null
#endif

    [<Emit("$0.add($1)")>]
    let private mark (set: obj) (element: obj) : unit = jsNative

    [<Emit("$0.delete($1)")>]
    let private forget (set: obj) (element: obj) : unit = jsNative

    [<Emit("(r => typeof r.pause === 'function' && r.pause())($0)")>]
    let private pauseIfPlayable (revertible: obj) : unit = jsNative

    [<Emit("((t, k, f) => { const previous = t[k]; t[k] = self => { if (previous) previous(self); f(self); }; })($0, $1, $2)")>]
    let private chain (target: obj) (callback: string) (after: obj -> unit) : unit = jsNative

    [<Emit("Object.assign({}, $0, {[$1]: $2})")>]
    let private withKey (parameters: 'p) (key: string) (value: obj) : 'p = jsNative


    [<Emit("(e => { const top = e.offsetTop, left = e.offsetLeft, width = e.offsetWidth, height = e.offsetHeight, s = e.style; s.position = 'absolute'; s.top = top + 'px'; s.left = left + 'px'; s.width = width + 'px'; s.height = height + 'px'; s.margin = '0'; s.boxSizing = 'border-box'; })($0)")>]
    let private pinOutOfFlow (element: obj) : unit = jsNative

    [<Emit("Array.prototype.filter.call($0.children, c => !$1.has(c))")>]
    let private liveChildren (root: obj) (leaving: obj) : TS.Dom.Element array = jsNative

    [<Emit("$0.children = $1")>]
    let private measureChildren (layout: AutoLayout) (children: TS.Dom.Element array) : unit = jsNative

    [<Emit("$0.scrollHeight")>]
    let private contentHeight (element: obj) : float = jsNative

    [<Emit("$0.style.overflow = $1")>]
    let private overflow (element: obj) (value: string) : unit = jsNative

#if FABLE_COMPILER
    // The exit part, with the motion part's hook run first under the same placement; the cleanup runs the motion
    // cleanup, then the exit's.
    [<Emit("((e, m) => new e.constructor(e.tag, [e.fields[0], (i, n, f) => { const c = m.fields[1](i, n, f); const p = e.fields[1](i, n, f); return () => { try { c(); } finally { p(); } }; }]))($0, $1)")>]
    let private joined (exit: Part<'native, 'inst>) (motion: Part<'native, 'inst>) : Part<'native, 'inst> = jsNative
#else
    let private joined (exit: Part<'native, 'inst>) (_: Part<'native, 'inst>) : Part<'native, 'inst> = exit
#endif

    // The one call of each curried seam member: the parts pass delegates, which compile to plain functions.
    let private motionPart (hook: Func<'inst, 'native, MotionContext, unit -> unit>) : Part<'native, 'inst> =
        Hooks.motion (fun inst node context -> hook.Invoke (inst, node, context))

    let private exitPart (exit: Func<'inst, 'native, unit -> unit, unit -> unit>) : Part<'native, 'inst> =
        Hooks.exit (fun inst node finish -> exit.Invoke (inst, node, finish))

    let inline private targetsOf (node: 'native) : Targets =
        Erased.cast node

    let inline private elementOf (node: 'native) : TS.Dom.Element =
        Erased.cast node

    let private nothing () = ()

    let private reduced () =
        match Api.untrack (fun () -> (Reactive.policy ()).Value) with
        | Reactive.Reduced -> true
        | Reactive.Full -> false

    // The cleanup of a part's anime object: paused while the element's exit follows, reverted otherwise.
    let private settle (context: MotionContext) (revertible: IRevertible) =
        let release () =
            if context.ExitPending () then
                pauseIfPlayable revertible
            else
                revertible.revert ()

        release

    let private entering (play: 'inst -> TS.Dom.Element -> #ITickable) =
        Func<'inst, 'native, MotionContext, unit -> unit>(fun inst node context ->
            if context.Inserted && not (reduced ()) then
                settle context (play inst (elementOf node) :> IRevertible)
            else
                nothing)

    // Starts the exit, pinned out of flow when `pin`; the element leaves once the tickable completes, and at once under
    // Reduced. The returned cancel stops the tickable.
    let private departure (pin: bool) (play: 'inst -> TS.Dom.Element -> #ITickable) =
        Func<'inst, 'native, unit -> unit, unit -> unit>(fun inst node finish ->
            let element = box node

            let finished () =
                forget departing element
                finish ()

            if reduced () then
                finished ()
                nothing
            else
                mark departing element

                if pin then
                    pinOutOfFlow element

                let tickable = play inst (elementOf node) :> ITickable
                chain tickable "onComplete" (fun _ -> finished ())

                let cancel () =
                    forget departing element
                    tickable.cancel ()

                cancel)

    /// <summary>Plays on every placement, hydration included; the cleanup reverts the animation.</summary>
    let on (play: 'inst -> TS.Dom.Element -> #IRevertible) : Part<'native, 'inst> =
        motionPart (Func<_, _, _, _>(fun inst node context -> settle context (play inst (elementOf node) :> IRevertible)))

    /// <summary>Plays on every placement of an element created on the client; an element adopted from server HTML stays as served.</summary>
    let appear (play: 'inst -> TS.Dom.Element -> #IRevertible) : Part<'native, 'inst> =
        motionPart (
            Func<_, _, _, _>(fun inst node context ->
                if context.Hydrated then
                    nothing
                else
                    settle context (play inst (elementOf node) :> IRevertible))
        )

    /// <summary>
    /// Plays when the element's row or arm is created by a region pass after the region's first; rows of the first pass,
    /// hydrated rows and rows nested inside an entering row stay still.
    /// </summary>
    let enter (play: 'inst -> TS.Dom.Element -> #ITickable) : Part<'native, 'inst> =
        motionPart (entering play)

    /// <summary>
    /// On a row or arm root element: plays when the row is retired, and the element leaves when the tickable completes.
    /// </summary>
    /// <remarks>A forced removal, as when the mount is disposed, cancels the tickable and removes the element at once.</remarks>
    let exit (play: 'inst -> TS.Dom.Element -> #ITickable) : Part<'native, 'inst> =
        exitPart (departure false play)

    /// <summary>
    /// On a row or arm root element: <c>enter</c> and <c>exit</c> together. The leaving element is pinned out of flow,
    /// absolute at its current box, so later rows take its place while it plays.
    /// </summary>
    /// <remarks>The element's offset parent is the containing block of the pinned box; position the list's container.</remarks>
    let presence (enter: 'inst -> TS.Dom.Element -> #ITickable) (exit: 'inst -> TS.Dom.Element -> #ITickable) : Part<'native, 'inst> =
        joined (exitPart (departure true exit)) (motionPart (entering enter))

    /// <summary>Presence that fades the element in over 240 ms and out over 180 ms.</summary>
    let fade<'native, 'inst> : Part<'native, 'inst> =
        presence
            (fun (_: 'inst) node -> Anime.animate (node, Anim (opacity = Value.fromTo (0, 1), duration = 240, ease = Ease.outQuad)))
            (fun _ node -> Anime.animate (node, Anim (opacity = 0, duration = 180, ease = Ease.inQuad)))

    /// <summary>Presence that fades the element in while rising 12 px, and out while rising a further 12 px.</summary>
    let rise<'native, 'inst> : Part<'native, 'inst> =
        presence
            (fun (_: 'inst) node ->
                Anime.animate (node, Anim (y = Value.fromTo (12, 0), opacity = Value.fromTo (0, 1), duration = 240, ease = Ease.outQuad)))
            (fun _ node -> Anime.animate (node, Anim (y = -12, opacity = 0, duration = 180, ease = Ease.inQuad)))

    /// <summary>Presence that fades the element in from 92 % scale and out to it.</summary>
    let scale<'native, 'inst> : Part<'native, 'inst> =
        presence
            (fun (_: 'inst) node ->
                Anime.animate (node, Anim (scale = Value.fromTo (0.92, 1), opacity = Value.fromTo (0, 1), duration = 240, ease = Ease.outQuad)))
            (fun _ node -> Anime.animate (node, Anim (scale = 0.92, opacity = 0, duration = 180, ease = Ease.inQuad)))

    let private unfold (_: 'inst) (node: TS.Dom.Element) =
        let element = box node
        overflow element "hidden"

        Anime.animate (
            node,
            Anim (
                height = Value.fromTo (0, contentHeight element),
                duration = 240,
                ease = Ease.outQuad,
                onComplete =
                    fun animation ->
                        Utils.cleanInlineStyles animation
                        overflow element ""
            )
        )

    let private fold (_: 'inst) (node: TS.Dom.Element) =
        overflow (box node) "hidden"
        Anime.animate (node, Anim (height = 0, duration = 180, ease = Ease.inQuad))

    /// <summary>
    /// Presence that unfolds the element's height from 0 and folds it back to 0, in flow, with its overflow hidden while
    /// it plays.
    /// </summary>
    let collapse<'native, 'inst> : Part<'native, 'inst> =
        joined (exitPart (departure false fold)) (motionPart (entering unfold))

    /// <summary>
    /// FLIP for the rows of every region under the element: anime's <c>createLayout</c> over the element's element
    /// children whose Motion exit has not started, recorded before each flush's region passes and animated once their
    /// nodes and commits are in place.
    /// </summary>
    /// <remarks>
    /// <c>parameters</c> apply to every animation; <c>children</c> is replaced. The layout animates transforms through
    /// WAAPI, and its leaving rows through <c>leaveTo</c>, so a row with a Motion exit is left out of the measurement.
    /// </remarks>
    let flip (parameters: LayoutParams) : Part<'native, 'inst> =
        Hooks.reflow (fun _ node ->
            let root = box node
            let layout: AutoLayout ref = ref null
            let recorded = ref false

            Api.onCleanup (fun () ->
                if not (isNull layout.Value) then
                    layout.Value.revert ())

            {
                Before =
                    fun () ->
                        recorded.Value <- not (reduced ())

                        if recorded.Value then
                            if isNull layout.Value then
                                layout.Value <- Anime.layout (elementOf node, withKey parameters "children" (liveChildren root departing))
                            else
                                measureChildren layout.Value (liveChildren root departing)
                                layout.Value.record () |> ignore
                After =
                    fun () ->
                        if recorded.Value then
                            recorded.Value <- false
                            measureChildren layout.Value (liveChildren root departing)
                            layout.Value.animate () |> ignore
            })
