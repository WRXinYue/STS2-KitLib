"""SQLite storage for crawled Spire Codex runs."""

from __future__ import annotations

import json
import sqlite3
from dataclasses import dataclass
from datetime import datetime, timezone
from pathlib import Path
from typing import Any, Iterable

from codex_crawl.versioning import build_id_at_least


def utc_now() -> str:
    return datetime.now(timezone.utc).replace(microsecond=0).isoformat()


@dataclass(frozen=True)
class RunMeta:
    run_hash: str
    character: str | None
    ascension: int | None
    win: bool | None
    was_abandoned: bool | None
    game_mode: str | None
    run_time: int | None
    floors_reached: int | None
    killed_by: str | None
    deck_size: int | None
    relic_count: int | None
    username: str | None
    build_id: str | None
    submitted_at: str | None


class CodexDatabase:
    def __init__(self, data_dir: Path) -> None:
        self.data_dir = data_dir
        self.data_dir.mkdir(parents=True, exist_ok=True)
        self.runs_dir = self.data_dir / "runs"
        self.runs_dir.mkdir(parents=True, exist_ok=True)
        self.db_path = self.data_dir / "codex.db"
        self.conn = sqlite3.connect(self.db_path)
        self.conn.row_factory = sqlite3.Row
        self._init_schema()

    def close(self) -> None:
        self.conn.close()

    def _init_schema(self) -> None:
        self.conn.executescript(
            """
            CREATE TABLE IF NOT EXISTS runs (
                run_hash TEXT PRIMARY KEY,
                character TEXT,
                ascension INTEGER,
                win INTEGER,
                was_abandoned INTEGER,
                game_mode TEXT,
                run_time INTEGER,
                floors_reached INTEGER,
                killed_by TEXT,
                deck_size INTEGER,
                relic_count INTEGER,
                username TEXT,
                build_id TEXT,
                submitted_at TEXT,
                schema_version INTEGER,
                has_full INTEGER NOT NULL DEFAULT 0,
                crawl_error TEXT,
                list_seen_at TEXT,
                full_fetched_at TEXT
            );

            CREATE INDEX IF NOT EXISTS idx_runs_has_full ON runs(has_full);
            CREATE INDEX IF NOT EXISTS idx_runs_filters ON runs(character, ascension, win, build_id);

            CREATE TABLE IF NOT EXISTS list_cursors (
                filter_key TEXT PRIMARY KEY,
                last_page INTEGER NOT NULL DEFAULT 0,
                total_pages INTEGER,
                total_runs INTEGER,
                completed INTEGER NOT NULL DEFAULT 0,
                updated_at TEXT
            );
            """
        )
        self.conn.commit()

    @staticmethod
    def meta_from_list_row(row: dict[str, Any]) -> RunMeta:
        return RunMeta(
            run_hash=str(row["run_hash"]),
            character=row.get("character"),
            ascension=_maybe_int(row.get("ascension")),
            win=_maybe_bool(row.get("win")),
            was_abandoned=_maybe_bool(row.get("was_abandoned")),
            game_mode=row.get("game_mode"),
            run_time=_maybe_int(row.get("run_time")),
            floors_reached=_maybe_int(row.get("floors_reached")),
            killed_by=row.get("killed_by"),
            deck_size=_maybe_int(row.get("deck_size")),
            relic_count=_maybe_int(row.get("relic_count")),
            username=row.get("username"),
            build_id=row.get("build_id"),
            submitted_at=row.get("submitted_at"),
        )

    def upsert_list_rows(self, rows: Iterable[RunMeta]) -> int:
        now = utc_now()
        count = 0
        for meta in rows:
            self.conn.execute(
                """
                INSERT INTO runs (
                    run_hash, character, ascension, win, was_abandoned, game_mode,
                    run_time, floors_reached, killed_by, deck_size, relic_count,
                    username, build_id, submitted_at, list_seen_at
                ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                ON CONFLICT(run_hash) DO UPDATE SET
                    character=excluded.character,
                    ascension=excluded.ascension,
                    win=excluded.win,
                    was_abandoned=excluded.was_abandoned,
                    game_mode=excluded.game_mode,
                    run_time=excluded.run_time,
                    floors_reached=excluded.floors_reached,
                    killed_by=excluded.killed_by,
                    deck_size=excluded.deck_size,
                    relic_count=excluded.relic_count,
                    username=excluded.username,
                    build_id=excluded.build_id,
                    submitted_at=excluded.submitted_at,
                    list_seen_at=excluded.list_seen_at
                """,
                (
                    meta.run_hash,
                    meta.character,
                    meta.ascension,
                    _bool_to_int(meta.win),
                    _bool_to_int(meta.was_abandoned),
                    meta.game_mode,
                    meta.run_time,
                    meta.floors_reached,
                    meta.killed_by,
                    meta.deck_size,
                    meta.relic_count,
                    meta.username,
                    meta.build_id,
                    meta.submitted_at,
                    now,
                ),
            )
            count += 1
        self.conn.commit()
        return count

    def get_list_cursor(self, filter_key: str) -> sqlite3.Row | None:
        cur = self.conn.execute(
            "SELECT * FROM list_cursors WHERE filter_key = ?",
            (filter_key,),
        )
        return cur.fetchone()

    def save_list_cursor(
        self,
        filter_key: str,
        last_page: int,
        total_pages: int | None,
        total_runs: int | None,
        completed: bool,
    ) -> None:
        self.conn.execute(
            """
            INSERT INTO list_cursors (filter_key, last_page, total_pages, total_runs, completed, updated_at)
            VALUES (?, ?, ?, ?, ?, ?)
            ON CONFLICT(filter_key) DO UPDATE SET
                last_page=excluded.last_page,
                total_pages=excluded.total_pages,
                total_runs=excluded.total_runs,
                completed=excluded.completed,
                updated_at=excluded.updated_at
            """,
            (
                filter_key,
                last_page,
                total_pages,
                total_runs,
                1 if completed else 0,
                utc_now(),
            ),
        )
        self.conn.commit()

    def pending_full_hashes(
        self,
        limit: int,
        *,
        min_build_id: str | None = None,
    ) -> list[str]:
        cur = self.conn.execute(
            """
            SELECT run_hash, build_id FROM runs
            WHERE has_full = 0 AND (crawl_error IS NULL OR crawl_error = '')
            ORDER BY submitted_at DESC
            LIMIT ?
            """,
            (max(limit * 4, limit),),
        )
        selected: list[str] = []
        for row in cur.fetchall():
            build_id = row["build_id"]
            if min_build_id and build_id and not build_id_at_least(str(build_id), min_build_id):
                continue
            selected.append(str(row["run_hash"]))
            if len(selected) >= limit:
                break
        return selected

    def save_full_run(self, run_hash: str, payload: dict[str, Any]) -> None:
        path = self.runs_dir / f"{run_hash}.json"
        path.write_text(json.dumps(payload, ensure_ascii=False), encoding="utf-8")
        schema_version = _maybe_int(payload.get("schema_version"))
        build_id = payload.get("build_id")
        self.conn.execute(
            """
            UPDATE runs SET
                has_full = 1,
                schema_version = ?,
                build_id = COALESCE(?, build_id),
                crawl_error = NULL,
                full_fetched_at = ?
            WHERE run_hash = ?
            """,
            (schema_version, build_id, utc_now(), run_hash),
        )
        self.conn.commit()

    def mark_full_error(self, run_hash: str, message: str) -> None:
        self.conn.execute(
            "UPDATE runs SET crawl_error = ? WHERE run_hash = ?",
            (message[:500], run_hash),
        )
        self.conn.commit()

    def load_full_run(self, run_hash: str) -> dict[str, Any] | None:
        path = self.runs_dir / f"{run_hash}.json"
        if not path.is_file():
            return None
        return json.loads(path.read_text(encoding="utf-8"))

    def stats(self) -> dict[str, int]:
        cur = self.conn.execute(
            """
            SELECT
                COUNT(*) AS total,
                SUM(CASE WHEN has_full = 1 THEN 1 ELSE 0 END) AS with_full,
                SUM(CASE WHEN crawl_error IS NOT NULL AND crawl_error != '' THEN 1 ELSE 0 END) AS errors
            FROM runs
            """
        )
        row = cur.fetchone()
        return {
            "total": int(row["total"] or 0),
            "with_full": int(row["with_full"] or 0),
            "errors": int(row["errors"] or 0),
        }

    def iter_full_runs(
        self,
        *,
        character: str | None = None,
        ascension: int | None = None,
        win: bool | None = None,
        min_schema: int | None = None,
        min_build_id: str | None = None,
    ) -> Iterable[tuple[str, dict[str, Any]]]:
        clauses = ["has_full = 1"]
        params: list[Any] = []
        if character:
            clauses.append("character = ?")
            params.append(character.upper())
        if ascension is not None:
            clauses.append("ascension = ?")
            params.append(ascension)
        if win is not None:
            clauses.append("win = ?")
            params.append(1 if win else 0)
        if min_schema is not None:
            clauses.append("schema_version >= ?")
            params.append(min_schema)

        sql = f"SELECT run_hash, build_id FROM runs WHERE {' AND '.join(clauses)} ORDER BY submitted_at DESC"
        cur = self.conn.execute(sql, params)
        for row in cur:
            build_id = row["build_id"]
            if min_build_id and not build_id_at_least(str(build_id) if build_id else None, min_build_id):
                continue
            run_hash = str(row["run_hash"])
            payload = self.load_full_run(run_hash)
            if payload is not None:
                yield run_hash, payload


def _maybe_int(value: Any) -> int | None:
    if value is None or value == "":
        return None
    return int(value)


def _maybe_bool(value: Any) -> bool | None:
    if value is None or value == "":
        return None
    if isinstance(value, bool):
        return value
    return bool(int(value))


def _bool_to_int(value: bool | None) -> int | None:
    if value is None:
        return None
    return 1 if value else 0
