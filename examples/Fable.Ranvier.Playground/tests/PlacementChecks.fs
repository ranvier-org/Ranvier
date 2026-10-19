module PlacementChecks

open Ranvier
open Fable.Core.TS.Dom
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Scheduling
open Ranvier.View.Foundation.Mounting
open Ranvier.View.Foundation.Template
open CheckSupport

/// <summary>A div whose mounted hook records whether the div is in the host when the hook and its cleanup run.</summary>
let private placedInHost (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let log = ResizeArray<string>()
    let target = host ()

    let view =
        Typed.div
            ops
            [|
                Typed.mounted (fun (node: HTMLDivElement) ->
                    log.Add (sprintf "hook %b" (target.contains node))
                    fun () -> log.Add (sprintf "cleanup %b" (target.contains node)))
            |]
            [||]

    let mounted = mountView ops run queued graph strict target view
    equal [ "hook true" ] (List.ofSeq log)
    mounted.Dispose ()
    equal ([ "hook true"; "cleanup true" ], 0.) (List.ofSeq log, target.childNodes.length)

let private browserHookUnplaced (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let log = ResizeArray<string>()
    let target = host ()

    let view =
        Typed.div
            ops
            [|
                Typed.browser (fun (node: HTMLDivElement) ->
                    log.Add (sprintf "browser %b" (target.contains node))
                    ignore)
                Typed.mounted (fun (node: HTMLDivElement) ->
                    log.Add (sprintf "mounted %b" (target.contains node))
                    ignore)
            |]
            [||]

    let mounted = mountView ops run queued graph strict target view
    equal [ "browser false"; "mounted true" ] (List.ofSeq log)
    mounted.Dispose ()

/// <summary>A component's div, an inlined child component's span and two list rows, each with a hook that logs its name.</summary>
let private setupOrder (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let log = ResizeArray<string>()
    let target = host ()

    let hook (name: string) : Typed.Modifier<'native> =
        Typed.mounted (fun _ ->
            log.Add name
            ignore)

    let view =
        ops.Component (fun _ ->
            Typed.div
                ops
                [| hook "parent" |]
                [|
                    ops.Component (fun _ -> span ops [| hook "child" |] [||])
                    Keyed.list ops id (fun () -> [ 1; 2 ]) (fun current -> span ops [| hook ("row " + string (current ())) |] [||])
                |])

    let mounted = mountView ops run queued graph strict target view
    equal [ "parent"; "child"; "row 1"; "row 2" ] (List.ofSeq log)
    mounted.Dispose ()

/// <summary>
/// A list whose rows log their hooks: a row added after the mount runs its hook in the host, and inside one batch the
/// list pass runs once with the final items.
/// </summary>
let private rowsAfterLayout (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let rows = graph.Run (fun () -> Api.createSignal [ 1 ])
    let log = ResizeArray<string>()
    let target = host ()

    let view =
        Keyed.list ops id (fun () -> rows.Value) (fun current ->
            let item = current ()

            span
                ops
                [|
                    Typed.mounted (fun (node: HTMLSpanElement) ->
                        log.Add (sprintf "row %d %b" item (target.contains node))
                        ignore)
                |]
                [| ops.Text (string item) |])

    let mounted = mountView ops run queued graph strict target view
    graph.Run (fun () -> rows.Value <- [ 1; 2 ])
    mounted.Flush ()
    equal [ "row 1 true"; "row 2 true" ] (List.ofSeq log)

    graph.Run (fun () ->
        Api.batch (fun () ->
            rows.Value <- [ 1; 2; 3 ]
            rows.Value <- [ 1; 2 ]))

    mounted.Flush ()
    equal ([ "row 1 true"; "row 2 true" ], "12") (List.ofSeq log, target.textContent)
    mounted.Dispose ()

let private armsReplace (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let arm = graph.Run (fun () -> Api.createSignal 0)
    let log = ResizeArray<string>()
    let target = host ()

    let view =
        Branch.select ops id (fun () -> arm.Value) (fun current ->
            let key = current ()

            span
                ops
                [|
                    Typed.mounted (fun (node: HTMLSpanElement) ->
                        log.Add (sprintf "arm %d %b" key (target.contains node))
                        fun () -> log.Add (sprintf "arm cleanup %d" key))
                |]
                [| ops.Text (string key) |])

    let mounted = mountView ops run queued graph strict target view
    graph.Run (fun () -> arm.Value <- 1)
    mounted.Flush ()
    equal [ "arm 0 true"; "arm cleanup 0"; "arm 1 true" ] (List.ofSeq log)
    mounted.Dispose ()

/// <summary>A div with a hook that succeeds and a hook that raises during the mount.</summary>
let private initialHookFailure (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let log = ResizeArray<string>()
    let target = host ()

    let view =
        Typed.div
            ops
            [|
                Typed.mounted (fun (_: HTMLDivElement) ->
                    log.Add "first"
                    fun () -> log.Add "first cleanup")
                Typed.mounted (fun (_: HTMLDivElement) -> failwith "hook")
            |]
            [||]

    let raised =
        try
            mountView ops run queued graph strict target view
            |> ignore

            None
        with error ->
            Some error.Message

    equal (Some "hook", [ "first"; "first cleanup" ], 0.) (raised, List.ofSeq log, target.childNodes.length)

/// <summary>A second list row whose hook raises after the mount: the failure is reported and the row stays.</summary>
let private laterHookFailure (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let rows = graph.Run (fun () -> Api.createSignal [ 1 ])
    let reports = ResizeArray<exn list>()
    let target = host ()

    let view =
        Keyed.list ops id (fun () -> rows.Value) (fun current ->
            let item = current ()

            span
                ops
                [|
                    Typed.mounted (fun (_: HTMLSpanElement) -> if item = 2 then failwith "row 2" else ignore)
                |]
                [| ops.Text (string item) |])

    let mounted = mountView ops run queued graph reports.Add target view
    graph.Run (fun () -> rows.Value <- [ 1; 2 ])
    mounted.Flush ()
    equal ("12", [ [ "row 2" ] ]) (target.textContent, messages reports)
    mounted.Dispose ()

let private mountInBatch (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let log = ResizeArray<string>()
    let reports = ResizeArray<exn list>()
    let target = host ()
    let mounted = ref Unchecked.defaultof<Mounted>

    let view =
        Typed.div
            ops
            [|
                Typed.mounted (fun (node: HTMLDivElement) ->
                    log.Add (sprintf "hook %b" (target.contains node))
                    ignore)
                Typed.mounted (fun (_: HTMLDivElement) -> failwith "late")
            |]
            [||]

    graph.Run (fun () ->
        Api.batch (fun () ->
            mounted.Value <- mountView ops run queued graph reports.Add target view
            log.Add "returned"))

    equal ([ "returned"; "hook true" ], [ [ "late" ] ]) (List.ofSeq log, messages reports)
    mounted.Value.Dispose ()

let private nodeRefLifetime (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let field: NodeRef<HTMLInputElement> = NodeRef.create ()
    let seen = ResizeArray<bool>()
    let target = host ()

    let view =
        Typed.input
            ops
            [|
                Typed.ref field
                Typed.browser (fun (_: HTMLInputElement) ->
                    seen.Add field.Current.IsSome
                    ignore)
            |]

    let mounted = mountView ops run queued graph strict target view

    let placed =
        match field.Current with
        | Some node -> obj.ReferenceEquals (node, target.querySelector "input")
        | None -> false

    mounted.Dispose ()
    equal ([ false ], true, false) (List.ofSeq seen, placed, field.Current.IsSome)

let private movesKeepHooks (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let rows = graph.Run (fun () -> Api.createSignal [ 1; 2 ])
    let runs = ref 0
    let first, second = host (), host ()

    let view =
        Keyed.list ops id (fun () -> rows.Value) (fun current ->
            span
                ops
                [|
                    Typed.mounted (fun (_: HTMLSpanElement) ->
                        runs.Value <- runs.Value + 1
                        ignore)
                |]
                [| ops.Text (string (current ())) |])

    let mounted = mountView ops run queued graph strict first view
    graph.Run (fun () -> rows.Value <- [ 2; 1 ])
    mounted.Flush ()
    mounted.MoveBefore (second, None)
    equal (2, "21") (runs.Value, second.textContent)
    mounted.Dispose ()

/// <summary>A hook that reads and writes a signal read by a text binding of the same view.</summary>
let private hookWritesBinding (ops: Operations<'view>) (run: 'view -> Backend -> unit) queued =
    use graph = new Graph ()
    let focused = graph.Run (fun () -> Api.createSignal "no")
    let runs = ref 0
    let target = host ()

    let view =
        Typed.div
            ops
            [|
                Typed.mounted (fun (_: HTMLDivElement) ->
                    runs.Value <- runs.Value + 1

                    if focused.Value = "no" then
                        focused.Value <- "yes"

                    ignore)
            |]
            [| Typed.reactiveText ops (fun () -> focused.Value) |]

    let mounted = mountView ops run queued graph strict target view
    mounted.Flush ()
    equal ("yes", 1) (target.textContent, runs.Value)
    graph.Run (fun () -> focused.Value <- "again")
    mounted.Flush ()
    equal ("again", 1) (target.textContent, runs.Value)
    mounted.Dispose ()

let private serverHook (ops: Operations<'view>) (run: 'view -> Backend -> unit) =
    let view =
        Typed.div ops [| Typed.mounted (fun (_: HTMLDivElement) -> ignore) |] [| ops.Text "x" |]

    let rejected =
        try
            Html.render Reject (run view) |> ignore
            None
        with error ->
            Some error.Message

    equal ("<div>x</div>", Some "Browser capability requires explicit Omit policy") (Html.render Omit (run view), rejected)

/// <summary>An input in a host attached to the document; its mounted hook focuses and measures it.</summary>
let private focusAndMeasure () =
    use graph = new Graph ()
    let target = host ()
    attachToBody target
    let width = ref 0.

    let view =
        Typed.input
            Program.ops
            [|
                Typed.attribute "style" "width: 120px"
                Typed.mounted (fun (input: HTMLInputElement) ->
                    input.focus ()
                    width.Value <- input.getBoundingClientRect().width
                    ignore)
            |]

    let mounted =
        mountWith SynchronousCommits graph None strict target None (fun context -> Program.run view context.Backend)

    let focused = obj.ReferenceEquals (activeElement (), target.querySelector "input")

    mounted.Dispose ()
    target.remove ()
    equal (true, true) (focused, width.Value > 0.)

let cases: (string * (unit -> unit)) array =
    Array.concat
        [
            both
                "a mounted hook runs after the element is in the host and its cleanup runs before the element leaves"
                (placedInHost Tree.ops Tree.run)
                (placedInHost Program.ops Program.run)
            both
                "setup-time browser hooks run on an unplaced element"
                (browserHookUnplaced Tree.ops Tree.run)
                (browserHookUnplaced Program.ops Program.run)
            both
                "hooks run in setup order: an instance, its inlined children, then region rows"
                (setupOrder Tree.ops Tree.run)
                (setupOrder Program.ops Program.run)
            both
                "a row added by a list pass runs its hook after the pass lays it out, and a batch that adds and removes a row creates none"
                (rowsAfterLayout Tree.ops Tree.run)
                (rowsAfterLayout Program.ops Program.run)
            both
                "an arm's hook runs after the arm is moved in; a replaced arm's cleanup runs before the next arm's hook"
                (armsReplace Tree.ops Tree.run)
                (armsReplace Program.ops Program.run)
            both
                "a hook failure during mountWith unwinds the mount and rethrows"
                (initialHookFailure Tree.ops Tree.run)
                (initialHookFailure Program.ops Program.run)
            both
                "a later hook failure goes to report once and the instance stays"
                (laterHookFailure Tree.ops Tree.run)
                (laterHookFailure Program.ops Program.run)
            both
                "a mount inside Api.batch runs its hooks after the batch and reports their failures"
                (mountInBatch Tree.ops Tree.run)
                (mountInBatch Program.ops Program.run)
            both
                "NodeRef.Current is None during setup, Some while placed and None after disposal"
                (nodeRefLifetime Tree.ops Tree.run)
                (nodeRefLifetime Program.ops Program.run)
            both "moving a mount or reordering rows does not re-run hooks" (movesKeepHooks Tree.ops Tree.run) (movesKeepHooks Program.ops Program.run)
            both
                "a mounted hook that writes a signal read by a binding updates the binding and runs once"
                (hookWritesBinding Tree.ops Tree.run)
                (hookWritesBinding Program.ops Program.run)
            [|
                "tree: a mounted hook raises under Reject and writes nothing under Omit", (fun () -> serverHook Tree.ops Tree.run)
                "program: a mounted hook raises under Reject and writes nothing under Omit", (fun () -> serverHook Program.ops Program.run)
            |]
        ]

let browserCases: (string * (unit -> unit)) array =
    [| "a mounted hook can focus and measure its element", focusAndMeasure |]
