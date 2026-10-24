/// <summary>
/// The generated projects of the documentation's component fences: a shared project of page modules, a .NET exporter
/// that writes each cell's server HTML and browser entry, and a Fable client bundled per page.
/// </summary>
module Docs.MapWorkspace

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.RegularExpressions
open Ranvier.Docs.Maps.Authoring

/// <summary>A compiler or exporter diagnostic, placed on its page when it came from a cell.</summary>
type Message =
    {
        Page: PageUnit option
        /// <summary>The page body line and column.</summary>
        At: (int * int) option
        Text: string
        IsError: bool
    }

/// <summary>What a build of the workspace produced.</summary>
type Built =
    {
        /// <summary>The server HTML of each host id.</summary>
        Fragments: Map<string, string>
        /// <summary>The bundle's files, by path relative to the bundle directory.</summary>
        Files: (string * string) list
        Messages: Message list
    }

let private sharedProject =
    """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="pages/*.fs" />
    <ProjectReference Include="../../../maps/Ranvier.Docs.Maps.fsproj" />
  </ItemGroup>
</Project>
"""

let private exporterProject =
    """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Program.fs" />
    <ProjectReference Include="../Shared/Shared.fsproj" />
  </ItemGroup>
</Project>
"""

let private clientProject =
    """<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="../out/entries/*.fs" />
    <ProjectReference Include="../Shared/Shared.fsproj" />
  </ItemGroup>
</Project>
"""

let private packageJson (packages: (string * string) list) =
    let dependencies =
        packages
        |> List.map (fun (name, version) -> $"    \"%s{name}\": \"%s{version}\"")
        |> String.concat ",\n"

    $"{{\n  \"name\": \"ranvier-docs-maps\",\n  \"private\": true,\n  \"type\": \"module\",\n  \"dependencies\": {{\n%s{dependencies}\n  }}\n}}\n"

let private hash (text: string) =
    SHA256.HashData (Encoding.UTF8.GetBytes text)
    |> Convert.ToHexString

/// <summary>Writes <c>content</c> when the file's content differs.</summary>
let private write (path: string) (content: string) =
    Directory.CreateDirectory (Path.GetDirectoryName path)
    |> ignore

    if
        not (File.Exists path)
        || File.ReadAllText path <> content
    then
        File.WriteAllText (path, content)

let private run (directory: string) (environment: (string * string) list) (file: string) (arguments: string list) =
    let info =
        ProcessStartInfo (file, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, WorkingDirectory = directory)

    for argument in arguments do
        info.ArgumentList.Add argument

    for name, value in environment do
        info.Environment[name] <- value

    use proc = new Process (StartInfo = info)
    let output = StringBuilder ()

    let collect =
        DataReceivedEventHandler (fun _ line ->
            if not (isNull line.Data) then
                lock output (fun () -> output.AppendLine line.Data |> ignore))

    proc.OutputDataReceived.AddHandler collect
    proc.ErrorDataReceived.AddHandler collect
    proc.Start () |> ignore
    proc.BeginOutputReadLine ()
    proc.BeginErrorReadLine ()

    if not (proc.WaitForExit (int (TimeSpan.FromMinutes 15.).TotalMilliseconds)) then
        proc.Kill true
        failwithf "%s timed out" file

    proc.WaitForExit ()
    proc.ExitCode, output.ToString ()

let private diagnostic =
    Regex (
        @"^\s*(?<file>.+?\.fs)\((?<line>\d+),(?<column>\d+)\)(?::\s*\(\d+,\d+\))?:?\s*(?<severity>error|warning)\s+(?<code>[A-Za-z]+\d*)\s*:\s*(?<text>.*?)(?:\s*\[[^\]]*\])?\s*$",
        RegexOptions.Compiled
    )

/// <summary>Places compiler diagnostics on the pages whose modules they name; warnings outside a cell are dropped.</summary>
let private messages (units: PageUnit list) (output: string) =
    output.Split '\n'
    |> Array.choose (fun line ->
        let matched = diagnostic.Match (line.TrimEnd '\r')

        if not matched.Success then
            None
        else
            let file = Path.GetFileName matched.Groups["file"].Value
            let isError = matched.Groups["severity"].Value = "error"
            let code = matched.Groups["code"].Value
            let lineNumber = matched.Groups["line"].Value
            let path = matched.Groups["file"].Value
            let message = matched.Groups["text"].Value
            let text = $"%s{code}: %s{message}"

            let page =
                units
                |> List.tryFind (fun unit ->
                    MapPage.fileName unit.Key = file
                    || file.StartsWith ("P" + unit.Key.Substring 1 + "_"))

            let at =
                page
                |> Option.filter (fun unit -> MapPage.fileName unit.Key = file)
                |> Option.bind (fun unit -> MapPage.locate unit.Spans (int matched.Groups["line"].Value) (int matched.Groups["column"].Value))

            match page, at with
            | Some _, Some _ ->
                Some
                    {
                        Page = page
                        At = at
                        Text = text
                        IsError = isError
                    }
            | _ when isError ->
                Some
                    {
                        Page = page
                        At = None
                        Text = $"%s{path}(%s{lineNumber}): %s{text}"
                        IsError = true
                    }
            | _ -> None)
    |> Array.distinct
    |> List.ofArray

let private failure (step: string) (output: string) =
    {
        Page = None
        At = None
        Text = $"%s{step} failed:\n%s{output.Trim ()}"
        IsError = true
    }

/// <summary>The files whose change requires a rebuild: the map libraries and every source project they reference.</summary>
let private inputs (repository: string) =
    let skip (path: string) =
        let parts = path.Split ([| '\\'; '/' |])

        parts
        |> Array.exists (fun part ->
            part = "bin"
            || part = "obj"
            || part = "node_modules"
            || part = "tests")

    [ Path.Combine (repository, "docs", "maps"); Path.Combine (repository, "src") ]
    |> List.collect (fun directory ->
        Directory.EnumerateFiles (directory, "*", SearchOption.AllDirectories)
        |> Seq.filter (fun path ->
            not (skip (Path.GetRelativePath (directory, path)))
            && (path.EndsWith ".fs"
                || path.EndsWith ".fsproj"
                || path.EndsWith ".props"
                || path.EndsWith ".targets"))
        |> List.ofSeq)
    |> List.sort
    |> List.map (fun path ->
        let info = FileInfo path
        $"%s{path}|%d{info.Length}|%d{info.LastWriteTimeUtc.Ticks}")
    |> String.concat "\n"

let private readTree (directory: string) =
    if Directory.Exists directory then
        Directory.EnumerateFiles (directory, "*", SearchOption.AllDirectories)
        |> Seq.sort
        |> Seq.map (fun path -> (Path.GetRelativePath (directory, path)).Replace('\\', '/'), File.ReadAllText path)
        |> List.ofSeq
    else
        []

let private reset (directory: string) =
    if Directory.Exists directory then
        Directory.Delete (directory, true)

    Directory.CreateDirectory directory |> ignore

/// <summary>
/// Generates the workspace under <c>root/.nacara/ranvier-maps</c>, builds what changed, and reads the server HTML and
/// bundle.
/// </summary>
/// <param name="root">The documentation project's directory.</param>
/// <param name="esbuild">The esbuild executable.</param>
/// <param name="minify">Whether the bundle is minified.</param>
/// <param name="packages">The npm packages the compiled client imports.</param>
/// <param name="log">Told what is taking the time.</param>
/// <param name="units">Every page with component cells.</param>
let build (root: string) (esbuild: string) (minify: bool) (packages: (string * string) list) (log: string -> unit) (units: PageUnit list) =
    let workspace = Path.Combine (root, ".nacara", "ranvier-maps")
    let repository = Path.GetFullPath (Path.Combine (root, ".."))
    let out = Path.Combine (workspace, "out")
    let bundle = Path.Combine (out, "bundle")
    let fragments = Path.Combine (out, "fragments")
    let pages = Path.Combine (workspace, "Shared", "pages")
    let program = MapPage.exporter units

    write (Path.Combine (workspace, "Shared", "Shared.fsproj")) sharedProject
    write (Path.Combine (workspace, "Exporter", "Exporter.fsproj")) exporterProject
    write (Path.Combine (workspace, "Exporter", "Program.fs")) program
    write (Path.Combine (workspace, "Client", "Client.fsproj")) clientProject
    write (Path.Combine (workspace, "package.json")) (packageJson packages)

    let current =
        units
        |> List.map (fun unit -> MapPage.fileName unit.Key)
        |> Set.ofList

    if Directory.Exists pages then
        for file in Directory.GetFiles (pages, "*.fs") do
            if not (current.Contains (Path.GetFileName file)) then
                File.Delete file

    for unit in units do
        write (Path.Combine (pages, MapPage.fileName unit.Key)) unit.Code

    let fingerprint =
        [
            program
            yield! units |> List.map _.Code
            packageJson packages
            string minify
            inputs repository
        ]
        |> String.concat "\n\u0000"
        |> hash

    let stamp = Path.Combine (workspace, "fingerprint")

    let fresh =
        File.Exists stamp
        && File.ReadAllText stamp = fingerprint
        && Directory.Exists bundle

    let messages =
        if fresh then
            []
        else
            if File.Exists stamp then
                File.Delete stamp

            let trace = [ "RanvierTrace", "true" ]
            log "building the map exporter"

            let code, output =
                run
                    workspace
                    trace
                    "dotnet"
                    [
                        "build"
                        "Exporter/Exporter.fsproj"
                        "-c"
                        "Release"
                        "-p:RanvierTrace=true"
                        "-v"
                        "q"
                        "-nologo"
                        "-clp:NoSummary"
                    ]

            if code <> 0 then
                match messages units output |> List.filter _.IsError with
                | [] -> [ failure "dotnet build" output ]
                | found -> found
            else
                reset out
                log "rendering the maps"

                let code, output =
                    run workspace [] "dotnet" [ Path.Combine ("Exporter", "bin", "Release", "net10.0", "Exporter.dll"); out ]

                let exported =
                    output.Split '\n'
                    |> Array.choose (fun line ->
                        match (line.TrimEnd '\r').Split '\t' with
                        | [| "ranvier-map"; key; cell; text |] ->
                            let page = units |> List.tryFind (fun unit -> unit.Key = key)

                            let at =
                                page
                                |> Option.bind (fun unit -> unit.Cells |> List.tryFind (fun c -> c.Id = cell))
                                |> Option.map (fun c -> c.Line - 1, 1)

                            Some
                                {
                                    Page = page
                                    At = at
                                    Text = $"rendering %s{cell} on the server: %s{text}"
                                    IsError = true
                                }
                        | _ -> None)
                    |> List.ofArray

                if code <> 0 then
                    match exported with
                    | [] -> [ failure "the map exporter" output ]
                    | found -> found
                else
                    log "compiling the maps with Fable"

                    let code, output =
                        run
                            workspace
                            trace
                            "dotnet"
                            [
                                "fickl"
                                "Client/Client.fsproj"
                                "-o"
                                Path.Combine (out, "js")
                                "-e"
                                ".js"
                                "-c"
                                "Release"
                            ]

                    let compiled = messages units output

                    if code <> 0 || compiled |> List.exists _.IsError then
                        match compiled |> List.filter _.IsError with
                        | [] -> [ failure "fickl" output ]
                        | found -> found
                    else
                        let installed = Path.Combine (workspace, "node_modules", ".package-hash")

                        let wanted = hash (packageJson packages)

                        let npm =
                            if
                                File.Exists installed
                                && File.ReadAllText installed = wanted
                            then
                                0, ""
                            else
                                log "installing the maps' npm packages"

                                // A relative npm.cmd resolves its own script against the working directory.
                                let npm =
                                    let name = if OperatingSystem.IsWindows () then "npm.cmd" else "npm"

                                    (Environment.GetEnvironmentVariable "PATH").Split Path.PathSeparator
                                    |> Array.map (fun directory -> Path.Combine (directory.Trim '"', name))
                                    |> Array.tryFind File.Exists
                                    |> Option.defaultValue name

                                let result = run workspace [] npm [ "install"; "--no-audit"; "--no-fund" ]

                                if fst result = 0 then
                                    File.WriteAllText (installed, wanted)

                                result

                        if fst npm <> 0 then
                            [ failure "npm install" (snd npm) ]
                        else
                            let compiledEntries =
                                Directory.EnumerateFiles (Path.Combine (out, "js"), "*.js", SearchOption.AllDirectories)
                                |> Seq.filter (fun path -> path.Replace('\\', '/').Contains "/entries/")
                                |> Seq.map (fun path -> Path.GetFileNameWithoutExtension path, path)
                                |> Map.ofSeq

                            let entries = Path.Combine (workspace, "pages")
                            reset entries

                            let missing =
                                [
                                    for unit in units do
                                        let path (cell: PageCell) =
                                            compiledEntries
                                            |> Map.tryFind (MapPage.entryName unit.Key cell.Id)
                                            |> Option.map (fun path ->
                                                "./"
                                                + (Path.GetRelativePath (entries, path)).Replace('\\', '/'))

                                        match MapPage.pageEntry path unit with
                                        | None -> unit.Key
                                        | Some entry -> write (Path.Combine (entries, unit.Key + ".js")) entry
                                ]

                            if not missing.IsEmpty then
                                [
                                    failure "fickl" $"""no compiled entries for %s{String.concat ", " missing}"""
                                ]
                            else
                                log "bundling the maps"

                                let pageEntries =
                                    Directory.GetFiles (entries, "*.js")
                                    |> Array.sort
                                    |> List.ofArray

                                if pageEntries.IsEmpty then
                                    File.WriteAllText (stamp, fingerprint)
                                    Directory.CreateDirectory bundle |> ignore
                                    []
                                else
                                    let code, output =
                                        run
                                            workspace
                                            []
                                            esbuild
                                            [
                                                yield! pageEntries
                                                "--bundle"
                                                "--splitting"
                                                "--format=esm"
                                                $"--outdir=%s{bundle}"
                                                "--entry-names=[name]"
                                                "--chunk-names=chunks/[name]-[hash]"
                                                "--log-level=warning"
                                                if minify then
                                                    "--minify"
                                            ]

                                    if code <> 0 then
                                        [ failure "esbuild" output ]
                                    else
                                        File.WriteAllText (stamp, fingerprint)
                                        compiled

    let fragments =
        readTree fragments
        |> List.map (fun (name, html) -> Path.GetFileNameWithoutExtension name, html)
        |> Map.ofList

    {
        Fragments = fragments
        Files =
            if messages |> List.exists _.IsError then
                []
            else
                readTree bundle
        Messages = messages
    }
