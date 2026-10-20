/// <summary>Value checks of the bindings and of <c>Reactive</c> under jsdom, on a clock advanced by the test.</summary>
module AnimeChecks

open Fable.Core
open Fable.Core.TS.Dom
open global.Ranvier
open Ranvier.Anime

[<Global>]
let private document: Document = jsNative

[<Emit("$0.style.getPropertyValue($1)")>]
let private styleOf (element: Element) (property: string) : string = jsNative

[<Emit("Promise.resolve()")>]
let private resolved () : JS.Promise<unit> = jsNative

/// <summary>Advances the mocked <c>Date</c> by the given milliseconds.</summary>
type Clock = float -> unit

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

let private element () : Element =
    let element = document.createElement "div"
    document.body.appendChild element |> ignore
    element

let private advance (clock: Clock) (ms: float) =
    clock ms
    Testing.step ()

let private synchronous (check: Clock -> unit) : Clock -> JS.Promise<unit> =
    fun clock ->
        check clock
        resolved ()

/// <summary>Hands anime's engine to the test clock; called once, after <c>animejs</c> is imported.</summary>
let prepare () =
    Testing.manual ()

let private linearSamples (clock: Clock) =
    let el = element ()

    Anime.animate (el, Anim (opacity = Value.fromTo (0, 1), duration = 1000, ease = Ease.linear))
    |> ignore

    let samples = ResizeArray ()

    for step in 0..4 do
        advance clock (if step = 0 then 0. else 250.)
        samples.Add (styleOf el "opacity")

    equal [ "0"; "0.262"; "0.512"; "0.762"; "1" ] (List.ofSeq samples)

let private transformAndCallbacks (clock: Clock) =
    let el = element ()
    let log = ResizeArray ()

    Anime.animate (
        el,
        Anim (
            x = 100,
            rotate = 90,
            duration = 1000,
            ease = Ease.linear,
            onBegin = (fun _ -> log.Add "begin"),
            onComplete = (fun _ -> log.Add "complete")
        )
    )
    |> ignore

    advance clock 0.
    advance clock 500.
    equal "translateX(51.2px) rotate(46.08deg)" (styleOf el "transform")
    advance clock 600.
    equal "translateX(100px) rotate(90deg)" (styleOf el "transform")
    equal [ "begin"; "complete" ] (List.ofSeq log)

let private customProperty (clock: Clock) =
    let el = element ()

    let animation =
        Anime.animate (el, Anim(duration = 100, ease = Ease.linear).With("--hue", 120))

    animation.complete ()
    equal "120" (styleOf el "--hue")

let private staggered (clock: Clock) =
    let els = [| element (); element (); element () |]

    Anime.animate (els, Anim (opacity = Value.fromTo (0, 1), duration = 100, delay = Value.stagger 100, ease = Ease.linear))
    |> ignore

    advance clock 0.
    advance clock 150.
    equal [| "1"; "0.62"; "0" |] (els |> Array.map (fun el -> styleOf el "opacity"))

let private timelineSeek (clock: Clock) =
    let el = element ()
    let calls = ResizeArray ()

    let timeline =
        Anime
            .timeline(TimelineParams (autoplay = false))
            .add(el, Anim (x = 100, duration = 100, ease = Ease.linear))
            .add(el, Anim (y = 50, duration = 100, ease = Ease.linear), At.withPrevious)
            .call((fun _ -> calls.Add "end"), At.ms 200)
            .label("start", At.ms 0)

    equal 200. timeline.duration
    timeline.progress <- 0.75
    equal "translate(100px,25px)" (styleOf el "transform")
    timeline.progress <- 1.
    equal [ "end" ] (List.ofSeq calls)

let private utilities (clock: Clock) =
    let el = element ()

    Utils.set (el, Anim (x = 5, opacity = 0.5))
    |> ignore

    equal "translateX(5px)" (styleOf el "transform")
    equal 5. (Utils.getNumber (el, "x"))
    equal "0.5" (Utils.get (el, "opacity"))
    equal 3. (Utils.clamp (7., 0., 3.))
    equal 50. (Utils.mapRange (0.5, 0., 1., 0., 100.))

let private animatable (clock: Clock) =
    let el = element ()

    let properties =
        Anime.animatable (el, AnimatableParams (x = 1000, ease = Ease.linear))

    properties.set ("x", 200.)
    advance clock 0.
    advance clock 500.
    equal 102.4 (properties.get "x")
    advance clock 600.
    equal "translateX(200px)" (styleOf el "transform")

let private completion (clock: Clock) =
    async {
        let el = element ()
        let animation = Anime.animate (el, Anim (x = 1, duration = 100))
        let completed = Anime.completion animation
        animation.complete ()
        do! Async.AwaitPromise completed
        equal true animation.completed
    }
    |> Async.StartAsPromise

let private ownReverts (clock: Clock) =
    use graph = new Graph ()
    let el = element ()

    let owner =
        graph.Run (fun () ->
            Api.createRoot (fun owner ->
                Reactive.own (Anime.animate (el, Anim (opacity = 0.2, duration = 500, ease = Ease.linear)))
                |> ignore

                owner))

    advance clock 0.
    advance clock 250.
    equal "0.5808" (styleOf el "opacity")
    graph.Run (fun () -> owner.Dispose ())
    equal "" (styleOf el "opacity")

let private trackPlayback (clock: Clock) =
    use graph = new Graph ()
    let el = element ()
    let mutable completions = 0

    let playback =
        graph.Run (fun () ->
            Api.createRoot (fun _ ->
                Anime.animate (
                    el,
                    Anim (
                        opacity = Value.fromTo (0, 1),
                        duration = 1000,
                        ease = Ease.linear,
                        onComplete = (fun _ -> completions <- completions + 1)
                    )
                )
                |> Reactive.track))

    let read () =
        graph.Run (fun () -> playback.Progress.Value, playback.Running.Value, playback.Completed.Value)

    equal (0., true, false) (read ())
    advance clock 0.
    advance clock 500.
    equal (0.512, true, false) (read ())
    advance clock 600.
    equal (1., false, true) (read ())
    equal 1 completions

let private scrubSeeks (clock: Clock) =
    use graph = new Graph ()
    let el = element ()

    let progress =
        graph.Run (fun () ->
            let progress = Api.createSignal 0.

            Anime.animate (el, Anim (opacity = Value.fromTo (0, 1), duration = 1000, ease = Ease.linear, autoplay = false))
            |> Reactive.scrub (fun () -> progress.Value)

            progress)

    equal "0" (styleOf el "opacity")
    graph.Run (fun () -> progress.Value <- 0.5)
    equal "0.5" (styleOf el "opacity")
    graph.Run (fun () -> progress.Value <- 1.)
    equal "1" (styleOf el "opacity")

let private driveAnimates (clock: Clock) =
    use graph = new Graph ()
    let el = element ()

    let target =
        graph.Run (fun () ->
            let target = Api.createSignal 0.

            let properties =
                Anime.animatable (el, AnimatableParams (opacity = 1000, ease = Ease.linear))

            Reactive.drive (fun () -> target.Value) properties "opacity"
            target)

    advance clock 0.
    advance clock 1100.
    equal "0" (styleOf el "opacity")
    graph.Run (fun () -> target.Value <- 1.)
    advance clock 0.
    advance clock 500.
    equal "0.512" (styleOf el "opacity")
    advance clock 600.
    equal "1" (styleOf el "opacity")

[<Emit("globalThis.window.matchMedia = query => { const media = { matches: $0, listeners: [], addEventListener: (_, f) => media.listeners.push(f) }; globalThis.__media = media; return media; }")>]
let private stubMatchMedia (matches: bool) : unit = jsNative

[<Emit("(() => { const media = globalThis.__media; delete globalThis.__media; delete globalThis.window.matchMedia; media.listeners.forEach(f => f({ matches: $0 })); })()")>]
let private changeAndUnstub (matches: bool) : unit = jsNative

let private policyDefaults (clock: Clock) =
    use graph = new Graph ()

    graph.Run (fun () ->
        let policy = Reactive.policy ()
        equal Reactive.Full policy.Value
        equal true (obj.ReferenceEquals (policy, Reactive.policy ()))
        policy.Value <- Reactive.Reduced
        equal Reactive.Reduced (Reactive.policy ()).Value)

let private policyFollowsMedia (clock: Clock) =
    use graph = new Graph ()
    stubMatchMedia true

    graph.Run (fun () ->
        let policy = Reactive.policy ()
        equal Reactive.Reduced policy.Value
        changeAndUnstub false
        equal Reactive.Full policy.Value)

/// <summary>Each check by name; a check receives the test clock.</summary>
let cases: (string * (Clock -> JS.Promise<unit>)) array =
    [|
        "a linear opacity tween reads exact values at each step", synchronous linearSamples
        "transforms compose in parameter order and callbacks fire", synchronous transformAndCallbacks
        "With sets a CSS variable", synchronous customProperty
        "a stagger delays each target", synchronous staggered
        "a timeline places, positions and seeks its children", synchronous timelineSeek
        "utilities set, read and compute values", synchronous utilities
        "an animatable property animates to each value set", synchronous animatable
        "completion resolves once the animation completes", completion
        "own reverts the animation when its owner is disposed", synchronous ownReverts
        "track publishes progress, running and completion as memos", synchronous trackPlayback
        "scrub seeks to the signal's progress", synchronous scrubSeeks
        "drive animates the property to the signal's value", synchronous driveAnimates
        "policy defaults to Full without matchMedia and is shared and writable", synchronous policyDefaults
        "policy seeds from matchMedia and follows its changes", synchronous policyFollowsMedia
    |]
