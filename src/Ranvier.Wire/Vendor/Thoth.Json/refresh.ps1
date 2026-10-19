# Rebuilds the vendored Thoth.Json files from the pinned upstream commit plus ./patches (applied in name order),
# or, with -Check, fails when a committed file differs from that result. Needs network access to GitHub.
#
#   pwsh src/Ranvier.Wire/Vendor/Thoth.Json/refresh.ps1 [-Check]
param([switch]$Check)

$ErrorActionPreference = 'Stop'
$pin = 'de668a4a6378245d86a96427cc90c5da23d195e2'
$files = [ordered]@{
    'Thoth.Json.Core'             = @('Types.fs', 'Decode.fs', 'Encode.fs', 'DecoderCE.fs', 'Codec.fs', 'Object.fs', 'Variant.fs')
    'Thoth.Json.System.Text.Json' = @('Decode.fs', 'Encode.fs')
    'Thoth.Json.JavaScript'       = @('Decode.fs', 'Encode.fs')
}
$work = Join-Path ([IO.Path]::GetTempPath()) ('ranvier-thoth-' + [guid]::NewGuid().ToString('N'))

function Invoke-Git {
    & git @args
    if ($LASTEXITCODE -ne 0) { throw "git $($args -join ' ') failed with exit code $LASTEXITCODE" }
}

function Read-Normalised([string]$path) { (Get-Content -LiteralPath $path -Raw) -replace "`r`n", "`n" }

try {
    Invoke-Git -c core.autocrlf=false clone --quiet --no-checkout https://github.com/thoth-org/Thoth.Json.git $work
    Invoke-Git -C $work -c core.autocrlf=false checkout --quiet $pin
    $patches = @(Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'patches') -Filter '*.patch' | Sort-Object Name)
    foreach ($patch in $patches) { Invoke-Git -C $work apply $patch.FullName }

    $pairs = @(@{ Source = 'LICENSE.txt'; Target = 'LICENSE.txt' })
    foreach ($package in $files.Keys) {
        foreach ($file in $files[$package]) { $pairs += @{ Source = "packages/$package/$file"; Target = "$package/$file" } }
    }

    $differences = 0
    foreach ($pair in $pairs) {
        $source = Join-Path $work $pair.Source
        $target = Join-Path $PSScriptRoot $pair.Target
        if ($Check) {
            if (-not (Test-Path -LiteralPath $target) -or (Read-Normalised $source) -ne (Read-Normalised $target)) {
                Write-Host "differs from the pin plus patches: $($pair.Target)"
                $differences++
            }
        } else {
            New-Item -ItemType Directory -Force -Path (Split-Path $target) | Out-Null
            Copy-Item -LiteralPath $source -Destination $target -Force
        }
    }
    if ($differences -gt 0) { throw "$differences vendored file(s) differ; run refresh.ps1 without -Check and review the diff" }
    $verb = if ($Check) { 'matches' } else { 'refreshed from' }
    Write-Host "vendored Thoth.Json $verb $pin plus $($patches.Count) patch(es)"
} finally {
    if (Test-Path -LiteralPath $work) { Remove-Item -LiteralPath $work -Recurse -Force }
}
