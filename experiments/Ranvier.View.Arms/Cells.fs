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

/// <summary>The live entries of one instance per target, and the values the last withdrawn entry of each target held.</summary>
[<AllowNullLiteral>]
type Slots() =
    let live = ResizeArray<ResizeArray<Entry>>()
    let withdrawn = ResizeArray<obj array>()

    member private _.Ensure(target: int) =
        while live.Count <= target do
            live.Add (ResizeArray ())
            withdrawn.Add null

    member this.Add(target: int, entry: Entry) =
        this.Ensure target
        live[target].Add entry

    member this.Withdraw(target: int, entry: Entry) =
        this.Ensure target
        live[target].Remove entry |> ignore
        withdrawn[target] <- entry.Last

    member this.Live(target: int) : ResizeArray<Entry> =
        this.Ensure target
        live[target]

    member this.Withdrawn(target: int) : obj array =
        this.Ensure target
        withdrawn[target]

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
let inline private guarded (slots: Slots) ([<InlineIfLambda>] action: unit -> 'a) : 'a =
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
/// The bound values of the reader's own arm: the pattern variables, then the matched value. Reads every live entry of
/// the ambient graph (every entry when no graph is ambient) and returns the first; with no live entry, the values the
/// last withdrawn entry held.
/// </summary>
let values (cells: Cells) (instance: obj) (target: int) : obj array =
    match tryFind cells.Table instance with
    | null -> Errors.noArm ()
    | slots ->
        let graph = ambient ()

        guarded slots (fun () ->
            let mutable first: obj array = null

            for entry in slots.Live target do
                if
                    isNull graph
                    || obj.ReferenceEquals (entry.Graph, graph)
                then
                    let read = entry.Values ()
                    entry.Last <- read

                    if isNull first then
                        first <- read

            if not (isNull first) then
                first
            else
                match slots.Withdrawn target with
                | null -> Errors.noArm ()
                | held -> held)
