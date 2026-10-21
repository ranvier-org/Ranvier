/// <summary>The golden dump fixtures: each case mounts a view and records its <c>Debug</c> text and its host's HTML.</summary>
module DumpChecks

#if RANVIER_VIEW_INSPECT || !FABLE_COMPILER
open global.Ranvier
open Fable.Core.TS.Dom
open Ranvier.View
open Ranvier.View.Dom
open Ranvier.View.Testing
open CheckSupport

type CardItem = { Id: int; Title: string }

type CardState = { Title: Signal<string> }

type Board =
    {
        Columns: Memo<int array>
        Adding: Signal<bool>
        Cards: Signal<CardItem array>
        Editing: Signal<bool>
    }

/// <summary>The async source the current case's boundary content reads.</summary>
let private query () =
    Conformance.local "DumpChecks.query" (fun () -> ref (Unchecked.defaultof<AsyncSource<string>>))

// A card row whose exit holds its root until the mount is disposed.
let private card =
    Component.define
        (fun _ (item: unit -> CardItem) ->
            {
                Title = Api.createSignal (item ()).Title
            })
        (fun t ->
            t.li
                [|
                    t.Attrs.className "card"
                    t.Hooks.exit (System.Func<_, _, _, _>(fun (_: CardState) (_: HTMLLIElement) _ -> ignore))
                |]
                [| t.text (fun (s: CardState) -> s.Title.Value) |])

let private editPanel =
    Component.define (fun _ (_: unit -> unit) -> ()) (fun t -> t.form [||] [| t.text "edit" |])

let private viewPanel =
    Component.define (fun _ (_: unit -> unit) -> ()) (fun t -> t.section [||] [| t.text "view" |])

let private loading =
    Component.define (fun _ () -> ()) (fun t -> t.p [||] [| t.text "Loading" |])

let private failedView =
    Component.define (fun _ (failure: BoundaryFailure) -> failure) (fun t -> t.p [||] [| t.text (fun (f: BoundaryFailure) -> f.Error.Message) |])

let private loaded =
    Component.define (fun _ (status: BoundaryStatus) -> status) (fun t -> t.p [||] [| t.text (fun (_: BoundaryStatus) -> (query ()).Value.Value) |])

let private items =
    [|
        { Id = 1; Title = "Write spec" }
        { Id = 2; Title = "Review" }
        { Id = 3; Title = "Ship" }
        { Id = 4; Title = "Rest" }
    |]

let private board =
    Component.define
        (fun _ () ->
            let cards = Api.createSignal items

            {
                Columns = Api.createMemo (fun _ -> cards.Value |> Array.map (fun c -> c.Id))
                Adding = Api.createSignal false
                Cards = cards
                Editing = Api.createSignal true
            })
        (fun t ->
            t.div
                [| t.Attrs.className "board" |]
                [|
                    t.h2 [||] [| t.text "Cards: "; t.text (fun (b: Board) -> string b.Cards.Value.Length) |]
                    t.ul [||] [| t.each (fun (c: CardItem) -> c.Id) (fun (b: Board) -> b.Cards.Value) card |]
                    t.branch (fun (b: Board) -> if b.Editing.Value then "edit" else "view") (fun key ->
                        if key = "edit" then
                            t.arm editPanel ()
                        else
                            t.arm viewPanel ())
                    t.boundary loading failedView loaded
                |])

let private options = Debug.DumpOptions.defaults

let private record (name: string) (graph: Graph) (target: HTMLElement) =
    Conformance.recordIfRunning name (Debug.textWith options (Debug.captureNode options graph target))
    Conformance.recordIfRunning (name + " html") target.innerHTML

let private mounted (view: Program.View) (check: Graph -> HTMLElement -> Mounting.Mounted -> unit) =
    use graph = new Graph ()
    (query ()).Value <- graph.Run (fun () -> Api.createAsyncSource<string>())
    let target = host ()
    let mounted = mountTemplate false graph strict target view
    mounted.Flush ()

    try
        check graph target mounted
    finally
        mounted.Dispose ()

let private boardOf (target: HTMLElement) : Board =
    let rec find (node: Node) =
        match Inspect.probeOf node with
        | Some (Inspect.InstanceProbe (_, instance, _, _)) when (instance :? Board) -> Some (unbox<Board> instance)
        | _ ->
            let mutable child = node.firstChild
            let mutable found = None

            while found.IsNone && child.IsSome do
                found <- find (unbox<Node> child.Value)
                child <- (unbox<Node> child.Value).nextSibling

            found

    (find target).Value

let private boardDump () =
    mounted (Component.render board ()) (fun graph target mounted ->
        // Removing the second card leaves its row held by its exit.
        let state = boardOf target
        graph.Run (fun () -> state.Cards.Value <- items |> Array.filter (fun c -> c.Id <> 2))
        mounted.Flush ()
        record "board" graph target)

let private armDump () =
    mounted (Component.render board ()) (fun graph target mounted ->
        record "edit" graph target
        graph.Run (fun () -> (boardOf target).Editing.Value <- false)
        mounted.Flush ()
        record "view" graph target)

let private boundaryDump () =
    mounted (Component.render board ()) (fun graph target mounted ->
        record "pending" graph target
        graph.Run (fun () -> (query ()).Value.Settle "Loaded")
        mounted.Flush ()
        record "ready" graph target)

let private failedBoundaryDump () =
    mounted (Component.render board ()) (fun graph target mounted ->
        graph.Run (fun () -> (query ()).Value.Fail(exn "offline"))
        mounted.Flush ()
        record "failed" graph target)

type Holed = { Body: Program.View }

let private holed =
    Component.define
        (fun _ () ->
            {
                Body = Component.render viewPanel ignore
            })
        (fun t -> t.div [| t.Attrs.className "holder" |] [| t.view (fun (h: Holed) -> h.Body) |])

let private holeDump () =
    mounted (Component.render holed ()) (fun graph target _ -> record "hole" graph target)

let private headed =
    Component.define (fun _ () -> ()) (fun t ->
        t.div
            [||]
            [|
                t.Document.title "Board (4)"
                t.Document.meta
                    [|
                        t.Attrs.attribute ("name", "description")
                        t.Attrs.attribute ("content", "Four cards")
                    |]
                t.text "page"
            |])

let private headDump () =
    mounted (Component.render headed ()) (fun graph target _ -> record "head" graph target)

let cases: Conformance.Case array =
    Conformance.ofChecks
        [|
            "the board dump shows its instance, a list with a leaving row, the edit arm and a pending boundary", boardDump
            "an arm dump shows the selected key and definition before and after a switch", armDump
            "a boundary dump shows the pending and then the ready state", boundaryDump
            "a boundary dump shows the failed state with its error", failedBoundaryDump
            "a view hole dump shows the view mounted in the hole", holeDump
            "the head section lists the marked head elements", headDump
        |]
#endif
