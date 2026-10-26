/// <summary>Additional restored assemblies for Nacara's browser F# checker.</summary>
module Nacara.Plugins.LiveExampleMetadata

open System
open System.IO
open System.Text.Json
open System.Text.RegularExpressions
open Nacara.Core

/// <summary>Finds an exact package DLL in the package folders recorded by a restored project's assets file.</summary>
let nuget (assetsFile: string) (package: string) (version: string) (framework: string) (assembly: string) : string =
    use document = JsonDocument.Parse (File.ReadAllText assetsFile)

    let relative =
        Path.Combine (package.ToLowerInvariant (), version.ToLowerInvariant (), "lib", framework, assembly + ".dll")

    document.RootElement.GetProperty("packageFolders").EnumerateObject()
    |> Seq.map (fun folder -> Path.Combine (folder.Name, relative))
    |> Seq.tryFind File.Exists
    |> Option.defaultWith (fun () ->
        raise (
            FileNotFoundException (
                $"LiveExample metadata: restore {package} {version}; {relative} was absent from the package folders in {assetsFile}"
            )
        ))

let private workerReferences =
    Regex (@"\[\s*""Browser\.Dom""\s*,\s*""Browser\.Blob""\s*,\s*""Browser\.Event""\s*,\s*""Browser\.WebStorage""\s*\]")

let private extendWorker (names: string list) (text: string) =
    let found = workerReferences.Matches text

    if found.Count <> 1 then
        invalidOp "LiveExample metadata: expected exactly one browser reference list in live-example.js; check the installed LiveExample version"

    let original = found[0].Value

    let extra =
        names
        |> List.map (fun name -> JsonSerializer.Serialize name)
        |> String.concat ","

    text.Replace (
        original,
        original.Substring (0, original.Length - 1)
        + ","
        + extra
        + "]"
    )

/// <summary>Copies DLLs beside LiveExample's checker references and adds their names to its browser worker.</summary>
/// <remarks>Register after LiveExample. An empty list changes nothing; a missing DLL or changed worker layout fails the build.</remarks>
let register (assemblies: string list) (site: Site) : Site =
    if assemblies.IsEmpty then
        site
    else
        Site.plugin
            { new IPlugin with
                member _.Name = "live-example-metadata"

                member _.Configure registry =
                    let hasWorker =
                        registry.Assets
                        |> List.exists (fun asset ->
                            (RelativePath.value asset.Destination).Replace('\\', '/') = "assets/live-example/live-example.js")

                    if not hasWorker then
                        invalidOp "LiveExample metadata: the live-example.js asset is missing; register after a compatible LiveExample plugin"

                    let references =
                        assemblies
                        |> List.map (fun path ->
                            let path = Path.GetFullPath path

                            if not (File.Exists path) then
                                raise (FileNotFoundException ("LiveExample metadata assembly was not found", path))

                            Path.GetFileNameWithoutExtension path, path)
                        |> List.distinctBy fst

                    let directories =
                        registry.Assets
                        |> List.map (fun asset ->
                            RelativePath.value asset.Destination
                            |> fun path -> path.Replace ('\\', '/'))
                        |> List.filter (fun path ->
                            path.StartsWith ("assets/live-example/refs/", StringComparison.Ordinal)
                            && path.EndsWith ("/FSharp.Core.dll.txt", StringComparison.Ordinal))
                        |> List.map (fun path -> path.Substring (0, path.LastIndexOf '/'))
                        |> List.distinct

                    let directory =
                        match directories with
                        | [ directory ] -> directory
                        | _ -> invalidOp "LiveExample metadata: register after LiveExample with one .dll.txt checker reference directory"

                    let withReferences =
                        references
                        |> List.fold
                            (fun state (name, path) ->
                                state
                                |> Registry.asset (Asset.CopyFile (AbsolutePath.create path, RelativePath.create $"{directory}/{name}.dll.txt")))
                            registry

                    withReferences
                    |> Registry.assetTransform
                        {
                            Name = "live-example-metadata"
                            Extensions = [ ".js" ]
                            Transform =
                                fun context ->
                                    if (RelativePath.value context.Path).Replace('\\', '/') = "assets/live-example/live-example.js" then
                                        extendWorker (references |> List.map fst) context.Content
                                    else
                                        context.Content
                        }
            }
            site
