"""Flatten full .run JSON into macro decision samples."""

from __future__ import annotations

import json
from pathlib import Path
from typing import Any, Iterable, Iterator


def card_id(card: dict[str, Any] | None) -> str | None:
    if not card:
        return None
    raw = card.get("id")
    return str(raw) if raw else None


def iter_macro_samples(run_hash: str, run: dict[str, Any]) -> Iterator[dict[str, Any]]:
    players = run.get("players") or []
    if not players:
        return

    player = players[0]
    character = _strip_prefix(player.get("character") or run.get("character"), "CHARACTER.")
    ascension = run.get("ascension")
    win = bool(run.get("win"))
    build_id = run.get("build_id")
    schema_version = run.get("schema_version")
    seed = run.get("seed")

    deck_ids: list[str] = []
    for c in player.get("deck") or []:
        cid = card_id(c) if isinstance(c, dict) else None
        if cid:
            deck_ids.append(cid)

    relic_ids: list[str] = []
    for r in player.get("relics") or []:
        if isinstance(r, dict) and r.get("id"):
            rid = _strip_prefix(r.get("id"), "RELIC.")
            if rid:
                relic_ids.append(rid)

    base = {
        "run_hash": run_hash,
        "seed": seed,
        "character": character,
        "ascension": ascension,
        "win": win,
        "build_id": build_id,
        "schema_version": schema_version,
        "final_deck_size": len(deck_ids),
        "final_relic_count": len(relic_ids),
    }

    history = run.get("map_point_history") or []
    floor_idx = 0
    for act_index, act_nodes in enumerate(history):
        if not isinstance(act_nodes, list):
            continue
        for node in act_nodes:
            if not isinstance(node, dict):
                continue
            floor_idx += 1
            map_type = node.get("map_point_type")
            rooms = node.get("rooms") or []
            room = rooms[0] if rooms else {}
            room_type = room.get("room_type")
            encounter_id = room.get("model_id")

            for ps in node.get("player_stats") or []:
                if not isinstance(ps, dict):
                    continue
                ctx = {
                    **base,
                    "act_index": act_index,
                    "floor_idx": floor_idx,
                    "map_point_type": map_type,
                    "room_type": room_type,
                    "encounter_id": encounter_id,
                    "current_hp": ps.get("current_hp"),
                    "max_hp": ps.get("max_hp"),
                    "current_gold": ps.get("current_gold"),
                    "deck_ids": list(deck_ids),
                    "relic_ids": list(relic_ids),
                }

                card_choices = ps.get("card_choices") or []
                if card_choices:
                    offers = [card_id(c.get("card")) for c in card_choices]
                    picked = [card_id(c.get("card")) for c in card_choices if c.get("was_picked")]
                    yield {
                        **ctx,
                        "sample_type": "card_choice",
                        "offers": [o for o in offers if o],
                        "picked": picked,
                        "skipped": [o for o in offers if o and o not in picked],
                        "context": _choice_context(map_type, room_type),
                    }

                relic_choices = ps.get("relic_choices") or []
                if relic_choices:
                    offers = [_strip_prefix(c.get("choice"), "RELIC.") for c in relic_choices]
                    picked = [
                        _strip_prefix(c.get("choice"), "RELIC.")
                        for c in relic_choices
                        if c.get("was_picked")
                    ]
                    yield {
                        **ctx,
                        "sample_type": "relic_choice",
                        "offers": [o for o in offers if o],
                        "picked": [p for p in picked if p],
                        "context": _choice_context(map_type, room_type),
                    }

                rest_choices = ps.get("rest_site_choices") or []
                if rest_choices:
                    yield {
                        **ctx,
                        "sample_type": "rest_site",
                        "choices": list(rest_choices),
                        "upgraded_cards": [
                            card_id(c) if isinstance(c, dict) else _strip_prefix(c, "CARD.")
                            for c in (ps.get("upgraded_cards") or [])
                        ],
                    }

                removed = ps.get("cards_removed") or []
                if removed:
                    yield {
                        **ctx,
                        "sample_type": "shop_remove",
                        "removed_cards": [card_id(c) for c in removed if card_id(c)],
                        "gold_spent": ps.get("gold_spent"),
                    }

                bought_relics = ps.get("bought_relics") or []
                bought_potions = ps.get("bought_potions") or []
                if bought_relics or bought_potions:
                    yield {
                        **ctx,
                        "sample_type": "shop_buy",
                        "bought_relics": [_strip_prefix(x, "RELIC.") for x in bought_relics],
                        "bought_potions": [_strip_prefix(x, "POTION.") for x in bought_potions],
                        "gold_spent": ps.get("gold_spent"),
                    }

                event_choices = ps.get("event_choices") or []
                if event_choices:
                    offers, picked = _parse_event_choices(event_choices)
                    yield {
                        **ctx,
                        "sample_type": "event",
                        "event_id": encounter_id,
                        "event_choices": event_choices,
                        "offers": offers,
                        "picked": picked,
                    }

                _apply_post_node_deck_updates(ps, deck_ids, relic_ids)


def collect_macro_samples(
    runs: Iterable[tuple[str, dict[str, Any]]],
    *,
    include_deck_snapshot: bool = False,
) -> list[dict[str, Any]]:
    rows: list[dict[str, Any]] = []
    for run_hash, run in runs:
        for sample in iter_macro_samples(run_hash, run):
            if not include_deck_snapshot:
                sample.pop("deck_ids", None)
                sample.pop("relic_ids", None)
            rows.append(sample)
    return rows


def export_macro_jsonl(
    runs: Iterable[tuple[str, dict[str, Any]]],
    output_path: Path,
    *,
    include_deck_snapshot: bool = False,
) -> int:
    count = 0
    with output_path.open("w", encoding="utf-8") as out:
        for run_hash, run in runs:
            for sample in iter_macro_samples(run_hash, run):
                if not include_deck_snapshot:
                    sample.pop("deck_ids", None)
                    sample.pop("relic_ids", None)
                out.write(json.dumps(sample, ensure_ascii=False) + "\n")
                count += 1
    return count


def export_macro_parquet(
    runs: Iterable[tuple[str, dict[str, Any]]],
    output_path: Path,
    *,
    include_deck_snapshot: bool = False,
) -> int:
    try:
        import pandas as pd
    except ImportError as exc:
        raise RuntimeError("Parquet export requires train deps: uv sync --group train") from exc

    rows = collect_macro_samples(runs, include_deck_snapshot=include_deck_snapshot)
    if not rows:
        output_path.parent.mkdir(parents=True, exist_ok=True)
        pd.DataFrame([]).to_parquet(output_path, index=False)
        return 0

    frame = pd.json_normalize(rows, sep="_")
    list_columns = [c for c in frame.columns if frame[c].map(lambda v: isinstance(v, list)).any()]
    for column in list_columns:
        frame[column] = frame[column].map(lambda v: json.dumps(v, ensure_ascii=False) if isinstance(v, list) else v)

    output_path.parent.mkdir(parents=True, exist_ok=True)
    frame.to_parquet(output_path, index=False)
    return len(frame)


def _choice_context(map_type: Any, room_type: Any) -> str:
    if room_type == "shop" or map_type == "shop":
        return "shop"
    if room_type == "monster" or map_type in {"monster", "elite", "boss"}:
        return "combat_reward"
    return str(room_type or map_type or "unknown")


def _parse_event_choices(event_choices: list[Any]) -> tuple[list[str], list[str]]:
    if not event_choices:
        return [], []

    if len(event_choices) == 1:
        picked = _event_option_key(event_choices[0])
        if not picked:
            return [], []
        return [picked], [picked]

    offer_keys = [_event_option_key(c) for c in event_choices]
    offer_keys = [k for k in offer_keys if k]
    picked = _event_option_key(event_choices[-1])
    return offer_keys, ([picked] if picked else [])


def _event_option_key(choice: Any) -> str | None:
    if isinstance(choice, str):
        return _normalize_event_option(choice)
    if not isinstance(choice, dict):
        return None

    raw_choice = choice.get("choice")
    if raw_choice:
        return _normalize_event_option(str(raw_choice))

    title = choice.get("title")
    if isinstance(title, dict):
        key = str(title.get("key") or "")
        table = str(title.get("table") or "")
        if key.endswith(".title"):
            key = key[: -len(".title")]
        parts = [p for p in key.split(".") if p]
        if not parts:
            return None
        if table == "relics":
            return _normalize_event_option(parts[0])
        if "options" in parts:
            idx = parts.index("options")
            if idx + 1 < len(parts):
                return _normalize_event_option(parts[idx + 1])
        return _normalize_event_option(parts[-1])
    return None


def _normalize_event_option(raw: str | None) -> str | None:
    if not raw:
        return None
    text = str(raw).strip().upper()
    for prefix in ("EVENT.OPTION.", "EVENT.", "NEOW.OPTION.", "NEOW.", "OPTION."):
        if text.startswith(prefix):
            text = text[len(prefix) :]
    if text.endswith(".TITLE"):
        text = text[: -len(".TITLE")]
    return text or None


def _strip_prefix(value: Any, prefix: str) -> str | None:
    if value is None:
        return None
    text = str(value)
    if text.startswith(prefix):
        return text[len(prefix) :]
    return text


def _apply_post_node_deck_updates(
    ps: dict[str, Any],
    deck_ids: list[str],
    relic_ids: list[str],
) -> None:
    for gained in ps.get("cards_gained") or []:
        cid = card_id(gained) if isinstance(gained, dict) else _strip_prefix(gained, "CARD.")
        if cid:
            deck_ids.append(cid)

    for removed in ps.get("cards_removed") or []:
        cid = card_id(removed)
        if cid and cid in deck_ids:
            deck_ids.remove(cid)

    for relic in ps.get("bought_relics") or []:
        rid = _strip_prefix(relic, "RELIC.")
        if rid:
            relic_ids.append(rid)
