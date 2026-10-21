/// <summary>A site's lookup from an instance to the entries of its mounted arms, per target.</summary>
module internal Ranvier.View.Arms.Cells

open global.Ranvier
open Ranvier.View.Foundation.Representation
#if FABLE_COMPILER
open Fable.Core

// An entry is { g: graph, c: the arm's current props, l: last values, n: the next live entry, w: its withdrawal }. An
// instance's slots hold the first live entry of target t at key t, the others linked through n in mount order, and the
// target's last withdrawn entry at key ~t. A withdrawn entry keeps its values only, and the next arm of its target on the
// instance reuses it.

/// <summary>The site's WeakMap from an instance to its slots.</summary>
type Cells = obj

[<Emit("new WeakMap()")>]
let create () : Cells = jsNative

/// <summary>The value of an optional argument, or undefined.</summary>
[<Emit("$0")>]
let erased (value: obj option) : obj = jsNative

/// <summary>The ambient graph, or undefined outside one.</summary>
[<Emit("$0")>]
let tag (graph: Graph voption) : obj = jsNative

[<Emit("($0.get($1) || $0.set($1, {}).get($1))")>]
let slotsOf (cells: Cells) (instance: obj) : obj = jsNative

[<Emit("$0.get($1)")>]
let tryFind (cells: Cells) (instance: obj) : obj = jsNative

[<Emit("{ g: $0, c: $1 }")>]
let entry (graph: obj) (current: obj) : obj = jsNative

/// <summary>Binds a withdrawn entry to the graph and the arm's props, with no values read and no next entry.</summary>
[<Emit("($0.g = $1, $0.c = $2, $0.l = $0.n = null)")>]
let rebind (entry: obj) (graph: obj) (current: obj) : unit = jsNative

/// <summary>The first live entry of the target, or null.</summary>
[<Emit("($0 && $0[$1])")>]
let live (slots: obj) (target: obj) : obj = jsNative

/// <summary>The first live entry of the target in existing slots, or null.</summary>
[<Emit("$0[$1]")>]
let liveIn (slots: obj) (target: obj) : obj = jsNative

[<Emit("$0[$1] = $2")>]
let setLive (slots: obj) (target: obj) (entry: obj) : unit = jsNative

[<Emit("($0 && $0[~$1])")>]
let withdrawn (slots: obj) (target: obj) : obj = jsNative

/// <summary>The target's withdrawn entry in existing slots, or null.</summary>
[<Emit("$0[~$1]")>]
let withdrawnIn (slots: obj) (target: obj) : obj = jsNative

[<Emit("$0[~$1] = null")>]
let clearWithdrawn (slots: obj) (target: obj) : unit = jsNative

[<Emit("$0.n")>]
let next (entry: obj) : obj = jsNative

[<Emit("$0.n = $1")>]
let setNext (entry: obj) (next: obj) : unit = jsNative

/// <summary>The entry's withdrawal, created once per entry.</summary>
[<Emit("($0.w || ($0.w = $1))")>]
let withdrawalOf (entry: obj) (withdraw: unit -> unit) : unit -> unit = jsNative

/// <summary>Keeps the entry as the target's withdrawn entry when it holds values, and releases the arm's props.</summary>
[<Emit("($2.l && ($0[~$1] = $2), $2.c = null)")>]
let retire (slots: obj) (target: obj) (entry: obj) : unit = jsNative

[<Emit("$0 == null")>]
let absent (value: obj) : bool = jsNative

[<Emit("$0 === $1")>]
let same (a: obj) (b: obj) : bool = jsNative

[<Emit("null")>]
let unread () : obj array = jsNative

[<Emit("($0 == null || $1.g == null || $1.g === $0)")>]
let servesUnder (graph: obj) (entry: obj) : bool = jsNative

[<Emit("$0.l = $0.c()[1]")>]
let read (entry: obj) : obj array = jsNative

[<Emit("$0.l")>]
let last (entry: obj) : obj array = jsNative

/// <summary>
/// Registers the arm's current props (the instance and its bound values) for the instance and target, and withdraws them
/// on the arm's cleanup.
/// </summary>
let inline enter (cells: Cells) (setup: Setup) (instance: obj) (target: obj) (current: unit -> obj * obj array) : unit =
    let slots = slotsOf cells instance
    let graph = tag Graph.TryCurrent
    let held = withdrawnIn slots target

    let registered =
        if absent held then
            entry graph current
        else
            clearWithdrawn slots target
            rebind held graph current
            held

    let mutable tail = liveIn slots target

    if absent tail then
        setLive slots target registered
    else
        while not (absent (next tail)) do
            tail <- next tail

        setNext tail registered

    setup.OnCleanup (
        withdrawalOf registered (fun () ->
            let mutable previous = liveIn slots target

            if same previous registered then
                setLive slots target (next registered)
            else
                while not (same (next previous) registered) do
                    previous <- next previous

                setNext previous (next registered)

            retire slots target registered)
    )

/// <summary>
/// The bound values of the reader's own arm: the pattern variables, then the matched value. Reads every live entry
/// serving the ambient graph and returns the first; with none, the values held by the target's last withdrawn entry
/// when it serves the ambient graph. A withdrawal under another graph replaces the target's withdrawn entry; a held read
/// under the first graph then raises AM006.
/// </summary>
let inline values (cells: Cells) (instance: obj) (target: obj) : obj array =
    let slots = tryFind cells instance
    let graph = tag Graph.TryCurrent
    let serves = fun (entry: obj) -> servesUnder graph entry
    let mutable candidate = live slots target
    let mutable first = unread ()

    while not (absent candidate) do
        if serves candidate then
            let values = read candidate

            if absent first then
                first <- values

        candidate <- next candidate

    if not (absent first) then
        first
    else
        let held = withdrawn slots target

        if not (absent held) && serves held then
            last held
        else
            Errors.noArm ()
#else
let private ambient () : obj =
    match Graph.TryCurrent with
    | ValueSome graph -> box graph
    | ValueNone -> null

/// <summary>A mounted arm's payload reader, the graph its setup ran under, and the values of its latest read.</summary>
[<AllowNullLiteral>]
type Entry(graph: obj, values: unit -> obj array) =
    member _.Graph = graph
    member _.Values = values
    member val Last: obj array = null with get, set

/// <summary>Whether an entry serves a read under the graph: a null graph or a null tag matches any.</summary>
let private serves (graph: obj) (entry: Entry) =
    isNull graph
    || isNull entry.Graph
    || obj.ReferenceEquals (entry.Graph, graph)

/// <summary>
/// The live entries of an instance per target, as arrays replaced on each change, and each target's last withdrawn
/// entry that held values.
/// </summary>
[<AllowNullLiteral>]
type Slots() =
    let mutable live: Entry array array = [||]
    let mutable withdrawn: Entry array = [||]

    member private _.Ensure(target: int) =
        if live.Length <= target then
            live <- Array.append live (Array.create (target + 1 - live.Length) [||])
            withdrawn <- Array.append withdrawn (Array.create (target + 1 - withdrawn.Length) null)

    member this.Add(target: int, entry: Entry) =
        this.Ensure target
        let next = Array.copy live
        next[target] <- Array.append live[target] [| entry |]
        live <- next

    member this.Withdraw(target: int, entry: Entry) =
        this.Ensure target
        let next = Array.copy live

        next[target] <-
            live[target]
            |> Array.filter (fun other -> not (obj.ReferenceEquals (other, entry)))

        live <- next

        if not (isNull entry.Last) then
            withdrawn[target] <- entry

    /// <summary>The live entries of the target, as an immutable array.</summary>
    member _.Live(target: int) : Entry array =
        let current = live
        if target < current.Length then current[target] else [||]

    member _.Withdrawn(target: int) : Entry =
        let current = withdrawn
        if target < current.Length then current[target] else null

open System.Runtime.CompilerServices

let private newTable () : obj =
    ConditionalWeakTable<obj, Slots>() :> obj

let private tryFind (table: obj) (instance: obj) : Slots =
    match (table :?> ConditionalWeakTable<obj, Slots>).TryGetValue instance with
    | true, slots -> slots
    | _ -> null

let private slotsOf (table: obj) (instance: obj) : Slots =
    (table :?> ConditionalWeakTable<obj, Slots>).GetValue(instance, fun _ -> Slots ())

/// <summary>Runs <c>action</c> holding the instance's lock.</summary>
let inline private guarded (slots: Slots) ([<InlineIfLambda>] action: unit -> unit) : unit =
    lock slots action

[<Sealed>]
type Cells internal (table: obj) =
    member internal _.Table = table

let create () =
    Cells (newTable ())

/// <summary>Registers the arm's payload reader for the instance and target, and withdraws it on the arm's cleanup.</summary>
let enter (cells: Cells) (setup: Setup) (instance: obj) (target: int) (values: unit -> obj array) : unit =
    let slots = slotsOf cells.Table instance
    let entry = Entry (ambient (), values)
    guarded slots (fun () -> slots.Add (target, entry))
    setup.OnCleanup (fun () -> guarded slots (fun () -> slots.Withdraw (target, entry)))

/// <summary>
/// The bound values of the reader's own arm: the pattern variables, then the matched value. Reads every live entry
/// serving the ambient graph and returns the first; with none, the values held by the target's last withdrawn entry
/// when it serves the ambient graph. A withdrawal under another graph replaces the target's withdrawn entry; a held read
/// under the first graph then raises AM006. Reads run outside the instance's lock.
/// </summary>
let values (cells: Cells) (instance: obj) (target: int) : obj array =
    match tryFind cells.Table instance with
    | null -> Errors.noArm ()
    | slots ->
        let graph = ambient ()
        let mutable first: obj array = null

        for entry in slots.Live target do
            if serves graph entry then
                let read = entry.Values ()
                entry.Last <- read

                if isNull first then
                    first <- read

        if not (isNull first) then
            first
        else
            match slots.Withdrawn target with
            | null -> Errors.noArm ()
            | held when serves graph held -> held.Last
            | _ -> Errors.noArm ()

#endif
