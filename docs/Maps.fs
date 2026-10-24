/// The `map` fence: a plain F# scenario rewritten into a live or replayed signal map. The `ranvier` fence: a
/// Ranvier.View component rendered on the server and hydrated in the page.
module Docs.Maps

open System.Collections.Concurrent
open Nacara.Core
open Nacara.Plugins
open Nacara.Plugins.Internal
open Ranvier.Docs.Maps.Authoring

let private codeOptions = ConcurrentDictionary<struct (string * string), MapFlags>()

let private spans (spans: MapSpan list) : SolidLineSpan list =
    spans
    |> List.map (fun s ->
        {
            Generated = s.Generated
            Length = s.Length
            Body = s.Body
            Indent = s.Indent
        })

/// <summary>Compiles a <c>map</c> fence into a <c>SignalMap</c> of its scenario.</summary>
let transform (input: SolidTransformInput) : SolidTransformOutput =
    match MapFence.compile input.CellId input.Flags input.Code with
    | Ok output ->
        match MapFlags.parse input.Flags with
        | Ok flags -> codeOptions[struct (input.PageKey, input.CellId)] <- flags
        | Error _ -> ()

        SolidTransformOutput.Compiled
            {
                Code = output.Code
                Render = output.Render
                Spans = spans output.Spans
            }
    | Error problems -> SolidTransformOutput.Rejected problems

/// <summary>Adds native code disclosures to rendered map cards before HTML minification.</summary>
let register =
    Site.plugin
        { new IPlugin with
            member _.Name = "map-code"

            member _.Configure registry =
                registry
                |> Registry.assetTransform
                    {
                        Name = "map-code"
                        Extensions = [ ".html" ]
                        Transform =
                            fun context ->
                                MapCode.rewrite
                                    (fun page cell ->
                                        match codeOptions.TryGetValue (struct (page, cell)) with
                                        | true, options -> Some options
                                        | _ -> None)
                                    context.Content
                    }
        }

/// <summary>The word that marks an <c>fsharp</c> fence as a Ranvier component.</summary>
let private fenceToken = "ranvier"

/// <summary>What the fence transform leaves on a page for the compile hook.</summary>
let private dataKey = "ranvier-maps"

/// <summary>Where the bundles are written, relative to the output directory.</summary>
let private outputPath = "_ranvier/maps"

/// <summary>The server HTML and bundle URLs of the last compile.</summary>
type private Compiled() =
    member val Fragments: Map<string, string> = Map.empty with get, set
    member val Scripts: Map<string, string> = Map.empty with get, set

let private cellOf (cell: SolidCell) : Result<PageCell, string> =
    let kind =
        match cell.Kind with
        | SolidCellKind.Expression -> Ok PageCellKind.Expression
        | SolidCellKind.Declarations (Some name) -> Ok (PageCellKind.Render name)
        | SolidCellKind.Declarations None
        | SolidCellKind.Setup -> Ok PageCellKind.Declarations
        | SolidCellKind.Use _ -> Error "a ranvier component renders from a fence, not from a code span"
        | SolidCellKind.Transformed _ -> Error "a ranvier fence takes no transform"

    kind
    |> Result.map (fun kind ->
        {
            Id = cell.Id
            Kind = kind
            Code = cell.Code
            Line = cell.Line
        })

let private validId = System.Text.RegularExpressions.Regex "^[A-Za-z0-9_-]+$"

/// <summary>Rewrites a page's component fences into cards and hosts, and keeps the page's module on the page.</summary>
let private transformPage (context: TransformContext) (page: Page) =
    if not (page.Body.Contains fenceToken) then
        page
    else
        let key = SolidGenerate.pageKey page.Id
        let scan = SolidScan.scanWith [] fenceToken false key page.Body

        if scan.Cells.IsEmpty then
            page
        else
            let report line problem =
                let diagnostic = Diagnostic.error "fence" problem

                match page.SourceFile with
                | Some file ->
                    context.Diagnostics.Add (
                        diagnostic
                        |> Diagnostic.at file (page.BodyLine + line - 1) 1
                    )
                | None -> context.Diagnostics.Add diagnostic

            for line, problem in scan.Problems do
                report line problem

            let cells =
                scan.Cells
                |> List.choose (fun cell ->
                    if not (validId.IsMatch cell.Id) then
                        report (cell.Line - 1) $"id=%s{cell.Id} must hold only letters, digits, '_' and '-'"
                        None
                    else
                        match cellOf cell with
                        | Ok cell -> Some cell
                        | Error problem ->
                            report (cell.Line - 1) problem
                            None)

            let unit =
                MapPage.unit key (page.SourceFile |> Option.map AbsolutePath.value) page.BodyLine cells

            { page with
                Body = MapPage.hosts scan.Body
            }
                .WithData(dataKey, unit)

let private report (context: HookContext) (message: MapWorkspace.Message) =
    // Watch keeps serving while a cell is broken; its host stays empty.
    let diagnostic =
        if message.IsError && not context.IsWatch then
            Diagnostic.error "compile" message.Text
        else
            Diagnostic.warning "compile" message.Text

    let diagnostic =
        match message.Page with
        | Some {
                   Source = Some source
                   BodyLine = bodyLine
               } ->
            let file = AbsolutePath.create source

            match message.At with
            | Some (line, column) ->
                diagnostic
                |> Diagnostic.at file (bodyLine + line - 1) column
            | None -> diagnostic |> Diagnostic.inFile file
        | _ -> diagnostic

    context.Diagnostics.Add diagnostic

let private compileAll (compiled: Compiled) (context: HookContext) =
    let units =
        context.Pages
        |> List.choose (fun page -> page.TryData<PageUnit> dataKey)
        |> List.filter (fun unit -> not (MapPage.rendered unit).IsEmpty)
        |> List.sortBy _.Key

    compiled.Fragments <- Map.empty
    compiled.Scripts <- Map.empty

    if not units.IsEmpty then
        match EsbuildBinary.resolve () with
        | Error problem -> context.Diagnostics.Add (Diagnostic.error "esbuild" problem)
        | Ok esbuild ->
            let started = System.Diagnostics.Stopwatch.StartNew ()

            let built =
                MapWorkspace.build
                    (AbsolutePath.value context.ProjectRoot)
                    esbuild
                    (not context.IsWatch)
                    [ "animejs", "4.5.0" ]
                    (fun line -> Log.info $"ranvier maps: %s{line}")
                    units

            built.Messages |> List.iter (report context)

            if not built.Files.IsEmpty then
                for name, text in built.Files do
                    context.Write $"%s{outputPath}/%s{name}" text
                    |> ignore

                compiled.Fragments <- built.Fragments

                compiled.Scripts <-
                    units
                    |> List.map (fun unit -> unit.Key, context.Site.UrlOfAsset $"%s{outputPath}/%s{unit.Key}.js")
                    |> Map.ofList

            if started.ElapsedMilliseconds > 1000L then
                Log.info $"ranvier maps: %d{units.Length} page(s) in %d{started.ElapsedMilliseconds}ms"

let private compiled = Compiled ()

/// <summary>Compiles <c>fsharp ranvier</c> fences into server-rendered, hydrated components.</summary>
/// <remarks>Register <c>hydrate</c> after HTML minification.</remarks>
let components =
    Site.plugin
        { new IPlugin with
            member _.Name = "ranvier-maps"

            member _.Configure registry =
                let registry =
                    registry
                    |> Registry.transform
                        {
                            Name = "ranvier-maps"
                            Extensions = [ ".md"; ".markdown" ]
                            Transform = transformPage
                        }

                // First in line, so the fences are rewritten before the markdown renderer and the Solid scanner read
                // them.
                let transforms =
                    List.last registry.Transforms
                    :: List.take (registry.Transforms.Length - 1) registry.Transforms

                { registry with
                    Transforms = transforms
                }
                |> Registry.onPagesRouted (compileAll compiled)
        }

/// <summary>Fills each map host with its server HTML and adds the page's bundle, after HTML minification.</summary>
let hydrate =
    Site.plugin
        { new IPlugin with
            member _.Name = "ranvier-maps-hydrate"

            member _.Configure registry =
                registry
                |> Registry.assetTransform
                    {
                        Name = "ranvier-maps-hydrate"
                        Extensions = [ ".html" ]
                        Transform =
                            fun context ->
                                if context.Content.Contains "rvm-p" then
                                    MapPage.fill
                                        (fun host -> Map.tryFind host compiled.Fragments)
                                        (fun key -> Map.tryFind key compiled.Scripts)
                                        context.Content
                                else
                                    context.Content
                    }
        }
