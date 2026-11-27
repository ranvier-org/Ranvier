// Renders the Open Graph cards of the site and README: 1200 x 630 cards written to brand/og/<name>.png at 2400 x 1260.
//
// Each card is a Ranvier.View.Dsl SVG component, mounted with Ranvier.View.Dom into a headless
// Ranvier.View.WebCore host and snapshotted to PNG. Geist and Geist Mono load from Google Fonts through
// the host's resource provider. Cards with a docs page are also copied to docs/static/og/<key>.png.
//
//   dotnet fsi brand/og/render.fsx                 renders every card
//   dotnet fsi brand/og/render.fsx home async      renders the named cards

#r "nuget: Ranvier.View, 0.7.0"
#r "nuget: Ranvier.View.WebCore, 0.3.3"

open System
open System.Diagnostics
open System.Globalization
open System.IO
open System.Net.Http
open System.Text.Json
open System.Text.RegularExpressions
open System.Threading
open System.Xml.Linq
open Ranvier
open Ranvier.View
open Ranvier.View.Dsl
open Ranvier.View.Dom
open Ranvier.View.WebCore

let brandRoot = Path.GetFullPath (Path.Combine (__SOURCE_DIRECTORY__, ".."))
let repoRoot = Path.GetFullPath (Path.Combine (brandRoot, ".."))

// ---------------------------------------------------------------------------------------------------
// Brand inputs: the dark-scheme tokens, the wordmark and the state marks.

/// The dark scheme of brand/tokens/ranvier-brand-tokens.json.
module Brand =
    let private dark =
        let json =
            File.ReadAllText (Path.Combine (brandRoot, "tokens", "ranvier-brand-tokens.json"))

        JsonDocument.Parse(json).RootElement.GetProperty("themes").GetProperty "dark"

    let private color (name: string) =
        dark.GetProperty("colors").GetProperty(name).GetString()

    let canvas = color "canvas"
    let text = color "text"
    let muted = color "muted"

    /// The accent gradient's stops as (colour, offset): cyan at 0, blue at .53, violet at 1.
    let accent =
        [|
            for m in Regex.Matches (dark.GetProperty("gradients").GetProperty("accent").GetString(), "(#[0-9A-Fa-f]{6}) ([0-9.]+)%") ->
                m.Groups[1].Value.ToUpperInvariant(), float m.Groups[2].Value / 100.
        |]

    let cyan = fst accent[0]
    let blue = fst accent[1]
    let violet = fst accent[2]

    /// The dots of a pulse's trail, between the canvas and the muted text.
    let trail = "#3A4855"

    let fonts =
        "https://fonts.googleapis.com/css2?family=Geist:wght@400..700&family=Geist+Mono:wght@400..600&display=swap"

    let sans = "Geist, system-ui, sans-serif"
    let mono = "Geist Mono, ui-monospace, monospace"

// ---------------------------------------------------------------------------------------------------
// Numbers, colours and parts.

let private invariant = CultureInfo.InvariantCulture

let num (value: float) =
    value.ToString ("R", invariant)

let f0 (value: float) =
    value.ToString ("F0", invariant)

let f1 (value: float) =
    value.ToString ("F1", invariant)

let f2 (value: float) =
    value.ToString ("F2", invariant)

let f3 (value: float) =
    value.ToString ("F3", invariant)

/// The fractional part, as Python's <c>x % 1</c> takes it for negative x too.
let frac (value: float) =
    value - floor value

/// The colour <c>t</c> of the way from <c>c0</c> to <c>c1</c>, per sRGB channel.
let mix (c0: string) (c1: string) (t: float) =
    let channel (c: string) i =
        float (Convert.ToInt32 (c.Substring (1 + 2 * i, 2), 16))

    let t = min 1. (max 0. t)

    "#"
    + String.Join (
        "",
        [
            for i in 0..2 -> (int (Math.Round (channel c0 i + (channel c1 i - channel c0 i) * t))).ToString "X2"
        ]
    )

/// The brand accent gradient at <c>t</c>.
let grad (t: float) =
    let knot = snd Brand.accent[1]

    if t <= knot then
        mix Brand.cyan Brand.blue (t / knot)
    else
        mix Brand.blue Brand.violet ((t - knot) / (1. - knot))

/// An attribute part on any element: <c>circle (at "r" "4")</c> is <c>circle (circle.attribute ("r", "4"))</c>.
let at (name: string) (value: string) : Part<'native, unit> =
    TemplateParts.attribute name value

/// An SVG element with the tag <c>name</c>, for tags read from a brand SVG file.
let element (name: string) (parts: Part<Ranvier.Dom.SVGElement, unit> array) (children: TemplateView<unit> array) =
    TemplateParts.element (HtmlTag name) parts children

let group (parts: Part<Ranvier.Dom.SVGGElement, unit> array) (children: TemplateView<unit> seq) =
    g (parts) { yield! Array.ofSeq children }

/// A <c>filter</c> attribute when there is a filter.
let filterOf (filter: string option) : Part<'native, unit> array =
    filter
    |> Option.map (at "filter")
    |> Option.toArray

let dot (cx: string) (cy: string) (r: string) (parts: Part<Ranvier.Dom.SVGCircleElement, unit> list) =
    circle (Array.append [| at "cx" cx; at "cy" cy; at "r" r |] (Array.ofList parts)) { () }

let label (x: string) (y: string) (fill: string) (size: string) (parts: Part<Ranvier.Dom.SVGTextElement, unit> list) (content: string) =
    text' (Array.append [| at "x" x; at "y" y; at "fill" fill; at "font-size" size |] (Array.ofList parts)) { content }

let mono: Part<Ranvier.Dom.SVGTextElement, unit> = at "font-family" Brand.mono
let middle: Part<Ranvier.Dom.SVGTextElement, unit> = at "text-anchor" "middle"

/// An element of a brand SVG file and its descendants as template nodes.
let rec ofXml (node: XElement) : TemplateView<unit> =
    element node.Name.LocalName [| for a in node.Attributes () -> at a.Name.LocalName a.Value |] [| for c in node.Elements () -> ofXml c |]

let private brandSvg (relative: string) =
    XDocument.Load(Path.Combine (brandRoot, relative)).Root

/// The wordmark's shapes, in its own units.
let private wordmarkShapes =
    [|
        for e in (brandSvg "wordmark/ranvier-wordmark-dark.svg").Elements() -> ofXml e
    |]

/// The wordmark at (x, y), <c>scale</c> times its own units.
let wordmark (x: float) (y: float) (scale: float) =
    group [| at "transform" $"translate({num x} {num y}) scale({num scale})" |] wordmarkShapes

/// A state mark's stylesheet and its shapes, in its 96-unit box.
let stateMark (name: string) =
    let root = brandSvg $"states/{name}-dark.svg"

    let style =
        root.Elements ()
        |> Seq.filter (fun e -> e.Name.LocalName = "style")
        |> Seq.map _.Value
        |> String.concat ""

    style,
    [|
        for e in root.Elements () do
            if e.Name.LocalName <> "style" then
                ofXml e
    |]

// ---------------------------------------------------------------------------------------------------
// A card: its definitions, body and stylesheet.

type Card() =
    let definitions = ResizeArray<TemplateView<unit>>()
    let body = ResizeArray<TemplateView<unit>>()
    let mutable ids = 0

    member _.Uid(prefix: string) =
        ids <- ids + 1
        prefix + string ids

    member _.Define(view: TemplateView<unit>) =
        definitions.Add view

    member _.Add(view: TemplateView<unit>) =
        body.Add view

    member _.AddAll(views: TemplateView<unit> seq) =
        body.AddRange views

    member _.Definitions = definitions.ToArray ()
    member _.Body = body.ToArray ()

    /// Rules the card's elements use by class, such as a state mark's.
    member val Css = "" with get, set

    /// A user-space linear gradient from <c>c0</c> at <c>p0</c> to <c>c1</c> at <c>p1</c>.
    member this.Lin(p0: float * float, p1: float * float, c0: string, c1: string) =
        let id = this.Uid "l"
        let (x1, y1), (x2, y2) = p0, p1

        this.Define (
            linearGradient (at "id" id, at "gradientUnits" "userSpaceOnUse", at "x1" (f1 x1), at "y1" (f1 y1), at "x2" (f1 x2), at "y2" (f1 y2)) {
                stop (at "offset" "0", at "stop-color" c0) { () }
                stop (at "offset" "1", at "stop-color" c1) { () }
            }
        )

        $"url(#{id})"

    /// A soft elliptical light over the whole card, centred on (cx, cy).
    member this.Glow(cx: float, cy: float, r: float, ?squash: float, ?strength: float, ?colors: string * string * string) =
        let squash = defaultArg squash 0.5
        let strength = defaultArg strength 1.
        let c0, c1, c2 = defaultArg colors (Brand.cyan, Brand.blue, Brand.violet)
        let id = this.Uid "g"

        this.Define (
            radialGradient (
                at "id" id,
                at "cx" (num cx),
                at "cy" (num cy),
                at "r" (num r),
                at "gradientUnits" "userSpaceOnUse",
                at "gradientTransform" $"translate({num cx} {num cy}) scale(1 {num squash}) translate({num -cx} {num -cy})"
            ) {
                stop (at "offset" "0", at "stop-color" c0, at "stop-opacity" (f3 (0.16 * strength))) { () }
                stop (at "offset" ".45", at "stop-color" c1, at "stop-opacity" (f3 (0.07 * strength))) { () }
                stop (at "offset" "1", at "stop-color" c2, at "stop-opacity" "0") { () }
            }
        )

        this.Add (rect (at "width" "1200", at "height" "630", at "fill" $"url(#{id})") { () })

    /// A Gaussian blur filter of standard deviation <c>sd</c>. Its region is the blurred element's box, extended by 20% of
    /// its width on each side and 50% of its height above and below; with <c>whole</c> it is the entire card, which
    /// keeps a wide glow on a flat element unclipped.
    member this.Blur(sd: float, ?whole: bool) =
        let id = this.Uid "b"

        let region =
            if defaultArg whole false then
                [|
                    at "id" id
                    at "filterUnits" "userSpaceOnUse"
                    at "x" "0"
                    at "y" "0"
                    at "width" "1200"
                    at "height" "630"
                |]
            else
                [|
                    at "id" id
                    at "x" "-20%"
                    at "y" "-50%"
                    at "width" "140%"
                    at "height" "200%"
                |]

        this.Define (filter region { feGaussianBlur (at "stdDeviation" (num sd)) { () } })

        $"url(#{id})"

/// The card as a page: the font stylesheet, then the 1200 x 630 SVG.
let page (card: Card) : TemplateView<unit> =
    fragment
        [|
            Document.link (link.attribute ("rel", "stylesheet"), link.attribute ("href", Brand.fonts))
            svg (at "width" "1200", at "height" "630", at "viewBox" "0 0 1200 630", at "font-family" Brand.sans, at "style" "display:block") {
                yield defs () { yield! card.Definitions }

                if card.Css <> "" then
                    yield defs (defs.unsafeHtml (UnsafeHtml.Trust ("<style>" + card.Css + "</style>"))) { () }

                yield rect (at "width" "1200", at "height" "630", at "fill" Brand.canvas) { () }
                yield! card.Body
            }
        |]

/// The wordmark, a mono eyebrow, a headline and a line of copy.
let header (card: Card) (top: float) (eyebrow: float * string) (title: float * float * string) (copy: float * float * string) =
    let eyebrowY, section = eyebrow
    let titleY, titleSize, headline = title
    let copyY, copySize, line = copy

    card.AddAll
        [
            wordmark 88. top 0.62
            label "100" (num eyebrowY) Brand.cyan "22" [ mono; at "letter-spacing" "3" ] section
            label "100" (num titleY) Brand.text (num titleSize) [ at "font-weight" "600" ] headline
            label "100" (num copyY) Brand.muted (num copySize) [] line
        ]

// ---------------------------------------------------------------------------------------------------
// The myelinated fibre: a tapered axon with myelin segments, its nodes and the pulse train between them.

type Cubic = (float * float) * (float * float) * (float * float) * (float * float)

type NodeState =
    | Lit
    | Next
    | Idle

let private cubic (((x0, y0), (x1, y1), (x2, y2), (x3, y3)): Cubic) (t: float) =
    let u = 1. - t

    u ** 3. * x0
    + 3. * u * u * t * x1
    + 3. * u * t * t * x2
    + t ** 3. * x3,
    u ** 3. * y0
    + 3. * u * u * t * y1
    + 3. * u * t * t * y2
    + t ** 3. * y3

let distance ((x0, y0): float * float) ((x1, y1): float * float) =
    Math.Sqrt ((x1 - x0) ** 2. + (y1 - y0) ** 2.)

/// A quadratic Bézier at <c>t</c>.
let quad (((x0, y0), (cx, cy), (x1, y1)): (float * float) * (float * float) * (float * float)) (t: float) =
    let u = 1. - t
    u * u * x0 + 2. * u * t * cx + t * t * x1, u * u * y0 + 2. * u * t * cy + t * t * y1

/// The first index whose value is at least <c>value</c>, as Python's <c>bisect_left</c>.
let private lowerBound (sorted: float array) (value: float) =
    let mutable lo = 0
    let mutable hi = sorted.Length

    while lo < hi do
        let mid = (lo + hi) / 2
        if sorted[mid] < value then lo <- mid + 1 else hi <- mid

    lo

/// A myelinated fibre along a cubic spline: thick (near) at the crest, thin (far) at the ends.
type Fibre(segments: Cubic list, ?crestX: float, ?reach: float, ?wmin: float, ?wmax: float, ?segLength: float, ?gap: float, ?offset: float) =
    let crestX = defaultArg crestX 560.
    let reach = defaultArg reach 760.
    let wmin = defaultArg wmin 10.
    let wmax = defaultArg wmax 34.
    let segLength = defaultArg segLength 178.
    let gap = defaultArg gap 40.
    let offset = defaultArg offset 30.

    let points =
        [|
            for s in segments do
                for i in 0..1500 -> cubic s (float i / 1500.)
        |]

    let lengths =
        let acc = Array.zeroCreate points.Length

        for i in 1 .. points.Length - 1 do
            acc[i] <- acc[i - 1] + distance points[i - 1] points[i]

        acc

    let total = lengths[lengths.Length - 1]

    member _.At(d: float) =
        points[min (lowerBound lengths (max d 0.)) (points.Length - 1)]

    member this.Normal(d: float) =
        let (ax, ay), (bx, by) = this.At (d - 2.), this.At (d + 2.)
        let dx, dy = bx - ax, by - ay

        let n =
            match Math.Sqrt (dx * dx + dy * dy) with
            | 0. -> 1.
            | n -> n

        dy / n, -dx / n

    member this.Width(d: float) =
        let c =
            1.
            - min 1. (abs (fst (this.At d) - crestX) / reach)

        wmin + (wmax - wmin) * c ** 1.3

    member this.Depth(d: float) =
        (this.Width d - wmin) / (wmax - wmin)

    member this.Hue(d: float) =
        grad (1. - this.Depth d)

    member this.Fill(d: float) =
        mix Brand.canvas (this.Hue d) (0.06 + 0.34 * this.Depth d ** 1.2)

    member this.Edge(d: float) =
        mix Brand.canvas (this.Hue d) (0.18 + 0.72 * this.Depth d)

    /// The axon between arc lengths <c>a</c> and <c>b</c>, as a run of short lines.
    member this.Axon(a: float, b: float) =
        [
            for i in 0..12 .. points.Length - 13 do
                if lengths[i] >= a && lengths[i + 12] <= b then
                    let (x0, y0), (x1, y1) = points[i], points[i + 12]
                    let d = lengths[i]

                    line (
                        at "x1" (f1 x0),
                        at "y1" (f1 y0),
                        at "x2" (f1 x1),
                        at "y2" (f1 y1),
                        at "stroke" (mix Brand.canvas (this.Hue d) (0.2 + 0.5 * this.Depth d)),
                        at "stroke-width" (f2 (max 1.5 (this.Width d * 0.1))),
                        at "stroke-linecap" "round"
                    ) {
                        ()
                    }
        ]

    /// The visible part, <c>a</c> to <c>b</c>, of a myelin sheath that runs from <c>d0</c> to <c>d1</c> and rounds off
    /// at both of its ends.
    member this.Sheath(card: Card, a: float, b: float, d0: float, d1: float) =
        let side sign =
            [|
                for j in 0..120 do
                    let d = a + (b - a) * float j / 120.
                    let x, y = this.At d
                    let nx, ny = this.Normal d
                    let r = this.Width d / 2.
                    let e = min 1. (min ((d - d0) / r) ((d1 - d) / r))

                    let r =
                        if e < 1. then
                            r * sqrt (max 0. (1. - (1. - e) ** 2.))
                        else
                            r

                    x + sign * nx * r, y + sign * ny * r
            |]

        let outline = Array.append (side 1.) (Array.rev (side -1.))
        let pa, pb = this.At a, this.At b
        let fill = card.Lin (pa, pb, this.Fill a, this.Fill b)
        let stroke = card.Lin (pa, pb, this.Edge a, this.Edge b)

        let d =
            "M"
            + (outline
               |> Array.map (fun (x, y) -> $"{f1 x} {f1 y}")
               |> String.concat " L")
            + "Z"

        path (at "d" d, at "fill" fill, at "stroke" stroke, at "stroke-width" "2", at "stroke-linejoin" "round") { () }

    /// The arc length of the fibre's point nearest (x, y).
    member _.ArcOf(x: float, y: float) =
        lengths[points
                |> Array.mapi (fun i (px, py) -> i, (px - x) ** 2. + (py - y) ** 2.)
                |> Array.minBy snd
                |> fst]

    member _.Total = total

    /// Draws the axon and one myelin sheath between each pair of consecutive <c>stops</c>, ending <c>inset</c> short
    /// of both, so each stop is the bare node between two sheaths.
    member this.DrawBetween(card: Card, stops: float array, inset: float, ?opacity: float, ?filter: string) =
        let parts =
            ResizeArray<TemplateView<unit>>(this.Axon (stops[0], stops[stops.Length - 1]))

        for i in 0 .. stops.Length - 2 do
            let d0, d1 = stops[i] + inset, stops[i + 1] - inset
            parts.Add (this.Sheath (card, d0, d1, d0, d1))

        card.Add (group (Array.append [| at "opacity" (num (defaultArg opacity 1.)) |] (filterOf filter)) parts)

    /// Draws the axon and myelin segments; returns the arc lengths of the nodes visible on the card.
    member this.Draw(card: Card, ?opacity: float, ?filter: string) =
        let parts = ResizeArray<TemplateView<unit>>(this.Axon (0., total))
        let nodes = ResizeArray<float>()
        let mutable k = 0

        while float k * (segLength + gap) - offset <= total do
            let d0 = float k * (segLength + gap) - offset
            let d1 = d0 + segLength
            let a, b = max d0 0., min d1 total

            if b - a > 4. then
                parts.Add (this.Sheath (card, a, b, d0, d1))

            let node = d1 + gap / 2.

            if
                0. < node
                && node < total
                && -40. < fst (this.At node)
                && fst (this.At node) < 1240.
            then
                nodes.Add node

            k <- k + 1

        card.Add (group (Array.append [| at "opacity" (num (defaultArg opacity 1.)) |] (filterOf filter)) parts)

        Array.ofSeq nodes

    /// A node of Ranvier: lit by the pulse, next in line, or idle.
    member this.Node(card: Card, d: float, state: NodeState) =
        let x, y = this.At d
        let s = this.Width d / 30.

        match state with
        | Lit ->
            card.Add (dot (f1 x) (f1 y) (f1 (22. * s)) [ at "fill" Brand.cyan; at "opacity" ".14" ])
            card.Add (dot (f1 x) (f1 y) (f1 (9. * s)) [ at "fill" Brand.cyan ])
        | Next -> card.Add (dot (f1 x) (f1 y) (f1 (9. * s)) [ at "fill" Brand.canvas; at "stroke" Brand.cyan; at "stroke-width" "2.5" ])
        | Idle -> card.Add (dot (f1 x) (f1 y) (f1 (max 2.5 (6. * s))) [ at "fill" (this.Edge d) ])

    /// The arc of a hop from node <c>a</c> to node <c>b</c>, lifted off the fibre.
    member this.Hop(a: float, b: float, lift: float) =
        let p0, p1 = this.At a, this.At b
        let nx, ny = this.Normal ((a + b) / 2.)
        let nx, ny = if ny > 0. then -nx, -ny else nx, ny
        let h = lift * distance p0 p1
        p0, ((fst p0 + fst p1) / 2. + nx * h, (snd p0 + snd p1) / 2. + ny * h), p1

    /// The point <c>u</c> hop units along <c>hops</c>, with its hop index and the fraction of that hop.
    member this.Along(hops: float array, lift: float) =
        let curves =
            [| for i in 0 .. hops.Length - 2 -> this.Hop (hops[i], hops[i + 1], lift) |]

        fun (u: float) ->
            let h = min (int u) (curves.Length - 1)
            let t = u - float h
            h, t, quad curves[h] t

    /// The colour of the fibre <c>t</c> of the way through hop <c>h</c>.
    member this.HueOn(hops: float array, h: int, t: float) =
        this.Hue (hops[h] + t * (hops[h + 1] - hops[h]))

    /// A pulse train jumping node to node along <c>hops</c>, its lead packet at <c>head</c> hop units.
    member this.Pulse(card: Card, hops: float array, head: float, ?lift: float, ?trail: bool, ?filter: string) =
        let lift = defaultArg lift 0.55
        let period, packets, particles, size = 0.5, 4, 20, 1.
        let levels = [| 1.; 0.55; 0.32; 0.18 |]
        let along = this.Along (hops, lift)

        if defaultArg trail true then
            for i in 0 .. hops.Length - 2 do
                let curve = this.Hop (hops[i], hops[i + 1], lift)

                for j in 1..29 do
                    let x, y = quad curve (float j / 30.)
                    card.Add (dot (f1 x) (f1 y) "1.6" [ at "fill" Brand.trail ])

        let parts = ResizeArray<TemplateView<unit>>()
        let mutable k = 0

        while k < packets && head - float k * period > 0. do
            let h0 = head - float k * period
            let level = levels[min k (levels.Length - 1)]

            for i in 0 .. particles - 1 do
                let f = float i / float (particles - 1)
                let u = h0 - 0.3 * (1. - f) ** 1.3

                if u >= 0. then
                    let h, t, (x, y) = along u
                    let _, _, (xa, ya) = along (max (u - 0.01) 0.)
                    let _, _, (xb, yb) = along (u + 0.01)
                    let dx, dy = xb - xa, yb - ya

                    let m =
                        match Math.Sqrt (dx * dx + dy * dy) with
                        | 0. -> 1.
                        | m -> m

                    let jitter =
                        frac (
                            sin (float i * 12.9898 + float k * 78.233)
                            * 43758.5453
                        )
                        - 0.5

                    let spread = (1. - f) * 20.
                    let x, y = x - dy / m * jitter * spread, y + dx / m * jitter * spread
                    let r = (2.6 + 6.4 * f ** 2.) * size

                    parts.Add (
                        dot
                            (f1 x)
                            (f1 y)
                            (f2 r)
                            [
                                at "fill" (this.HueOn (hops, h, t))
                                at "opacity" (f2 ((0.15 + 0.85 * f ** 2.) * level))
                            ]
                    )

            let h, t, (x, y) = along h0
            parts.Add (dot (f1 x) (f1 y) (f1 (26. * level ** 0.5 * size)) [ at "fill" (this.HueOn (hops, h, t)); at "opacity" (f2 (0.16 * level)) ])

            if k = 0 then
                parts.Add (dot (f1 x) (f1 y) (f1 (4.5 * size)) [ at "fill" Brand.text ])

            k <- k + 1

        card.Add (group (filterOf filter) parts)

/// A long exposure of the pulse train: each packet drawn out into a ribbon that brightens toward its head. <c>size</c>
/// scales the ribbons and the head; each hop arcs off the fibre by <c>lift</c> times its length.
let exposureWith (card: Card) (fibre: Fibre) (hops: float array) (head: float) (size: float) (lift: float) =
    let levels = [| 1.; 0.6; 0.35; 0.2 |]
    let period, length = 0.5, 1.1
    let along = fibre.Along (hops, lift)

    let streak (u0: float) (u1: float) (width: float) (level: float) (filter: string option) =
        let parts = ResizeArray<TemplateView<unit>>()
        let n = 90
        let mutable previous = let _, _, p = along (max u0 0.) in p

        for i in 1..n do
            let u = u0 + (u1 - u0) * float i / float n

            if u > 0. then
                let h, t, (x, y) = along u
                let k = float i / float n
                let x0, y0 = previous

                parts.Add (
                    line (
                        at "x1" (f1 x0),
                        at "y1" (f1 y0),
                        at "x2" (f1 x),
                        at "y2" (f1 y),
                        at "stroke" (fibre.HueOn (hops, h, t)),
                        at "stroke-width" (f2 (width * (0.25 + 0.75 * k))),
                        at "opacity" (f2 (level * k ** 2.))
                    ) {
                        ()
                    }
                )

                previous <- x, y

        card.Add (group (Array.append (filterOf filter) [| at "stroke-linecap" "round" |]) parts)

    let bloom = card.Blur (8., whole = true)

    levels
    |> Array.iteri (fun k level ->
        let h0 = head - float k * period

        if h0 > 0. then
            streak (h0 - length) h0 (22. * size) (0.35 * level) (Some bloom)
            streak (h0 - length) h0 (5. * size) level None
            streak (h0 - length * 0.55) h0 (1.6 * size) level None)

    let _, _, (x, y) = along head
    card.Add (dot (f1 x) (f1 y) (f1 (26. * size)) [ at "fill" Brand.cyan; at "opacity" ".18" ])
    card.Add (dot (f1 x) (f1 y) (f1 (5. * size)) [ at "fill" Brand.text ])

/// The long exposure at the size and lift of the home card.
let exposure (card: Card) (fibre: Fibre) (hops: float array) (head: float) =
    exposureWith card fibre hops head 1. 0.55

/// An axon in cross-section: a core inside concentric myelin lamellae, brighter and sharper when near.
/// A lit axon carries the signal as a bright core with a horizontal flare.
let axon (card: Card) (cx: float) (cy: float) (r: float) (depth: float) (sd: float) (lit: bool) =
    let core = r * 0.56
    let x, y = num cx, num cy

    let rings =
        [
            dot x y (f1 r) [ at "fill" (mix Brand.canvas Brand.violet (0.05 + 0.1 * depth)) ]
            for i in 0..6 do
                let t = float i / 6.
                let rr = core + (r - core) * (float i + 0.5) / 7.

                let colour =
                    mix Brand.canvas (grad (0.15 + 0.85 * t)) (0.25 + 0.6 * depth * (1. - 0.5 * t))

                dot
                    x
                    y
                    (f1 rr)
                    [
                        at "fill" "none"
                        at "stroke" colour
                        at "stroke-width" (f2 ((r - core) / 7. * 0.55))
                    ]
            dot
                x
                y
                (f1 core)
                [
                    at "fill" (mix Brand.canvas Brand.cyan (0.05 + 0.08 * depth))
                    at "stroke" (mix Brand.canvas Brand.cyan (0.3 + 0.6 * depth))
                    at "stroke-width" "2"
                ]
        ]

    card.Add (
        group
            [|
                at "opacity" (f2 (0.35 + 0.65 * depth))
                if sd > 0. then
                    at "filter" (card.Blur sd)
            |]
            rings
    )

    if lit then
        let id = card.Uid "r"

        card.Define (
            radialGradient (at "id" id) {
                stop (at "offset" "0", at "stop-color" Brand.text, at "stop-opacity" ".95") { () }
                stop (at "offset" ".25", at "stop-color" Brand.cyan, at "stop-opacity" ".6") { () }
                stop (at "offset" "1", at "stop-color" Brand.blue, at "stop-opacity" "0") { () }
            }
        )

        card.AddAll
            [
                dot x y (f1 (core * 0.95)) [ at "fill" $"url(#{id})" ]
                ellipse (
                    at "cx" x,
                    at "cy" y,
                    at "rx" (f0 (r * 2.5)),
                    at "ry" "3",
                    at "fill" Brand.cyan,
                    at "opacity" ".35",
                    at "filter" (card.Blur 3.)
                ) {
                    ()
                }
                ellipse (at "cx" x, at "cy" y, at "rx" (f0 (r * 1.6)), at "ry" "1.2", at "fill" Brand.text, at "opacity" ".5") { () }
                dot x y "6" [ at "fill" Brand.text ]
            ]

/// Soft out-of-focus particles in the band <c>y0</c> to <c>y0 + h</c>, placed deterministically.
let dust (card: Card) (n: int) (y0: float) (h: float) (seed: int) =
    let soft = card.Blur 3.

    let dots =
        [
            for i in seed .. seed + n - 1 do
                let i = float i
                let x = frac (sin (i * 91.7) * 43758.5453) * 1200.
                let y = y0 + frac (sin (i * 17.3) * 23421.631) * h
                let r = 1.5 + frac (sin (i * 5.1) * 9173.1) * 5.
                let colour = grad (frac (sin (i * 3.3) * 777.7))
                dot (f0 x) (f0 y) (f1 r) [ at "fill" colour; at "opacity" (f2 (0.08 + 0.18 * (r / 6.5))) ]
        ]

    card.Add (group [| at "filter" soft |] dots)

/// The index of the node nearest <c>x</c> across the card.
let nearest (fibre: Fibre) (nodes: float array) (x: float) =
    nodes
    |> Array.mapi (fun i d -> i, abs (fst (fibre.At d) - x))
    |> Array.minBy snd
    |> fst

let slice (start: int) (count: int) (items: 'a array) =
    items |> Array.skip start |> Array.truncate count

// ---------------------------------------------------------------------------------------------------
// The cards.

/// Home and default card: a long exposure of the pulse train along the fibre, with a far fibre and dust for depth.
let home (c: Card) =
    c.Glow (520., 500., 640., strength = 1.3)
    c.Glow (1050., 120., 420., squash = 0.6, strength = 0.55, colors = (Brand.violet, Brand.blue, Brand.blue))

    let far =
        Fibre (
            [
                (640., -40.), (820., 60.), (900., 200.), (1060., 190.)
                (1060., 190.), (1180., 180.), (1220., 110.), (1300., 80.)
            ],
            crestX = 1000.,
            reach = 420.,
            wmin = 4.,
            wmax = 13.,
            segLength = 90.,
            gap = 22.
        )

    far.Draw (c, opacity = 0.55, filter = c.Blur 2.2)
    |> ignore

    dust c 46 400. 230. 0

    c.AddAll
        [
            wordmark 88. 70. 1.55
            label "100" "325" Brand.text "46" [ at "font-weight" "500" ] "Fine-grained reactive computation for .NET."
            label "100" "377" Brand.muted "28" [] "Signals, memos, effects and async boundaries in F#"
        ]

    let f =
        Fibre (
            [
                (-90., 660.), (120., 640.), (250., 470.), (470., 478.)
                (470., 478.), (690., 486.), (760., 600.), (940., 580.)
                (940., 580.), (1080., 565.), (1150., 470.), (1300., 400.)
            ]
        )

    let nodes = f.Draw c
    let hops = nodes |> slice (nearest f nodes 270.) 5
    exposure c f hops 3.3

    for d in nodes do
        f.Node (
            c,
            d,
            (if Array.contains d hops[..2] then Lit
             elif d = hops[3] then Next
             else Idle)
        )

/// The fibre the guide cards share, drawn side-on across the lower half.
let guideFibre crestX =
    Fibre (
        [
            (-90., 470.), (200., 480.), (380., 420.), (620., 420.)
            (620., 420.), (860., 420.), (1000., 470.), (1290., 440.)
        ],
        crestX = crestX,
        reach = 820.,
        wmin = 12.,
        wmax = 28.,
        segLength = 200.,
        gap = 44.,
        offset = 250.
    )

/// Async card: the fibre as a timeline; each node is a boundary state, each hop the call that moves it.
let asyncCard (c: Card) =
    c.Glow (560., 420., 620., strength = 0.9)
    header c 44. (170., "GUIDE") (232., 60., "Async and pending") (278., 26., "A boundary shows a fallback while its inputs are in flight.")
    let f = guideFibre 560.
    let all = f.Draw c

    let states =
        [|
            "pending", "Pending"
            "fallback", "Fallback"
            "ready", "Ready"
            "failed", "Failed"
            "recovered", "Recovered"
        |]

    let nodes = all |> slice (nearest f all 140.) states.Length
    f.Pulse (c, nodes[..3], 1.55, lift = 0.36)
    c.Css <- fst (stateMark "ready")

    nodes
    |> Array.iteri (fun i d ->
        let x, _ = f.At d

        f.Node (
            c,
            d,
            (if i < 2 then Lit
             elif i = 2 then Next
             else Idle)
        )

        let key, name = states[i]

        c.Add (
            group
                [|
                    if i > 2 then
                        at "opacity" ".45"
                |]
                [
                    group [| at "transform" $"translate({f1 (x - 33.)} 498) scale(.6875)" |] (snd (stateMark key))
                    label (f1 x) "600" Brand.muted "22" [ middle; mono ] name
                ]
        ))

    [ "createAsyncSource ()"; "price.Settle 4m"; "price.Fail exn" ]
    |> List.iteri (fun i call ->
        let (x0, y0), (x1, y1) = f.At nodes[i], f.At nodes[i + 1]

        label (f1 ((x0 + x1) / 2.)) (f1 (min y0 y1 - 62.)) Brand.text "20" [ middle; mono; at "opacity" (if i < 2 then "1" else ".45") ] call
        |> c.Add)

/// Signal maps card: the fibre as a traced graph drawn live; a write travels from the signal to the effect.
let signalMaps (c: Card) =
    c.Glow (700., 430., 640., strength = 0.9)
    header c 44. (170., "GUIDE") (232., 60., "Signal maps") (278., 26., "The traced graph behind an example, drawn live as it runs.")
    let f = guideFibre 700.
    let all = f.Draw c

    let steps =
        [|
            "write", "lines", "[4M; 4M]"
            "moved", "subtotal", "8M"
            "moved", "total", "13M"
            "run", "effect", ""
        |]

    let nodes = all |> slice (nearest f all 140.) steps.Length
    f.Pulse (c, nodes, 2.6, lift = 0.36)

    nodes
    |> Array.iteri (fun i d ->
        let x, y = f.At d
        f.Node (c, d, (if i = nodes.Length - 1 then Next else Lit))
        let kind, name, value = steps[i]

        c.AddAll
            [
                label (f1 x) (f1 (y - 58.)) Brand.cyan "20" [ middle; mono ] value
                label (f1 x) "560" Brand.muted "20" [ middle; mono ] kind
                label (f1 x) "592" Brand.text "22" [ middle; mono ] name
            ])

/// Timing card: changed captures extending quiet time or sharing a fixed window.
let timing (c: Card) =
    c.Glow (850., 445., 560., strength = 0.9)
    header c 44. (170., "GUIDE") (242., 64., "Debounce and throttle") (292., 26., "Capture now. Admit after quiet, or at a fixed deadline.")

    for y, name, start, deadline, caption in
        [
            400., "DEBOUNCE", 650., 990., "quiet period"
            525., "THROTTLE LAST", 410., 830., "fixed window"
        ] do
        c.AddAll
            [
                label "100" (num (y + 7.)) Brand.muted "20" [ mono ] name
                path (at "d" $"M330 {num y}H1090", at "stroke" Brand.muted, at "stroke-opacity" ".3", at "stroke-width" "2") { () }
                rect (
                    at "x" (num start),
                    at "y" (num (y - 25.)),
                    at "width" (num (deadline - start)),
                    at "height" "50",
                    at "rx" "12",
                    at "fill" Brand.violet,
                    at "fill-opacity" ".08",
                    at "stroke" Brand.violet,
                    at "stroke-width" "2",
                    at "stroke-dasharray" "6 5"
                ) {
                    ()
                }
                for x in [ 410.; 525.; 650. ] do
                    dot (num x) (num y) "7" [ at "fill" Brand.cyan ]
                dot (num deadline) (num y) "18" [ at "fill" Brand.violet; at "fill-opacity" ".18" ]
                dot (num deadline) (num y) "9" [ at "fill" Brand.violet ]
                label (num ((start + deadline) / 2.)) (num (y + 54.)) Brand.muted "18" [ middle; mono ] caption
            ]

/// Tracing card: a why chain read backwards along the fibre, each node a hop from the run to the write that caused it.
let tracing (c: Card) =
    c.Glow (640., 440., 640., strength = 0.9)
    header c 44. (170., "GUIDE") (232., 60., "Tracing") (278., 26., "Why a node ran, why it did not, and where it came from.")
    let f = guideFibre 620.
    let all = f.Draw c

    let steps =
        [|
            "Write", "/lines"
            "Moved", "/subtotal"
            "Moved", "/total"
            "RunStart", "/banner"
        |]

    let nodes = all |> slice (nearest f all 140.) steps.Length
    // The chain is read from the run back to its cause, so the pulse travels right to left.
    f.Pulse (c, Array.rev nodes, 2.6, lift = 0.36)

    nodes
    |> Array.iteri (fun i d ->
        let x, _ = f.At d
        f.Node (c, d, (if i = 0 then Next else Lit))
        let kind, name = steps[i]

        c.AddAll
            [
                label (f1 x) "560" Brand.muted "20" [ middle; mono ] kind
                label (f1 x) "592" Brand.text "22" [ middle; mono ] name
            ])

    let x, y = f.At nodes[0]
    c.Add (label (f1 (x - 30.)) (f1 (y - 115.)) Brand.cyan "20" [ at "text-anchor" "start"; mono ] "root: user write")

/// The fibre side-on with a long exposure of the pulse, under a section's title.
let side (c: Card) (glowX: float) (bracket: bool) =
    c.Glow (glowX, 490., 600., strength = 1.1)

    let f =
        Fibre (
            [
                (-90., 590.), (260., 610.), (420., 490.), (700., 480.)
                (700., 480.), (960., 470.), (1080., 410.), (1300., 370.)
            ],
            crestX = 780.,
            reach = 700.,
            wmin = 8.,
            wmax = 30.
        )

    let nodes = f.Draw c
    let hops = nodes |> slice (nearest f nodes 420.) 4
    exposure c f hops 2.3

    for d in nodes do
        f.Node (
            c,
            d,
            (if Array.contains d hops[..1] then Lit
             elif d = hops[2] then Next
             else Idle)
        )

    if bracket then
        let (x0, y0), (x1, y1) = f.At hops[0], f.At hops[2]
        let yb = max y0 y1 + 58.

        c.AddAll
            [
                path (
                    at "d" $"M{f1 x0} {f1 (yb - 8.)}V{f1 yb}H{f1 x1}V{f1 (yb - 8.)}",
                    at "fill" "none",
                    at "stroke" Brand.muted,
                    at "stroke-width" "1.5",
                    at "opacity" ".7"
                ) {
                    ()
                }
                label (f1 x0) (f1 (yb + 30.)) Brand.muted "20" [ middle; mono ] "m(N)"
                label (f1 x1) (f1 (yb + 30.)) Brand.muted "20" [ middle; mono ] "m(2N)"
            ]

/// The nerve end-on: a bundle of axons in cross-section, one of them carrying the signal.
let endOn (c: Card) =
    c.Glow (1000., 470., 520., squash = 0.8, strength = 1.3)
    c.Glow (1080., 60., 420., squash = 0.6, strength = 0.5, colors = (Brand.violet, Brand.blue, Brand.blue))

    for cx, cy, r, depth, sd in
        [
            560., 610., 26., 0.15, 6.
            760., 560., 40., 0.25, 4.
            1190., 360., 44., 0.3, 4.
            1150., 60., 64., 0.45, 2.2
            960., 40., 38., 0.3, 3.5
            830., 470., 58., 0.45, 2.
            1150., 600., 70., 0.5, 1.6
        ] do
        axon c cx cy r depth sd false

    axon c 1010. 450. 110. 1. 0. true

/// A section card: eyebrow, headline and line over the fibre side-on, or the nerve end-on.
let section (key: string) (eyebrow: string) (title: string) (line: string) (view: Card -> unit) (c: Card) =
    view c
    dust c 30 380. 250. key.Length
    header c 48. (200., eyebrow) (278., 72., title) (330., 28., line)

/// A tooling card: the fibre side-on under a smaller headline.
let tooling (key: string) (eyebrow: string) (title: string) (line: string) (c: Card) =
    side c 830. false
    dust c 30 380. 250. key.Length
    header c 48. (200., eyebrow) (278., 60., title) (330., 27., line)

// ---------------------------------------------------------------------------------------------------
// Rendering: mount each card in one headless WebCore host and snapshot it.

/// The cubic segments of a Catmull–Rom spline from the first point to the last, through every point between.
let spline (points: (float * float) list) : Cubic list =
    let p = Array.ofList points

    let point i =
        p[Math.Clamp (i, 0, p.Length - 1)]

    [
        for i in 0 .. p.Length - 2 ->
            let (x0, y0), (x1, y1), (x2, y2), (x3, y3) =
                point (i - 1), point i, point (i + 1), point (i + 2)

            (x1, y1), (x1 + (x2 - x0) / 6., y1 + (y2 - y0) / 6.), (x2 - (x3 - x1) / 6., y2 - (y3 - y1) / 6.), (x2, y2)
    ]

/// Core concepts card: a reactive graph drawn as one nerve. Each vertex is a node of Ranvier and each dependency one
/// myelin sheath; a write's pulse jumps vertex to vertex, and the fibres of the unchanged signal stay dark.
let core (c: Card) =
    c.Glow (620., 500., 640., strength = 1.1)
    c.Glow (1050., 120., 420., squash = 0.6, strength = 0.55, colors = (Brand.violet, Brand.blue, Brand.blue))
    dust c 40 400. 230. 5
    header c 44. (170., "GUIDE") (232., 60., "Signals, memos and effects") (278., 26., "A write re-runs the memos and effects that read it.")

    let signal, memo, memo2, effect =
        (190., 512.), (450., 452.), (730., 486.), (995., 440.)

    let signal2, memo3, effect2 = (570., 572.), (850., 568.), (1110., 540.)
    let soft = c.Blur 0.9

    // The unchanged signal: one input rising into the second memo from below, and a memo and an effect of its own.
    let input =
        let (x0, y0), (x1, y1) = signal2, memo2
        Fibre ([ ((x0, y0), (x0 + 70., y0 - 50.), (x1 - 10., y1 + 50.), (x1, y1)) ], crestX = 1400., reach = 2400., wmin = 8., wmax = 13.)

    input.DrawBetween (c, [| 0.; input.Total |], 11., opacity = 0.95, filter = soft)

    let back =
        Fibre (spline [ signal2; memo3; effect2 ], crestX = 1400., reach = 2400., wmin = 8., wmax = 13.)

    let backStops = [| for v in [ signal2; memo3; effect2 ] -> back.ArcOf v |]
    back.DrawBetween (c, backStops, 10., opacity = 0.95, filter = soft)

    for d in backStops do
        back.Node (c, d, Idle)

    // The nerve the write runs along, thickest mid-way, and the pulse jumping from vertex to vertex.
    let nerve =
        Fibre (spline [ signal; memo; memo2; effect ], crestX = 640., reach = 760., wmin = 12., wmax = 26.)

    let stops = [| for v in [ signal; memo; memo2; effect ] -> nerve.ArcOf v |]
    nerve.DrawBetween (c, stops, 11.)
    exposureWith c nerve stops 2.72 1.25 0.3

    for x, y in [ signal; memo; memo2 ] do
        c.AddAll
            [
                dot (f1 x) (f1 y) "26" [ at "fill" Brand.cyan; at "opacity" ".14" ]
                dot (f1 x) (f1 y) "9" [ at "fill" Brand.cyan ]
            ]

    let ex, ey = effect
    c.Add (dot (f1 ex) (f1 ey) "11" [ at "fill" Brand.canvas; at "stroke" Brand.cyan; at "stroke-width" "3" ])

    for (x, y), text in [ signal, "signal"; memo, "memo"; effect, "effect" ] do
        c.Add (label (f1 x) (f1 (y + 42.)) Brand.muted "20" [ middle; mono ] text)

/// Collections card: keyed rows as parallel fibres. The row whose item changed carries the pulse; the others stay idle.
let collections (c: Card) =
    c.Glow (700., 500., 640., strength = 1.1)
    dust c 36 380. 250. 9
    header c 44. (170., "GUIDE") (232., 60., "Keyed collections") (278., 26., "Projections, lookups and selectors keep one row per key.")

    [| "a", 392.; "b", 452.; "c", 512.; "d", 572. |]
    |> Array.iteri (fun i (key, y) ->
        let changed = i = 2

        let row =
            Fibre (
                [ ((150., y + 8.), (500., y - 10.), (820., y + 12.), (1260., y - 6.)) ],
                crestX = 760.,
                reach = 760.,
                wmin = (if changed then 11. else 7.),
                wmax = (if changed then 22. else 14.),
                segLength = 118.,
                gap = 26.,
                offset = 20. + 41. * float i
            )

        let nodes = row.Draw (c, opacity = (if changed then 1. else 0.6))

        if changed then
            let hops = nodes |> slice (nearest row nodes 560.) 4
            row.Pulse (c, hops, 2.6, lift = 0.38)

            for d in nodes do
                row.Node (
                    c,
                    d,
                    (if Array.contains d hops[..2] then Lit
                     elif d = hops[3] then Next
                     else Idle)
                )

        c.Add (label "100" (num (y + 8.)) (if changed then Brand.cyan else Brand.muted) "22" [ mono ] key))

/// Extensions card: the core as a trunk fibre carrying the pulse, with a branch to each package built on it.
let extensions (c: Card) =
    c.Glow (700., 520., 620., strength = 1.1)
    dust c 36 380. 250. 11
    header c 44. (170., "EXTENSIONS") (232., 60., "Packages on the graph") (278., 26., "Queries, Elmish, the DOM, JSON wire codecs and MCP.")

    let trunk =
        Fibre (
            [
                (-80., 590.), (260., 600.), (420., 540.), (640., 545.)
                (640., 545.), (860., 550.), (980., 500.), (1290., 470.)
            ],
            crestX = 640.,
            reach = 760.,
            wmin = 8.,
            wmax = 24.
        )

    for x, name, top in
        [
            300., "Ranvier.Query", 410.
            500., "Ranvier.Elmish", 392.
            700., "Ranvier.Dom", 408.
            880., "Ranvier.Wire", 390.
            1060., "Ranvier.Mcp", 404.
        ] do
        let x0, y0 = trunk.At (trunk.ArcOf (x, 560.))
        let tip = x + 40., top

        let branch =
            Fibre (
                [ ((x0, y0), (x0 + 10., y0 - 50.), (fst tip, top + 45.), tip) ],
                crestX = x,
                reach = 600.,
                wmin = 4.,
                wmax = 8.,
                segLength = 58.,
                gap = 12.,
                offset = 8.
            )

        branch.Draw (c, opacity = 0.7) |> ignore

        c.AddAll
            [
                dot
                    (f1 (fst tip))
                    (f1 top)
                    "6"
                    [
                        at "fill" Brand.canvas
                        at "stroke" (mix Brand.canvas Brand.blue 0.6)
                        at "stroke-width" "2"
                    ]
                label (f1 (fst tip)) (f1 (top - 16.)) Brand.muted "17" [ middle; mono ] name
            ]

    let nodes = trunk.Draw c
    let hops = nodes |> slice (nearest trunk nodes 420.) 4
    exposure c trunk hops 2.3

    for d in nodes do
        trunk.Node (
            c,
            d,
            (if Array.contains d hops[..1] then Lit
             elif d = hops[2] then Next
             else Idle)
        )

/// API reference card: a nerve in cross-section with one axon lit, each axon a documented member.
let reference (c: Card) =
    c.Glow (920., 470., 520., squash = 0.8, strength = 1.3)
    c.Glow (1080., 60., 420., squash = 0.6, strength = 0.5, colors = (Brand.violet, Brand.blue, Brand.blue))

    for cx, cy, r, depth, sd in
        [
            700., 470., 30., 0.2, 4.5
            760., 590., 46., 0.35, 3.
            900., 625., 40., 0.3, 3.5
            990., 230., 34., 0.25, 4.
            1180., 250., 50., 0.3, 3.5
            1180., 470., 40., 0.35, 3.
            1060., 395., 62., 0.45, 2.
            1090., 565., 56., 0.4, 2.4
        ] do
        axon c cx cy r depth sd false

    axon c 900. 470. 90. 1. 0. true
    dust c 30 380. 250. 9
    header c 48. (200., "REFERENCE") (278., 72., "API reference") (330., 28., "Every public type and member, with its documentation.")

/// A card's key, its file under brand/og, whether docs/static/og carries it, and how it is drawn.
type Entry =
    {
        Key: string
        File: string
        Docs: bool
        Draw: Card -> unit
    }

let entry key docs draw =
    {
        Key = key
        File = (if key = "home" then "ranvier-og" else $"ranvier-og-{key}")
        Docs = docs
        Draw = draw
    }

let cards =
    [
        entry "home" true home
        entry "async" true asyncCard
        entry "signal-maps" true signalMaps
        entry "timing" true timing
        entry "tracing" true tracing
        entry
            "guide"
            true
            (section "guide" "GUIDE" "Build with signals" "Installation, collections, async boundaries and troubleshooting." (fun c ->
                side c 760. false))
        entry "concepts" true (section "concepts" "CONCEPTS" "How the graph works" "Signals, memos, effects and the async graph behind them." endOn)
        entry
            "benchmarks"
            true
            (section "benchmarks" "BENCHMARKS" "Measured per commit" "BenchmarkDotNet suites and instruction counts." (fun c -> side c 760. true))
        entry "fable" true (tooling "fable" "FABLE" "The JavaScript target" "Compile reactive models and browser views from F#.")
        entry "nacara" true (tooling "nacara" "NACARA PLUGIN" "Runnable documentation" "Ranvier components, signal maps and editable examples.")
        entry "core" true core
        entry "collections" true collections
        entry "extensions" true extensions
        entry "reference" true reference
        entry "blog" true (tooling "blog" "BLOG" "Building with Ranvier" "Practical comparisons and workflows for real applications.")
    ]

/// Device pixels per CSS pixel of the PNGs: the 1200 x 630 cards are written at 2400 x 1260, sharp on high-density screens.
let scale = 2.

/// Paints each card in a headless 1200 x 630 host and returns its PNG at <c>scale</c>.
let renderAll (selected: Entry list) =
    use host = WebCoreHost.Create (1200, 630)
    use http = new HttpClient ()
    let mutable pending = 0
    let failures = Collections.Concurrent.ConcurrentQueue<string>()

    // The host loads nothing by itself: the stylesheet and the font files it names come through this provider.
    let provider: Ranvier.Dom.ResourceProvider =
        fun request complete ->
            Interlocked.Increment &pending |> ignore

            Tasks.Task.Run (fun () ->
                try
                    try
                        use response = http.GetAsync(request.Url).Result.EnsureSuccessStatusCode()
                        let kind = response.Content.Headers.ContentType.MediaType

                        complete (
                            Ok
                                {
                                    ContentType = kind
                                    Bytes = response.Content.ReadAsByteArrayAsync().Result
                                }
                        )
                    with error ->
                        failures.Enqueue $"{request.Url}: {error.Message}"
                        complete (Error error.Message)
                finally
                    Interlocked.Decrement &pending |> ignore)
            |> ignore

            { new IDisposable with
                member _.Dispose() = ()
            }

    use _resources = Resources.forHost host provider
    let fonts = Fonts.forHost host
    use snapshots = Snapshots.forHost host
    use graph = new Graph ()
    host.Document.body.setAttribute ("style", "margin:0;overflow:hidden")

    // Runs the host until every load has settled and the fonts are ready for five quiet cycles in a row.
    let settle (key: string) =
        let clock = Stopwatch.StartNew ()
        let mutable quiet = 0

        while quiet < 5 do
            if clock.Elapsed.TotalSeconds > 60. then
                failwith $"{key}: resources did not settle within 60 s"

            host.Cycle 20
            host.Frame ()

            quiet <-
                if
                    pending = 0
                    && fonts.Status = Ranvier.Dom.FontSetStatus.Loaded
                then
                    quiet + 1
                else
                    0

    let capture (key: string) =
        let mutable outcome = None

        let options =
            { Ranvier.Dom.SnapshotContract.defaults with
                Scale = Some scale
                Encoding = Ranvier.Dom.ImageEncoding.Png
            }

        use _operation =
            snapshots.Capture (Ranvier.Dom.SnapshotSubject.Viewport, options, fun result -> outcome <- Some result)

        while outcome.IsNone do
            host.Cycle 10

        match outcome.Value with
        | Ranvier.Dom.ResourceOutcome.Succeeded image -> Array.copy image.Data
        | Ranvier.Dom.ResourceOutcome.Failed failure -> failwith $"{key}: snapshot failed: {failure}"

    [
        for card in selected do
            let drawn = Card ()
            card.Draw drawn
            let definition = Component.template (fun _ () -> ()) (page drawn)

            let mounted =
                Mounting.mount graph None (fun errors -> raise (AggregateException (Seq.cast<exn> errors))) host.Document.body None (fun context ->
                    Program.run (Component.render definition ()) context.Backend)

            settle card.Key
            let png = capture card.Key
            mounted.Dispose ()

            for failure in failures do
                eprintfn $"warning: {failure}"

            failures.Clear ()
            card, png
    ]

let requested = fsi.CommandLineArgs |> Array.skip 1 |> Set.ofArray

let unknown =
    requested
    - (cards |> List.map _.Key |> Set.ofList)

if not unknown.IsEmpty then
    failwith $"""Unknown card(s): {String.Join (", ", unknown)}. Cards: {String.Join (", ", cards |> List.map _.Key)}"""

for card, png in
    renderAll (
        cards
        |> List.filter (fun c -> requested.IsEmpty || requested.Contains c.Key)
    ) do
    let target = Path.Combine (brandRoot, "og", card.File + ".png")
    File.WriteAllBytes (target, png)

    if card.Docs then
        File.WriteAllBytes (Path.Combine (repoRoot, "docs", "static", "og", card.Key + ".png"), png)

    let copy =
        if card.Docs then
            $" and docs/static/og/{card.Key}.png"
        else
            ""

    printfn $"{card.Key}: {Path.GetRelativePath (repoRoot, target)}{copy}"
