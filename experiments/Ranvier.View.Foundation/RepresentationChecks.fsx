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
                [ Attribute ("id", "\"&<>") ]
                [
                    ops.Text "<>&\"'"
                    ops.Fragment [ ops.Empty; ops.Bind (=) (fun () -> "&live") ]
                ])

    equal 0 setup
    let expected = "<div id=\"&quot;&amp;&lt;&gt;\">&lt;&gt;&amp;\"'&amp;live</div>"
    equal expected (render Reject view)
    equal expected (render Reject view)
    equal 2 setup
    equal 2 clean

    let browser =
        ops.Element Button [ Browser (fun _ -> failwith "browser hook executed on server") ] []

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
    equal "<input value=\"&quot;&amp;\">" (render Reject (ops.Element Input [ InputValue "\"&" ] []))
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
            ops.Element Div [ Attribute ("id", "first"); Attribute ("id", "last") ] []

        equal "<div id=\"last\"></div>" (Html.render Reject (run view))
        let input = ops.Element Input [ InputValue "first"; InputValue "last" ] []
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
