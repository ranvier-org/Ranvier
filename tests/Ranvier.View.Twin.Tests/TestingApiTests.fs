module Ranvier.View.Twin.Tests.TestingApiTests

open Expecto
open Ranvier.View.Twin
open Ranvier.View.Testing

[<Tests>]
let tests =
    testList
        "Testing API"
        [
            testCase "check clicks only when the state differs"
            <| fun () ->
                let host = TwinHost.Create ()
                use _ = host.Document
                let root = host.Document.CreateElement "div"
                Html.parseInto root "<input type=\"checkbox\" checked>"
                let clicks = ref 0

                Events.listen root "click" true (fun _ -> clicks.Value <- clicks.Value + 1)
                |> ignore

                Interact.check root.Native "input" true
                Expect.equal clicks.Value 0 "already checked"
                Interact.check root.Native "input" false
                Expect.equal clicks.Value 1 "one click"

            testCase "input fires beforeinput and skips the write when it is cancelled"
            <| fun () ->
                let host = TwinHost.Create ()
                use _ = host.Document
                let root = host.Document.CreateElement "div"
                Html.parseInto root "<input>"

                Events.listen root "beforeinput" true (fun e -> (Events.eventOf e).PreventDefault())
                |> ignore

                Interact.input root.Native "input" "x"
                Expect.equal (root.FirstChild :?> TwinElement).Value "" "not written"

            testCase "select sets the value, then fires input and change"
            <| fun () ->
                let host = TwinHost.Create ()
                use _ = host.Document
                let root = host.Document.CreateElement "div"
                Html.parseInto root "<select><option value=\"a\">A</option><option value=\"b\">B</option></select>"
                let log = ResizeArray<string>()

                Events.listen root "input" true (fun _ -> log.Add "input")
                |> ignore

                Events.listen root "change" true (fun _ -> log.Add "change")
                |> ignore

                Interact.select root.Native "select" "b"

                Expect.equal ((root.FirstChild :?> TwinElement).Value, List.ofSeq log) ("b", [ "input"; "change" ]) "selected"
        ]
