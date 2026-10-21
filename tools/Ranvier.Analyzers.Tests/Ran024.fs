module Ranvier.Analyzers.Tests.Ran024

open Expecto
open Ranvier.Analyzers
open Ranvier.Analyzers.Tests.Options

let private header =
    "module Fixture\nopen Ranvier\nopen Ranvier.View.Foundation\nopen Ranvier.View.Foundation.Representation\nopen Ranvier.View.Foundation.Template\nopen Ranvier.View.Foundation.Sugar\n"

let private findings (body: string) =
    run Analyzers.ran024Cli (context (ranvier.Force ()) (header + body))

let private codes body =
    findings body |> List.map _.Code

[<Tests>]
let tests =
    testList
        "RAN024"
        [
            test "a script element, with its exact message" {
                Expect.equal
                    (findings "let c = Component.define (fun _ () -> ()) (fun t -> t.script [||] [||])\n"
                     |> List.map _.Message)
                    [
                        "`script` is refused by every render (\"Script elements are not created by templates or typed views\"). Load scripts through the server document shell, for example `Server.Page` scripts."
                    ]
                    "one message"
            }

            test "the finding's range is the element" {
                let body =
                    "let c = Component.define (fun _ () -> ()) (fun t -> t.script [||] [||])\n"

                let range = (findings body).Head.Range
                Expect.equal (textAt (header + body) range) "t.script [||] [||]" "the script element"
            }

            test "a style element" {
                Expect.equal (codes "let c = Component.define (fun _ () -> ()) (fun t -> t.style [||] [||])\n") [ "RAN024" ] "style"
            }

            test "a template element" {
                Expect.equal (codes "let c = Component.define (fun _ () -> ()) (fun t -> t.template [||] [||])\n") [ "RAN024" ] "template"
            }

            test "tr directly in table" {
                Expect.equal (codes "let c = Component.template (fun _ () -> ()) (table () { tr () { td () { \"x\" } } })\n") [ "RAN024" ] "table-row"
            }

            test "tr in tbody in table" {
                Expect.isEmpty
                    (codes "let c = Component.template (fun _ () -> ()) (table () { tbody () { tr () { td () { \"x\" } } } })\n")
                    "parsed as written"
            }

            test "a div in a div" {
                Expect.isEmpty (codes "let c = Component.define (fun _ () -> ()) (fun t -> t.div [||] [| t.div [||] [||] |])\n") "valid nesting"
            }

            test "nesting through a child component is not inspected" {
                let body =
                    "let row = Component.template (fun _ () -> ()) (tr () { td () { \"x\" } })\nlet c = Component.template (fun _ () -> ()) (table () { renderOf row () })\n"

                Expect.isEmpty (codes body) "false negative by design"
            }
        ]
