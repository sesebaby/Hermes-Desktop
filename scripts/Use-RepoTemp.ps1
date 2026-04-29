param(
    [Parameter(Mandatory = $true)]
    [string] $RepoRoot
)

$repoTemp = Join-Path $RepoRoot ".tmp"
New-Item -ItemType Directory -Force -Path $repoTemp | Out-Null

$env:TEMP = $repoTemp
$env:TMP = $repoTemp
$env:TMPDIR = $repoTemp
