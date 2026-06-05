# Spire Codex crawl

Download public run data from [Spire Codex](https://spire-codex.com/runs) into a local store for offline macro-AI training. Uses the public REST API ([Developer docs](https://spire-codex.com/developers)); no auth required. Rate limit: **60 req/min** (client defaults to 55).

Managed with **[uv](https://docs.astral.sh/uv/)** — config in `uv.toml`, dependencies in `pyproject.toml`.

## Setup

```powershell
cd tools/codex-crawl
uv sync                  # core: httpx + rich
uv sync --group train    # optional: pandas + pyarrow for Parquet export
```

## Quick start

```powershell
cd tools/codex-crawl

# Index + download A10 Defect wins (small test)
uv run codex-crawl sync `
  --character DEFECT --ascension 10 --win `
  --max-list-pages 2 --max-runs 20 --fetch-scores

uv run codex-crawl status

# JSONL for Python training scripts
uv run codex-crawl export-macro `
  --character DEFECT --ascension 10 --win `
  --output data/defect_a10_wins.jsonl

# Parquet for pandas / sklearn / pytorch (requires train group)
uv sync --group train
uv run codex-crawl export-parquet `
  --character DEFECT --ascension 10 --win `
  --output data/defect_a10_wins.parquet
```

Equivalent module invocation:

```powershell
uv run python -m codex_crawl status
```

## Output layout

| Path | Contents |
| --- | --- |
| `data/codex.db` | Run metadata + crawl progress (SQLite) |
| `data/runs/{hash}.json` | Full `.run` replay per hash |
| `data/scores/*.json` | Codex card/relic/potion win-rate scores |
| `data/macro_samples.jsonl` | Flattened decision samples |
| `data/macro_samples.parquet` | Same samples, columnar |

All under `data/` are gitignored.

## Commands

| Command | Purpose |
| --- | --- |
| `status` | Row counts in local DB |
| `fetch-scores` | Download `/api/runs/scores/{cards,relics,potions}` |
| `crawl-list` | Paginate `/api/runs/list` into SQLite (resumable) |
| `crawl-full` | Fetch `/api/runs/shared/{hash}` for pending runs |
| `sync` | `crawl-list` then `crawl-full` |
| `export-macro` | JSONL training rows |
| `export-parquet` | Parquet export (`uv sync --group train`) |

## Recommended filters

Macro samples with **choices + was_picked** need **schema ≥ 9** (build **≥ v0.101.0**). Defaults:

- `--min-schema 9`
- `--min-build-id v0.101.0`

Full A10 win corpus:

```powershell
uv run codex-crawl sync --ascension 10 --win --fetch-scores
```

## JSONL sample types

| `sample_type` | Fields |
| --- | --- |
| `card_choice` | `offers`, `picked`, `skipped`, `context` |
| `relic_choice` | shop or combat relic picks |
| `rest_site` | `choices` (`HEAL` / `SMITH` / `LIFT`) |
| `shop_remove` | `removed_cards` |
| `shop_buy` | `bought_relics`, `bought_potions` |
| `event` | `event_id`, `event_choices`, `offers`, `picked` |

Use `--include-deck` for deck/relic snapshots at each decision point.

## Resume & scale

- List crawl resumes from last page per filter key.
- Full crawl skips hashes that already have `data/runs/{hash}.json`.
- Spire Codex indexes **100k+** runs; filter by character/ascension/win first.

## Training notes

- Upload-biased sample (players who submit runs).
- No combat turn logs — macro decisions only.
- Filter `build_id` after balance patches.
- Keep `--rpm` ≤ 60 per [API docs](https://spire-codex.com/developers).
