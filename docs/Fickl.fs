/// <summary>The fickl release named in the repository's tool manifest, and the browser compiler it carries.</summary>
/// <remarks>The manifest is the one input: <c>dotnet tool restore</c> takes the command-line compiler from it, and
/// the live editor's browser compiler comes from the same release's two archives.</remarks>
module Docs.Fickl

open System.IO
open System.Text.Json

let private manifest =
    Path.Combine (__SOURCE_DIRECTORY__, "..", ".config", "dotnet-tools.json")

let private noEntry () =
    failwith "No fickl entry in .config/dotnet-tools.json"

/// <summary>The fickl version in <c>.config/dotnet-tools.json</c>.</summary>
let version =
    let text =
        try
            File.ReadAllText manifest
        with :? IOException ->
            noEntry ()

    let document =
        try
            JsonDocument.Parse text
        with :? JsonException ->
            noEntry ()

    use document = document

    match document.RootElement.TryGetProperty "tools" with
    | true, tools ->
        match tools.TryGetProperty "fickl" with
        | true, fickl ->
            match fickl.TryGetProperty "version" with
            | true, value when value.ValueKind = JsonValueKind.String -> value.GetString ()
            | _ -> noEntry ()
        | _ -> noEntry ()
    | _ -> noEntry ()

let private release =
    $"https://github.com/shayanhabibi/fickl/releases/download/v%s{version}"

/// <summary>The release archive holding the browser compiler.</summary>
let standaloneUrl = $"%s{release}/fickl-standalone-%s{version}.tgz"

/// <summary>The release archive holding the assemblies the browser compiler checks against.</summary>
let metadataUrl = $"%s{release}/fickl-metadata-%s{version}.tgz"
