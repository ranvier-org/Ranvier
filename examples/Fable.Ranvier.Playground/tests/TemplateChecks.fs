module TemplateChecks

open global.Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template

[<Global>]
let private globalThis: TS.Es.GlobalThis = jsNative

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

[<Emit("(() => { const shape = n => n.nodeType + (n.nodeName || '') + '(' + Array.from(n.childNodes).map(shape).join(',') + ')'; const t = document.createElement('template'); t.innerHTML = $0; const first = shape(t.content); const again = document.createElement('template'); again.innerHTML = t.innerHTML; return first === shape(again.content); })()")>]
let private roundTrips (markup: string) : bool = jsNative

type Greeting = { Title: string; Name: string }

let private greeting =
    Component.define (fun _ (props: Greeting) -> props) (fun t ->
        t.fragment
            [|
                t.div
                    [| t.Attrs.className "panel"; t.Attrs.title (fun r -> r.Title) |]
                    [| t.text "Hello "; t.text (fun r -> r.Name); t.text (fun _ -> "!") |]
                t.span [||] [| t.text (fun r -> r.Name) |]
            |])

let private walkPrograms () =
    equal [| 0; 3; 1; 0; 1; 3 |] (TemplateDom.walkProgram [| [| 0 |]; [| 1; 1 |] |])
    equal [| 0; 0; 1; 3; 2; 1; 3 |] (TemplateDom.walkProgram [| [| 0; 1 |]; [| 1 |] |])

let private compiledSkeleton () =
    let compiled = TemplateDom.compile greeting
    let markup, targets = skeletonMarkup (greeting :> TemplateComponent).Root
    equal "<div class=\"panel\">Hello <!--!--> <!--!--> </div><span> </span>" markup
    equal true (roundTrips markup)
    equal 4 targets.Length
    equal 4 compiled.NodeCount
    equal 4 compiled.RenderSlots.Length
    equal 2. compiled.Skeleton.childNodes.length

/// <summary>Checks template compilation and DOM materialisation.</summary>
let cases =
    [|
        "walk programs reach dynamic nodes in document order", walkPrograms
        "a compiled skeleton round-trips and records every dynamic node", compiledSkeleton
    |]
