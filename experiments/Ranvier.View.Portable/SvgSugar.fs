namespace Ranvier.View.Foundation.Sugar

open System
open Fable.Core.TS
open Ranvier.View.Foundation.Representation
open Ranvier.View.Foundation.Template

/// <summary>SVG tags. Each creates its element, and every element under it, in the SVG namespace on every mount path.</summary>
/// <remarks>
/// The first use of an SVG tag enables SVG creation on the expanded path; bundles without an SVG tag create HTML
/// elements only.
/// </remarks>
[<AutoOpen>]
module SvgElements =
    type svg<'i> private (ps: Part<Dom.SVGSVGElement, 'i> array, primary: unit) =
        inherit Element<Dom.SVGSVGElement, 'i>((HtmlTag "svg"), ps)
        do Ranvier.View.Foundation.Departures.enableSvg ()
        new([<ParamArray>] ps: Part<Dom.SVGSVGElement, 'i> array) = svg<'i>(ps, ())

    type g<'i> private (ps: Part<Dom.SVGGElement, 'i> array, primary: unit) =
        inherit Element<Dom.SVGGElement, 'i>((HtmlTag "g"), ps)
        do Ranvier.View.Foundation.Departures.enableSvg ()
        new([<ParamArray>] ps: Part<Dom.SVGGElement, 'i> array) = g<'i>(ps, ())

    type path<'i> private (ps: Part<Dom.SVGPathElement, 'i> array, primary: unit) =
        inherit Element<Dom.SVGPathElement, 'i>((HtmlTag "path"), ps)
        do Ranvier.View.Foundation.Departures.enableSvg ()
        new([<ParamArray>] ps: Part<Dom.SVGPathElement, 'i> array) = path<'i>(ps, ())

    type circle<'i> private (ps: Part<Dom.SVGCircleElement, 'i> array, primary: unit) =
        inherit Element<Dom.SVGCircleElement, 'i>((HtmlTag "circle"), ps)
        do Ranvier.View.Foundation.Departures.enableSvg ()
        new([<ParamArray>] ps: Part<Dom.SVGCircleElement, 'i> array) = circle<'i>(ps, ())

    type ellipse<'i> private (ps: Part<Dom.SVGEllipseElement, 'i> array, primary: unit) =
        inherit Element<Dom.SVGEllipseElement, 'i>((HtmlTag "ellipse"), ps)
        do Ranvier.View.Foundation.Departures.enableSvg ()
        new([<ParamArray>] ps: Part<Dom.SVGEllipseElement, 'i> array) = ellipse<'i>(ps, ())

    type line<'i> private (ps: Part<Dom.SVGLineElement, 'i> array, primary: unit) =
        inherit Element<Dom.SVGLineElement, 'i>((HtmlTag "line"), ps)
        do Ranvier.View.Foundation.Departures.enableSvg ()
        new([<ParamArray>] ps: Part<Dom.SVGLineElement, 'i> array) = line<'i>(ps, ())

    type polyline<'i> private (ps: Part<Dom.SVGPolylineElement, 'i> array, primary: unit) =
        inherit Element<Dom.SVGPolylineElement, 'i>((HtmlTag "polyline"), ps)
        do Ranvier.View.Foundation.Departures.enableSvg ()
        new([<ParamArray>] ps: Part<Dom.SVGPolylineElement, 'i> array) = polyline<'i>(ps, ())

    type polygon<'i> private (ps: Part<Dom.SVGPolygonElement, 'i> array, primary: unit) =
        inherit Element<Dom.SVGPolygonElement, 'i>((HtmlTag "polygon"), ps)
        do Ranvier.View.Foundation.Departures.enableSvg ()
        new([<ParamArray>] ps: Part<Dom.SVGPolygonElement, 'i> array) = polygon<'i>(ps, ())

    type rect<'i> private (ps: Part<Dom.SVGRectElement, 'i> array, primary: unit) =
        inherit Element<Dom.SVGRectElement, 'i>((HtmlTag "rect"), ps)
        do Ranvier.View.Foundation.Departures.enableSvg ()
        new([<ParamArray>] ps: Part<Dom.SVGRectElement, 'i> array) = rect<'i>(ps, ())
