/// <summary>The twin's HTML: a parser for the subset Server writes and template markup holds, and two serialisers.</summary>
module Ranvier.View.Twin.Html

open System
open System.Collections.Generic
open System.Text

/// <summary>Markup outside the parser's subset; <c>construct</c> names it, at a 1-based line and column.</summary>
exception TwinParseException of construct: string * line: int * column: int with
    override this.Message =
        $"The DOM twin's parser does not accept {this.construct} (line {this.line}, column {this.column}); run this markup under jsdom"

let private voids =
    HashSet<string>
        [
            "area"
            "base"
            "br"
            "col"
            "embed"
            "hr"
            "img"
            "input"
            "link"
            "meta"
            "source"
            "track"
            "wbr"
        ]

let private rawText = HashSet<string> [ "script"; "style" ]
let private escapableRaw = HashSet<string> [ "textarea"; "title" ]

let private serialisedRaw =
    HashSet<string> [ "style"; "script"; "xmp"; "iframe"; "noembed"; "noframes"; "plaintext" ]

let private leadingLineFeed = HashSet<string> [ "pre"; "listing"; "textarea" ]

let private closesParagraph =
    HashSet<string>
        [
            "address"
            "article"
            "aside"
            "blockquote"
            "details"
            "div"
            "dl"
            "fieldset"
            "figcaption"
            "figure"
            "footer"
            "form"
            "h1"
            "h2"
            "h3"
            "h4"
            "h5"
            "h6"
            "header"
            "hr"
            "main"
            "menu"
            "nav"
            "ol"
            "p"
            "pre"
            "section"
            "table"
            "ul"
        ]

let private selfClosing =
    HashSet<string> [ "li"; "option"; "dd"; "dt"; "tr"; "td"; "th" ]

let private documentLevel = HashSet<string> [ "html"; "head"; "body" ]

let private tableContent =
    dict
        [
            "table",
            set
                [
                    "caption"
                    "colgroup"
                    "thead"
                    "tbody"
                    "tfoot"
                    "template"
                    "script"
                    "style"
                ]
            "tbody", set [ "tr"; "template"; "script"; "style" ]
            "thead", set [ "tr"; "template"; "script"; "style" ]
            "tfoot", set [ "tr"; "template"; "script"; "style" ]
            "tr", set [ "td"; "th"; "template"; "script"; "style" ]
        ]

let private named =
    dict [ "amp", "&"; "lt", "<"; "gt", ">"; "quot", "\""; "apos", "'"; "nbsp", " " ]

type private Reader(text: string) =
    let mutable index = 0
    member _.Index = index
    member _.AtEnd = index >= text.Length
    member _.Peek = if index < text.Length then text[index] else '\000'

    member _.PeekAt k =
        if index + k < text.Length then text[index + k] else '\000'

    member _.Advance n =
        index <- index + n

    member _.StartsWith(s: string) =
        String.CompareOrdinal (text, index, s, 0, s.Length) = 0

    member _.StartsWithIgnoreCase(s: string) =
        index + s.Length <= text.Length
        && String.Compare (text, index, s, 0, s.Length, StringComparison.OrdinalIgnoreCase) = 0

    member _.Position =
        let mutable line, column = 1, 1

        for i in 0 .. index - 1 do
            if text[i] = '\n' then
                line <- line + 1
                column <- 1
            else
                column <- column + 1

        line, column

    member this.Fail(construct: string) =
        let line, column = this.Position
        raise (TwinParseException (construct, line, column))

let private isAsciiLetter c =
    (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z')

let private isSpace c =
    c = ' ' || c = '\t' || c = '\n' || c = '\f'

/// <summary>Reads a character reference at <c>&amp;</c>; a bare <c>&amp;</c> stays literal.</summary>
let private reference (reader: Reader) (out: StringBuilder) =
    let codePoint (value: int) =
        if
            value = 0
            || value > 0x10FFFF
            || (value >= 0xD800 && value <= 0xDFFF)
        then
            "�"
        elif value >= 0x80 && value <= 0x9F then
            reader.Fail $"the numeric reference &#{value}; in the C1 range"
        else
            Char.ConvertFromUtf32 value

    if reader.PeekAt 1 = '#' then
        let hex = reader.PeekAt 2 = 'x' || reader.PeekAt 2 = 'X'
        let start = if hex then 3 else 2
        let digits = StringBuilder ()
        let mutable k = start

        while (if hex then
                   Uri.IsHexDigit (reader.PeekAt k)
               else
                   Char.IsAsciiDigit (reader.PeekAt k)) do
            digits.Append (reader.PeekAt k) |> ignore
            k <- k + 1

        if digits.Length = 0 || reader.PeekAt k <> ';' then
            reader.Fail "a numeric reference without digits or a semicolon"

        let value = Convert.ToInt32 (digits.ToString (), if hex then 16 else 10)
        out.Append (codePoint value) |> ignore
        reader.Advance (k + 1)
    elif isAsciiLetter (reader.PeekAt 1) then
        let name = StringBuilder ()
        let mutable k = 1

        while Char.IsAsciiLetterOrDigit (reader.PeekAt k) do
            name.Append (reader.PeekAt k) |> ignore
            k <- k + 1

        let name = name.ToString ()

        match named.TryGetValue name with
        | true, value when reader.PeekAt k = ';' ->
            out.Append value |> ignore
            reader.Advance (k + 1)
        | _ -> reader.Fail $"&{name};"
    else
        out.Append '&' |> ignore
        reader.Advance 1

let private normalise (html: string) =
    html.Replace("\r\n", "\n").Replace('\r', '\n')

let private parseNodes (parent: TwinNode) (html: string) =
    let document = parent.Document
    let reader = Reader (normalise html)
    let stack = Stack<TwinNode>()
    stack.Push parent

    let container (node: TwinNode) =
        match node with
        | :? TwinElement as e when e.LocalName = "template" -> e.Content
        | _ -> node

    let currentName () =
        match stack.Peek () with
        | :? TwinElement as e -> e.LocalName
        | _ -> ""

    let append (node: TwinNode) =
        (container (stack.Peek ())).AppendChild node
        |> ignore

    let appendText (text: string) =
        if text.Length > 0 then
            match tableContent.TryGetValue (currentName ()) with
            | true, _ when text |> Seq.exists (isSpace >> not) -> reader.Fail "foster parenting (text inside a table)"
            | _ ->
                match (container (stack.Peek ())).LastChild with
                | :? TwinCharacterData as last when last.Kind = NodeKind.Text -> last.Data <- last.Data + text
                | _ -> append (document.CreateText text)

    let readUntil (stop: string) (decode: bool) (dropLeadingLineFeed: bool) =
        let out = StringBuilder ()

        if dropLeadingLineFeed && reader.Peek = '\n' then
            reader.Advance 1

        while not (reader.AtEnd || reader.StartsWithIgnoreCase stop) do
            if decode && reader.Peek = '&' then
                reference reader out
            else
                out.Append reader.Peek |> ignore
                reader.Advance 1

        if reader.AtEnd then
            reader.Fail $"an unclosed {stop.Substring 2}"
        else
            out.ToString ()

    let readName () =
        let out = StringBuilder ()

        while not (
            reader.AtEnd
            || isSpace reader.Peek
            || reader.Peek = '/'
            || reader.Peek = '>'
            || reader.Peek = '='
        ) do
            out.Append (Char.ToLowerInvariant reader.Peek)
            |> ignore

            reader.Advance 1

        out.ToString ()

    let skipSpace () =
        while isSpace reader.Peek do
            reader.Advance 1

    let readAttributeValue () =
        let out = StringBuilder ()

        match reader.Peek with
        | '"'
        | '\'' as quote ->
            reader.Advance 1

            while not (reader.AtEnd || reader.Peek = quote) do
                if reader.Peek = '&' then
                    reference reader out
                else
                    out.Append reader.Peek |> ignore
                    reader.Advance 1

            if reader.AtEnd then
                reader.Fail "an unterminated attribute value"

            reader.Advance 1
        | _ ->
            while not (
                reader.AtEnd
                || isSpace reader.Peek
                || reader.Peek = '>'
            ) do
                if reader.Peek = '&' then
                    reference reader out
                else
                    out.Append reader.Peek |> ignore
                    reader.Advance 1

        out.ToString ()

    let appendRaw (element: TwinElement) (text: string) =
        if text.Length > 0 then
            element.AppendChild (document.CreateText text)
            |> ignore

    let startTag () =
        reader.Advance 1
        let name = readName ()

        if documentLevel.Contains name then
            reader.Fail $"the document-level tag <{name}>"

        let current = currentName ()

        if current = "p" && closesParagraph.Contains name then
            reader.Fail $"an implied end tag (<p> closed by <{name}>)"

        if selfClosing.Contains name && current = name then
            reader.Fail $"an implied end tag (<{name}> inside <{name}>)"

        match tableContent.TryGetValue current with
        | true, allowed when not (allowed.Contains name) ->
            if current = "table" && name = "tr" then
                reader.Fail "a <table> without <tbody>"
            else
                reader.Fail $"foster parenting (<{name}> inside <{current}>)"
        | _ -> ()

        let element = document.CreateElement name
        skipSpace ()

        while not (
            reader.AtEnd
            || reader.Peek = '>'
            || reader.StartsWith "/>"
        ) do
            let attribute = readName ()
            skipSpace ()

            let value =
                if reader.Peek = '=' then
                    reader.Advance 1
                    skipSpace ()
                    readAttributeValue ()
                else
                    ""

            if attribute.Length = 0 then
                reader.Fail "an empty attribute name"

            if not (element.HasAttribute attribute) then
                element.SetAttribute (attribute, value)

            skipSpace ()

        if reader.AtEnd then
            reader.Fail $"an unterminated <{name}> tag"

        reader.Advance (if reader.Peek = '/' then 2 else 1)
        append element

        if rawText.Contains name then
            appendRaw element (readUntil $"</{name}" false false)
            reader.Advance (name.Length + 2)
            skipSpace ()
            reader.Advance 1
        elif escapableRaw.Contains name then
            appendRaw element (readUntil $"</{name}" true (name = "textarea"))
            reader.Advance (name.Length + 2)
            skipSpace ()
            reader.Advance 1
        elif not (voids.Contains name) then
            stack.Push element

            if
                leadingLineFeed.Contains name
                && reader.Peek = '\n'
            then
                reader.Advance 1

    let endTag () =
        reader.Advance 2
        let name = readName ()
        skipSpace ()

        if reader.Peek <> '>' then
            reader.Fail $"an unterminated </{name}> tag"

        reader.Advance 1

        if voids.Contains name then
            reader.Fail $"the end tag </{name}> of a void element"

        if stack.Count = 1 || currentName () <> name then
            reader.Fail $"the end tag </{name}> that does not close <{currentName ()}>"

        stack.Pop () |> ignore

    let comment () =
        reader.Advance 4

        if reader.StartsWith ">" || reader.StartsWith "->" then
            reader.Fail "an abruptly closed comment"

        let out = StringBuilder ()

        while not (reader.AtEnd || reader.StartsWith "-->") do
            out.Append reader.Peek |> ignore
            reader.Advance 1

        if reader.AtEnd then
            reader.Fail "an unclosed comment"

        reader.Advance 3
        append (document.CreateComment (out.ToString ()))

    let text = StringBuilder ()

    let flush () =
        appendText (text.ToString ())
        text.Clear () |> ignore

    while not reader.AtEnd do
        if reader.StartsWith "<!--" then
            flush ()
            comment ()
        elif reader.StartsWithIgnoreCase "<!doctype html>" then
            reader.Advance 15
        elif reader.StartsWith "<!" || reader.StartsWith "<?" then
            reader.Fail "a bogus comment"
        elif
            reader.StartsWith "</"
            && isAsciiLetter (reader.PeekAt 2)
        then
            flush ()
            endTag ()
        elif
            reader.Peek = '<'
            && isAsciiLetter (reader.PeekAt 1)
        then
            flush ()
            startTag ()
        elif reader.Peek = '&' then
            reference reader text
        else
            text.Append reader.Peek |> ignore
            reader.Advance 1

    flush ()

    if stack.Count > 1 then
        reader.Fail $"an unclosed <{currentName ()}>"

/// <summary>Appends the nodes of <c>html</c> to <c>parent</c>, or to its content when <c>parent</c> is a template.</summary>
let parseInto (parent: TwinNode) (html: string) : unit =
    parseNodes parent html

let parse (document: TwinDocument) (html: string) : TwinFragment =
    let fragment = document.CreateFragment ()
    parseNodes fragment html
    fragment

let private jsdomText (text: string) =
    text.Replace("&", "&amp;").Replace(" ", "&nbsp;").Replace("<", "&lt;").Replace(">", "&gt;")

let private jsdomAttribute (value: string) =
    value.Replace("&", "&amp;").Replace(" ", "&nbsp;").Replace("\"", "&quot;")

let rec private write
    (out: StringBuilder)
    (text: string -> string)
    (attribute: string -> string)
    (server: bool)
    (parentName: string)
    (node: TwinNode)
    =
    match node with
    | :? TwinElement as e ->
        out.Append('<').Append e.LocalName |> ignore

        for name, value in e.Attributes do
            out.Append(' ').Append(name).Append("=\"").Append(attribute value).Append '"'
            |> ignore

        out.Append '>' |> ignore

        if not (voids.Contains e.LocalName) then
            let children =
                (if e.LocalName = "template" then
                     e.Content
                 else
                     e :> TwinNode)
                    .Children

            match children with
            | (:? TwinCharacterData as first) :: _ when
                server
                && leadingLineFeed.Contains e.LocalName
                && first.Kind = NodeKind.Text
                && first.Data.StartsWith "\n"
                ->
                out.Append '\n' |> ignore
            | _ -> ()

            for child in children do
                write out text attribute server e.LocalName child

            out.Append("</").Append(e.LocalName).Append '>'
            |> ignore
    | :? TwinCharacterData as c when c.Kind = NodeKind.Comment ->
        out.Append("<!--").Append(c.Data).Append "-->"
        |> ignore
    | :? TwinCharacterData as c ->
        out.Append (
            if serialisedRaw.Contains parentName then
                c.Data
            else
                text c.Data
        )
        |> ignore
    | _ ->
        for child in node.Children do
            write out text attribute server parentName child

let private serialise text attribute server (nodes: TwinNode list) (parentName: string) =
    let out = StringBuilder ()

    for node in nodes do
        write out text attribute server parentName node

    out.ToString ()

let private nameOf (node: TwinNode) =
    match node with
    | :? TwinElement as e -> e.LocalName
    | _ -> ""

let private childrenOf (node: TwinNode) =
    match node with
    | :? TwinElement as e when e.LocalName = "template" -> e.Content.Children
    | _ -> node.Children

/// <summary>The node in jsdom's serialisation: <c>outerHTML</c> for an element, the data of a text node escaped as text.</summary>
let outer (node: TwinNode) : string =
    serialise jsdomText jsdomAttribute false [ node ] (nameOf node.Parent)

/// <summary>The node's children in jsdom's serialisation, as <c>innerHTML</c> returns them.</summary>
let inner (node: TwinNode) : string =
    serialise jsdomText jsdomAttribute false (childrenOf node) (nameOf node)

/// <summary>The node's children in Server's escaping, with Server's doubled leading line feed in <c>pre</c>, <c>textarea</c> and <c>listing</c>.</summary>
let server (node: TwinNode) : string =
    serialise
        (Ranvier.View.Foundation.Representation.escapeHtml false)
        (Ranvier.View.Foundation.Representation.escapeHtml true)
        true
        (childrenOf node)
        (nameOf node)
