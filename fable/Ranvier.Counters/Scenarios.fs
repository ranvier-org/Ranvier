/// <summary>
/// The workloads of <c>bench/Ranvier.Counters</c> under Fable, with a Fable.Ripple implementation of each workload
/// that Fable.Ripple's API expresses. Engines of a scenario build the same shape; in the row workloads a root holds a
/// shared source and its rows, and a row is a source of its own plus one reaction reading the row and the shared
/// source.
/// </summary>
module FableCounters.Scenarios

open System
open CounterBench

/// <summary>
/// State built outside the measured region. <c>Run</c> performs the operations the
/// state was prepared for; <c>Teardown</c> releases what <c>Run</c> left behind.
/// </summary>
type Prepared =
    {
        Run: unit -> unit
        Teardown: unit -> unit
    }

type Case =
    {
        Scenario: string
        Engine: string
        /// <summary>
        /// What one operation is, as the report prints it.
        /// </summary>
        Unit: string
        /// <summary>
        /// N: the smaller of the two operation counts measured. The larger is
        /// 2N.
        /// </summary>
        Ops: int
        Prepare: int -> Prepared
    }

[<Literal>]
let RowCount = 1000

[<Literal>]
let UpdateStride = 10

[<Literal>]
let ChainDepth = 4

[<Literal>]
let FormFields = 8

/// <summary>
/// Written by every reaction, so no engine can prove a reaction's result
/// unused.
/// </summary>
let mutable private sink = 0

/// <summary>
/// Written by the acts of the <c>work-*</c> workloads.
/// </summary>
let mutable private label = ""

let private noTeardown () = ()

let private disposeAll (items: IDisposable seq) =
    for item in items do
        item.Dispose ()

module private Ranvier =
    open Ranvier

    /// <summary>
    /// A root and the sources of its rows. Each row's reaction is a
    /// <c>createEffectOn</c> when <c>split</c>, an <c>Effect</c> otherwise.
    /// </summary>
    let root (split: bool) (graph: Graph) =
        graph.CreateRoot (fun owner ->
            let shared = Signal (graph, 0)
            use _ = if split then graph.Activate () else null

            let rows =
                Array.init RowCount (fun i ->
                    let row = Signal (graph, i)

                    if split then
                        createEffectOn (fun () -> shared.Value + row.Value) (fun v -> sink <- v)
                    else
                        new Effect (graph, (fun () -> sink <- shared.Value + row.Value))
                        |> ignore

                    row)

            owner, rows)

    let create (split: bool) (n: int) =
        let graph = new Graph ()
        let roots = ResizeArray<Owner> n

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        roots.Add (fst (root split graph))
            Teardown =
                fun () ->
                    disposeAll (Seq.cast roots)
                    (graph :> IDisposable).Dispose()
        }

    let update (split: bool) (n: int) =
        let graph = new Graph ()
        let owner, rowSources = root split graph

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for i in 0..UpdateStride .. RowCount - 1 do
                            let row = rowSources[i]
                            row.Value <- row.Peek + 1
            Teardown =
                fun () ->
                    owner.Dispose ()
                    (graph :> IDisposable).Dispose()
        }

    let chain (n: int) =
        let graph = new Graph ()

        let source, tail =
            graph.Run (fun () ->
                let source = createSignal 0
                let mutable tail = createMemo (fun _ -> source.Value + 1)

                for _ in 2..ChainDepth do
                    let previous = tail
                    tail <- createMemo (fun _ -> previous.Value + 1)

                source, tail)

        sink <- tail.Value
        let next = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- next.Value + 1
                        source.Value <- next.Value
                        sink <- tail.Value
            Teardown = fun () -> (graph :> IDisposable).Dispose()
        }

    let chainMap (n: int) =
        let graph = new Graph ()
        let source = Signal (graph, 0)

        let tail =
            source
            |> Signal.map (fun v -> v + 1)
            |> Memo.map (fun v -> v + 1)
            |> Memo.map (fun v -> v + 1)
            |> Memo.map (fun v -> v + 1)

        sink <- tail.Value
        let next = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- next.Value + 1
                        source.Value <- next.Value
                        sink <- tail.Value
            Teardown = fun () -> (graph :> IDisposable).Dispose()
        }

    let chainMemoOn (n: int) =
        let graph = new Graph ()

        let source, tail =
            graph.Run (fun () ->
                let source = createSignal 0
                let mutable tail = createMemoOn (fun () -> source.Value) (fun v -> v + 1)

                for _ in 2..4 do
                    let prev = tail
                    tail <- createMemoOn (fun () -> prev.Value) (fun v -> v + 1)

                source, tail)

        sink <- tail.Value
        let next = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- next.Value + 1
                        source.Value <- next.Value
                        sink <- tail.Value
            Teardown = fun () -> (graph :> IDisposable).Dispose()
        }

    let cutoff (n: int) =
        let graph = new Graph ()
        let source = Signal (graph, 1)

        let owner =
            graph.CreateRoot (fun owner ->
                new Effect (graph, (fun () -> sink <- source.Value))
                |> ignore

                owner)

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        source.Value <- 1
            Teardown =
                fun () ->
                    owner.Dispose ()
                    (graph :> IDisposable).Dispose()
        }

    let dispose (split: bool) (n: int) =
        let graph = new Graph ()
        let roots = Array.init n (fun _ -> fst (root split graph))

        {
            Run =
                fun () ->
                    for owner in roots do
                        owner.Dispose ()
            Teardown = fun () -> (graph :> IDisposable).Dispose()
        }

    /// <summary>
    /// One reaction over a source written with a new value each operation,
    /// whose derived value never changes. <c>split</c> derives it in
    /// <c>createEffectOn</c>'s compute; otherwise an <c>Effect</c> derives it in its body.
    /// </summary>
    let derivedCutoff (split: bool) (n: int) =
        let graph = new Graph ()
        let source = Signal (graph, 1)

        let owner =
            graph.CreateRoot (fun owner ->
                if split then
                    use _ = graph.Activate ()
                    createEffectOn (fun () -> source.Value >>> 30) (fun v -> sink <- v)
                else
                    new Effect (graph, (fun () -> sink <- source.Value >>> 30))
                    |> ignore

                owner)

        let next = ref 1

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- next.Value + 1
                        source.Value <- next.Value
            Teardown =
                fun () ->
                    owner.Dispose ()
                    (graph :> IDisposable).Dispose()
        }

    /// <summary>
    /// <c>RowCount</c> rows, each a source and one reaction whose act formats a
    /// label from <c>derive</c> of the row's value. An operation increments every
    /// <c>UpdateStride</c>th row. <c>split</c> builds the reaction with <c>createEffectOn</c>.
    /// </summary>
    let private labelled (derive: int -> int) (split: bool) (n: int) =
        let graph = new Graph ()

        let owner, rowSources =
            graph.CreateRoot (fun owner ->
                use _ = graph.Activate ()

                let rows =
                    Array.init RowCount (fun i ->
                        let row = Signal (graph, i)

                        if split then
                            createEffectOn (fun () -> derive row.Value) (fun v -> label <- String.Concat ("row ", string v))
                        else
                            createEffect (fun () -> label <- String.Concat ("row ", string (derive row.Value)))

                        row)

                owner, rows)

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for i in 0..UpdateStride .. RowCount - 1 do
                            let row = rowSources[i]
                            row.Value <- row.Peek + 1
            Teardown =
                fun () ->
                    owner.Dispose ()
                    (graph :> IDisposable).Dispose()
        }

    /// <summary>
    /// A label of the row's tier: one write in ten changes it.
    /// </summary>
    let status = labelled (fun v -> v / 10)

    /// <summary>
    /// A label of the row's value: every write changes it.
    /// </summary>
    let value = labelled id

    /// <summary>
    /// <c>RowCount / FormFields</c> forms of <c>FormFields</c> field sources. Each
    /// form's reaction derives validity from its fields and writes it to a
    /// signal read by one downstream effect. An operation increments one
    /// field of every form. <c>split</c> builds the reaction with <c>createEffectOn</c>.
    /// </summary>
    let form (split: bool) (n: int) =
        let graph = new Graph ()
        let forms = RowCount / FormFields

        let owner, fieldSources =
            graph.CreateRoot (fun owner ->
                use _ = graph.Activate ()

                let fields =
                    Array.init forms (fun f ->
                        let fields = Array.init FormFields (fun i -> Signal (graph, (f + i) % 50))
                        let valid = Signal (graph, false)

                        let validate () =
                            let mutable sum = 0
                            let mutable filled = true

                            for field in fields do
                                let v = field.Value
                                sum <- sum + v
                                filled <- filled && v > 0

                            filled && sum < 300

                        if split then
                            createEffectOn validate (fun v -> valid.Value <- v)
                        else
                            createEffect (fun () -> valid.Value <- validate ())

                        createEffect (fun () -> sink <- if valid.Value then 1 else 0)
                        fields)

                owner, fields)

        let next = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- (next.Value + 1) % FormFields

                        for fields in fieldSources do
                            let field = fields[next.Value]
                            field.Value <- (field.Peek + 1) % 50
            Teardown =
                fun () ->
                    owner.Dispose ()
                    (graph :> IDisposable).Dispose()
        }

module private Ripple =
    open Fable.Ripple

    /// <summary>
    /// A root and the sources of its rows.
    /// </summary>
    let root () =
        let rows, handle =
            Signal.root (fun () ->
                let shared = Var.create 0

                Array.init RowCount (fun i ->
                    let row = Var.create i

                    Signal.effect (fun () -> sink <- shared.Value + row.Value)
                    |> ignore

                    row))

        handle, rows

    let create (n: int) =
        let roots = ResizeArray<IDisposable> n

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        roots.Add (fst (root ()))
            Teardown = fun () -> disposeAll roots
        }

    let update (n: int) =
        let handle, rowSources = root ()

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for i in 0..UpdateStride .. RowCount - 1 do
                            let row = rowSources[i]
                            row.Value <- row.Peek () + 1
            Teardown = fun () -> handle.Dispose ()
        }

    let chain (n: int) =
        let source = Var.create 0
        let mutable tail = Signal.computed (fun () -> source.Value + 1)

        for _ in 2..ChainDepth do
            let previous = tail
            tail <- Signal.computed (fun () -> previous.Value + 1)

        let tail = tail
        sink <- tail.Value
        let next = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- next.Value + 1
                        source.Value <- next.Value
                        sink <- tail.Value
            Teardown = noTeardown
        }

    let cutoff (n: int) =
        let source = Var.create 1

        let (), handle =
            Signal.root (fun () ->
                Signal.effect (fun () -> sink <- source.Value)
                |> ignore)

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        source.Value <- 1
            Teardown = fun () -> handle.Dispose ()
        }

    let dispose (n: int) =
        let roots = Array.init n (fun _ -> fst (root ()))

        {
            Run =
                fun () ->
                    for handle in roots do
                        handle.Dispose ()
            Teardown = noTeardown
        }

    /// <summary>
    /// The <c>Ranvier.chainMap</c> shape: four <c>Signal.map</c> steps over a source.
    /// </summary>
    let chainMap (n: int) =
        let source = Var.create 0

        let tail =
            (source :> Signal<int>)
            |> Signal.map (fun v -> v + 1)
            |> Signal.map (fun v -> v + 1)
            |> Signal.map (fun v -> v + 1)
            |> Signal.map (fun v -> v + 1)

        sink <- tail.Value
        let next = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- next.Value + 1
                        source.Value <- next.Value
                        sink <- tail.Value
            Teardown = noTeardown
        }

    /// <summary>
    /// The <c>Ranvier.derivedCutoff</c> shape. <c>split</c> derives the value in a <c>Signal.computed</c> read by the
    /// effect; otherwise the effect derives it in its body.
    /// </summary>
    let derivedCutoff (split: bool) (n: int) =
        let source = Var.create 1

        let (), handle =
            Signal.root (fun () ->
                if split then
                    let derived = Signal.computed (fun () -> source.Value >>> 30)

                    Signal.effect (fun () -> sink <- derived.Value)
                    |> ignore
                else
                    Signal.effect (fun () -> sink <- source.Value >>> 30)
                    |> ignore)

        let next = ref 1

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- next.Value + 1
                        source.Value <- next.Value
            Teardown = fun () -> handle.Dispose ()
        }

    /// <summary>
    /// The <c>Ranvier.labelled</c> shape. <c>split</c> derives the row's value in a <c>Signal.computed</c> read by the
    /// formatting effect.
    /// </summary>
    let private labelled (derive: int -> int) (split: bool) (n: int) =
        let rowSources, handle =
            Signal.root (fun () ->
                Array.init RowCount (fun i ->
                    let row = Var.create i

                    if split then
                        let derived = Signal.computed (fun () -> derive row.Value)

                        Signal.effect (fun () -> label <- String.Concat ("row ", string derived.Value))
                        |> ignore
                    else
                        Signal.effect (fun () -> label <- String.Concat ("row ", string (derive row.Value)))
                        |> ignore

                    row))

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for i in 0..UpdateStride .. RowCount - 1 do
                            let row = rowSources[i]
                            row.Value <- row.Peek () + 1
            Teardown = fun () -> handle.Dispose ()
        }

    let status = labelled (fun v -> v / 10)

    let value = labelled id

    /// <summary>
    /// The <c>Ranvier.form</c> shape. <c>split</c> derives validity in a <c>Signal.computed</c>; otherwise an effect
    /// writes it to a <c>Var</c>. One downstream effect reads validity.
    /// </summary>
    let form (split: bool) (n: int) =
        let forms = RowCount / FormFields

        let fieldSources, handle =
            Signal.root (fun () ->
                Array.init forms (fun f ->
                    let fields = Array.init FormFields (fun i -> Var.create ((f + i) % 50))

                    let validate () =
                        let mutable sum = 0
                        let mutable filled = true

                        for field in fields do
                            let v = field.Value
                            sum <- sum + v
                            filled <- filled && v > 0

                        filled && sum < 300

                    let valid: Signal<bool> =
                        if split then
                            Signal.computed validate
                        else
                            let valid = Var.create false

                            Signal.effect (fun () -> valid.Value <- validate ())
                            |> ignore

                            valid :> Signal<bool>

                    Signal.effect (fun () -> sink <- if valid.Value then 1 else 0)
                    |> ignore

                    fields))

        let next = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- (next.Value + 1) % FormFields

                        for fields in fieldSources do
                            let field = fields[next.Value]
                            field.Value <- (field.Peek () + 1) % 50
            Teardown = fun () -> handle.Dispose ()
        }

    /// <summary>The <c>Workloads.wideDiamond</c> shape with <c>Signal.computed</c> branches and join.</summary>
    let wideDiamond (n: int) =
        let source = Var.create 0

        let (), handle =
            Signal.root (fun () ->
                let branches =
                    Array.init Workloads.DiamondWidth (fun j -> Signal.computed (fun () -> source.Value + j))

                let sum =
                    Signal.computed (fun () ->
                        let mutable total = 0

                        for branch in branches do
                            total <- total + branch.Value

                        total)

                Signal.effect (fun () -> sink <- sum.Value)
                |> ignore)

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        source.Value <- source.Peek () + 1
            Teardown = fun () -> handle.Dispose ()
        }

    /// <summary>The <c>Workloads.dynamicBranches</c> shape with one <c>Signal.effect</c> per reader.</summary>
    let dynamicBranches (n: int) =
        let condition = Var.create true
        let a = Array.init Workloads.DynamicReaders Var.create
        let b = Array.init Workloads.DynamicReaders Var.create

        let (), handle =
            Signal.root (fun () ->
                for i in 0 .. Workloads.DynamicReaders - 1 do
                    Signal.effect (fun () -> sink <- if condition.Value then a[i].Value else b[i].Value)
                    |> ignore)

        {
            Run =
                fun () ->
                    for op in 1..n do
                        if op % 2 = 1 then
                            condition.Value <- not (condition.Peek ())
                        else
                            let active = if condition.Peek () then a else b

                            for source in active do
                                source.Value <- source.Peek () + 1
            Teardown = fun () -> handle.Dispose ()
        }

    /// <summary>
    /// The <c>Workloads.selection</c> shape. <c>read</c> builds the row's selection test from the selected index.
    /// </summary>
    let private selectionWith (read: Var<int> -> (int -> bool)) (n: int) =
        let rows = Array.init Workloads.Rows Var.create
        let selected = Var.create 0

        let (), handle =
            Signal.root (fun () ->
                let isSelected = read selected

                for i in 0 .. Workloads.Rows - 1 do
                    Signal.effect (fun () -> sink <- if isSelected i then rows[i].Value else 0)
                    |> ignore)

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        selected.Value <- (selected.Peek () + 37) % Workloads.Rows
            Teardown = fun () -> handle.Dispose ()
        }

    /// <summary>Every row compares the selected index.</summary>
    let selectionCompare = selectionWith (fun selected i -> selected.Value = i)


    /// <summary>The <c>Workloads.batchWrites</c> shape: every write of an operation inside one <c>Signal.batch</c>.</summary>
    let batchWrites (n: int) =
        let sources = Array.init Workloads.BatchWidth Var.create

        let (), handle =
            Signal.root (fun () ->
                Signal.effect (fun () ->
                    let mutable total = 0

                    for source in sources do
                        total <- total + source.Value

                    sink <- total)
                |> ignore)

        let write () =
            for source in sources do
                source.Value <- source.Peek () + 1

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        Signal.batch write
            Teardown = fun () -> handle.Dispose ()
        }

/// <summary>
/// Solid 2 (<c>@solidjs/signals</c>) implementations. Solid applies writes when it flushes, so every operation calls
/// <c>flush</c> after each write it makes, and the effects of a newly built graph run in a <c>flush</c> outside the
/// measured region. Solid's <c>createEffect</c> separates a tracked compute from an untracked effect, as Ranvier's
/// <c>createEffectOn</c> does.
/// </summary>
module private Solid =
    open Fable.Core
    open Fable.Core.JsInterop

    [<ImportMember("@solidjs/signals")>]
    let private createSignal (value: 'T) : (unit -> 'T) * ('T -> unit) = jsNative

    [<ImportMember("@solidjs/signals")>]
    let private createMemo (compute: unit -> 'T) : unit -> 'T = jsNative

    [<ImportMember("@solidjs/signals")>]
    let private createEffect (compute: unit -> 'T) (effect: 'T -> unit) : unit = jsNative

    [<ImportMember("@solidjs/signals")>]
    let private createRoot (init: (unit -> unit) -> 'T) : 'T = jsNative

    [<ImportMember("@solidjs/signals")>]
    let private flush () : unit = jsNative

    [<ImportMember("@solidjs/signals")>]
    let private createProjection (fn: obj -> unit) (seed: obj) : obj = jsNative

    [<ImportMember("@solidjs/signals")>]
    let private mapArray (list: unit -> 'T[]) (map: Func<unit -> 'T, unit -> int, 'U>) (options: obj) : unit -> 'U[] = jsNative

    /// <summary>An effect function that writes its value through <c>set</c> and returns no cleanup.</summary>
    [<Emit("(v) => { $0(v); }")>]
    let private writeTo (set: 'T -> unit) : 'T -> unit = jsNative

    [<Emit("$0[$1] = true")>]
    let private mark (target: obj) (key: int) : unit = jsNative

    [<Emit("delete $0[$1]")>]
    let private unmark (target: obj) (key: int) : unit = jsNative

    [<Emit("$0[$1] === true")>]
    let private marked (target: obj) (key: int) : bool = jsNative

    /// <summary>A root and the getters and setters of its rows, each row read with the shared source by one effect.</summary>
    let private root () =
        createRoot (fun dispose ->
            let shared, _ = createSignal 0

            let rows =
                Array.init RowCount (fun i ->
                    let row, setRow = createSignal i
                    createEffect (fun () -> shared () + row ()) (fun v -> sink <- v)
                    row, setRow)

            dispose, rows)

    let create (n: int) =
        let roots = ResizeArray<unit -> unit> n

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        roots.Add (fst (root ()))
                        flush ()
            Teardown =
                fun () ->
                    for dispose in roots do
                        dispose ()
        }

    let update (n: int) =
        let dispose, rows = root ()
        flush ()

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for i in 0..UpdateStride .. RowCount - 1 do
                            let row, setRow = rows[i]
                            setRow (row () + 1)
                            flush ()
            Teardown = dispose
        }

    let chain (n: int) =
        let dispose, setSource, tail =
            createRoot (fun dispose ->
                let source, setSource = createSignal 0
                let mutable tail = createMemo (fun () -> source () + 1)

                for _ in 2..ChainDepth do
                    let previous = tail
                    tail <- createMemo (fun () -> previous () + 1)

                dispose, setSource, tail)

        flush ()
        sink <- tail ()
        let next = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- next.Value + 1
                        setSource next.Value
                        flush ()
                        sink <- tail ()
            Teardown = dispose
        }

    let cutoff (n: int) =
        let dispose, setSource =
            createRoot (fun dispose ->
                let source, setSource = createSignal 1
                createEffect source (fun v -> sink <- v)
                dispose, setSource)

        flush ()

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        setSource 1
                        flush ()
            Teardown = dispose
        }

    let dispose (n: int) =
        let roots =
            Array.init n (fun _ ->
                let dispose, _ = root ()
                flush ()
                dispose)

        {
            Run =
                fun () ->
                    for dispose in roots do
                        dispose ()
            Teardown = noTeardown
        }

    /// <summary>The <c>Ranvier.derivedCutoff</c> shape, deriving the value in the effect's compute.</summary>
    let derivedCutoff (n: int) =
        let dispose, setSource =
            createRoot (fun dispose ->
                let source, setSource = createSignal 1
                createEffect (fun () -> source () >>> 30) (fun v -> sink <- v)
                dispose, setSource)

        flush ()
        let next = ref 1

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- next.Value + 1
                        setSource next.Value
                        flush ()
            Teardown = dispose
        }

    /// <summary>The <c>Ranvier.labelled</c> shape: the effect's compute derives the row's value, its effect formats it.</summary>
    let private labelled (derive: int -> int) (n: int) =
        let dispose, rows =
            createRoot (fun dispose ->
                let rows =
                    Array.init RowCount (fun i ->
                        let row, setRow = createSignal i

                        createEffect (fun () -> derive (row ())) (fun v -> label <- String.Concat ("row ", string v))

                        row, setRow)

                dispose, rows)

        flush ()

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for i in 0..UpdateStride .. RowCount - 1 do
                            let row, setRow = rows[i]
                            setRow (row () + 1)
                            flush ()
            Teardown = dispose
        }

    let status = labelled (fun v -> v / 10)

    let value = labelled id

    /// <summary>
    /// The <c>Ranvier.form</c> shape. With <c>memo</c>, validity is a <c>createMemo</c>; otherwise an effect writes it
    /// to a signal. One downstream effect reads validity.
    /// </summary>
    let form (memo: bool) (n: int) =
        let forms = RowCount / FormFields

        let dispose, fieldSources =
            createRoot (fun dispose ->
                let fields =
                    Array.init forms (fun f ->
                        let fields = Array.init FormFields (fun i -> createSignal ((f + i) % 50))

                        let validate () =
                            let mutable sum = 0
                            let mutable filled = true

                            for field, _ in fields do
                                let v = field ()
                                sum <- sum + v
                                filled <- filled && v > 0

                            filled && sum < 300

                        let valid =
                            if memo then
                                createMemo validate
                            else
                                let valid, setValid = createSignal false
                                createEffect validate (writeTo setValid)
                                valid

                        createEffect valid (fun v -> sink <- if v then 1 else 0)
                        fields)

                dispose, fields)

        flush ()
        let next = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- (next.Value + 1) % FormFields

                        for fields in fieldSources do
                            let field, setField = fields[next.Value]
                            setField ((field () + 1) % 50)
                            flush ()
            Teardown = dispose
        }

    /// <summary>The <c>Workloads.wideDiamond</c> shape with <c>createMemo</c> branches and join.</summary>
    let wideDiamond (n: int) =
        let dispose, source, setSource =
            createRoot (fun dispose ->
                let source, setSource = createSignal 0

                let branches =
                    Array.init Workloads.DiamondWidth (fun j -> createMemo (fun () -> source () + j))

                let sum =
                    createMemo (fun () ->
                        let mutable total = 0

                        for branch in branches do
                            total <- total + branch ()

                        total)

                createEffect sum (fun v -> sink <- v)
                dispose, source, setSource)

        flush ()

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        setSource (source () + 1)
                        flush ()
            Teardown = dispose
        }

    /// <summary>The <c>Workloads.dynamicBranches</c> shape with one effect per reader.</summary>
    let dynamicBranches (n: int) =
        let dispose, condition, a, b =
            createRoot (fun dispose ->
                let condition = createSignal true
                let a = Array.init Workloads.DynamicReaders (fun i -> createSignal i)
                let b = Array.init Workloads.DynamicReaders (fun i -> createSignal i)
                let isA = fst condition

                for i in 0 .. Workloads.DynamicReaders - 1 do
                    let readA = fst a[i]
                    let readB = fst b[i]
                    createEffect (fun () -> if isA () then readA () else readB ()) (fun v -> sink <- v)

                dispose, condition, a, b)

        flush ()
        let isA, setA = condition

        {
            Run =
                fun () ->
                    for op in 1..n do
                        if op % 2 = 1 then
                            setA (not (isA ()))
                            flush ()
                        else
                            for read, write in (if isA () then a else b) do
                                write (read () + 1)
                                flush ()
            Teardown = dispose
        }

    /// <summary>
    /// The <c>Workloads.selection</c> shape. With <c>projection</c>, every row reads its selection from one
    /// <c>createProjection</c> that marks the selected key; otherwise every row compares the selected index.
    /// </summary>
    let selection (projection: bool) (n: int) =
        let dispose, selected, setSelected =
            createRoot (fun dispose ->
                let rows = Array.init Workloads.Rows (fun i -> fst (createSignal i))
                let selected, setSelected = createSignal 0

                let isSelected: int -> bool =
                    if projection then
                        let previous = ref -1

                        let store =
                            createProjection
                                (fun draft ->
                                    let key = selected ()

                                    if previous.Value >= 0 then
                                        unmark draft previous.Value

                                    mark draft key
                                    previous.Value <- key)
                                (createObj [])

                        marked store
                    else
                        fun i -> selected () = i

                for i in 0 .. Workloads.Rows - 1 do
                    let row = rows[i]
                    createEffect (fun () -> if isSelected i then row () else 0) (fun v -> sink <- v)

                dispose, selected, setSelected)

        flush ()

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        setSelected ((selected () + 37) % Workloads.Rows)
                        flush ()
            Teardown = dispose
        }

    /// <summary>The <c>Workloads.batchWrites</c> shape: every write of an operation lands in one <c>flush</c>.</summary>
    let batchWrites (n: int) =
        let dispose, sources =
            createRoot (fun dispose ->
                let sources = Array.init Workloads.BatchWidth (fun i -> createSignal i)

                createEffect
                    (fun () ->
                        let mutable total = 0

                        for read, _ in sources do
                            total <- total + read ()

                        total)
                    (fun v -> sink <- v)

                dispose, sources)

        flush ()

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for read, write in sources do
                            write (read () + 1)

                        flush ()
            Teardown = dispose
        }

    /// <summary>
    /// The <c>Workloads.keyedPass</c> shape with fresh items: a <c>mapArray</c> keyed by Id, one effect per mapped row
    /// reading its value, and one effect reading the row count.
    /// </summary>
    let keyed (edit: Workloads.KeyedItem[] -> Workloads.KeyedItem[]) (n: int) =
        let initial =
            Array.init Workloads.Rows (fun i ->
                {
                    Workloads.KeyedItem.Id = i
                    Workloads.KeyedItem.Value = i
                })

        let edited = edit initial

        let writes =
            Array.init n (fun op ->
                (if op % 2 = 0 then edited else initial)
                |> Array.map (fun item -> { item with Id = item.Id }))

        let dispose, setSource =
            createRoot (fun dispose ->
                let source, setSource = createSignal initial

                let rows =
                    mapArray
                        source
                        (Func<_, _, _>(fun (item: unit -> Workloads.KeyedItem) (_: unit -> int) ->
                            createEffect (fun () -> (item ()).Value) (fun v -> sink <- v)))
                        (createObj
                            [
                                "keyed"
                                ==> Func<Workloads.KeyedItem, int>(fun item -> item.Id)
                            ])

                createEffect (fun () -> (rows ()).Length) (fun v -> sink <- v)
                dispose, setSource)

        flush ()

        {
            Run =
                fun () ->
                    for write in writes do
                        setSource write
                        flush ()
            Teardown = dispose
        }

let private workload (prepare: int -> Workloads.Workload) (n: int) : Prepared =
    let prepared = prepare n

    {
        Run = prepared.Run
        Teardown = prepared.Teardown
    }

let private scenario name unit ops (engines: (string * (int -> Prepared)) list) =
    engines
    |> List.map (fun (engine, prepare) ->
        {
            Scenario = name
            Engine = engine
            Unit = unit
            Ops = ops
            Prepare = prepare
        })

/// <summary>
/// Every case, in report order, with the operation counts of the .NET bench.
/// <c>scale</c> multiplies each scenario's N.
/// </summary>
let all (scale: int) : Case list =
    [
        for mode in [ "debounce"; "first"; "last"; "both" ] do
            yield!
                scenario
                    ("timed-capture-" + mode)
                    "capture a changed input in 64 timed nodes"
                    (1000 * scale)
                    [ "Ranvier", workload (Workloads.timed mode false) ]

            yield!
                scenario
                    ("timed-admit-" + mode)
                    "capture and admit in 64 timed nodes"
                    (1000 * scale)
                    [ "Ranvier", workload (Workloads.timed mode true) ]
        yield!
            scenario
                "create"
                $"one root of %d{RowCount} rows"
                (8 * scale)
                [
                    "Ranvier", Ranvier.create false
                    "Fable.Ripple", Ripple.create
                    "Solid", Solid.create
                ]
        yield!
            scenario
                "update"
                $"write every %d{UpdateStride}th of %d{RowCount} rows"
                (50 * scale)
                [
                    "Ranvier", Ranvier.update false
                    "Fable.Ripple", Ripple.update
                    "Solid", Solid.update
                ]
        yield!
            scenario
                "chain"
                $"write the source of %d{ChainDepth} memos, read the tail"
                (5000 * scale)
                [ "Ranvier", Ranvier.chain; "Fable.Ripple", Ripple.chain; "Solid", Solid.chain ]
        yield!
            scenario
                "cutoff"
                "write an equal value to an observed source"
                (50000 * scale)
                [
                    "Ranvier", Ranvier.cutoff
                    "Fable.Ripple", Ripple.cutoff
                    "Solid", Solid.cutoff
                ]
        yield!
            scenario
                "dispose"
                $"dispose one root of %d{RowCount} rows"
                (8 * scale)
                [
                    "Ranvier", Ranvier.dispose false
                    "Fable.Ripple", Ripple.dispose
                    "Solid", Solid.dispose
                ]
        yield! scenario "create-on" $"one root of %d{RowCount} createEffectOn rows" (8 * scale) [ "Ranvier", Ranvier.create true ]
        yield!
            scenario "update-on" $"write every %d{UpdateStride}th of %d{RowCount} createEffectOn rows" (50 * scale) [ "Ranvier", Ranvier.update true ]
        yield! scenario "dispose-on" $"dispose one root of %d{RowCount} createEffectOn rows" (8 * scale) [ "Ranvier", Ranvier.dispose true ]
        yield!
            scenario
                "chain-map"
                "write the source of 4 mapped memos, read the tail"
                (5000 * scale)
                [ "Ranvier", Ranvier.chainMap; "Fable.Ripple", Ripple.chainMap ]
        yield! scenario "chain-on" "write the source of 4 createMemoOn memos, read the tail" (5000 * scale) [ "Ranvier", Ranvier.chainMemoOn ]
        yield!
            scenario
                "derive-effect"
                "write a new value whose derived value is unchanged, Effect"
                (50000 * scale)
                [
                    "Ranvier", Ranvier.derivedCutoff false
                    "Fable.Ripple", Ripple.derivedCutoff false
                ]
        yield!
            scenario
                "derive-on"
                "write a new value whose derived value is unchanged, createEffectOn"
                (50000 * scale)
                [
                    "Ranvier", Ranvier.derivedCutoff true
                    "Fable.Ripple (computed + effect)", Ripple.derivedCutoff true
                    "Solid", Solid.derivedCutoff
                ]
        yield!
            scenario
                "work-status"
                "write every 10th of 1000 rows; the act formats a label, 1 write in 10 changes it"
                (50 * scale)
                [
                    "Ranvier (createEffect)", Ranvier.status false
                    "Ranvier (createEffectOn)", Ranvier.status true
                    "Fable.Ripple (effect)", Ripple.status false
                    "Fable.Ripple (computed + effect)", Ripple.status true
                    "Solid (createEffect)", Solid.status
                ]
        yield!
            scenario
                "work-value"
                "write every 10th of 1000 rows; the act formats a label, every write changes it"
                (50 * scale)
                [
                    "Ranvier (createEffect)", Ranvier.value false
                    "Ranvier (createEffectOn)", Ranvier.value true
                    "Fable.Ripple (effect)", Ripple.value false
                    "Fable.Ripple (computed + effect)", Ripple.value true
                    "Solid (createEffect)", Solid.value
                ]
        yield!
            scenario
                "work-form"
                "write one field of each of 125 8-field forms; validity feeds a signal and an effect"
                (50 * scale)
                [
                    "Ranvier (createEffect)", Ranvier.form false
                    "Ranvier (createEffectOn)", Ranvier.form true
                    "Fable.Ripple (effect)", Ripple.form false
                    "Fable.Ripple (computed)", Ripple.form true
                    "Solid (createEffect)", Solid.form false
                    "Solid (createMemo)", Solid.form true
                ]
        for name, unit, edit in
            [
                "keyed-remove", "alternately remove and restore the middle row", Workloads.removeMiddle
                "keyed-swap", "alternately swap and restore the second and second-to-last rows", Workloads.swapInner
                "keyed-insert", "alternately prepend a row and restore", Workloads.insertFirst
            ] do
            yield!
                scenario
                    name
                    $"%s{unit} of a %d{Workloads.Rows}-row projection"
                    (50 * scale)
                    [
                        "Ranvier (fresh items)", workload (Workloads.keyedPass true edit)
                        "Ranvier (stable items)", workload (Workloads.keyedPass false edit)
                        "Solid (mapArray)", Solid.keyed edit
                    ]
        yield!
            scenario
                "app-table"
                $"alternate a filter query and a sort direction over %d{Workloads.Rows} rows"
                (50 * scale)
                [ "Ranvier", workload Workloads.table ]
        yield!
            scenario
                "app-detail"
                $"move the selection over %d{Workloads.Rows} rows; the detail rebuilds %d{Workloads.DetailNodes} memo and effect pairs"
                (50 * scale)
                [ "Ranvier", workload Workloads.detail ]
        yield!
            scenario
                "shape-diamond"
                $"write a source read by %d{Workloads.DiamondWidth} memos joined by one"
                (5000 * scale)
                [
                    "Ranvier", workload Workloads.wideDiamond
                    "Fable.Ripple", Ripple.wideDiamond
                    "Solid", Solid.wideDiamond
                ]
        yield!
            scenario
                "shape-dynamic"
                $"alternate a branch flip and a write of every active source of %d{Workloads.DynamicReaders} effects"
                (500 * scale)
                [
                    "Ranvier", workload Workloads.dynamicBranches
                    "Fable.Ripple", Ripple.dynamicBranches
                    "Solid", Solid.dynamicBranches
                ]
        yield!
            scenario
                "select"
                $"move the selection over %d{Workloads.Rows} rows, each read by one effect"
                (50 * scale)
                [
                    "Ranvier", workload (Workloads.selection true)
                    "Ranvier (compare)", workload (Workloads.selection false)
                    "Fable.Ripple (compare)", Ripple.selectionCompare
                    "Solid (createProjection)", Solid.selection true
                    "Solid (compare)", Solid.selection false
                ]
        yield!
            scenario
                "batch"
                $"write %d{Workloads.BatchWidth} sources read by one effect inside one batch"
                (5000 * scale)
                [
                    "Ranvier", workload Workloads.batchWrites
                    "Fable.Ripple", Ripple.batchWrites
                    "Solid", Solid.batchWrites
                ]
        yield!
            scenario
                "async-resolve"
                $"reload %d{Workloads.Widgets} suspense widgets of %d{Workloads.SourcesPerWidget} sources, then settle each"
                (50 * scale)
                [ "Ranvier", workload Workloads.asyncResolve ]
        yield!
            scenario
                "async-recover"
                "fail one source of one error-boundary widget, then settle a replacement"
                (500 * scale)
                [ "Ranvier", workload Workloads.asyncRecover ]
        for burst in [ 1; 16; 256 ] do
            yield!
                scenario
                    $"stream-burst-%d{burst}"
                    $"deliver %d{burst} items per round into a stream fold read by a memo and an effect"
                    ((if burst = 256 then 20 else 500) * scale)
                    [
                        "Ranvier (stream, batched)", workload (Workloads.streamBurst burst)
                        "Ranvier (AsyncSource, batched)", workload (Workloads.settleBurst true burst)
                        "Ranvier (AsyncSource, unbatched)", workload (Workloads.settleBurst false burst)
                    ]
        yield!
            scenario
                "stream-restart"
                "write a dependency of a stream, restarting it, then deliver one item; or of an async memo, restarting its flight"
                (500 * scale)
                [
                    "Ranvier (stream)", workload Workloads.streamRestart
                    "Ranvier (async memo, CancelPrevious)", workload Workloads.flightRestart
                ]
    ]
