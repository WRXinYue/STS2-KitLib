from __future__ import annotations

import json
from typing import Any


def parse_json_list(value: Any) -> list[Any]:
    if value is None:
        return []
    if isinstance(value, list):
        return value
    if isinstance(value, float) and value != value:
        return []
    text = str(value).strip()
    if not text or text.lower() == "nan":
        return []
    try:
        parsed = json.loads(text)
        return parsed if isinstance(parsed, list) else []
    except json.JSONDecodeError:
        return []


def normalize_card_id(card_id: str | None) -> str | None:
    if not card_id:
        return None
    text = str(card_id).strip().upper()
    if text.startswith("CARD."):
        text = text[5:]
    return text or None


def normalize_relic_id(relic_id: str | None) -> str | None:
    if not relic_id:
        return None
    text = str(relic_id).strip().upper()
    if text.startswith("RELIC."):
        text = text[6:]
    return text or None


def normalize_character(character: str | None) -> str | None:
    if not character:
        return None
    text = str(character).strip().upper()
    if text.startswith("CHARACTER."):
        text = text[10:]
    return text or None


def normalize_event_id(event_id: str | None) -> str | None:
    if not event_id:
        return None
    text = str(event_id).strip().upper()
    if text.startswith("EVENT."):
        text = text[6:]
    return text or None


def normalize_event_option(raw: str | None) -> str | None:
    if not raw:
        return None
    text = str(raw).strip().upper()
    for prefix in ("EVENT.OPTION.", "EVENT.", "NEOW.OPTION.", "NEOW.", "OPTION."):
        if text.startswith(prefix):
            text = text[len(prefix) :]
    if text.endswith(".TITLE"):
        text = text[: -len(".TITLE")]
    return text or None


def event_option_from_choice(choice: Any) -> str | None:
    if isinstance(choice, str):
        return normalize_event_option(choice)
    if not isinstance(choice, dict):
        return None

    if choice.get("choice"):
        return normalize_event_option(str(choice.get("choice")))

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
            return normalize_event_option(parts[0])
        if "options" in parts:
            idx = parts.index("options")
            if idx + 1 < len(parts):
                return normalize_event_option(parts[idx + 1])
        return normalize_event_option(parts[-1])
    return None


def extract_event_pick(event_choices: list[Any]) -> str | None:
    if not event_choices:
        return None
    if len(event_choices) == 1:
        return event_option_from_choice(event_choices[0])
    return event_option_from_choice(event_choices[-1])


def bayesian_rate(wins: float, n: int, global_rate: float, prior_n: float = 20.0) -> float:
    if n <= 0:
        return global_rate
    return (wins + prior_n * global_rate) / (n + prior_n)


def rate_to_score(rate: float) -> int:
    return max(0, min(100, int(round(rate * 100))))


def rate_to_bonus(rate: float, global_rate: float) -> int:
    delta = rate - global_rate
    return max(-15, min(15, int(round(delta * 40))))


def hp_ratio_band(hp: Any, max_hp: Any) -> str:
    try:
        hp_v = float(hp)
        max_v = float(max_hp)
    except (TypeError, ValueError):
        return "unknown"
    if max_v <= 0:
        return "unknown"
    ratio = hp_v / max_v
    if ratio < 0.45:
        return "low"
    if ratio < 0.65:
        return "mid"
    if ratio < 0.85:
        return "high"
    return "full"


def deck_size_band(size: Any) -> str:
    try:
        n = int(size)
    except (TypeError, ValueError):
        return "unknown"
    if n <= 15:
        return "small"
    if n <= 22:
        return "medium"
    return "large"
