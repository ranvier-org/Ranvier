module Ranvier.View.Twin.Tests.BridgeTests

open System
open Expecto
open Fable.Core.TS.Dom
open Ranvier.View.Twin

[<Tests>]
let tests =
    testList
        "Bridge"
        [
            testCase "a node has one bridged object"
            <| fun () ->
                use document = new TwinDocument ()
                let div = document.CreateElement "div"
                Expect.isTrue (obj.ReferenceEquals (div.Native, div.Native)) "same object"
                Expect.equal (Bridge.nodeOf div.Native) (div :> TwinNode) "back to the node"

            testCase "the interface follows the tag"
            <| fun () ->
                use document = new TwinDocument ()
                Expect.isTrue ((document.CreateElement "input").Native :? HTMLInputElement) "input"
                Expect.isTrue ((document.CreateElement "template").Native :? HTMLTemplateElement) "template"
                Expect.isTrue ((document.CreateElement "button").Native :? HTMLButtonElement) "button"
                Expect.isTrue ((document.CreateElement "section").Native :? HTMLElement) "section"
                Expect.isTrue ((document.CreateText "x").Native :? Text) "text"
                Expect.isTrue ((document.CreateComment "x").Native :? Comment) "comment"
                Expect.isTrue ((document.CreateElement "input").Native :? ARIAMixin) "inherited interfaces"

            testCase "nullable returns are options and numbers are floats"
            <| fun () ->
                use document = new TwinDocument ()
                let div = (document.CreateElement "div").Native
                Expect.isNone div.firstChild "no child"
                Expect.equal div.nodeType 1.0 "nodeType"
                let text = document.CreateText "t"
                div.appendChild text.Native |> ignore
                Expect.isTrue (obj.ReferenceEquals (div.firstChild.Value, text.Native)) "Some child"

            testCase "generated property writers reach the form state"
            <| fun () ->
                use document = new TwinDocument ()
                let input = document.CreateElement "input"
                (input.Native :?> HTMLInputElement).value <- "x"
                (input.Native :?> HTMLInputElement).``checked`` <- true
                Expect.equal (input.Value, input.Checked) ("x", true) "written"

            testCase "an unimplemented member names its interface and member"
            <| fun () ->
                use document = new TwinDocument ()
                let element = (document.CreateElement "div").Native :?> HTMLElement

                Expect.throwsC (fun () -> element.offsetWidth |> ignore) (fun e ->
                    Expect.isTrue (e :? NotSupportedException) "NotSupportedException"
                    Expect.equal e.Message "The DOM twin does not implement HTMLElement.offsetWidth" "message")

            testCase "innerHTML parses and serialises"
            <| fun () ->
                use document = new TwinDocument ()
                let div = (document.CreateElement "div").Native :?> Element
                div.innerHTML <- "<b>x</b><!--m-->"
                Expect.equal div.innerHTML "<b>x</b><!--m-->" "round trip"
                Expect.equal div.childNodes.length 2.0 "children"

            testCase "served members are recorded by interface"
            <| fun () ->
                Bridge.resetServed ()
                use document = new TwinDocument ()

                (document.CreateElement "div").Native.nodeType
                |> ignore

                Expect.contains (Bridge.served ()) "Node.nodeType" "recorded"

            testCase "a call from another thread raises"
            <| fun () ->
                use document = new TwinDocument ()
                let div = (document.CreateElement "div").Native
                let failure = ref (null: exn)

                let thread =
                    Threading.Thread (fun () ->
                        try
                            div.nodeType |> ignore
                        with e ->
                            failure.Value <- e)

                thread.Start ()
                thread.Join ()
                Expect.isTrue (failure.Value :? InvalidOperationException) "InvalidOperationException"
        ]
