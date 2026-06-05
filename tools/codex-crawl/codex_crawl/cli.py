"""CLI for Spire Codex crawl and export."""

from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

from codex_crawl.client import DEFAULT_BASE_URL, DEFAULT_RPM, CodexClient
from codex_crawl.db import CodexDatabase
from codex_crawl.export_macro import export_macro_jsonl, export_macro_parquet
from codex_crawl.paths import DEFAULT_DATA_DIR
from codex_crawl.versioning import build_id_at_least


def _print(msg: str) -> None:
    """Plain stdout safe on Windows cp1252 consoles (no Rich spinners/markup)."""
    try:
        print(msg, flush=True)
    except UnicodeEncodeError:
        print(msg.encode("ascii", errors="replace").decode("ascii"), flush=True)


def _build_filter_key(params: dict[str, Any]) -> str:
    parts = [f"{k}={params[k]}" for k in sorted(params) if params[k] is not None]
    return "|".join(parts) if parts else "all"


def cmd_status(args: argparse.Namespace) -> int:
    db = CodexDatabase(Path(args.data_dir))
    stats = db.stats()
    _print(f"Database: {db.db_path}")
    _print(f"Runs indexed:    {stats['total']}")
    _print(f"Full JSON saved: {stats['with_full']}")
    _print(f"Fetch errors:    {stats['errors']}")
    pending = stats["total"] - stats["with_full"] - stats["errors"]
    if pending > 0:
        _print(f"Pending full:    {pending}")
    db.close()
    return 0


def cmd_fetch_scores(args: argparse.Namespace) -> int:
    data_dir = Path(args.data_dir)
    scores_dir = data_dir / "scores"
    scores_dir.mkdir(parents=True, exist_ok=True)

    with CodexClient(base_url=args.base_url, rpm=args.rpm) as client:
        for entity in ("cards", "relics", "potions"):
            _print(f"Fetching /api/runs/scores/{entity} ...")
            payload = client.get_json(f"/api/runs/scores/{entity}")
            out_path = scores_dir / f"{entity}.json"
            out_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")
            count = len(payload) if isinstance(payload, dict) else 0
            _print(f"  saved {count} entries -> {out_path}")

    _print(f"Done. Scores directory: {scores_dir}")
    return 0


def cmd_crawl_list(args: argparse.Namespace) -> int:
    data_dir = Path(args.data_dir)
    db = CodexDatabase(data_dir)

    list_params: dict[str, Any] = {
        "limit": args.page_size,
        "character": args.character.upper() if args.character else None,
        "ascension": args.ascension,
        "win": "true" if args.win else ("false" if args.win is False else None),
        "username": args.username,
        "seed": args.seed,
        "sort": args.sort,
    }
    filter_key = _build_filter_key(list_params)
    cursor = db.get_list_cursor(filter_key)
    start_page = 1 if args.reset else ((cursor["last_page"] + 1) if cursor else 1)
    if cursor and cursor["completed"] and not args.reset:
        _print(f"List crawl already completed for filter: {filter_key}")
        db.close()
        return 0

    _print(f"Listing runs filter={filter_key} from page {start_page} (page_size={args.page_size})")
    page = start_page
    total_saved = 0
    total_pages = cursor["total_pages"] if cursor else None
    total_runs = cursor["total_runs"] if cursor else None

    with CodexClient(base_url=args.base_url, rpm=args.rpm) as client:
        while True:
            params = {**list_params, "page": page}
            payload = client.get_json("/api/runs/list", params)
            rows = payload.get("runs") or []
            total_pages = payload.get("total_pages", total_pages)
            total_runs = payload.get("total", total_runs)

            metas = [db.meta_from_list_row(row) for row in rows]
            saved = db.upsert_list_rows(metas)
            total_saved += saved
            db.save_list_cursor(filter_key, page, total_pages, total_runs, completed=False)

            _print(
                f"  page {page}/{total_pages or '?'} "
                f"(+{saved}, cumulative={total_saved}, remote total={total_runs})"
            )

            if args.max_pages and page - start_page + 1 >= args.max_pages:
                _print(f"Stopped after --max-pages={args.max_pages}")
                break
            if total_pages and page >= total_pages:
                db.save_list_cursor(filter_key, page, total_pages, total_runs, completed=True)
                _print(f"List crawl complete ({total_runs} runs for this filter).")
                break
            if not rows:
                db.save_list_cursor(filter_key, page, total_pages, total_runs, completed=True)
                _print("No more rows.")
                break
            page += 1

    db.close()
    return 0


def cmd_crawl_full(args: argparse.Namespace) -> int:
    data_dir = Path(args.data_dir)
    db = CodexDatabase(data_dir)
    remaining = args.max_runs if args.max_runs > 0 else None
    fetched = 0

    with CodexClient(base_url=args.base_url, rpm=args.rpm) as client:
        while remaining is None or remaining > 0:
            batch_size = min(args.batch_size, remaining) if remaining is not None else args.batch_size
            pending = db.pending_full_hashes(batch_size, min_build_id=args.min_build_id or None)
            if not pending:
                stats = db.stats()
                remaining_rows = stats["total"] - stats["with_full"] - stats["errors"]
                if remaining_rows > 0 and args.min_build_id:
                    _print(
                        f"No fetchable runs left at min-build-id {args.min_build_id} "
                        f"({remaining_rows} indexed rows still lack full JSON)."
                    )
                break

            for run_hash in pending:
                try:
                    payload = client.get_json(f"/api/runs/shared/{run_hash}")
                except RuntimeError as exc:
                    db.mark_full_error(run_hash, str(exc))
                    _print(f"ERROR {run_hash}: {exc}")
                    continue

                schema_version = payload.get("schema_version")
                build_id = payload.get("build_id")
                if args.min_schema and (schema_version or 0) < args.min_schema:
                    db.mark_full_error(run_hash, f"schema {schema_version} < min {args.min_schema}")
                    continue
                if args.min_build_id and not build_id_at_least(str(build_id) if build_id else None, args.min_build_id):
                    db.mark_full_error(run_hash, f"build {build_id} < min {args.min_build_id}")
                    continue

                db.save_full_run(run_hash, payload)
                fetched += 1
                if fetched == 1 or fetched % 25 == 0:
                    stats = db.stats()
                    _print(
                        f"  fetched {fetched} this session "
                        f"({stats['with_full']}/{stats['total']} total with full JSON)"
                    )
                if remaining is not None:
                    remaining -= 1
                    if remaining <= 0:
                        break

    stats = db.stats()
    _print(
        f"Done. Full JSON: {stats['with_full']}/{stats['total']} "
        f"({stats['errors']} errors) in {db.runs_dir}"
    )
    db.close()
    return 0


def _export_runs(args: argparse.Namespace):
    db = CodexDatabase(Path(args.data_dir))
    win = True if args.win else (False if args.loss else None)
    runs = db.iter_full_runs(
        character=args.character.upper() if args.character else None,
        ascension=args.ascension,
        win=win,
        min_schema=args.min_schema,
        min_build_id=args.min_build_id,
    )
    return db, runs


def cmd_export_macro(args: argparse.Namespace) -> int:
    db, runs = _export_runs(args)
    output = Path(args.output)
    count = export_macro_jsonl(runs, output, include_deck_snapshot=args.include_deck)
    _print(f"Exported {count} macro samples -> {output}")
    db.close()
    return 0


def cmd_export_parquet(args: argparse.Namespace) -> int:
    db, runs = _export_runs(args)
    output = Path(args.output)
    count = export_macro_parquet(runs, output, include_deck_snapshot=args.include_deck)
    _print(f"Exported {count} macro rows -> {output}")
    db.close()
    return 0


def cmd_sync(args: argparse.Namespace) -> int:
    list_args = argparse.Namespace(**vars(args))
    list_args.max_pages = args.max_list_pages
    rc = cmd_crawl_list(list_args)
    if rc != 0:
        return rc

    rc = cmd_crawl_full(args)
    if rc != 0:
        return rc

    if args.fetch_scores:
        scores_args = argparse.Namespace(
            data_dir=args.data_dir,
            base_url=args.base_url,
            rpm=args.rpm,
        )
        cmd_fetch_scores(scores_args)
    return 0


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="Crawl Spire Codex runs for offline AI training.",
    )
    parser.add_argument(
        "--data-dir",
        default=str(DEFAULT_DATA_DIR),
        help=f"Output directory (default: {DEFAULT_DATA_DIR})",
    )
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL)
    parser.add_argument(
        "--rpm",
        type=float,
        default=DEFAULT_RPM,
        help=f"Max requests per minute (API limit 60; default {DEFAULT_RPM})",
    )

    sub = parser.add_subparsers(dest="command", required=True)

    sub.add_parser("status", help="Show local crawl statistics").set_defaults(func=cmd_status)
    sub.add_parser("fetch-scores", help="Download /api/runs/scores/* JSON").set_defaults(func=cmd_fetch_scores)

    def add_list_filters(p: argparse.ArgumentParser, *, include_loss: bool = False) -> None:
        p.add_argument("--character", help="IRONCLAD, DEFECT, SILENT, ...")
        p.add_argument("--ascension", type=int)
        p.add_argument("--win", action="store_true", help="Only winning runs")
        if include_loss:
            p.add_argument("--loss", action="store_true", help="Only losing runs")
        p.add_argument("--username")
        p.add_argument("--seed")
        p.add_argument("--sort", default="newest", help="newest, fastest, highest_asc, ...")
        p.add_argument("--page-size", type=int, default=100)
        p.add_argument("--max-pages", type=int, help="Stop after N list pages")
        p.add_argument("--reset", action="store_true", help="Restart list pagination from page 1")

    def add_full_filters(p: argparse.ArgumentParser) -> None:
        p.add_argument("--max-runs", type=int, default=0, help="Max full runs to fetch (0=unlimited)")
        p.add_argument("--batch-size", type=int, default=50)
        p.add_argument("--min-schema", type=int, default=9, help="Skip runs below schema version")
        p.add_argument("--min-build-id", default="v0.101.0", help="Skip older game builds")

    def add_export_options(p: argparse.ArgumentParser) -> None:
        add_list_filters(p, include_loss=True)
        add_full_filters(p)
        p.add_argument(
            "--output",
            help="Output path (defaults differ per command)",
        )
        p.add_argument(
            "--include-deck",
            action="store_true",
            help="Include deck_ids/relic_ids snapshot on each sample",
        )

    p_list = sub.add_parser("crawl-list", help="Index run metadata via /api/runs/list")
    add_list_filters(p_list)
    p_list.set_defaults(func=cmd_crawl_list)

    p_full = sub.add_parser("crawl-full", help="Download full .run JSON for indexed runs")
    add_full_filters(p_full)
    p_full.set_defaults(func=cmd_crawl_full)

    p_sync = sub.add_parser("sync", help="crawl-list then crawl-full (recommended)")
    add_list_filters(p_sync)
    add_full_filters(p_sync)
    p_sync.add_argument("--max-list-pages", type=int, help="Cap list pages before full fetch")
    p_sync.add_argument("--fetch-scores", action="store_true", help="Also download entity scores")
    p_sync.set_defaults(func=cmd_sync)

    p_jsonl = sub.add_parser("export-macro", help="Flatten stored runs to JSONL training samples")
    add_export_options(p_jsonl)
    p_jsonl.set_defaults(
        func=cmd_export_macro,
        output=str(DEFAULT_DATA_DIR / "macro_samples.jsonl"),
    )

    p_parquet = sub.add_parser("export-parquet", help="Flatten stored runs to Parquet (needs train group)")
    add_export_options(p_parquet)
    p_parquet.set_defaults(
        func=cmd_export_parquet,
        output=str(DEFAULT_DATA_DIR / "macro_samples.parquet"),
    )

    return parser


def main(argv: list[str] | None = None) -> int:
    parser = build_parser()
    args = parser.parse_args(argv)
    if getattr(args, "win", False) and getattr(args, "loss", False):
        parser.error("Use only one of --win or --loss")
    return int(args.func(args))
