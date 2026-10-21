module Ranvier.View.Testing.Query

open Fable.Core.TS.Dom

/// <summary>A query that requires one match found <c>count</c>; <c>excerpt</c> is a dump of the root.</summary>
exception QueryException of selector: string * count: int * excerpt: string with
    override this.Message =
        $"Expected one match for '{this.selector}', found {this.count}.{this.excerpt}"

#if FABLE_COMPILER
let mutable private excerpt: Node -> string = Unchecked.defaultof<_>

let private readExcerpt () = excerpt

let private writeExcerpt (value: Node -> string) =
    excerpt <- value
#else
// Per thread, as the conformance harness and the twin documents are.
[<Sealed; AbstractClass>]
type private ExcerptSlot =
    [<System.ThreadStatic; DefaultValue>]
    static val mutable private current: Node -> string

    static member Current
        with get () = ExcerptSlot.current
        and set (value: Node -> string) = ExcerptSlot.current <- value

let private readExcerpt () =
    ExcerptSlot.Current

let private writeExcerpt (value: Node -> string) =
    ExcerptSlot.Current <- value
#endif

/// <summary>Sets the dump excerpt appended to query and expectation failures on this thread.</summary>
let internal setExcerpt (excerptOf: Node -> string) =
    writeExcerpt excerptOf

/// <summary>The dump excerpt of <c>root</c> appended to query and expectation failures; empty until one is set.</summary>
let internal excerptOf (root: Node) : string =
    match box (readExcerpt ()) with
    | null -> ""
    | _ -> (readExcerpt ()) root

let query (root: Node) (selector: string) : Element option =
    Selector.query root selector

let queryAll (root: Node) (selector: string) : Element array =
    Selector.queryAll root selector

/// <summary>The single match; raises <c>QueryException</c> with the match count and a dump of the root otherwise.</summary>
let get (root: Node) (selector: string) : Element =
    match queryAll root selector with
    | [| element |] -> element
    | found -> raise (QueryException (selector, found.Length, excerptOf root))

/// <summary>The element's <c>textContent</c>, empty when it is null.</summary>
let textOf (element: Element) : string =
    match element.textContent with
    | null -> ""
    | text -> text

let private normalised (text: string) =
    System.String.Join (" ", text.Split ([| ' '; '\t'; '\n'; '\r'; '\f' |], System.StringSplitOptions.RemoveEmptyEntries))

/// <summary>
/// The innermost element whose whitespace-normalised <c>textContent</c> equals <c>text</c>; raises
/// <c>QueryException</c> unless exactly one element qualifies.
/// </summary>
let byText (root: Node) (text: string) : Element =
    let candidates =
        queryAll root "*"
        |> Array.filter (fun e -> normalised (textOf e) = text)

    let innermost =
        candidates
        |> Array.filter (fun e ->
            not (
                candidates
                |> Array.exists (fun other ->
                    not (obj.ReferenceEquals (other, e))
                    && e.contains (other :> Node))
            ))

    match innermost with
    | [| element |] -> element
    | found -> raise (QueryException ($"text \"{text}\"", found.Length, excerptOf root))
