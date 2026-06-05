from __future__ import annotations

import argparse
from pathlib import Path

import pandas as pd

from codex_train.paths import DEFAULT_PARQUET, DEFAULT_PRIORS_DIR, DEFAULT_PRIORS_JSON, DEFAULT_SCORES_DIR, MOD_PRIORS_JSON
from codex_train.eval_holdout import run_eval, write_report
from codex_train.train_priors import train_all, write_priors


def cmd_analyze(parquet: Path) -> int:
    df = pd.read_parquet(parquet)
    print(f"rows={len(df)} runs={df['run_hash'].nunique()}")
    print("\n=== sample_type ===")
    print(df["sample_type"].value_counts())
    print("\n=== character (runs) ===")
    print(df.groupby("character")["run_hash"].nunique().sort_values(ascending=False))
    print("\n=== context (card_choice) ===")
    card = df[df["sample_type"] == "card_choice"]
    if not card.empty:
        print(card["context"].value_counts())
    print("\n=== sample_type event ===")
    event = df[df["sample_type"] == "event"]
    if not event.empty:
        print(f"rows={len(event)}")
        if "event_id" in event.columns:
            print(event["event_id"].value_counts().head(10))
    return 0


def cmd_train(parquet: Path, scores_dir: Path, output: Path, mod_output: Path) -> int:
    df = pd.read_parquet(parquet)
    priors = train_all(df, scores_dir if scores_dir.is_dir() else None)
    write_priors(priors, output)
    write_priors(priors, mod_output)
    print(f"Wrote priors -> {output}")
    print(f"Synced mod -> {mod_output}")
    print(f"  cards: {priors['cards'].get('_meta', {})}")
    print(f"  skip buckets: {priors['skip'].get('_meta', {})}")
    print(f"  rest buckets: {len([k for k in priors['rest'] if k != '_meta'])}")
    print(f"  events: {priors['events'].get('_meta', {})}")
    return 0


def cmd_eval(parquet: Path, priors_path: Path, report_path: Path) -> int:
    df = pd.read_parquet(parquet)
    import json

    priors = json.loads(priors_path.read_text(encoding="utf-8"))
    report = run_eval(df, priors)
    write_report(report, report_path)
    print(json.dumps(report, indent=2))
    return 0


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Train DevMode Codex priors")
    parser.add_argument("--parquet", type=Path, default=DEFAULT_PARQUET)
    parser.add_argument("--scores-dir", type=Path, default=DEFAULT_SCORES_DIR)
    parser.add_argument("--output", type=Path, default=DEFAULT_PRIORS_JSON)
    parser.add_argument("--mod-output", type=Path, default=MOD_PRIORS_JSON)
    parser.add_argument("--report", type=Path, default=DEFAULT_PRIORS_DIR / "eval_report.json")

    sub = parser.add_subparsers(dest="command", required=True)
    sub.add_parser("analyze", help="Print dataset summary").set_defaults(func=lambda a: cmd_analyze(a.parquet))
    sub.add_parser("train", help="Train priors and sync to mod").set_defaults(
        func=lambda a: cmd_train(a.parquet, a.scores_dir, a.output, a.mod_output)
    )
    sub.add_parser("eval", help="Holdout evaluation").set_defaults(
        func=lambda a: cmd_eval(a.parquet, a.output, a.report)
    )

    args = parser.parse_args(argv)
    return int(args.func(args))


if __name__ == "__main__":
    raise SystemExit(main())
