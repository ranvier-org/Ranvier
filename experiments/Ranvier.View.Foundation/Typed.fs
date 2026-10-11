module Ranvier.View.Foundation.Typed

open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation.Representation

[<Erase>]
type Modifier<'native> = private Modifier of Token

let id value : Modifier<'native> =
    Modifier (Attribute ("id", value))

let title value : Modifier<'native> =
    Modifier (Attribute ("title", value))

let value text : Modifier<HTMLInputElement> =
    Modifier (InputValue text)

let browser (hook: 'native -> (unit -> unit)) : Modifier<'native> =
    Modifier (Browser (fun node -> hook (unbox node)))

let private tokens modifiers =
    modifiers
    |> List.map (fun (Modifier token) -> token)

let div (ops: Operations<'view>) (modifiers: Modifier<HTMLDivElement> list) children =
    ops.Element Div (tokens modifiers) children

let button (ops: Operations<'view>) (modifiers: Modifier<HTMLButtonElement> list) children =
    ops.Element Button (tokens modifiers) children

let input (ops: Operations<'view>) (modifiers: Modifier<HTMLInputElement> list) =
    ops.Element Input (tokens modifiers) []

let reactiveText (ops: Operations<'view>) policy read =
    ops.BindWith policy read
