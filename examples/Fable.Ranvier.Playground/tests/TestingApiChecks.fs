module TestingApiChecks

open global.Ranvier
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open Ranvier.View.Testing

type private FormState =
    {
        Name: Signal<string>
        Agreed: Signal<bool>
        Key: Signal<string>
    }

let private targetOf (e: Event) : HTMLInputElement =
    unbox<HTMLInputElement> e.target.Value

let private form =
    Component.define
        (fun _ () ->
            {
                Name = Api.createSignal ""
                Agreed = Api.createSignal false
                Key = Api.createSignal ""
            })
        (fun t ->
            t.div
                [||]
                [|
                    t.input
                        [|
                            t.Attrs.className "name"
                            t.Events.on ("input", fun s e -> s.Name.Value <- (targetOf e).value)
                        |]
                    t.input
                        [|
                            t.Attrs.className "agree"
                            t.Attrs.Input.``type`` "checkbox"
                            t.Events.on ("change", fun s e -> s.Agreed.Value <- (targetOf e).``checked``)
                        |]
                    t.input
                        [|
                            t.Attrs.className "keys"
                            t.Events.on ("keydown", fun s e -> s.Key.Value <- (unbox<KeyboardEvent> e).key)
                        |]
                    t.p
                        [||]
                        [|
                            t.text (fun s ->
                                s.Name.Value
                                + "|"
                                + (if s.Agreed.Value then "yes" else "no")
                                + "|"
                                + s.Key.Value)
                        |]
                |])

// Connected to the document: a checkbox fires input and change only when it is connected.
let private mounted () =
    let host = (Conformance.document ()).createElement "main"

    (Conformance.document ()).body.appendChild host
    |> ignore

    let graph = new Graph ()

    let mount =
        Mounting.mount graph None (fun errors -> failwithf "%A" errors) host None (fun context ->
            Program.run (Component.render form ()) context.Backend)

    host :> Node, graph, mount

let private interactions () =
    let root, graph, mount = mounted ()
    Interact.input root "input.name" "Ada"
    Interact.check root "input.agree" true
    Interact.key root "input.keys" "Enter"
    Expect.text root "p" "Ada|yes|Enter"
    Expect.count root "input" 3
    Expect.attribute root "input.agree" "type" (Some "checkbox")
    Expect.value root "input.name" "Ada"
    mount.Dispose ()
    graph.Dispose ()
    (unbox<Element> root).remove()

let private failures () =
    let root, graph, mount = mounted ()

    let raised =
        try
            Query.get root "li" |> ignore
            false
        with Query.QueryException (selector, count, _) ->
            selector = "li" && count = 0

    if not raised then
        failwith "Query.get did not raise for zero matches"

    let failed =
        try
            Expect.text root "p" "nope"
            false
        with Expect.ExpectationException message ->
            message.Contains "nope"

    if not failed then
        failwith "Expect.text did not raise"

    mount.Dispose ()
    graph.Dispose ()
    (unbox<Element> root).remove()

let private byText () =
    let root, graph, mount = mounted ()
    Interact.input root "input.name" "Zed"
    let p = Query.byText root "Zed|no|"

    if p.localName <> "p" then
        failwith "byText found the wrong element"

    mount.Dispose ()
    graph.Dispose ()
    (unbox<Element> root).remove()

let cases: Conformance.Case array =
    Conformance.ofChecks
        [|
            "interactions drive input, check and key and the expectations read the result", interactions
            "a query or expectation that fails raises with the selector", failures
            "byText returns the innermost element with the text", byText
        |]
