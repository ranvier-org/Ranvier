/// <summary>Hydration checks: jsdom through tests/hydration.test.mjs, Chromium through tests/hydration.spec.mjs.</summary>
module HydrationChecks

open System
open global.Ranvier
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open CheckSupport
open ServerFixtures

/// <summary>The <c>id</c> attributes of the elements under <c>root</c>, in document order.</summary>
let private domIds (root: HTMLElement) : string list =
    let found = root.querySelectorAll "[id]"

    [
        for index in 0 .. int found.length - 1 -> (unbox<Element>(found.item (float index))).id
    ]

let private mount graph (target: HTMLElement) view =
    Mounting.mount graph None strict target None (fun context -> Program.run view context.Backend)

/// <summary>The element ids of the server HTML of <c>definition</c>, parsed under a detached element.</summary>
let private servedIds (definition: Component<'props>) (props: 'props) =
    let parsed = host ()
    parsed.innerHTML <- Server.render Server.Options.defaults definition props
    domIds parsed

let private clientIdentity () =
    let served = servedIds identified [| 7; 8 |]
    use graph = new Graph ()
    let target = host ()
    identityLog.Clear ()
    let mounted = mount graph target (Component.render identified [| 7; 8 |])
    let issued = List.sort (List.ofSeq identityLog)
    let shown = domIds target
    mounted.Dispose ()
    // The root's second id is logged only.
    let visible =
        identityIds "r"
        |> List.filter (fun id -> id <> "r-0-1")

    equal (identityIds "r", visible, served) (issued, List.sort served, shown)

let private clientCreateIdAfterSetup () =
    use graph = new Graph ()
    let captured: Setup option ref = ref None

    let capturing =
        Component.define (fun setup () -> captured.Value <- Some setup) (fun t -> t.span [||] [||])

    let mounted = mount graph (host ()) (Component.render capturing ())

    let rejected =
        ServerChecks.failure (fun () -> captured.Value.Value.CreateId () |> ignore)

    mounted.Dispose ()
    equal true (rejected :? InvalidOperationException)

/// <summary>
/// Mounts <c>definition</c> and checks that the client's sorted id log and its DOM ids in document order equal
/// <c>expected</c>, and that the server's DOM ids equal the client's.
/// </summary>
let private sameIds (definition: Component<'props>) (props: 'props) (expected: string list) =
    let served = servedIds definition props
    use graph = new Graph ()
    let target = host ()
    identityLog.Clear ()
    let mounted = mount graph target (Component.render definition props)
    let issued = List.sort (List.ofSeq identityLog)
    let shown = domIds target
    mounted.Dispose ()
    equal (List.sort expected, expected, shown) (issued, shown, served)

let private clientHoleIdentity () =
    sameIds idHole () holeIds

let private clientNestedIdentity () =
    sameIds idNested [| 1; 2 |] nestedIds

let private clientHolesIdentity () =
    sameIds idHoles () holesIds

let private clientCreateIdInLaterSetup () =
    use graph = new Graph ()

    let error =
        ServerChecks.failure (fun () ->
            mount graph (host ()) (Component.render (capturedCreateId ()) ())
            |> ignore)

    equal (true, "CreateId is valid only while a component's setup runs") (error :? InvalidOperationException, error.Message)

let cases: (string * (unit -> unit)) array =
    strictCases
        [|
            "a client mount issues the server's ids for the same structure", clientIdentity
            "CreateId after a client setup raises", clientCreateIdAfterSetup
            "a client view hole's first template takes the row's position and a later one the authored step under it", clientHoleIdentity
            "a client mount numbers inlined children in pre-order and puts an inlined child's rows under its path, as the server does",
            clientNestedIdentity
            "each client view row counts its authored steps from 0 and the enclosing row resumes its count, as the server does", clientHolesIdentity
            "a Setup kept from one client setup raises when called during another instance's setup", clientCreateIdInLaterSetup
        |]

let browserCases: (string * (unit -> unit)) array = [||]
