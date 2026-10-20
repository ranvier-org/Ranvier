/// <summary>Sugared and explicit twins compared by skeleton layout, structural hash and server HTML, on .NET and under Fable.</summary>
module SugarServerChecks

open System
open global.Ranvier
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open SugarFixtures

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

let private sameLayouts (pairs: (string * TemplateComponent * TemplateComponent) array) () =
    for name, explicitDefinition, sugarDefinition in pairs do
        equal
            (name, layoutSignature explicitDefinition.Root, structuralHash explicitDefinition)
            (name, layoutSignature sugarDefinition.Root, structuralHash sugarDefinition)

let private sameServerHtml (pairs: unit -> (string * string * string) array) () =
    for name, explicitHtml, sugarHtml in pairs () do
        equal (name, explicitHtml) (name, sugarHtml)

let private semanticsOrder () =
    equal semanticsMarkup (skeletonLayout semantics.Root).Markup

let private ignoredParameterIsSlot () =
    equal (1, 0) ((skeletonLayout ignoredParameter.Root).Targets.Length, (skeletonLayout literalText.Root).Targets.Length)

let private definitionTimeReadIsStatic () =
    let layout = skeletonLayout definitionTimeRead.Root
    equal ("<span>1</span>", 0) (layout.Markup, layout.Targets.Length)

let cases: (string * (unit -> unit)) array =
    [|
        "sugared elements share their explicit twins' layouts and hashes", sameLayouts elementPairs
        "sugared elements render their explicit twins' server HTML", sameServerHtml elementServerPairs
        "a CE body keeps document order across for, if, yield! and adjacent texts", semanticsOrder
        "a body lambda that ignores its parameter is a text slot", ignoredParameterIsSlot
        "a body line reading a module-level signal is static text", definitionTimeReadIsStatic
    |]
