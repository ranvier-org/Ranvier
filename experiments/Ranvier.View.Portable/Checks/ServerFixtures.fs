/// <summary>Template fixtures shared by the server checks, the hydration checks and the first-content benchmarks.</summary>
module ServerFixtures

open global.Ranvier
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template

type GreetingProps = { Title: string; Name: string }

let greeting =
    Component.define (fun _ (props: GreetingProps) -> props) (fun t ->
        t.div
            [| t.Attrs.className "panel"; t.Attrs.title (fun r -> r.Title) |]
            [| t.text "Hello "; t.text (fun r -> r.Name); t.text (fun _ -> "!") |])

let greetingProps = { Title = "T &"; Name = "<Ada>" }

let named =
    Component.define (fun _ (props: GreetingProps) -> props) (fun t ->
        t.div [| t.Attrs.className "panel"; t.Attrs.title (fun r -> r.Title) |] [| t.span [||] [| t.text (fun r -> r.Name) |] |])

let clicking =
    Component.define (fun _ (props: GreetingProps) -> props) (fun t -> t.button [| t.Events.on ("click", fun _ _ -> ()) |] [||])

let nullTitle =
    Component.define (fun _ (props: GreetingProps) -> props) (fun t -> t.div [| t.Attrs.title (fun _ -> (null: string)) |] [||])

type HoleProps = { Caption: string; Body: Program.View }

let holed =
    Component.define (fun _ (props: HoleProps) -> props) (fun t ->
        t.div [| t.Attrs.className "card" |] [| t.text (fun r -> r.Caption); t.view (fun r -> r.Body) |])

type Stage =
    | Waiting
    | Working of int
    | Complete of string

let waitingArm =
    Component.define (fun _ (_: unit -> unit) -> ()) (fun t -> t.span [||] [| t.text "waiting" |])

let workingArm =
    Component.define (fun _ (progress: unit -> int) -> progress) (fun t ->
        t.span [||] [| t.text (fun progress -> "working " + string (progress ())) |])

let completeArm =
    Component.define (fun _ (result: unit -> string) -> result) (fun t -> t.span [||] [| t.text (fun result -> "complete " + result ()) |])

let staged =
    Component.define (fun _ (stage: Stage) -> stage) (fun t ->
        t.div
            [||]
            [|
                t.matchWith id (function
                    | Waiting -> t.arm waitingArm ()
                    | Working progress -> t.arm workingArm progress
                    | Complete result -> t.arm completeArm result)
            |])

let decadeArm =
    Component.define (fun _ (decade: unit -> int) -> decade) (fun t -> t.span [||] [| t.text (fun decade -> string (decade ())) |])

let decades =
    Component.define (fun _ (value: int) -> value) (fun t ->
        t.div [||] [| t.branch (fun value -> value / 10) (fun decade -> t.arm decadeArm decade) |])

type Listed = { Id: int; Text: string }

let listedRow =
    Component.define (fun _ (item: unit -> Listed) -> item) (fun t -> t.li [||] [| t.text (fun item -> (item ()).Text) |])

let listed =
    Component.define (fun _ (items: Listed array) -> items) (fun t -> t.ul [||] [| t.each (fun (item: Listed) -> item.Id) id listedRow |])

let listedItems = [| { Id = 1; Text = "a" }; { Id = 2; Text = "<b>" } |]

type BadgeProps = { Count: int }

let badge =
    Component.define (fun _ (props: BadgeProps) -> props) (fun t ->
        t.span [| t.Attrs.title (fun badge -> string badge.Count) |] [| t.text (fun badge -> string badge.Count) |])

let carded =
    Component.define (fun _ (props: GreetingProps) -> props) (fun t ->
        t.div
            [||]
            [|
                t.text (fun r -> r.Name)
                t.render badge (fun r -> { Count = r.Title.Length })
            |])

let textual =
    Component.define (fun _ (value: string) -> value) (fun t -> t.text (fun value -> value))

let besideText =
    Component.define (fun _ (props: GreetingProps) -> props) (fun t -> t.span [||] [| t.text "a"; t.render textual (fun r -> r.Name) |])

type Flags =
    {
        Name: string
        Nickname: string option
        Busy: bool
        Expanded: bool option
        State: string
    }

let flagsTemplate =
    Component.define (fun _ (props: Flags) -> props) (fun t ->
        t.button
            [|
                t.Attrs.title (fun r -> r.Name)
                t.Attrs.id (fun r -> r.Nickname)
                t.Attrs.Button.disabled (fun r -> r.Busy)
                t.Attrs.ariaExpanded (fun r -> r.Expanded)
                t.Attrs.ariaBusy (fun r -> r.Busy)
                t.Attrs.data ("state", fun r -> Some r.State)
            |]
            [| t.text "go" |])

let flagCases =
    [
        {
            Name = "a"
            Nickname = None
            Busy = false
            Expanded = None
            State = "x"
        }
        {
            Name = "b"
            Nickname = Some "n"
            Busy = true
            Expanded = Some false
            State = "y"
        }
        {
            Name = "c"
            Nickname = Some "m"
            Busy = false
            Expanded = Some true
            State = "z"
        }
        {
            Name = null
            Nickname = None
            Busy = false
            Expanded = None
            State = "n"
        }
    ]

let staticTemplate =
    Component.define (fun _ () -> ()) (fun t ->
        t.div
            [||]
            [|
                t.button
                    [|
                        t.Attrs.Button.disabled false
                        t.Attrs.ariaExpanded false
                        t.Attrs.id (None: string option)
                        t.Attrs.title "t"
                    |]
                    [||]
                t.button
                    [|
                        t.Attrs.Button.disabled true
                        t.Attrs.ariaExpanded true
                        t.Attrs.id (Some "i")
                        t.Attrs.data ("k", "v")
                    |]
                    [||]
            |])

let pair =
    Component.define (fun _ (props: GreetingProps) -> props) (fun t ->
        t.fragment
            [|
                t.div
                    [| t.Attrs.className "panel"; t.Attrs.title (fun r -> r.Title) |]
                    [| t.text "Hello "; t.text (fun r -> r.Name); t.text (fun _ -> "!") |]
                t.span [||] [| t.text (fun r -> r.Name) |]
            |])

/// <summary>The source the <c>guarded</c> fixture's content reads; assign it inside a graph before rendering.</summary>
let mutable boundarySource: AsyncSource<string> = Unchecked.defaultof<_>

let boundaryPending =
    Component.define (fun _ () -> ()) (fun t -> t.span [||] [| t.text "loading" |])

let boundaryFailed =
    Component.define (fun _ (failure: BoundaryFailure) -> failure) (fun t -> t.span [||] [| t.text (fun failure -> failure.Error.Message) |])

let boundaryContent =
    Component.define (fun _ (status: BoundaryStatus) -> status) (fun t -> t.span [||] [| t.text (fun _ -> boundarySource.Value) |])

let guarded =
    Component.define (fun _ () -> ()) (fun t -> t.div [||] [| t.boundary boundaryPending boundaryFailed boundaryContent |])

let emptyText =
    Component.define (fun _ (value: string) -> value) (fun t -> t.span [||] [| t.text (fun value -> value) |])

let voidChildren =
    Component.define (fun _ () -> ()) (fun t -> t.div [||] [| t.element (HtmlTag "br") [||] [| t.text "x" |] |])

let rowInTable =
    Component.define (fun _ () -> ()) (fun t -> t.table [||] [| t.tr [||] [| t.td [||] [| t.text "x" |] |] |])

type ComponentLine =
    {
        Index: int
        Label: Signal<string>
        Clicks: Signal<int>
    }

/// <summary>The row of the framework comparison's component workload.</summary>
let componentRow =
    Component.define
        (fun _ (item: unit -> int) ->
            let index = item ()

            {
                Index = index
                Label = Api.createSignal ("row " + string index)
                Clicks = Api.createSignal 0
            })
        (fun t ->
            t.div
                [|
                    t.Attrs.id (fun r -> "row-" + string r.Index)
                    t.Attrs.className "panel"
                    t.Attrs.title (t.from (fun r -> r.Label))
                |]
                [|
                    t.input [| t.Props.Input.value (t.from (fun r -> r.Label)) |]
                    t.button [| t.Events.onClick (fun r _ -> r.Clicks.Value <- r.Clicks.Value + 1) |] [| t.text (fun r -> string r.Clicks.Value) |]
                |])

let componentPage =
    Component.define (fun _ (rows: int) -> Array.init rows (fun index -> index + 1)) (fun t ->
        t.element (HtmlTag "section") [||] [| t.each id id componentRow |])

type KeyedLine = { Id: int; Label: string }

/// <summary>The row of the js-framework-benchmark keyed workload.</summary>
let keyedRow =
    Component.define (fun _ (item: unit -> KeyedLine) -> item ()) (fun t ->
        t.tr
            [||]
            [|
                t.td [| t.Attrs.className "col-md-1" |] [| t.text (fun (row: KeyedLine) -> string row.Id) |]
                t.td
                    [| t.Attrs.className "col-md-4" |]
                    [|
                        t.a [| t.Events.onClick (fun _ _ -> ()) |] [| t.text (fun (row: KeyedLine) -> row.Label) |]
                    |]
                t.td
                    [| t.Attrs.className "col-md-1" |]
                    [|
                        t.a
                            [| t.Events.onClick (fun _ _ -> ()) |]
                            [|
                                t.span
                                    [|
                                        t.Attrs.className "glyphicon glyphicon-remove"
                                        t.Attrs.attribute ("aria-hidden", "true")
                                    |]
                                    [||]
                            |]
                    |]
                t.td [| t.Attrs.className "col-md-6" |] [||]
            |])

let keyedPage =
    Component.define
        (fun _ (rows: int) ->
            Array.init rows (fun index ->
                {
                    Id = index + 1
                    Label = "row " + string (index + 1)
                }))
        (fun t -> t.table [| t.Attrs.className "table" |] [| t.tbody [||] [| t.each (fun (row: KeyedLine) -> row.Id) id keyedRow |] |])

let renderOmit (definition: Component<'props>) (props: 'props) =
    Html.render Omit (Program.run (Component.render definition props))

/// <summary>The first content of the component workload at <c>rows</c> rows, rendered as one request.</summary>
let renderComponentPage (rows: int) : string =
    Server.render
        { Server.Options.defaults with
            Policy = Omit
        }
        componentPage
        rows

/// <summary>The first content of the keyed workload at <c>rows</c> rows, rendered as one request.</summary>
let renderKeyedPage (rows: int) : string =
    Server.render
        { Server.Options.defaults with
            Policy = Omit
        }
        keyedPage
        rows
