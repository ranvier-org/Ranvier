namespace Ranvier.View.Foundation.Sugar

open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template

/// <summary>Opt-in template sugar: element computation expressions and control flow with inline definitions.</summary>
[<AutoOpen>]
module Builders =
    /// <summary>An element's children, built with its template; <c>Run</c> returns the element.</summary>
    /// <remarks>
    /// A string yields static text, an <c>'inst -> string</c> a text slot and a <c>Source</c> a direct text slot, as the
    /// three <c>t.text</c> overloads do. A yielded lambda infers its parameter type from its own body; write
    /// <c>t.text</c> when the body needs the instance type.
    /// </remarks>
    [<Sealed>]
    type ElementBuilder<'native, 'inst> internal (tag: Tag, parts: Part<'native, 'inst> array) =
        member _.Yield(view: TemplateView<'inst>) : TemplateView<'inst> array =
            [| view |]

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

    type TemplateBuilder<'inst> with
        /// <summary>An arm definition whose instance is its props.</summary>
        member _.armOf(build: TemplateBuilder<unit -> 'payload> -> TemplateView<unit -> 'payload>) : Component<unit -> 'payload> =
            Component.define (fun _ payload -> payload) build

        /// <summary>One row per item; a row reads its current item, and items with equal keys keep their row.</summary>
        member t.forEach<'item, 'key when 'key: equality>
            (key: 'item -> 'key)
            (items: 'inst -> 'item array)
            (row: TemplateBuilder<unit -> 'item> -> TemplateView<unit -> 'item>)
            : TemplateView<'inst> =
            t.each key items (Component.define (fun _ (current: unit -> 'item) -> current) row)

        /// <summary>One row per item; a row's instance is the item it was created for, and items with equal keys keep their row.</summary>
        member t.forEachFixed<'item, 'key when 'key: equality>
            (key: 'item -> 'key)
            (items: 'inst -> 'item array)
            (row: TemplateBuilder<'item> -> TemplateView<'item>)
            : TemplateView<'inst> =
            t.each key items (Component.define (fun _ (current: unit -> 'item) -> current ()) row)

        /// <summary>Renders <c>content</c> while <c>condition</c> holds; a change to false runs its cleanups and removes its nodes.</summary>
        member t.show (condition: 'inst -> bool) (content: TemplateBuilder<'inst> -> TemplateView<'inst>) : TemplateView<'inst> =
            let shown = Component.define (fun _ (current: unit -> 'inst) -> current ()) content
            let hidden = emptyArm ()

            t.matchWith (fun instance -> if condition instance then ValueSome instance else ValueNone) (function
                | ValueSome instance -> t.arm shown instance
                | ValueNone -> t.arm hidden ())

        /// <summary>Renders <c>content</c> while <c>condition</c> holds and <c>fallback</c> otherwise; each change of the condition remounts.</summary>
        member t.showElse
            (condition: 'inst -> bool)
            (content: TemplateBuilder<'inst> -> TemplateView<'inst>)
            (fallback: TemplateBuilder<'inst> -> TemplateView<'inst>)
            : TemplateView<'inst> =
            let shown = Component.define (fun _ (current: unit -> 'inst) -> current ()) content
            let other = Component.define (fun _ (current: unit -> 'inst) -> current ()) fallback

            t.matchWith
                (fun instance ->
                    if condition instance then
                        Choice1Of2 instance
                    else
                        Choice2Of2 instance)
                (function
                 | Choice1Of2 instance -> t.arm shown instance
                 | Choice2Of2 instance -> t.arm other instance)

    /// <summary>A void element: the element view, with no children.</summary>
    [<AbstractClass>]
    type VoidElement<'inst> internal (view: TemplateView<'inst>) =
        member internal _.View = view

    /// <summary>An element's children, built with its template; <c>Run</c> returns the element.</summary>
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

    module Component =
        /// <summary>Defines a component from per-instance setup and its template, built once.</summary>
        let template (setup: Setup -> 'props -> 'inst) (view: TemplateView<'inst>) : Component<'props> =
            Component.define setup (fun _ -> view)
