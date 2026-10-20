# SPDX-License-Identifier: EUPL-1.2
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Layout,
    [Parameter(Mandatory = $true)][Version]$MinimumVersion
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

# NuGet auditing does not cover DLLs supplied by the shared Windows Desktop framework.
# Check the actual self-contained output, including the framework's copy of Pkcs.
$runtimeConfig = Get-Content -LiteralPath (Join-Path $Layout 'xFirW.runtimeconfig.json') -Raw | ConvertFrom-Json
foreach ($framework in @('Microsoft.NETCore.App', 'Microsoft.WindowsDesktop.App')) {
    $included = @($runtimeConfig.runtimeOptions.includedFrameworks | Where-Object { $_.name -eq $framework })
    if ($included.Count -ne 1 -or [Version]$included[0].version -lt $MinimumVersion) {
        throw "Package must include $framework $MinimumVersion or newer. Update the .NET SDK/runtime packs."
    }
}
foreach ($component in @('System.Private.CoreLib.dll', 'System.Security.Cryptography.Pkcs.dll')) {
    $path = Join-Path $Layout $component
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing runtime component: $component" }
    # Microsoft also uses labels such as 10.0.13-servicing.<build> on released runtime DLLs.
    $version = ([Diagnostics.FileVersionInfo]::GetVersionInfo($path).ProductVersion -split '[-+]')[0]
    if ([Version]$version -lt $MinimumVersion) {
        throw "Vulnerable runtime component $component ($version); require $MinimumVersion or newer."
    }
}
