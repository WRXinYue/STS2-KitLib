from __future__ import annotations

import json
from collections import defaultdict
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

import pandas as pd

from codex_train.util import (
    bayesian_rate,
    deck_size_band,
    extract_event_pick,
    hp_ratio_band,
    normalize_card_id,
    normalize_character,
    normalize_event_id,
    normalize_event_option,
    normalize_relic_id,
    parse_json_list,
    rate_to_bonus,
    rate_to_score,
)


def train_all(df: pd.DataFrame, scores_dir: Path | None = None) -> dict[str, Any]:
    cards = train_card_priors(df)
    skip = train_skip_priors(df)
    rest = train_rest_priors(df)
    remove = train_remove_priors(df)
    relics = train_relic_priors(df)
    events = train_event_priors(df)
    codex_scores = load_codex_card_scores(scores_dir) if scores_dir else {}

    merge_codex_scores(cards, codex_scores)

    return {
        "version": 1,
        "trained_at": datetime.now(timezone.utc).replace(microsecond=0).isoformat(),
        "source_rows": int(len(df)),
        "cards": cards,
        "skip": skip,
        "rest": rest,
        "remove": remove,
        "relics": relics,
        "events": events,
    }


def train_card_priors(df: pd.DataFrame) -> dict[str, Any]:
    subset = df[df["sample_type"] == "card_choice"]
    counts: dict[tuple[str, str, str], list[int]] = defaultdict(lambda: [0, 0])

    global_picks = 0
    global_offers = 0

    for row in subset.itertuples(index=False):
        character = normalize_character(getattr(row, "character", None))
        context = str(getattr(row, "context", "") or "unknown")
        offers = [normalize_card_id(x) for x in parse_json_list(getattr(row, "offers", None))]
        picked = {normalize_card_id(x) for x in parse_json_list(getattr(row, "picked", None))}
        offers = [o for o in offers if o]
        if not character or not offers:
            continue
        for offer in offers:
            global_offers += 1
            key = (offer, character, context)
            counts[key][1] += 1
            if offer in picked:
                counts[key][0] += 1
                global_picks += 1

    global_rate = global_picks / global_offers if global_offers else 0.33
    out: dict[str, Any] = {}
    char_context_rates: dict[tuple[str, str], list[float]] = defaultdict(list)

    for (card_id, character, context), (wins, n) in counts.items():
        rate = bayesian_rate(wins, n, global_rate)
        char_context_rates[(character, context)].append(rate)
        entry = out.setdefault(card_id, {}).setdefault(character, {})
        entry[context] = {
            "pick_rate": round(rate, 4),
            "score": rate_to_score(rate),
            "bonus": rate_to_bonus(rate, global_rate),
            "n": n,
        }

    out["_meta"] = {
        "global_pick_rate": round(global_rate, 4),
        "keys": len(counts),
    }
    return out


def train_skip_priors(df: pd.DataFrame) -> dict[str, Any]:
    subset = df[df["sample_type"] == "card_choice"]
    buckets: dict[str, list[int]] = defaultdict(lambda: [0, 0])

    for row in subset.itertuples(index=False):
        character = normalize_character(getattr(row, "character", None))
        if not character:
            continue
        picked = parse_json_list(getattr(row, "picked", None))
        skipped = len(picked) == 0
        act = int(getattr(row, "act_index", 0) or 0)
        deck_band = deck_size_band(getattr(row, "final_deck_size", None))
        hp_band = hp_ratio_band(getattr(row, "current_hp", None), getattr(row, "max_hp", None))
        key = f"{character}|act{act}|deck_{deck_band}|hp_{hp_band}"
        buckets[key][1] += 1
        if skipped:
            buckets[key][0] += 1

    out: dict[str, Any] = {}
    for key, (skips, n) in buckets.items():
        if n < 30:
            continue
        rate = skips / n
        out[key] = {
            "skip_rate": round(rate, 4),
            "threshold_offset": max(-8, min(12, int(round((rate - 0.35) * 20)))),
            "n": n,
        }
    out["_meta"] = {"buckets": len(out)}
    return out


def train_rest_priors(df: pd.DataFrame) -> dict[str, Any]:
    subset = df[df["sample_type"] == "rest_site"]
    buckets: dict[str, dict[str, int]] = defaultdict(lambda: defaultdict(int))

    for row in subset.itertuples(index=False):
        character = normalize_character(getattr(row, "character", None))
        if not character:
            continue
        act = int(getattr(row, "act_index", 0) or 0)
        hp_band = hp_ratio_band(getattr(row, "current_hp", None), getattr(row, "max_hp", None))
        choices = [str(c).upper() for c in parse_json_list(getattr(row, "choices", None))]
        if not choices:
            continue
        choice = choices[0]
        key = f"{character}|act{act}|hp_{hp_band}"
        buckets[key][choice] += 1
        buckets[key]["_total"] += 1

    out: dict[str, Any] = {}
    for key, counts in buckets.items():
        total = counts.pop("_total", 0)
        if total < 20:
            continue
        dist = {k: round(v / total, 4) for k, v in counts.items()}
        best = max(dist, key=dist.get)
        out[key] = {"choices": dist, "preferred": best, "n": total}
    out["_meta"] = {"buckets": len(out) - 1 if "_meta" in out else len(out)}
    return out


def train_remove_priors(df: pd.DataFrame) -> dict[str, Any]:
    subset = df[df["sample_type"] == "shop_remove"]
    counts: dict[tuple[str, str], int] = defaultdict(int)

    for row in subset.itertuples(index=False):
        character = normalize_character(getattr(row, "character", None))
        if not character:
            continue
        for card in parse_json_list(getattr(row, "removed_cards", None)):
            card_id = normalize_card_id(card if isinstance(card, str) else str(card))
            if card_id:
                counts[(card_id, character)] += 1

    if not counts:
        return {"_meta": {"keys": 0}}

    max_count = max(counts.values())
    out: dict[str, Any] = {}
    for (card_id, character), n in counts.items():
        bonus = max(0, min(15, int(round(15 * n / max_count))))
        out.setdefault(card_id, {})[character] = {"bonus": bonus, "n": n}
    out["_meta"] = {"keys": len(counts)}
    return out


def train_relic_priors(df: pd.DataFrame) -> dict[str, Any]:
    subset = df[df["sample_type"] == "relic_choice"]
    counts: dict[tuple[str, str, str], list[int]] = defaultdict(lambda: [0, 0])
    global_picks = 0
    global_offers = 0

    for row in subset.itertuples(index=False):
        character = normalize_character(getattr(row, "character", None))
        context = str(getattr(row, "context", "") or "unknown")
        offers = [normalize_relic_id(x) for x in parse_json_list(getattr(row, "offers", None))]
        picked = {normalize_relic_id(x) for x in parse_json_list(getattr(row, "picked", None))}
        offers = [o for o in offers if o]
        if not character or not offers:
            continue
        for offer in offers:
            global_offers += 1
            key = (offer, character, context)
            counts[key][1] += 1
            if offer in picked:
                counts[key][0] += 1
                global_picks += 1

    global_rate = global_picks / global_offers if global_offers else 0.33
    out: dict[str, Any] = {}
    for (relic_id, character, context), (wins, n) in counts.items():
        if n < 10:
            continue
        rate = bayesian_rate(wins, n, global_rate)
        entry = out.setdefault(relic_id, {}).setdefault(character, {})
        entry[context] = {
            "pick_rate": round(rate, 4),
            "bonus": rate_to_bonus(rate, global_rate),
            "n": n,
        }
    out["_meta"] = {"global_pick_rate": round(global_rate, 4), "keys": len(counts)}
    return out


def train_event_priors(df: pd.DataFrame) -> dict[str, Any]:
    subset = df[df["sample_type"] == "event"]
    option_picks: dict[tuple[str, str, str], int] = defaultdict(int)
    event_totals: dict[tuple[str, str], int] = defaultdict(int)
    global_picks = 0

    for row in subset.itertuples(index=False):
        character = normalize_character(getattr(row, "character", None))
        if not character:
            continue

        event_id = normalize_event_id(getattr(row, "event_id", None) or getattr(row, "encounter_id", None))
        if not event_id:
            continue

        picked_list = parse_json_list(getattr(row, "picked", None))
        if picked_list:
            picked = normalize_event_option(str(picked_list[0]))
        else:
            event_choices = parse_json_list(getattr(row, "event_choices", None))
            picked = extract_event_pick(event_choices)

        if not picked:
            continue

        option_picks[(event_id, picked, character)] += 1
        event_totals[(event_id, character)] += 1
        global_picks += 1

    if global_picks == 0:
        return {"_meta": {"keys": 0, "global_pick_rate": 0.0}}

    global_rate = 1.0 / max(1, len(option_picks))
    out: dict[str, Any] = {}
    keys_written = 0

    for (event_id, option_key, character), wins in option_picks.items():
        total = event_totals.get((event_id, character), wins)
        if wins < 10:
            continue
        rate = bayesian_rate(wins, total, global_rate, prior_n=30.0)
        entry = out.setdefault(event_id, {}).setdefault(option_key, {}).setdefault(character, {})
        entry["pick_rate"] = round(rate, 4)
        entry["bonus"] = rate_to_bonus(rate, global_rate)
        entry["n"] = wins
        keys_written += 1

        if event_id == "NEOW":
            alias = out.setdefault("EVENT.NEOW", {}).setdefault(option_key, {})
            alias[character] = dict(entry)

    out["_meta"] = {
        "global_pick_rate": round(global_rate, 4),
        "keys": keys_written,
        "samples": int(global_picks),
    }
    return out


def load_codex_card_scores(scores_dir: Path) -> dict[str, dict[str, Any]]:
    path = scores_dir / "cards.json"
    if not path.is_file():
        return {}
    raw = json.loads(path.read_text(encoding="utf-8"))
    if not isinstance(raw, dict):
        return {}
    return raw


def merge_codex_scores(cards: dict[str, Any], codex_scores: dict[str, dict[str, Any]]) -> None:
    for card_key, payload in codex_scores.items():
        norm = normalize_card_id(card_key) or card_key.upper()
        if norm in cards and norm != "_meta":
            continue
        score = payload.get("score")
        if score is None:
            continue
        bonus = max(-15, min(15, int(round((float(score) - 50) * 0.25))))
        cards.setdefault(norm, {})["_codex"] = {
            "score": int(score),
            "bonus": bonus,
            "win_rate": payload.get("win_rate"),
            "n": payload.get("picks"),
        }


def write_priors(payload: dict[str, Any], output_path: Path) -> None:
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
