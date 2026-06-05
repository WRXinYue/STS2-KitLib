# AI A10 regression bench

Fixed-seed solo runs for **StrongStrategy** win-rate tracking. Reads decision logs written by DevMode AutoPlay (`AiDecisionLog` / game log).

## Prerequisites

- DevMode built and deployed (`make deploy` or sync to game mods)
- Solo run with **AI Host → AutoPlay** enabled
- `AutoPlayStrategy` = `Strong` (default in settings)

## Usage

```powershell
# From repo root — prints seed checklist and parses latest godot log for outcomes
powershell -File tools/ai-bench/run-bench.ps1

# Optional: path to godot.log
powershell -File tools/ai-bench/run-bench.ps1 -LogPath "C:\path\to\godot.log"
```

## Manual regression loop

1. Start a solo run with a seed from `seeds.json` (character + ascension 10).
2. Enable AutoPlay in DevMode AI Host panel.
3. Let the run finish (win or loss).
4. Re-run `run-bench.ps1` — it appends results to `tools/ai-bench/results.csv`.

## Output columns

| Column | Meaning |
|--------|---------|
| `seed` | Run seed id |
| `character` | Character model id |
| `ascension` | Ascension level |
| `outcome` | `win` / `loss` / `unknown` |
| `floor` | Last floor reached |
| `cause` | Death room or `victory` |

Target: **≥40–50%** win rate per character at A10 after tuning.

## Scenario regression

`scenarios.json` lists key summon/minion encounters (Obscura, Ovicopter, Fabricator, etc.). After policy changes, spot-check combat logs for:

- `flags=HasIllusionRevive` targets should show `bias=-60`
- Summoner fights should show `bias=35` on primary

Tune non-damage weights in `EnemyThreatWeights.cs` and re-run A10 bench.
