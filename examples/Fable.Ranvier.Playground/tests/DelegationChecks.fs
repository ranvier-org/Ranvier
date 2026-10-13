module DelegationChecks

open global.Ranvier
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation

module H = Ranvier.View.Foundation.Generated.Html

[<Global>]
let private globalThis: TS.Es.GlobalThis = jsNative

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

let private element (tag: string) =
    globalThis.document.createElement tag

let private mount graph host view =
    Mounting.mount graph None (fun errors -> failwithf "%A" errors) host None (fun context -> Program.run view context.Backend)

[<Emit("(() => { const counts = { add: 0, remove: 0 }; const add = $0.addEventListener.bind($0), remove = $0.removeEventListener.bind($0); $0.addEventListener = (type, listener, options) => { if (type === 'click') counts.add++; add(type, listener, options); }; $0.removeEventListener = (type, listener, options) => { if (type === 'click') counts.remove++; remove(type, listener, options); }; return counts; })()")>]
let private spyClickListeners (host: HTMLElement) : obj = jsNative

[<Emit("[$0.add, $0.remove]")>]
let private listenerCounts (counts: obj) : int array = jsNative

[<Emit("$0.dispatchEvent(new FocusEvent('focus'))")>]
let private dispatchFocus (node: HTMLElement) : unit = jsNative

let private detachedHost () =
    use graph = new Graph ()
    let host = element "main"
    let mutable current: obj = null

    let view =
        H.Program.button [| H.Events.onClick (fun event -> current <- box event.currentTarget.Value) |] [||]

    let mounted = mount graph host view

    let button =
        host.querySelector "button"
        |> unbox<HTMLButtonElement>

    button.click ()
    equal true (obj.ReferenceEquals (current, button))
    mounted.Dispose ()

let private nested stop =
    use graph = new Graph ()
    let host = element "main"
    let rows = graph.Run (fun () -> Api.createSignal [ 1; 2 ])
    let rowClicks, outerClicks = ResizeArray<int>(), ResizeArray<int>()

    let view =
        H.Program.div
            [| H.Events.onClick (fun _ -> outerClicks.Add 0) |]
            [|
                Keyed.list Program.ops (=) id (fun () -> rows.Value) (fun current ->
                    H.Program.button
                        [|
                            H.Events.onClick (fun event ->
                                rowClicks.Add (current ())

                                if stop then
                                    event.stopPropagation ())
                        |]
                        [||])
            |]

    let mounted = mount graph host view

    let second =
        host.querySelectorAll("button").item 1.
        |> unbox<HTMLButtonElement>

    second.click ()
    equal [ 2 ] (List.ofSeq rowClicks)
    equal (if stop then 0 else 1) outerClicks.Count
    mounted.Dispose ()

let private nonBubbling () =
    use graph = new Graph ()
    let host = element "main"
    let mutable focused = 0
    let view = H.Program.input [| H.Events.onFocus (fun _ -> focused <- focused + 1) |]
    let mounted = mount graph host view

    let input =
        host.querySelector "input"
        |> unbox<HTMLInputElement>

    dispatchFocus input
    equal 1 focused
    mounted.Dispose ()
    dispatchFocus input
    equal 1 focused

let private moved () =
    use graph = new Graph ()
    let first, second = element "main", element "main"
    let counts = spyClickListeners first
    let mutable clicks = 0

    let view =
        H.Program.button [| H.Events.onClick (fun _ -> clicks <- clicks + 1) |] [||]

    let mounted = mount graph first view

    let button =
        first.querySelector "button"
        |> unbox<HTMLButtonElement>

    mounted.MoveBefore (second, None)
    equal [| 1; 1 |] (listenerCounts counts)
    button.click ()
    equal 1 clicks
    mounted.Dispose ()

let private sharedHost () =
    use graph = new Graph ()
    let host = element "main"
    let counts = spyClickListeners host
    let view = H.Program.button [| H.Events.onClick ignore |] [||]
    let first, second = mount graph host view, mount graph host view
    equal [| 1; 0 |] (listenerCounts counts)
    first.Dispose ()
    equal [| 1; 0 |] (listenerCounts counts)
    second.Dispose ()
    equal [| 1; 1 |] (listenerCounts counts)

/// <summary>Checks delegated event dispatch from mount hosts.</summary>
let cases =
    [|
        "delegated clicks reach handlers in a detached host with the element as currentTarget", detachedHost
        "nested keyed mounts dispatch a click once per handler", fun () -> nested false
        "stopPropagation stops delegated ancestors", fun () -> nested true
        "non-bubbling events use direct listeners removed on disposal", nonBubbling
        "moved mounts dispatch from their new host", moved
        "a shared host keeps one listener until its last mount is disposed", sharedHost
    |]
