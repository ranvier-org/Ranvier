module Ranvier.View.Twin.Tests.EventTests

open Expecto
open Fable.Core.TS.Dom
open Ranvier.View.Twin

let private setup () =
    Events.ensure ()
    let document = new TwinDocument ()
    let outer = document.CreateElement "div"
    let inner = document.CreateElement "button"
    outer.AppendChild inner |> ignore
    document.Body.AppendChild outer |> ignore
    document, outer, inner

let private plain name bubbles =
    Events.create
        {
            Type = name
            Kind = Ranvier.View.Testing.Plain
            Bubbles = bubbles
            Cancelable = true
            Init = []
        }

[<Tests>]
let tests =
    testList
        "Events"
        [
            testCase "capture runs root to target, then target, then bubble"
            <| fun () ->
                let document, outer, inner = setup ()
                use _ = document
                let log = ResizeArray<string>()

                Events.listen outer "x" true (fun _ -> log.Add "outer capture")
                |> ignore

                Events.listen inner "x" false (fun _ -> log.Add "inner")
                |> ignore

                Events.listen outer "x" false (fun _ -> log.Add "outer bubble")
                |> ignore

                Events.dispatch inner (plain "x" true) |> ignore
                Expect.equal (List.ofSeq log) [ "outer capture"; "inner"; "outer bubble" ] "order"

            testCase "a non-bubbling event skips the bubble phase"
            <| fun () ->
                let document, outer, inner = setup ()
                use _ = document
                let log = ResizeArray<string>()

                Events.listen outer "x" false (fun _ -> log.Add "outer")
                |> ignore

                Events.dispatch inner (plain "x" false) |> ignore
                Expect.isEmpty log "no bubble"

            testCase "stopPropagation finishes the node's listeners; stopImmediatePropagation does not"
            <| fun () ->
                let document, outer, inner = setup ()
                use _ = document
                let log = ResizeArray<string>()

                Events.listen inner "x" false (fun e ->
                    (Events.eventOf e).StopPropagation()
                    log.Add "a")
                |> ignore

                Events.listen inner "x" false (fun _ -> log.Add "b")
                |> ignore

                Events.listen outer "x" false (fun _ -> log.Add "outer")
                |> ignore

                Events.dispatch inner (plain "x" true) |> ignore
                Expect.equal (List.ofSeq log) [ "a"; "b" ] "stopped after the node"

            testCase "preventDefault on a cancelable event makes dispatch return false"
            <| fun () ->
                let document, _, inner = setup ()
                use _ = document

                Events.listen inner "x" false (fun e -> (Events.eventOf e).PreventDefault())
                |> ignore

                Expect.isFalse (Events.dispatch inner (plain "x" true)) "cancelled"

            testCase "clicking a checkbox toggles it and fires click, input and change"
            <| fun () ->
                Events.ensure ()
                use document = new TwinDocument ()
                let box = document.CreateElement "input"
                box.SetAttribute ("type", "checkbox")
                document.Body.AppendChild box |> ignore
                let log = ResizeArray<string>()

                for name in [ "click"; "input"; "change" ] do
                    Events.listen box name false (fun _ -> log.Add (name + ":" + string box.Checked))
                    |> ignore

                Events.click box
                Expect.equal (List.ofSeq log) [ "click:True"; "input:True"; "change:True" ] "sequence"

            testCase "a cancelled checkbox click restores the state and fires no change"
            <| fun () ->
                Events.ensure ()
                use document = new TwinDocument ()
                let box = document.CreateElement "input"
                box.SetAttribute ("type", "checkbox")
                document.Body.AppendChild box |> ignore
                let changed = ref false

                Events.listen box "click" false (fun e -> (Events.eventOf e).PreventDefault())
                |> ignore

                Events.listen box "change" false (fun _ -> changed.Value <- true)
                |> ignore

                Events.click box
                Expect.equal (box.Checked, changed.Value) (false, false) "restored"

            testCase "clicking a submit button in a form fires submit"
            <| fun () ->
                Events.ensure ()
                use document = new TwinDocument ()
                let form = document.CreateElement "form"
                let button = document.CreateElement "button"
                form.AppendChild button |> ignore
                document.Body.AppendChild form |> ignore
                let submitted = ref false

                Events.listen form "submit" false (fun _ -> submitted.Value <- true)
                |> ignore

                Events.click button
                Expect.isTrue submitted.Value "submit"

            testCase "clicking a label clicks its control"
            <| fun () ->
                Events.ensure ()
                use document = new TwinDocument ()
                let label = document.CreateElement "label"
                let box = document.CreateElement "input"
                box.SetAttribute ("type", "checkbox")
                label.AppendChild box |> ignore
                document.Body.AppendChild label |> ignore
                Events.click label
                Expect.isTrue box.Checked "forwarded"

            testCase "focus moves fire blur, focusout, focus and focusin in order"
            <| fun () ->
                Events.ensure ()
                use document = new TwinDocument ()
                let a, b = document.CreateElement "input", document.CreateElement "input"
                document.Body.AppendChild a |> ignore
                document.Body.AppendChild b |> ignore
                let log = ResizeArray<string>()

                for name in [ "blur"; "focusout"; "focus"; "focusin" ] do
                    Events.listen document.Body name true (fun e ->
                        log.Add (
                            name
                            + ":"
                            + (Bridge.nodeOf (Events.eventOf e).Target.Native :?> TwinElement).LocalName
                        ))
                    |> ignore

                Events.focus a
                log.Clear ()
                Events.focus b
                Expect.equal (List.ofSeq log) [ "blur:input"; "focusout:input"; "focus:input"; "focusin:input" ] "order"
                Expect.equal document.ActiveElement b "active"

            testCase "a click arrives as a PointerEvent proxy"
            <| fun () ->
                let document, _, inner = setup ()
                use _ = document
                let seen = ref (null: obj)

                Events.listen inner "click" false (fun e -> seen.Value <- e)
                |> ignore

                Events.click inner
                Expect.isTrue (seen.Value :? PointerEvent) "PointerEvent"
                Expect.isTrue (obj.ReferenceEquals ((seen.Value :?> Event).target.Value, inner.Native)) "target"

            testCase "a listener's exception surfaces after the dispatch ends"
            <| fun () ->
                let document, outer, inner = setup ()
                use _ = document
                let outerRan = ref false
                document.RaiseListenerErrors <- true

                Events.listen inner "x" false (fun _ -> failwith "boom")
                |> ignore

                Events.listen outer "x" false (fun _ -> outerRan.Value <- true)
                |> ignore

                Expect.throwsT<TwinListenerException> (fun () -> Events.dispatch inner (plain "x" true) |> ignore) "raised"
                Expect.isTrue outerRan.Value "dispatch continued"

            testCase "a listener's exception is collected by default and the dispatch returns"
            <| fun () ->
                let document, _, inner = setup ()
                use _ = document

                Events.listen inner "x" false (fun _ -> failwith "boom")
                |> ignore

                Events.dispatch inner (plain "x" true) |> ignore

                Expect.equal
                    (document.ListenerErrors
                     |> Seq.map (fun e -> e.Message)
                     |> List.ofSeq)
                    [ "boom" ]
                    "collected"

            testCase "every listener of one dispatch receives the same event object"
            <| fun () ->
                let document, outer, inner = setup ()
                use _ = document
                let seen = ResizeArray<obj>()
                Events.listen inner "x" false seen.Add |> ignore
                Events.listen outer "x" false seen.Add |> ignore
                Events.listen outer "x" true seen.Add |> ignore
                Events.dispatch inner (plain "x" true) |> ignore
                Expect.equal seen.Count 3 "three listeners"

                Expect.isTrue
                    (seen
                     |> Seq.forall (fun e -> obj.ReferenceEquals (e, seen[0])))
                    "one object"
        ]
