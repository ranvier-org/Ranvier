module Ranvier.View.Twin.Tests.TreeTests

open Expecto
open Ranvier.View.Twin

let private names (node: TwinNode) =
    node.Children
    |> List.map (fun c ->
        match c with
        | :? TwinElement as e -> e.LocalName
        | :? TwinCharacterData as t -> "#" + t.Data
        | _ -> "?")

[<Tests>]
let tests =
    testList
        "Tree"
        [
            testCase "a new document has html, head and body"
            <| fun () ->
                use document = new TwinDocument ()
                Expect.equal (names document) [ "html" ] "root"
                Expect.equal (names document.Html) [ "head"; "body" ] "html"
                Expect.isTrue document.Body.IsConnected "connected"

            testCase "insertBefore moves a node and keeps sibling links consistent"
            <| fun () ->
                use document = new TwinDocument ()

                let a, b, c =
                    document.CreateElement "a", document.CreateElement "b", document.CreateElement "c"

                for n in [ a; b; c ] do
                    document.Body.AppendChild n |> ignore

                document.Body.InsertBefore (c, a) |> ignore
                Expect.equal (names document.Body) [ "c"; "a"; "b" ] "moved"
                Expect.isNull c.PreviousSibling "first"
                Expect.equal a.PreviousSibling (c :> TwinNode) "back link"
                Expect.equal b.NextSibling null "last"

            testCase "inserting a fragment moves its children and empties it"
            <| fun () ->
                use document = new TwinDocument ()
                let fragment = document.CreateFragment ()

                fragment.AppendChild (document.CreateText "x")
                |> ignore

                fragment.AppendChild (document.CreateComment "y")
                |> ignore

                document.Body.AppendChild fragment |> ignore
                Expect.equal (names document.Body) [ "#x"; "#y" ] "children moved"
                Expect.isEmpty fragment.Children "fragment emptied"

            testCase "inserting an ancestor into its descendant is a HierarchyRequestError"
            <| fun () ->
                use document = new TwinDocument ()
                let outer = document.CreateElement "div"
                let inner = document.CreateElement "span"
                outer.AppendChild inner |> ignore

                Expect.throwsC (fun () -> inner.AppendChild outer |> ignore) (fun e ->
                    match e with
                    | TwinDomException (name, _) -> Expect.equal name "HierarchyRequestError" "name"
                    | other -> failtestf "unexpected %O" other)

            testCase "textContent concatenates descendant text and setting it replaces the children"
            <| fun () ->
                use document = new TwinDocument ()
                let div = document.CreateElement "div"

                div.AppendChild (document.CreateText "a")
                |> ignore

                div.AppendChild (document.CreateComment "skip")
                |> ignore

                let span = document.CreateElement "span"

                span.AppendChild (document.CreateText "b")
                |> ignore

                div.AppendChild span |> ignore
                Expect.equal div.TextContent "ab" "comments excluded"
                div.TextContent <- ""
                Expect.isEmpty div.Children "cleared"

            testCase "attributes keep insertion order and setAttribute lowercases the name"
            <| fun () ->
                use document = new TwinDocument ()
                let e = document.CreateElement "div"
                e.SetAttribute ("B", "1")
                e.SetAttribute ("a", "2")
                e.SetAttribute ("b", "3")
                Expect.equal e.Attributes [ "b", "3"; "a", "2" ] "order and replacement"

            testCase "an input's value reads the attribute until it is written"
            <| fun () ->
                use document = new TwinDocument ()
                let input = document.CreateElement "input"
                input.SetAttribute ("value", "a")
                Expect.equal input.Value "a" "attribute"
                input.Value <- "b"
                input.SetAttribute ("value", "c")
                Expect.equal input.Value "b" "dirty value"
                Expect.equal input.DefaultValue "c" "defaultValue reflects the attribute"

            testCase "a checkbox's checked follows the attribute until it is written"
            <| fun () ->
                use document = new TwinDocument ()
                let box = document.CreateElement "input"
                box.SetAttribute ("type", "checkbox")
                box.SetAttribute ("checked", "")
                Expect.isTrue box.Checked "attribute"
                box.Checked <- false
                box.SetAttribute ("checked", "")
                Expect.isFalse box.Checked "dirty checkedness"

            testCase "checking a radio unchecks the others of its group"
            <| fun () ->
                use document = new TwinDocument ()

                let radio name =
                    let r = document.CreateElement "input"
                    r.SetAttribute ("type", "radio")
                    r.SetAttribute ("name", name)
                    document.Body.AppendChild r |> ignore
                    r

                let a, b, other = radio "g", radio "g", radio "h"
                a.Checked <- true
                other.Checked <- true
                b.Checked <- true
                Expect.equal (a.Checked, b.Checked, other.Checked) (false, true, true) "group"

            testCase "a select's value is its first selected option's value"
            <| fun () ->
                use document = new TwinDocument ()
                let select = document.CreateElement "select"

                for v in [ "a"; "b" ] do
                    let o = document.CreateElement "option"
                    o.SetAttribute ("value", v)
                    select.AppendChild o |> ignore

                Expect.equal select.Value "a" "first option by default"
                select.Value <- "b"
                Expect.equal select.Value "b" "selected by value"
                Expect.isFalse (select.Children[0] :?> TwinElement).Selected "deselected"

            testCase "a textarea's default value is its text"
            <| fun () ->
                use document = new TwinDocument ()
                let area = document.CreateElement "textarea"

                area.AppendChild (document.CreateText "t")
                |> ignore

                Expect.equal area.Value "t" "default"
                area.Value <- "u"
                Expect.equal area.DefaultValue "t" "defaultValue unchanged"

            testCase "a template element owns a content fragment"
            <| fun () ->
                use document = new TwinDocument ()
                let template = document.CreateElement "template"
                Expect.equal template.Content.Kind NodeKind.Fragment "content"
                Expect.isNull (document.CreateElement "div").Content "other elements"

            testCase "a document used from another thread raises"
            <| fun () ->
                use document = new TwinDocument ()
                let failure = ref (null: exn)

                let thread =
                    System.Threading.Thread (fun () ->
                        try
                            document.Check "appendChild"
                        with e ->
                            failure.Value <- e)

                thread.Start ()
                thread.Join ()
                Expect.isTrue (failure.Value :? System.InvalidOperationException) "InvalidOperationException"
                Expect.stringContains failure.Value.Message "belongs to thread" "message"
        ]
