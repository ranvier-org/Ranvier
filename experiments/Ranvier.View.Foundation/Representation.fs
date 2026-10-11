module Ranvier.View.Foundation.Representation

type Tag =
    | Div
    | Button
    | Input

type Token =
    | Attribute of string * string
    | InputValue of string
    | Browser of (obj -> (unit -> unit))

type BrowserPolicy =
    | Reject
    | Omit

type Setup = { OnCleanup: (unit -> unit) -> unit }

[<AbstractClass>]
type Backend() =
    abstract Text: string -> unit
    abstract Element: Tag * Token list * (unit -> unit) -> unit
    abstract Bind: (string -> string -> bool) * (unit -> string) -> unit
    /// <summary>Interprets a keyed item snapshot and renders its items into this backend.</summary>
    abstract Keyed: (obj -> obj -> bool) * (unit -> (obj * obj) list) * ((unit -> obj) -> Backend -> unit) -> unit
    abstract Component: (Setup -> unit) -> unit

type Operations<'view> =
    {
        Empty: 'view
        Text: string -> 'view
        Fragment: 'view list -> 'view
        Element: Tag -> Token list -> 'view list -> 'view
        Bind: (string -> string -> bool) -> (unit -> string) -> 'view
        /// <summary>Creates the internal keyed-list representation used by <c>Keyed.list</c>.</summary>
        Keyed: (obj -> obj -> bool) -> (unit -> (obj * obj) list) -> ((unit -> obj) -> 'view) -> 'view
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

    /// <summary>Creates a reactive keyed list that retains mounted rows while their extracted keys remain equal.</summary>
    /// <remarks><c>render</c> receives a reader for the latest item value.
    /// Duplicate keys raise <see cref="T:System.ArgumentException"/> before reconciliation.</remarks>
    let list
        (ops: Operations<'view>)
        (equals: 'key -> 'key -> bool)
        (key: 'item -> 'key)
        (read: unit -> 'item list)
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
            |> List.map (fun item -> box (key item), box item)

        let renderObject (current: unit -> obj) =
            render (fun () -> unbox<'item>(current ()))

        ops.Keyed equalsObjects readObjects renderObject

module Tree =
    type View =
        private
        | Empty
        | Text of string
        | Fragment of View list
        | Element of Tag * Token list * View list
        | Bind of (string -> string -> bool) * (unit -> string)
        | Keyed of (obj -> obj -> bool) * (unit -> (obj * obj) list) * ((unit -> obj) -> View)
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
#if FABLE_COMPILER
    [<Fable.Core.Erase>]
#endif
    type View = private View of (Backend -> unit)

    let run (View build) backend =
        build backend

    let ops =
        {
            Empty = View ignore
            Text = fun value -> View (fun backend -> backend.Text value)
            Fragment =
                fun children ->
                    View (fun backend ->
                        for child in children do
                            run child backend)
            Element =
                fun tag tokens children ->
                    View (fun backend ->
                        backend.Element (
                            tag,
                            tokens,
                            fun () ->
                                for child in children do
                                    run child backend
                        ))
            Bind = fun equals read -> View (fun backend -> backend.Bind (equals, read))
            Keyed = fun equals read render -> View (fun backend -> backend.Keyed (equals, read, fun current target -> run (render current) target))
            Component = fun setup -> View (fun backend -> backend.Component (fun context -> run (setup context) backend))
        }

let name =
    function
    | Div -> "div"
    | Button -> "button"
    | Input -> "input"

let validateAttribute name =
    if name <> "id" && name <> "title" then
        invalidArg "name" "Only id/title are supported in this experiment"

module Html =
    let private escape attribute (value: string) =
        let escaped = value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")

        if attribute then
            escaped.Replace ("\"", "&quot;")
        else
            escaped

    let render policy build =
        let output = System.Text.StringBuilder ()

        let append (value: string) =
            output.Append value |> ignore

        let backend =
            { new Backend() with
                member _.Text value =
                    append (escape false value)

                member _.Bind(_, read) =
                    append (escape false (read ()))

                member this.Keyed(equals, read, render) =
                    let items = read ()
                    Keyed.validateUniqueKeys equals items

                    for _, item in items do
                        render (fun () -> item) this

                member _.Element(tag, tokens, children) =
                    append ("<" + name tag)
                    let attributes = System.Collections.Generic.Dictionary<string, string>()
                    let keys = ResizeArray<string>()

                    let set key value =
                        if not (attributes.ContainsKey key) then
                            keys.Add key

                        attributes[key] <- value

                    for token in tokens do
                        match token with
                        | Attribute (key, value) ->
                            validateAttribute key
                            set key value
                        | InputValue value ->
                            if tag <> Input then
                                invalidArg "tokens" "Input value requires input"

                            set "value" value
                        | Browser _ ->
                            if policy = Reject then
                                invalidOp "Browser capability requires explicit Omit policy"

                    for key in keys do
                        append (
                            " "
                            + key
                            + "=\""
                            + escape true attributes[key]
                            + "\""
                        )

                    append ">"

                    if tag <> Input then
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
    member _.Yield(view: 'view) = [ view ]
    member _.YieldFrom(views: 'view list) = views
    member _.Zero() = []

    member _.Combine(left, right) =
        left @ right

    member _.Delay(body: unit -> 'view list) = body ()

    member _.Run(children) =
        ops.Fragment children

let functionSugar (ops: Operations<'view>) text =
    ops.Element Div [ Attribute ("title", "sample") ] [ ops.Text text ]
