namespace Ranvier.View.Foundation.Sugar

open System
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template

/// <summary>Opt-in template sugar: element computation expressions and control flow with inline definitions.</summary>
[<AutoOpen>]
module Builders =
    /// <summary>The children of one element, built while its template is built; <c>Run</c> returns the element.</summary>
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
