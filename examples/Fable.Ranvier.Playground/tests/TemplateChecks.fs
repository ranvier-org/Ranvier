module TemplateChecks

open global.Ranvier
open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template

[<Global>]
let private globalThis: TS.Es.GlobalThis = jsNative

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

[<Emit("(() => { const shape = n => n.nodeType + (n.nodeName || '') + '(' + Array.from(n.childNodes).map(shape).join(',') + ')'; const t = document.createElement('template'); t.innerHTML = $0; const first = shape(t.content); const again = document.createElement('template'); again.innerHTML = t.innerHTML; return first === shape(again.content); })()")>]
let private roundTrips (markup: string) : bool = jsNative

type Greeting = { Title: string; Name: string }

let private greeting =
    Component.define (fun _ (props: Greeting) -> props) (fun t ->
        t.fragment
            [|
                t.div
                    [| t.Attrs.className "panel"; t.Attrs.title (fun r -> r.Title) |]
                    [| t.text "Hello "; t.text (fun r -> r.Name); t.text (fun _ -> "!") |]
                t.span [||] [| t.text (fun r -> r.Name) |]
            |])

let private walkPrograms () =
    equal [| 0; 3; 1; 0; 1; 3 |] (TemplateDom.walkProgram [| [| 0 |]; [| 1; 1 |] |])
    equal [| 0; 0; 1; 3; 2; 1; 3 |] (TemplateDom.walkProgram [| [| 0; 1 |]; [| 1 |] |])

let private compiledSkeleton () =
    let compiled = TemplateDom.compile greeting
    let markup, targets, rootCount = skeletonMarkup (greeting :> TemplateComponent).Root
    equal 2 rootCount
    equal "<div class=\"panel\">Hello <!--!--> <!--!--> </div><span> </span>" markup
    equal true (roundTrips markup)
    equal 4 targets.Length
    equal 4 compiled.NodeCount
    equal 4 compiled.RenderSlots.Length
    equal 2. compiled.Skeleton.childNodes.length

module H = Ranvier.View.Foundation.Generated.Html

type RowProps =
    {
        Index: int
        Source: Signal<string>
        Disabled: Memo<bool>
    }

type Row =
    {
        Index: int
        Source: Signal<string>
        Disabled: Memo<bool>
        Local: Signal<int>
        Log: ResizeArray<string>
    }

let private rowLog = ResizeArray<string>()

let private row =
    Component.define
        (fun setup (props: RowProps) ->
            let row =
                {
                    Index = props.Index
                    Source = props.Source
                    Disabled = props.Disabled
                    Local = Api.createSignal 0
                    Log = rowLog
                }

            setup.OnCleanup (fun () -> rowLog.Add ("cleanup " + string row.Index))
            row)
        (fun t ->
            t.div
                [|
                    t.Attrs.className "panel"
                    t.Attrs.id (fun r -> "row-" + string r.Index)
                    t.Attrs.title (fun r -> r.Source.Value)
                |]
                [|
                    t.input [| t.Props.value (fun r -> r.Source.Value) |]
                    t.button
                        [|
                            t.Props.disabled (fun r -> r.Disabled.Value)
                            t.Events.onClick (fun r _ -> r.Local.Value <- r.Local.Value + 1)
                        |]
                        [| t.text (fun r -> string r.Local.Value) |]
                |])

let private host () =
    globalThis.document.createElement "main"

let private mount graph (host: HTMLElement) view =
    Mounting.mount graph None (fun errors -> failwithf "%A" errors) host None (fun context -> Program.run view context.Backend)

let private state (host: HTMLElement) =
    let div = host.querySelector "div" |> unbox<HTMLElement>

    let input =
        host.querySelector "input"
        |> unbox<HTMLInputElement>

    let button =
        host.querySelector "button"
        |> unbox<HTMLButtonElement>

    div.getAttribute "class", div.getAttribute "id", div.getAttribute "title", input.value, button.disabled, button.textContent

let private twinEquality () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "a")

    let disabled =
        graph.Run (fun () -> Api.createMemo (fun _ -> source.Value = "blocked"))

    let first, second = host (), host ()

    let props =
        {
            Index = 1
            Source = source
            Disabled = disabled
        }

    let templateMount = mount graph first (Component.render row props)

    let twin =
        H.Program.div
            [|
                H.Attrs.classStatic "panel"
                H.Attrs.idStatic "row-1"
                H.Attrs.titleReactive (fun () -> Some source.Value)
            |]
            [|
                H.Program.input [| H.Properties.HTMLInputElement.valueReactive (fun () -> source.Value) |]
                H.Program.button [| H.Properties.HTMLButtonElement.disabledReactive (fun () -> disabled.Value) |] [| Program.text "0" |]
            |]

    let twinMount = mount graph second twin
    equal (state second) (state first)
    graph.Run (fun () -> source.Value <- "blocked")
    equal (state second) (state first)
    templateMount.Dispose ()
    twinMount.Dispose ()

let private changedSlotsOnly () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "a")
    let disabled = graph.Run (fun () -> Api.createMemo (fun _ -> false))
    let host = host ()

    let mounted =
        mount
            graph
            host
            (Component.render
                row
                {
                    Index = 1
                    Source = source
                    Disabled = disabled
                })

    let div = host.querySelector "div" |> unbox<HTMLElement>
    let mutable titleWrites = 0
    let setAttribute = div?setAttribute

    div?setAttribute <-
        fun (name: string) (value: string) ->
            if name = "title" then
                titleWrites <- titleWrites + 1

            setAttribute?call (div, name, value)

    (host.querySelector "button"
     |> unbox<HTMLButtonElement>)
        .click()

    equal 0 titleWrites
    graph.Run (fun () -> source.Value <- "b")
    equal 1 titleWrites
    mounted.Dispose ()

let private readerFailure () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "a")

    let failing =
        Component.define (fun _ (source: Signal<string>) -> source) (fun t ->
            t.div
                [|
                    t.Attrs.title (fun s -> if s.Value = "bad" then failwith "reader" else s.Value)
                |]
                [| t.text (fun s -> s.Value) |])

    let host = host ()
    let reported = ResizeArray<exn>()

    let mounted =
        Mounting.mount graph None reported.AddRange host None (fun context -> Program.run (Component.render failing source) context.Backend)

    equal 0 reported.Count
    graph.Run (fun () -> source.Value <- "bad")
    equal (Some "a", "bad") ((host.querySelector "div" |> unbox<HTMLElement>).getAttribute "title", host.textContent)
    equal [ "reader" ] [ for error in reported -> error.Message ]
    graph.Run (fun () -> source.Value <- "c")
    equal (Some "c", "c") ((host.querySelector "div" |> unbox<HTMLElement>).getAttribute "title", host.textContent)
    equal 1 reported.Count
    mounted.Dispose ()

let private recoveredFailureRecurs () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "a")
    let failure = exn "cached"

    let failing =
        Component.define (fun _ (source: Signal<string>) -> source) (fun t ->
            t.div
                [|
                    t.Attrs.title (fun s -> if s.Value.StartsWith "bad" then raise failure else s.Value)
                |]
                [||])

    let host = host ()
    let reported = ResizeArray<exn>()

    let mounted =
        Mounting.mount graph None reported.AddRange host None (fun context -> Program.run (Component.render failing source) context.Backend)

    graph.Run (fun () -> source.Value <- "bad")
    graph.Run (fun () -> source.Value <- "bad!")
    equal 1 reported.Count
    graph.Run (fun () -> source.Value <- "ok")
    graph.Run (fun () -> source.Value <- "bad")
    equal 2 reported.Count
    mounted.Dispose ()

let private throwingReporter () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "a")

    let failing =
        Component.define (fun _ (source: Signal<string>) -> source) (fun t ->
            t.div
                [|
                    t.Attrs.title (fun s -> if s.Value = "bad" then failwith "title" else s.Value)
                |]
                [|
                    t.text (fun s -> if s.Value = "bad" then failwith "text" else s.Value)
                    t.span [||] [| t.isolated (t.text (fun s -> s.Value + "!")) |]
                |])

    let host = host ()
    let reported = ResizeArray<exn>()

    let reporter (errors: exn list) =
        reported.AddRange errors
        failwith "reporter"

    let mounted =
        Mounting.mount graph None reporter host None (fun context -> Program.run (Component.render failing source) context.Backend)

    let run value =
        try
            graph.Run (fun () -> source.Value <- value)
            None
        with error ->
            Some error

    let surfaced = run "bad"
    equal None surfaced
    equal [ "title" ] [ for error in reported -> error.Message ]
    equal (Some "a", "abad!") ((host.querySelector "div" |> unbox<HTMLElement>).getAttribute "title", host.textContent)
    equal None (run "ok")
    equal 1 reported.Count
    equal (Some "ok", "okok!") ((host.querySelector "div" |> unbox<HTMLElement>).getAttribute "title", host.textContent)
    mounted.Dispose ()

let private initialReaderFailure () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "bad")

    let failing =
        Component.define (fun _ (source: Signal<string>) -> source) (fun t ->
            t.div
                [|
                    t.Attrs.title (fun s -> if s.Value = "bad" then failwith "reader" else s.Value)
                |]
                [| t.text (fun s -> s.Value) |])

    let host = host ()
    let reported = ResizeArray<exn>()

    let mounted =
        Mounting.mount graph None reported.AddRange host None (fun context -> Program.run (Component.render failing source) context.Backend)

    equal ("bad", 1) (host.textContent, reported.Count)
    graph.Run (fun () -> source.Value <- "ok")
    equal (Some "ok", "ok") ((host.querySelector "div" |> unbox<HTMLElement>).getAttribute "title", host.textContent)
    mounted.Dispose ()

let private setterRollback () =
    use graph = new Graph ()
    let host = host ()
    host.innerHTML <- "<span>keep</span>"
    let log = ResizeArray<string>()

    let failing =
        Component.define
            (fun setup () ->
                setup.OnCleanup (fun () -> log.Add "cleanup")
                ())
            (fun t ->
                t.button
                    [|
                        t.Props.propertyWith ((fun (_: HTMLButtonElement) (_: bool) -> failwith "setter"), (fun _ -> true), primitiveSame)
                    |]
                    [||])

    let mutable failed = false

    try
        mount graph host (Component.render failing ())
        |> ignore
    with _ ->
        failed <- true

    equal (true, [ "cleanup" ], "<span>keep</span>") (failed, List.ofSeq log, host.innerHTML)

let private cleanupOrder () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "a")
    let disabled = graph.Run (fun () -> Api.createMemo (fun _ -> false))
    rowLog.Clear ()
    let host = host ()

    let view =
        Program.fragment
            [|
                for index in 1..2 ->
                    Component.render
                        row
                        {
                            Index = index
                            Source = source
                            Disabled = disabled
                        }
            |]

    let mounted = mount graph host view
    mounted.Dispose ()
    equal [ "cleanup 2"; "cleanup 1" ] (List.ofSeq rowLog)
    equal 0. host.childNodes.length

let private adjacentTexts () =
    use graph = new Graph ()
    let host = host ()

    let mounted =
        mount graph host (Component.render greeting { Title = "T"; Name = "Ada" })

    equal "Hello Ada!Ada" host.textContent
    mounted.Dispose ()
    equal 0. host.childNodes.length

let private compiledOnce () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "a")
    let disabled = graph.Run (fun () -> Api.createMemo (fun _ -> false))
    let first, second = host (), host ()

    let a =
        mount
            graph
            first
            (Component.render
                row
                {
                    Index = 1
                    Source = source
                    Disabled = disabled
                })

    let compiled = (row :> TemplateDefinition).Compiled

    let b =
        mount
            graph
            second
            (Component.render
                row
                {
                    Index = 2
                    Source = source
                    Disabled = disabled
                })

    equal true (obj.ReferenceEquals (compiled, (row :> TemplateDefinition).Compiled))

    (first.querySelector "button"
     |> unbox<HTMLButtonElement>)
        .click()

    equal ("1", "0") ((first.querySelector "button").Value.textContent, (second.querySelector "button").Value.textContent)
    a.Dispose ()
    b.Dispose ()

let private keyedRows () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "a")
    let disabled = graph.Run (fun () -> Api.createMemo (fun _ -> false))
    let rows = graph.Run (fun () -> Api.createSignal [ 1; 2 ])
    let host = host ()

    let view =
        Keyed.list Program.ops (=) id (fun () -> rows.Value) (fun current ->
            Component.render
                row
                {
                    Index = current ()
                    Source = source
                    Disabled = disabled
                })

    let mounted = mount graph host view

    let removed =
        host.querySelectorAll("div").item 1.
        |> unbox<HTMLElement>

    graph.Run (fun () -> rows.Value <- [ 1 ])
    graph.Run (fun () -> source.Value <- "b")
    equal (Some "a") (removed.getAttribute "title")
    equal (Some "b") ((host.querySelector "div" |> unbox<HTMLElement>).getAttribute "title")
    mounted.Dispose ()

let private nullText () =
    use graph = new Graph ()

    let nothing =
        Component.define (fun _ () -> ()) (fun t -> t.span [||] [| t.text (fun _ -> (null: string)) |])

    let host = host ()
    let mounted = mount graph host (Component.render nothing ())
    equal "" host.textContent
    mounted.Dispose ()

let private staticTemplate () =
    use graph = new Graph ()

    let fixedView =
        Component.define (fun _ () -> ()) (fun t -> t.div [| t.Attrs.className "fixed" |] [| t.text "static" |])

    let first, second = host (), host ()
    let a = mount graph first (Component.render fixedView ())
    let b = mount graph second (Component.render fixedView ())

    let shape (host: HTMLElement) =
        (host.querySelector "div.fixed").Value.textContent

    equal ("static", "static") (shape first, shape second)
    equal 0 ((fixedView :> TemplateDefinition).Compiled :?> TemplateDom.Compiled).RenderSlots.Length
    a.Dispose ()
    b.Dispose ()

type Counted =
    {
        First: Signal<int>
        Second: Signal<int>
        Reads: int ref
    }

let private isolatedSlots () =
    use graph = new Graph ()
    let first = graph.Run (fun () -> Api.createSignal 1)
    let second = graph.Run (fun () -> Api.createSignal 1)
    let reads = ref 0

    let counted =
        Component.define (fun _ (props: Counted) -> props) (fun t ->
            t.div
                [||]
                [|
                    t.span [||] [| t.text (fun c -> string c.First.Value) |]
                    t.span
                        [||]
                        [|
                            t.isolated (
                                t.text (fun c ->
                                    c.Reads.Value <- c.Reads.Value + 1
                                    string c.Second.Value)
                            )
                        |]
                |])

    let host = host ()

    let mounted =
        mount
            graph
            host
            (Component.render
                counted
                {
                    First = first
                    Second = second
                    Reads = reads
                })

    graph.Run (fun () -> first.Value <- 2)
    equal 1 reads.Value
    graph.Run (fun () -> second.Value <- 2)
    equal (2, "22") (reads.Value, host.textContent)
    mounted.Dispose ()

let private sourceSlots () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "a")

    let doubled =
        graph.Run (fun () -> Api.createMemo (fun _ -> source.Value + source.Value))

    let sourced =
        Component.define (fun _ () -> source, doubled) (fun t ->
            t.div [| t.Attrs.title (t.from (fun (s, _) -> s)) |] [| t.text (t.from (fun (_, d) -> d)) |])

    let host = host ()
    let mounted = mount graph host (Component.render sourced ())
    graph.Run (fun () -> source.Value <- "b")
    equal (Some "b", "bb") ((host.querySelector "div" |> unbox<HTMLElement>).getAttribute "title", host.textContent)
    mounted.Dispose ()

[<Emit("$0.dispatchEvent(new FocusEvent('focus'))")>]
let private dispatchFocus (node: HTMLElement) : unit = jsNative

let private clicks () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "a")
    let disabled = graph.Run (fun () -> Api.createMemo (fun _ -> false))
    let host = host ()

    let mounted =
        mount
            graph
            host
            (Component.render
                row
                {
                    Index = 1
                    Source = source
                    Disabled = disabled
                })

    let button =
        host.querySelector "button"
        |> unbox<HTMLButtonElement>

    button.click ()
    button.click ()
    equal "2" button.textContent
    mounted.Dispose ()
    button.click ()
    equal "2" button.textContent

let private movedClicks () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "a")
    let disabled = graph.Run (fun () -> Api.createMemo (fun _ -> false))
    let first, second = host (), host ()

    let mounted =
        mount
            graph
            first
            (Component.render
                row
                {
                    Index = 1
                    Source = source
                    Disabled = disabled
                })

    let button =
        first.querySelector "button"
        |> unbox<HTMLButtonElement>

    mounted.MoveBefore (second, None)
    button.click ()
    equal "1" button.textContent
    mounted.Dispose ()

let private focusBesideClick () =
    use graph = new Graph ()
    let log = ResizeArray<string>()

    let both =
        Component.define (fun _ () -> log) (fun t ->
            t.input
                [|
                    t.Events.onFocus (fun l _ -> l.Add "focus")
                    t.Events.onClick (fun l _ -> l.Add "click")
                |])

    let host = host ()
    let mounted = mount graph host (Component.render both ())

    let input =
        host.querySelector "input"
        |> unbox<HTMLInputElement>

    dispatchFocus input
    input.click ()
    mounted.Dispose ()
    dispatchFocus input
    equal [ "focus"; "click" ] (List.ofSeq log)

let private queuedFallback () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "a")
    let disabled = graph.Run (fun () -> Api.createMemo (fun _ -> false))
    let host = host ()

    let mounted =
        Mounting.mountWith (Scheduling.QueuedCommits (Scheduling.CollectAndContinue, ignore)) graph None ignore host None (fun context ->
            Program.run
                (Component.render
                    row
                    {
                        Index = 1
                        Source = source
                        Disabled = disabled
                    })
                context.Backend)

    mounted.Flush ()
    equal (Some "a") ((host.querySelector "div" |> unbox<HTMLElement>).getAttribute "title")
    graph.Run (fun () -> source.Value <- "b")
    mounted.Flush ()
    equal (Some "b") ((host.querySelector "div" |> unbox<HTMLElement>).getAttribute "title")
    mounted.Dispose ()

let private browserInterpreter () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "a")

    let plain =
        Component.define (fun _ (source: Signal<string>) -> source) (fun t -> t.div [| t.Attrs.id "row-1" |] [| t.text (fun s -> s.Value) |])

    let host = host ()

    let dispose =
        Browser.mount graph host ignore None (Program.run (Component.render plain source))

    equal (Some "row-1", "a") ((host.querySelector "div" |> unbox<HTMLElement>).getAttribute "id", host.textContent)
    graph.Run (fun () -> source.Value <- "b")
    equal "b" host.textContent
    dispose ()
    equal 0. host.childNodes.length

let private preparedRejects () =
    let mutable rejected = false

    try
        let template =
            Prepared.prepare (Component.render greeting { Title = "T"; Name = "N" })

        use graph = new Graph ()

        mount graph (host ()) (Prepared.view template (fun _ _ -> ()))
        |> ignore
    with :? System.InvalidOperationException ->
        rejected <- true

    equal true rejected

let private serverMarkup () =
    equal
        "<div class=\"panel\" title=\"T &amp;\">Hello <!--!-->&lt;Ada&gt;<!--!-->!</div><span>&lt;Ada&gt;</span>"
        (Html.render Omit (Program.run (Component.render greeting { Title = "T &"; Name = "<Ada>" })))

let private mountFailure (view: Program.View) =
    use graph = new Graph ()

    try
        (mount graph (host ()) view).Dispose()
        []
    with error ->
        [ error.Message ]

let private restructuredMarkup () =
    let tableCell =
        Component.define (fun _ () -> ()) (fun t ->
            t.element
                (HtmlTag "table")
                [||]
                [|
                    t.element (HtmlTag "tr") [||] [| t.element (HtmlTag "td") [||] [| t.text (fun _ -> "x") |] |]
                |])

    let paragraph =
        Component.define (fun _ () -> ()) (fun t -> t.element (HtmlTag "p") [||] [| t.div [||] [| t.text "x" |] |])

    let cellFailure = mountFailure (Component.render tableCell ())
    equal (true, true) (cellFailure.Length > 0, cellFailure |> List.exists _.Contains("td"))
    equal true (isNull (tableCell :> TemplateDefinition).Compiled)
    let paragraphFailure = mountFailure (Component.render paragraph ())
    equal true (paragraphFailure |> List.exists _.Contains("root"))

let private unsetText () =
    use graph = new Graph ()
    let flight = graph.Run (fun () -> Api.createAsyncSource<string>())

    let viewOf (read: unit -> string) =
        Component.define (fun _ () -> ()) (fun t -> t.span [||] [| t.text (fun _ -> read ()) |])

    let pendingHost, failedHost = host (), host ()

    let pending =
        mount graph pendingHost (Component.render (viewOf (fun () -> flight.Value)) ())

    let failed =
        Mounting.mount graph None ignore failedHost None (fun context ->
            Program.run (Component.render (viewOf (fun () -> failwith "reader")) ()) context.Backend)

    equal ("", "") (pendingHost.textContent, failedHost.textContent)
    graph.Run (fun () -> flight.Settle "ready")
    equal "ready" pendingHost.textContent
    pending.Dispose ()
    failed.Dispose ()

let private nullAttributes () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal (null: string))
    let other = graph.Run (fun () -> Api.createSignal 0)

    let nulls =
        Component.define (fun _ () -> ()) (fun t ->
            t.div
                [|
                    t.Attrs.title (fun _ ->
                        other.Value |> ignore
                        (null: string))
                    t.Attrs.id (t.from (fun _ -> source))
                    t.Attrs.className (fun _ -> source.Value)
                |]
                [| t.text (fun _ -> string other.Value) |])

    let host = host ()
    let mounted = mount graph host (Component.render nulls ())
    let div = host.querySelector "div" |> unbox<HTMLElement>
    let mutable writes = 0
    let setAttribute = div?setAttribute

    div?setAttribute <-
        fun (name: string) (value: string) ->
            writes <- writes + 1
            setAttribute?call (div, name, value)

    equal (None, None, None) (div.getAttribute "title", div.getAttribute "id", div.getAttribute "class")
    graph.Run (fun () -> other.Value <- 1)
    equal (0, None, "1") (writes, div.getAttribute "title", host.textContent)
    graph.Run (fun () -> source.Value <- "set")
    equal (Some "set", Some "set") (div.getAttribute "id", div.getAttribute "class")
    mounted.Dispose ()

    let server =
        Component.define (fun _ () -> ()) (fun t -> t.div [| t.Attrs.title (fun _ -> (null: string)) |] [| t.text (fun _ -> "0") |])

    equal "<div>0</div>" (Html.render Omit (Program.run (Component.render server ())))

type Setter = { Current: Signal<string> }

let private laterSetterFailure () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "a")
    let written = ResizeArray<string>()

    let failing =
        Component.define (fun _ () -> { Current = source }) (fun t ->
            t.button
                [|
                    t.Props.propertyWith (
                        (fun (_: HTMLButtonElement) (value: string) ->
                            if value = "bad" then
                                failwith "setter"

                            written.Add value),
                        (fun s -> s.Current.Value),
                        primitiveSame
                    )
                    t.Attrs.title (fun s -> s.Current.Value)
                |]
                [| t.text (fun s -> s.Current.Value) |])

    let host = host ()
    let reported = ResizeArray<exn>()

    let mounted =
        Mounting.mount graph None reported.AddRange host None (fun context -> Program.run (Component.render failing ()) context.Backend)

    graph.Run (fun () -> source.Value <- "bad")
    let button = host.querySelector "button" |> unbox<HTMLElement>
    equal ([ "setter" ], Some "bad", "bad") ([ for error in reported -> error.Message ], button.getAttribute "title", host.textContent)
    graph.Run (fun () -> source.Value <- "c")
    equal ([ "a"; "c" ], "c") (List.ofSeq written, host.textContent)
    mounted.Dispose ()

let private textRoot () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "a")

    let only = Component.define (fun _ () -> source) (fun t -> t.text (t.from id))

    let host = host ()
    let mounted = mount graph host (Component.render only ())
    equal "a" host.textContent
    graph.Run (fun () -> source.Value <- "b")
    equal "b" host.textContent
    mounted.Dispose ()
    equal 0. host.childNodes.length

let private fragmentRoot () =
    use graph = new Graph ()
    let source = graph.Run (fun () -> Api.createSignal "a")

    let wrapped =
        Component.define (fun _ () -> source) (fun t -> t.fragment [| t.div [||] [| t.text (t.from id) |] |])

    let host = host ()
    let mounted = mount graph host (Component.render wrapped ())
    equal 1. host.children.length
    equal "a" host.textContent
    graph.Run (fun () -> source.Value <- "b")
    equal "b" host.textContent
    mounted.Dispose ()
    equal 0. host.childNodes.length

let private movedRoots () =
    use graph = new Graph ()
    let first, second = host (), host ()

    let mounted =
        mount graph first (Component.render greeting { Title = "T"; Name = "Ada" })

    equal 2. first.children.length
    mounted.MoveBefore (second, None)
    equal 0. first.children.length
    equal 2. second.children.length
    equal "Hello Ada!Ada" second.textContent
    mounted.Dispose ()
    equal 0. second.childNodes.length

let private walkProgramEmptyPath () =
    equal [| 3; 0; 1; 3 |] (TemplateDom.walkProgram [| [||]; [| 1 |] |])

let private singleRootCompiled () =
    let single =
        Component.define (fun _ () -> ()) (fun t -> t.div [| t.Attrs.title (fun _ -> "x") |] [||])

    let compiled = TemplateDom.compile single
    equal 1 compiled.RootCount
    equal 1. compiled.Skeleton.nodeType

    equal
        true
        (compiled.Walk.Length = 0
         || compiled.Walk[0] <> TemplateDom.FirstChild)

type Holed = { Caption: string; Body: Program.View }

let private holed =
    Component.define (fun _ (props: Holed) -> props) (fun t ->
        t.div [| t.Attrs.className "card" |] [| t.text (fun r -> r.Caption); t.view (fun r -> r.Body) |])

let private holeMarkup () =
    let body = Program.element (HtmlTag "em") [||] [| Program.text "<b>" |]

    equal
        "<div class=\"card\">A<!--[--><em>&lt;b&gt;</em><!--]--></div>"
        (Html.render Omit (Program.run (Component.render holed { Caption = "A"; Body = body })))

let private holeSlotPaths () =
    let between =
        Component.define (fun _ () -> ()) (fun t -> t.fragment [| t.text "x"; t.view (fun _ -> Program.fragment [||]); t.text (fun _ -> "y") |])

    let markup, targets, rootCount = skeletonMarkup (between :> TemplateComponent).Root
    equal ("x<!--[--><!--]--> ", 4, [| 3 |]) (markup, rootCount, snd targets[1])

    let leading =
        Component.define (fun _ () -> ()) (fun t -> t.fragment [| t.view (fun _ -> Program.fragment [||]); t.text (fun _ -> "y") |])

    let markup, _, _ = skeletonMarkup (leading :> TemplateComponent).Root
    equal "<!--[--><!--]--> " markup

[<Emit("$0.innerHTML.replace(/<!--.*?-->/g, '')")>]
let private withoutComments (node: HTMLElement) : string = jsNative

[<Emit("Array.from($0.childNodes).filter(n => n.nodeType === 8 && (n.data === '[' || n.data === ']')).length")>]
let private regionMarkers (node: Node) : int = jsNative

let private rootHole =
    Component.define (fun _ (body: Program.View) -> body) (fun t -> t.fragment [| t.view id |])

let private viewHole () =
    use graph = new Graph ()
    let label = graph.Run (fun () -> Api.createSignal "x")
    let mutable cleaned = 0

    let body =
        Program.createComponent (fun context ->
            context.OnCleanup (fun () -> cleaned <- cleaned + 1)
            Program.element (HtmlTag "em") [||] [| Program.bind (=) (fun () -> label.Value) |])

    let host = host ()

    let mounted =
        mount graph host (Component.render holed { Caption = "A"; Body = body })

    let div = host.querySelector "div" |> unbox<HTMLElement>
    equal ("<div class=\"card\">A<em>x</em></div>", 2) (withoutComments host, regionMarkers div)
    graph.Run (fun () -> label.Value <- "y")
    equal "Ay" host.textContent
    mounted.Dispose ()
    equal (1, 0.) (cleaned, host.childNodes.length)

let private holeTwin () =
    use graph = new Graph ()
    let label = graph.Run (fun () -> Api.createSignal "x")

    let body () =
        Program.element (HtmlTag "em") [||] [| Program.bind (=) (fun () -> label.Value) |]

    let first, second = host (), host ()

    let templateMount =
        mount graph first (Component.render holed { Caption = "A"; Body = body () })

    let twinMount =
        mount graph second (Component.renderWith Program.ops holed { Caption = "A"; Body = body () })

    equal (withoutComments second) (withoutComments first)
    graph.Run (fun () -> label.Value <- "y")
    equal (withoutComments second, "<div class=\"card\">A<em>y</em></div>") (withoutComments first, withoutComments second)
    templateMount.Dispose ()
    twinMount.Dispose ()

let private emptyHole () =
    use graph = new Graph ()
    let host = host ()

    let mounted =
        mount
            graph
            host
            (Component.render
                holed
                {
                    Caption = "E"
                    Body = Program.fragment [||]
                })

    let div = host.querySelector "div" |> unbox<HTMLElement>
    equal ("<div class=\"card\">E</div>", 2) (withoutComments host, regionMarkers div)
    mounted.Dispose ()
    equal 0. host.childNodes.length

let private viewReadOnce () =
    use graph = new Graph ()
    let choice = graph.Run (fun () -> Api.createSignal "first")
    let mutable reads = 0

    let once =
        Component.define (fun _ () -> ()) (fun t ->
            t.div
                [||]
                [|
                    t.view (fun _ ->
                        reads <- reads + 1
                        Program.text choice.Value)
                |])

    let host = host ()
    let mounted = mount graph host (Component.render once ())
    graph.Run (fun () -> choice.Value <- "second")
    equal ("first", 1) (host.textContent, reads)
    mounted.Dispose ()

let private failingHole () =
    use graph = new Graph ()
    let host = host ()
    host.innerHTML <- "<span>keep</span>"
    let mutable failed = false

    try
        mount
            graph
            host
            (Component.render
                holed
                {
                    Caption = "F"
                    Body = Program.createComponent (fun _ -> failwith "view")
                })
        |> ignore
    with error ->
        failed <- error.Message = "view"

    equal (true, "<span>keep</span>") (failed, host.innerHTML)

let private rootHoleClicks () =
    use graph = new Graph ()
    let clicks = ResizeArray<string>()

    let body =
        H.Program.button [| H.Events.onClick (fun _ -> clicks.Add "click") |] [||]

    let first, second = host (), host ()
    let mounted = mount graph first (Component.render rootHole body)

    let button =
        first.querySelector "button"
        |> unbox<HTMLButtonElement>

    button.click ()
    mounted.MoveBefore (second, None)
    button.click ()
    equal (2, 0., 1.) (clicks.Count, first.childNodes.length, second.children.length)
    mounted.Dispose ()
    button.click ()
    equal (2, 0.) (clicks.Count, second.childNodes.length)

let private holeFallbacks () =
    use graph = new Graph ()
    let queuedHost, browserHost = host (), host ()

    let queued =
        Mounting.mountWith (Scheduling.QueuedCommits (Scheduling.CollectAndContinue, ignore)) graph None ignore queuedHost None (fun context ->
            Program.run
                (Component.render
                    holed
                    {
                        Caption = "Q"
                        Body = Program.text "v"
                    })
                context.Backend)

    queued.Flush ()

    let dispose =
        Browser.mount
            graph
            browserHost
            ignore
            None
            (Program.run (
                Component.render
                    holed
                    {
                        Caption = "B"
                        Body = Program.text "v"
                    }
            ))

    equal ("Qv", "Bv") (queuedHost.textContent, browserHost.textContent)
    queued.Dispose ()
    dispose ()
    equal (0., 0.) (queuedHost.childNodes.length, browserHost.childNodes.length)

type Stage =
    | Waiting
    | Working of int
    | Complete of string

let private stageLog = ResizeArray<string>()
let private stageRenders = ResizeArray<string>()

let private waitingArm =
    Component.define
        (fun setup (_: unit -> unit) ->
            stageLog.Add "setup waiting"
            setup.OnCleanup (fun () -> stageLog.Add "cleanup waiting"))
        (fun t -> t.span [||] [| t.text "waiting" |])

let private workingArm =
    Component.define
        (fun setup (progress: unit -> int) ->
            stageLog.Add "setup working"
            setup.OnCleanup (fun () -> stageLog.Add "cleanup working")
            progress)
        (fun t ->
            t.span
                [||]
                [|
                    t.text (fun progress ->
                        let text = "working " + string (progress ())
                        stageRenders.Add text
                        text)
                |])

let private completeArm =
    Component.define
        (fun setup (result: unit -> string) ->
            stageLog.Add "setup complete"
            setup.OnCleanup (fun () -> stageLog.Add "cleanup complete")
            result)
        (fun t -> t.span [||] [| t.text (fun result -> "complete " + result ()) |])

/// <summary>Selects the arm of a stage; <c>complete</c> renders <c>Complete</c>.</summary>
let private stageArmsWith (complete: Component<unit -> string>) (t: TemplateBuilder<'inst>) (stage: Stage) =
    match stage with
    | Waiting -> t.arm waitingArm ()
    | Working progress -> t.arm workingArm progress
    | Complete result -> t.arm complete result

let private stageArms (t: TemplateBuilder<'inst>) (stage: Stage) =
    stageArmsWith completeArm t stage

let private staged =
    Component.define (fun _ (stage: Signal<Stage>) -> stage) (fun t -> t.div [||] [| t.matchWith (fun stage -> stage.Value) (stageArms t) |])

let private matchSwitching () =
    use graph = new Graph ()
    stageLog.Clear ()
    let stage = graph.Run (fun () -> Api.createSignal Waiting)
    let host = host ()
    let mounted = mount graph host (Component.render staged stage)
    equal "waiting" host.textContent
    graph.Run (fun () -> stage.Value <- Working 1)
    graph.Run (fun () -> stage.Value <- Working 2)
    equal "working 2" host.textContent
    graph.Run (fun () -> stage.Value <- Complete "ok")
    equal "complete ok" host.textContent

    equal
        [
            "setup waiting"
            "setup working"
            "cleanup waiting"
            "setup complete"
            "cleanup working"
        ]
        (List.ofSeq stageLog)

    mounted.Dispose ()
    equal ("cleanup complete", 0.) (stageLog[stageLog.Count - 1], host.childNodes.length)

let private matchSameFlush () =
    use graph = new Graph ()
    stageLog.Clear ()
    stageRenders.Clear ()
    let stage = graph.Run (fun () -> Api.createSignal (Working 1))
    let host = host ()
    let mounted = mount graph host (Component.render staged stage)

    graph.Run (fun () ->
        Api.batch (fun () ->
            stage.Value <- Complete "x"
            stage.Value <- Working 2))

    equal ("working 2", [ "setup working" ]) (host.textContent, List.ofSeq stageLog)

    graph.Run (fun () ->
        Api.batch (fun () ->
            stage.Value <- Working 3
            stage.Value <- Complete "y"))

    equal ("complete y", [ "setup working"; "setup complete"; "cleanup working" ]) (host.textContent, List.ofSeq stageLog)
    equal [ "working 1"; "working 2" ] (List.ofSeq stageRenders)
    mounted.Dispose ()

let private matchTwin () =
    use graph = new Graph ()
    stageLog.Clear ()
    let stage = graph.Run (fun () -> Api.createSignal (Working 1))
    let first, second = host (), host ()
    let templateMount = mount graph first (Component.render staged stage)
    let twinMount = mount graph second (Component.renderWith Program.ops staged stage)
    equal (withoutComments second, "<div><span>working 1</span></div>") (withoutComments first, withoutComments second)
    graph.Run (fun () -> stage.Value <- Working 2)
    equal (withoutComments second, "<div><span>working 2</span></div>") (withoutComments first, withoutComments second)
    graph.Run (fun () -> stage.Value <- Complete "ok")
    equal (withoutComments second, "<div><span>complete ok</span></div>") (withoutComments first, withoutComments second)
    templateMount.Dispose ()
    twinMount.Dispose ()
    equal (0., 0.) (first.childNodes.length, second.childNodes.length)

let mutable private haltDuringSetup: unit -> unit = ignore

let private haltingArm =
    Component.define
        (fun setup (_: unit -> string) ->
            stageLog.Add "setup halting"
            setup.OnCleanup (fun () -> stageLog.Add "cleanup halting")
            haltDuringSetup ())
        (fun t -> t.span [||] [| t.text "halting" |])

let private halting =
    Component.define (fun _ (stage: Signal<Stage>) -> stage) (fun t ->
        t.div [||] [| t.matchWith (fun stage -> stage.Value) (stageArmsWith haltingArm t) |])

let private disposedMidSwitch () =
    use graph = new Graph ()
    stageLog.Clear ()
    let stage = graph.Run (fun () -> Api.createSignal (Working 1))
    let host = host ()
    let reported = ResizeArray<exn>()

    let mounted =
        Mounting.mount graph None reported.AddRange host None (fun context -> Program.run (Component.render halting stage) context.Backend)

    haltDuringSetup <- fun () -> mounted.Dispose ()

    try
        graph.Run (fun () -> stage.Value <- Complete "x")
    finally
        haltDuringSetup <- ignore

    equal (0., 0) (host.childNodes.length, reported.Count)

    equal [ "cleanup halting"; "cleanup working"; "setup halting"; "setup working" ] (List.sort (List.ofSeq stageLog))

let private stagedServer =
    Component.define (fun _ (stage: Stage) -> stage) (fun t -> t.div [||] [| t.matchWith id (stageArms t) |])

let private matchMarkup () =
    equal "<div><!--[--><span>working 3</span><!--]--></div>" (Html.render Omit (Program.run (Component.render stagedServer (Working 3))))

let private decadeLog = ResizeArray<string>()

let private decadeArm =
    Component.define
        (fun setup (decade: unit -> int) ->
            let initial = decade ()
            decadeLog.Add ("setup " + string initial)
            setup.OnCleanup (fun () -> decadeLog.Add ("cleanup " + string initial))
            decade)
        (fun t -> t.span [||] [| t.text (fun decade -> string (decade ())) |])

let private decades =
    Component.define (fun _ (value: Signal<int>) -> value) (fun t ->
        t.div
            [||]
            [|
                t.branch (fun value -> value.Value / 10) (fun decade -> t.arm decadeArm decade)
            |])

let private branchSwitching () =
    use graph = new Graph ()
    decadeLog.Clear ()
    let value = graph.Run (fun () -> Api.createSignal 11)
    let host = host ()
    let mounted = mount graph host (Component.render decades value)
    graph.Run (fun () -> value.Value <- 12)
    equal ("1", [ "setup 1" ]) (host.textContent, List.ofSeq decadeLog)
    graph.Run (fun () -> value.Value <- 21)
    equal ("2", [ "setup 1"; "setup 2"; "cleanup 1" ]) (host.textContent, List.ofSeq decadeLog)
    mounted.Dispose ()
    equal ([ "setup 1"; "setup 2"; "cleanup 1"; "cleanup 2" ], 0.) (List.ofSeq decadeLog, host.childNodes.length)

let private branchTwin () =
    use graph = new Graph ()
    decadeLog.Clear ()
    let value = graph.Run (fun () -> Api.createSignal 11)
    let first, second = host (), host ()
    let templateMount = mount graph first (Component.render decades value)
    let twinMount = mount graph second (Component.renderWith Program.ops decades value)
    equal (withoutComments second, "<div><span>1</span></div>") (withoutComments first, withoutComments second)
    graph.Run (fun () -> value.Value <- 12)
    equal (withoutComments second, "<div><span>1</span></div>") (withoutComments first, withoutComments second)
    graph.Run (fun () -> value.Value <- 21)
    equal (withoutComments second, "<div><span>2</span></div>") (withoutComments first, withoutComments second)
    templateMount.Dispose ()
    twinMount.Dispose ()
    equal (0., 0.) (first.childNodes.length, second.childNodes.length)

let private branchMarkup () =
    use graph = new Graph ()
    let value = graph.Run (fun () -> Api.createSignal 42)

    equal "<div><!--[--><span>4</span><!--]--></div>" (graph.Run (fun () -> Html.render Omit (Program.run (Component.render decades value))))

type Listed = { Id: int; Text: string }

let private listedLog = ResizeArray<string>()

let private listedRow =
    Component.define
        (fun setup (item: unit -> Listed) ->
            let id = (item ()).Id
            listedLog.Add ("setup " + string id)
            setup.OnCleanup (fun () -> listedLog.Add ("cleanup " + string id))
            item)
        (fun t ->
            t.element
                (HtmlTag "li")
                [|
                    t.Events.onClick (fun item _ -> listedLog.Add ("click " + string (item ()).Id))
                |]
                [| t.text (fun item -> (item ()).Text) |])

let private listed =
    Component.define (fun _ (items: Signal<Listed array>) -> items) (fun t ->
        t.element
            (HtmlTag "ul")
            [||]
            [|
                t.each (fun (item: Listed) -> item.Id) (fun items -> items.Value) listedRow
            |])

let private rootListed =
    Component.define (fun _ (items: Signal<Listed array>) -> items) (fun t ->
        t.fragment
            [|
                t.each (fun (item: Listed) -> item.Id) (fun items -> items.Value) listedRow
            |])

let private eachRows () =
    use graph = new Graph ()
    listedLog.Clear ()

    let items =
        graph.Run (fun () -> Api.createSignal [| { Id = 1; Text = "a" }; { Id = 2; Text = "b" } |])

    let host = host ()
    let mounted = mount graph host (Component.render listed items)
    let first = host.querySelector "li" |> unbox<HTMLElement>
    equal "ab" host.textContent
    graph.Run (fun () -> items.Value <- [| { Id = 2; Text = "B" }; { Id = 1; Text = "a" } |])
    equal ("Ba", true) (host.textContent, obj.ReferenceEquals (first, host.querySelectorAll("li").item 1.))
    first.click ()
    graph.Run (fun () -> items.Value <- [| { Id = 1; Text = "a" } |])
    equal "a" host.textContent
    mounted.Dispose ()

    equal ([ "setup 1"; "setup 2"; "click 1"; "cleanup 2"; "cleanup 1" ], 0.) (List.ofSeq listedLog, host.childNodes.length)

let private eachDuplicates () =
    use graph = new Graph ()
    let first, second = host (), host ()
    first.innerHTML <- "<span>keep</span>"

    let duplicated =
        graph.Run (fun () -> Api.createSignal [| { Id = 1; Text = "a" }; { Id = 1; Text = "b" } |])

    let mutable rejected = false

    try
        mount graph first (Component.render listed duplicated)
        |> ignore
    with :? System.ArgumentException ->
        rejected <- true

    equal (true, "<span>keep</span>") (rejected, first.innerHTML)

    let items =
        graph.Run (fun () -> Api.createSignal [| { Id = 1; Text = "a" }; { Id = 2; Text = "b" } |])

    let reported = ResizeArray<exn>()

    let mounted =
        Mounting.mount graph None reported.AddRange second None (fun context -> Program.run (Component.render listed items) context.Backend)

    graph.Run (fun () -> items.Value <- [| { Id = 3; Text = "c" }; { Id = 3; Text = "d" } |])
    equal ("ab", 1, true) (second.textContent, reported.Count, reported[0] :? System.ArgumentException)
    graph.Run (fun () -> items.Value <- [| { Id = 2; Text = "b" } |])
    equal ("b", 1) (second.textContent, reported.Count)
    mounted.Dispose ()

let private eachEmpty () =
    use graph = new Graph ()
    listedLog.Clear ()
    let nested = graph.Run (fun () -> Api.createSignal ([||]: Listed array))
    let rooted = graph.Run (fun () -> Api.createSignal ([||]: Listed array))
    let first, second = host (), host ()
    let a = mount graph first (Component.render listed nested)
    let b = mount graph second (Component.render rootListed rooted)
    let list = first.querySelector "ul" |> unbox<HTMLElement>
    equal (2., 2, "") (list.childNodes.length, regionMarkers second, second.textContent)

    graph.Run (fun () ->
        nested.Value <- [| { Id = 1; Text = "a" } |]
        rooted.Value <- [| { Id = 2; Text = "b" } |])

    equal ("a", "b") (first.textContent, second.textContent)
    (second.querySelector "li" |> unbox<HTMLElement>).click()
    equal true (listedLog.Contains "click 2")

    graph.Run (fun () ->
        nested.Value <- [||]
        rooted.Value <- [||])

    equal (2., 2, "", "") (list.childNodes.length, regionMarkers second, first.textContent, second.textContent)
    a.Dispose ()
    b.Dispose ()
    equal (0., 0.) (first.childNodes.length, second.childNodes.length)

let private eachTwin () =
    use graph = new Graph ()
    listedLog.Clear ()

    let items =
        graph.Run (fun () -> Api.createSignal [| { Id = 1; Text = "a" }; { Id = 2; Text = "b" } |])

    let first, second = host (), host ()
    let templateMount = mount graph first (Component.render listed items)
    let twinMount = mount graph second (Component.renderWith Program.ops listed items)

    let matches expected =
        equal (withoutComments second, expected) (withoutComments first, withoutComments second)

    matches "<ul><li>a</li><li>b</li></ul>"
    graph.Run (fun () -> items.Value <- [| { Id = 1; Text = "a" }; { Id = 3; Text = "c" }; { Id = 2; Text = "b" } |])
    matches "<ul><li>a</li><li>c</li><li>b</li></ul>"
    graph.Run (fun () -> items.Value <- [| { Id = 1; Text = "a" }; { Id = 2; Text = "b" } |])
    matches "<ul><li>a</li><li>b</li></ul>"
    graph.Run (fun () -> items.Value <- [| { Id = 2; Text = "b" }; { Id = 1; Text = "a" } |])
    matches "<ul><li>b</li><li>a</li></ul>"
    let retained = first.querySelector "li" |> unbox<HTMLElement>
    graph.Run (fun () -> items.Value <- [| { Id = 2; Text = "B" }; { Id = 1; Text = "a" } |])
    matches "<ul><li>B</li><li>a</li></ul>"
    equal true (obj.ReferenceEquals (retained, first.querySelector "li"))
    templateMount.Dispose ()
    twinMount.Dispose ()
    equal (0., 0.) (first.childNodes.length, second.childNodes.length)

type Shelf =
    {
        Label: Signal<string>
        Left: Signal<Listed array>
        Right: Signal<Listed array>
    }

let private shelved =
    Component.define (fun _ (shelf: Shelf) -> shelf) (fun t ->
        t.fragment
            [|
                t.text "<"
                t.each (fun (item: Listed) -> item.Id) (fun shelf -> shelf.Left.Value) listedRow
                t.text (fun shelf -> shelf.Label.Value)
                t.each (fun (item: Listed) -> item.Id) (fun shelf -> shelf.Right.Value) listedRow
            |])

let private rootRegions () =
    use graph = new Graph ()
    listedLog.Clear ()

    let shelf =
        graph.Run (fun () ->
            {
                Label = Api.createSignal "|"
                Left = Api.createSignal [| { Id = 1; Text = "a" } |]
                Right = Api.createSignal [| { Id = 2; Text = "b" }; { Id = 3; Text = "c" } |]
            })

    let first, second = host (), host ()
    let mounted = mount graph first (Component.render shelved shelf)
    equal ("<a|bc", 4) (first.textContent, regionMarkers first)

    let moved =
        first.querySelectorAll("li").item 2.
        |> unbox<HTMLElement>

    graph.Run (fun () ->
        shelf.Left.Value <- [| { Id = 4; Text = "d" }; { Id = 1; Text = "A" } |]
        shelf.Right.Value <- [| { Id = 3; Text = "c" }; { Id = 2; Text = "b" } |]
        shelf.Label.Value <- "/")

    equal ("<dA/cb", true) (first.textContent, obj.ReferenceEquals (moved, first.querySelectorAll("li").item 2.))
    mounted.MoveBefore (second, None)
    equal (0., "<dA/cb", 4) (first.childNodes.length, second.textContent, regionMarkers second)
    moved.click ()

    graph.Run (fun () ->
        shelf.Left.Value <- [||]
        shelf.Right.Value <- [| { Id = 2; Text = "b" }; { Id = 5; Text = "e" } |])

    equal ("</be", 4) (second.textContent, regionMarkers second)
    mounted.Dispose ()
    equal (0., 0.) (first.childNodes.length, second.childNodes.length)

    equal
        (List.sort
            [
                "setup 1"
                "setup 2"
                "setup 3"
                "setup 4"
                "setup 5"
                "click 3"
                "cleanup 1"
                "cleanup 2"
                "cleanup 3"
                "cleanup 4"
                "cleanup 5"
            ])
        (List.sort (List.ofSeq listedLog))

type Job = { JobId: int; Stage: Stage }

let private jobRow =
    Component.define (fun _ (job: unit -> Job) -> job) (fun t ->
        t.element (HtmlTag "li") [||] [| t.matchWith (fun job -> (job ()).Stage) (stageArms t) |])

let private jobs =
    Component.define (fun _ (items: Signal<Job array>) -> items) (fun t ->
        t.element (HtmlTag "ul") [||] [| t.each (fun (job: Job) -> job.JobId) (fun items -> items.Value) jobRow |])

let private nestedRegions () =
    use graph = new Graph ()
    stageLog.Clear ()

    let items =
        graph.Run (fun () -> Api.createSignal [| { JobId = 1; Stage = Waiting }; { JobId = 2; Stage = Working 1 } |])

    let host = host ()
    let mounted = mount graph host (Component.render jobs items)

    let second =
        host.querySelectorAll("li").item 1.
        |> unbox<HTMLElement>

    equal "waitingworking 1" host.textContent

    graph.Run (fun () -> items.Value <- [| { JobId = 2; Stage = Working 2 }; { JobId = 1; Stage = Complete "ok" } |])

    equal ("working 2complete ok", true) (host.textContent, obj.ReferenceEquals (second, host.querySelector "li" |> unbox<HTMLElement>))
    graph.Run (fun () -> items.Value <- [| { JobId = 1; Stage = Complete "ok" } |])
    equal "complete ok" host.textContent
    mounted.Dispose ()
    equal 0. host.childNodes.length

    equal
        [
            "setup waiting"
            "setup working"
            "setup complete"
            "cleanup waiting"
            "cleanup working"
            "cleanup complete"
        ]
        (List.ofSeq stageLog)

let private eachMarkup () =
    use graph = new Graph ()

    let items =
        graph.Run (fun () -> Api.createSignal [| { Id = 1; Text = "a" }; { Id = 2; Text = "<b>" } |])

    equal
        "<ul><!--[--><li>a</li><li>&lt;b&gt;</li><!--]--></ul>"
        (graph.Run (fun () -> Html.render Omit (Program.run (Component.render listed items))))

/// <summary>Checks template compilation and DOM materialisation.</summary>
let cases =
    [|
        "walk programs reach dynamic nodes in document order", walkPrograms
        "a compiled skeleton round-trips and records every dynamic node", compiledSkeleton
        "a template renders the same DOM as its createComponent twin", twinEquality
        "slot writes happen only for changed values", changedSlotsOnly
        "a throwing reader fails only its slot", readerFailure
        "a slot failing on first render is reported without failing the mount", initialReaderFailure
        "a throwing reporter leaves no failure for the next successful run", throwingReporter
        "a recurring slot failure object is reported again after the slot recovers", recoveredFailureRecurs
        "a throwing setter rolls back the mount", setterRollback
        "template cleanups run in use order without an instance owner", cleanupOrder
        "adjacent text slots keep separate nodes", adjacentTexts
        "a definition mounted twice keeps instances independent", compiledOnce
        "a template inside a removed keyed row stops updating", keyedRows
        "a null text value renders empty", nullText
        "a static template mounts without effects", staticTemplate
        "an isolated slot re-runs only on its own source", isolatedSlots
        "source slots follow their signal and memo", sourceSlots
        "template click handlers receive the instance and stop after disposal", clicks
        "template handlers dispatch after a move", movedClicks
        "focus uses a direct listener beside a delegated click", focusBesideClick
        "queued mounts render templates through expansion", queuedFallback
        "the Browser interpreter renders templates through expansion", browserInterpreter
        "prepared skeletons reject templates", preparedRejects
        "server markup under Fable matches the portable expectation", serverMarkup
        "markup restructured by the HTML parser fails compilation", restructuredMarkup
        "a pending or failed text slot renders empty", unsetText
        "null attribute values remove the attribute", nullAttributes
        "a later setter failure is reported and keeps the other slots", laterSetterFailure
        "a template whose single root is a text slot mounts, updates and disposes", textRoot
        "a single root reached through a fragment mounts, updates and disposes", fragmentRoot
        "a multi-root template moves together and disposes empty", movedRoots
        "walk programs begin with a collect for the empty path", walkProgramEmptyPath
        "a single-root definition compiles to an element skeleton", singleRootCompiled
        "view hole server markup under Fable matches .NET", holeMarkup
        "a slot after a view hole is indexed past both markers without a separator", holeSlotPaths
        "a view hole mounts its view between region markers and disposes it", viewHole
        "a view hole renders the same DOM as its default expansion", holeTwin
        "an empty view hole keeps its markers", emptyHole
        "a view hole reads its view once per instance", viewReadOnce
        "a failing view hole rolls back the mount", failingHole
        "a root-level view hole dispatches clicks before and after a move", rootHoleClicks
        "queued mounts and the Browser interpreter render view holes through expansion", holeFallbacks
        "matchWith switches arms by case and updates payloads in place", matchSwitching
        "a match changed twice in one flush settles on its final case", matchSameFlush
        "matchWith renders the same DOM as its default expansion across a switch and a payload change", matchTwin
        "disposing the mount during an arm's setup ends the switch cleanly", disposedMidSwitch
        "matchWith server markup under Fable matches .NET", matchMarkup
        "branch remounts on a key change and keeps the arm for an equal key", branchSwitching
        "branch renders the same DOM as its default expansion across a key change", branchTwin
        "branch server markup under Fable matches .NET", branchMarkup
        "each renders template rows, retains them by key and dispatches their clicks", eachRows
        "duplicate each keys fail the mount, are reported on update and keep the rows", eachDuplicates
        "an empty each region keeps its markers, fills and empties", eachEmpty
        "each renders the same DOM as its default expansion across insert, remove, reorder and an item change", eachTwin
        "root-level each regions beside other roots update, reorder, move and dispose", rootRegions
        "a matchWith inside each rows follows, moves and leaves with its row", nestedRegions
        "each server markup under Fable matches .NET", eachMarkup
    |]
