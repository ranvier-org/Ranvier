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
