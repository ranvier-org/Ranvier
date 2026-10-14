module TemplateCatalogueChecks

open global.Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Generated.HtmlTemplate

[<Global>]
let private globalThis: TS.Es.GlobalThis = jsNative

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

let private host () =
    globalThis.document.createElement "main"

let private mount graph (host: HTMLElement) view =
    Mounting.mount graph None (fun errors -> failwithf "%A" errors) host None (fun context -> Program.run view context.Backend)

type Decoy = { Index: string }

type Card =
    {
        Index: int
        Name: string
        Busy: Signal<bool>
        Value: Signal<string>
    }

let private card =
    Component.define (fun _ (props: Card) -> props) (fun t ->
        t.div
            [| t.Attrs.className "card"; t.Attrs.id (fun r -> "card-" + string r.Index) |]
            [|
                t.input [| t.Props.Input.value (t.from (fun r -> r.Value)) |]
                t.button
                    [|
                        t.Props.Button.disabled (fun r -> r.Busy.Value)
                        t.Events.onClick (fun r _ -> r.Busy.Value <- true)
                    |]
                    [| t.span [||] [| t.text (fun r -> r.Name) |] |]
            |])

let private labelInference () =
    use graph = new Graph ()
    let host = host ()
    let busy = graph.Run (fun () -> Api.createSignal false)
    let value = graph.Run (fun () -> Api.createSignal "v")

    let mounted =
        mount
            graph
            host
            (Component.render
                card
                {
                    Index = 7
                    Name = "Ada"
                    Busy = busy
                    Value = value
                })

    equal (Some "card-7") ((host.querySelector "div" |> unbox<HTMLElement>).getAttribute "id")

    equal
        "v"
        (host.querySelector "input"
         |> unbox<HTMLInputElement>)
            .value

    (host.querySelector "button"
     |> unbox<HTMLButtonElement>)
        .click()

    equal
        true
        (host.querySelector "button"
         |> unbox<HTMLButtonElement>)
            .disabled

    mounted.Dispose ()

let cases =
    [|
        "a template lambda types against the instance when another record shares the field name", labelInference
    |]
