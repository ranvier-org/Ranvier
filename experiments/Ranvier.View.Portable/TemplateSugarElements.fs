namespace Ranvier.View.Foundation.Sugar

open System
open Fable.Core.TS
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template

[<AutoOpen>]
module Elements =
    type TemplateBuilder<'inst> with
        member _.A([<ParamArray>] parts: Part<Dom.HTMLAnchorElement, 'inst> array) : ElementBuilder<Dom.HTMLAnchorElement, 'inst> =
            ElementBuilder<Dom.HTMLAnchorElement, 'inst>(HtmlTag "a", parts)

        member _.B([<ParamArray>] parts: Part<Dom.HTMLElement, 'inst> array) : ElementBuilder<Dom.HTMLElement, 'inst> =
            ElementBuilder<Dom.HTMLElement, 'inst>(HtmlTag "b", parts)

        member _.Br([<ParamArray>] parts: Part<Dom.HTMLBRElement, 'inst> array) : TemplateView<'inst> =
            TemplateParts.element (HtmlTag "br") parts [||]

        member _.Button([<ParamArray>] parts: Part<Dom.HTMLButtonElement, 'inst> array) : ElementBuilder<Dom.HTMLButtonElement, 'inst> =
            ElementBuilder<Dom.HTMLButtonElement, 'inst>(Button, parts)

        member _.Div([<ParamArray>] parts: Part<Dom.HTMLDivElement, 'inst> array) : ElementBuilder<Dom.HTMLDivElement, 'inst> =
            ElementBuilder<Dom.HTMLDivElement, 'inst>(Div, parts)

        member _.Em([<ParamArray>] parts: Part<Dom.HTMLElement, 'inst> array) : ElementBuilder<Dom.HTMLElement, 'inst> =
            ElementBuilder<Dom.HTMLElement, 'inst>(HtmlTag "em", parts)

        member _.Input([<ParamArray>] parts: Part<Dom.HTMLInputElement, 'inst> array) : TemplateView<'inst> =
            TemplateParts.element Input parts [||]

        member _.Li([<ParamArray>] parts: Part<Dom.HTMLLIElement, 'inst> array) : ElementBuilder<Dom.HTMLLIElement, 'inst> =
            ElementBuilder<Dom.HTMLLIElement, 'inst>(HtmlTag "li", parts)

        member _.P([<ParamArray>] parts: Part<Dom.HTMLParagraphElement, 'inst> array) : ElementBuilder<Dom.HTMLParagraphElement, 'inst> =
            ElementBuilder<Dom.HTMLParagraphElement, 'inst>(HtmlTag "p", parts)

        member _.Span([<ParamArray>] parts: Part<Dom.HTMLSpanElement, 'inst> array) : ElementBuilder<Dom.HTMLSpanElement, 'inst> =
            ElementBuilder<Dom.HTMLSpanElement, 'inst>(HtmlTag "span", parts)

        member _.Table([<ParamArray>] parts: Part<Dom.HTMLTableElement, 'inst> array) : ElementBuilder<Dom.HTMLTableElement, 'inst> =
            ElementBuilder<Dom.HTMLTableElement, 'inst>(HtmlTag "table", parts)

        member _.Tbody([<ParamArray>] parts: Part<Dom.HTMLTableSectionElement, 'inst> array) : ElementBuilder<Dom.HTMLTableSectionElement, 'inst> =
            ElementBuilder<Dom.HTMLTableSectionElement, 'inst>(HtmlTag "tbody", parts)

        member _.Td([<ParamArray>] parts: Part<Dom.HTMLTableCellElement, 'inst> array) : ElementBuilder<Dom.HTMLTableCellElement, 'inst> =
            ElementBuilder<Dom.HTMLTableCellElement, 'inst>(HtmlTag "td", parts)

        member _.Tr([<ParamArray>] parts: Part<Dom.HTMLTableRowElement, 'inst> array) : ElementBuilder<Dom.HTMLTableRowElement, 'inst> =
            ElementBuilder<Dom.HTMLTableRowElement, 'inst>(HtmlTag "tr", parts)

        member _.Ul([<ParamArray>] parts: Part<Dom.HTMLUListElement, 'inst> array) : ElementBuilder<Dom.HTMLUListElement, 'inst> =
            ElementBuilder<Dom.HTMLUListElement, 'inst>(HtmlTag "ul", parts)
