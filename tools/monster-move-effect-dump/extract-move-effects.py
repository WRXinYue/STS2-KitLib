#!/usr/bin/env python3
"""Extract ordered monster move effects from official STS2 monster C# handlers."""
import json
import re
from pathlib import Path

MONSTERS_DIR = Path(r"C:\Users\WRXinYue\Documents\Project\STS2\Slay the Spire 2\src\Core\Models\Monsters")
OUT_PATH = Path(__file__).resolve().parents[2] / "src" / "AI" / "Data" / "monster-move-effects.json"

ID_FIXES = {
    "EYEWITHTEETH": "EYE_WITH_TEETH",
    "SHRINKERBEETLE": "SHRINKER_BEETLE",
    "LEAFSLIMES": "LEAF_SLIME_S",
    "LEAFSLIMEM": "LEAF_SLIME_M",
    "TWIGSLIMEM": "TWIG_SLIME_M",
    "LIVINGFOG": "LIVING_FOG",
    "HAUNTEDSHIP": "HAUNTED_SHIP",
    "PHROGPARASITE": "PHROG_PARASITE",
    "SLIMEDBERSERKER": "SLIMED_BERSERKER",
    "SOULFYSH": "SOUL_FYSH",
    "THEINSATIABLE": "THE_INSATIABLE",
    "MECHAKNIGHT": "MECHA_KNIGHT",
    "THIEVINGHOPPER": "THIEVING_HOPPER",
    "VINESHAMBLER": "VINE_SHAMBLER",
    "GASBOMB": "GAS_BOMB",
    "THEOBSCURA": "THE_OBSCURA",
    "TOADPOLE": "TOADPOLE",
    "WRIGGLER": "WRIGGLER",
    "LIVINGSHIELD": "LIVING_SHIELD",
}

MOVE_STATE_RE = re.compile(r'new MoveState\("([^"]+)",\s*(\w+)')

CARD_PREVIEW_RE = re.compile(
    r"AddToCombatAndPreview<(\w+)>\([^,]+,\s*PileType\.(\w+),\s*([^,\)]+)"
)
CARD_GEN_RE = re.compile(
    r"AddGeneratedCardToCombat<(\w+)>\([^,]+,\s*PileType\.(\w+)"
)
CARD_GEN_POS_RE = re.compile(
    r"AddGeneratedCardToCombat\([^,]+,\s*(\w+),\s*addedByPlayer:\s*false,\s*CardPilePosition\.(\w+)"
)
SUMMON_RE = re.compile(r"CreatureCmd\.Add<(\w+)>")
ATTACK_RE = re.compile(r"DamageCmd\.Attack\((\w+)\)")
STRENGTH_RE = re.compile(r"PowerCmd\.Apply<StrengthPower>\(([^\)]+)\)")
THORNS_RE = re.compile(r"PowerCmd\.Apply<ThornsPower>")


def pascal_to_id(name: str) -> str:
    raw = re.sub(r"(?<!^)(?=[A-Z])", "_", name).upper()
    compact = raw.replace("_", "")
    return ID_FIXES.get(compact, raw)


def pascal_card_id(name: str) -> str:
    return re.sub(r"(?<!^)(?=[A-Z])", "_", name).upper()


def extract_method_body(text: str, method: str) -> str:
    m = re.search(rf"(?:private|protected)\s+async\s+Task\s+{method}\([^)]*\)\s*\{{", text)
    if not m:
        return ""
    start = m.end()
    depth = 1
    i = start
    while i < len(text) and depth > 0:
        if text[i] == "{":
            depth += 1
        elif text[i] == "}":
            depth -= 1
        i += 1
    return text[start : i - 1]


def parse_strength_delta(arg: str) -> int:
    arg = arg.strip()
    m = re.search(r"(-?\d+)m?", arg)
    if m:
        return int(m.group(1))
    return 1


def is_ally_strength_target(args: str) -> bool:
    first = args.split(",")[0].strip()
    return "GetTeammatesOf" in first or first == "targets"


def parse_effects_ordered(body: str) -> list[dict]:
    events: list[tuple[int, dict]] = []

    for m in CARD_PREVIEW_RE.finditer(body):
        card, pile, cnt = m.group(1), m.group(2), m.group(3).strip()
        count = int(cnt) if cnt.isdigit() else 0
        events.append(
            (
                m.start(),
                {
                    "kind": "StatusInject",
                    "cardId": pascal_card_id(card),
                    "count": count,
                    "pile": pile,
                },
            )
        )

    for m in CARD_GEN_RE.finditer(body):
        card, pile = m.group(1), m.group(2)
        events.append(
            (
                m.start(),
                {
                    "kind": "StatusInject",
                    "cardId": pascal_card_id(card),
                    "count": 1,
                    "pile": pile,
                },
            )
        )

    for m in CARD_GEN_POS_RE.finditer(body):
        pile_var, position = m.group(1), m.group(2)
        pile = "Draw" if "Draw" in pile_var else "Discard"
        if position == "Random":
            pile += "Random"
        events.append(
            (
                m.start(),
                {
                    "kind": "StatusInject",
                    "cardId": "UNKNOWN",
                    "count": 1,
                    "pile": pile,
                },
            )
        )

    for m in SUMMON_RE.finditer(body):
        events.append(
            (
                m.start(),
                {"kind": "Summon", "spawnMonsterId": pascal_card_id(m.group(1))},
            )
        )

    for m in ATTACK_RE.finditer(body):
        events.append((m.start(), {"kind": "Attack", "damage": 0}))

    for m in STRENGTH_RE.finditer(body):
        args = m.group(1)
        parts = [p.strip() for p in args.split(",")]
        delta = parse_strength_delta(parts[1] if len(parts) > 1 else "1")
        kind = "AllyStrength" if is_ally_strength_target(args) else "EnemyStrength"
        events.append((m.start(), {"kind": kind, "strengthDelta": delta}))

    if "SwipePower" in body and "RemoveFromCombat" in body:
        pos = body.find("RemoveFromCombat")
        events.append((pos, {"kind": "Steal"}))
        events.append((pos + 1, {"kind": "PowerAffliction", "powerId": "SWIPE"}))

    if "SmoggyPower" in body:
        pos = body.find("SmoggyPower")
        events.append(
            (pos, {"kind": "PowerAffliction", "powerId": "SMOGGY", "skillCostPenalty": 1})
        )
    if "TangledPower" in body:
        pos = body.find("TangledPower")
        events.append(
            (pos, {"kind": "PowerAffliction", "powerId": "TANGLED", "attackCostPenalty": 1})
        )
    if "ChainsOfBindingPower" in body:
        pos = body.find("ChainsOfBindingPower")
        events.append(
            (
                pos,
                {
                    "kind": "PowerAffliction",
                    "powerId": "CHAINS_OF_BINDING",
                    "boundCardsPerTurn": 3,
                },
            )
        )
    if "ShrinkPower" in body:
        pos = body.find("ShrinkPower")
        events.append(
            (
                pos,
                {"kind": "PowerDebuff", "powerId": "SHRINK", "attackDamageMultiplier": 0.7},
            )
        )

    events.sort(key=lambda x: x[0])
    seen: set[str] = set()
    result: list[dict] = []
    for _, effect in events:
        key = json.dumps(effect, sort_keys=True)
        if key in seen:
            continue
        seen.add(key)
        result.append(effect)
    return result


def main() -> None:
    moves: dict[str, dict[str, list]] = {}
    for path in sorted(MONSTERS_DIR.glob("*.cs")):
        text = path.read_text(encoding="utf-8")
        mid = pascal_to_id(path.stem)
        for move_id, method in MOVE_STATE_RE.findall(text):
            body = extract_method_body(text, method)
            if not body:
                continue
            effects = parse_effects_ordered(body)
            if effects:
                moves.setdefault(mid, {})[move_id] = effects

    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUT_PATH.write_text(json.dumps({"moves": moves}, indent=2), encoding="utf-8")
    print(f"Wrote {len(moves)} monsters to {OUT_PATH}")


if __name__ == "__main__":
    main()
