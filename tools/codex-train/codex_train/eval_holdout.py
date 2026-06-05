from __future__ import annotations

import json
from pathlib import Path
from typing import Any

import pandas as pd

from codex_train.util import (
    extract_event_pick,
    normalize_character,
    normalize_event_id,
    normalize_event_option,
    parse_json_list,
)


def evaluate_card_choice(df: pd.DataFrame, train_hashes: set[str]) -> dict[str, Any]:
    test = df[(df["sample_type"] == "card_choice") & (df["run_hash"].isin(train_hashes) == False)]  # noqa: E712
    if test.empty:
        return {"top1_accuracy": None, "n": 0}

    correct = 0
    total = 0
    for row in test.itertuples(index=False):
        offers = [normalize_card_id(x) for x in parse_json_list(getattr(row, "offers", None))]
        picked = [normalize_card_id(x) for x in parse_json_list(getattr(row, "picked", None))]
        offers = [o for o in offers if o]
        picked = [p for p in picked if p]
        if not offers or not picked:
            continue
        total += 1
        if picked[0] in offers:
            correct += 1

    return {
        "top1_accuracy": round(correct / total, 4) if total else None,
        "n": total,
    }


def evaluate_with_priors(df: pd.DataFrame, priors: dict[str, Any], train_hashes: set[str]) -> dict[str, Any]:
    test = df[(df["sample_type"] == "card_choice") & (~df["run_hash"].isin(train_hashes))]
    card_priors = priors.get("cards", {})
    global_rate = card_priors.get("_meta", {}).get("global_pick_rate", 0.33)

    correct = 0
    total = 0
    for row in test.itertuples(index=False):
        character = normalize_character(getattr(row, "character", None))
        context = str(getattr(row, "context", "") or "unknown")
        offers = [normalize_card_id(x) for x in parse_json_list(getattr(row, "offers", None))]
        picked = [normalize_card_id(x) for x in parse_json_list(getattr(row, "picked", None))]
        offers = [o for o in offers if o]
        picked = [p for p in picked if p]
        if not character or not offers or not picked:
            continue

        def score_card(card_id: str) -> float:
            char_map = card_priors.get(card_id, {})
            if isinstance(char_map, dict):
                ctx = char_map.get(character, {})
                if isinstance(ctx, dict) and context in ctx:
                    return float(ctx[context].get("pick_rate", global_rate))
                codex = char_map.get("_codex")
                if isinstance(codex, dict):
                    return float(codex.get("score", 50)) / 100.0
            return global_rate

        best = max(offers, key=score_card)
        total += 1
        if best == picked[0]:
            correct += 1

    return {
        "prior_top1_accuracy": round(correct / total, 4) if total else None,
        "n": total,
    }


def evaluate_rest(df: pd.DataFrame, priors: dict[str, Any], train_hashes: set[str]) -> dict[str, Any]:
    from codex_train.util import hp_ratio_band

    test = df[(df["sample_type"] == "rest_site") & (~df["run_hash"].isin(train_hashes))]
    rest_priors = priors.get("rest", {})
    correct = 0
    total = 0

    for row in test.itertuples(index=False):
        character = normalize_character(getattr(row, "character", None))
        if not character:
            continue
        act = int(getattr(row, "act_index", 0) or 0)
        hp_band = hp_ratio_band(getattr(row, "current_hp", None), getattr(row, "max_hp", None))
        choices = [str(c).upper() for c in parse_json_list(getattr(row, "choices", None))]
        if not choices:
            continue
        key = f"{character}|act{act}|hp_{hp_band}"
        bucket = rest_priors.get(key)
        if not isinstance(bucket, dict):
            continue
        preferred = bucket.get("preferred")
        if not preferred:
            continue
        total += 1
        if choices[0] == preferred:
            correct += 1

    return {
        "rest_top1_accuracy": round(correct / total, 4) if total else None,
        "n": total,
    }


def evaluate_event_choice(df: pd.DataFrame, priors: dict[str, Any], train_hashes: set[str]) -> dict[str, Any]:
    test = df[(df["sample_type"] == "event") & (~df["run_hash"].isin(train_hashes))]
    event_priors = priors.get("events", {})
    correct = 0
    total = 0

    for row in test.itertuples(index=False):
        character = normalize_character(getattr(row, "character", None))
        event_id = normalize_event_id(getattr(row, "event_id", None) or getattr(row, "encounter_id", None))
        if not character or not event_id:
            continue

        picked_list = parse_json_list(getattr(row, "picked", None))
        if picked_list:
            picked = normalize_event_option(str(picked_list[0]))
        else:
            picked = extract_event_pick(parse_json_list(getattr(row, "event_choices", None)))
        if not picked:
            continue

        event_node = event_priors.get(event_id) or event_priors.get("EVENT.NEOW" if event_id == "NEOW" else "")
        if not isinstance(event_node, dict):
            continue

        def score_option(option_key: str) -> float:
            char_node = event_node.get(option_key, {})
            if isinstance(char_node, dict) and character in char_node:
                return float(char_node[character].get("pick_rate", 0.0))
            return 0.0

        candidates = [
            normalize_event_option(str(x))
            for x in parse_json_list(getattr(row, "offers", None))
        ]
        candidates = [c for c in candidates if c]
        if not candidates:
            candidates = list(event_node.keys())
            candidates = [c for c in candidates if c != "_meta" and isinstance(event_node.get(c), dict)]

        if not candidates:
            continue

        best = max(candidates, key=score_option)
        total += 1
        if best == picked:
            correct += 1

    return {
        "event_top1_accuracy": round(correct / total, 4) if total else None,
        "n": total,
    }


def run_eval(df: pd.DataFrame, priors: dict[str, Any], holdout: float = 0.2) -> dict[str, Any]:
    hashes = sorted(df["run_hash"].dropna().unique())
    split = max(1, int(len(hashes) * (1 - holdout)))
    train_hashes = set(hashes[:split])
    test_hashes = set(hashes[split:])

    report = {
        "holdout_runs": len(test_hashes),
        "train_runs": len(train_hashes),
        "random_baseline": 0.333,
        "oracle_pick_rate": evaluate_card_choice(df, train_hashes),
        "prior_model": evaluate_with_priors(df, priors, train_hashes),
        "rest_model": evaluate_rest(df, priors, train_hashes),
        "event_model": evaluate_event_choice(df, priors, train_hashes),
    }
    return report


def write_report(report: dict[str, Any], path: Path) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
