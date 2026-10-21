// The external facts the analyzers rely on: Fable's inlining of module-mutable snapshots (RAN001) and the Fable
// precompile failure on generated union testers (RAN002). Exits non-zero when a fact no longer holds: retire or rescope
// the rule that describes it. Run from any directory: dotnet fsi tools/Ranvier.Analyzers.Truth/Truth.fsx
open System
open System.Diagnostics
open System.IO
open System.Text.RegularExpressions

let root = Path.GetFullPath (Path.Combine (__SOURCE_DIRECTORY__, "../.."))
let output = Path.Combine (root, "artifacts/analyzers/truth")

/// Runs `dotnet` with `arguments` in the repository root; the exit code and the combined output.
let dotnet (arguments: string list) : int * string =
    let info =
        ProcessStartInfo ("dotnet", WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false)

    for argument in arguments do
        info.ArgumentList.Add argument

    use proc = Process.Start info
    let out = proc.StandardOutput.ReadToEndAsync ()
    let err = proc.StandardError.ReadToEndAsync ()
    proc.WaitForExit ()
    proc.ExitCode, out.Result + err.Result

let failures = ResizeArray<string>()

let check (name: string) (holds: bool) (detail: string) =
    if holds then
        printfn "ok   %s" name
    else
        failures.Add name
        printfn "FAIL %s\n%s" name detail

let inlining () =
    let target = Path.Combine (output, "inlining")

    let code, log =
        dotnet
            [
                "fable"
                "tools/Ranvier.Analyzers.Truth/Inlining"
                "-c"
                "Release"
                "--noCache"
                "-e"
                ".js"
                "-o"
                target
            ]

    let js =
        if code = 0 then
            File.ReadAllText (Path.Combine (target, "Probe.js"))
        else
            ""

    check "Fable compiles the inlining fixture" (code = 0) log

    check
        "RAN001: Fable Release inlines each single-use snapshot (snapshot, closure, tuple)"
        (Regex.Matches(js, Regex.Escape "childSetups() - childSetups()").Count
         >= 3)
        js

    check "RAN001: Fable keeps a let mutable snapshot" (js.Contains "let held = childSetups()") js

let precompile () =
    let testers, testersLog =
        dotnet
            [
                "fable"
                "precompile"
                "tools/Ranvier.Analyzers.Truth/Testers"
                "--noCache"
                "-o"
                Path.Combine (output, "testers")
            ]

    check
        "RAN002: the precompile fails on a non-generic union tester"
        (testers <> 0
         && testersLog.Contains "MethodDefNotFound")
        testersLog

    let generic, genericLog =
        dotnet
            [
                "fable"
                "precompile"
                "tools/Ranvier.Analyzers.Truth/GenericTesters"
                "--noCache"
                "-o"
                Path.Combine (output, "generic-testers")
            ]

    check "RAN002: the precompile accepts a generic union tester" (generic = 0) genericLog

let build () =
    let analyzerCode, analyzerLog =
        dotnet
            [
                "build"
                "tools/Ranvier.Analyzers/Ranvier.Analyzers.fsproj"
                "-c"
                "Release"
                "-v"
                "q"
            ]

    check "the analyzers build" (analyzerCode = 0) analyzerLog
    let project = "tools/Ranvier.Analyzers.Truth/Build/Build.fsproj"

    let code, log = dotnet [ "build"; project; "-c"; "Release"; "--no-incremental" ]

    check
        "RanvierAnalyze reports RAN001 as an MSBuild warning"
        (code = 0
         && log.Contains "B.fs(4,8): warning RAN001")
        log

    check "an ignore-line-next comment silences its line" (not (log.Contains "B.fs(11,")) log

    let quiet, quietLog =
        dotnet
            [
                "build"
                project
                "-c"
                "Release"
                "--no-incremental"
                "-p:MSBuildWarningsAsMessages=RAN001"
            ]

    check
        "MSBuildWarningsAsMessages demotes RAN001"
        (quiet = 0
         && not (quietLog.Contains "warning RAN001"))
        quietLog

    let strict, strictLog =
        dotnet
            [
                "build"
                project
                "-c"
                "Release"
                "--no-incremental"
                "-p:RanvierAnalyzersFlags=--treat-as-error RAN001"
            ]

    check "an error-promoted RAN001 fails the build" (strict <> 0 && strictLog.Contains "error RAN001") strictLog

dotnet [ "tool"; "restore" ] |> ignore
inlining ()
precompile ()
build ()

if failures.Count > 0 then
    eprintfn "%d truth checks failed: %s" failures.Count (String.Join (", ", failures))
    exit 1
