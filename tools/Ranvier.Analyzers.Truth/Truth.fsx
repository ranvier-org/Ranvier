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

dotnet [ "tool"; "restore" ] |> ignore
inlining ()
precompile ()

if failures.Count > 0 then
    eprintfn "%d truth checks failed: %s" failures.Count (String.Join (", ", failures))
    exit 1
