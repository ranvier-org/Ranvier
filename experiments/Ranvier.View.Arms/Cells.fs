/// <summary>A site's lookup from an instance to the payload readers of its mounted arms, by target.</summary>
module internal Ranvier.View.Arms.Cells

open Ranvier.View.Foundation.Representation

/// <summary>The payload reader of each target's mounted arm for an instance.</summary>
[<AllowNullLiteral>]
type Slots() =
    let readers = ResizeArray<unit -> obj array>()

    member _.Set(target: int, read: unit -> obj array) =
        while readers.Count <= target do
            readers.Add Unchecked.defaultof<_>

        readers[target] <- read

    member _.Get(target: int) : unit -> obj array =
        if target < readers.Count then
            readers[target]
        else
            Unchecked.defaultof<_>

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

[<Sealed>]
type Cells internal (table: obj) =
    member internal _.Table = table

let create () =
    Cells (newTable ())

/// <summary>Registers the arm's payload reader for the instance and target.</summary>
let enter (cells: Cells) (_: Setup) (instance: obj) (target: int) (values: unit -> obj array) : unit =
    (slotsOf cells.Table instance).Set(target, values)

/// <summary>The bound values of the reader's own arm: the pattern variables, then the matched value.</summary>
let values (cells: Cells) (instance: obj) (target: int) : obj array =
    match tryFind cells.Table instance with
    | null -> Errors.noArm ()
    | slots ->
        let read = slots.Get target

        if isNull (box read) then Errors.noArm () else read ()
