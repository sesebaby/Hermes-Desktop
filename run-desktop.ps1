# From repo root: .\run-desktop.ps1
# Optional: .\run-desktop.ps1 -Rebuild   (full rebuild first; note -t:Rebuild not Rebuild.)
param(
    [switch] $Rebuild
)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot
if (-not $env:HERMES_HOME) {
    $env:HERMES_HOME = Join-Path $PSScriptRoot ".hermes"
}

New-Item -ItemType Directory -Path $env:HERMES_HOME -Force | Out-Null
. (Join-Path $PSScriptRoot "scripts\Use-RepoTemp.ps1") -RepoRoot $PSScriptRoot

if ($Rebuild) {
    dotnet build HermesDesktop.sln -t:Rebuild
}

dotnet run --project "Desktop\HermesDesktop\HermesDesktop.csproj" @args
