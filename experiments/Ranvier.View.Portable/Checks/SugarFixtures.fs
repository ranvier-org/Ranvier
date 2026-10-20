/// <summary>Explicit and sugared twins of each sugar form, shared by the .NET and Fable checks.</summary>
module SugarFixtures

open System
open global.Ranvier
open Fable.Core.TS
open Ranvier.View.Foundation
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template
open Ranvier.View.Foundation.Sugar

let private slotText (slot: Slot) =
    let target =
        match slot.Target with
        | AttributeTarget name -> "attr:" + name
        | PropertyTarget _ -> "prop"
        | TextTarget -> "text"
        | ContentTarget -> "content"

    let mode =
        match slot.Mode with
        | RenderMode -> "render"
        | IsolatedMode -> "isolated"
        | DirectMode -> "direct"

    target + "/" + mode

/// <summary>The skeleton layout of <c>root</c> as text: markup, root count, owners, inlined children and each target with its path and kind; item-row regions include their row's signature.</summary>
let rec layoutSignature (root: TemplateNode) : string =
    let layout = skeletonLayout root

    let describe (node: TemplateNode) =
        match node with
        | ElementNode (tag, attributes, properties, slots, events, placed, _) ->
            sprintf
                "element %s attrs=%A props=%d slots=%A events=%A placed=%d"
                (name tag)
                attributes
                properties.Length
                (Array.map slotText slots)
                (Array.map fst events)
                placed.Length
        | TextSlotNode slot -> "textslot " + slotText slot
        | RegionNode region ->
            match region.Row with
            | ItemRow (row, _) -> "region item {" + layoutSignature row.Root + "}"
            | ArmRow -> "region arm"
            | ViewRow -> "region view"
            | BoundaryRow -> "region boundary"
        | ChildNode (child, _) -> "child {" + layoutSignature child.Root + "}"
        | HeadNode head -> "head " + head.Key
        | TextNode value -> "text " + value
        | FragmentNode _ -> "fragment"

    String.concat
        "\n"
        [
            yield layout.Markup
            yield sprintf "roots=%d owners=%A children=%d" layout.RootCount layout.Owners layout.Children.Length
            for node, path in layout.Targets -> sprintf "%A %s" path (describe node)
        ]

let private omit =
    { Server.Options.defaults with
        Policy = Omit
    }

type BenchRow =
    {
        Index: int
        Source: Signal<string>
        Disabled: Signal<bool>
        Local: Signal<int>
    }

let private benchSetup (_: Setup) (index: int) =
    {
        Index = index
        Source = Api.createSignal "s<1>"
        Disabled = Api.createSignal false
        Local = Api.createSignal 7
    }

let benchExplicit =
    Component.define benchSetup (fun t ->
        t.div
            [|
                t.Attrs.id (fun (r: BenchRow) -> "row-" + string r.Index)
                t.Attrs.className "panel"
                t.Attrs.title (t.from (fun (r: BenchRow) -> r.Source))
            |]
            [|
                t.input [| t.Props.Input.value (t.from (fun (r: BenchRow) -> r.Source)) |]
                t.button
                    [|
                        t.Props.Button.disabled (t.from (fun (r: BenchRow) -> r.Disabled))
                        t.Events.onClick (fun (r: BenchRow) _ -> r.Local.Value <- r.Local.Value + 1)
                    |]
                    [| t.text (fun (r: BenchRow) -> string r.Local.Value) |]
            |])

let benchSugar =
    Component.define benchSetup (fun t ->
        t.Div (
            t.Attrs.id (fun (r: BenchRow) -> "row-" + string r.Index),
            t.Attrs.className "panel",
            t.Attrs.title (t.from (fun (r: BenchRow) -> r.Source))
        ) {
            t.Input (t.Props.Input.value (t.from (fun (r: BenchRow) -> r.Source)))

            t.Button (
                t.Props.Button.disabled (t.from (fun (r: BenchRow) -> r.Disabled)),
                t.Events.onClick (fun (r: BenchRow) _ -> r.Local.Value <- r.Local.Value + 1)
            ) {
                fun r -> string r.Local.Value
            }
        })

type Texts = { Name: Signal<string> }

let textsExplicit =
    Component.define (fun _ (name: Signal<string>) -> { Name = name }) (fun t ->
        t.div
            [||]
            [|
                t.text "Hello "
                t.text (fun (r: Texts) -> r.Name.Value)
                t.text (t.from (fun (r: Texts) -> r.Name))
                t.br [||]
                t.span [||] [||]
            |])

let textsSugar =
    Component.define (fun _ (name: Signal<string>) -> { Name = name }) (fun t ->
        t.Div () {
            "Hello "
            fun (r: Texts) -> r.Name.Value
            t.from (fun (r: Texts) -> r.Name)
            t.Br ()
            t.Span () { }
        })

let private flags = [ "a"; "b" ]

let semantics =
    Component.define (fun _ () -> ()) (fun t ->
        t.Div () {
            "start"

            for flag in flags do
                t.Span (t.Attrs.className flag) { flag }

            if flags.Length = 2 then
                "wide"

            yield! [| t.text "spliced"; t.text "two" |]
            t.Span () { () }
            t.Span () { }
        })

let semanticsMarkup =
    "<div>start<span class=\"a\">a</span><span class=\"b\">b</span>wide<!--!-->spliced<!--!-->two<span></span><span></span></div>"

/// <summary>A body lambda that ignores its parameter.</summary>
let ignoredParameter =
    Component.define (fun _ () -> ()) (fun t -> t.Span () { fun _ -> "x" })

let literalText = Component.define (fun _ () -> ()) (fun t -> t.Span () { "x" })

let private definitionGraph = new Graph ()
let definitionCounter = definitionGraph.Run (fun () -> Api.createSignal 1)

/// <summary>A body line that reads a module-level signal while the definition is built.</summary>
let definitionTimeRead =
    Component.define (fun _ () -> ()) (fun t -> t.Span () { string definitionCounter.Value })

let elementPairs: (string * TemplateComponent * TemplateComponent) array =
    [|
        "benchmark row", benchExplicit, benchSugar
        "text forms and void tags", textsExplicit, textsSugar
    |]

let elementServerPairs () : (string * string * string) array =
    let graph = new Graph ()
    let name = graph.Run (fun () -> Api.createSignal "<Ada>")

    [|
        "benchmark row", Server.render omit benchExplicit 3, Server.render omit benchSugar 3
        "text forms and void tags", Server.render omit textsExplicit name, Server.render omit textsSugar name
    |]
