module Ranvier.View.Twin.Tests.HtmlTests

open Expecto
open Ranvier.View.Twin

let private roundTrip (html: string) =
    use document = new TwinDocument ()
    Html.inner (Html.parse document html)

let private rejects (html: string) (construct: string) =
    use document = new TwinDocument ()

    Expect.throwsC (fun () -> Html.parse document html |> ignore) (fun e ->
        match e with
        | Html.TwinParseException (found, line, column) ->
            Expect.stringContains found construct "construct"
            Expect.isGreaterThan line 0 "line"
            Expect.isGreaterThan column 0 "column"
        | other -> failtestf "unexpected %O" other)

[<Tests>]
let tests =
    testList
        "Html"
        [
            testCase "markers, attributes and void elements round trip"
            <| fun () ->
                let html =
                    "<!--[r--><div class=\"panel\" title=\"T &amp;\">Hello <input value=\"x\"><!--]--></div>"

                Expect.equal (roundTrip html) html "round trip"

            testCase "unquoted and single-quoted attribute values parse"
            <| fun () -> Expect.equal (roundTrip "<p a=1 b='2'></p>") "<p a=\"1\" b=\"2\"></p>" "normalised quotes"

            testCase "tags and attribute names are lowercased"
            <| fun () -> Expect.equal (roundTrip "<DIV ID=\"a\"></DIV>") "<div id=\"a\"></div>" "lowercased"

            testCase "character references decode"
            <| fun () -> Expect.equal (roundTrip "<p>&lt;&gt;&amp;&quot;&apos;&nbsp;&#65;&#x42;</p>") "<p>&lt;&gt;&amp;\"'&nbsp;AB</p>" "decoded"

            testCase "raw text and RCDATA keep their content"
            <| fun () ->
                Expect.equal (roundTrip "<script>if (a < b) {}</script>") "<script>if (a < b) {}</script>" "script raw"
                Expect.equal (roundTrip "<textarea>&lt;b&gt;</textarea>") "<textarea>&lt;b&gt;</textarea>" "textarea decodes"

            testCase "the leading line feed of pre and textarea is dropped"
            <| fun () ->
                Expect.equal (roundTrip "<pre>\nx</pre>") "<pre>x</pre>" "pre"
                Expect.equal (roundTrip "<textarea>\ny</textarea>") "<textarea>y</textarea>" "textarea"

            testCase "template content parses into the content fragment"
            <| fun () ->
                use document = new TwinDocument ()
                let fragment = Html.parse document "<template><b>x</b></template>"
                let template = fragment.FirstChild :?> TwinElement
                Expect.isEmpty template.Children "no direct children"
                Expect.equal (Html.inner template.Content) "<b>x</b>" "content"
                Expect.equal (Html.inner fragment) "<template><b>x</b></template>" "serialised"

            testCase "serialisers escape the jsdom and server sets"
            <| fun () ->
                use document = new TwinDocument ()
                let p = document.CreateElement "p"
                p.SetAttribute ("title", "a&b\"c <d>\re")

                p.AppendChild (document.CreateText "x&y<z> \"q\r")
                |> ignore

                let host = document.CreateElement "div"
                host.AppendChild p |> ignore
                Expect.equal (Html.inner host) "<p title=\"a&amp;b&quot;c&nbsp;<d>\re\">x&amp;y&lt;z&gt;&nbsp;\"q\r</p>" "jsdom"
                Expect.equal (Html.server host) "<p title=\"a&amp;b&quot;c &lt;d&gt;&#13;e\">x&amp;y&lt;z&gt; \"q&#13;</p>" "server"
                Expect.equal (Html.server (Html.parse document (Html.server host))) (Html.server host) "server round trip"

            testCase "carriage returns in the input are normalised to line feeds"
            <| fun () -> Expect.equal (roundTrip "<p>a\r\nb\rc</p>") "<p>a\nb\nc</p>" "normalised"

            testCase "server text with a leading line feed in pre keeps its doubled line feed"
            <| fun () ->
                use document = new TwinDocument ()
                let s = "<pre>\n\nx</pre>"
                Expect.equal (Html.server (Html.parse document s)) s "Server writes a second line feed"

            testCase "an implied end tag is rejected"
            <| fun () -> rejects "<p>a<div>b</div></p>" "implied end tag"

            testCase "a table without tbody is rejected"
            <| fun () -> rejects "<table><tr><td>a</td></tr></table>" "<table> without <tbody>"

            testCase "text inside a table is rejected as foster parenting"
            <| fun () -> rejects "<table>x</table>" "foster parenting"

            testCase "a named reference outside the set is rejected"
            <| fun () -> rejects "<p>&copy;</p>" "&copy;"

            testCase "a mismatched end tag is rejected"
            <| fun () -> rejects "<b><i>x</b></i>" "end tag </b>"
        ]
