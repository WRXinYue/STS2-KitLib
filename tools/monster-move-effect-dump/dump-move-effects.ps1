# Regenerate monster-move-effects.json from official STS2 monster source.
param(
    [string]$Sts2MonstersDir = "C:\Users\WRXinYue\Documents\Project\STS2\Slay the Spire 2\src\Core\Models\Monsters"
)

$scriptDir = Split-Path $PSScriptRoot -Parent
$repoRoot = Split-Path $scriptDir -Parent
$py = Join-Path $PSScriptRoot "extract-move-effects.py"

if (-not (Test-Path $Sts2MonstersDir)) {
    Write-Error "Official monsters dir not found: $Sts2MonstersDir"
    exit 1
}

python $py
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$out = Join-Path $repoRoot "src\AI\Data\monster-move-effects.json"
Write-Host "Generated $out"
