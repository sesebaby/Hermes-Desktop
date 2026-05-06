<#
.SYNOPSIS
  Synchronize the local Stardew NPC autonomy allowlist in Hermes config.yaml.

.DESCRIPTION
  Updates only the stardew.npc_autonomy_enabled_ids key in the local Hermes
  config file. Other sections, provider keys, and unrelated Stardew settings
  are preserved.

.PARAMETER ConfigPath
  Path to config.yaml. Defaults to %HERMES_HOME%\config.yaml when HERMES_HOME
  is set, otherwise %LOCALAPPDATA%\hermes\config.yaml.

.PARAMETER EnabledNpcIds
  Comma-separated NPC ids to enable. Defaults to haley,penny.

.PARAMETER NoBackup
  Do not write a .bak timestamped backup next to the config file.

.EXAMPLE
  .\scripts\sync-stardew-npc-config.ps1

.EXAMPLE
  .\scripts\sync-stardew-npc-config.ps1 -EnabledNpcIds "haley,penny"
#>
param(
    [string] $ConfigPath,

    [ValidateNotNullOrEmpty()]
    [string] $EnabledNpcIds = "haley,penny",

    [switch] $NoBackup
)

$ErrorActionPreference = "Stop"

if (-not $ConfigPath) {
    $hermesHome = if ($env:HERMES_HOME) {
        $env:HERMES_HOME
    } else {
        Join-Path $env:LOCALAPPDATA "hermes"
    }

    $ConfigPath = Join-Path $hermesHome "config.yaml"
}

$configDir = Split-Path -Parent $ConfigPath
if ($configDir -and -not (Test-Path $configDir)) {
    New-Item -ItemType Directory -Path $configDir -Force | Out-Null
}

$lines = [System.Collections.Generic.List[string]]::new()
if (Test-Path $ConfigPath) {
    foreach ($line in Get-Content -LiteralPath $ConfigPath) {
        $lines.Add($line)
    }
}

if ((Test-Path $ConfigPath) -and -not $NoBackup) {
    $backupPath = "$ConfigPath.bak.$((Get-Date).ToString('yyyyMMddHHmmss'))"
    Copy-Item -LiteralPath $ConfigPath -Destination $backupPath -Force
    Write-Host "Backup: $backupPath" -ForegroundColor DarkGray
}

$sectionName = "stardew"
$keyName = "npc_autonomy_enabled_ids"
$keyLine = "  ${keyName}: $EnabledNpcIds"
$sectionStart = -1
$sectionEnd = $lines.Count

for ($i = 0; $i -lt $lines.Count; $i++) {
    $raw = $lines[$i]
    $trimmed = $raw.TrimEnd()
    if ([string]::IsNullOrWhiteSpace($trimmed)) {
        continue
    }

    if ($raw.Length -gt 0 -and -not [char]::IsWhiteSpace($raw[0]) -and $trimmed.EndsWith(":")) {
        if ($trimmed.Equals("${sectionName}:", [System.StringComparison]::OrdinalIgnoreCase)) {
            $sectionStart = $i
            $sectionEnd = $lines.Count
            continue
        }

        if ($sectionStart -ge 0) {
            $sectionEnd = $i
            break
        }
    }
}

if ($sectionStart -lt 0) {
    if ($lines.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($lines[$lines.Count - 1])) {
        $lines.Add("")
    }

    $lines.Add("${sectionName}:")
    $lines.Add($keyLine)
} else {
    $keyIndex = -1
    for ($i = $sectionStart + 1; $i -lt $sectionEnd; $i++) {
        $trimmed = $lines[$i].Trim()
        if ($trimmed.StartsWith("${keyName}:", [System.StringComparison]::OrdinalIgnoreCase)) {
            $keyIndex = $i
            break
        }
    }

    if ($keyIndex -ge 0) {
        $lines[$keyIndex] = $keyLine
    } else {
        $insertAt = $sectionStart + 1
        while ($insertAt -lt $sectionEnd -and [string]::IsNullOrWhiteSpace($lines[$insertAt])) {
            $insertAt++
        }

        $lines.Insert($insertAt, $keyLine)
    }
}

[System.IO.File]::WriteAllLines($ConfigPath, $lines)
Write-Host "Stardew NPC autonomy enabled ids synced: $EnabledNpcIds" -ForegroundColor Green
Write-Host "Config: $ConfigPath" -ForegroundColor DarkGray
