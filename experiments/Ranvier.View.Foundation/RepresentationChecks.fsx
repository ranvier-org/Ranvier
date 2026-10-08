#if INTERACTIVE
#load "Representation.fs"
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
