#!/usr/bin/env python3
"""Extract monster move effects from official STS2 monster C# handlers."""
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
}

CARD_RE = re.compile(r"AddToCombatAndPreview<(\w+)>\([^,]+,\s*PileType\.(\w+),\s*([^,\)]+)")
SUMMON_RE = re.compile(r"CreatureCmd\.Add<(\w+)>")
MOVE_STATE_RE = re.compile(r'new MoveState\("([^"]+)",\s*(\w+)')
METHOD_RE = re.compile(r"(?:private|protected)\s+async\s+Task\s+(\w+)\([^)]*\)\s*\{")


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


def parse_effects(body: str) -> list[dict]:
    effects: list[dict] = []
    for cm in CARD_RE.finditer(body):
        card, pile, cnt = cm.group(1), cm.group(2), cm.group(3).strip()
        count = int(cnt) if cnt.isdigit() else 0
        effects.append(
            {
                "kind": "StatusInject",
                "cardId": pascal_card_id(card),
                "count": count,
                "pile": pile,
            }
        )
    for sm in SUMMON_RE.finditer(body):
        effects.append(
            {
                "kind": "Summon",
                "spawnMonsterId": pascal_card_id(sm.group(1)),
            }
        )
    if "SmoggyPower" in body:
        effects.append({"kind": "PowerAffliction", "powerId": "SMOGGY", "skillCostPenalty": 1})
    if "TangledPower" in body:
        effects.append({"kind": "PowerAffliction", "powerId": "TANGLED", "attackCostPenalty": 1})
    if "ChainsOfBindingPower" in body:
        effects.append(
            {"kind": "PowerAffliction", "powerId": "CHAINS_OF_BINDING", "boundCardsPerTurn": 3}
        )
    if "ShrinkPower" in body:
        effects.append(
            {"kind": "PowerDebuff", "powerId": "SHRINK", "attackDamageMultiplier": 0.7}
        )
    if "SwipePower" in body and "RunRng" in body:
        effects.append(
            {"kind": "PowerAffliction", "powerId": "SWIPE", "isNonDeterministic": True}
        )
    return effects


def main() -> None:
    moves: dict[str, dict[str, list]] = {}
    for path in sorted(MONSTERS_DIR.glob("*.cs")):
        text = path.read_text(encoding="utf-8")
        mid = pascal_to_id(path.stem)
        for move_id, method in MOVE_STATE_RE.findall(text):
            body = extract_method_body(text, method)
            if not body:
                continue
            effects = parse_effects(body)
            if effects:
                moves.setdefault(mid, {})[move_id] = effects

    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUT_PATH.write_text(json.dumps({"moves": moves}, indent=2), encoding="utf-8")
    print(f"Wrote {len(moves)} monsters to {OUT_PATH}")


if __name__ == "__main__":
    main()
