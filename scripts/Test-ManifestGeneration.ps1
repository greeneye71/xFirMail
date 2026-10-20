# SPDX-License-Identifier: EUPL-1.2
# Invoke only the generation target; do not compile or publish the application.
[CmdletBinding()]
param([string]$DotnetPath)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (-not $DotnetPath) {
    $DotnetPath = Join-Path $repo '.tools/dotnet/dotnet.exe'
    if (-not (Test-Path -LiteralPath $DotnetPath)) { $DotnetPath = (Get-Command dotnet -ErrorAction Stop).Source }
}
$folder = Join-Path $repo ('tmp/manifest-check-' + [Guid]::NewGuid().ToString('N'))
foreach ($version in @('1.0.2', '7.8.9')) {
    $output = Join-Path $folder $version
    & $DotnetPath msbuild (Join-Path $repo 'src/Xfir.App/Xfir.App.csproj') -t:XfirGenerateWin32Manifest `
        "-p:Version=$version" "-p:IntermediateOutputPath=$output/" -nologo -verbosity:quiet
    if ($LASTEXITCODE -ne 0) { throw 'Manifest generation failed.' }
    [xml]$manifest = Get-Content -LiteralPath (Join-Path $output 'xfir.generated.manifest') -Raw
    if ($manifest.assembly.assemblyIdentity.version -ne "$version.0") { throw 'Manifest and build versions differ.' }
    Write-Output "PASS: generated manifest $version.0."
}
[xml]$template = Get-Content -LiteralPath (Join-Path $repo 'src/Xfir.App/app.manifest') -Raw
if ($template.assembly.assemblyIdentity.version -ne '0.0.0.0') { throw 'Source template was modified.' }
