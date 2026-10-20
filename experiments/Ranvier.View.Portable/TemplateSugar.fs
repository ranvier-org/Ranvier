namespace Ranvier.View.Foundation.Sugar

open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template

/// <summary>Opt-in template sugar: free tags, control flow over views and component declarations.</summary>
/// <remarks>
/// Two scope rules apply to a file that opens the sugar. Opening <c>Generated.Html.Authoring</c> in the same file makes
/// one of the two <c>Attrs</c> and <c>Props</c> vocabularies unreachable by its short name, so the sugar and
/// <c>Authoring</c> belong in separate files. A local value named like a tag shadows the tag: <c>let p = "note"</c>
/// followed by <c>p () { "x" }</c> fails with FS0003.
/// </remarks>
[<AutoOpen>]
module Builders =
    [<Sealed>]
    type private EmptyArm private () =
        static let gate = obj ()
        static let mutable shared: Component<unit -> unit> = Unchecked.defaultof<_>

        static member Get() =
            // Reads shared without the lock; null re-checks under the lock.
            if isNull (box shared) then
#if FABLE_COMPILER
                shared <- Component.define (fun _ (_: unit -> unit) -> ()) (fun t -> t.fragment [||])
#else
                lock gate (fun () ->
                    if isNull (box shared) then
                        shared <- Component.define (fun _ (_: unit -> unit) -> ()) (fun t -> t.fragment [||]))
#endif
            shared

    /// <summary>The zero-root arm every <c>show</c> selects while its condition is false, created on first use.</summary>
    let internal emptyArm () : Component<unit -> unit> =
        EmptyArm.Get ()

    /// <summary>A void element, accepted as a child by <c>Element</c>.</summary>
    [<AbstractClass>]
    type VoidElement<'inst> internal (view: TemplateView<'inst>) =
        member internal _.View = view

    /// <summary>The computation expression for an element's children; <c>Run</c> returns the element.</summary>
    /// <remarks>
    /// A string yields static text, an <c>'inst -> string</c> a text slot, a <c>Source</c> a direct text slot, and a
    /// void element its view. A yielded lambda gets its instance type from its own body.
    /// </remarks>
    [<AbstractClass>]
    type Element<'native, 'inst> internal (tag: Tag, parts: Part<'native, 'inst> array) =
        member _.Yield(view: TemplateView<'inst>) : TemplateView<'inst> array =
            [| view |]

        member _.Yield(element: VoidElement<'inst>) : TemplateView<'inst> array =
            [| element.View |]

        member _.Yield(value: string) : TemplateView<'inst> array =
            [| TemplateParts.text value |]

        member _.Yield(read: 'inst -> string) : TemplateView<'inst> array =
            [| TemplateParts.readText read |]

        member _.Yield(source: Source<'inst, string>) : TemplateView<'inst> array =
            [| TemplateParts.sourceText source |]

        member _.YieldFrom(views: TemplateView<'inst> array) : TemplateView<'inst> array = views
        member _.Zero() : TemplateView<'inst> array = [||]

        member _.Combine(first: TemplateView<'inst> array, rest: TemplateView<'inst> array) : TemplateView<'inst> array =
            Array.append first rest

        member _.Delay(body: unit -> TemplateView<'inst> array) : TemplateView<'inst> array = body ()

        /// <summary>Repeats the body for each item while the template is built; the result is static structure.</summary>
        member _.For(items: seq<'item>, body: 'item -> TemplateView<'inst> array) : TemplateView<'inst> array =
            items |> Seq.collect body |> Array.ofSeq

        member _.Run(children: TemplateView<'inst> array) : TemplateView<'inst> =
            TemplateParts.element tag parts children

    let Attrs<'inst> : AttributeBuilder<'inst> = AttributeBuilder<'inst>()
    let Props<'inst> : PropertyBuilder<'inst> = PropertyBuilder<'inst>()
    let Events<'inst> : EventBuilder<'inst> = EventBuilder<'inst>()
    let Hooks<'inst> : HookBuilder<'inst> = HookBuilder<'inst>()

    [<System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)>]
    let sourceOf<'inst, 'value> (read: System.Func<obj, obj>) : Source<'inst, 'value> =
        Source read

    /// <summary>A source over the signal or memo returned by <c>accessor</c>.</summary>
    let inline from<'inst, ^s, 'value when ^s: (member Value: 'value)> ([<InlineIfLambda>] accessor: 'inst -> ^s) : Source<'inst, 'value> =
        sourceOf (System.Func<obj, obj>(fun instance -> box ((^s: (member Value: 'value) (accessor (unbox<'inst> instance))))))

    /// <summary>A text slot; the same node as a yielded reader.</summary>
    let text (read: 'inst -> string) : TemplateView<'inst> =
        TemplateParts.readText read

    let fragment (children: TemplateView<'inst> array) : TemplateView<'inst> =
        TemplateBuilder<'inst>().fragment children

    /// <summary>An arm definition whose instance is its props.</summary>
    let armOf (view: TemplateView<unit -> 'payload>) : Component<unit -> 'payload> =
        Component.define (fun _ payload -> payload) (fun _ -> view)

    /// <summary>An arm that renders <c>definition</c>; its props read the payload of the arm currently selected.</summary>
    let arm (definition: Component<unit -> 'payload>) (payload: 'payload) : Arm =
        TemplateParts.arm definition payload

    let matchWith (read: 'inst -> 'union) (arms: 'union -> Arm) : TemplateView<'inst> =
        TemplateBuilder<'inst>().matchWith read arms

    let branch<'inst, 'key when 'key: equality> (key: 'inst -> 'key) (arms: 'key -> Arm) : TemplateView<'inst> =
        TemplateBuilder<'inst>().branch key arms

    let each<'inst, 'item, 'key when 'key: equality>
        (key: 'item -> 'key)
        (items: 'inst -> 'item array)
        (row: Component<unit -> 'item>)
        : TemplateView<'inst> =
        TemplateBuilder<'inst>().each key items row

    /// <summary>One row per item; a row reads its current item, and items with equal keys keep their row.</summary>
    let forEach<'inst, 'item, 'key when 'key: equality>
        (key: 'item -> 'key)
        (items: 'inst -> 'item array)
        (row: TemplateView<unit -> 'item>)
        : TemplateView<'inst> =
        each key items (Component.define (fun _ (current: unit -> 'item) -> current) (fun _ -> row))

    /// <summary>One row per item; a row's instance is the item it was created for, and items with equal keys keep their row.</summary>
    let forEachFixed<'inst, 'item, 'key when 'key: equality>
        (key: 'item -> 'key)
        (items: 'inst -> 'item array)
        (row: TemplateView<'item>)
        : TemplateView<'inst> =
        each key items (Component.define (fun _ (current: unit -> 'item) -> current ()) (fun _ -> row))

    /// <summary>Renders <c>content</c> while <c>condition</c> holds; a change to false runs its cleanups and removes its nodes.</summary>
    let show (condition: 'inst -> bool) (content: TemplateView<'inst>) : TemplateView<'inst> =
        let shown =
            Component.define (fun _ (current: unit -> 'inst) -> current ()) (fun _ -> content)

        let hidden = emptyArm ()

        matchWith (fun instance -> if condition instance then ValueSome instance else ValueNone) (function
            | ValueSome instance -> arm shown instance
            | ValueNone -> arm hidden ())

    /// <summary>Renders <c>content</c> while <c>condition</c> holds and <c>fallback</c> otherwise; each change of the condition remounts.</summary>
    let showElse (condition: 'inst -> bool) (content: TemplateView<'inst>) (fallback: TemplateView<'inst>) : TemplateView<'inst> =
        let shown =
            Component.define (fun _ (current: unit -> 'inst) -> current ()) (fun _ -> content)

        let other =
            Component.define (fun _ (current: unit -> 'inst) -> current ()) (fun _ -> fallback)

        matchWith
            (fun instance ->
                if condition instance then
                    Choice1Of2 instance
                else
                    Choice2Of2 instance)
            (function
             | Choice1Of2 instance -> arm shown instance
             | Choice2Of2 instance -> arm other instance)

    let boundary (pending: Component<unit>) (failed: Component<BoundaryFailure>) (content: Component<BoundaryStatus>) : TemplateView<'inst> =
        TemplateBuilder<'inst>().boundary pending failed content

    /// <summary>Inlines a child template; its props are read from the instance, untracked, once per instance.</summary>
    let render (definition: Component<'props>) (props: 'inst -> 'props) : TemplateView<'inst> =
        TemplateBuilder<'inst>().render definition props

    /// <summary>Inlines a child template with fixed props.</summary>
    let renderOf (definition: Component<'props>) (props: 'props) : TemplateView<'inst> =
        TemplateBuilder<'inst>().render definition (fun _ -> props)

    /// <summary>A component used as a tag: yields the child view of <c>definition</c>.</summary>
    /// <remarks>Subclasses take a private primary constructor and public overloads.</remarks>
    [<AbstractClass>]
    type ComponentTag<'props, 'inst>(definition: Component<'props>, props: 'inst -> 'props) =
        inherit VoidElement<'inst>(TemplateBuilder<'inst>().render definition props)

    module Component =
        /// <summary>Defines a component from per-instance setup and its template, built once.</summary>
        let template (setup: Setup -> 'props -> 'inst) (view: TemplateView<'inst>) : Component<'props> =
            Component.define setup (fun _ -> view)

        /// <summary>A component from the type's static <c>Create</c> (setup) and <c>View</c> (template, read once).</summary>
        let inline ofMembers< ^T, 'props when ^T: (static member Create: Setup * 'props -> ^T) and ^T: (static member View: TemplateView< ^T >)>
            ()
            : Component<'props> =
            Component.define (fun setup props -> (^T: (static member Create: Setup * 'props -> ^T) (setup, props))) (fun _ ->
                (^T: (static member View: TemplateView< ^T >) ()))

        /// <summary>A component from a constructor (setup) and the type's static <c>View</c> (template, read once).</summary>
        let inline ofClass ([<InlineIfLambda>] create: Setup * 'props -> ^T) : Component<'props> =
            Component.define (fun setup props -> create (setup, props)) (fun _ -> (^T: (static member View: TemplateView< ^T >) ()))
