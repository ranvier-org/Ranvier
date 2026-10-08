module RepresentationChecks

open Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Fable.Ranvier
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

[<Emit("$0.remove()")>]
let private remove (node: Node) : unit = jsNative

[<Emit("(() => { const fn = () => $1(); $0.addEventListener('click', fn); return () => $0.removeEventListener('click', fn); })()")>]
let private listen (node: HTMLButtonElement) (handler: unit -> unit) : unit -> unit = jsNative

[<Emit("$0.querySelector('button').click()")>]
let private click (node: HTMLElement) : unit = jsNative

[<Emit("$0.firstChild === $1.firstChild")>]
let private sameFirst (a: HTMLElement) (b: HTMLElement) : bool = jsNative

let private exercise (ops: Operations<'view>) run =
    use graph = new Graph ()
    let host = Dom.element "section" [] []
    let other = Dom.element "section" [] []
    let source = graph.Run (fun () -> Api.createSignal 0)
    let mutable setups = 0
    let mutable cleanups = 0
    let mutable calls = 0

    let view =
        ops.Component (fun context ->
            setups <- setups + 1
            let snapshot = source.Value

            context.OnCleanup (fun () ->
                equal
                    true
                    (host.childNodes.length > 0.
                     || other.childNodes.length > 0.)

                cleanups <- cleanups + 1)

            Typed.div
                ops
                [ Typed.id "root" ]
                [
                    ops.Text (string snapshot)
                    ops.Fragment [ ops.Empty; ops.Bind (=) (fun () -> string (source.Value / 2)) ]
                    Typed.button ops [ Typed.browser (fun button -> listen button (fun () -> calls <- calls + 1)) ] [ ops.Text "go" ]
                    Typed.input ops [ Typed.value "fixed" ]
                ])

    equal 0 setups
    let a, b = Browser.metrics (), Browser.metrics ()

    let disposeA =
        Browser.mount graph host (fun errors -> failwithf "%A" errors) (Some a) (run view)

    let disposeB =
        Browser.mount graph other (fun errors -> failwithf "%A" errors) (Some b) (run view)

    equal 2 setups
    equal false (sameFirst host other)
    equal "00go" host.textContent
    equal 1 a.Effects
    equal 2 a.Owners
    equal 1 a.Setups
    equal 1 a.Writes
    graph.Run (fun () -> source.Value <- 1)
    equal 1 a.Writes
    graph.Run (fun () -> source.Value <- 2)
    equal "01go" host.textContent
    equal 2 a.Writes
    equal 2 setups
    click host
    equal 1 calls
    disposeA ()
    disposeA ()
    equal "" host.textContent
    equal 1 cleanups
    graph.Run (fun () -> source.Value <- 4)
    equal 2 a.Writes
    equal "02go" other.textContent
    click other
    equal 2 calls
    graph.Dispose ()
    equal 2 cleanups
    equal "" other.textContent
    disposeB ()
    remove host
    remove other

let private staticAndFailure (ops: Operations<'view>) run =
    use graph = new Graph ()
    let host = Dom.element "section" [] []
    let sibling = Dom.element "aside" [] []
    host.appendChild sibling |> ignore
    let counts = Browser.metrics ()

    let dispose =
        Browser.mount graph host ignore (Some counts) (run (ops.Fragment [ ops.Empty; ops.Text "static" ]))

    equal 0 counts.Effects
    equal 1 counts.Owners
    dispose ()
    equal 1. host.childNodes.length
    let mutable cleaned = false
    let failure = System.InvalidOperationException "setup"

    let view =
        ops.Component (fun context ->
            context.OnCleanup (fun () -> cleaned <- true)
            raise failure)

    let mutable caught = false

    try
        Browser.mount graph host ignore None (run view)
        |> ignore
    with error ->
        equal true (obj.ReferenceEquals (error, failure))
        caught <- true

    equal true caught
    equal true cleaned
    equal 1. host.childNodes.length

let cases =
    [|
        "tree reusable typed views have local synchronous bindings", fun () -> exercise Tree.ops Tree.run
        "program reusable typed views have local synchronous bindings", fun () -> exercise Program.ops Program.run
        "tree static allocation and setup failure unwind", fun () -> staticAndFailure Tree.ops Tree.run
        "program static allocation and setup failure unwind", fun () -> staticAndFailure Program.ops Program.run
    |]
