module Ranvier.View.Foundation.Template

open System
open System.Text
open Fable.Core
open Fable.Core.TS.Dom
open Ranvier
open Ranvier.View.Foundation.Representation

/// <summary>Where a dynamic slot writes on its node.</summary>
[<NoEquality; NoComparison>]
type SlotTarget =
    /// <summary>An attribute written from a <c>string option</c>; <c>None</c> removes it.</summary>
    | AttributeTarget of string
    /// <summary>A property written by a shared writer.</summary>
    | PropertyTarget of Func<obj, obj, unit>
    /// <summary>The data of a text node.</summary>
    | TextTarget

/// <summary>How a dynamic slot is re-evaluated.</summary>
type SlotMode =
    /// <summary>Within the instance's render effect.</summary>
    | RenderMode
    /// <summary>In its own effect.</summary>
    | IsolatedMode
    /// <summary>From one fixed source; evaluated in the render effect until core provides fixed-dependency bindings.</summary>
    | DirectMode

/// <summary>A dynamic part of a template: reads a value from the instance and writes it to a node.</summary>
[<NoEquality; NoComparison>]
type Slot =
    {
        Target: SlotTarget
        Mode: SlotMode
        Read: Func<obj, obj>
        Same: Func<obj, obj, bool>
    }

/// <summary>A node of a backend-neutral template tree.</summary>
[<NoEquality; NoComparison>]
type TemplateNode =
    | ElementNode of
        Tag *
        (string * string) array *
        (Func<obj, obj, unit> * obj) array *
        Slot array *
        (string * Func<obj, obj, unit>) array *
        TemplateNode array
    | TextNode of string
    | TextSlotNode of Slot
    | FragmentNode of TemplateNode array

#if FABLE_COMPILER
[<Emit("$0 === $1")>]
let private strictEquals (left: obj) (right: obj) : bool = jsNative
#else
let private strictEquals (left: obj) (right: obj) =
    Object.Equals (left, right)
#endif

/// <summary>Compares primitive slot values: strings, booleans, numbers and options of them.</summary>
let primitiveSame = Func<obj, obj, bool>(fun left right -> strictEquals left right)

/// <summary>One part of an element in a template for instances of type <c>'inst</c>.</summary>
[<NoEquality; NoComparison>]
type Part<'native, 'inst> =
    internal
    | StaticAttribute of string * string
    | StaticProperty of Func<obj, obj, unit> * obj
    | DynamicPart of Slot
    | EventPart of string * Func<obj, obj, unit>

/// <summary>A template subtree for instances of type <c>'inst</c>.</summary>
[<NoEquality; NoComparison>]
type TemplateView<'inst> = internal TemplateView of TemplateNode

/// <summary>A reader of one fixed signal or memo of an instance.</summary>
[<NoEquality; NoComparison>]
type Source<'inst, 'value> = internal Source of Func<obj, obj>

let private slot target mode (read: 'inst -> 'value) same =
    {
        Target = target
        Mode = mode
        Read = Func<obj, obj>(fun instance -> box (read (unbox instance)))
        Same = same
    }

let private sourceSlot target (Source read: Source<'inst, 'value>) same =
    {
        Target = target
        Mode = DirectMode
        Read = read
        Same = same
    }

let private someOf (Source read: Source<'inst, string>) : Source<'inst, string option> =
    Source (Func<obj, obj>(fun instance -> box (Some (unbox<string>(read.Invoke instance)))))

let private writer (write: 'native -> 'value -> unit) =
    Func<obj, obj, unit>(fun node value -> write (unbox node) (unbox value))

let private nodeOf (TemplateView node) = node

let private elementNode tag (parts: Part<'native, 'inst> array) (children: TemplateView<'inst> array) =
    let attributes = ResizeArray ()
    let properties = ResizeArray ()
    let slots = ResizeArray ()
    let events = ResizeArray ()

    for part in parts do
        match part with
        | StaticAttribute (name, value) ->
            validateAttribute name
            attributes.Add (name, value)
        | StaticProperty (write, value) -> properties.Add (write, value)
        | DynamicPart slot -> slots.Add slot
        | EventPart (name, handler) -> events.Add (name, handler)

    TemplateView (ElementNode (tag, attributes.ToArray (), properties.ToArray (), slots.ToArray (), events.ToArray (), Array.map nodeOf children))

/// <summary>Attribute parts: a value is static, a reader or source is dynamic.</summary>
[<Sealed>]
type AttributeBuilder<'inst> internal () =
    member _.attribute(name: string, value: string) : Part<'native, 'inst> =
        StaticAttribute (name, value)

    member _.attribute(name: string, read: 'inst -> string option) : Part<'native, 'inst> =
        validateAttribute name
        DynamicPart (slot (AttributeTarget name) RenderMode read primitiveSame)

    member _.attribute(name: string, source: Source<'inst, string option>) : Part<'native, 'inst> =
        validateAttribute name
        DynamicPart (sourceSlot (AttributeTarget name) source primitiveSame)

    member this.className(value: string) : Part<'native, 'inst> =
        this.attribute ("class", value)

    member this.className(read: 'inst -> string) : Part<'native, 'inst> =
        this.attribute ("class", fun instance -> Some (read instance))

    member this.className(source: Source<'inst, string>) : Part<'native, 'inst> =
        this.attribute ("class", someOf source)

    member this.id(value: string) : Part<'native, 'inst> =
        this.attribute ("id", value)

    member this.id(read: 'inst -> string) : Part<'native, 'inst> =
        this.attribute ("id", fun instance -> Some (read instance))

    member this.id(source: Source<'inst, string>) : Part<'native, 'inst> =
        this.attribute ("id", someOf source)

    member this.title(value: string) : Part<'native, 'inst> =
        this.attribute ("title", value)

    member this.title(read: 'inst -> string) : Part<'native, 'inst> =
        this.attribute ("title", fun instance -> Some (read instance))

    member this.title(source: Source<'inst, string>) : Part<'native, 'inst> =
        this.attribute ("title", someOf source)

/// <summary>Property parts: a value is written once per instance, a reader or source is dynamic.</summary>
[<Sealed>]
type PropertyBuilder<'inst> internal () =
    member _.propertyStatic(write: 'native -> 'value -> unit, value: 'value) : Part<'native, 'inst> =
        StaticProperty (writer write, box value)

    member _.propertyWith(write: 'native -> 'value -> unit, read: 'inst -> 'value, same: Func<obj, obj, bool>) : Part<'native, 'inst> =
        DynamicPart (slot (PropertyTarget (writer write)) RenderMode read same)

    member _.propertyFrom(write: 'native -> 'value -> unit, source: Source<'inst, 'value>, same: Func<obj, obj, bool>) : Part<'native, 'inst> =
        DynamicPart (sourceSlot (PropertyTarget (writer write)) source same)

    member this.value(value: string) : Part<HTMLInputElement, 'inst> =
        this.propertyStatic ((fun (node: HTMLInputElement) next -> node.value <- next), value)

    member this.value(read: 'inst -> string) : Part<HTMLInputElement, 'inst> =
        this.propertyWith ((fun (node: HTMLInputElement) next -> node.value <- next), read, primitiveSame)

    member this.value(source: Source<'inst, string>) : Part<HTMLInputElement, 'inst> =
        this.propertyFrom ((fun (node: HTMLInputElement) next -> node.value <- next), source, primitiveSame)

    member this.disabled(value: bool) : Part<HTMLButtonElement, 'inst> =
        this.propertyStatic ((fun (node: HTMLButtonElement) next -> node.disabled <- next), value)

    member this.disabled(read: 'inst -> bool) : Part<HTMLButtonElement, 'inst> =
        this.propertyWith ((fun (node: HTMLButtonElement) next -> node.disabled <- next), read, primitiveSame)

    member this.disabled(source: Source<'inst, bool>) : Part<HTMLButtonElement, 'inst> =
        this.propertyFrom ((fun (node: HTMLButtonElement) next -> node.disabled <- next), source, primitiveSame)

/// <summary>Event parts: one shared handler per template, given the instance and the event.</summary>
[<Sealed>]
type EventBuilder<'inst> internal () =
    member _.on(eventName: string, handler: 'inst -> Event -> unit) : Part<'native, 'inst> =
        EventPart (eventName, Func<obj, obj, unit>(fun instance event -> handler (unbox instance) (unbox event)))

    member this.onClick(handler: 'inst -> MouseEvent -> unit) : Part<'native, 'inst> =
        this.on ("click", fun instance event -> handler instance (unbox event))

    member this.onInput(handler: 'inst -> InputEvent -> unit) : Part<'native, 'inst> =
        this.on ("input", fun instance event -> handler instance (unbox event))

    member this.onFocus(handler: 'inst -> FocusEvent -> unit) : Part<'native, 'inst> =
        this.on ("focus", fun instance event -> handler instance (unbox event))

/// <summary>Builds a template for instances of type <c>'inst</c>; dynamic parts receive the instance as their argument.</summary>
[<Sealed>]
type TemplateBuilder<'inst> internal () =
    let attrs = AttributeBuilder<'inst>()
    let props = PropertyBuilder<'inst>()
    let events = EventBuilder<'inst>()

    member _.Attrs = attrs
    member _.Props = props
    member _.Events = events

    member _.element (tag: Tag) (parts: Part<'native, 'inst> array) (children: TemplateView<'inst> array) : TemplateView<'inst> =
        elementNode tag parts children

    member _.div (parts: Part<HTMLDivElement, 'inst> array) (children: TemplateView<'inst> array) =
        elementNode Div parts children

    member _.section (parts: Part<HTMLElement, 'inst> array) (children: TemplateView<'inst> array) =
        elementNode (HtmlTag "section") parts children

    member _.span (parts: Part<HTMLSpanElement, 'inst> array) (children: TemplateView<'inst> array) =
        elementNode (HtmlTag "span") parts children

    member _.button (parts: Part<HTMLButtonElement, 'inst> array) (children: TemplateView<'inst> array) =
        elementNode Button parts children

    member _.input(parts: Part<HTMLInputElement, 'inst> array) : TemplateView<'inst> =
        elementNode Input parts [||]

    member _.fragment(children: TemplateView<'inst> array) : TemplateView<'inst> =
        TemplateView (FragmentNode (Array.map nodeOf children))

    member _.text(value: string) : TemplateView<'inst> =
        TemplateView (TextNode value)

    member _.text(read: 'inst -> string) : TemplateView<'inst> =
        TemplateView (TextSlotNode (slot TextTarget RenderMode read primitiveSame))

    member _.text(source: Source<'inst, string>) : TemplateView<'inst> =
        TemplateView (TextSlotNode (sourceSlot TextTarget source primitiveSame))

    member _.from(accessor: 'inst -> Signal<'value>) : Source<'inst, 'value> =
        Source (Func<obj, obj>(fun instance -> box (accessor (unbox instance)).Value))

    member _.from(accessor: 'inst -> Memo<'value>) : Source<'inst, 'value> =
        Source (Func<obj, obj>(fun instance -> box (accessor (unbox instance)).Value))

    member _.isolated(part: Part<'native, 'inst>) : Part<'native, 'inst> =
        match part with
        | DynamicPart slot -> DynamicPart { slot with Mode = IsolatedMode }
        | _ -> invalidArg "part" "Only dynamic parts can be isolated"

    member _.isolated(view: TemplateView<'inst>) : TemplateView<'inst> =
        match view with
        | TemplateView (TextSlotNode slot) -> TemplateView (TextSlotNode { slot with Mode = IsolatedMode })
        | _ -> invalidArg "view" "Only text slots can be isolated"

let rec private flatten (nodes: TemplateNode array) =
    [
        for node in nodes do
            match node with
            | FragmentNode children -> yield! flatten children
            | TextNode "" -> ()
            | other -> other
    ]

let private isText node =
    match node with
    | TextNode _
    | TextSlotNode _ -> true
    | _ -> false

let private rawText tag =
    match name tag with
    | "script"
    | "style"
    | "iframe"
    | "noscript"
    | "xmp"
    | "noembed"
    | "noframes"
    | "plaintext" -> true
    | _ -> false

[<NoEquality; NoComparison>]
type private MarkupMode =
    | Skeleton
    | Server of instance: obj * rejectBrowser: bool

let private hasBrowserParts properties (slots: Slot array) (events: (string * Func<obj, obj, unit>) array) =
    Array.length properties > 0
    || events.Length > 0
    || slots
       |> Array.exists (fun slot ->
           match slot.Target with
           | PropertyTarget _ -> true
           | _ -> false)

let private writeMarkup (output: StringBuilder) mode (targets: ResizeArray<TemplateNode * int array>) (root: TemplateNode) =
    let append (value: string) =
        output.Append value |> ignore

    let rec writeChildren (children: TemplateNode array) (path: int list) =
        let mutable index = 0
        let mutable previousText = false

        for child in flatten children do
            let text = isText child

            if text && previousText then
                append "<!--!-->"
                index <- index + 1

            writeNode child (index :: path)
            index <- index + 1
            previousText <- text

    and writeNode node path =
        match node with
        | TextNode value -> append (Html.escape false value)
        | TextSlotNode slot ->
            match mode with
            | Skeleton ->
                targets.Add (node, Array.ofList (List.rev path))
                append " "
            | Server (instance, _) ->
                let value = unbox<string>(slot.Read.Invoke instance)
                append (Html.escape false (if isNull value then "" else value))
        | FragmentNode _ -> invalidOp "Fragments are flattened before writing"
        | ElementNode (tag, attributes, properties, slots, events, children) ->
            if rawText tag then
                raise (InvalidOperationException "Raw-text elements require an explicit HTML serialization policy")

            match mode with
            | Skeleton ->
                if
                    properties.Length > 0
                    || slots.Length > 0
                    || events.Length > 0
                then
                    targets.Add (node, Array.ofList (List.rev path))
            | Server (_, rejectBrowser) ->
                if
                    rejectBrowser
                    && hasBrowserParts properties slots events
                then
                    invalidOp "Browser capability requires explicit Omit policy"

            append ("<" + name tag)

            for key, value in attributes do
                append (" " + key + "=\"" + Html.escape true value + "\"")

            match mode with
            | Server (instance, _) ->
                for slot in slots do
                    match slot.Target with
                    | AttributeTarget key ->
                        match unbox<string option>(slot.Read.Invoke instance) with
                        | Some value -> append (" " + key + "=\"" + Html.escape true value + "\"")
                        | None -> ()
                    | _ -> ()
            | Skeleton -> ()

            append ">"

            if not (isVoid tag) then
                writeChildren children path
                append ("</" + name tag + ">")

    writeChildren [| root |] []

/// <summary>The skeleton markup of a template and the path of every dynamic node, in document order.</summary>
/// <remarks>A path lists child indexes from the skeleton's root fragment.</remarks>
let skeletonMarkup (root: TemplateNode) =
    let output = StringBuilder ()
    let targets = ResizeArray ()
    writeMarkup output Skeleton targets root
    output.ToString (), targets.ToArray ()

/// <summary>A template component definition without its props type.</summary>
[<AbstractClass>]
type TemplateComponent internal (setup: Func<Setup, obj, obj>, root: TemplateNode) =
    inherit TemplateDefinition()
    member internal _.Setup = setup
    member internal _.Root = root

/// <summary>Renders an instance as server HTML with the skeleton's markers.</summary>
let renderHtml (definition: TemplateComponent) (setup: Setup) (props: obj) (rejectBrowser: bool) =
    let instance = definition.Setup.Invoke (setup, props)
    let output = StringBuilder ()
    writeMarkup output (Server (instance, rejectBrowser)) (ResizeArray ()) definition.Root
    output.ToString ()

let rec private expandNode (ops: Operations<'view>) (instance: obj) (node: TemplateNode) : 'view =
    match node with
    | TextNode value -> ops.Text value
    | TextSlotNode slot -> ops.Bind (fun left right -> slot.Same.Invoke (box left, box right)) (fun () -> unbox<string>(slot.Read.Invoke instance))
    | FragmentNode children -> ops.Fragment (Array.map (expandNode ops instance) children)
    | ElementNode (tag, attributes, properties, slots, events, children) ->
        let modifiers: ModifierAction array =
            [|
                for key, value in attributes -> Attribute (key, value)
                for write, value in properties -> fun (backend: Backend) -> backend.Property (fun target -> write.Invoke (target, value))
                for slot in slots ->
                    match slot.Target with
                    | AttributeTarget key -> ReactiveAttribute (key, fun () -> unbox<string option>(slot.Read.Invoke instance))
                    | PropertyTarget write -> fun (backend: Backend) -> backend.ReactiveProperty ((fun () -> slot.Read.Invoke instance), write)
                    | TextTarget -> invalidOp "A text slot is a child node"
                for eventName, handler in events -> listenModifier eventName (fun event -> handler.Invoke (instance, event))
            |]

        ops.Element tag modifiers (Array.map (expandNode ops instance) children)

/// <summary>Expands a definition into any view representation through its operations.</summary>
let expandWith (ops: Operations<'view>) (definition: TemplateComponent) (props: obj) : 'view =
    ops.Component (fun context -> expandNode ops (definition.Setup.Invoke (context, props)) definition.Root)

/// <summary>A component whose template is built once per definition; setup runs per instance.</summary>
[<Sealed>]
type Component<'props> internal (setup: Func<Setup, obj, obj>, root: TemplateNode) =
    inherit TemplateComponent(setup, root)

    override this.Expand(backend, props) =
        Program.run (expandWith Program.ops (this :> TemplateComponent) props) backend

    override this.RenderHtml(context, props, rejectBrowser) =
        renderHtml (this :> TemplateComponent) context props rejectBrowser

module Component =
    /// <summary>Defines a component from per-instance setup and a template built once.</summary>
    /// <remarks><c>setup</c> runs untracked for each instance and returns the instance that dynamic parts receive.</remarks>
    let define (setup: Setup -> 'props -> 'inst) (build: TemplateBuilder<'inst> -> TemplateView<'inst>) : Component<'props> =
        let (TemplateView root) = build (TemplateBuilder<'inst>())
        Component<'props>(Func<Setup, obj, obj>(fun context props -> box (setup context (unbox props))), root)

    /// <summary>Renders an instance as a Program view; the DOM backend materialises the compiled template.</summary>
    let render (definition: Component<'props>) (props: 'props) : Program.View =
        Program.View (fun backend -> backend.Template (definition, box props))

    /// <summary>Renders an instance in any representation through the default expansion.</summary>
    let renderWith (ops: Operations<'view>) (definition: Component<'props>) (props: 'props) : 'view =
        expandWith ops (definition :> TemplateComponent) (box props)
