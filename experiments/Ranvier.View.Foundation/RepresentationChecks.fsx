#if INTERACTIVE
#r "../../src/Ranvier/bin/Release/net10.0/Ranvier.dll"
#load "Representation.fs"
#load "Fixtures.fs"
#endif

open Ranvier.View.Foundation.Representation

let equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

let exercise name (ops: Operations<'view>) render =
    let mutable setup = 0
    let mutable clean = 0

    let view =
        ops.Component (fun context ->
            setup <- setup + 1
            context.OnCleanup (fun () -> clean <- clean + 1)

            ops.Element
                Div
                [| Attribute ("id", "\"&<>") |]
                [|
                    ops.Text "<>&\"'"
                    ops.Fragment [| ops.Empty; ops.Bind (=) (fun () -> "&live") |]
                |])

    equal 0 setup
    let expected = "<div id=\"&quot;&amp;&lt;&gt;\">&lt;&gt;&amp;\"'&amp;live</div>"
    equal expected (render Reject view)
    equal expected (render Reject view)
    equal 2 setup
    equal 2 clean

    let browser =
        ops.Element Button [| browserModifier (fun _ -> failwith "browser hook executed on server") |] [||]

    equal "<button></button>" (render Omit browser)
    let mutable rejected = false

    try
        render Reject browser |> ignore
    with _ ->
        rejected <- true

    equal true rejected

    let failing =
        ops.Component (fun context ->
            context.OnCleanup (fun () -> clean <- clean + 1)
            failwith "setup")

    let mutable failed = false

    try
        render Reject failing |> ignore
    with error ->
        equal "setup" error.Message
        failed <- true

    equal true failed
    equal 3 clean
    equal "<input value=\"&quot;&amp;\">" (render Reject (ops.Element Input [| InputValue "\"&" |] [||]))
    printfn "PASS %s portable equivalence, reuse, browser policy and failure unwind" name

exercise "tree" Tree.ops (fun policy view -> Html.render policy (Tree.run view))
exercise "program" Program.ops (fun policy view -> Html.render policy (Program.run view))

let sugarChecks ops run =
    for workload in [ "static"; "wide"; "fragments"; "reactive" ] do
        let output sugar =
            Html.render Reject (run (Ranvier.View.Foundation.Fixtures.create ops workload sugar (fun () -> "live")))

        let direct = output "direct"
        equal true (direct.StartsWith ("<div"))
        equal direct (output "functions")
        equal direct (output "builder")

    printfn "PASS direct/function/builder output equivalence"

sugarChecks Tree.ops Tree.run
sugarChecks Program.ops Program.run

let edgeChecks name (ops: Operations<'view>) run =
    let failures = ResizeArray<string>()

    let check label action =
        try
            action ()
        with error ->
            failures.Add (label + ": " + error.Message)

    check "last modifier wins" (fun () ->
        let view =
            ops.Element Div [| Attribute ("id", "first"); Attribute ("id", "last") |] [||]

        equal "<div id=\"last\"></div>" (Html.render Reject (run view))
        let input = ops.Element Input [| InputValue "first"; InputValue "last" |] [||]
        equal "<input value=\"last\">" (Html.render Reject (run input)))

    check "setup and cleanup failures are preserved" (fun () ->
        let original = System.InvalidOperationException "setup"
        let mutable completed = false

        let view =
            ops.Component (fun context ->
                context.OnCleanup (fun () -> failwith "cleanup")
                context.OnCleanup (fun () -> completed <- true)
                raise original)

        let mutable caught = false

        try
            Html.render Reject (run view) |> ignore
        with :? System.AggregateException as error ->
            equal
                [ "setup"; "cleanup" ]
                (error.InnerExceptions
                 |> Seq.map _.Message
                 |> List.ofSeq)

            equal true (obj.ReferenceEquals (original, error.InnerExceptions[0]))
            caught <- true

        equal true caught
        equal true completed)

    for failure in failures do
        printfn "FAIL %s %s" name failure

    equal 0 failures.Count
    printfn "PASS %s portable modifier/failure edges" name

edgeChecks "tree" Tree.ops Tree.run
edgeChecks "program" Program.ops Program.run

#if !INTERACTIVE
if
    System.Environment.GetCommandLineArgs ()
    |> Array.contains "--allocations"
then
    let measure representation ops =
        for workload in [ "static"; "wide"; "fragments"; "reactive" ] do
            for sugar in [ "direct"; "functions"; "builder" ] do
                let mutable sink = ops.Empty
                let samples = ResizeArray<float>()

                for round in -3 .. 6 do
                    let before = System.GC.GetAllocatedBytesForCurrentThread ()

                    for _ in 1..100 do
                        sink <- Ranvier.View.Foundation.Fixtures.create ops workload sugar (fun () -> "live")

                    let bytes =
                        float (
                            System.GC.GetAllocatedBytesForCurrentThread ()
                            - before
                        )
                        / 100.

                    System.GC.KeepAlive sink

                    if round >= 0 then
                        samples.Add bytes

                let median = (samples |> Seq.sort |> Seq.toArray)[3]

                printfn
                    "{\"representation\":\"%s\",\"workload\":\"%s\",\"sugar\":\"%s\",\"bytesPerDefinition\":%f}"
                    representation
                    workload
                    sugar
                    median

    measure "tree" Tree.ops
    measure "program" Program.ops
#endif

open Ranvier.View.Foundation.Template

type GreetingProps = { Title: string; Name: string }

let greeting =
    Component.define (fun _ (props: GreetingProps) -> props) (fun t ->
        t.div
            [| t.Attrs.className "panel"; t.Attrs.title (fun r -> r.Title) |]
            [| t.text "Hello "; t.text (fun r -> r.Name); t.text (fun _ -> "!") |])

let greetingProps = { Title = "T &"; Name = "<Ada>" }

let render policy view =
    Html.render policy (Program.run view)

// Separators keep adjacent texts distinct; values are escaped as the HTML backend escapes them.
equal "<div class=\"panel\" title=\"T &amp;\">Hello <!--!-->&lt;Ada&gt;<!--!-->!</div>" (render Omit (Component.render greeting greetingProps))

let named =
    Component.define (fun _ (props: GreetingProps) -> props) (fun t ->
        t.div [| t.Attrs.className "panel"; t.Attrs.title (fun r -> r.Title) |] [| t.span [||] [| t.text (fun r -> r.Name) |] |])

let namedTwin (props: GreetingProps) =
    Program.createComponent (fun _ ->
        Program.element
            Div
            [|
                Attribute ("class", "panel")
                ReactiveAttribute ("title", fun () -> Some props.Title)
            |]
            [|
                Program.element (HtmlTag "span") [||] [| Program.bind (=) (fun () -> props.Name) |]
            |])

// A template and its createComponent twin produce identical server HTML.
equal (render Omit (namedTwin greetingProps)) (render Omit (Component.render named greetingProps))

let clicking =
    Component.define (fun _ (props: GreetingProps) -> props) (fun t -> t.button [| t.Events.on ("click", fun _ _ -> ()) |] [||])

equal "<button></button>" (render Omit (Component.render clicking greetingProps))

let mutable rejected = false

try
    render Reject (Component.render clicking greetingProps)
    |> ignore
with :? System.InvalidOperationException ->
    rejected <- true

equal true rejected

// The tree representation renders a template through the default expansion.
equal
    "<div class=\"panel\" title=\"T &amp;\"><span>&lt;Ada&gt;</span></div>"
    (Html.render Omit (Tree.run (Component.renderWith Tree.ops named greetingProps)))

let nullTitle =
    Component.define (fun _ (props: GreetingProps) -> props) (fun t -> t.div [| t.Attrs.title (fun _ -> (null: string)) |] [||])

// A null attribute value omits the attribute.
equal "<div></div>" (render Omit (Component.render nullTitle greetingProps))
equal "<div></div>" (Html.render Omit (Tree.run (Component.renderWith Tree.ops nullTitle greetingProps)))

printfn "PASS template portable rendering"

let stripMarkers (html: string) =
    System.Text.RegularExpressions.Regex.Replace (html, "<!--.*?-->", "")

type HoleProps = { Caption: string; Body: Program.View }

let holed =
    Component.define (fun _ (props: HoleProps) -> props) (fun t ->
        t.div [| t.Attrs.className "card" |] [| t.text (fun r -> r.Caption); t.view (fun r -> r.Body) |])

let holeProps =
    {
        Caption = "A"
        Body = Program.element (HtmlTag "em") [||] [| Program.text "<b>" |]
    }

// A view hole renders between region markers; text beside a marker has no separator.
equal "<div class=\"card\">A<!--[--><em>&lt;b&gt;</em><!--]--></div>" (render Omit (Component.render holed holeProps))

// The default expansion renders the same HTML without markers.
equal (stripMarkers (render Omit (Component.render holed holeProps))) (Html.render Omit (Tree.run (Component.renderWith Tree.ops holed holeProps)))

// An empty view hole keeps its markers.
equal
    "<div class=\"card\">B<!--[--><!--]--></div>"
    (render
        Omit
        (Component.render
            holed
            {
                Caption = "B"
                Body = Program.fragment [||]
            }))

let mutable holeRejected = false

try
    render
        Reject
        (Component.render
            holed
            {
                Caption = "C"
                Body = Program.element Button [| listenModifier "click" ignore |] [||]
            })
    |> ignore
with :? System.InvalidOperationException ->
    holeRejected <- true

// A browser-only part inside a view hole follows the Reject policy.
equal true holeRejected

printfn "PASS template view holes"

type Stage =
    | Waiting
    | Working of int
    | Complete of string

let waitingArm =
    Component.define (fun _ (_: unit -> unit) -> ()) (fun t -> t.span [||] [| t.text "waiting" |])

let workingArm =
    Component.define (fun _ (progress: unit -> int) -> progress) (fun t ->
        t.span [||] [| t.text (fun progress -> "working " + string (progress ())) |])

let completeArm =
    Component.define (fun _ (result: unit -> string) -> result) (fun t -> t.span [||] [| t.text (fun result -> "complete " + result ()) |])

let staged =
    Component.define (fun _ (stage: Stage) -> stage) (fun t ->
        t.div
            [||]
            [|
                t.matchWith id (function
                    | Waiting -> t.arm waitingArm ()
                    | Working progress -> t.arm workingArm progress
                    | Complete result -> t.arm completeArm result)
            |])

equal "<div><!--[--><span>working 3</span><!--]--></div>" (render Omit (Component.render staged (Working 3)))
equal "<div><!--[--><span>complete &lt;ok&gt;</span><!--]--></div>" (render Omit (Component.render staged (Complete "<ok>")))

equal (stripMarkers (render Omit (Component.render staged Waiting))) (Html.render Omit (Tree.run (Component.renderWith Tree.ops staged Waiting)))

printfn "PASS template matchWith"

let decadeArm =
    Component.define (fun _ (decade: unit -> int) -> decade) (fun t -> t.span [||] [| t.text (fun decade -> string (decade ())) |])

let decades =
    Component.define (fun _ (value: int) -> value) (fun t ->
        t.div [||] [| t.branch (fun value -> value / 10) (fun decade -> t.arm decadeArm decade) |])

equal "<div><!--[--><span>4</span><!--]--></div>" (render Omit (Component.render decades 42))
equal "<div><span>4</span></div>" (Html.render Omit (Tree.run (Component.renderWith Tree.ops decades 42)))

printfn "PASS template branch"
