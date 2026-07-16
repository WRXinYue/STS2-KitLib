# Dump MonsterMechanicIndex via in-game MCP bridge (game running with KitLib.Dev).
param(
    [string]$McpUrl = "http://127.0.0.1:9877/messages",
    [string]$OutPath = "",
    [string]$Prefix = ""
)

$body = @{
    jsonrpc = "2.0"
    id = 1
    method = "tools/call"
    params = @{
        name = "dev_dump_monster_mechanics"
        arguments = @{}
    }
} | ConvertTo-Json -Depth 6

if ($Prefix) {
    $bodyObj = $body | ConvertFrom-Json
    $bodyObj.params.arguments = @{ prefix = $Prefix }
    $body = $bodyObj | ConvertTo-Json -Depth 6
}

$response = Invoke-RestMethod -Uri $McpUrl -Method Post -Body $body -ContentType "application/json"
$payload = $response.result.content[0].text | ConvertFrom-Json

if (-not $OutPath) {
    $repo = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
    $OutPath = Join-Path $repo "tools\monster-probe-dump\monster-mechanics.json"
}

$payload | ConvertTo-Json -Depth 8 | Set-Content -Path $OutPath -Encoding UTF8
Write-Host "Wrote $($payload.count) monsters to $OutPath"
