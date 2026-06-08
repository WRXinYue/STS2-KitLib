# Codex train

Train static AI priors from [`tools/codex-crawl`](../codex-crawl/) macro exports.

```powershell
cd tools/codex-crawl
uv run codex-crawl export-parquet --ascension 10 --min-schema 9

cd ../codex-train
uv sync
uv run codex-train --parquet ../codex-crawl/data/macro_samples.parquet analyze
uv run codex-train --parquet ../codex-crawl/data/macro_samples.parquet train
uv run codex-train --parquet ../codex-crawl/data/macro_samples.parquet eval
```

`train` writes `codex-priors.json` and syncs to `src/AI/Data/codex-priors.json` (embedded in KitLib.dll).

## Prior tables

| JSON key | `sample_type` | Runtime API |
| --- | --- | --- |
| `cards` | `card_choice` | `GetCardBonus` |
| `relics` | `relic_choice` | `GetRelicBonus` (context: `event`, `combat_reward`, `shop`) |
| `events` | `event` | `GetEventOptionBonus` (Neow + generic events) |
| `rest` | `rest_site` | `GetPreferredRestChoice` |
| `skip` | `card_choice` | `GetSkipThresholdOffset` |
| `remove` | `shop_remove` | `GetRemoveBonus` |

Event rows use `event_choices` title keys (e.g. `LEAD_PAPERWEIGHT.title` → option `LEAD_PAPERWEIGHT`). Export also adds normalized `offers` / `picked` columns.
