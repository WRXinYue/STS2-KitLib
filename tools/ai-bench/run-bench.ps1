param(
    [string]$LogPath = "",
    [string]$SeedsPath = "",
    [string]$ResultsPath = ""
)

$repoRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
if (-not $SeedsPath) { $SeedsPath = Join-Path $PSScriptRoot "seeds.json" }
if (-not $ResultsPath) { $ResultsPath = Join-Path $PSScriptRoot "results.csv" }

if (-not $LogPath) {
    $candidates = @(
        (Join-Path $repoRoot "godot.log"),
        (Join-Path $env:APPDATA "Godot\app_userdata\Slay the Spire 2\logs\godot.log")
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) { $LogPath = $c; break }
    }
}

Write-Host "=== AI A10 Bench ==="
Write-Host "Seeds: $SeedsPath"
Write-Host "Log:   $(if ($LogPath) { $LogPath } else { '(not found)' })"
Write-Host ""

$seeds = Get-Content $SeedsPath -Raw | ConvertFrom-Json
Write-Host "Fixed seed set ($($seeds.Count) runs):"
foreach ($s in $seeds) {
    Write-Host "  $($s.character) A$($s.ascension) seed=$($s.seed)"
}

if (-not (Test-Path $LogPath)) {
    Write-Host ""
    Write-Host "No log file — run manual seeds with AutoPlay, then re-run with -LogPath."
    exit 0
}

$lines = Get-Content $LogPath -Tail 5000
$lastFloor = ($lines | Select-String -Pattern '\[AiHost\]|totalFloor|Floor' | Select-Object -Last 1).Line
$won = $lines | Select-String -Pattern 'Victory|GameOver.*Win|RUN_WON' -SimpleMatch:$false | Select-Object -Last 1
$lost = $lines | Select-String -Pattern 'Game Over|RUN_LOST|isDead' | Select-Object -Last 1

$outcome = "unknown"
if ($won) { $outcome = "win" }
elseif ($lost) { $outcome = "loss" }

$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$row = "$timestamp,manual,$outcome,$lastFloor"

if (-not (Test-Path $ResultsPath)) {
    "timestamp,seed,outcome,last_log_line" | Out-File $ResultsPath -Encoding utf8
}
Add-Content $ResultsPath $row

Write-Host ""
Write-Host "Parsed outcome: $outcome"
Write-Host "Appended to $ResultsPath"

if (Test-Path $ResultsPath) {
    $all = Import-Csv $ResultsPath
    $wins = ($all | Where-Object outcome -eq 'win').Count
    $total = $all.Count
    if ($total -gt 0) {
        $pct = [math]::Round(100 * $wins / $total, 1)
        Write-Host "Historical: $wins / $total wins ($pct%)"
    }
}
