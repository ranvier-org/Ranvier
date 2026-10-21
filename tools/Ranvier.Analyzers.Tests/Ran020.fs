module Ranvier.Analyzers.Tests.Ran020

open Expecto
open Ranvier.Analyzers
open Ranvier.Analyzers.Tests.Options

let private header =
    "module Fixture\nopen Ranvier\nopen Ranvier.View.Foundation\nopen Ranvier.View.Foundation.Representation\nopen Ranvier.View.Foundation.Template\nopen Ranvier.View.Foundation.Sugar\nlet count = createSignal 0\n"

let private findings (body: string) =
    run Analyzers.ran020Cli (context (ranvier.Force ()) (header + body))

let private codes body =
    findings body |> List.map _.Code

[<Tests>]
let tests =
    testList
        "RAN020"
        [
            test "a static text yield, with its exact message" {
                Expect.equal
                    (findings "let c = Component.template (fun _ () -> ()) (div () { string count.Value })\n"
                     |> List.map _.Message)
                    [
                        "`count.Value` is read once, when the template is built, so this text never updates. Yield a reader: `fun _ -> string count.Value`."
                    ]
                    "one message"
            }

            test "the finding's range is the read" {
                let range =
                    (findings "let c = Component.template (fun _ () -> ()) (div () { string count.Value })\n").Head.Range

                Expect.equal (range.StartLine, range.StartColumn, range.EndColumn) (8, 61, 72) "count.Value"
            }

            test "t.text with a string" {
                Expect.equal
                    (codes "let c = Component.define (fun _ () -> ()) (fun t -> t.div [||] [| t.text (string count.Value) |])\n")
                    [ "RAN020" ]
                    "explicit text"
            }

            test "a static attribute" {
                Expect.equal
                    (codes "let c = Component.template (fun _ () -> ()) (div (Attrs.className (string count.Value)) { \"x\" })\n")
                    [ "RAN020" ]
                    "className value"
            }

            test "renderOf's props" {
                let body =
                    "let child = Component.template (fun _ (n: int) -> n) (span () { \"x\" })\nlet c = Component.template (fun _ () -> ()) (div () { renderOf child count.Value })\n"

                Expect.equal (codes body) [ "RAN020" ] "renderOf"
            }

            test "a setup that returns a read" {
                Expect.equal (codes "let c = Component.template (fun _ () -> count.Value) (div () { \"x\" })\n") [ "RAN020" ] "setup return"
            }

            test "render's props reader that returns a read" {
                let body =
                    "type S = { Count: Signal<int> }\nlet child = Component.template (fun _ (n: int) -> n) (span () { \"x\" })\nlet c = Component.template (fun _ (s: S) -> s) (div () { render child (fun s -> s.Count.Value) })\n"

                Expect.equal (codes body) [ "RAN020" ] "render props"
            }

            test "a reader yield" {
                Expect.isEmpty (codes "let c = Component.template (fun _ () -> count) (div () { fun (c: Signal<int>) -> string c.Value })\n") "reader"
            }

            test "a Source part" {
                Expect.isEmpty
                    (codes "let c = Component.template (fun _ () -> count) (div () { from (fun (c: Signal<int>) -> c) |> ignore; \"x\" })\n")
                    "from"
            }

            test "Peek in a static yield" {
                Expect.isEmpty (codes "let c = Component.template (fun _ () -> ()) (div () { string count.Peek })\n") "untracked"
            }

            test "a read in an event handler" {
                Expect.isEmpty
                    (codes
                        "let c = Component.template (fun _ () -> count) (button (Events.onClick (fun (c: Signal<int>) _ -> c.Value <- c.Value + 1)) { \"+\" })\n")
                    "handler"
            }

            test "setup reading a value to seed createSignal" {
                Expect.isEmpty
                    (codes
                        "type P = { Start: int }\nlet c = Component.template (fun _ (p: P) -> createSignal (count.Value + p.Start)) (div () { \"x\" })\n")
                    "seed"
            }

            test "the text fix type-checks and clears the finding" {
                let source =
                    header
                    + "let c = Component.template (fun _ () -> ()) (div () { string count.Value })\n"

                let fix =
                    (run Analyzers.ran020Cli (context (ranvier.Force ()) source)).Head.Fixes
                    |> List.exactlyOne

                let repaired = applyFix source fix
                Expect.isEmpty (run Analyzers.ran020Cli (context (ranvier.Force ()) repaired)) "no finding after the fix"
            }
        ]
