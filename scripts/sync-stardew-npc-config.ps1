<#
.SYNOPSIS
  Synchronize the local Stardew NPC config and default local LLM lanes in Hermes config.yaml.

.DESCRIPTION
  Updates the root model, the Stardew NPC autonomy allowlist, and the
  delegation lane used by NPC child-agent work so this project can run against
  a local OpenAI-compatible LM Studio model instead of the previous cloud model.
  The Stardew autonomy lane is configured to request JSON-object output from
  OpenAI-compatible providers that support response_format.
  delegation.max_spawn_depth is emitted only as a reserved flat-only v1 marker;
  current Hermes does not enforce nested delegation depth.

.PARAMETER ConfigPath
  Path to config.yaml. Defaults to %HERMES_HOME%\config.yaml when HERMES_HOME
  is set, otherwise the repository-local .hermes\config.yaml.

.PARAMETER EnabledNpcIds
  Comma-separated NPC ids to enable. Defaults to haley,penny.

.PARAMETER Provider
  Provider for the root model and delegation lane. Defaults to openai for
  OpenAI-compatible local endpoints such as LM Studio.

.PARAMETER BaseUrl
  Base URL for the root model and delegation lane. Defaults to
  http://127.0.0.1:1234/v1.

.PARAMETER Model
  Model id for the root model and delegation lane. Defaults to
  qwen3.5-2b-gpt-5.1-highiq-instruct-i1.

.PARAMETER ApiKey
  API key/token placeholder for the local OpenAI-compatible model endpoint.
  Defaults to lm-studio.

.PARAMETER AuthMode
  Auth mode written into the model and delegation sections. Defaults to
  api_key.

.PARAMETER AutonomyResponseFormat
  Response format written into the stardew_autonomy section. Defaults to
  json_object for DeepSeek JSON Output-compatible autonomy decisions.

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

    [ValidateNotNullOrEmpty()]
    [string] $Provider = "openai",

    [ValidateNotNullOrEmpty()]
    [string] $BaseUrl = "http://127.0.0.1:1234/v1",

    [ValidateNotNullOrEmpty()]
    [string] $Model = "qwen3.5-2b-gpt-5.1-highiq-instruct-i1",

    [ValidateNotNullOrEmpty()]
    [string] $ApiKey = "lm-studio",

    [ValidateNotNullOrEmpty()]
    [string] $AuthMode = "api_key",

    [ValidateNotNullOrEmpty()]
    [string] $AutonomyResponseFormat = "json_object",

    [switch] $NoBackup
)

$ErrorActionPreference = "Stop"

function Set-TopLevelSection {
    param(
        [System.Collections.Generic.List[string]] $Lines,
        [string] $SectionName,
        [string[]] $Entries
    )

    $sectionStart = -1
    $sectionEnd = $Lines.Count

    for ($i = 0; $i -lt $Lines.Count; $i++) {
        $raw = $Lines[$i]
        $trimmed = $raw.TrimEnd()
        if ([string]::IsNullOrWhiteSpace($trimmed)) {
            continue
        }

        if ($raw.Length -gt 0 -and -not [char]::IsWhiteSpace($raw[0]) -and $trimmed.EndsWith(":")) {
            if ($trimmed.Equals("${SectionName}:", [System.StringComparison]::OrdinalIgnoreCase)) {
                $sectionStart = $i
                $sectionEnd = $Lines.Count
                continue
            }

            if ($sectionStart -ge 0) {
                $sectionEnd = $i
                break
            }
        }
    }

    $sectionLines = [System.Collections.Generic.List[string]]::new()
    $sectionLines.Add("${SectionName}:")
    foreach ($entry in $Entries) {
        $sectionLines.Add("  $entry")
    }
    $sectionLines.Add("")

    if ($sectionStart -lt 0) {
        if ($Lines.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($Lines[$Lines.Count - 1])) {
            $Lines.Add("")
        }

        foreach ($line in $sectionLines) {
            $Lines.Add($line)
        }

        return
    }

    for ($i = $sectionEnd - 1; $i -ge $sectionStart; $i--) {
        $Lines.RemoveAt($i)
    }

    for ($i = 0; $i -lt $sectionLines.Count; $i++) {
        $Lines.Insert($sectionStart + $i, $sectionLines[$i])
    }
}

if (-not $ConfigPath) {
    $hermesHome = if ($env:HERMES_HOME) {
        $env:HERMES_HOME
    } else {
        Join-Path (Split-Path -Parent $PSScriptRoot) ".hermes"
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

Set-TopLevelSection -Lines $lines -SectionName "delegation" -Entries @(
    "provider: $Provider",
    "base_url: $BaseUrl",
    "model: $Model",
    "auth_mode: $AuthMode",
    "# LM Studio accepts this placeholder; do not commit a real token here.",
    "api_key: $ApiKey",
    "# max_spawn_depth is reserved for future nested delegation; flat-only v1 ignores it.",
    "max_spawn_depth: 1",
    "# max_concurrent_children is reserved for future fan-out policy; flat-only v1 ignores it.",
    "max_concurrent_children: 1"
)

Set-TopLevelSection -Lines $lines -SectionName "model" -Entries @(
    "provider: $Provider",
    "base_url: $BaseUrl",
    "default: $Model",
    "auth_mode: $AuthMode",
    "# LM Studio accepts this placeholder; do not commit a real token here.",
    "api_key: $ApiKey"
)

Set-TopLevelSection -Lines $lines -SectionName "stardew_autonomy" -Entries @(
    "response_format: $AutonomyResponseFormat"
)

Set-TopLevelSection -Lines $lines -SectionName "stardew" -Entries @(
    "npc_autonomy_enabled_ids: $EnabledNpcIds"
)

[System.IO.File]::WriteAllLines($ConfigPath, $lines)
Write-Host "Stardew NPC config synced with local root and delegation LLM lanes." -ForegroundColor Green
Write-Host "NPCs: $EnabledNpcIds" -ForegroundColor DarkGray
Write-Host "Provider: $Provider" -ForegroundColor DarkGray
Write-Host "Base URL: $BaseUrl" -ForegroundColor DarkGray
Write-Host "Model: $Model" -ForegroundColor DarkGray
Write-Host "Delegation depth: max_spawn_depth is reserved and ignored by flat-only v1." -ForegroundColor DarkGray
Write-Host "Autonomy Response Format: $AutonomyResponseFormat" -ForegroundColor DarkGray
Write-Host "Config: $ConfigPath" -ForegroundColor DarkGray
