/// <summary>Checks that the portable project compiles without a browser.</summary>
module PackagingChecks

open System.IO
open System.Text.RegularExpressions
open System.Xml.Linq

let private equal expected actual =
    if expected <> actual then
        failwithf "Expected %A; got %A" expected actual

let private portable = Path.GetFullPath (Path.Combine (__SOURCE_DIRECTORY__, ".."))

let private project () =
    XDocument.Load (Path.Combine (portable, "Ranvier.View.Portable.fsproj"))

let private includes (element: string) (attribute: string) =
    [
        for node in (project ()).Descendants(XName.Get element) do
            match node.Attribute (XName.Get attribute) with
            | null -> ()
            | value -> value.Value.Replace ("\\", "/")
    ]

let private compiled () =
    includes "Compile" "Include"

let private browserReferences () =
    let expectedProjects = set [ "../../src/Ranvier/Ranvier.fsproj" ]

    let expectedPackages = set [ "Xantham.Fable.Core.TS" ]

    let extra (expected: Set<string>) (actual: string list) =
        actual
        |> List.filter (expected.Contains >> not)
        |> List.sort

    equal [] (extra expectedProjects (includes "ProjectReference" "Include"))

    equal [] (extra (expectedPackages.Add "FSharp.Core") (includes "PackageReference" "Include"))

let private browserAttribute =
    Regex (@"\[<\s*(?:[\w.]*\.)?(Emit|Import|ImportAll|ImportDefault|ImportMember|Global)\b.*?>\]", RegexOptions.Singleline)

let private browserEmits () =
    let touchesBrowser (found: Match) =
        let attribute = found.Value

        found.Groups[1].Value <> "Emit"
        || attribute.Contains "document"
        || attribute.Contains "window"
        || attribute.Contains "addEventListener"

    let offending =
        [
            for file in compiled () do
                // Native.fs holds the host primitives; each Emit there has a .NET body, which nativeBodies checks.
                if file <> "Native.fs" then
                    let text =
                        File.ReadAllLines (Path.Combine (portable, file))
                        |> Array.filter (fun line -> not (line.TrimStart().StartsWith "//"))
                        |> String.concat "\n"

                    for found in browserAttribute.Matches text do
                        if touchesBrowser found then
                            file + ": " + found.Value
        ]

    equal [] offending

/// <summary>Every <c>#if FABLE_COMPILER</c> block of <c>Native.fs</c> has an <c>#else</c>, and the file has no Import or Global attribute.</summary>
let private nativeBodies () =
    let lines =
        File.ReadAllLines (Path.Combine (portable, "Native.fs"))
        |> Array.map _.Trim()

    let fableBlocks =
        lines
        |> Array.filter ((=) "#if FABLE_COMPILER")
        |> Array.length

    let netBlocks =
        lines
        |> Array.filter ((=) "#else")
        |> Array.length

    equal fableBlocks netBlocks

    let text = String.concat "\n" lines

    let imports =
        [
            for found in browserAttribute.Matches text do
                if found.Groups[1].Value <> "Emit" then
                    found.Value
        ]

    equal [] imports

let cases: (string * (unit -> unit)) array =
    [|
        "the portable project references only src/Ranvier and the Xantham type bindings", browserReferences
        "the portable project's attributes use no Import or Global, and no Emit outside Native.fs touches document, window or addEventListener",
        browserEmits
        "each Emit block of Native.fs has a .NET body, and Native.fs uses no Import or Global", nativeBodies
    |]
