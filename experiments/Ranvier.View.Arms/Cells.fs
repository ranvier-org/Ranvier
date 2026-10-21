/// <summary>A site's lookup from an instance to the payload readers of its mounted arms, one list per target.</summary>
module internal Ranvier.View.Arms.Cells

open global.Ranvier
open Ranvier.View.Foundation.Representation

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

    /// <summary>The live entries of the target; the array is never changed after it is returned.</summary>
    member _.Live(target: int) : Entry array =
        let current = live
        if target < current.Length then current[target] else [||]

    member _.Withdrawn(target: int) : Entry =
        let current = withdrawn
        if target < current.Length then current[target] else null

#if FABLE_COMPILER
open Fable.Core

[<Emit("new WeakMap()")>]
let private newTable () : obj = jsNative

[<Emit("$0.get($1)")>]
let private tryFind (table: obj) (instance: obj) : Slots = jsNative

[<Emit("$0.set($1, $2)")>]
let private store (table: obj) (instance: obj) (slots: Slots) : unit = jsNative

let private slotsOf (table: obj) (instance: obj) : Slots =
    match tryFind table instance with
    | null ->
        let slots = Slots ()
        store table instance slots
        slots
    | slots -> slots
#else
open System.Runtime.CompilerServices

let private newTable () : obj =
    ConditionalWeakTable<obj, Slots>() :> obj

let private tryFind (table: obj) (instance: obj) : Slots =
    match (table :?> ConditionalWeakTable<obj, Slots>).TryGetValue instance with
    | true, slots -> slots
    | _ -> null

let private slotsOf (table: obj) (instance: obj) : Slots =
    (table :?> ConditionalWeakTable<obj, Slots>).GetValue(instance, fun _ -> Slots ())
#endif

/// <summary>Runs <c>action</c> holding the instance's lock on .NET; Fable runs it directly.</summary>
let inline private guarded (slots: Slots) ([<InlineIfLambda>] action: unit -> unit) : unit =
#if FABLE_COMPILER
    action ()
#else
    lock slots action
#endif

let private ambient () : obj =
    match Graph.TryCurrent with
    | ValueSome graph -> box graph
    | ValueNone -> null

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
/// when it serves the ambient graph. Reads run outside the instance's lock.
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
