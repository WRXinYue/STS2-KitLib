# Overnight Spire Codex crawl: A10 wins first, then all A10 runs, then export.
$ErrorActionPreference = "Continue"
Set-Location $PSScriptRoot

$env:PYTHONUTF8 = "1"
$env:PYTHONIOENCODING = "utf-8"
if ($Host.UI.RawUI) { try { chcp 65001 | Out-Null } catch {} }

$log = Join-Path $PSScriptRoot "data\crawl.log"
$data = Join-Path $PSScriptRoot "data"
New-Item -ItemType Directory -Force -Path $data | Out-Null

function Write-Log([string]$Message) {
    $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Message"
    Add-Content -Path $log -Value $line -Encoding utf8
    Write-Host $line
}

Write-Log "=== Overnight crawl started ==="
Write-Log "Phase 1: A10 wins (~6844 runs, ~2h @ 55 rpm)"
uv run codex-crawl sync --ascension 10 --win --fetch-scores 2>&1 | ForEach-Object { Write-Log $_ }

Write-Log "Phase 2: all A10 runs (~36686 total, continues until done or interrupted)"
uv run codex-crawl sync --ascension 10 2>&1 | ForEach-Object { Write-Log $_ }

Write-Log "Phase 3: export slim Parquet (recommended for training)"
uv run codex-crawl export-parquet --ascension 10 --min-schema 9 `
    --output (Join-Path $data "a10_macro_slim.parquet") 2>&1 | ForEach-Object { Write-Log $_ }

Write-Log "=== Final status ==="
uv run codex-crawl status 2>&1 | ForEach-Object { Write-Log $_ }
Write-Log "=== Overnight crawl finished ==="
