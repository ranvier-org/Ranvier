/// <summary>
/// The host primitives of the view sources. Under Fable each primitive is an Emit expression inlined at its call sites; on
/// .NET each is F# over the <c>Fable.Core.TS.Dom</c> interfaces, the installed host, or .NET collections.
/// </summary>
module internal Ranvier.View.Foundation.Native

open Fable.Core
open Fable.Core.TS.Dom

#if !FABLE_COMPILER
/// <summary>The DOM globals of the current thread; the DOM twin installs one per document.</summary>
type IHost =
    abstract Document: Document
    abstract QueueMicrotask: (unit -> unit) -> unit
    abstract SetTimeout: (unit -> unit) * int -> obj
    abstract ClearTimeout: obj -> unit
    abstract RequestAnimationFrame: (float -> unit) -> obj
    abstract CancelAnimationFrame: obj -> unit
    /// <summary>Removes the nodes strictly between two siblings, as <c>Range.deleteContents</c> does.</summary>
    abstract DeleteBetween: Node * Node -> unit
    /// <summary>Adds an event listener and returns its removal.</summary>
    abstract Listen: target: obj * eventName: string * listener: (obj -> unit) -> (unit -> unit)
    /// <summary>Makes <c>currentTarget</c> of <c>event</c> read <c>read ()</c> until the dispatch ends.</summary>
    abstract OverrideCurrentTarget: event: obj * read: (unit -> obj) -> unit

[<Sealed; AbstractClass>]
type private HostSlot =
    [<System.ThreadStatic; DefaultValue>]
    static val mutable private current: IHost

    static member Current
        with get () = HostSlot.current
        and set (value: IHost) = HostSlot.current <- value

/// <summary>
/// Installs <c>host</c> for the current thread until the returned value is disposed. Disposal restores the previous
/// host; it raises <c>InvalidOperationException</c> on another thread or while a later installation is current.
/// </summary>
let install (host: IHost) : System.IDisposable =
    let previous = HostSlot.Current
    let thread = System.Environment.CurrentManagedThreadId
    HostSlot.Current <- host
    let disposed = ref false

    { new System.IDisposable with
        member _.Dispose() =
            if not disposed.Value then
                let current = System.Environment.CurrentManagedThreadId

                if current <> thread then
                    raise (System.InvalidOperationException $"A DOM host installed on thread {thread} was disposed on thread {current}")

                if not (obj.ReferenceEquals (HostSlot.Current, host)) then
                    raise (System.InvalidOperationException "A DOM host was disposed while a later installation is current")

                disposed.Value <- true
                HostSlot.Current <- previous
    }

let private host () : IHost =
    let current = HostSlot.Current

    if isNull (box current) then
        raise (System.InvalidOperationException "No DOM host is installed on this thread; create a TwinDocument first")

    current

let inline private orNull (value: 'T option) : 'R =
    match value with
    | Some item -> unbox<'R> item
    | None -> Unchecked.defaultof<'R>
#endif

#if FABLE_COMPILER
[<Emit("document")>]
let document () : Document = jsNative
#else
let document () : Document =
    (host ()).Document
#endif

#if FABLE_COMPILER
[<Emit("$0 == null")>]
let isNullish (value: 'T) : bool = jsNative
#else
let isNullish (value: 'T) : bool =
    isNull (box value)
#endif

#if FABLE_COMPILER
[<Emit("$0.nextSibling")>]
let nextOrNull (node: Node) : Node = jsNative
#else
let nextOrNull (node: Node) : Node =
    orNull node.nextSibling
#endif

#if FABLE_COMPILER
[<Emit("$0.firstChild")>]
let firstChildOf (node: Node) : Node = jsNative
#else
let firstChildOf (node: Node) : Node =
    orNull node.firstChild
#endif

#if FABLE_COMPILER
[<Emit("$0.remove()")>]
let removeNode (node: Node) : unit = jsNative
#else
let removeNode (node: Node) : unit =
    (unbox<ChildNode> node).remove()
#endif

#if FABLE_COMPILER
[<Emit("$0.textContent")>]
let textContentOf (node: Node) : string = jsNative
#else
let textContentOf (node: Node) : string =
    Option.toObj node.textContent
#endif

#if FABLE_COMPILER
[<Emit("document.getElementById($0)")>]
let elementById (id: string) : Element = jsNative
#else
let elementById (id: string) : Element =
    orNull ((document ()).getElementById id)
#endif

#if !FABLE_COMPILER
/// <summary>
/// JavaScript's SameValueZero over the values Fable writes as primitives: strings, chars, numbers, booleans and
/// <c>Some</c> of those compare by value, NaN equals NaN and -0 equals +0; every other value compares by reference.
/// </summary>
[<Sealed>]
type SameValueZero private () =
    static member val Instance = SameValueZero ()

    static member internal Normal(value: obj) : obj =
        match value with
        | null -> null
        | :? string -> value
        | :? char as c -> box (string c)
        | :? bool -> value
        | :? int8
        | :? uint8
        | :? int16
        | :? uint16
        | :? int32
        | :? uint32
        | :? float32
        | :? float -> box (System.Convert.ToDouble value)
        | _ ->
            let t = value.GetType ()

            if
                t.IsGenericType
                && t.GetGenericTypeDefinition () = typedefof<option<_>>
            then
                SameValueZero.Normal (t.GetProperty("Value").GetValue value)
            else
                value

    interface System.Collections.Generic.IEqualityComparer<obj> with
        member _.Equals(a, b) =
            match SameValueZero.Normal a, SameValueZero.Normal b with
            | null, null -> true
            | (:? string as x), (:? string as y) -> System.String.Equals (x, y, System.StringComparison.Ordinal)
            | (:? float as x), (:? float as y) ->
                x = y
                || (System.Double.IsNaN x && System.Double.IsNaN y)
            | (:? System.ValueType as x), (:? System.ValueType as y) -> x.GetType () = y.GetType () && x.Equals y
            | x, y -> obj.ReferenceEquals (x, y)

        member _.GetHashCode a =
            match SameValueZero.Normal a with
            | null -> 0
            | :? string as s -> System.StringComparer.Ordinal.GetHashCode s
            | :? float as f ->
                if System.Double.IsNaN f then 0x7ff8
                elif f = 0.0 then 0
                else f.GetHashCode ()
            | :? System.ValueType as v -> v.GetHashCode ()
            | other -> System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode other

/// <summary>A JavaScript <c>Map</c>, <c>Set</c>, <c>WeakMap</c> or <c>WeakSet</c> on .NET; a set stores each key as its own value.</summary>
[<Sealed; AllowNullLiteral>]
type NativeTable(weak: bool) =
    let ordered =
        if weak then
            null
        else
            System.Collections.Generic.OrderedDictionary<obj, obj>(SameValueZero.Instance)

    let held =
        if weak then
            System.Runtime.CompilerServices.ConditionalWeakTable<obj, obj>()
        else
            null

    member _.Has(key: obj) =
        if weak then
            let mutable value = null
            held.TryGetValue (key, &value)
        else
            ordered.ContainsKey key

    member _.Get(key: obj) : obj =
        let mutable value = null

        if weak then
            held.TryGetValue (key, &value) |> ignore
        else
            ordered.TryGetValue (key, &value) |> ignore

        value

    member _.Set(key: obj, value: obj) =
        if weak then
            held.AddOrUpdate (key, value)
        else
            ordered[key] <- value

    member _.Delete(key: obj) =
        if weak then
            held.Remove key |> ignore
        else
            ordered.Remove key |> ignore

    member _.Keys: obj array =
        if weak then
            invalidOp "A weak table cannot be enumerated"
        else
            Seq.toArray ordered.Keys

let private table (value: obj) : NativeTable =
    value :?> NativeTable

let private expandos =
    System.Runtime.CompilerServices.ConditionalWeakTable<obj, System.Collections.Generic.Dictionary<string, obj>>()
#endif

// Collections

#if FABLE_COMPILER
[<Emit("new Map()")>]
let newMap () : obj = jsNative

[<Emit("new Set()")>]
let newSet () : obj = jsNative

[<Emit("new WeakMap()")>]
let newWeakMap () : obj = jsNative

[<Emit("new WeakSet()")>]
let newWeakSet () : obj = jsNative
#else
let newMap () : obj =
    NativeTable false

let newSet () : obj =
    NativeTable false

let newWeakMap () : obj =
    NativeTable true

let newWeakSet () : obj =
    NativeTable true
#endif

#if FABLE_COMPILER
[<Emit("$0.has($1)")>]
let has (tableValue: obj) (key: 'K) : bool = jsNative
#else
let has (tableValue: obj) (key: 'K) : bool =
    (table tableValue).Has(box key)
#endif

#if FABLE_COMPILER
[<Emit("$0.get($1)")>]
let get<'K, 'T> (tableValue: obj) (key: 'K) : 'T = jsNative
#else
let get<'K, 'T> (tableValue: obj) (key: 'K) : 'T =
    match (table tableValue).Get(box key) with
    | null -> Unchecked.defaultof<'T>
    | value -> unbox<'T> value
#endif

#if FABLE_COMPILER
[<Emit("($0.get($1) ?? -1)")>]
let getIndex (tableValue: obj) (key: 'K) : int = jsNative
#else
let getIndex (tableValue: obj) (key: 'K) : int =
    match (table tableValue).Get(box key) with
    | null -> -1
    | value -> unbox<int> value
#endif

#if FABLE_COMPILER
[<Emit("$0.set($1, $2)")>]
let set (tableValue: obj) (key: 'K) (value: 'T) : unit = jsNative
#else
let set (tableValue: obj) (key: 'K) (value: 'T) : unit =
    (table tableValue).Set(box key, box value)
#endif

#if FABLE_COMPILER
[<Emit("$0.add($1)")>]
let add (tableValue: obj) (key: 'K) : unit = jsNative
#else
let add (tableValue: obj) (key: 'K) : unit =
    (table tableValue).Set(box key, box key)
#endif

#if FABLE_COMPILER
[<Emit("$0.delete($1)")>]
let delete (tableValue: obj) (key: 'K) : unit = jsNative
#else
let delete (tableValue: obj) (key: 'K) : unit =
    (table tableValue).Delete(box key)
#endif

#if FABLE_COMPILER
[<Emit("$0.forEach($1)")>]
let forEach (tableValue: obj) (action: 'K -> unit) : unit = jsNative
#else
let forEach (tableValue: obj) (action: 'K -> unit) : unit =
    for key in (table tableValue).Keys do
        action (unbox<'K> key)
#endif

// Arrays: a JavaScript read past the end is undefined and a write at the end appends.

#if FABLE_COMPILER
[<Emit("$0[$1]")>]
let listAt (items: ResizeArray<'T>) (index: int) : 'T = jsNative

[<Emit("$0[$1] = $2")>]
let listSet (items: ResizeArray<'T>) (index: int) (value: 'T) : unit = jsNative

[<Emit("$0.length = $1")>]
let listTruncate (items: ResizeArray<'T>) (count: int) : unit = jsNative

[<Emit("$0.splice($1, $2, ...$3)")>]
let listSplice (items: ResizeArray<'T>) (start: int) (count: int) (inserted: 'T array) : unit = jsNative

[<Emit("$0[$1]")>]
let arrayAt (items: 'T array) (index: int) : 'T = jsNative

[<Emit("$0[$1] = $2")>]
let arraySet (items: 'T array) (index: int) (value: 'T) : unit = jsNative

[<Emit("$0.slice()")>]
let arrayCopy (items: 'T array) : 'T array = jsNative

[<Emit("$0.slice($1, $2)")>]
let arraySlice (items: 'T array) (start: int) (finish: int) : 'T array = jsNative

[<Emit("new Array($0)")>]
let allocate (length: int) : obj array = jsNative
#else
let listAt (items: ResizeArray<'T>) (index: int) : 'T =
    if index >= 0 && index < items.Count then
        items[index]
    else
        Unchecked.defaultof<'T>

let listSet (items: ResizeArray<'T>) (index: int) (value: 'T) : unit =
    if index = items.Count then
        items.Add value
    else
        items[index] <- value

let listTruncate (items: ResizeArray<'T>) (count: int) : unit =
    if count < items.Count then
        items.RemoveRange (count, items.Count - count)

let listSplice (items: ResizeArray<'T>) (start: int) (count: int) (inserted: 'T array) : unit =
    items.RemoveRange (start, min count (items.Count - start))
    items.InsertRange (start, inserted)

let arrayAt (items: 'T array) (index: int) : 'T =
    if index >= 0 && index < items.Length then
        items[index]
    else
        Unchecked.defaultof<'T>

let arraySet (items: 'T array) (index: int) (value: 'T) : unit =
    items[index] <- value

let arrayCopy (items: 'T array) : 'T array =
    Array.copy items

let arraySlice (items: 'T array) (start: int) (finish: int) : 'T array =
    let finish = min finish items.Length

    if finish <= start then
        [||]
    else
        Array.sub items start (finish - start)

let allocate (length: int) : obj array =
    Array.zeroCreate length
#endif

// Expandos: the per-object properties Fable writes as JavaScript properties.

#if FABLE_COMPILER
[<Emit("$0[$1]")>]
let expando<'T> (target: obj) (name: string) : 'T = jsNative

[<Emit("$0[$1] = $2")>]
let setExpando (target: obj) (name: string) (value: 'T) : unit = jsNative
#else
let expando<'T> (target: obj) (name: string) : 'T =
    let mutable slots = null

    if expandos.TryGetValue (target, &slots) then
        match slots.TryGetValue name with
        | true, value -> unbox<'T> value
        | false, _ -> Unchecked.defaultof<'T>
    else
        Unchecked.defaultof<'T>

let setExpando (target: obj) (name: string) (value: 'T) : unit =
    let slots =
        expandos.GetValue (target, fun _ -> System.Collections.Generic.Dictionary<string, obj>())

    slots[name] <- box value
#endif
