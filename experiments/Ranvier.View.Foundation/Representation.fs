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
    abstract Component: (Setup -> unit) -> unit

type Operations<'view> =
    {
        Empty: 'view
        Text: string -> 'view
        Fragment: 'view list -> 'view
        Element: Tag -> Token list -> 'view list -> 'view
        Bind: (string -> string -> bool) -> (unit -> string) -> 'view
        Component: (Setup -> 'view) -> 'view
    }

module Tree =
    type View =
        private
        | Empty
        | Text of string
        | Fragment of View list
        | Element of Tag * Token list * View list
        | Bind of (string -> string -> bool) * (unit -> string)
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
        | Component setup -> backend.Component (fun context -> run (setup context) backend)

    let ops =
        {
            Empty = Empty
            Text = Text
            Fragment = Fragment
            Element = fun tag tokens children -> Element (tag, tokens, children)
            Bind = fun equals read -> Bind (equals, read)
            Component = Component
        }

module Program =
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

                member _.Element(tag, tokens, children) =
                    append ("<" + name tag)

                    for token in tokens do
                        match token with
                        | Attribute (key, value) ->
                            validateAttribute key
                            append (" " + key + "=\"" + escape true value + "\"")
                        | InputValue value ->
                            if tag <> Input then
                                invalidArg "tokens" "Input value requires input"

                            append (" value=\"" + escape true value + "\"")
                        | Browser _ ->
                            if policy = Reject then
                                invalidOp "Browser capability requires explicit Omit policy"

                    append ">"

                    if tag <> Input then
                        children ()
                        append ("</" + name tag + ">")

                member _.Component setup =
                    let cleanups = ResizeArray<unit -> unit>()

                    try
                        setup { OnCleanup = cleanups.Add }
                    finally
                        let errors = ResizeArray<exn>()

                        for cleanup in cleanups do
                            try
                                cleanup ()
                            with error ->
                                errors.Add error

                        if errors.Count > 0 then
                            raise (System.AggregateException (errors))
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
