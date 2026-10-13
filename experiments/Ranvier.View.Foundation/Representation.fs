module Ranvier.View.Foundation.Representation

type Tag =
    | Div
    | Button
    | Input
    | Select
    | Option
    | HtmlTag of string

type BrowserPolicy =
    | Reject
    | Omit

type Setup = { OnCleanup: (unit -> unit) -> unit }

[<AbstractClass>]
type Backend() =
    abstract Text: string -> unit
    abstract Element: Tag * (Backend -> unit) array * (unit -> unit) -> unit
    /// <summary>Creates an element whose children run against this backend in order.</summary>
    abstract ElementWith: Tag * (Backend -> unit) array * (Backend -> unit) array -> unit

    default this.ElementWith(tag, modifiers, children) =
        this.Element (
            tag,
            modifiers,
            fun () ->
                for child in children do
                    child this
        )

    /// <summary>Sets an attribute on the element currently receiving modifiers.</summary>
    abstract Attribute: string * string -> unit
    /// <summary>Sets or removes an attribute on the element currently receiving modifiers.</summary>
    abstract OptionalAttribute: string * string option -> unit
    /// <summary>Binds an attribute reader to the element currently receiving modifiers.</summary>
    abstract ReactiveAttribute: string * (unit -> string option) -> unit
    /// <summary>Sets the live input value of the element currently receiving modifiers.</summary>
    abstract InputValue: string -> unit
    /// <summary>Acquires browser resources for the element currently receiving modifiers.</summary>
    abstract Browser: (obj -> (unit -> unit)) -> unit
    /// <summary>Handles a named DOM event on the element currently receiving modifiers.</summary>
    /// <remarks>The handler receives the event object. A backend may dispatch it through a listener on an ancestor.</remarks>
    abstract Listen: string * (obj -> unit) -> unit
    /// <summary>Applies a static property writer to the element currently receiving modifiers.</summary>
    abstract Property: (obj -> unit) -> unit
    /// <summary>Binds a property reader and target-aware writer to the element currently receiving modifiers.</summary>
    abstract ReactiveProperty: (unit -> obj) * System.Func<obj, obj, unit> -> unit
    abstract Bind: (string -> string -> bool) * (unit -> string) -> unit
    /// <summary>Interprets a keyed item snapshot and renders its items into this backend.</summary>
    abstract Keyed: (obj -> obj -> bool) * (unit -> (obj * obj) array) * ((unit -> obj) -> Backend -> unit) -> unit
    abstract Component: (Setup -> unit) -> unit

/// <summary>Applies a modifier to the backend's current element.</summary>
type ModifierAction = Backend -> unit

/// <summary>Creates a static attribute modifier.</summary>
let Attribute (key, value) : ModifierAction =
    fun backend -> backend.Attribute (key, value)

/// <summary>Creates an attribute modifier that removes its attribute for None.</summary>
let OptionalAttribute (key, value) : ModifierAction =
    fun backend -> backend.OptionalAttribute (key, value)

/// <summary>Creates an attribute modifier from a tracked reader.</summary>
let ReactiveAttribute (key, read) : ModifierAction =
    fun backend -> backend.ReactiveAttribute (key, read)

/// <summary>Creates a modifier for a live input value.</summary>
let InputValue value : ModifierAction =
    fun backend -> backend.InputValue value

/// <summary>Creates a modifier that acquires browser resources and returns their cleanup.</summary>
let browserModifier hook : ModifierAction =
    fun backend -> backend.Browser hook

/// <summary>Creates a modifier that handles a named DOM event.</summary>
let listenModifier eventName handler : ModifierAction =
    fun backend -> backend.Listen (eventName, handler)

type Operations<'view> =
    {
        Empty: 'view
        Text: string -> 'view
        Fragment: 'view array -> 'view
        Element: Tag -> ModifierAction array -> 'view array -> 'view
        Bind: (string -> string -> bool) -> (unit -> string) -> 'view
        /// <summary>Creates the internal keyed-list representation used by <c>Keyed.list</c>.</summary>
        Keyed: (obj -> obj -> bool) -> (unit -> (obj * obj) array) -> ((unit -> obj) -> 'view) -> 'view
        Component: (Setup -> 'view) -> 'view
    }

module Keyed =
    let internal validateUniqueKeys equals items =
        let seen = ResizeArray<obj>()

        for key, _ in items do
            let mutable duplicate = false

            for previous in seen do
                if equals previous key then
                    duplicate <- true

            if duplicate then
                raise (System.ArgumentException ("Keyed items must have unique keys", "items"))

            seen.Add key

    let private create
        (ops: Operations<'view>)
        (equals: 'key -> 'key -> bool)
        (key: 'item -> 'key)
        (read: unit -> 'item array)
        (render: (unit -> 'item) -> 'view)
        =
        if isNull (box equals) then
            invalidArg "equals" "Key comparer cannot be null"

        if isNull (box read) then
            invalidArg "read" "Keyed item reader cannot be null"

        if isNull (box key) then
            invalidArg "key" "Key selector cannot be null"

        if isNull (box render) then
            invalidArg "render" "Keyed row renderer cannot be null"

        let equalsObjects left right =
            equals (unbox left) (unbox right)

        let readObjects () =
            read ()
            |> Array.map (fun item -> box (key item), box item)

        let renderObject (current: unit -> obj) =
            render (fun () -> unbox<'item>(current ()))

        ops.Keyed equalsObjects readObjects renderObject

    /// <summary>Retains keyed rows and supplies their current item values.</summary>
    /// <remarks>The input array's slots are copied before key callbacks run.
    /// Duplicate keys fail before reconciliation mutates the DOM.</remarks>
    let array ops equals key (read: unit -> 'item array) render =
        if isNull (box read) then
            invalidArg "read" "Keyed item reader cannot be null"

        create ops equals key (fun () -> Array.copy (read ())) render

    /// <summary>Adapts list snapshots to the array-backed keyed renderer.</summary>
    /// <remarks>Equal keys retain rows; their item readers return current values.</remarks>
    let list ops equals key (read: unit -> 'item list) render =
        if isNull (box read) then
            invalidArg "read" "Keyed item reader cannot be null"

        create ops equals key (fun () -> List.toArray (read ())) render

module Branch =
    /// <summary>Selects a mounted instance by key and supplies a reader for its current selection.</summary>
    /// <remarks>Equal keys retain the instance; changed keys replace it.
    /// Include the branch discriminator in the key when component shape changes.</remarks>
    let select (ops: Operations<'view>) (equals: 'key -> 'key -> bool) (key: 'item -> 'key) (read: unit -> 'item) (render: (unit -> 'item) -> 'view) =
        if isNull (box equals) then
            invalidArg "equals" "Key comparer cannot be null"

        if isNull (box key) then
            invalidArg "key" "Key selector cannot be null"

        if isNull (box read) then
            invalidArg "read" "Selection reader cannot be null"

        if isNull (box render) then
            invalidArg "render" "Instance renderer cannot be null"

        let readObjects () =
            let item = read ()
            [| box (key item), box item |]

        ops.Keyed (fun left right -> equals (unbox left) (unbox right)) readObjects (fun current -> render (fun () -> unbox (current ())))

module Tree =
    type View =
        private
        | Empty
        | Text of string
        | Fragment of View array
        | Element of Tag * ModifierAction array * View array
        | Bind of (string -> string -> bool) * (unit -> string)
        | Keyed of (obj -> obj -> bool) * (unit -> (obj * obj) array) * ((unit -> obj) -> View)
        | Component of (Setup -> View)

    let rec run view (backend: Backend) =
        match view with
        | Empty -> ()
        | Text value -> backend.Text value
        | Fragment children ->
            for child in children do
                run child backend
        | Element (tag, tokens, children) ->
            backend.Element (
                tag,
                tokens,
                fun () ->
                    for child in children do
                        run child backend
            )
        | Bind (equals, read) -> backend.Bind (equals, read)
        | Keyed (equals, read, render) -> backend.Keyed (equals, read, fun current target -> run (render current) target)
        | Component setup -> backend.Component (fun context -> run (setup context) backend)

    let ops =
        {
            Empty = Empty
            Text = Text
            Fragment = Fragment
            Element = fun tag tokens children -> Element (tag, tokens, children)
            Bind = fun equals read -> Bind (equals, read)
            Keyed = fun equals read render -> Keyed (equals, read, render)
            Component = Component
        }

module Program =
    /// <summary>Builds a reusable view through its target backend.</summary>
    type View = delegate of Backend -> unit

    let inline run (build: View) (backend: Backend) =
        build.Invoke backend

    /// <summary>Builds fixed text.</summary>
    let inline text value : View =
        View (fun backend -> backend.Text value)

    /// <summary>Builds children in array order.</summary>
    let inline fragment (children: View array) : View =
        View (fun backend ->
            for child in children do
                child.Invoke backend)

    /// <summary>Builds an element with its modifier and child arrays.</summary>
    /// <summary>Presents views as backend actions; Fable reuses the erased view array.</summary>
    let childActions (children: View array) : (Backend -> unit) array =
#if FABLE_COMPILER
        unbox children
#else
        children |> Array.map (fun child -> child.Invoke)
#endif

    let inline element tag (modifiers: ModifierAction array) (children: View array) : View =
        View (fun backend -> backend.ElementWith (tag, modifiers, childActions children))

    /// <summary>Builds text from a tracked reader.</summary>
    let inline bind equals read : View =
        View (fun backend -> backend.Bind (equals, read))

    /// <summary>Runs setup once for each mounted component instance.</summary>
    let inline createComponent (setup: Setup -> View) : View =
        View (fun backend -> backend.Component (fun context -> (setup context).Invoke backend))

    let ops =
        {
            Empty = View ignore
            Text = text
            Fragment = fragment
            Element = element
            Bind = bind
            Keyed = fun equals read render -> View (fun backend -> backend.Keyed (equals, read, fun current target -> (render current).Invoke target))
            Component = createComponent
        }

let private validNameCharacters underscore (value: string) =
    let mutable valid = true
    let mutable index = 0

    while valid && index < value.Length do
        let c = value[index]

        valid <-
            (c >= 'a' && c <= 'z')
            || (c >= '0' && c <= '9')
            || c = '-'
            || (underscore && c = '_')

        index <- index + 1

    valid

let name =
    function
    | Div -> "div"
    | Button -> "button"
    | Input -> "input"
    | Select -> "select"
    | Option -> "option"
    | HtmlTag value ->
        if
            System.String.IsNullOrEmpty value
            || value[0] < 'a'
            || value[0] > 'z'
            || not (validNameCharacters false value)
        then
            invalidArg "tag" "HTML tag names must contain lowercase letters, digits or hyphens"

        value

let isVoid tag =
    match name tag with
    | "area"
    | "base"
    | "br"
    | "col"
    | "embed"
    | "hr"
    | "img"
    | "input"
    | "link"
    | "meta"
    | "param"
    | "source"
    | "track"
    | "wbr" -> true
    | _ -> false

let validateAttribute name =
    if
        System.String.IsNullOrEmpty name
        || name[0] < 'a'
        || name[0] > 'z'
        || name.StartsWith ("on", System.StringComparison.Ordinal)
        || name = "srcdoc"
        || not (validNameCharacters true name)
    then
        raise (System.ArgumentException ("Attribute names must be lowercase and cannot contain event handlers or raw document content", "name"))

module Html =
    let private escape attribute (value: string) =
        let escaped =
            value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\r", "&#13;")

        if attribute then
            escaped.Replace ("\"", "&quot;")
        else
            escaped

    let render policy build =
        let output = System.Text.StringBuilder ()
        let mutable leadingLineFeed = false

        let mutable attributeTarget: System.Collections.Generic.Dictionary<string, string> =
            Unchecked.defaultof<_>

        let mutable attributeKeys: ResizeArray<string> = Unchecked.defaultof<_>
        let mutable attributeTag = Div

        let ensureAttributes () =
            if isNull (box attributeTarget) then
                invalidOp "Element modifiers require an active element"

        let set key value =
            ensureAttributes ()

            if not (attributeKeys.Contains key) then
                attributeKeys.Add key

            attributeTarget[key] <- value

        let setOptional key value =
            ensureAttributes ()
            validateAttribute key

            match value with
            | Some value -> set key value
            | None ->
                attributeTarget.Remove key |> ignore
                attributeKeys.Remove key |> ignore

        let applyModifiers (backend: Backend) tag attributes keys modifiers =
            let previousTarget = attributeTarget
            let previousKeys = attributeKeys
            let previousTag = attributeTag
            attributeTarget <- attributes
            attributeKeys <- keys
            attributeTag <- tag

            try
                for apply in modifiers do
                    apply backend
            finally
                attributeTarget <- previousTarget
                attributeKeys <- previousKeys
                attributeTag <- previousTag

        let browserCapability () =
            ensureAttributes ()

            if policy = Reject then
                invalidOp "Browser capability requires explicit Omit policy"

        let append (value: string) =
            if leadingLineFeed && value.Length > 0 then
                leadingLineFeed <- false

                if value[0] = '\n' then
                    output.Append '\n' |> ignore

            output.Append value |> ignore

        let backend =
            { new Backend() with
                member _.Text value =
                    append (escape false value)

                member _.Bind(_, read) =
                    append (escape false (read ()))

                member this.Keyed(equals, read, render) =
                    let items = Array.copy (read ())
                    Keyed.validateUniqueKeys equals items

                    for _, item in items do
                        render (fun () -> item) this

                member _.Attribute(key, value) =
                    validateAttribute key
                    set key value

                member _.OptionalAttribute(key, value) =
                    setOptional key value

                member _.ReactiveAttribute(key, read) =
                    ensureAttributes ()
                    validateAttribute key
                    setOptional key (read ())

                member _.InputValue value =
                    ensureAttributes ()

                    if attributeTag <> Input then
                        invalidArg "modifiers" "Input value requires input"

                    set "value" value

                member _.Browser _ =
                    browserCapability ()

                member _.Listen(_, _) =
                    browserCapability ()

                member _.Property _ =
                    browserCapability ()

                member _.ReactiveProperty(_, _) =
                    browserCapability ()

                member this.Element(tag, modifiers, children) =
                    if
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
                    then
                        raise (System.InvalidOperationException "Raw-text elements require an explicit HTML serialization policy")

                    append ("<" + name tag)
                    let attributes = System.Collections.Generic.Dictionary<string, string>()
                    let keys = ResizeArray<string>()

                    applyModifiers this tag attributes keys modifiers

                    for key in keys do
                        match attributes.TryGetValue key with
                        | false, _ -> ()
                        | true, value -> append (" " + key + "=\"" + escape true value + "\"")

                    append ">"

                    if not (isVoid tag) then
                        leadingLineFeed <-
                            name tag = "pre"
                            || name tag = "textarea"
                            || name tag = "listing"

                        children ()
                        append ("</" + name tag + ">")

                member _.Component setup =
                    let cleanups = ResizeArray<unit -> unit>()
                    let mutable completed = false

                    let onCleanup callback =
                        if completed then
                            raise (System.InvalidOperationException "Server component setup is completed")

                        cleanups.Add callback

                    let mutable setupFailure: exn option = None

                    try
                        setup { OnCleanup = onCleanup }
                    with error ->
                        setupFailure <- Some error

                    completed <- true
                    let errors = ResizeArray<exn>()

                    for cleanup in cleanups do
                        try
                            cleanup ()
                        with error ->
                            errors.Add error

                    match setupFailure, errors.Count with
                    | None, 0 -> ()
                    | Some error, 0 -> raise error
                    | Some error, _ ->
                        errors.Insert (0, error)
                        raise (System.AggregateException errors)
                    | None, _ -> raise (System.AggregateException errors)
            }

        build backend
        output.ToString ()

type FragmentBuilder<'view>(ops: Operations<'view>) =
    member _.Yield(view: 'view) =
        [| view |]

    member _.YieldFrom(views: 'view array) = views
    member _.Zero() = [||]

    member _.Combine(left, right) =
        Array.append left right

    member _.Delay(body: unit -> 'view array) = body ()

    member _.Run(children) =
        ops.Fragment children

let functionSugar (ops: Operations<'view>) text =
    ops.Element Div [| Attribute ("title", "sample") |] [| ops.Text text |]
