module Ranvier.Analyzers.Tests.Ran021

open Expecto
open FSharp.Analyzers.SDK
open Ranvier.Analyzers
open Ranvier.Analyzers.Tests.Options

let private header =
    "module Fixture\nopen System\nopen Ranvier\nopen Ranvier.View.Foundation\nopen Ranvier.View.Foundation.Representation\nopen Ranvier.View.Foundation.Template\nopen Ranvier.View.Foundation.Sugar\ntype Todo = { Id: int; Label: string }\ntype Plain(id: int) =\n    member _.Id = id\nlet todos = createSignal [| { Id = 1; Label = \"a\" } |]\nlet row = li () { fun (row: unit -> Todo) -> (row ()).Label }\n"

let private findings (key: string) =
    let body =
        $"let view = Component.template (fun _ () -> todos) (ul () {{ forEach ({key}) (fun (s: Signal<Todo array>) -> s.Value) row }})\n"

    run Analyzers.ran021Cli (context (ranvier.Force ()) (header + body))

let private severities key =
    findings key |> List.map _.Severity

[<Tests>]
let tests =
    testList
        "RAN021"
        [
            test "a positional key, with its exact message" {
                Expect.equal
                    (findings "fun (todo: Todo) -> Array.IndexOf (todos.Peek, todo)"
                     |> List.map _.Message)
                    [
                        "This key is the item's position (`Array.IndexOf`), so moving or inserting an item remounts every row after it. Key by a value unique to the item, such as an id."
                    ]
                    "one message"
            }

            test "the finding's range is the key function" {
                let range = (findings "fun (_: Todo) -> Guid.NewGuid ()").Head.Range
                Expect.equal (range.StartLine, range.StartColumn, range.EndColumn) (13, 68, 100) "the key lambda"
            }

            test "Array.findIndex is positional" {
                Expect.equal (severities "fun (todo: Todo) -> Array.findIndex ((=) todo) todos.Peek") [ Severity.Warning ] "findIndex"
            }

            test "Guid.NewGuid makes a key per pass" { Expect.equal (severities "fun (_: Todo) -> Guid.NewGuid ()") [ Severity.Warning ] "NewGuid" }

            test "a module mutable written by the key makes a key per pass" {
                let source =
                    header
                    + "let mutable next = 0\nlet view = Component.template (fun _ () -> todos) (ul () { forEach (fun (_: Todo) -> next <- next + 1; next) (fun (s: Signal<Todo array>) -> s.Value) row })\n"

                Expect.equal
                    (run Analyzers.ran021Cli (context (ranvier.Force ()) source)
                     |> List.map _.Severity)
                    [ Severity.Warning ]
                    "captured mutable"
            }

            test "a reactive read in the key" {
                let source = header + "let offset = createSignal 0\n"

                let body =
                    "let view = Component.template (fun _ () -> todos) (ul () { forEach (fun (todo: Todo) -> todo.Id + offset.Value) (fun (s: Signal<Todo array>) -> s.Value) row })\n"

                Expect.equal
                    (run Analyzers.ran021Cli (context (ranvier.Force ()) (source + body))
                     |> List.map _.Severity)
                    [ Severity.Warning ]
                    "tracked key"
            }

            test "forEach keyed by the record item itself is Info" {
                Expect.equal (severities "fun (todo: Todo) -> todo") [ Severity.Info ] "identity"
            }

            test "a key compared by reference is Info" {
                let source =
                    header
                    + "let plainItems = createSignal [| Plain 1 |]\nlet plainRow = li () { \"x\" }\n"

                let body =
                    "let view = Component.template (fun _ () -> plainItems) (ul () { forEach (fun (item: Plain) -> item) (fun (s: Signal<Plain array>) -> s.Value) plainRow })\n"

                Expect.equal
                    (run Analyzers.ran021Cli (context (ranvier.Force ()) (source + body))
                     |> List.map _.Severity)
                    [ Severity.Info ]
                    "reference key"
            }

            test "an id key" { Expect.isEmpty (findings "fun (todo: Todo) -> todo.Id") "stable" }

            test "a string list keyed by id is correct" {
                let source =
                    header
                    + "let names = createSignal [| \"a\" |]\nlet nameRow = li () { \"x\" }\n"

                let body =
                    "let view = Component.template (fun _ () -> names) (ul () { forEach id (fun (s: Signal<string array>) -> s.Value) nameRow })\n"

                Expect.isEmpty (run Analyzers.ran021Cli (context (ranvier.Force ()) (source + body))) "strings"
            }

            test "a ref cell written by the key makes a key per pass" {
                let source =
                    header
                    + "let counter = ref 0\nlet view = Component.template (fun _ () -> todos) (ul () { forEach (fun (_: Todo) -> counter.Value <- counter.Value + 1; counter.Value) (fun (s: Signal<Todo array>) -> s.Value) row })\n"

                Expect.equal
                    (run Analyzers.ran021Cli (context (ranvier.Force ()) source)
                     |> List.map _.Severity)
                    [ Severity.Warning ]
                    "ref cell"
            }

            test "a tuple key is stable" { Expect.isEmpty (findings "fun (todo: Todo) -> todo.Id, todo.Label") "structural" }

            test "t.each with a positional key" {
                let body =
                    "let rowDef = Component.define (fun _ (row: unit -> Todo) -> row) (fun t -> t.li [||] [||])\nlet view = Component.define (fun _ () -> todos) (fun t -> t.ul [||] [| t.each (fun (todo: Todo) -> Array.IndexOf (todos.Peek, todo)) (fun (s: Signal<Todo array>) -> s.Value) rowDef |])\n"

                Expect.equal
                    (run Analyzers.ran021Cli (context (ranvier.Force ()) (header + body))
                     |> List.map _.Code)
                    [ "RAN021" ]
                    "builder member"
            }
        ]
