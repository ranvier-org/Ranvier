module Ranvier.View.Foundation.Typed

open Fable.Core
open Fable.Core.JsInterop
open Fable.Core.TS.Dom
open Ranvier
open Ranvier.View.Foundation.Representation

[<Erase>]
type Modifier<'native> = private Modifier of Token

let attribute name value : Modifier<'native> =
    validateAttribute name
    Modifier (Attribute (name, value))

let optionalAttribute name value : Modifier<'native> =
    validateAttribute name
    Modifier (OptionalAttribute (name, value))

let reactiveAttribute name (read: unit -> string option) : Modifier<'native> =
    validateAttribute name

    if isNull (box read) then
        invalidArg "read" "Attribute reader cannot be null"

    Modifier (ReactiveAttribute (name, read))

let booleanAttribute name enabled : Modifier<'native> =
    optionalAttribute name (if enabled then Some "" else None)

let reactiveBooleanAttribute name (read: unit -> bool) : Modifier<'native> =
    if isNull (box read) then
        invalidArg "read" "Attribute reader cannot be null"

    reactiveAttribute name (fun () -> if read () then Some "" else None)

let ariaBooleanAttribute name enabled : Modifier<'native> =
    attribute name (if enabled then "true" else "false")

let reactiveAriaBooleanAttribute name (read: unit -> bool option) : Modifier<'native> =
    if isNull (box read) then
        invalidArg "read" "Attribute reader cannot be null"

    reactiveAttribute name (fun () ->
        read ()
        |> Option.map (fun enabled -> if enabled then "true" else "false"))

let prefixedAttributeName prefix suffix =
    if System.String.IsNullOrEmpty suffix then
        raise (System.ArgumentException ("Attribute suffix cannot be empty", "suffix"))

    let name = prefix + suffix
    validateAttribute name
    name

let id value : Modifier<'native> =
    Modifier (Attribute ("id", value))

let title value : Modifier<'native> =
    Modifier (Attribute ("title", value))

let value text : Modifier<HTMLInputElement> =
    Modifier (InputValue text)

let browser (hook: 'native -> (unit -> unit)) : Modifier<'native> =
    Modifier (Browser (fun node -> hook (unbox node)))

let staticProperty (setter: 'native -> 'value -> unit) value : Modifier<'native> =
    browser (fun node ->
        setter node value
        ignore)

let reactiveProperty (setter: 'native -> 'value -> unit) (read: unit -> 'value) : Modifier<'native> =
    Modifier (ReactiveProperty (fun node -> (fun () -> box (read ())), (fun value -> setter (unbox node) (unbox value))))

let listener (eventName: string) (handler: Event -> unit) : Modifier<'native> =
    browser (fun native ->
        let node = unbox<EventTarget>(box native)
        let graph = Graph.Current
        let owner = Api.getOwner ()

        let invoke: EventListener =
            fun event ->
                if not owner.IsDisposed then
                    graph.Run (fun () -> Api.runWithOwner owner (fun () -> Api.untrack (fun () -> handler event)))

        let callback: EventListenerOrEventListenerObject = !^invoke
        node.addEventListener (eventName, callback)
        fun () -> node.removeEventListener (eventName, callback))

let private tokens modifiers =
    modifiers
    |> List.map (fun (Modifier token) -> token)

let element (ops: Operations<'view>) tag (modifiers: Modifier<'native> list) children =
    ops.Element tag (tokens modifiers) children

let div (ops: Operations<'view>) (modifiers: Modifier<HTMLDivElement> list) children =
    ops.Element Div (tokens modifiers) children

let button (ops: Operations<'view>) (modifiers: Modifier<HTMLButtonElement> list) children =
    ops.Element Button (tokens modifiers) children

let input (ops: Operations<'view>) (modifiers: Modifier<HTMLInputElement> list) =
    ops.Element Input (tokens modifiers) []

/// <summary>Creates text updated from a tracked string reader, using string equality to skip unchanged writes.</summary>
let reactiveText (ops: Operations<'view>) read =
    ops.Bind (=) read
