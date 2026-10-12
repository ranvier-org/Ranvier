module AuthoringChecks

open Ranvier
open Fable.Core.TS.Dom
open Fable.Ranvier
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Components
open Ranvier.View.Foundation.Authoring

module Html = Ranvier.View.Foundation.Generated.Html.Program
module Attr = Ranvier.View.Foundation.Generated.Html.Attrs
module Events = Ranvier.View.Foundation.Generated.Html.Events

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

type private PanelProps =
    {
        Label: Prop<string>
        Children: Program.View list
        Changed: int -> unit
    }

let private mountedComponents () =
    use graph = new Graph ()
    let label = graph.Run (fun () -> Api.createSignal "First")
    let mutable setups, cleanups, changes = 0, 0, 0

    let panel props =
        createComponent
            (fun context props ->
                setups <- setups + 1
                context.OnCleanup (fun () -> cleanups <- cleanups + 1)
                let count = Api.createSignal 0

                Html.section
                    []
                    [
                        propText props.Label
                        Html.button
                            [
                                Events.onClick (fun _ ->
                                    count.Value <- count.Value + 1
                                    props.Changed count.Value)
                            ]
                            [ reactiveText (fun () -> string count.Value) ]
                        fragment props.Children
                    ])
            props

    let view =
        panel
            {
                Label = Prop.ofSignal label
                Children = [ text " child" ]
                Changed = fun value -> changes <- value
            }

    let first, second = Dom.element "section" [] [], Dom.element "section" [] []

    let mount host =
        Mounting.mount graph None (fun errors -> failwithf "%A" errors) host None (fun context -> Program.run view context.Backend)

    equal 0 setups
    let a, b = mount first, mount second
    equal 2 setups
    equal "First0 child" first.textContent

    let button =
        first.querySelector ("button")
        |> unbox<HTMLButtonElement>

    button.click ()
    equal 1 changes
    equal "First1 child" first.textContent
    equal "First0 child" second.textContent
    graph.Run (fun () -> label.Value <- "Second")
    equal 2 setups
    equal "Second1 child" first.textContent
    equal "Second0 child" second.textContent
    a.Dispose ()
    button.click ()
    equal 1 changes
    equal 1 cleanups
    graph.Run (fun () -> label.Value <- "Third")
    equal "" first.textContent
    equal "Third0 child" second.textContent
    b.Dispose ()
    equal 2 cleanups

let private portableAuthoring () =
    let view =
        Html.article
            [ Attr.classStatic "panel" ]
            [
                propText (Constant "<&")
                Html.br []
                keyedArray (=) fst (fun () -> [| 1, "one"; 2, "two" |]) (fun current -> Html.p [] [ reactiveText (fun () -> snd (current ())) ])
                branch (=) id (fun () -> "selected") (fun current -> text (current ()))
                empty
            ]

    equal "<article class=\"panel\">&lt;&amp;<br><p>one</p><p>two</p>selected</article>" (Representation.Html.render Reject (Program.run view))

let cases =
    [|
        "authoring components retain independent state, reactive props and cleanup", mountedComponents
        "authoring elements, arrays and branches share the portable Program representation", portableAuthoring
    |]
