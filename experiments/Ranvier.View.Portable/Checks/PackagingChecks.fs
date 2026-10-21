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
                // Native.fs is checked by nativeStructure.
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

let private fableBlock =
    Regex (
        @"^[ \t]*#if FABLE_COMPILER[ \t]*\r?$(?<fable>.*?)^[ \t]*#else[ \t]*\r?$(?<net>.*?)^[ \t]*#endif",
        RegexOptions.Singleline ||| RegexOptions.Multiline
    )

let private topLevelLet =
    Regex (@"^let (?:inline )?(?:private )?(?:internal )?(?:rec )?(\w+)", RegexOptions.Multiline)

/// <summary>
/// The violations of Native.fs's shape in <c>text</c>: each <c>#if FABLE_COMPILER</c> block has an <c>#else</c> that
/// defines the same top-level names, every attribute is an Emit inside a Fable branch, and every guard naming
/// <c>FABLE_COMPILER</c> is <c>#if FABLE_COMPILER</c> or <c>#if !FABLE_COMPILER</c>.
/// </summary>
let nativeStructure (text: string) : string list =
    let text =
        text.Split '\n'
        |> Array.filter (fun line -> not (line.TrimStart().StartsWith "//"))
        |> String.concat "\n"

    let blocks = [ for found in fableBlock.Matches text -> found ]

    let names (group: Group) =
        set [ for found in topLevelLet.Matches group.Value -> found.Groups[1].Value ]

    let inFable (index: int) =
        blocks
        |> List.exists (fun block ->
            let fable = block.Groups["fable"]

            index >= fable.Index
            && index < fable.Index + fable.Length)

    [
        let guards =
            text.Split '\n'
            |> Array.map _.Trim()
            |> Array.filter (fun line ->
                line.StartsWith "#if"
                && line.Contains "FABLE_COMPILER")

        for guard in guards do
            if
                guard <> "#if FABLE_COMPILER"
                && guard <> "#if !FABLE_COMPILER"
            then
                "unsupported guard: " + guard

        let opened =
            guards
            |> Array.filter ((=) "#if FABLE_COMPILER")
            |> Array.length

        if opened <> blocks.Length then
            $"{opened - blocks.Length} #if FABLE_COMPILER block(s) without an #else"

        for block in blocks do
            let fable, net = block.Groups["fable"], block.Groups["net"]

            if
                fable.Value.Contains "#if"
                || fable.Value.Contains "#endif"
            then
                "a #if FABLE_COMPILER block without an #else before the next directive"
            elif names fable <> names net then
                $"Fable names %A{names fable} differ from .NET names %A{names net}"

        for found in browserAttribute.Matches text do
            if found.Groups[1].Value <> "Emit" then
                "attribute other than Emit: " + found.Value
            elif not (inFable found.Index) then
                "Emit outside a #if FABLE_COMPILER block: "
                + found.Value
    ]

let private nativeBodies () =
    equal [] (nativeStructure (File.ReadAllText (Path.Combine (portable, "Native.fs"))))

let private nativeStructureRejects () =
    let guarded =
        "#if FABLE_COMPILER\n[<Emit(\"document\")>]\nlet document () : Document = jsNative\n#else\nlet document () : Document = host ()\n#endif\n"

    equal [] (nativeStructure guarded)

    let unguarded =
        guarded
        + "[<Emit(\"document.body\")>]\nlet body () : HTMLElement = jsNative\n"

    equal 1 (nativeStructure unguarded).Length

    let missing =
        "#if FABLE_COMPILER\n[<Emit(\"$0.a\")>]\nlet a x = jsNative\n[<Emit(\"$0.b\")>]\nlet b x = jsNative\n#else\nlet a x = x\n#endif\n"

    equal 1 (nativeStructure missing).Length

    let noElse =
        "#if FABLE_COMPILER\n[<Emit(\"$0.a\")>]\nlet a x = jsNative\n#endif\n"
        + guarded

    equal true ((nativeStructure noElse).Length >= 1)

    equal 1 (nativeStructure "#if FABLE_COMPILER && X\nlet a = 1\n#endif\n").Length

let cases: (string * (unit -> unit)) array =
    [|
        "the portable project references only src/Ranvier and the Xantham type bindings", browserReferences
        "the portable project's attributes use no Import or Global, and no Emit outside Native.fs touches document, window or addEventListener",
        browserEmits
        "every Emit of Native.fs sits in a #if FABLE_COMPILER block whose #else defines the same names", nativeBodies
        "the Native.fs shape check rejects an unguarded Emit, a missing .NET body, a block without #else and other guards", nativeStructureRejects
    |]
