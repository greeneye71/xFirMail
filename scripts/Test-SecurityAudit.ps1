# SPDX-License-Identifier: EUPL-1.2
# Integration test: a deliberately unavailable loopback audit source must block release restore.
[CmdletBinding()]
param([string]$DotnetPath)
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
if (-not $DotnetPath) {
    $DotnetPath = Join-Path $repo '.tools/dotnet/dotnet.exe'
    if (-not (Test-Path -LiteralPath $DotnetPath)) { $DotnetPath = (Get-Command dotnet -ErrorAction Stop).Source }
}
$folder = Join-Path $repo ('tmp/audit-failure-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $folder -Force | Out-Null
$listener = New-Object Net.Sockets.TcpListener ([Net.IPAddress]::Loopback), 0
$listener.Start()
$port = $listener.LocalEndpoint.Port
$listener.Stop()
$props = [Security.SecurityElement]::Escape((Join-Path $repo 'Directory.Build.props'))
$cache = $env:NUGET_PACKAGES
if (-not $cache) { $cache = Join-Path $env:USERPROFILE '.nuget/packages' }
$escapedCache = [Security.SecurityElement]::Escape($cache)
@"
<Project Sdk="Microsoft.NET.Sdk">
  <Import Project="$props" />
  <ItemGroup><PackageReference Include="System.Security.Cryptography.Pkcs" Version="8.0.2" /></ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $folder 'AuditProbe.csproj') -Encoding UTF8
@"
<configuration>
  <packageSources><clear /><add key="cached-packages" value="$escapedCache" /></packageSources>
  <auditSources><clear /><add key="unavailable-audit" value="https://127.0.0.1:$port/v3/index.json" /></auditSources>
</configuration>
"@ | Set-Content -LiteralPath (Join-Path $folder 'NuGet.Config') -Encoding UTF8
foreach ($required in @('false', 'true')) {
    $log = & $DotnetPath restore (Join-Path $folder 'AuditProbe.csproj') --configfile (Join-Path $folder 'NuGet.Config') `
        --force-evaluate --no-http-cache -m:1 "-p:SecurityAuditRequired=$required" 2>&1
    $code = $LASTEXITCODE
    $text = $log -join "`n"
    $text | Set-Content -LiteralPath (Join-Path $folder ("audit-$required.log")) -Encoding UTF8
    if ($text -notmatch 'NU1900') { throw "Audit availability was not exercised. See $folder" }
    if ($required -eq 'true' -and $code -eq 0) { throw 'Release restore accepted an unavailable audit source.' }
    if ($required -eq 'false' -and $code -ne 0) { throw "Local offline restore failed unexpectedly. See $folder" }
    Write-Output "SecurityAuditRequired=$required -> exit $code (NU1900 verified)."
}
Write-Output "PASS: release restore is blocked, local offline restore retains a warning. Logs: $folder"
