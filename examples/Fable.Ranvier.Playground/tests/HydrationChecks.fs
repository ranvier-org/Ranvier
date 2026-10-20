/// <summary>Hydration checks: jsdom through tests/hydration.test.mjs, Chromium through tests/hydration.spec.mjs.</summary>
module HydrationChecks

open System
open global.Ranvier
open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open CheckSupport
open ServerFixtures

/// <summary>The <c>id</c> attributes of the elements under <c>root</c>, in document order.</summary>
let private domIds (root: HTMLElement) : string list =
    let found = root.querySelectorAll "[id]"

    [
        for index in 0 .. int found.length - 1 -> (unbox<Element>(found.item (float index))).id
    ]

let private mount graph (target: HTMLElement) view =
    Mounting.mount graph None strict target None (fun context -> Program.run view context.Backend)

/// <summary>The element ids of the server HTML of <c>definition</c>, parsed under a detached element.</summary>
let private servedIds (definition: Component<'props>) (props: 'props) =
    let parsed = host ()
    parsed.innerHTML <- Server.render Server.Options.defaults definition props
    domIds parsed

let private clientIdentity () =
    let served = servedIds identified [| 7; 8 |]
    use graph = new Graph ()
    let target = host ()
    identityLog.Clear ()
    let mounted = mount graph target (Component.render identified [| 7; 8 |])
    let issued = List.sort (List.ofSeq identityLog)
    let shown = domIds target
    mounted.Dispose ()
    // The root's second id is logged only.
    let visible =
        identityIds "r"
        |> List.filter (fun id -> id <> "r-0-1")

    equal (identityIds "r", visible, served) (issued, List.sort served, shown)

let private clientCreateIdAfterSetup () =
    use graph = new Graph ()
    let captured: Setup option ref = ref None

    let capturing =
        Component.define (fun setup () -> captured.Value <- Some setup) (fun t -> t.span [||] [||])

    let mounted = mount graph (host ()) (Component.render capturing ())

    let rejected =
        ServerChecks.failure (fun () -> captured.Value.Value.CreateId () |> ignore)

    mounted.Dispose ()
    equal true (rejected :? InvalidOperationException)

let private strictOptions: Hydration.HydrationOptions =
    { IdPrefix = "r"; Strict = true }

let private omit =
    { Server.Options.defaults with
        Policy = Omit
    }

/// <summary>A detached host holding the server HTML of <c>definition</c> for <c>props</c>.</summary>
let private serve (definition: Component<'props>) (props: 'props) : HTMLElement =
    let host = host ()
    host.innerHTML <- Server.render omit definition props
    host

let private hydrate graph options (host: HTMLElement) (definition: Component<'props>) (props: 'props) =
    Hydration.hydrate graph None strict options host definition props

/// <summary>Every node under <c>root</c>, in document order.</summary>
[<Emit("(() => { const w = document.createTreeWalker($0, 0xFFFFFFFF); const a = []; while (w.nextNode()) a.push(w.currentNode); return a; })()")>]
let private nodesOf (root: Node) : Node array = jsNative

[<Emit("Array.from($0.querySelectorAll($1))")>]
let private all (root: Node) (selector: string) : Node array = jsNative

let private sameNodes (before: Node array) (after: Node array) =
    before.Length = after.Length
    && Array.forall2 (fun (left: Node) (right: Node) -> obj.ReferenceEquals (left, right)) before after

/// <summary>Records mutations under <c>root</c>; the returned function stops and returns their count.</summary>
[<Emit("(() => { const o = new MutationObserver(() => {}); o.observe($0, { subtree: true, childList: true, attributes: true, characterData: true }); return () => { const n = o.takeRecords().length; o.disconnect(); return n; }; })()")>]
let private recordMutations (root: Node) : unit -> int = jsNative

[<Emit("$0.dispatchEvent(new FocusEvent('focus'))")>]
let private focus (node: Node) : unit = jsNative

[<Emit("$0.remove()")>]
let private detach (node: Node) : unit = jsNative

[<Emit("$0.setSelectionRange($1, $2)")>]
let private select (node: Node) (start: int) (finish: int) : unit = jsNative

[<Emit("[$0.selectionStart, $0.selectionEnd]")>]
let private selection (node: Node) : int array = jsNative

[<Emit("$0.textContent = $1")>]
let private setText (node: obj) (text: string) : unit = jsNative

let private element (host: HTMLElement) (selector: string) =
    host.querySelector selector |> unbox<HTMLElement>

let private input (host: HTMLElement) (selector: string) =
    host.querySelector selector
    |> unbox<HTMLInputElement>

type private CardState =
    {
        Title: Signal<string>
        Clicks: Signal<int>
    }

let private card =
    Component.define
        (fun _ (title: string) ->
            {
                Title = Api.createSignal title
                Clicks = Api.createSignal 0
            })
        (fun t ->
            t.div
                [| t.Attrs.title (t.from (fun r -> r.Title)) |]
                [|
                    t.text "Title: "
                    t.text (t.from (fun r -> r.Title))
                    t.button [| t.Events.onClick (fun r _ -> r.Clicks.Value <- r.Clicks.Value + 1) |] [| t.text (fun r -> string r.Clicks.Value) |]
                    t.input
                        [|
                            t.Events.on ("focus", fun r _ -> r.Clicks.Value <- r.Clicks.Value + 10)
                            t.Attrs.Input.value (fun r -> r.Title.Value)
                        |]
                |])

/// <summary>The title attribute, the text and the input value of a hydrated <c>card</c>.</summary>
let private cardShown (host: HTMLElement) =
    (element host "div").getAttribute "title", (element host "div").textContent, (input host "input").value

let private adoptsEveryNode () =
    use graph = new Graph ()
    let host = serve card "a"
    equal """<!--[r--><div title="a">Title: <!--!-->a<button>0</button><input value="a"></div><!--]-->""" host.innerHTML
    let before = nodesOf host
    let mounted = hydrate graph strictOptions host card "a"
    equal (true, (Some "a", "Title: a0", "a")) (sameNodes before (nodesOf host), cardShown host)
    mounted.Dispose ()

let private updatesAndDisposes () =
    use graph = new Graph ()
    let host = serve card "a"
    let mounted = hydrate graph strictOptions host card "a"
    let button = element host "button"
    button.click ()
    equal "1" button.textContent
    focus (unbox<Node>(host.querySelector "input"))
    equal "11" button.textContent
    mounted.Dispose ()
    equal 0. host.childNodes.length
    button.click ()
    equal "11" button.textContent

let private matchingValuesWriteNothing () =
    use graph = new Graph ()
    let host = serve card "a"
    let mutations = recordMutations host
    let mounted = hydrate graph strictOptions host card "a"
    equal (0, (Some "a", "Title: a0", "a")) (mutations (), cardShown host)
    mounted.Dispose ()

let private differingValuesCorrected () =
    use graph = new Graph ()
    let host = serve card "a"
    let mounted = hydrate graph strictOptions host card "b"
    equal (Some "b", "Title: b0", "b") (cardShown host)
    equal (Some "b") ((input host "input").getAttribute "value")
    mounted.Dispose ()

let private appending =
    Component.define (fun _ (text: string) -> Api.createSignal text) (fun t ->
        t.span
            [|
                t.Events.onClick (fun (text: Signal<string>) _ -> text.Value <- text.Value + "x")
            |]
            [| t.text (fun (text: Signal<string>) -> text.Value) |])

let private emptyTextHydrates () =
    use graph = new Graph ()
    let host = serve appending ""
    equal "<!--[r--><span><!--_--></span><!--]-->" host.innerHTML
    let mounted = hydrate graph strictOptions host appending ""
    let span = element host "span"
    equal (1., 3.) (span.childNodes.length, span.firstChild.Value.nodeType)
    span.click ()
    equal "x" span.textContent
    mounted.Dispose ()

let private shapes () =
    let check (definition: Component<'props>) (props: 'props) (text: string) =
        use graph = new Graph ()
        let host = serve definition props
        let before = nodesOf host
        let mounted = hydrate graph strictOptions host definition props
        equal (true, text) (sameNodes before (nodesOf host), host.textContent)
        mounted.Dispose ()
        equal 0. host.childNodes.length

    check (Component.define (fun _ () -> ()) (fun t -> t.fragment [||])) () ""
    check textual "one" "one"
    check pair greetingProps "Hello <Ada>!<Ada>"
    check (Component.define (fun _ () -> ()) (fun t -> t.fragment [| t.text "a"; t.span [||] [| t.text "b" |] |])) () "ab"

let private regionsMountWithContent () =
    use graph = new Graph ()
    let host = serve listed listedItems
    let list = element host "ul"
    let markers = list.firstChild.Value, list.lastChild.Value
    let served = all host "li"
    let mounted = hydrate graph strictOptions host listed listedItems
    let rows = all host "li"

    let fresh =
        rows
        |> Array.forall (fun row ->
            served
            |> Array.forall (fun server -> not (obj.ReferenceEquals (row, server))))

    equal
        (true, true, true, "a<b>", 2, true)
        (obj.ReferenceEquals (list, element host "ul"),
         obj.ReferenceEquals (fst markers, list.firstChild.Value),
         obj.ReferenceEquals (snd markers, list.lastChild.Value),
         list.textContent,
         rows.Length,
         fresh)

    mounted.Dispose ()

let private afterRegions =
    Component.define (fun _ (items: Listed array) -> items) (fun t ->
        t.fragment
            [|
                t.each (fun (item: Listed) -> item.Id) id listedRow
                t.ul
                    [||]
                    [|
                        t.each (fun (item: Listed) -> item.Id) id listedRow
                        t.li [| t.Attrs.title (fun (items: Listed array) -> Some (string items.Length)) |] [| t.text "last" |]
                    |]
            |])

let private nodesAfterRegions () =
    use graph = new Graph ()
    let host = serve afterRegions listedItems
    let list = element host "ul"
    let last = element host "li[title]"
    let mounted = hydrate graph strictOptions host afterRegions listedItems

    equal
        (true, true, Some "2", "a<b>a<b>last", 5)
        (obj.ReferenceEquals (list, element host "ul"),
         obj.ReferenceEquals (last, element host "li[title]"),
         last.getAttribute "title",
         host.textContent,
         (all host "li").Length)

    mounted.Dispose ()
    equal 0. host.childNodes.length

let private queuedRaises () =
    use graph = new Graph ()
    let host = serve card "a"

    let error =
        ServerChecks.failure (fun () ->
            Hydration.hydrateWith (Scheduling.QueuedCommits (Scheduling.CollectAndContinue, ignore)) graph None strict strictOptions host card "a"
            |> ignore)

    equal (true, "Hydration supports synchronous commits only") (error :? NotSupportedException, error.Message)

let private foreignSiblings () =
    use graph = new Graph ()
    let host = host ()

    host.innerHTML <-
        "\n  <p>before</p>"
        + Server.render omit card "a"
        + "<script type=\"application/json\">{}</script>\n"

    let paragraph, script = host.querySelector "p", host.querySelector "script"
    let mounted = hydrate graph strictOptions host card "a"
    (element host "button").click()
    equal "Title: a1" (element host "div").textContent
    mounted.Dispose ()

    equal
        (true, true, true, "\n  before{}\n")
        (obj.ReferenceEquals (paragraph, host.querySelector "p"),
         obj.ReferenceEquals (script, host.querySelector "script"),
         isNull (box (host.querySelector "div")),
         host.textContent)

let private carriageReturn () =
    use graph = new Graph ()
    let host = serve card "a\r\nb"
    let mutations = recordMutations host
    let mounted = hydrate graph strictOptions host card "a\r\nb"
    // A text input's value drops line breaks; its value attribute keeps them.
    equal (0, (Some "a\r\nb", "Title: a\r\nb0", "ab"), Some "a\r\nb") (mutations (), cardShown host, (input host "input").getAttribute "value")
    mounted.Dispose ()

let private editable =
    Component.define (fun _ (text: string) -> Api.createSignal text) (fun t ->
        t.div
            [||]
            [|
                t.input [| t.Props.Input.value (fun (text: Signal<string>) -> text.Value) |]
                t.input [| t.Props.Input.value "fixed" |]
                t.button [| t.Events.onClick (fun (text: Signal<string>) _ -> text.Value <- "set") |] [||]
            |])

let private editedInputsKept () =
    use graph = new Graph ()
    let host = serve editable "a"
    attachToBody host
    let inputs = all host "input"

    let live, fixedInput =
        unbox<HTMLInputElement> inputs[0], unbox<HTMLInputElement> inputs[1]

    live.value <- "typed"
    fixedInput.value <- "edited"
    live.focus ()
    select inputs[0] 1 3
    let mounted = hydrate graph strictOptions host editable "a"

    let kept =
        live.value, fixedInput.value, obj.ReferenceEquals (activeElement (), live), List.ofArray (selection inputs[0])

    (element host "button").click()
    let set = live.value
    mounted.Dispose ()
    detach host
    equal (("typed", "edited", true, [ 1; 3 ]), "set") (kept, set)

let private differingInputsCorrected () =
    use graph = new Graph ()
    let host = serve editable "a"
    let mounted = hydrate graph strictOptions host editable "b"
    equal "b" (input host "input").value
    mounted.Dispose ()

type private RawState = { Markup: Signal<string> }

let private rawCard =
    Component.define (fun _ (markup: string) -> { Markup = Api.createSignal markup }) (fun t ->
        t.div
            [||]
            [|
                t.span [| t.Unsafe.html (fun r -> UnsafeHtml.Trust r.Markup.Value) |] [||]
                t.button [| t.Events.onClick (fun r _ -> r.Markup.Value <- "<i>next</i>") |] [||]
            |])

let private rawContentRewritten () =
    use graph = new Graph ()
    let host = serve rawCard "<b>x</b>"
    let content = element host "span"
    let server = host.querySelector "b"
    let mutations = recordMutations content
    let mounted = hydrate graph strictOptions host rawCard "<b>x</b>"

    let rewritten =
        mutations (), content.innerHTML, obj.ReferenceEquals (server, host.querySelector "b")

    (element host "button").click()
    let updated = content.innerHTML
    mounted.Dispose ()
    equal ((1, "<b>x</b>", false), "<i>next</i>") (rewritten, updated)

let private placedCard =
    Component.define (fun _ (log: ResizeArray<obj>) -> log) (fun t ->
        t.div
            [||]
            [|
                t.span
                    [|
                        t.Hooks.mounted (fun (log: ResizeArray<obj>) span ->
                            log.Add (box span)
                            ignore)
                    |]
                    [| t.text "s" |]
                t.div
                    [|
                        t.Hooks.widget (fun (log: ResizeArray<obj>) widget ->
                            setText widget "widget"
                            log.Add (box widget)
                            fun () -> log.Add (box "destroyed"))
                    |]
                    [||]
            |])

let private placedOnAdopted () =
    use graph = new Graph ()
    let log = ResizeArray<obj>()
    let host = serve placedCard log

    let root, span, widget =
        host.querySelector "div", host.querySelector "span", host.querySelector ":scope > div > div"

    let served = host.innerHTML
    let mounted = hydrate graph strictOptions host placedCard log
    let hooked = List.ofSeq log

    let placed =
        match hooked with
        | [ first; second ] ->
            obj.ReferenceEquals (first, span), obj.ReferenceEquals (second, widget), obj.ReferenceEquals ((unbox<Node> first).parentNode.Value, root)
        | _ -> false, false, false

    let shown = (element host ":scope > div > div").textContent
    mounted.Dispose ()

    equal
        ("<!--[r--><div><span>s</span><div></div></div><!--]-->", (true, true, true), "widget", box "destroyed")
        (served, placed, shown, Seq.last log)

let private noted =
    Component.define (fun _ (text: string) -> Api.createSignal text) (fun t ->
        t.div
            [||]
            [|
                t.textarea [||] [| t.text (fun (text: Signal<string>) -> text.Value) |]
                t.button [| t.Events.onClick (fun (text: Signal<string>) _ -> text.Value <- "typed") |] [||]
            |])

let private emptyRcdataText () =
    use graph = new Graph ()
    let host = serve noted ""

    let area =
        host.querySelector "textarea"
        |> unbox<HTMLTextAreaElement>

    let served = area.childNodes.length
    let mounted = hydrate graph strictOptions host noted ""
    let adopted = area.childNodes.length, area.firstChild.Value.nodeType, area.value
    (element host "button").click()
    let typed = area.value
    mounted.Dispose ()
    equal (0., (1., 3., ""), "typed") (served, adopted, typed)

let private emptyAndMarked =
    Component.define (fun _ (text: string) -> text) (fun t ->
        t.span
            [||]
            [|
                t.text (fun text -> text)
                t.b [| t.Attrs.title (fun _ -> Some "x") |] [||]
            |])

let private emptyAndOther =
    Component.define (fun _ (text: string) -> text) (fun t ->
        t.span
            [||]
            [|
                t.text (fun text -> text)
                t.i [| t.Attrs.title (fun _ -> Some "x") |] [||]
            |])

let private listedThenMore =
    Component.define (fun _ (items: Listed array) -> items) (fun t ->
        t.fragment
            [|
                t.ul [||] [| t.each (fun (item: Listed) -> item.Id) id listedRow |]
                t.b [||] [||]
            |])

let private mismatchRestores () =
    let check (served: string) (client: Component<'props>) (props: 'props) =
        use graph = new Graph ()
        let host = host ()
        host.innerHTML <- served
        let before = nodesOf host

        let outcome =
            match
                ServerChecks.failure (fun () ->
                    hydrate graph strictOptions host client props
                    |> ignore)
            with
            | TemplateDom.HydrationMismatchException (path, expected, found) as error -> Some (path, expected, found, error.Message)
            | _ -> None

        outcome, host.innerHTML = served, sameNodes before (nodesOf host)

    equal
        [
            Some ("0/1", "<i>", "b", "Hydration mismatch at 0/1: expected <i>, found b"), true, true
            Some ("#1", "<!--]-->", "b", "Hydration mismatch at #1: expected <!--]-->, found b"), true, true
            Some ("#0", "a text node", "div", "Hydration mismatch at #0: expected a text node, found div"), true, true
        ]
        [
            check (Server.render omit emptyAndMarked "") emptyAndOther ""
            check (Server.render omit listedThenMore listedItems) listed listedItems
            check (Server.render omit card "a") textual "a"
        ]

let private rootMarkerAbsent () =
    use graph = new Graph ()
    let host = host ()
    host.innerHTML <- "<div>plain</div>"

    match
        ServerChecks.failure (fun () ->
            hydrate graph strictOptions host card "a"
            |> ignore)
    with
    | TemplateDom.HydrationMismatchException (path, expected, found) ->
        equal (("root", "<!--[r-->", "absent"), "<div>plain</div>") ((path, expected, found), host.innerHTML)
    | other -> failwithf "Expected HydrationMismatchException; got %O" other

/// <summary>The server HTML of <c>definition</c> with an extra <c>&lt;b&gt;</c> root before the root end marker.</summary>
let private withTrailingRoot (definition: Component<'props>) (props: 'props) =
    let html = Server.render omit definition props

    html.Substring (0, html.Length - 8)
    + "<b></b><!--]-->"

/// <summary>An element whose <c>title</c> and <c>id</c> are written when the props are true.</summary>
let private attributed =
    Component.define (fun _ (on: bool) -> on) (fun t ->
        t.span
            [|
                t.Attrs.title (fun (on: bool) -> if on then Some "x" else None)
                t.Attrs.id (fun (on: bool) -> if on then Some "i" else None)
            |]
            [| t.text "s" |])

/// <summary>A template whose only content is <c>definition</c>, inlined inside a <c>div</c>.</summary>
let private inlined (definition: Component<'props>) =
    Component.define (fun _ (props: 'props) -> props) (fun t -> t.div [||] [| t.render definition id |])

/// <summary>The textarea value, the checkbox state and the select's selected index.</summary>
let private formState (host: HTMLElement) =
    (host.querySelector "textarea"
     |> unbox<HTMLTextAreaElement>)
        .value,
    (input host "input").``checked``,
    (host.querySelector "select"
     |> unbox<HTMLSelectElement>)
        .selectedIndex

let private strictRestoresValues () =
    let check (definition: Component<'props>) (served: 'props) (client: 'props) (shown: HTMLElement -> 'shown) =
        use graph = new Graph ()
        let host = host ()
        host.innerHTML <- withTrailingRoot definition served
        // The parsed form of the served HTML: a pre's leading line feed is dropped by parsing.
        let html = host.innerHTML
        let before = nodesOf host
        let expected = shown host

        let path =
            match
                ServerChecks.failure (fun () ->
                    hydrate graph strictOptions host definition client
                    |> ignore)
            with
            | TemplateDom.HydrationMismatchException (path, _, _) -> path
            | other -> other.Message

        equal ("#1", html, true, expected) (path, host.innerHTML, sameNodes before (nodesOf host), shown host)

    check card "a" "b" cardShown
    check editable "a" "b" (fun host -> (input host "input").value, (all host "input").Length)
    check rawCard "<b>x</b>" "<i>y</i>" (fun host -> (element host "span").innerHTML)

    let served: ServerChecks.FormState = { Text = "x"; On = true; Choice = "b" }
    let client: ServerChecks.FormState = { Text = "y"; On = false; Choice = "a" }
    check ServerChecks.formFixture served client formState
    check (inlined ServerChecks.formFixture) served client formState
    check attributed true false (fun host -> (element host "span").getAttribute "title")
    check (inlined attributed) true false (fun host -> (element host "span").getAttribute "id")

let private rootWidget =
    Component.define (fun _ (log: ResizeArray<string>) -> log) (fun t ->
        t.div
            [|
                t.Hooks.widget (fun (log: ResizeArray<string>) _ ->
                    log.Add "attached"
                    fun () -> log.Add "destroyed")
            |]
            [||])

let private hydratedRootWidgetMoves () =
    use graph = new Graph ()
    let log = ResizeArray<string>()
    let host = serve rootWidget log
    let widget = host.querySelector "div"
    let other = CheckSupport.host ()
    let mounted = hydrate graph strictOptions host rootWidget log
    mounted.MoveBefore (other, None)
    let moved = obj.ReferenceEquals (widget, other.querySelector "div")
    mounted.Dispose ()
    equal (true, [ "attached"; "destroyed" ], 0.) (moved, List.ofSeq log, other.childNodes.length)

let private formsHydrate () =
    let shown (host: HTMLElement) =
        let area =
            host.querySelector "textarea"
            |> unbox<HTMLTextAreaElement>

        let toggle = input host "input"

        let list =
            host.querySelector "select"
            |> unbox<HTMLSelectElement>

        area.value, toggle.``checked``, list.selectedIndex, (element host "pre").textContent

    let served: ServerChecks.FormState =
        {
            Text = "\nline"
            On = true
            Choice = "b"
        }

    let differing: ServerChecks.FormState =
        {
            Text = "other"
            On = false
            Choice = "a"
        }

    let hydrateWith (client: ServerChecks.FormState) =
        use graph = new Graph ()
        let host = serve ServerChecks.formFixture served
        let mutations = recordMutations host
        let mounted = hydrate graph strictOptions host ServerChecks.formFixture client
        let outcome = mutations (), shown host
        mounted.Dispose ()
        outcome

    equal ((0, ("\nline", true, 1., "\nline")), ("other", false, 0., "other")) (hydrateWith served, snd (hydrateWith differing))

/// <summary>
/// Mounts <c>definition</c> and checks that the client's sorted id log and its DOM ids in document order equal
/// <c>expected</c>, and that the server's DOM ids equal the client's.
/// </summary>
let private sameIds (definition: Component<'props>) (props: 'props) (expected: string list) =
    let served = servedIds definition props
    use graph = new Graph ()
    let target = host ()
    identityLog.Clear ()
    let mounted = mount graph target (Component.render definition props)
    let issued = List.sort (List.ofSeq identityLog)
    let shown = domIds target
    mounted.Dispose ()
    equal (List.sort expected, expected, shown) (issued, shown, served)

let private clientHoleIdentity () =
    sameIds idHole () holeIds

let private clientNestedIdentity () =
    sameIds idNested [| 1; 2 |] nestedIds

let private clientHolesIdentity () =
    sameIds idHoles () holesIds

/// <summary>
/// Mounts <c>idBoundary</c> with its content ready, pending or failed and checks the client's id log in setup order, its
/// DOM ids against the server's, and the failures the boundary reported.
/// </summary>
let private clientBoundaryIdentity () =
    let check ready fails (expected: string list) (visible: string list) (reported: string list list) =
        idContentReady <- ready
        idContentFails <- fails
        let served = servedIds idBoundary ()
        use graph = new Graph ()
        let target = host ()
        let reports = ResizeArray<exn list>()
        identityLog.Clear ()

        let mounted =
            Mounting.mount graph None reports.Add target None (fun context -> Program.run (Component.render idBoundary ()) context.Backend)

        let issued = List.ofSeq identityLog
        let shown = domIds target
        mounted.Dispose ()
        idContentReady <- true
        idContentFails <- false

        equal (expected, visible, visible, reported) (issued, shown, served, [ for errors in reports -> [ for error in errors -> error.Message ] ])

    check true false boundaryContentIds boundaryContentIds []
    check false false boundaryPendingIds [ "r-0-0"; "r-0.r0.1-0" ] []
    check true true boundaryErrorIds [ "r-0-0"; "r-0.r0.1-0" ] [ [ "failed" ] ]

let private clientCreateIdInLaterSetup () =
    use graph = new Graph ()

    let error =
        ServerChecks.failure (fun () ->
            mount graph (host ()) (Component.render (capturedCreateId ()) ())
            |> ignore)

    equal (true, "CreateId is valid only while a component's setup runs") (error :? InvalidOperationException, error.Message)

let cases: (string * (unit -> unit)) array =
    strictCases
        [|
            "a client mount issues the server's ids for the same structure", clientIdentity
            "CreateId after a client setup raises", clientCreateIdAfterSetup
            "a client view hole's first template takes the row's position and a later one the authored step under it", clientHoleIdentity
            "a client mount numbers inlined children in pre-order and puts an inlined child's rows under its path, as the server does",
            clientNestedIdentity
            "each client view row counts its authored steps from 0 and the enclosing row resumes its count, as the server does", clientHolesIdentity
            "a Setup kept from one client setup raises when called during another instance's setup", clientCreateIdInLaterSetup
            "a client boundary's content takes row 0 and its pending or error view row 1, as the server's [c, [p and [e boundaries do",
            clientBoundaryIdentity
            "hydrating server HTML adopts every node", adoptsEveryNode
            "a hydrated template updates, dispatches delegated and direct events, and disposes like a mount", updatesAndDisposes
            "hydration writes no text or attribute when values match", matchingValuesWriteNothing
            "hydration corrects differing values without a report", differingValuesCorrected
            "empty text slots hydrate through their placeholder and update", emptyTextHydrates
            "empty, one-node, multi-root and fragment templates hydrate", shapes
            "a template with regions hydrates its own nodes and mounts the client's rows fresh between the server's markers", regionsMountWithContent
            "a region occupies two logical positions: the nodes and roots after a region's end marker are adopted", nodesAfterRegions
            "queued-commit hydration raises NotSupportedException", queuedRaises
            "hydration adopts only the root marker pair and leaves foreign siblings in place", foreignSiblings
            "a text value with CR LF hydrates without writes", carriageReturn
            "inputs edited before hydration keep their values, focus and selection when the server and client values agree", editedInputsKept
            "a live input value differing from the server's is written by hydration", differingInputsCorrected
            "hydration rewrites raw content once and the slot then updates", rawContentRewritten
            "placed hooks run on the adopted nodes after adoption, and a widget attaches to its adopted empty element", placedOnAdopted
            "an empty text slot in a textarea the server left empty hydrates into a created text node", emptyRcdataText
            "a structural mismatch names the path, the expected and the found node, and leaves the server HTML as it was", mismatchRestores
            "a host without a root marker raises a mismatch at root", rootMarkerAbsent
            "a mismatch after the first commits leaves the server's text, attributes, live values and raw content in place", strictRestoresValues
            "a widget on a hydrated root moves with its mount and is destroyed without a report", hydratedRootWidgetMoves
            "forms hydrate a leading line feed, checked and selectedIndex without writes, and write the client's differing values", formsHydrate
        |]

/// <summary>The text and value of the first element matching <c>selector</c> after the browser parses <c>html</c>.</summary>
let private parsed (html: string) (selector: string) : string * string =
    let container = globalThis.document.createElement "div"
    container.innerHTML <- html

    let element =
        container.querySelector selector
        |> unbox<HTMLElement>

    element.textContent, (if isNull element?value then "" else string element?value)

let private leadingLineFeeds () =
    let html =
        Server.render
            { Server.Options.defaults with
                Policy = Omit
            }
            ServerChecks.formFixture
            {
                ServerChecks.FormState.Text = "\nline"
                On = false
                Choice = "a"
            }

    equal ("\nline", "\nline") (parsed html "textarea")
    equal "\nline" (fst (parsed html "pre"))
    equal "\nstatic" (fst (parsed html "pre + pre"))

let browserCases: (string * (unit -> unit)) array =
    [|
        "textarea value and pre text keep a leading line feed after parsing", leadingLineFeeds
    |]
