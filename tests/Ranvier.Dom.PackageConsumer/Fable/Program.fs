module DomPackageConsumer

open Fable.Core
open Ranvier.Dom

[<Emit("({ nodeName: 'DIV' })")>]
let private node () : Node = jsNative

[<Emit("({ style: { cssText: '' } })")>]
let private styled () : ElementCSSInlineStyle = jsNative

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

    printfn "DOM_PACKAGE_CSS_ESCAPE PASS"
    printfn "DOM_PACKAGE_BROWSER PASS"
    0
