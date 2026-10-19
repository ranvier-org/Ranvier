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

type Probe =
    {
        Log: ResizeArray<string>
        Host: HTMLElement
    }

let private mountTemplate queued graph report (target: HTMLElement) (view: Program.View) =
    mountView Program.ops Program.run queued graph report target view

let private placedDiv =
    Component.define (fun _ (probe: Probe) -> probe) (fun t ->
        t.div
            [|
                t.Hooks.mounted (fun probe (node: HTMLDivElement) ->
                    probe.Log.Add (sprintf "hook %b" (probe.Host.contains node))
                    fun () -> probe.Log.Add (sprintf "cleanup %b" (probe.Host.contains node)))
            |]
            [||])

let private templatePlacedInHost queued () =
    use graph = new Graph ()
    let probe = { Log = ResizeArray (); Host = host () }

    let mounted =
        mountTemplate queued graph strict probe.Host (Component.render placedDiv probe)

    equal [ "hook true" ] (List.ofSeq probe.Log)
    mounted.Dispose ()
    equal ([ "hook true"; "cleanup true" ], 0.) (List.ofSeq probe.Log, probe.Host.childNodes.length)

let private orderLog = ResizeArray<string>()

let private orderChild =
    Component.define (fun _ () -> ()) (fun t ->
        t.span
            [|
                t.Hooks.mounted (fun _ (_: HTMLSpanElement) ->
                    orderLog.Add "child"
                    ignore)
            |]
            [||])

let private orderRow =
    Component.define (fun _ (item: unit -> int) -> item) (fun t ->
        t.span
            [|
                t.Hooks.mounted (fun item (_: HTMLSpanElement) ->
                    orderLog.Add ("row " + string (item ()))
                    ignore)
            |]
            [||])

let private orderParent =
    Component.define (fun _ () -> ()) (fun t ->
        t.div
            [|
                t.Hooks.mounted (fun _ (_: HTMLDivElement) ->
                    orderLog.Add "parent"
                    ignore)
            |]
            [| t.render orderChild id; t.each id (fun _ -> [| 1; 2 |]) orderRow |])

let private templateSetupOrder queued () =
    use graph = new Graph ()
    orderLog.Clear ()

    let mounted =
        mountTemplate queued graph strict (host ()) (Component.render orderParent ())

    equal [ "parent"; "child"; "row 1"; "row 2" ] (List.ofSeq orderLog)
    mounted.Dispose ()

let mutable private rowsProbe: Probe = Unchecked.defaultof<_>

let private hookedRow =
    Component.define (fun _ (item: unit -> int) -> item) (fun t ->
        t.span
            [|
                t.Hooks.mounted (fun item (node: HTMLSpanElement) ->
                    rowsProbe.Log.Add (sprintf "row %d %b" (item ()) (rowsProbe.Host.contains node))
                    ignore)
            |]
            [| t.text (fun item -> string (item ())) |])

let private hookedRows =
    Component.define (fun _ (items: Signal<int list>) -> items) (fun t ->
        t.div
            [||]
            [|
                t.each id (fun (items: Signal<int list>) -> Array.ofList items.Value) hookedRow
            |])

let private templateRowsAfterLayout queued () =
    use graph = new Graph ()
    let rows = graph.Run (fun () -> Api.createSignal [ 1 ])
    rowsProbe <- { Log = ResizeArray (); Host = host () }

    let mounted =
        mountTemplate queued graph strict rowsProbe.Host (Component.render hookedRows rows)

    graph.Run (fun () -> rows.Value <- [ 1; 2 ])
    mounted.Flush ()
    equal [ "row 1 true"; "row 2 true" ] (List.ofSeq rowsProbe.Log)

    graph.Run (fun () ->
        Api.batch (fun () ->
            rows.Value <- [ 1; 2; 3 ]
            rows.Value <- [ 1; 2 ]))

    mounted.Flush ()
    equal ([ "row 1 true"; "row 2 true" ], "12") (List.ofSeq rowsProbe.Log, rowsProbe.Host.textContent)
    mounted.Dispose ()

let mutable private armProbe: Probe = Unchecked.defaultof<_>

let private armSpan =
    Component.define (fun _ (payload: unit -> int) -> payload) (fun t ->
        t.span
            [|
                t.Hooks.mounted (fun payload (node: HTMLSpanElement) ->
                    let key = payload ()
                    armProbe.Log.Add (sprintf "arm %d %b" key (armProbe.Host.contains node))
                    fun () -> armProbe.Log.Add (sprintf "arm cleanup %d" key))
            |]
            [| t.text (fun payload -> string (payload ())) |])

let private armHost =
    Component.define (fun _ (arm: Signal<int>) -> arm) (fun t ->
        t.div
            [||]
            [|
                t.branch (fun (arm: Signal<int>) -> arm.Value) (fun key -> t.arm armSpan key)
            |])

let private templateArmsReplace queued () =
    use graph = new Graph ()
    let arm = graph.Run (fun () -> Api.createSignal 0)
    armProbe <- { Log = ResizeArray (); Host = host () }

    let mounted =
        mountTemplate queued graph strict armProbe.Host (Component.render armHost arm)

    graph.Run (fun () -> arm.Value <- 1)
    mounted.Flush ()
    equal [ "arm 0 true"; "arm cleanup 0"; "arm 1 true" ] (List.ofSeq armProbe.Log)
    mounted.Dispose ()

let private failingDiv =
    Component.define (fun _ (log: ResizeArray<string>) -> log) (fun t ->
        t.div
            [|
                t.Hooks.mounted (fun log (_: HTMLDivElement) ->
                    log.Add "first"
                    fun () -> log.Add "first cleanup")
                t.Hooks.mounted (fun _ (_: HTMLDivElement) -> failwith "hook")
            |]
            [||])

let private templateInitialHookFailure queued () =
    use graph = new Graph ()
    let log = ResizeArray<string>()
    let target = host ()

    let raised =
        try
            mountTemplate queued graph strict target (Component.render failingDiv log)
            |> ignore

            None
        with error ->
            Some error.Message

    equal (Some "hook", [ "first"; "first cleanup" ], 0.) (raised, List.ofSeq log, target.childNodes.length)

let private failingRow =
    Component.define (fun _ (item: unit -> int) -> item) (fun t ->
        t.span
            [|
                t.Hooks.mounted (fun item (_: HTMLSpanElement) -> if item () = 2 then failwith "row 2" else ignore)
            |]
            [| t.text (fun item -> string (item ())) |])

let private failingRows =
    Component.define (fun _ (items: Signal<int list>) -> items) (fun t ->
        t.div
            [||]
            [|
                t.each id (fun (items: Signal<int list>) -> Array.ofList items.Value) failingRow
            |])

let private templateLaterHookFailure queued () =
    use graph = new Graph ()
    let rows = graph.Run (fun () -> Api.createSignal [ 1 ])
    let reports = ResizeArray<exn list>()
    let target = host ()

    let mounted =
        mountTemplate queued graph reports.Add target (Component.render failingRows rows)

    graph.Run (fun () -> rows.Value <- [ 1; 2 ])
    mounted.Flush ()
    equal ("12", [ [ "row 2" ] ]) (target.textContent, messages reports)
    mounted.Dispose ()

let private batchedDiv =
    Component.define (fun _ (probe: Probe) -> probe) (fun t ->
        t.div
            [|
                t.Hooks.mounted (fun probe (node: HTMLDivElement) ->
                    probe.Log.Add (sprintf "hook %b" (probe.Host.contains node))
                    ignore)
                t.Hooks.mounted (fun _ (_: HTMLDivElement) -> failwith "late")
            |]
            [||])

let private templateMountInBatch queued () =
    use graph = new Graph ()
    let probe = { Log = ResizeArray (); Host = host () }
    let reports = ResizeArray<exn list>()
    let mounted = ref Unchecked.defaultof<Mounted>

    graph.Run (fun () ->
        Api.batch (fun () ->
            mounted.Value <- mountTemplate queued graph reports.Add probe.Host (Component.render batchedDiv probe)
            probe.Log.Add "returned"))

    equal ([ "returned"; "hook true" ], [ [ "late" ] ]) (List.ofSeq probe.Log, messages reports)
    mounted.Value.Dispose ()

type Field =
    {
        Field: NodeRef<HTMLInputElement>
        Seen: ResizeArray<bool>
    }

let private referenced =
    Component.define
        (fun _ (field: Field) ->
            field.Seen.Add field.Field.Current.IsSome
            field)
        (fun t -> t.input [| t.Hooks.ref (fun field -> field.Field) |])

let private templateNodeRef queued () =
    use graph = new Graph ()

    let field =
        {
            Field = NodeRef.create ()
            Seen = ResizeArray ()
        }

    let target = host ()

    let mounted =
        mountTemplate queued graph strict target (Component.render referenced field)

    let placed =
        match field.Field.Current with
        | Some node -> obj.ReferenceEquals (node, target.querySelector "input")
        | None -> false

    mounted.Dispose ()
    equal ([ false ], true, false) (List.ofSeq field.Seen, placed, field.Field.Current.IsSome)

let private moveRuns = ref 0

let private countedRow =
    Component.define (fun _ (item: unit -> int) -> item) (fun t ->
        t.span
            [|
                t.Hooks.mounted (fun _ (_: HTMLSpanElement) ->
                    moveRuns.Value <- moveRuns.Value + 1
                    ignore)
            |]
            [| t.text (fun item -> string (item ())) |])

let private countedRows =
    Component.define (fun _ (items: Signal<int list>) -> items) (fun t ->
        t.div
            [||]
            [|
                t.each id (fun (items: Signal<int list>) -> Array.ofList items.Value) countedRow
            |])

let private templateMovesKeepHooks queued () =
    use graph = new Graph ()
    let rows = graph.Run (fun () -> Api.createSignal [ 1; 2 ])
    let first, second = host (), host ()
    moveRuns.Value <- 0

    let mounted =
        mountTemplate queued graph strict first (Component.render countedRows rows)

    graph.Run (fun () -> rows.Value <- [ 2; 1 ])
    mounted.Flush ()
    mounted.MoveBefore (second, None)
    equal (2, "21") (moveRuns.Value, second.textContent)
    mounted.Dispose ()

type RefRow =
    {
        Item: unit -> int
        Button: NodeRef<HTMLButtonElement>
    }

let private refReads = ResizeArray<string>()

let private refRow =
    Component.define
        (fun _ (item: unit -> int) ->
            {
                Item = item
                Button = NodeRef.create ()
            })
        (fun t ->
            t.button
                [|
                    t.Attrs.attribute ("data-item", (fun (row: RefRow) -> Some (string (row.Item ()))))
                    t.Hooks.ref (fun row -> row.Button)
                    t.Events.onClick (fun row _ ->
                        refReads.Add (
                            match row.Button.Current with
                            | Some node ->
                                node.getAttribute "data-item"
                                |> Option.defaultValue "absent"
                            | None -> "none"
                        ))
                |]
                [||])

let private refRows =
    Component.define (fun _ (items: Signal<int list>) -> items) (fun t ->
        t.div
            [||]
            [|
                t.each id (fun (items: Signal<int list>) -> Array.ofList items.Value) refRow
            |])

/// <summary>Reverses two rows whose handlers read their row's NodeRef, then clicks the first button.</summary>
let private templateRefAcrossReorder queued () =
    use graph = new Graph ()
    let rows = graph.Run (fun () -> Api.createSignal [ 1; 2 ])
    let target = host ()
    refReads.Clear ()

    let mounted =
        mountTemplate queued graph strict target (Component.render refRows rows)

    graph.Run (fun () -> rows.Value <- [ 2; 1 ])
    mounted.Flush ()

    (target.querySelector "button"
     |> unbox<HTMLButtonElement>)
        .click()

    equal [ "2" ] (List.ofSeq refReads)
    mounted.Dispose ()

let private serverHooked =
    Component.define (fun _ () -> ()) (fun t -> t.div [| t.Hooks.mounted (fun _ (_: HTMLDivElement) -> ignore) |] [| t.text "x" |])

let private templateServerHook () =
    let render policy =
        Html.render policy (Program.run (Component.render serverHooked ()))

    let rejected =
        try
            render Reject |> ignore
            None
        with error ->
            Some error.Message

    equal ("<div>x</div>", Some "Browser capability requires explicit Omit policy") (render Omit, rejected)

/// <summary>The template variant of a check, under synchronous and queued commits.</summary>
let private templated (name: string) (check: bool -> unit -> unit) =
    [|
        for queued in [ false; true ] do
            yield sprintf "template %s: %s" (if queued then "queued" else "synchronous") name, check queued
    |]

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
            templated "a mounted hook runs after the element is in the host and its cleanup runs before the element leaves" templatePlacedInHost
            templated "hooks run in setup order: an instance, its inlined children, then region rows" templateSetupOrder
            templated
                "a row added by a list pass runs its hook after the pass lays it out, and a batch that adds and removes a row creates none"
                templateRowsAfterLayout
            templated "an arm's hook runs after the arm is moved in; a replaced arm's cleanup runs before the next arm's hook" templateArmsReplace
            templated "a hook failure during mountWith unwinds the mount and rethrows" templateInitialHookFailure
            templated "a later hook failure goes to report once and the instance stays" templateLaterHookFailure
            templated "a mount inside Api.batch runs its hooks after the batch and reports their failures" templateMountInBatch
            templated "NodeRef.Current is None during setup, Some while placed and None after disposal" templateNodeRef
            templated "moving a mount or reordering rows does not re-run hooks" templateMovesKeepHooks
            templated "a NodeRef read from a handler after a keyed reorder holds its row's element" templateRefAcrossReorder
            [|
                "template: a mounted hook raises under Reject and writes nothing under Omit", templateServerHook
            |]
        ]

let browserCases: (string * (unit -> unit)) array =
    [| "a mounted hook can focus and measure its element", focusAndMeasure |]
