/// <summary>One selector engine over the DOM interfaces, for the testing API on both hosts and the twin's own queries.</summary>
module Ranvier.View.Testing.Selector

open Fable.Core.TS.Dom

/// <summary>Selector syntax outside the supported set, at a 0-based column.</summary>
exception SelectorException of selector: string * column: int with
    override this.Message =
        $"The selector '{this.selector}' is not supported at column {this.column}"

type private Operator =
    | Exists
    | Equals
    | Includes
    | Prefix
    | Suffix
    | Contains

type private Simple =
    | Tag of string
    | Universal
    | Id of string
    | Class of string
    | Attribute of name: string * operator: Operator * value: string
    | Checked
    | Disabled
    | FirstChild
    | LastChild
    | NthChild of int
    | Not of Simple

type private Combinator =
    | Descendant
    | Child

/// <summary>A selector list; each complex selector holds its compounds right to left with the combinator to the next.</summary>
type Parsed = private Parsed of (Simple list * Combinator) list list

let private isNameChar (c: char) =
    System.Char.IsLetterOrDigit c
    || c = '-'
    || c = '_'

let parse (selector: string) : Parsed =
    let fail column =
        raise (SelectorException (selector, column))

    let i = ref 0

    let peek () =
        if i.Value < selector.Length then
            selector[i.Value]
        else
            '\000'

    let name () =
        let start = i.Value

        while i.Value < selector.Length
              && isNameChar selector[i.Value] do
            i.Value <- i.Value + 1

        if i.Value = start then
            fail start

        selector.Substring (start, i.Value - start)

    let skipSpace () =
        while peek () = ' ' do
            i.Value <- i.Value + 1

    let quoted () =
        match peek () with
        | '"'
        | '\'' as q ->
            i.Value <- i.Value + 1
            let start = i.Value

            while i.Value < selector.Length
                  && selector[i.Value] <> q do
                i.Value <- i.Value + 1

            if i.Value >= selector.Length then
                fail start

            let value = selector.Substring (start, i.Value - start)
            i.Value <- i.Value + 1
            value
        | _ -> name ()

    let rec simple () : Simple =
        let start = i.Value

        match peek () with
        | '#' ->
            i.Value <- i.Value + 1
            Id (name ())
        | '.' ->
            i.Value <- i.Value + 1
            Class (name ())
        | '*' ->
            i.Value <- i.Value + 1
            Universal
        | '[' ->
            i.Value <- i.Value + 1
            skipSpace ()
            let attribute = (name ()).ToLowerInvariant()
            skipSpace ()

            let operator =
                match peek () with
                | ']' -> Exists
                | '=' -> Equals
                | '~' -> Includes
                | '^' -> Prefix
                | '$' -> Suffix
                | '*' -> Contains
                | _ -> fail i.Value

            let value =
                if operator = Exists then
                    ""
                else
                    i.Value <- i.Value + (if operator = Equals then 1 else 2)

                    if operator <> Equals && selector[i.Value - 1] <> '=' then
                        fail (i.Value - 1)

                    skipSpace ()
                    quoted ()

            skipSpace ()

            if peek () <> ']' then
                fail i.Value

            i.Value <- i.Value + 1
            Attribute (attribute, operator, value)
        | ':' ->
            i.Value <- i.Value + 1

            match name () with
            | "checked" -> Checked
            | "disabled" -> Disabled
            | "first-child" -> FirstChild
            | "last-child" -> LastChild
            | "nth-child" ->
                if peek () <> '(' then
                    fail i.Value

                i.Value <- i.Value + 1
                let n = name ()

                if peek () <> ')' then
                    fail i.Value

                i.Value <- i.Value + 1

                match System.Int32.TryParse n with
                | true, value -> NthChild value
                | false, _ -> fail (i.Value - n.Length - 1)
            | "not" ->
                if peek () <> '(' then
                    fail i.Value

                i.Value <- i.Value + 1
                let inner = simple ()

                if peek () <> ')' then
                    fail i.Value

                i.Value <- i.Value + 1
                Not inner
            | _ -> fail start
        | c when isNameChar c -> Tag ((name ()).ToLowerInvariant())
        | _ -> fail start

    let compound () =
        let parts = ResizeArray<Simple>()
        parts.Add (simple ())

        while i.Value < selector.Length
              && (match peek () with
                  | '#'
                  | '.'
                  | '['
                  | ':'
                  | '*' -> true
                  | _ -> false) do
            parts.Add (simple ())

        List.ofSeq parts

    let complex () =
        let compounds = ResizeArray<Simple list * Combinator>()
        skipSpace ()
        let mutable current = compound ()
        let mutable going = true

        while going do
            let before = i.Value
            skipSpace ()

            match peek () with
            | '>' ->
                i.Value <- i.Value + 1
                skipSpace ()
                compounds.Add ((current, Child))
                current <- compound ()
            | ','
            | '\000' -> going <- false
            | _ when i.Value > before ->
                compounds.Add ((current, Descendant))
                current <- compound ()
            | _ -> fail i.Value

        compounds.Add ((current, Descendant))
        compounds |> Seq.rev |> List.ofSeq

    let list = ResizeArray<(Simple list * Combinator) list>()
    list.Add (complex ())

    while peek () = ',' do
        i.Value <- i.Value + 1
        list.Add (complex ())

    if i.Value < selector.Length then
        fail i.Value

    Parsed (List.ofSeq list)

let private parentElement (element: Element) : Element =
    match element.parentNode with
    | Some parent when (unbox<Node> parent).nodeType = 1.0 -> unbox<Element> parent
    | _ -> Unchecked.defaultof<_>

let private siblings (element: Element) : Element list =
    match element.parentNode with
    | Some parent ->
        let mutable child = (unbox<Node> parent).firstChild
        let result = ResizeArray<Element>()

        let mutable going = true

        while going do
            match child with
            | Some value ->
                let node = unbox<Node> value

                if node.nodeType = 1.0 then
                    result.Add (unbox<Element> node)

                child <- node.nextSibling
            | None -> going <- false

        List.ofSeq result
    | None -> [ element ]

let private attribute (element: Element) (name: string) =
    Option.toObj (element.getAttribute name)

let rec private test (element: Element) (simple: Simple) : bool =
    match simple with
    | Tag tag -> element.localName = tag
    | Universal -> true
    | Id id -> attribute element "id" = id
    | Class name ->
        match attribute element "class" with
        | null -> false
        | classes ->
            classes.Split ([| ' '; '\t'; '\n' |], System.StringSplitOptions.RemoveEmptyEntries)
            |> Array.contains name
    | Attribute (name, operator, value) ->
        match attribute element name, operator with
        | null, _ -> false
        | _, Exists -> true
        | actual, Equals -> actual = value
        | actual, Includes -> actual.Split ' ' |> Array.contains value
        | actual, Prefix -> value <> "" && actual.StartsWith value
        | actual, Suffix -> value <> "" && actual.EndsWith value
        | actual, Contains -> value <> "" && actual.Contains value
    | Checked ->
        match element.localName with
        | "input" -> (unbox<HTMLInputElement> element).``checked``
        | "option" -> (unbox<HTMLOptionElement> element).selected
        | _ -> false
    | Disabled ->
        List.contains element.localName [ "button"; "input"; "select"; "textarea"; "option"; "optgroup"; "fieldset" ]
        && not (isNull (attribute element "disabled"))
    | FirstChild -> obj.ReferenceEquals (List.tryHead (siblings element) |> Option.toObj, element)
    | LastChild -> obj.ReferenceEquals (List.tryLast (siblings element) |> Option.toObj, element)
    | NthChild n ->
        siblings element
        |> List.tryItem (n - 1)
        |> Option.exists (fun e -> obj.ReferenceEquals (e, element))
    | Not inner -> not (test element inner)

let rec private matchFrom (element: Element) (compounds: (Simple list * Combinator) list) : bool =
    match compounds with
    | [] -> true
    | (simples, _) :: rest when List.forall (test element) simples ->
        match rest with
        | [] -> true
        | (_, _) :: _ ->
            let _, combinator = List.head compounds

            match combinator with
            | Child -> let parent = parentElement element in not (isNull (box parent)) && matchFrom parent rest
            | Descendant ->
                let mutable ancestor = parentElement element
                let mutable found = false

                while not found && not (isNull (box ancestor)) do
                    found <- matchFrom ancestor rest
                    ancestor <- parentElement ancestor

                found
    | _ -> false

let matches (Parsed list) (element: Element) : bool =
    list |> List.exists (matchFrom element)

let queryAll (root: Node) (selector: string) : Element array =
    let parsed = parse selector
    let result = ResizeArray<Element>()

    let rec walk (node: Node) =
        let mutable child = node.firstChild

        let mutable going = true

        while going do
            match child with
            | None -> going <- false
            | Some value ->
                let current = unbox<Node> value

                if current.nodeType = 1.0 then
                    let element = unbox<Element> current

                    if matches parsed element then
                        result.Add element

                    walk current

                child <- current.nextSibling

    walk root
    result.ToArray ()

let query (root: Node) (selector: string) : Element option =
    queryAll root selector |> Array.tryHead
