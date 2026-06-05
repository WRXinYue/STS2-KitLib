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

### Deck rhythm / shuffle (`deck_rhythm_shuffle`)

After L4 pile simulation, combat logs should include:

- `NEXT=` — top cards in draw pile (peek)
- `RESHUF=1` — next hand draw would reshuffle discard
- `POST_PLAY=` / `POST_BLK=` — expected damage/block after simulated EndTurn

Spot-check Leaf Slime: AI should rush before discard reshuffle mixes in Slimed; `POLL=` should correlate with bad `POST_PLAY=` on early EndTurn lines.

### Sim limitation fixes (Phase A + B)

Manual spot-checks:

1. **Lethal solver** — three 1-cost 6-dmg strikes + 3 energy → `MaxSingleTargetDamage == 18`.
2. **Shuffle fallback** — empty draw + discard, `shuffleSeed=0` → stable `StableShuffle`; debug log once.
3. **Enemy act order** — snapshot list order (`ActOrder`), not combat `index`.
4. **Random inject** — Noisebot / Soul Fysh / Insatiable moves use `rngShuffle` counter for `DrawRandom` / `DiscardRandom` placement.
5. **Headbutt pick** — discard-pile single-select uses highest `KeepScore` (live + sim); exhaust/discard still lowest.
6. **Power registry** — snapshot `playerPowers` maps Weak/Frail/Shrink/Smog/Tangle/Bind into `CombatCardCost` / `ThreatEconomy`.

### Phase C sim fidelity

1. **Strength/Dexterity** — `playerPowers` Strength +3 with 6-dmg Strike → `CombatDamageCalc` outgoing 9.
2. **Ordered move effects** — ThievingHopper steal before attack; TheObscura `AllyStrength` before ally hits.
3. **Steal** — `THIEVING_HOPPER` removes best card from draw/discard pile in sim.
4. **RNG streams** — snapshot includes `rngEnergyCosts`; missing `rngShuffle` logs Warn.
5. **Confused EV** — draw-pile planning uses cost EV 1.5 when Confused active.
