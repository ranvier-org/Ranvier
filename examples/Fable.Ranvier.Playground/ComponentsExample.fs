module ComponentsExample

open global.Ranvier
open Ranvier.View.Foundation.Authoring
open Ranvier.View.Foundation.Components
open Ranvier.View.Foundation.Representation

module Html = Ranvier.View.Foundation.Generated.Html.Program
module Attr = Ranvier.View.Foundation.Generated.Html.Attrs
module Events = Ranvier.View.Foundation.Generated.Html.Events

/// <summary>Inputs to a counter instance, including its reactive label and projected child views.</summary>
type CounterProps =
    {
        Id: string
        Label: Prop<string>
        InitialCount: int
        Changed: int -> unit
        Children: Program.View list
    }

/// <summary>Creates a counter with state owned by its mounted instance.</summary>
let counter props =
    createComponent
        (fun _ props ->
            let count = Api.createSignal props.InitialCount

            Html.article
                [ Attr.idStatic props.Id; Attr.classStatic "panel" ]
                [
                    Html.h2 [] [ propText props.Label ]
                    Html.output [ Attr.idStatic (props.Id + "-count"); Attr.classStatic "count" ] [ reactiveText (fun () -> string count.Value) ]
                    Html.button
                        [
                            Attr.idStatic (props.Id + "-increment")
                            Attr.classStatic "primary"
                            Attr.Button.typeStatic "button"
                            Events.onClick (fun _ ->
                                count.Value <- count.Value + 1
                                props.Changed count.Value)
                        ]
                        [ text "Increment" ]
                    fragment props.Children
                ])
        props

/// <summary>Creates two counters sharing a label and reporting changes to their parent.</summary>
let view () =
    createComponent
        (fun _ () ->
            let label = Api.createSignal "Shared label"
            let changed = Api.createSignal "No changes yet"

            Html.section
                [ Attr.idStatic "component-example" ]
                [
                    Html.h2 [] [ text "Reusable components" ]
                    Html.button
                        [
                            Attr.idStatic "component-rename"
                            Attr.Button.typeStatic "button"
                            Events.onClick (fun _ -> label.Value <- "Renamed")
                        ]
                        [ text "Rename both counters" ]
                    Html.p [ Attr.idStatic "component-changed" ] [ reactiveText (fun () -> changed.Value) ]
                    Html.div
                        [ Attr.classStatic "demo-grid" ]
                        [
                            counter
                                {
                                    Id = "component-first"
                                    Label = Prop.ofSignal label
                                    InitialCount = 0
                                    Changed = fun value -> changed.Value <- "First changed to " + string value
                                    Children = [ Html.p [] [ text "First counter child content" ] ]
                                }
                            counter
                                {
                                    Id = "component-second"
                                    Label = Prop.ofSignal label
                                    InitialCount = 10
                                    Changed = fun value -> changed.Value <- "Second changed to " + string value
                                    Children = [ Html.p [] [ reactiveText (fun () -> "Child sees " + label.Value) ] ]
                                }
                        ]
                ])
        ()
