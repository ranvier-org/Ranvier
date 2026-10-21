/// <summary>A site's lookup from an instance to the payload readers of its mounted arms, one list per target.</summary>
module internal Ranvier.View.Arms.Cells

open global.Ranvier
open Ranvier.View.Foundation.Representation
#if FABLE_COMPILER
open Fable.Core

// JavaScript runs one thread: entry lists change in place. An entry is { g: graph, v: values, l: last values }; an
// instance's slots are { l: live entries per target, w: last withdrawn entry per target }.

/// <summary>The site's WeakMap from an instance to its slots.</summary>
type Cells = obj

[<Emit("new WeakMap()")>]
let create () : Cells = jsNative

/// <summary>The ambient graph, or undefined outside one.</summary>
[<Emit("$0")>]
let private tag (graph: Graph voption) : obj = jsNative

[<Emit("($0.get($1) || ($0.set($1, { l: [], w: [] }), $0.get($1)))")>]
let private slotsOf (cells: Cells) (instance: obj) : obj = jsNative

[<Emit("$0.get($1)")>]
let private tryFind (cells: Cells) (instance: obj) : obj = jsNative

[<Emit("{ g: $0, v: $1, l: null }")>]
let private entry (graph: obj) (values: unit -> obj array) : obj = jsNative

[<Emit("($0.l[$1] || ($0.l[$1] = [])).push($2)")>]
let private add (slots: obj) (target: int) (entry: obj) : unit = jsNative

/// <summary>Removes the entry from the target's live list and, when it holds values, keeps it as the withdrawn entry.</summary>
[<Emit("($0.l[$1].splice($0.l[$1].indexOf($2) >>> 0, 1), $2.l != null && ($0.w[$1] = $2))")>]
let private withdraw (slots: obj) (target: int) (entry: obj) : unit = jsNative

[<Emit("($0 && $0.l[$1] || [])")>]
let private live (slots: obj) (target: int) : obj array = jsNative

[<Emit("($0 && $0.w[$1])")>]
let private withdrawn (slots: obj) (target: int) : obj = jsNative

[<Emit("$0[$1]")>]
let private at (entries: obj array) (index: int) : obj = jsNative

[<Emit("$0 == null")>]
let private absent (value: obj) : bool = jsNative

[<Emit("null")>]
let private unread () : obj array = jsNative

/// <summary>Whether an entry serves a read under the graph: a null graph or a null tag matches any.</summary>
[<Emit("($0 == null || $1.g == null || $1.g === $0)")>]
let private serves (graph: obj) (entry: obj) : bool = jsNative

[<Emit("$0.l = $0.v()")>]
let private read (entry: obj) : obj array = jsNative

[<Emit("$0.l")>]
let private last (entry: obj) : obj array = jsNative

/// <summary>Registers the arm's payload reader for the instance and target, and withdraws it on the arm's cleanup.</summary>
let enter (cells: Cells) (setup: Setup) (instance: obj) (target: int) (values: unit -> obj array) : unit =
    let slots = slotsOf cells instance
    let registered = entry (tag Graph.TryCurrent) values
    add slots target registered
    setup.OnCleanup (fun () -> withdraw slots target registered)

/// <summary>
/// The bound values of the reader's own arm: the pattern variables, then the matched value. Reads every live entry
/// serving the ambient graph and returns the first; with none, the values held by the target's last withdrawn entry
/// when it serves the ambient graph. A withdrawal under another graph replaces the target's withdrawn entry; a held read
/// under the first graph then raises AM006.
/// </summary>
let values (cells: Cells) (instance: obj) (target: int) : obj array =
    let slots = tryFind cells instance
    let graph = tag Graph.TryCurrent
    let entries = live slots target
    let mutable first = unread ()

    for index in 0 .. entries.Length - 1 do
        let candidate = at entries index

        if serves graph candidate then
            let values = read candidate

            if absent first then
                first <- values

    if not (absent first) then
        first
    else
        let held = withdrawn slots target

        if not (absent held) && serves graph held then
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
