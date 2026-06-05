# Resume full JSON fetch + slim export (list index already complete).
$ErrorActionPreference = "Continue"
Set-Location $PSScriptRoot

$env:PYTHONUTF8 = "1"
$env:PYTHONIOENCODING = "utf-8"
if ($Host.UI.RawUI) { try { chcp 65001 | Out-Null } catch {} }

$log = Join-Path $PSScriptRoot "data\crawl.log"
$data = Join-Path $PSScriptRoot "data"

function Write-Log([string]$Message) {
    $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] $Message"
    Add-Content -Path $log -Value $line -Encoding utf8
    Write-Host $line
}

Write-Log "=== Resume crawl-full (remaining pending runs) ==="
uv run codex-crawl crawl-full --max-runs 0 2>&1 | ForEach-Object { Write-Log $_ }

Write-Log "=== Export slim parquet (no deck snapshot) ==="
uv run codex-crawl export-parquet --ascension 10 --min-schema 9 `
    --output (Join-Path $data "a10_macro_slim.parquet") 2>&1 | ForEach-Object { Write-Log $_ }

Write-Log "=== Final status ==="
uv run codex-crawl status 2>&1 | ForEach-Object { Write-Log $_ }
Write-Log "=== Resume finished ==="
