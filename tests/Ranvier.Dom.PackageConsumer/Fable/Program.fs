module DomPackageConsumer

open Fable.Core
open Ranvier.Dom

[<Emit("({ nodeName: 'DIV' })")>]
let private node () : Node = jsNative

[<Emit("({ style: { cssText: '' } })")>]
let private styled () : ElementCSSInlineStyle = jsNative

[<Emit("({ defaultView: { CSS: { supports(...args) { return args.length === 2 ? args[0] === 'display' && args[1] === 'block' : args[0] === '(display: block)'; } } } })")>]
let private queryDocument () : Document = jsNative

[<Emit("""(() => {
    const document = { defaultView: { getComputedStyle(element, pseudo) {
        return { getPropertyValue(property) { return property === 'width' ? (pseudo === '::before' ? '11px' : '120px') : ''; } };
    } } };
    return [document, { ownerDocument: document }];
})()""")>]
let private computedFixture () : Document * Element = jsNative

[<Emit("""(() => {
    const rectangle = { x:10.25, y:20.5, width:30.75, height:12 };
    const fragments = { length:1, item(index) { return index === 0 ? rectangle : null; } };
    return [{ getClientRects() { return fragments; } }, { getClientRects() { return fragments; } }];
})()""")>]
let private fragmentFixture () : Element * Ranvier.Dom.Range = jsNative

let private codeUnit (value: int) =
    string (char value)

/// <summary>Checks browser member access through the packaged DOM contracts.</summary>
[<EntryPoint>]
let main _ =
    if (node ()).nodeName <> "DIV" then
        failwith "The packaged DOM interface changed the browser member access"

    let inlineStyle = (styled ()).style
    inlineStyle.cssText <- "color: red"

    if inlineStyle.cssText <> "color: red" then
        failwith "The packaged inline style contract changed the browser property access"

    if
        CSS.escape "0a" <> "\\30 a"
        || CSS.escape "-1" <> "-\\31 "
    then
        failwith "CSS identifier digit escaping changed"

    if
        CSS.escape "a\u0000b" <> "a\ufffdb"
        || CSS.escape null <> "null"
    then
        failwith "CSS identifier full-string conversion changed"

    let surrogate = codeUnit 0xd800

    if CSS.escape surrogate <> surrogate then
        failwith "CSS escaping lost UTF-16 code units"

    let queries = CssQueries.forDocument (queryDocument ())

    if not (queries.Supports ("display", "block")) then
        failwith "The packaged declaration overload lost an argument"

    if not (queries.Supports "(display: block)") then
        failwith "The packaged condition overload changed"

    if queries.Supports ("display", "invalid") then
        failwith "The packaged declaration result changed"

    let document, element = computedFixture ()
    let computed = ComputedStyles.forDocument document

    if
        (computed.Get (element, None)).getPropertyValue "width"
        <> "120px"
    then
        failwith "The packaged computed-style element query changed"

    if
        (computed.Get (element, Some "::before")).getPropertyValue "width"
        <> "11px"
    then
        failwith "The packaged computed-style query lost its pseudo-element"

    printfn "DOM_PACKAGE_COMPUTED_STYLE PASS"
    let fragmentElement, fragmentRange = fragmentFixture ()
    let elementFragments = fragmentElement.getClientRects ()
    let rangeFragments = fragmentRange.getClientRects ()

    if
        elementFragments.length <> 1u
        || rangeFragments.length <> 1u
    then
        failwith "The packaged fragment query/list contract changed"

    if
        (elementFragments.item 0u).x <> 10.25
        || (rangeFragments.item 0u).width <> 30.75
    then
        failwith "The packaged rectangle contract lost double precision"

    if not (isNull (box (elementFragments.item 1u))) then
        failwith "The packaged fragment list lost nullable indexing"

    printfn "DOM_PACKAGE_FRAGMENTS PASS"
    printfn "DOM_PACKAGE_CSS_QUERIES PASS"
    printfn "DOM_PACKAGE_CSS_ESCAPE PASS"
    printfn "DOM_PACKAGE_BROWSER PASS"
    0
