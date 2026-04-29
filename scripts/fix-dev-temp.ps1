<#
.SYNOPSIS
  Reset the current user's temp variables to portable Windows defaults.

.DESCRIPTION
  NuGet uses the process temp directory while restoring packages. If TEMP/TMP
  point to a machine-specific repo path from another checkout, restore can fail
  before project.assets.json is generated.

  Run this once on each development machine if Visual Studio or dotnet restore
  reports access errors under NuGetScratch.
#>

$ErrorActionPreference = "Stop"

$tempValue = "%USERPROFILE%\AppData\Local\Temp"
$expandedTemp = [Environment]::ExpandEnvironmentVariables($tempValue)
New-Item -ItemType Directory -Force -Path $expandedTemp | Out-Null

$reg = [Microsoft.Win32.Registry]::CurrentUser.OpenSubKey("Environment", $true)
if (-not $reg) {
    throw "Cannot open HKCU\Environment for writing."
}

try {
    $reg.SetValue("TEMP", $tempValue, [Microsoft.Win32.RegistryValueKind]::ExpandString)
    $reg.SetValue("TMP", $tempValue, [Microsoft.Win32.RegistryValueKind]::ExpandString)
    $reg.DeleteValue("TMPDIR", $false)
}
finally {
    $reg.Close()
}

$env:TEMP = $expandedTemp
$env:TMP = $expandedTemp
Remove-Item Env:TMPDIR -ErrorAction SilentlyContinue

$signature = @"
using System;
using System.Runtime.InteropServices;

public static class HermesEnvironmentBroadcast {
    [DllImport("user32.dll", SetLastError=true, CharSet=CharSet.Auto)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        UInt32 Msg,
        UIntPtr wParam,
        string lParam,
        UInt32 fuFlags,
        UInt32 uTimeout,
        out UIntPtr lpdwResult);
}
"@

if (-not ("HermesEnvironmentBroadcast" -as [type])) {
    Add-Type $signature
}

$result = [UIntPtr]::Zero
[HermesEnvironmentBroadcast]::SendMessageTimeout(
    [IntPtr]0xffff,
    0x001A,
    [UIntPtr]::Zero,
    "Environment",
    0x0002,
    5000,
    [ref]$result) | Out-Null

Write-Host "TEMP=$env:TEMP"
Write-Host "TMP=$env:TMP"
Write-Host "TMPDIR=$env:TMPDIR"
Write-Host "Restart Visual Studio and terminals so they read the updated user environment."
