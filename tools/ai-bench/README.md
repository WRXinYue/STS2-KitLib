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

### Beam block / ConsiderLeaf (`block_with_energy_incoming`)

After `CombatPlanner` changes, spot-check any turn with **incoming attack + affordable block still in hand**:

| Signal | Pass | Fail |
| --- | --- | --- |
| Decision tag | `beam d=1` (or deeper) playing Defend/skill | `beam end` while `E>0` and block was playable |
| Threat | `IN=` > 0, net damage not already blocked | Ignores block when `NeedsBlock` or `ShouldScoreBlock` would apply |
| Sim outlook | `POST_BLK=` rises on block lines vs naked end | Attack-only or end-turn with block left unplayed |
| Lethal shortcut | Only `beam d=` / `beam end` tags | Any `[lethal]`, `[aoe-lethal]`, or `[lethal-transform]` tag |

Any Act 1 fight works (e.g. Leaf Slime, Jaw Worm); no relic required. Enable verbose combat log in DevMode settings.

### No lethal fast-path (`no_lethal_fast_path`)

All combat polls go through beam search. After deploy, grep `godot.log` — there should be **no** `[lethal]`, `[aoe-lethal]`, or `[lethal-transform]` decision tags. Kills should still appear as `[beam d=N s=…]` with kill cards in `LINE=`.

### Block full before attack (`block_full_before_attack`)

When `IN>=12` and hand can afford enough block to cover net incoming, expect:

- `LINE=` starting with `DEFEND` / block skill, or
- End-of-turn `POST_BLK>=IN` before attack-heavy sequences

Fail: `BLOOD_WALL>PRIMAL>STRIKE` with `POST_BLK=5` while `IN=25`.

### Block then kill next (`block_then_kill_next`)

When the AI cannot secure-kill this turn without taking damage, it should block first (`POST_BLK` covers `IN` or `net=0`), then use high `POST_PLAY` on the following turn. HP should stay healthier than repeated `[lethal]` chip-attack lines.

**Verbose log fields** (one line per pick):

| Field | Meaning |
| --- | --- |
| `[beam d=N s=…]` / `[beam end s=…]` | Planner beam depth + leaf score (actual pick reason) |
| `scorer=…` | Same-step CombatScorer score for the picked action (may differ from beam) |
| `scorer-alts:` | Top single-step alternatives from CombatScorer (not beam path) |
| `LINE=Card→e0>…` | Full beam path the planner evaluated (first action is what gets played) |
| `SETUP=` | Setup debt from `CombatSetupEvaluator` (vuln deferral pressure) |
| `VULN=` | Count of affordable vuln-applying cards in hand |
| `ICE=` | 1 if energy retained next turn (Ice Cream etc.) |
| `POST_PLAY=` / `POST_BLK=` | Expected damage/block after simulating end turn (only when pick is EndTurn) |

If beam picks Defend but `scorer-alts` ranks Strike higher, that is normal — beam optimizes multi-step leaf score, not single-step scorer.

**Vuln / multi-enemy** (`vuln_same_target_multi`): debuff skills must target an enemy in sim (`CombatTargetTypes.NeedsEnemyTarget`); `LINE=TAUNT→e0>Strike→e0` — same `eN` on setup and attacks. Mismatch or `(search)` on targeted debuffs indicates index or target-enumeration regression.

**Combat index**: `SecondaryIndex` / `→eN` = 0-based slot in `CombatState.Enemies` (same as snapshot). Not the UI log's 1-based index. After a kill, `e1` still means the second slot — executor resolves via `CombatTargetResolver`, not shortened `HittableEnemies` position.

**Potions**: logged only on use as `potion pick [ID:+score] card=<best card score>`. Emergency path is **unmodeled** heal/block only (e.g. FRUIT); simulatable BLOCK/BLOOD/FIRE/etc. compete in beam. Fallback non-sim potions need `score >= card + 8`, max one per turn. Beam and fallback share `PotionUseScoring` (secure-kill block defer, full-energy buff penalty, debuff defer, retain waste).

### Card reward macro EV

Pick/skip uses **pure marginal score** (`best > 0`); no `MinPickScore` or skip-threshold gates.

| Log pattern | Meaning |
|-------------|---------|
| `card pick [NAME:+score] marginal=N nextFight=F vs=ENCOUNTER` | Picked offer; `nextFight` is sim delta vs baseline deck |
| `card skip best=B marginal=M nextFight=F` | All offers scored ≤ 0 |
| Snapshot `nextFightPreview[]` | Weighted route combats (up to 3) with `incomingTurn1` |

Scoring stack per offer: `MarginalPickScore` + synergy + dilution + codex + **NextFightDeckEvaluator** (8 shuffle MC samples × shallow beam depth 3 per weighted combat node).

Manual checks (`scenarios.json`):

1. **Weak dilute offer** — TRUE_GRIT / duplicate strike skipped when total ≤ 0.
2. **Block before elite** — low block deck favors block offer when `nextFightPreview` shows Elite with high `incomingTurn1`.
3. **Offense before killable fight** — thin deck favors damage when beam lethal on stub encounter.


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

### Phase D relic-aware sim

Regenerate data after STS2 relic changes:

```powershell
python tools/relic-combat-effect-dump/extract-relic-combat-effects.py
```

Spot-checks (`scenarios.json`):

1. **Unceasing Top** — empty hand mid-turn triggers extra draw in `CombatSimulator`.
2. **Runic Pyramid** — `EndTurn` retains hand (no flush to discard).
3. **Snecko Eye** — +2 draw per turn + Confused merged from `relic-combat-effects.json` when not already in `playerPowers`.

### Beam potion sim (`potion_beam_timing`)

Deterministic potions (FLEX, WEAK, FIRE, BLOCK, BLOOD, ENERGY, SWIFT) and random pick-one potions (COLORLESS/ATTACK/SKILL/POWER) are expanded in beam via `potion-combat-effects.json`. Emergency heal/block uses `PotionScorer.TryEmergencyPotion` only when the potion is **not** simulatable in JSON.

Verbose log checks:

1. **`LINE=`** — simulatable potions appear in beam path (e.g. `Strike>FLEX>Strike`, `WEAK→e0>Strike→e0`), not only standalone `potion pick`.
2. **`Potion#`** — planner pick uses slot index; `→eN` when enemy-targeted.
3. **`COLORLESS#2`** — random potion MC branch in beam path (branch not sent to executor; replan next poll).
4. **No early waste** — FLEX at full energy with no incoming should lose to `Strike>…` lines when damage matters.
