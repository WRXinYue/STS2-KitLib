#!/usr/bin/env python3
"""Extract combat-relevant relic hooks from official STS2 RelicModel handlers."""
import json
import re
from pathlib import Path

RELICS_DIR = Path(
    r"C:\Users\WRXinYue\Documents\Project\STS2\Slay the Spire 2\src\Core\Models\Relics"
)
OUT_PATH = (
    Path(__file__).resolve().parents[2] / "src" / "AI" / "Data" / "relic-combat-effects.json"
)

COMBAT_HOOKS = {
    "ModifyHandDraw",
    "ModifyHandDrawLate",
    "ModifyMaxEnergy",
    "ModifyEnergyGain",
    "AfterHandEmptied",
    "ShouldFlush",
    "ModifyDamageAdditive",
    "ModifyDamageMultiplicative",
    "ModifyDamageCap",
    "ModifyBlockAdditive",
    "ModifyBlockMultiplicative",
    "BeforeCombatStart",
    "BeforeCombatStartLate",
    "AfterPlayerTurnStart",
    "AfterPlayerTurnStartEarly",
    "AfterPlayerTurnStartLate",
    "BeforePlayPhaseStart",
    "ModifyShuffleOrder",
    "TryModifyEnergyCostInCombat",
    "ModifyCardPlayCount",
    "ShouldClearBlock",
    "AfterEnergyReset",
    "AfterEnergyResetLate",
    "BeforeCardPlayed",
    "AfterCardPlayed",
    "ModifyXValue",
    "ShouldDie",
    "TryPreventDeath",
    "AfterBlockBroken",
    "AfterCardDiscarded",
    "AfterCardExhausted",
    "ModifyAttackHitCount",
    "ShouldPlayerResetEnergy",
}

SIMULATABLE_KINDS = {
    "HandDrawBonus",
    "HandDrawBonusLate",
    "MaxEnergyDelta",
    "EnergyGainDelta",
    "RetainHandOnEndTurn",
    "AppliesPower",
    "DrawOnHandEmpty",
    "StartOfCombatBlock",
    "RetainEnergyOnTurnStart",
}

VAR_RE = re.compile(r"new\s+(\w+Var)\(([^)]*)\)")
POWER_APPLY_RE = re.compile(r"PowerCmd\.Apply<(\w+)>")
DRAW_RE = re.compile(r"CardPileCmd\.Draw")
BLOCK_GAIN_RE = re.compile(r"CreatureCmd\.GainBlock")
HAND_DRAW_DELTA_RE = re.compile(
    r"return\s+count\s*\+\s*(?:\(decimal\)\s*)?base\.DynamicVars\.(\w+)\.(?:BaseValue|IntValue)"
)
MAX_ENERGY_DELTA_RE = re.compile(
    r"return\s+amount\s*\+\s*(?:\(decimal\)\s*)?base\.DynamicVars\.(\w+)\.(?:BaseValue|IntValue)"
)
ENERGY_GAIN_DELTA_RE = re.compile(
    r"return\s+amount\s*\+\s*(?:\(decimal\)\s*)?base\.DynamicVars\.(\w+)\.(?:BaseValue|IntValue)"
)
ROUND_GT_ONE_RE = re.compile(r"RoundNumber\s*>\s*1")
ROUND_LE_ONE_RE = re.compile(r"RoundNumber\s*<=\s*1")
ROUND_EQ_ONE_RE = re.compile(r"RoundNumber\s*==\s*1")
SHOULD_FLUSH_FALSE_RE = re.compile(r"return\s+false\s*;")


def pascal_to_id(name: str) -> str:
    return re.sub(r"(?<!^)(?=[A-Z])", "_", name).upper()


def power_to_id(power_class: str) -> str:
    name = power_class
    if name.endswith("Power"):
        name = name[: -len("Power")]
    return pascal_to_id(name)


def parse_var_amount(arg: str) -> int | float | None:
    arg = arg.strip()
    m = re.search(r"(-?\d+(?:\.\d+)?)m?", arg)
    if not m:
        return None
    raw = m.group(1)
    return int(raw) if "." not in raw else float(raw)


def parse_canonical_vars(text: str) -> dict[str, int | float]:
    vars_out: dict[str, int | float] = {}
    for m in VAR_RE.finditer(text):
        var_type, arg = m.group(1), m.group(2)
        amount = parse_var_amount(arg)
        if amount is None:
            continue
        key = var_type.replace("Var", "")
        vars_out[key] = amount
    return vars_out


def extract_method_body(text: str, method: str) -> str:
    m = re.search(
        rf"public\s+override\s+(?:async\s+)?(?:Task|decimal|int|bool|\([^)]+\))\s+{method}\s*\([^)]*\)\s*\{{",
        text,
    )
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


def detect_hooks(text: str) -> list[str]:
    hooks: list[str] = []
    for hook in sorted(COMBAT_HOOKS):
        if re.search(rf"public\s+override\s+.*\s+{hook}\s*\(", text):
            hooks.append(hook)
    return hooks


def parse_hand_draw_bonus(body: str, canonical: dict[str, int | float], late: bool) -> dict | None:
    m = HAND_DRAW_DELTA_RE.search(body)
    if not m:
        return None
    var_key = m.group(1)
    delta = canonical.get(var_key.replace("Cards", "Cards"), None)
    if delta is None and var_key == "Cards":
        delta = canonical.get("Cards")
    if delta is None:
        return None
    effect = {
        "kind": "HandDrawBonusLate" if late else "HandDrawBonus",
        "delta": int(delta),
    }
    if ROUND_GT_ONE_RE.search(body):
        effect["maxCombatRound"] = 1
    elif ROUND_LE_ONE_RE.search(body) or ROUND_EQ_ONE_RE.search(body):
        effect["maxCombatRound"] = 1
    return effect


def parse_max_energy_delta(body: str, canonical: dict[str, int | float]) -> dict | None:
    m = MAX_ENERGY_DELTA_RE.search(body)
    if not m:
        return None
    var_key = m.group(1)
    delta = canonical.get(var_key, canonical.get("Energy"))
    if delta is None:
        return None
    return {"kind": "MaxEnergyDelta", "delta": int(delta)}


def parse_retain_energy(body: str) -> dict | None:
    if not SHOULD_FLUSH_FALSE_RE.search(body):
        return None
    effect: dict = {"kind": "RetainEnergyOnTurnStart"}
    if ROUND_EQ_ONE_RE.search(body):
        effect["minCombatRound"] = 2
    return effect


def parse_energy_gain_delta(body: str, canonical: dict[str, int | float]) -> dict | None:
    m = ENERGY_GAIN_DELTA_RE.search(body)
    if not m:
        return None
    var_key = m.group(1)
    delta = canonical.get(var_key, canonical.get("Energy"))
    if delta is None:
        return None
    return {"kind": "EnergyGainDelta", "delta": int(delta)}


def parse_power_applies(text: str, method: str | None = None) -> list[dict]:
    scope = extract_method_body(text, method) if method else text
    effects: list[dict] = []
    for m in POWER_APPLY_RE.finditer(scope):
        power_id = power_to_id(m.group(1))
        effects.append({"kind": "AppliesPower", "powerId": power_id, "amount": 1})
    return effects


def parse_start_block(text: str) -> dict | None:
    body = extract_method_body(text, "BeforeCombatStart")
    if not BLOCK_GAIN_RE.search(body):
        return None
    block = canonical.get("Block") if (canonical := parse_canonical_vars(text)) else None
    if block is None:
        return {"kind": "StartOfCombatBlock", "block": 0, "needsManual": True}
    return {"kind": "StartOfCombatBlock", "block": int(block)}


def parse_relic(path: Path) -> dict | None:
    text = path.read_text(encoding="utf-8")
    class_m = re.search(r"public\s+sealed\s+class\s+(\w+)\s*:\s*RelicModel", text)
    if not class_m:
        return None

    relic_id = pascal_to_id(class_m.group(1))
    hooks = detect_hooks(text)
    canonical = parse_canonical_vars(text)
    effects: list[dict] = []
    seen: set[str] = set()

    def add(effect: dict | None) -> None:
        if effect is None:
            return
        key = json.dumps(effect, sort_keys=True)
        if key in seen:
            return
        seen.add(key)
        effects.append(effect)

    hand_body = extract_method_body(text, "ModifyHandDraw")
    add(parse_hand_draw_bonus(hand_body, canonical, late=False))

    hand_late_body = extract_method_body(text, "ModifyHandDrawLate")
    add(parse_hand_draw_bonus(hand_late_body, canonical, late=True))

    add(parse_max_energy_delta(extract_method_body(text, "ModifyMaxEnergy"), canonical))
    add(parse_energy_gain_delta(extract_method_body(text, "ModifyEnergyGain"), canonical))

    flush_body = extract_method_body(text, "ShouldFlush")
    if flush_body and SHOULD_FLUSH_FALSE_RE.search(flush_body):
        add({"kind": "RetainHandOnEndTurn"})

    add(parse_retain_energy(extract_method_body(text, "ShouldPlayerResetEnergy")))

    empty_body = extract_method_body(text, "AfterHandEmptied")
    if empty_body and DRAW_RE.search(empty_body):
        add({"kind": "DrawOnHandEmpty", "count": 1})

    combat_power_hooks = {
        h
        for h in hooks
        if h
        in {
            "BeforeCombatStart",
            "BeforeCombatStartLate",
            "AfterPlayerTurnStart",
            "AfterPlayerTurnStartEarly",
            "AfterPlayerTurnStartLate",
        }
    }
    if combat_power_hooks:
        for eff in parse_power_applies(text):
            add(eff)

    add(parse_start_block(text))

    simulatable = any(e["kind"] in SIMULATABLE_KINDS and not e.get("needsManual") for e in effects)
    if not hooks and not effects:
        return None

    entry: dict = {"hooks": hooks}
    if effects:
        entry["effects"] = effects
    entry["simulatable"] = simulatable
    if hooks and not simulatable:
        entry["needsManual"] = True
    return relic_id, entry


def main() -> None:
    relics: dict[str, dict] = {}
    sim_count = 0
    hook_count = 0

    for path in sorted(RELICS_DIR.glob("*.cs")):
        parsed = parse_relic(path)
        if not parsed:
            continue
        relic_id, entry = parsed
        relics[relic_id] = entry
        if entry.get("hooks"):
            hook_count += 1
        if entry.get("simulatable"):
            sim_count += 1

    OUT_PATH.parent.mkdir(parents=True, exist_ok=True)
    OUT_PATH.write_text(
        json.dumps({"relics": relics}, indent=2, ensure_ascii=False) + "\n",
        encoding="utf-8",
    )
    print(f"Wrote {len(relics)} relics ({sim_count} simulatable, {hook_count} with combat hooks) -> {OUT_PATH}")


if __name__ == "__main__":
    main()
