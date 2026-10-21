param([switch]$CheckFable)

$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$temp = Join-Path ([IO.Path]::GetTempPath()) ('ranvier-packages-' + [guid]::NewGuid().ToString('N'))
$feed = Join-Path $temp 'feed'
$consumer = Join-Path $temp 'consumer'

function Invoke-DotNet {
    & dotnet @args
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
}

New-Item -ItemType Directory -Path $feed, $consumer | Out-Null
@"
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$feed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $consumer 'NuGet.Config')
try {
    $fableEntries = @()
    foreach ($traced in @($false, $true)) {
        $references = @()
        foreach ($name in @('Ranvier', 'Ranvier.Elmish', 'Ranvier.CSharp', 'Ranvier.Query', 'Ranvier.Wire')) {
            $project = Join-Path $repo "src/$name/$name.fsproj"
            Invoke-DotNet pack $project -c Release "-p:RanvierTrace=$($traced.ToString().ToLowerInvariant())" -o $feed
            $id = if ($traced) { "$name.Traced" } else { $name }
            $version = ([xml](Get-Content -LiteralPath $project -Raw)).Project.PropertyGroup.Version | Where-Object { $_ }
            $package = Join-Path $feed "$id.$version.nupkg"
            $archive = [IO.Compression.ZipFile]::OpenRead($package)
            try {
                $sourceProject = $archive.GetEntry("fable/$name.fsproj")
                if (-not $sourceProject) { throw "$id is missing its Fable source project" }
                $sourceReader = [IO.StreamReader]::new($sourceProject.Open())
                try { $fableProject = [xml]$sourceReader.ReadToEnd() } finally { $sourceReader.Dispose() }
                $sources = $fableProject.SelectNodes("//Compile")
                if ($sources.Count -eq 0) { throw "$id has no Fable sources" }
                foreach ($source in $sources) {
                    if ($source.Condition) { throw "$id has a conditional Fable source: $($source.Include)" }
                    $sourcePath = 'fable/' + $source.Include.Replace('\', '/')
                    if (-not $archive.GetEntry($sourcePath)) { throw "$id is missing $sourcePath" }
                }
                $entry = $archive.Entries | Where-Object { $_.Name.EndsWith('.nuspec') }
                $reader = [IO.StreamReader]::new($entry.Open())
                try { $nuspec = [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }
                if ($nuspec.SelectNodes("//*[local-name()='dependency' and @id='Fable.Package.SDK']").Count -ne 0) {
                    throw "$id must not depend on the Fable packaging SDK at runtime"
                }
                $dependencies = $nuspec.SelectNodes("//*[local-name()='dependency' and @id='FSharp.Core']")
                if ($dependencies.Count -ne 2) { throw "$id must declare FSharp.Core for both targets" }
                foreach ($dependency in $dependencies) {
                    if ($dependency.version -ne '8.0.100') { throw "$id requires FSharp.Core $($dependency.version), expected 8.0.100" }
                }
            } finally { $archive.Dispose() }
            $references += "<PackageReference Include=`"$id`" Version=`"$version`" />"
        }
        @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <WarningsAsErrors>NU1605</WarningsAsErrors>
    <RestorePackagesPath>$temp/packages</RestorePackagesPath>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="FSharp.Core" Version="8.0.100" />
    $($references -join "`n    ")
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $consumer 'Consumer.csproj')
        @'
using Microsoft.FSharp.Core;
using Ranvier;
using Ranvier.Elmish;
using Ranvier.Query;

using var graph = new Graph();
using var scope = graph.Activate();
var app = MvuModule.create(0, FuncConvert.FromFunc<int, int, int>((message, model) => model + message));
var count = app.Select(FuncConvert.FromFunc<int, int>(model => model));
app.Dispatch(2);
if (app.Model != 2 || count.Value != 2) throw new System.Exception("MVU dispatch or selector failed");
using var client = new QueryClient(graph);
var numbers = client.Define(System.Collections.Generic.EqualityComparer<int>.Default,
    FuncConvert.FromFunc<int, System.Threading.CancellationToken, System.Threading.Tasks.Task<int>>((key, token) => System.Threading.Tasks.Task.FromResult(key)));
using var query = numbers.Acquire(7);
if (query.Value != 7) throw new System.Exception("Query package failed");
System.Console.WriteLine("Package consumer runs with FSharp.Core 8.0.100");
'@ | Set-Content -LiteralPath (Join-Path $consumer 'Program.cs')
        Invoke-DotNet restore (Join-Path $consumer 'Consumer.csproj') --configfile (Join-Path $consumer 'NuGet.Config')
        Invoke-DotNet run --project (Join-Path $consumer 'Consumer.csproj') -c Release --no-restore
        if ($CheckFable) {
            $fableDirectory = Join-Path $repo 'fable/Ranvier.Tests.Fable'
            $fableConsumer = Join-Path $consumer 'FableConsumer.fsproj'
            $fableXml = [xml](Get-Content -LiteralPath (Join-Path $fableDirectory 'Ranvier.Tests.Fable.fsproj') -Raw)
            foreach ($item in $fableXml.SelectNodes('//Compile | //Import')) {
                $attribute = if ($item.Name -eq 'Import') { 'Project' } else { 'Include' }
                $item.SetAttribute($attribute, [IO.Path]::GetFullPath((Join-Path $fableDirectory $item.GetAttribute($attribute).Replace('\', '/'))))
            }
            foreach ($reference in @($fableXml.SelectNodes('//ProjectReference'))) {
                $name = [IO.Path]::GetFileNameWithoutExtension($reference.Include.Replace('\', '/'))
                $replacement = $fableXml.CreateElement('PackageReference')
                $replacement.SetAttribute('Include', $(if ($traced) { "$name.Traced" } else { $name }))
                $version = ([xml](Get-Content -LiteralPath (Join-Path $repo "src/$name/$name.fsproj") -Raw)).Project.PropertyGroup.Version | Where-Object { $_ }
                $replacement.SetAttribute('Version', $version)
                $reference.ParentNode.ReplaceChild($replacement, $reference) | Out-Null
            }
            $packagesPath = $fableXml.CreateElement('RestorePackagesPath')
            $packagesPath.InnerText = Join-Path $temp 'packages'
            $fableXml.Project.PropertyGroup.AppendChild($packagesPath) | Out-Null
            $fableXml.Save($fableConsumer)
            Invoke-DotNet restore $fableConsumer --configfile (Join-Path $consumer 'NuGet.Config')
            $output = Join-Path $consumer $(if ($traced) { 'fable-traced' } else { 'fable' })
            $fableArgs = @('fickl', $fableConsumer, '-e', '.fs.js', '-o', $output, '-c', 'Release')
            if ($traced) { $fableArgs += @('--define', 'RANVIER_TRACE') }
            Invoke-DotNet @fableArgs
            '{"type":"module"}' | Set-Content -LiteralPath (Join-Path $output 'package.json')
            $entryPoints = @(Get-ChildItem -LiteralPath $output -Recurse -Filter 'Main.fs.js')
            if ($entryPoints.Count -ne 1) { throw 'Expected one Fable test entry point' }
            $fableEntries += $entryPoints[0].FullName
        }
    }
    if (-not $CheckFable) {
        # An F# consumer of Ranvier.Analyzers: RanvierAnalyze reports RAN002 as a build warning through the package's
        # build/ targets and the fsharp-analyzers tool, and an error-severity RAN010 fails the build.
        Invoke-DotNet pack (Join-Path $repo 'tools/Ranvier.Analyzers/Ranvier.Analyzers.fsproj') -c Release -o $feed
        $analyzerConsumer = Join-Path $temp 'analyzer-consumer'
        New-Item -ItemType Directory -Path (Join-Path $analyzerConsumer '.config') | Out-Null
        Copy-Item (Join-Path $consumer 'NuGet.Config') $analyzerConsumer
        Copy-Item (Join-Path $repo 'global.json') $analyzerConsumer
        @'
{ "version": 1, "isRoot": true, "tools": { "fsharp-analyzers": { "version": "0.41.0", "commands": [ "fsharp-analyzers" ], "rollForward": false }, "fable": { "version": "5.18.0", "commands": [ "fable" ], "rollForward": false } } }
'@ | Set-Content -LiteralPath (Join-Path $analyzerConsumer '.config/dotnet-tools.json')
        $analyzerVersion = ([xml](Get-Content -LiteralPath (Join-Path $repo 'tools/Ranvier.Analyzers/Ranvier.Analyzers.fsproj') -Raw)).Project.PropertyGroup.Version | Where-Object { $_ }
        $ranvierVersion = ([xml](Get-Content -LiteralPath (Join-Path $repo 'src/Ranvier/Ranvier.fsproj') -Raw)).Project.PropertyGroup.Version | Where-Object { $_ }
        @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RanvierAnalyze>true</RanvierAnalyze>
    <RestorePackagesPath>$temp/packages</RestorePackagesPath>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="Library.fs" />
    <Compile Include="Error.fs" Condition="'`$(WithError)' == 'true'" />
    <PackageReference Include="Fable.Core" Version="5.3.0" />
    <PackageReference Include="Ranvier" Version="$ranvierVersion" />
    <PackageReference Include="Ranvier.Analyzers" Version="$analyzerVersion" PrivateAssets="all" />
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $analyzerConsumer 'AnalyzerConsumer.fsproj')
        @'
module Library

type Policy =
    | Queue
    | Drop of int

let queued (policy: Policy) = policy.IsQueue
'@ | Set-Content -LiteralPath (Join-Path $analyzerConsumer 'Library.fs')
        @'
module Error

open Ranvier

let label () = createMemo (fun _ -> onCleanup ignore; 1)
'@ | Set-Content -LiteralPath (Join-Path $analyzerConsumer 'Error.fs')
        Push-Location $analyzerConsumer
        try {
            Invoke-DotNet tool restore
            $build = & dotnet build AnalyzerConsumer.fsproj -c Release 2>&1 | Out-String
            if ($LASTEXITCODE -ne 0) { throw "The analyzer consumer failed to build:`n$build" }
            if ($build -notmatch 'warning RAN002') { throw "The analyzer consumer's build reported no RAN002:`n$build" }
            $failing = & dotnet build AnalyzerConsumer.fsproj -c Release -p:WithError=true 2>&1 | Out-String
            if ($LASTEXITCODE -eq 0) { throw "An error-severity RAN010 did not fail the analyzer consumer's build:`n$failing" }
            if ($failing -notmatch 'error RAN010') { throw "The failing build reported no RAN010:`n$failing" }
        } finally { Pop-Location }
        Write-Host 'Analyzer package consumer reports RAN002, and RAN010 fails its build'
    }
    if ($CheckFable) {
        & node (Join-Path $repo 'fable/Ranvier.Tests.Fable/Report.mjs') @fableEntries (Join-Path $temp 'fable-compat.md')
        if ($LASTEXITCODE -ne 0) { throw 'Fable package consumer compatibility check failed' }
    }
} finally {
    $resolved = [IO.Path]::GetFullPath($temp)
    if (-not $resolved.StartsWith([IO.Path]::GetFullPath([IO.Path]::GetTempPath()), [StringComparison]::OrdinalIgnoreCase)) {
        throw 'Refusing cleanup outside the temporary directory'
    }
    Remove-Item -LiteralPath $resolved -Recurse -Force
}
