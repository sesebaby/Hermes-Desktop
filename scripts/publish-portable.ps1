<#
.SYNOPSIS
  Publish Hermes Desktop as a self-contained, unpackaged folder that anyone can unzip and run.

.DESCRIPTION
  Produces a portable folder (no .NET SDK, no MSIX, no Windows App Runtime installer required).
  The output directory contains HermesDesktop.exe and all dependencies — users just double-click.

  This sidesteps all MSIX signing/registration bugs while Microsoft ships fixes.

  Steps:
    1) Build gate — fast dotnet build to catch compile errors before the slower publish
    2) dotnet publish  (self-contained, trimming OFF, ReadyToRun ON)
    3) Bundle skills/ into publish output
    4) Optionally compress to HermesDesktop-portable-{arch}.zip

  The output zip is what gets uploaded to GitHub Releases.

.PARAMETER Configuration
  Release (default) or Debug.

.PARAMETER Platform
  Target architecture: x64 (default) or ARM64.

.PARAMETER OutputDir
  Override the publish output directory. Defaults to Desktop\HermesDesktop\bin\publish-portable\.

.PARAMETER Zip
  If set, creates a .zip archive of the output folder for easy distribution.

.EXAMPLE
  .\scripts\publish-portable.ps1
  # -> Desktop\HermesDesktop\bin\publish-portable\HermesDesktop.exe

.EXAMPLE
  .\scripts\publish-portable.ps1 -Zip
  # -> Desktop\HermesDesktop\bin\HermesDesktop-portable-x64.zip

.EXAMPLE
  .\scripts\publish-portable.ps1 -Configuration Debug -Platform ARM64
#>
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [ValidateSet("x64", "ARM64")]
    [string] $Platform = "x64",

    [string] $OutputDir,

    [switch] $Zip
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

$csproj = Join-Path $repoRoot "Desktop\HermesDesktop\HermesDesktop.csproj"
$rid = if ($Platform -eq "ARM64") { "win-arm64" } else { "win-x64" }

if (-not $OutputDir) {
    $OutputDir = Join-Path $repoRoot "Desktop\HermesDesktop\bin\publish-portable"
}

# --- 1. Build gate: fast compile check before the slower publish step ---
Write-Host ""
Write-Host "=== Build gate (dotnet build -c $Configuration) ===" -ForegroundColor Cyan
dotnet build $csproj -c $Configuration -p:Platform=$Platform
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed - fix the errors above before publishing."
}

# --- 2. Publish (self-contained, trimming OFF to avoid WinUI runtime crashes) ---
if (Test-Path $OutputDir) {
    Write-Host "Cleaning previous publish: $OutputDir" -ForegroundColor Yellow
    Remove-Item -Recurse -Force $OutputDir
}

$publishArgs = @(
    "publish", $csproj,
    "-c", $Configuration,
    "-r", $rid,
    "--self-contained", "true",
    "-p:Platform=$Platform",
    "-p:PublishTrimmed=false",
    "-p:WindowsPackageType=None",
    "-p:WindowsAppSDKSelfContained=true",
    "-p:WindowsAppSdkDeploymentManagerInitialize=false",
    # PublishTrimmed MUST stay false: WinUI 3 / WinApp SDK 1.7 compiled bindings
    # (x:Bind with x:DataType) and XamlTypeInfoProvider activation are not trim-safe
    # and the linker silently strips members the activator needs at runtime,
    # producing "Cannot create instance of type <UserControl>" XamlParseException
    # at startup. Pass explicitly so a future csproj edit can't silently re-enable
    # trimming for the portable release.
    "-p:PublishTrimmed=false",
    "-o", $OutputDir,
    "-v:minimal"
)

if ($Configuration -eq "Release") {
    $publishArgs += "-p:PublishReadyToRun=true"
}

Write-Host ""
Write-Host "Publishing Hermes Desktop - portable, $rid, $Configuration ..." -ForegroundColor Cyan
Write-Host "Output: $OutputDir" -ForegroundColor DarkGray
Write-Host ""

dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}

# --- 3. Bundle the manifest-driven retain set into the publish folder ---
$bundledSkills = Join-Path $repoRoot "skills"
$targetSkills = Join-Path $OutputDir "skills"
if (Test-Path $targetSkills) {
    Remove-Item -Recurse -Force $targetSkills
}

$manifestPath = Join-Path $bundledSkills ".bundled-skills-manifest.json"
if (-not (Test-Path $manifestPath)) {
    throw "Bundled skill manifest not found: $manifestPath"
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
New-Item -ItemType Directory -Path $targetSkills -Force | Out-Null
Copy-Item -Force $manifestPath (Join-Path $targetSkills ".bundled-skills-manifest.json")

function Copy-RetainedSkillRoot {
    param(
        [Parameter(Mandatory=$true)][string]$SourceRootRelativePath
    )

    $sourceRoot = Join-Path $bundledSkills $SourceRootRelativePath
    $destinationRoot = Join-Path $targetSkills $SourceRootRelativePath
    if (-not (Test-Path $sourceRoot)) {
        throw "Retained bundled skill root is missing: $sourceRoot"
    }

    if (Test-Path $destinationRoot) {
        Remove-Item -Recurse -Force $destinationRoot
    }

    New-Item -ItemType Directory -Path $destinationRoot -Force | Out-Null
    Copy-Item -Recurse -Force $sourceRoot\* $destinationRoot

    $segments = $SourceRootRelativePath -split '[\\/]' | Where-Object { $_ -and $_.Trim().Length -gt 0 }
    if ($segments.Count -gt 1) {
        for ($i = 0; $i -lt $segments.Count - 1; $i++) {
            $categoryRelative = ($segments[0..$i] -join [IO.Path]::DirectorySeparatorChar)
            $sourceDescription = Join-Path (Join-Path $bundledSkills $categoryRelative) "DESCRIPTION.md"
            if (Test-Path $sourceDescription) {
                $destinationDescription = Join-Path (Join-Path $targetSkills $categoryRelative) "DESCRIPTION.md"
                New-Item -ItemType Directory -Path (Split-Path $destinationDescription) -Force | Out-Null
                Copy-Item -Force $sourceDescription $destinationDescription
            }
        }
    }
}

Write-Host "Bundling manifest-driven retain skills into publish output..." -ForegroundColor DarkGray
foreach ($skill in $manifest.skills | Where-Object { $_.disposition -eq "retain" }) {
    foreach ($sourceRoot in $skill.sourceRoots) {
        Copy-RetainedSkillRoot -SourceRootRelativePath $sourceRoot.rootRelativePath
    }
}

$exe = Join-Path $OutputDir "HermesDesktop.exe"
if (-not (Test-Path $exe)) {
    Write-Error "Publish output missing HermesDesktop.exe under $OutputDir"
}

Write-Host ""
Write-Host "Portable publish complete." -ForegroundColor Green
Write-Host "  Folder: $OutputDir" -ForegroundColor White
Write-Host "  Exe:    $exe" -ForegroundColor White
Write-Host ""

# --- 4. Zip (optional) ---
if ($Zip) {
    $zipName = "HermesDesktop-portable-$($Platform.ToLower()).zip"
    $zipPath = Join-Path (Split-Path $OutputDir) $zipName
    if (Test-Path $zipPath) { Remove-Item -Force $zipPath }

    Write-Host "Creating archive: $zipPath" -ForegroundColor Cyan
    Compress-Archive -Path "$OutputDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Host "Archive: $zipPath" -ForegroundColor Green

    $sizeMb = [math]::Round((Get-Item $zipPath).Length / 1MB, 1)
    Write-Host "Size: ${sizeMb} MB" -ForegroundColor DarkGray
    Write-Host ""
}

Write-Host "Users can run HermesDesktop.exe directly - no .NET SDK, no MSIX, no Windows App Runtime install needed." -ForegroundColor Cyan
Write-Host 'First run creates %LOCALAPPDATA%\hermes with config, memory, and logs.' -ForegroundColor DarkGray
