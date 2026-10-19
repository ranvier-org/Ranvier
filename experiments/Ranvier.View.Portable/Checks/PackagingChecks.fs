/// <summary>Checks that the portable project compiles without a browser.</summary>
module PackagingChecks

open System
open System.IO
open System.Xml.Linq

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

let private portable = Path.GetFullPath (Path.Combine (__SOURCE_DIRECTORY__, ".."))

let private project () =
    XDocument.Load (Path.Combine (portable, "Ranvier.View.Portable.fsproj"))

let private items (name: string) =
    [
        for element in (project ()).Descendants(XName.Get name) do
            element.Attribute(XName.Get "Include").Value
    ]

let private browserReferences () =
    equal
        []
        (items "ProjectReference"
         |> List.filter (fun path -> path.Contains "Fable.Ranvier"))

let private browserEmits () =
    let browser (line: string) =
        line.Contains "Emit("
        && (line.Contains "document"
            || line.Contains "window"
            || line.Contains "addEventListener")

    let offending =
        [
            for file in items "Compile" do
                for line in File.ReadAllLines (Path.Combine (portable, file)) do
                    if browser line then
                        file + ": " + line.Trim ()
        ]

    equal [] offending

let cases: (string * (unit -> unit)) array =
    [|
        "the portable project references src/Ranvier and type bindings only", browserReferences
        "the portable project's emits touch neither document, window nor addEventListener", browserEmits
    |]
