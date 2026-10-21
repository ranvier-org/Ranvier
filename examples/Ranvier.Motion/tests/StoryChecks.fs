/// <summary>The story page hydrated from its own server render under jsdom, with its scroll observers driven by hand.</summary>
module StoryChecks

open Fable.Core
open global.Ranvier
open Ranvier.Anime
open Ranvier.View.Foundation
open Harness

[<Emit("globalThis.motionObservers.filter(o => o._params.target === $0).at(-1)")>]
let private observerOf (element: obj) : ScrollObserver = jsNative

[<Emit("Object.defineProperty($0, 'progress', { value: $1, configurable: true })")>]
let private fixProgress (observer: ScrollObserver) (progress: float) : unit = jsNative

[<Emit("$0.onUpdate($0)")>]
let private fireUpdate (observer: ScrollObserver) : unit = jsNative

[<Emit("$0.onEnter($0)")>]
let private fireEnter (observer: ScrollObserver) : unit = jsNative

[<Emit("(() => { const r = $0.ownerDocument.createRange(); r.selectNodeContents($0); $0.replaceChildren(r.createContextualFragment($1)); })()")>]
let private loadServed (target: obj) (html: string) : unit = jsNative

let private served () =
    let target = host ()
    loadServed target (Server.render Server.Options.defaults Story.view ignore)
    target

let private scrollTo (graph: Graph) (mounted: StoryClient.Mounted) (section: obj) (progress: float) =
    let observer = observerOf section
    fixProgress observer progress
    fireUpdate observer
    mounted.Mount.Flush ()

let private hydratesStrictly (clock: Clock) =
    use graph = new Graph ()
    let target = served ()
    let eyebrow = find target ".eyebrow"
    let mounted = StoryClient.attach graph target
    advance clock 0.
    run clock 16. 200.
    let adopted = obj.ReferenceEquals (eyebrow, find target ".eyebrow")
    let split = countOf target ".headline [data-char]"

    let result =
        mounted.Hydrated, List.ofSeq mounted.Errors, adopted, styleOf eyebrow "opacity", split

    mounted.Mount.Dispose ()
    equal (true, [], true, "", 22) result

let private mountsWhenNotServed (clock: Clock) =
    use graph = new Graph ()
    let target = host ()
    let mounted = StoryClient.attach graph target
    advance clock 0.
    run clock 16. 100.
    let rising = styleOf (find target ".eyebrow") "opacity"
    mounted.Mount.Dispose ()
    equal (false, true) (mounted.Hydrated, rising <> "" && rising <> "1")

let private scrollWritesProgress (clock: Clock) =
    use graph = new Graph ()
    let target = served ()
    let mounted = StoryClient.attach graph target
    let section = find target "section[data-chapter=\"1\"]"
    let observer = observerOf section
    fixProgress observer 0.25
    fireUpdate observer
    fireEnter observer
    mounted.Mount.Flush ()
    let item = find target ".rail-item:nth-of-type(2)"

    let rail =
        textOf (find item ".rail-value"), attributeOf item "style", attributeOf item "class"

    let signals =
        graph.Run (fun () -> mounted.Story.Chapters[1].Progress.Value, mounted.Story.Chapters[1].Seen.Value)

    let others = textsOf target ".rail-value"
    mounted.Mount.Dispose ()
    equal (("25%", "--p:0.250", "rail-item seen"), (0.25, true), [| "0%"; "25%"; "0%" |]) (rail, signals, others)

let private extrasAppear (clock: Clock) =
    use graph = new Graph ()
    let target = served ()
    let mounted = StoryClient.attach graph target
    let gallery = find target ".gallery"
    scrollTo graph mounted gallery 0.4
    let before = countOf target ".extra", textOf (find target ".gallery-note")
    scrollTo graph mounted gallery 0.6
    let tile = find target ".extra"
    advance clock 0.
    run clock 16. 160.
    let popping = styleOf tile "opacity"
    run clock 16. 600.
    let popped = styleOf tile "opacity", textsOf target ".extra"
    scrollTo graph mounted gallery 0.3
    let after = countOf target ".extra"
    mounted.Mount.Dispose ()

    equal ((0, "Gallery 40%"), true, ("1", Story.extras), 0) (before, popping <> "" && popping <> "0" && popping <> "1", popped, after)

let cases: Case list =
    [
        case "the served story hydrates strictly: nodes adopted, appear skipped, the headline split" hydratesStrictly
        case "without a server render the story mounts, and appear plays" mountsWhenNotServed
        case "a chapter's scroll observer writes its progress and in-view state to the rail" scrollWritesProgress
        case "past half the gallery the client-created tiles appear, and leave before it" extrasAppear
    ]
