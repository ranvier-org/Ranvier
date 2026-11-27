/// <summary>
/// The workloads, one implementation per engine. In the first five, every engine builds the same shape: a root holds
/// a shared source and its rows, and a row is a source of its own plus one reaction reading the row and the shared
/// source. The cost of a root is constant in the number of roots alive. The other workloads measure Ranvier and each
/// comparator library with a counterpart API.
/// </summary>
module CounterBench.Scenarios

open System
open System.Threading.Tasks
open FSharp.Data.Adaptive
open Ranvier
open Ranvier.Elmish

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

[<Literal>]
let ModelFields = 64

/// <summary>
/// Written by every reaction, which keeps every reaction's result live.
/// </summary>
let mutable private sink = 0

/// <summary>
/// Written by the acts of the <c>work-*</c> workloads.
/// </summary>
let mutable private label = ""

let private noTeardown () = ()

let private disposeAll (items: ResizeArray<IDisposable>) =
    for i in 0 .. items.Count - 1 do
        items[i].Dispose()

module private Ranvier =

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
        let graph = Workloads.newGraph ()
        let roots = ResizeArray<Owner> n

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        roots.Add (fst (root split graph))
            Teardown =
                fun () ->
                    for i in 0 .. roots.Count - 1 do
                        (roots[i] :> IDisposable).Dispose()

                    (graph :> IDisposable).Dispose()
        }

    let update (split: bool) (n: int) =
        let graph = Workloads.newGraph ()
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

    /// <summary>A signal and a chain of <c>ChainDepth</c> memos over it in <c>graph</c>. An operation writes the source and reads the tail.</summary>
    let chainOn (graph: Graph) (n: int) =
        let source = Signal (graph, 0)
        let mutable tail = Memo (graph, (fun _ -> source.Value + 1))

        for _ in 2..ChainDepth do
            let previous = tail
            tail <- Memo (graph, (fun _ -> previous.Value + 1))

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
            Teardown = fun () -> (graph :> IDisposable).Dispose()
        }

    let chain (n: int) =
        chainOn (Workloads.newGraph ()) n

    /// <summary><c>chain</c> in a graph of thread affinity <c>affinity</c>.</summary>
    let chainAffinity (affinity: ThreadAffinity) (n: int) =
        chainOn (new Graph (GraphOptions.Default.WithThreadAffinity affinity)) n

    let chainMap (n: int) =
        let graph = Workloads.newGraph ()
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
        let graph = Workloads.newGraph ()

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
        let graph = Workloads.newGraph ()
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
        let graph = Workloads.newGraph ()
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
        let graph = Workloads.newGraph ()
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
        let graph = Workloads.newGraph ()

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
        let graph = Workloads.newGraph ()
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

    /// <summary>
    /// A projection of <c>RowCount</c> rows keyed by their first component, each row
    /// read by one effect. <c>items</c> is the source's initial value.
    /// </summary>
    let private projection (graph: Graph) (items: (int * int)[]) =
        use _ = graph.Activate ()
        let source = Signal (graph, items)

        let owner =
            graph.CreateRoot (fun owner ->
                let rows = createProjection fst snd (fun () -> source.Value)

                for key in 0 .. RowCount - 1 do
                    new Effect (graph, (fun () -> sink <- rows.Get key))
                    |> ignore

                owner)

        source, owner

    let projectEdit (n: int) =
        let graph = Workloads.newGraph ()
        let items = Array.init RowCount (fun i -> i, i)
        let source, owner = projection graph items

        let writes =
            Array.init n (fun op ->
                let next = Array.copy items
                next[0] <- 0, op + 1
                next)

        {
            Run =
                fun () ->
                    for write in writes do
                        source.Value <- write
            Teardown =
                fun () ->
                    owner.Dispose ()
                    (graph :> IDisposable).Dispose()
        }

    let projectReorder (n: int) =
        let graph = Workloads.newGraph ()
        let forward = Array.init RowCount (fun i -> i, i)
        let reversed = Array.rev forward
        let source, owner = projection graph forward

        {
            Run =
                fun () ->
                    for op in 1..n do
                        source.Value <- if op % 2 = 1 then reversed else forward
            Teardown =
                fun () ->
                    owner.Dispose ()
                    (graph :> IDisposable).Dispose()
        }

    /// <summary>
    /// A <c>RowCount</c>-row projection viewed through <c>filter</c> (even values),
    /// <c>sortBy</c> (descending) and <c>map</c>. One effect reads the tail's keys and
    /// one effect reads each row the filter keeps.
    /// </summary>
    let projectChain (n: int) =
        let graph = Workloads.newGraph ()
        let items = Array.init RowCount (fun i -> i, i)
        use _ = graph.Activate ()
        let source = Signal (graph, items)

        let owner =
            graph.CreateRoot (fun owner ->
                let tail =
                    createProjection fst snd (fun () -> source.Value)
                    |> Projection.filter (fun v -> v % 2 = 0)
                    |> Projection.sortBy (fun v -> -v)
                    |> Projection.map (fun v -> v + 1)

                new Effect (graph, (fun () -> sink <- tail.Keys.Length))
                |> ignore

                for key in 0..2 .. RowCount - 1 do
                    new Effect (graph, (fun () -> sink <- tail.Get key))
                    |> ignore

                owner)

        // Row 0 alternates between the front and the back of the sort order.
        let writes =
            Array.init n (fun op ->
                let next = Array.copy items
                next[0] <- 0, (if op % 2 = 0 then 2 * (RowCount + op) else -2 * (op + 1))
                next)

        {
            Run =
                fun () ->
                    for write in writes do
                        source.Value <- write
            Teardown =
                fun () ->
                    owner.Dispose ()
                    (graph :> IDisposable).Dispose()
        }

    /// <summary>
    /// A <c>RowCount</c>-row projection whose last key is replaced by a key the source has never held on each operation.
    /// One effect reads each other row and one reads <c>Keys</c>; <c>readers</c> key readers are each read by an effect
    /// of their own.
    /// </summary>
    let projectChurn (readers: int) (n: int) =
        let graph = Workloads.newGraph ()
        let items = Array.init RowCount (fun i -> i, i)
        use _ = graph.Activate ()
        let source = Signal (graph, items)

        let owner =
            graph.CreateRoot (fun owner ->
                let rows = createProjection fst snd (fun () -> source.Value)

                for key in 0 .. RowCount - 2 do
                    new Effect (graph, (fun () -> sink <- rows.Get key))
                    |> ignore

                new Effect (graph, (fun () -> sink <- rows.Keys.Length))
                |> ignore

                for _ in 1..readers do
                    let reader = rows.NewKeyReader ()

                    new Effect (graph, (fun () -> sink <- reader.Read().Changes.Count))
                    |> ignore

                owner)

        let writes =
            Array.init n (fun op ->
                let next = Array.copy items
                next[RowCount - 1] <- RowCount + op, op
                next)

        {
            Run =
                fun () ->
                    for write in writes do
                        source.Value <- write
            Teardown =
                fun () ->
                    owner.Dispose ()
                    (graph :> IDisposable).Dispose()
        }

    /// <summary>
    /// An async memo whose body reads a trigger and returns a task left open, in a graph of flight policy
    /// <c>policy</c>. An operation writes the trigger and reads the memo, twice, then completes every open task, reading
    /// the memo after each round, until none remains.
    /// </summary>
    let flight (policy: FlightPolicy) (n: int) =
        let graph = new Graph (GraphOptions.Default.WithFlightPolicy policy)
        let trigger = Signal (graph, 0)
        let pending = ResizeArray<TaskCompletionSource<int>>()

        let memo =
            new AsyncMemo<int> (
                graph,
                fun _ _ ->
                    trigger.Value |> ignore
                    let source = TaskCompletionSource<int>()
                    pending.Add source
                    source.Task
            )

        let settle (value: int) =
            while pending.Count > 0 do
                let round = pending.ToArray ()
                pending.Clear ()

                for source in round do
                    source.SetResult value

                memo.TryValue |> ignore

        memo.TryValue |> ignore
        settle 0
        let next = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for _ in 1..2 do
                            next.Value <- next.Value + 1
                            trigger.Value <- next.Value
                            memo.TryValue |> ignore

                        settle next.Value
            Teardown =
                fun () ->
                    memo.Dispose ()
                    (graph :> IDisposable).Dispose()
        }

    /// <summary>
    /// A memo read by one memo. The first throws a fresh exception on every run when <c>fails</c>, and returns its input
    /// otherwise. An operation writes the trigger the first memo reads, then reads the reader and the node its failure
    /// originated in.
    /// </summary>
    let recompute (fails: bool) (n: int) =
        let graph = Workloads.newGraph ()
        let trigger = Signal (graph, 0)

        let first =
            Memo (
                graph,
                fun _ ->
                    if fails && trigger.Value >= 0 then
                        raise (InvalidOperationException "failing")

                    trigger.Value
            )

        let reader = Memo (graph, (fun _ -> first.Value + 1))
        reader.TryValue |> ignore
        let next = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- next.Value + 1
                        trigger.Value <- next.Value

                        match reader.TryValue with
                        | Ready v -> sink <- v
                        | _ -> sink <- if isNull (box reader.ErrorOrigin) then 0 else 1
            Teardown = fun () -> (graph :> IDisposable).Dispose()
        }

    /// <summary>
    /// <c>RowCount</c> editables, each seeded from a source of its own and read by one effect. An operation writes every
    /// <c>UpdateStride</c>th row: an edit to the editable when <c>local</c>, a write to its source otherwise.
    /// </summary>
    let editable (local: bool) (n: int) =
        let graph = Workloads.newGraph ()

        let owner, rows =
            graph.CreateRoot (fun owner ->
                use _ = graph.Activate ()

                let rows =
                    Array.init RowCount (fun i ->
                        let source = createSignal i
                        let value = createEditable (fun _ -> source.Value)
                        createEffect (fun () -> sink <- value.Value)
                        source, value)

                owner, rows)

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for i in 0..UpdateStride .. RowCount - 1 do
                            let source, value = rows[i]

                            if local then
                                value.Value <- value.Peek + 1
                            else
                                source.Value <- source.Peek + 1
            Teardown =
                fun () ->
                    owner.Dispose ()
                    (graph :> IDisposable).Dispose()
        }

    /// <summary>
    /// An <c>int[]</c> model of <c>ModelFields</c> fields, each read by one effect. An operation changes one field:
    /// through <c>Mvu.Dispatch</c> and one <c>Select</c> per field when <c>mvu</c>, as a write to the field's own signal
    /// otherwise.
    /// </summary>
    let fieldWrite (mvu: bool) (n: int) =
        let graph = Workloads.newGraph ()

        let owner, write =
            graph.CreateRoot (fun owner ->
                use _ = graph.Activate ()

                if mvu then
                    let app =
                        Mvu.create (Array.zeroCreate<int> ModelFields) (fun (field: int) model ->
                            let copy = Array.copy model
                            copy[field] <- copy[field] + 1
                            copy)

                    for i in 0 .. ModelFields - 1 do
                        let selected = app.Select (fun model -> model[i])
                        createEffect (fun () -> sink <- selected.Value)

                    owner, app.Dispatch
                else
                    let fields = Array.init ModelFields (fun _ -> createSignal 0)

                    for field in fields do
                        createEffect (fun () -> sink <- field.Value)

                    owner, (fun (field: int) -> fields[field].Value <- fields[field].Peek + 1))

        {
            Run =
                fun () ->
                    for op in 1..n do
                        write (op % ModelFields)
            Teardown =
                fun () ->
                    owner.Dispose ()
                    (graph :> IDisposable).Dispose()
        }

module private Adaptive =

    /// <summary>
    /// A root is the list of callback subscriptions. Disposing them stops the
    /// rows reacting.
    /// </summary>
    let root () =
        let shared = cval 0
        let subscriptions = ResizeArray<IDisposable> RowCount

        let rowSources =
            Array.init RowCount (fun i ->
                let row = cval i
                let value = AVal.map2 (+) shared row
                subscriptions.Add (value.AddCallback (fun v -> sink <- v))
                row)

        subscriptions, rowSources

    let create (n: int) =
        let roots = ResizeArray<ResizeArray<IDisposable>> n

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        roots.Add (fst (root ()))
            Teardown =
                fun () ->
                    for subscriptions in roots do
                        disposeAll subscriptions
        }

    let update (n: int) =
        let subscriptions, rowSources = root ()
        let values = Array.init RowCount id

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for i in 0..UpdateStride .. RowCount - 1 do
                            values[i] <- values[i] + 1
                            let v = values[i]
                            transact (fun () -> rowSources[i].Value <- v)
            Teardown = fun () -> disposeAll subscriptions
        }

    let chain (n: int) =
        let source = cval 0
        let mutable tail = AVal.map (fun v -> v + 1) source

        for _ in 2..ChainDepth do
            tail <- AVal.map (fun v -> v + 1) tail

        let tail = tail
        sink <- AVal.force tail
        let next = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- next.Value + 1
                        let v = next.Value
                        transact (fun () -> source.Value <- v)
                        sink <- AVal.force tail
            Teardown = noTeardown
        }

    let cutoff (n: int) =
        let source = cval 1
        let subscription = source.AddCallback (fun v -> sink <- v)

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        transact (fun () -> source.Value <- 1)
            Teardown = fun () -> subscription.Dispose ()
        }

    let dispose (n: int) =
        let roots = Array.init n (fun _ -> fst (root ()))

        {
            Run =
                fun () ->
                    for subscriptions in roots do
                        disposeAll subscriptions
            Teardown = noTeardown
        }

    /// <summary>
    /// The <c>Workloads.selection</c> shape without a selector: every row maps the selected index and its own
    /// source, read by a callback.
    /// </summary>
    let selectionCompare (n: int) =
        let selected = cval 0

        let subscriptions =
            Array.init Workloads.Rows (fun i ->
                let row = cval i
                let value = AVal.map2 (fun s r -> if s = i then r else 0) selected row
                value.AddCallback (fun v -> sink <- v))

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        let next = (selected.Value + 37) % Workloads.Rows
                        transact (fun () -> selected.Value <- next)
            Teardown =
                fun () ->
                    for subscription in subscriptions do
                        subscription.Dispose ()
        }

    /// <summary>The <c>Workloads.batchWrites</c> shape: every write of an operation inside one <c>transact</c>.</summary>
    let batchWrites (n: int) =
        let sources = Array.init Workloads.BatchWidth (fun i -> cval i)

        let total =
            AVal.custom (fun token ->
                let mutable sum = 0

                for source in sources do
                    sum <- sum + source.GetValue token

                sum)

        let subscription = total.AddCallback (fun v -> sink <- v)

        let write () =
            for source in sources do
                source.Value <- source.Value + 1

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        transact write
            Teardown = fun () -> subscription.Dispose ()
        }

    /// <summary>The <c>Ranvier.derivedCutoff</c> shape: an <c>AVal.map</c> deriving the value, read by a callback.</summary>
    let derivedCutoff (n: int) =
        let source = cval 1
        let derived = AVal.map (fun v -> v >>> 30) source
        let subscription = derived.AddCallback (fun v -> sink <- v)
        let next = ref 1

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- next.Value + 1
                        let v = next.Value
                        transact (fun () -> source.Value <- v)
            Teardown = fun () -> subscription.Dispose ()
        }

    /// <summary>The <c>Ranvier.labelled</c> shape: an <c>AVal.map</c> per row, read by a callback that formats it.</summary>
    let private labelled (derive: int -> int) (n: int) =
        let rows = Array.init RowCount (fun i -> cval i)

        let subscriptions =
            rows
            |> Array.map (fun row -> (AVal.map derive row).AddCallback(fun v -> label <- String.Concat ("row ", string v)))

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for i in 0..UpdateStride .. RowCount - 1 do
                            let row = rows[i]
                            let v = row.Value + 1
                            transact (fun () -> row.Value <- v)
            Teardown =
                fun () ->
                    for subscription in subscriptions do
                        subscription.Dispose ()
        }

    let status = labelled (fun v -> v / 10)

    let value = labelled id

    /// <summary>The <c>Ranvier.form</c> shape: validity is an <c>AVal.custom</c> over the fields, read by a callback.</summary>
    let form (n: int) =
        let forms = RowCount / FormFields

        let fieldSources =
            Array.init forms (fun f -> Array.init FormFields (fun i -> cval ((f + i) % 50)))

        let subscriptions =
            fieldSources
            |> Array.map (fun fields ->
                let valid =
                    AVal.custom (fun token ->
                        let mutable sum = 0
                        let mutable filled = true

                        for field in fields do
                            let v = field.GetValue token
                            sum <- sum + v
                            filled <- filled && v > 0

                        filled && sum < 300)

                valid.AddCallback (fun v -> sink <- if v then 1 else 0))

        let next = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- (next.Value + 1) % FormFields

                        for fields in fieldSources do
                            let field = fields[next.Value]
                            let v = (field.Value + 1) % 50
                            transact (fun () -> field.Value <- v)
            Teardown =
                fun () ->
                    for subscription in subscriptions do
                        subscription.Dispose ()
        }

    /// <summary>The <c>Workloads.wideDiamond</c> shape: <c>AVal.map</c> branches joined by an <c>AVal.custom</c>.</summary>
    let wideDiamond (n: int) =
        let source = cval 0

        let branches =
            Array.init Workloads.DiamondWidth (fun j -> AVal.map (fun v -> v + j) source)

        let sum =
            AVal.custom (fun token ->
                let mutable total = 0

                for branch in branches do
                    total <- total + branch.GetValue token

                total)

        let subscription = sum.AddCallback (fun v -> sink <- v)

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        let v = source.Value + 1
                        transact (fun () -> source.Value <- v)
            Teardown = fun () -> subscription.Dispose ()
        }

    /// <summary>The <c>Workloads.dynamicBranches</c> shape: an <c>AVal.bind</c> per reader selects its source.</summary>
    let dynamicBranches (n: int) =
        let condition = cval true
        let a = Array.init Workloads.DynamicReaders (fun i -> cval i)
        let b = Array.init Workloads.DynamicReaders (fun i -> cval i)

        let subscriptions =
            Array.init Workloads.DynamicReaders (fun i ->
                let chosen =
                    condition
                    |> AVal.bind (fun c -> if c then a[i] :> aval<int> else b[i] :> aval<int>)

                chosen.AddCallback (fun v -> sink <- v))

        {
            Run =
                fun () ->
                    for op in 1..n do
                        if op % 2 = 1 then
                            let c = not condition.Value
                            transact (fun () -> condition.Value <- c)
                        else
                            for source in (if condition.Value then a else b) do
                                let v = source.Value + 1
                                transact (fun () -> source.Value <- v)
            Teardown =
                fun () ->
                    for subscription in subscriptions do
                        subscription.Dispose ()
        }

    /// <summary>
    /// The <c>Workloads.keyedPass</c> shape with fresh items: a <c>cmap</c> from Id to value, one callback per key on
    /// <c>AMap.tryFind</c> and one on <c>AMap.count</c>. An operation writes the whole map.
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

        let map =
            cmap (
                initial
                |> Array.map (fun item -> item.Id, item.Value)
            )

        let subscriptions =
            [|
                yield (AMap.count map).AddCallback(fun v -> sink <- v)

                for key in 0 .. Workloads.Rows do
                    yield
                        (AMap.tryFind key map)
                            .AddCallback(fun v ->
                                match v with
                                | Some value -> sink <- value
                                | None -> ())
            |]

        {
            Run =
                fun () ->
                    for write in writes do
                        let next =
                            write
                            |> Array.map (fun item -> item.Id, item.Value)
                            |> HashMap.ofArray

                        transact (fun () -> map.Value <- next)
            Teardown =
                fun () ->
                    for subscription in subscriptions do
                        subscription.Dispose ()
        }

module private R3 =

    open R3

    let private subscribe (source: Observable<int>) =
        ObservableSubscribeExtensions.Subscribe (source, (fun v -> sink <- v))

    /// <summary>
    /// A root is the list of subscriptions. Disposing them stops the rows
    /// reacting.
    /// </summary>
    let root () =
        let shared = new ReactiveProperty<int> (0)
        let subscriptions = ResizeArray<IDisposable> RowCount

        let rowSources =
            Array.init RowCount (fun i ->
                let row = new ReactiveProperty<int> (i)

                let value =
                    Observable.CombineLatest ((shared :> Observable<int>), (row :> Observable<int>), (fun a b -> a + b))

                subscriptions.Add (subscribe value)
                row)

        subscriptions, rowSources

    let create (n: int) =
        let roots = ResizeArray<ResizeArray<IDisposable>> n

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        roots.Add (fst (root ()))
            Teardown =
                fun () ->
                    for subscriptions in roots do
                        disposeAll subscriptions
        }

    let update (n: int) =
        let subscriptions, rowSources = root ()

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for i in 0..UpdateStride .. RowCount - 1 do
                            let row = rowSources[i]
                            row.Value <- row.CurrentValue + 1
            Teardown = fun () -> disposeAll subscriptions
        }

    let chain (n: int) =
        let source = new ReactiveProperty<int> (0)

        let mutable tail =
            ObservableExtensions.Select ((source :> Observable<int>), (fun v -> v + 1))

        for _ in 2..ChainDepth do
            tail <- ObservableExtensions.Select (tail, (fun v -> v + 1))

        let latest = ref 0

        let subscription =
            ObservableSubscribeExtensions.Subscribe (tail, (fun v -> latest.Value <- v))

        let next = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- next.Value + 1
                        source.Value <- next.Value
                        sink <- latest.Value
            Teardown = fun () -> subscription.Dispose ()
        }

    let cutoff (n: int) =
        let source = new ReactiveProperty<int> (1)
        let subscription = subscribe source

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        source.Value <- 1
            Teardown = fun () -> subscription.Dispose ()
        }

    let dispose (n: int) =
        let roots = Array.init n (fun _ -> fst (root ()))

        {
            Run =
                fun () ->
                    for subscriptions in roots do
                        disposeAll subscriptions
            Teardown = noTeardown
        }

    /// <summary>The <c>Ranvier.derivedCutoff</c> shape: <c>Select</c> then <c>DistinctUntilChanged</c>, subscribed.</summary>
    let derivedCutoff (n: int) =
        let source = new ReactiveProperty<int> (1)

        let subscription =
            ObservableExtensions.DistinctUntilChanged (ObservableExtensions.Select ((source :> Observable<int>), (fun v -> v >>> 30)))
            |> subscribe

        let next = ref 1

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- next.Value + 1
                        source.Value <- next.Value
            Teardown = fun () -> subscription.Dispose ()
        }

    /// <summary>The <c>Ranvier.labelled</c> shape: per row, <c>Select</c> then <c>DistinctUntilChanged</c>, formatted.</summary>
    let private labelled (derive: int -> int) (n: int) =
        let rows = Array.init RowCount (fun i -> new ReactiveProperty<int> (i))

        let subscriptions =
            rows
            |> Array.map (fun row ->
                ObservableSubscribeExtensions.Subscribe (
                    ObservableExtensions.DistinctUntilChanged (ObservableExtensions.Select ((row :> Observable<int>), derive)),
                    (fun v -> label <- String.Concat ("row ", string v))
                ))

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for i in 0..UpdateStride .. RowCount - 1 do
                            let row = rows[i]
                            row.Value <- row.CurrentValue + 1
            Teardown =
                fun () ->
                    for subscription in subscriptions do
                        subscription.Dispose ()
        }

    let status = labelled (fun v -> v / 10)

    let value = labelled id

    /// <summary>The <c>Workloads.dynamicBranches</c> shape: per reader, <c>Select</c> of the chosen source then <c>Switch</c>.</summary>
    let dynamicBranches (n: int) =
        let condition = new ReactiveProperty<bool> (true)
        let a = Array.init Workloads.DynamicReaders (fun i -> new ReactiveProperty<int> (i))
        let b = Array.init Workloads.DynamicReaders (fun i -> new ReactiveProperty<int> (i))

        let subscriptions =
            Array.init Workloads.DynamicReaders (fun i ->
                ObservableExtensions.Switch (
                    ObservableExtensions.Select (
                        (condition :> Observable<bool>),
                        (fun c ->
                            if c then
                                a[i] :> Observable<int>
                            else
                                b[i] :> Observable<int>)
                    )
                )
                |> subscribe)

        {
            Run =
                fun () ->
                    for op in 1..n do
                        if op % 2 = 1 then
                            condition.Value <- not condition.CurrentValue
                        else
                            for source in (if condition.CurrentValue then a else b) do
                                source.Value <- source.CurrentValue + 1
            Teardown =
                fun () ->
                    for subscription in subscriptions do
                        subscription.Dispose ()
        }

    /// <summary>
    /// The <c>Workloads.streamBurst</c> shape: a <c>Subject</c> folded by <c>Scan</c>, doubled by <c>Select</c> and
    /// subscribed. Each item runs the whole chain.
    /// </summary>
    let streamBurst (burst: int) (n: int) =
        let source = new Subject<int> ()
        let seen = ref 0

        let subscription =
            ObservableSubscribeExtensions.Subscribe (
                ObservableExtensions.Select (ObservableExtensions.Scan ((source :> Observable<int>), 0, (fun acc v -> acc + v)), (fun v -> v * 2)),
                (fun v -> seen.Value <- v)
            )

        let expected = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for i in 1..burst do
                            source.OnNext i

                        expected.Value <- expected.Value + burst * (burst + 1) / 2

                    if seen.Value <> 2 * expected.Value then
                        failwith $"R3 stream-burst folded %d{seen.Value / 2}, expected %d{expected.Value}"
            Teardown = fun () -> subscription.Dispose ()
        }

    /// <summary>
    /// A <c>TimeProvider</c> on a manual clock, matching the Ranvier timed workload's clock: <c>Advance</c> moves time
    /// 100 ms and fires every due timer.
    /// </summary>
    type private ManualTime() =
        inherit TimeProvider()
        let timers = ResizeArray<Threading.TimerCallback * obj * float ref>()
        let mutable now = 0.

        member _.Advance() =
            now <- now + 100.

            for i in 0 .. timers.Count - 1 do
                let callback, state, due = timers[i]

                if due.Value <= now then
                    due.Value <- infinity
                    callback.Invoke state

        override _.GetUtcNow() =
            DateTimeOffset.UnixEpoch.AddMilliseconds now

        override _.TimestampFrequency = 1000L
        override _.GetTimestamp() = int64 now

        override _.CreateTimer(callback, state, dueTime, _period) =
            let due = ref infinity
            timers.Add (callback, state, due)

            let arm (delay: TimeSpan) =
                due.Value <-
                    if delay = Threading.Timeout.InfiniteTimeSpan then
                        infinity
                    else
                        now + delay.TotalMilliseconds

            arm dueTime

            { new Threading.ITimer with
                member _.Change(delay, _) =
                    arm delay
                    true

                member _.Dispose() =
                    due.Value <- infinity

                member _.DisposeAsync() =
                    due.Value <- infinity
                    ValueTask.CompletedTask
            }

    /// <summary>
    /// The <c>Workloads.timed</c> shape: 64 subscriptions to one source through <c>Debounce</c>, <c>ThrottleFirst</c>,
    /// <c>ThrottleLast</c> or <c>ThrottleFirstLast</c> by <c>mode</c>, on a manual clock that <c>admit</c> advances
    /// after each write.
    /// </summary>
    let timed (mode: string) (admit: bool) (n: int) =
        let clock = ManualTime ()
        let source = new ReactiveProperty<int> (0)
        let window = TimeSpan.FromMilliseconds 100.

        let timedBy (input: Observable<int>) =
            match mode with
            | "first" -> ObservableExtensions.ThrottleFirst (input, window, clock)
            | "last" -> ObservableExtensions.ThrottleLast (input, window, clock)
            | "both" -> ObservableExtensions.ThrottleFirstLast (input, window, clock)
            | _ -> ObservableExtensions.Debounce (input, window, clock)

        let subscriptions = Array.init 64 (fun _ -> timedBy source |> subscribe)

        source.Value <- 1

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        source.Value <- source.CurrentValue + 1

                        if admit then
                            clock.Advance ()
            Teardown =
                fun () ->
                    for subscription in subscriptions do
                        subscription.Dispose ()
        }

    /// <summary>
    /// The <c>Ranvier.flight</c> shape: a <c>SelectAwait</c> over a trigger, by the <c>AwaitOperation</c> named
    /// <c>operation</c>, each await a <c>TaskCompletionSource</c> the operation settles.
    /// </summary>
    let flight (operation: string) (n: int) =
        let trigger = new ReactiveProperty<int> (0)
        let pending = ResizeArray<TaskCompletionSource<int>>()
        let latest = ref 0

        let subscription =
            ObservableSubscribeExtensions.Subscribe (
                ObservableExtensions.SelectAwait (
                    (trigger :> Observable<int>),
                    Func<int, Threading.CancellationToken, ValueTask<int>>(fun _ _ ->
                        let source = TaskCompletionSource<int>()
                        pending.Add source
                        ValueTask<int> source.Task),
                    Enum.Parse<AwaitOperation> operation
                ),
                (fun v -> latest.Value <- v)
            )

        let settle (value: int) =
            while pending.Count > 0 do
                let round = pending.ToArray ()
                pending.Clear ()

                for source in round do
                    source.SetResult value

        settle 0
        let next = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for _ in 1..2 do
                            next.Value <- next.Value + 1
                            trigger.Value <- next.Value

                        settle next.Value

                    sink <- latest.Value
            Teardown = fun () -> subscription.Dispose ()
        }

/// <summary>
/// System.Reactive (Rx.NET) implementations. A source is a <c>BehaviorSubject</c>; an equal write still pushes, so a
/// reader that skips unchanged values applies <c>DistinctUntilChanged</c>.
/// </summary>
module private Rx =
    open System.Reactive.Subjects

    type private Linq = System.Reactive.Linq.Observable

    let private subscribe (source: IObservable<int>) =
        ObservableExtensions.Subscribe (source, Action<int>(fun v -> sink <- v))

    /// <summary>A root is the list of subscriptions to its rows' <c>CombineLatest</c> with the shared source.</summary>
    let root () =
        let shared = new BehaviorSubject<int> (0)
        let subscriptions = ResizeArray<IDisposable> RowCount

        let rowSources =
            Array.init RowCount (fun i ->
                let row = new BehaviorSubject<int> (i)
                subscriptions.Add (subscribe (Linq.CombineLatest (shared, row, Func<int, int, int>(fun a b -> a + b))))
                row)

        subscriptions, rowSources

    let create (n: int) =
        let roots = ResizeArray<ResizeArray<IDisposable>> n

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        roots.Add (fst (root ()))
            Teardown =
                fun () ->
                    for subscriptions in roots do
                        disposeAll subscriptions
        }

    let update (n: int) =
        let subscriptions, rowSources = root ()

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for i in 0..UpdateStride .. RowCount - 1 do
                            let row = rowSources[i]
                            row.OnNext (row.Value + 1)
            Teardown = fun () -> disposeAll subscriptions
        }

    let chain (n: int) =
        let source = new BehaviorSubject<int> (0)
        let mutable tail = Linq.Select (source, Func<int, int>(fun v -> v + 1))

        for _ in 2..ChainDepth do
            tail <- Linq.Select (tail, Func<int, int>(fun v -> v + 1))

        let latest = ref 0

        let subscription =
            ObservableExtensions.Subscribe (tail, Action<int>(fun v -> latest.Value <- v))

        let next = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- next.Value + 1
                        source.OnNext next.Value
                        sink <- latest.Value
            Teardown = fun () -> subscription.Dispose ()
        }

    let cutoff (n: int) =
        let source = new BehaviorSubject<int> (1)
        let subscription = subscribe (Linq.DistinctUntilChanged source)

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        source.OnNext 1
            Teardown = fun () -> subscription.Dispose ()
        }

    let dispose (n: int) =
        let roots = Array.init n (fun _ -> fst (root ()))

        {
            Run =
                fun () ->
                    for subscriptions in roots do
                        disposeAll subscriptions
            Teardown = noTeardown
        }

    /// <summary>The <c>Ranvier.derivedCutoff</c> shape: <c>Select</c> then <c>DistinctUntilChanged</c>, subscribed.</summary>
    let derivedCutoff (n: int) =
        let source = new BehaviorSubject<int> (1)

        let subscription =
            subscribe (Linq.DistinctUntilChanged (Linq.Select (source, Func<int, int>(fun v -> v >>> 30))))

        let next = ref 1

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- next.Value + 1
                        source.OnNext next.Value
            Teardown = fun () -> subscription.Dispose ()
        }

    /// <summary>
    /// The <c>Workloads.streamBurst</c> shape: a <c>Subject</c> folded by <c>Scan</c>, doubled by <c>Select</c> and
    /// subscribed. Each item runs the whole chain.
    /// </summary>
    let streamBurst (burst: int) (n: int) =
        let source = new Subject<int> ()
        let seen = ref 0

        let subscription =
            ObservableExtensions.Subscribe (
                Linq.Select (Linq.Scan (source, 0, Func<int, int, int>(fun acc v -> acc + v)), Func<int, int>(fun v -> v * 2)),
                Action<int>(fun v -> seen.Value <- v)
            )

        let expected = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for i in 1..burst do
                            source.OnNext i

                        expected.Value <- expected.Value + burst * (burst + 1) / 2

                    if seen.Value <> 2 * expected.Value then
                        failwith $"System.Reactive stream-burst folded %d{seen.Value / 2}, expected %d{expected.Value}"
            Teardown = fun () -> subscription.Dispose ()
        }

    /// <summary>
    /// The <c>Workloads.timed</c> debounce shape: 64 subscriptions to one source through <c>Throttle</c>, Rx.NET's
    /// debounce, on a <c>HistoricalScheduler</c> that <c>admit</c> advances 100 ms after each write.
    /// </summary>
    let timed (admit: bool) (n: int) =
        let scheduler = System.Reactive.Concurrency.HistoricalScheduler ()
        let source = new BehaviorSubject<int> (0)
        let window = TimeSpan.FromMilliseconds 100.

        let subscriptions =
            Array.init 64 (fun _ -> subscribe (Linq.Throttle (source, window, scheduler)))

        source.OnNext 1

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        source.OnNext (source.Value + 1)

                        if admit then
                            scheduler.AdvanceBy window
            Teardown =
                fun () ->
                    for subscription in subscriptions do
                        subscription.Dispose ()
        }

/// <summary>
/// SignalsDotnet implementations. Its effects run synchronously on the writing thread and hold weak subscriptions, so
/// every case keeps its effects referenced until teardown disposes them.
/// </summary>
module private Signals =
    open SignalsDotnet

    let private effect (body: unit -> unit) =
        new Effect (Action body)

    let private computed (body: unit -> 'T) =
        Signal.Computed (Func<'T> body)

    let private disposeEffects (effects: Effect seq) =
        for e in effects do
            e.Dispose ()

    /// <summary>A root is the list of its rows' effects, each reading the row and the shared source.</summary>
    let root () =
        let shared = Signal<int>(0)
        let effects = ResizeArray<Effect> RowCount

        let rowSources =
            Array.init RowCount (fun i ->
                let row = Signal<int>(i)
                effects.Add (effect (fun () -> sink <- shared.Value + row.Value))
                row)

        effects, rowSources

    let create (n: int) =
        let roots = ResizeArray<ResizeArray<Effect>> n

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        roots.Add (fst (root ()))
            Teardown =
                fun () ->
                    for effects in roots do
                        disposeEffects effects
        }

    let update (n: int) =
        let effects, rowSources = root ()

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for i in 0..UpdateStride .. RowCount - 1 do
                            let row = rowSources[i]
                            row.Value <- row.UntrackedValue + 1
            Teardown = fun () -> disposeEffects effects
        }

    let chain (n: int) =
        let source = Signal<int>(0)
        let mutable tail = computed (fun () -> source.Value + 1)

        for _ in 2..ChainDepth do
            let previous = tail
            tail <- computed (fun () -> previous.Value + 1)

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
        let source = Signal<int>(1)
        let reader = effect (fun () -> sink <- source.Value)

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        source.Value <- 1
            Teardown = fun () -> reader.Dispose ()
        }

    let dispose (n: int) =
        let roots = Array.init n (fun _ -> fst (root ()))

        {
            Run =
                fun () ->
                    for effects in roots do
                        disposeEffects effects
            Teardown = noTeardown
        }

    /// <summary>
    /// The <c>Ranvier.derivedCutoff</c> shape. <c>split</c> derives the value in a <c>Signal.Computed</c> read by the
    /// effect; otherwise the effect derives it in its body.
    /// </summary>
    let derivedCutoff (split: bool) (n: int) =
        let source = Signal<int>(1)

        let reader =
            if split then
                let derived = computed (fun () -> source.Value >>> 30)
                effect (fun () -> sink <- derived.Value)
            else
                effect (fun () -> sink <- source.Value >>> 30)

        let next = ref 1

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- next.Value + 1
                        source.Value <- next.Value
            Teardown = fun () -> reader.Dispose ()
        }

    /// <summary>
    /// The <c>Ranvier.labelled</c> shape. <c>split</c> derives the row's value in a <c>Signal.Computed</c> read by the
    /// formatting effect.
    /// </summary>
    let private labelled (derive: int -> int) (split: bool) (n: int) =
        let rows = Array.init RowCount (fun i -> Signal<int>(i))

        let effects =
            rows
            |> Array.map (fun row ->
                if split then
                    let derived = computed (fun () -> derive row.Value)
                    effect (fun () -> label <- String.Concat ("row ", string derived.Value))
                else
                    effect (fun () -> label <- String.Concat ("row ", string (derive row.Value))))

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        for i in 0..UpdateStride .. RowCount - 1 do
                            let row = rows[i]
                            row.Value <- row.UntrackedValue + 1
            Teardown = fun () -> disposeEffects effects
        }

    let status = labelled (fun v -> v / 10)

    let value = labelled id

    /// <summary>
    /// The <c>Ranvier.form</c> shape. With <c>split</c>, validity is a <c>Signal.Computed</c>; otherwise an effect
    /// writes it to a signal. One downstream effect reads validity.
    /// </summary>
    let form (split: bool) (n: int) =
        let forms = RowCount / FormFields
        let effects = ResizeArray<Effect>()

        let fieldSources =
            Array.init forms (fun f ->
                let fields = Array.init FormFields (fun i -> Signal<int>((f + i) % 50))

                let validate () =
                    let mutable sum = 0
                    let mutable filled = true

                    for field in fields do
                        let v = field.Value
                        sum <- sum + v
                        filled <- filled && v > 0

                    filled && sum < 300

                if split then
                    let valid = computed validate
                    effects.Add (effect (fun () -> sink <- if valid.Value then 1 else 0))
                else
                    let valid = Signal<bool>(false)
                    effects.Add (effect (fun () -> valid.Value <- validate ()))
                    effects.Add (effect (fun () -> sink <- if valid.Value then 1 else 0))

                fields)

        let next = ref 0

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        next.Value <- (next.Value + 1) % FormFields

                        for fields in fieldSources do
                            let field = fields[next.Value]
                            field.Value <- (field.UntrackedValue + 1) % 50
            Teardown = fun () -> disposeEffects effects
        }

    /// <summary>The <c>Workloads.wideDiamond</c> shape with <c>Signal.Computed</c> branches and join.</summary>
    let wideDiamond (n: int) =
        let source = Signal<int>(0)

        let branches =
            Array.init Workloads.DiamondWidth (fun j -> computed (fun () -> source.Value + j))

        let sum =
            computed (fun () ->
                let mutable total = 0

                for branch in branches do
                    total <- total + branch.Value

                total)

        let reader = effect (fun () -> sink <- sum.Value)

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        source.Value <- source.UntrackedValue + 1
            Teardown = fun () -> reader.Dispose ()
        }

    /// <summary>The <c>Workloads.dynamicBranches</c> shape with one effect per reader.</summary>
    let dynamicBranches (n: int) =
        let condition = Signal<bool>(true)
        let a = Array.init Workloads.DynamicReaders (fun i -> Signal<int>(i))
        let b = Array.init Workloads.DynamicReaders (fun i -> Signal<int>(i))

        let effects =
            Array.init Workloads.DynamicReaders (fun i -> effect (fun () -> sink <- if condition.Value then a[i].Value else b[i].Value))

        {
            Run =
                fun () ->
                    for op in 1..n do
                        if op % 2 = 1 then
                            condition.Value <- not condition.UntrackedValue
                        else
                            for source in (if condition.UntrackedValue then a else b) do
                                source.Value <- source.UntrackedValue + 1
            Teardown = fun () -> disposeEffects effects
        }

    /// <summary>The <c>Workloads.selection</c> shape without a selector: every row's effect compares the index.</summary>
    let selectionCompare (n: int) =
        let rows = Array.init Workloads.Rows (fun i -> Signal<int>(i))
        let selected = Signal<int>(0)

        let effects =
            Array.init Workloads.Rows (fun i -> effect (fun () -> sink <- if selected.Value = i then rows[i].Value else 0))

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        selected.Value <- (selected.UntrackedValue + 37) % Workloads.Rows
            Teardown = fun () -> disposeEffects effects
        }

    /// <summary>The <c>Workloads.batchWrites</c> shape: every write of an operation inside one <c>Effect.AtomicOperation</c>.</summary>
    let batchWrites (n: int) =
        let sources = Array.init Workloads.BatchWidth (fun i -> Signal<int>(i))

        let reader =
            effect (fun () ->
                let mutable total = 0

                for source in sources do
                    total <- total + source.Value

                sink <- total)

        let write =
            Action (fun () ->
                for source in sources do
                    source.Value <- source.UntrackedValue + 1)

        {
            Run =
                fun () ->
                    for _ in 1..n do
                        Effect.AtomicOperation write
            Teardown = fun () -> reader.Dispose ()
        }

/// <summary>
/// DynamicData implementations: a <c>SourceCache</c> keyed by Id, written with <c>EditDiff</c>, which emits only the
/// adds, updates and removes between the cache and the written list.
/// </summary>
module private Dynamic =
    open DynamicData

    /// <summary>
    /// The <c>Workloads.keyedPass</c> shape with fresh items: one <c>WatchValue</c> subscription per key and one on
    /// <c>CountChanged</c>. An operation writes the whole list.
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

        let cache =
            new SourceCache<Workloads.KeyedItem, int> (Func<Workloads.KeyedItem, int>(fun item -> item.Id))

        let same =
            Func<Workloads.KeyedItem, Workloads.KeyedItem, bool>(fun a b -> a.Value = b.Value)

        cache.EditDiff (initial, same)

        let subscriptions =
            [|
                yield ObservableExtensions.Subscribe (cache.CountChanged, Action<int>(fun v -> sink <- v))

                for key in 0 .. Workloads.Rows do
                    yield ObservableExtensions.Subscribe (cache.WatchValue key, Action<Workloads.KeyedItem>(fun item -> sink <- item.Value))
            |]

        {
            Run =
                fun () ->
                    for write in writes do
                        cache.EditDiff (write, same)
            Teardown =
                fun () ->
                    for subscription in subscriptions do
                        subscription.Dispose ()

                    cache.Dispose ()
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
/// Every case, in report order. <c>scale</c> multiplies each scenario's N.
/// </summary>
let all (scale: int) : Case list =
    [
        for mode in [ "debounce"; "first"; "last"; "both" ] do
            yield!
                scenario
                    ("timed-capture-" + mode)
                    "capture a changed input in 64 timed nodes"
                    (1000 * scale)
                    [
                        "Ranvier", workload (Workloads.timed mode false)
                        "R3", R3.timed mode false
                        if mode = "debounce" then
                            "System.Reactive (Throttle)", Rx.timed false
                    ]

            yield!
                scenario
                    ("timed-admit-" + mode)
                    "capture and admit in 64 timed nodes"
                    (1000 * scale)
                    [
                        "Ranvier", workload (Workloads.timed mode true)
                        "R3", R3.timed mode true
                        if mode = "debounce" then
                            "System.Reactive (Throttle)", Rx.timed true
                    ]
        yield!
            scenario
                "create"
                $"one root of %d{RowCount} rows"
                (8 * scale)
                [
                    "Ranvier", Ranvier.create false
                    "FSharp.Data.Adaptive", Adaptive.create
                    "R3", R3.create
                    "System.Reactive", Rx.create
                    "SignalsDotnet", Signals.create
                ]
        yield!
            scenario
                "update"
                $"write every %d{UpdateStride}th of %d{RowCount} rows"
                (50 * scale)
                [
                    "Ranvier", Ranvier.update false
                    "FSharp.Data.Adaptive", Adaptive.update
                    "R3", R3.update
                    "System.Reactive", Rx.update
                    "SignalsDotnet", Signals.update
                ]
        yield!
            scenario
                "chain"
                $"write the source of %d{ChainDepth} memos, read the tail"
                (5000 * scale)
                [
                    "Ranvier", Ranvier.chain
                    "FSharp.Data.Adaptive", Adaptive.chain
                    "R3", R3.chain
                    "System.Reactive", Rx.chain
                    "SignalsDotnet", Signals.chain
                ]
        yield!
            scenario
                "cutoff"
                "write an equal value to an observed source"
                (50000 * scale)
                [
                    "Ranvier", Ranvier.cutoff
                    "FSharp.Data.Adaptive", Adaptive.cutoff
                    "R3", R3.cutoff
                    "System.Reactive", Rx.cutoff
                    "SignalsDotnet", Signals.cutoff
                ]
        yield!
            scenario
                "dispose"
                $"dispose one root of %d{RowCount} rows"
                (8 * scale)
                [
                    "Ranvier", Ranvier.dispose false
                    "FSharp.Data.Adaptive", Adaptive.dispose
                    "R3", R3.dispose
                    "System.Reactive", Rx.dispose
                    "SignalsDotnet", Signals.dispose
                ]
        yield! scenario "create-on" $"one root of %d{RowCount} createEffectOn rows" (8 * scale) [ "Ranvier", Ranvier.create true ]
        yield!
            scenario "update-on" $"write every %d{UpdateStride}th of %d{RowCount} createEffectOn rows" (50 * scale) [ "Ranvier", Ranvier.update true ]
        yield! scenario "dispose-on" $"dispose one root of %d{RowCount} createEffectOn rows" (8 * scale) [ "Ranvier", Ranvier.dispose true ]
        yield! scenario "chain-map" "write the source of 4 mapped memos, read the tail" (5000 * scale) [ "Ranvier", Ranvier.chainMap ]
        yield! scenario "chain-on" "write the source of 4 createMemoOn memos, read the tail" (5000 * scale) [ "Ranvier", Ranvier.chainMemoOn ]
        yield!
            scenario
                "derive-effect"
                "write a new value whose derived value is unchanged, Effect"
                (50000 * scale)
                [
                    "Ranvier", Ranvier.derivedCutoff false
                    "SignalsDotnet", Signals.derivedCutoff false
                ]
        yield!
            scenario
                "derive-on"
                "write a new value whose derived value is unchanged, createEffectOn"
                (50000 * scale)
                [
                    "Ranvier", Ranvier.derivedCutoff true
                    "FSharp.Data.Adaptive", Adaptive.derivedCutoff
                    "R3", R3.derivedCutoff
                    "System.Reactive", Rx.derivedCutoff
                    "SignalsDotnet (Computed + Effect)", Signals.derivedCutoff true
                ]
        yield!
            scenario
                "work-status"
                "write every 10th of 1000 rows; the act formats a label, 1 write in 10 changes it"
                (50 * scale)
                [
                    "Ranvier (createEffect)", Ranvier.status false
                    "Ranvier (createEffectOn)", Ranvier.status true
                    "FSharp.Data.Adaptive (AVal.map)", Adaptive.status
                    "R3 (DistinctUntilChanged)", R3.status
                    "SignalsDotnet (Effect)", Signals.status false
                    "SignalsDotnet (Computed + Effect)", Signals.status true
                ]
        yield!
            scenario
                "work-value"
                "write every 10th of 1000 rows; the act formats a label, every write changes it"
                (50 * scale)
                [
                    "Ranvier (createEffect)", Ranvier.value false
                    "Ranvier (createEffectOn)", Ranvier.value true
                    "FSharp.Data.Adaptive (AVal.map)", Adaptive.value
                    "R3 (DistinctUntilChanged)", R3.value
                    "SignalsDotnet (Effect)", Signals.value false
                    "SignalsDotnet (Computed + Effect)", Signals.value true
                ]
        yield!
            scenario
                "work-form"
                "write one field of each of 125 8-field forms; validity feeds a signal and an effect"
                (50 * scale)
                [
                    "Ranvier (createEffect)", Ranvier.form false
                    "Ranvier (createEffectOn)", Ranvier.form true
                    "FSharp.Data.Adaptive (AVal.custom)", Adaptive.form
                    "SignalsDotnet (Effect)", Signals.form false
                    "SignalsDotnet (Computed)", Signals.form true
                ]
        yield! scenario "project-edit" $"change one row of a %d{RowCount}-row projection" (50 * scale) [ "Ranvier", Ranvier.projectEdit ]
        yield! scenario "project-reorder" $"reverse a %d{RowCount}-row projection" (50 * scale) [ "Ranvier", Ranvier.projectReorder ]
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
                        "Ranvier (memo-free rows)", workload (OnceWorkloads.keyedOncePass edit)
                        "FSharp.Data.Adaptive (cmap)", Adaptive.keyed edit
                        "DynamicData (EditDiff)", Dynamic.keyed edit
                    ]
        yield!
            scenario
                "project-chain"
                $"change one row of a %d{RowCount}-row filter, sortBy, map chain"
                (50 * scale)
                [ "Ranvier", Ranvier.projectChain ]
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
                    "FSharp.Data.Adaptive", Adaptive.wideDiamond
                    "SignalsDotnet", Signals.wideDiamond
                ]
        yield!
            scenario
                "shape-dynamic"
                $"alternate a branch flip and a write of every active source of %d{Workloads.DynamicReaders} effects"
                (500 * scale)
                [
                    "Ranvier", workload Workloads.dynamicBranches
                    "FSharp.Data.Adaptive (AVal.bind)", Adaptive.dynamicBranches
                    "R3 (Switch)", R3.dynamicBranches
                    "SignalsDotnet", Signals.dynamicBranches
                ]
        yield!
            scenario
                "select"
                $"move the selection over %d{Workloads.Rows} rows, each read by one effect"
                (50 * scale)
                [
                    "Ranvier", workload (Workloads.selection true)
                    "Ranvier (compare)", workload (Workloads.selection false)
                    "FSharp.Data.Adaptive (compare)", Adaptive.selectionCompare
                    "SignalsDotnet (compare)", Signals.selectionCompare
                ]
        yield!
            scenario
                "batch"
                $"write %d{Workloads.BatchWidth} sources read by one effect inside one batch"
                (5000 * scale)
                [
                    "Ranvier", workload Workloads.batchWrites
                    "FSharp.Data.Adaptive", Adaptive.batchWrites
                    "SignalsDotnet", Signals.batchWrites
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
                        "R3 (Subject, Scan)", R3.streamBurst burst
                        "System.Reactive (Subject, Scan)", Rx.streamBurst burst
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
        yield!
            scenario
                "chain-affinity"
                $"write the source of %d{ChainDepth} memos, read the tail, per thread affinity"
                (5000 * scale)
                [
                    "Ranvier (Guarded)", Ranvier.chainAffinity Guarded
                    "Ranvier (Unchecked)", Ranvier.chainAffinity ThreadAffinity.Unchecked
                    "Ranvier (Serialised)", Ranvier.chainAffinity Serialised
                ]
        yield!
            scenario
                "project-churn"
                $"replace the last key of a %d{RowCount}-row projection"
                (50 * scale)
                [
                    "Ranvier (no reader)", Ranvier.projectChurn 0
                    "Ranvier (key reader)", Ranvier.projectChurn 1
                ]
        yield!
            scenario
                "flight"
                "write an async memo's trigger twice, then settle every flight the writes started"
                (500 * scale)
                [
                    "Ranvier (CancelPrevious)", Ranvier.flight CancelPrevious
                    "Ranvier (KeepLatest)", Ranvier.flight KeepLatest
                    "Ranvier (Queue)", Ranvier.flight FlightPolicy.Queue
                    "Ranvier (FinishCurrent)", Ranvier.flight FinishCurrent
                    "Ranvier (Drop)", Ranvier.flight Drop
                    "Ranvier (DropWhilePending)", Ranvier.flight DropWhilePending
                    "R3 (Switch)", R3.flight "Switch"
                    "R3 (ThrottleFirstLast)", R3.flight "ThrottleFirstLast"
                    "R3 (Sequential)", R3.flight "Sequential"
                    "R3 (Drop)", R3.flight "Drop"
                ]
        yield!
            scenario
                "fail-recompute"
                "re-run a memo and its one reader, then read the reader's failure origin"
                (500 * scale)
                [
                    "Ranvier (succeeding)", Ranvier.recompute false
                    "Ranvier (failing)", Ranvier.recompute true
                ]
        yield! scenario "editable-edit" $"edit every %d{UpdateStride}th of %d{RowCount} editables" (50 * scale) [ "Ranvier", Ranvier.editable true ]
        yield!
            scenario
                "editable-upstream"
                $"write the seed source of every %d{UpdateStride}th of %d{RowCount} editables"
                (50 * scale)
                [ "Ranvier", Ranvier.editable false ]
        yield!
            scenario
                "mvu-dispatch"
                $"change one field of a %d{ModelFields}-field model, one reader per field"
                (500 * scale)
                [
                    "Ranvier (signal per field)", Ranvier.fieldWrite false
                    "Ranvier (Mvu)", Ranvier.fieldWrite true
                ]
        yield!
            scenario
                Control.Scenario
                $"refill a %d{Control.Size}-entry closure table, sum it, and sort a copy of its array"
                (50 * scale)
                [ "Control", workload Control.prepare ]
    ]
