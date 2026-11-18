#!/usr/bin/env python3
"""One entry point for KitLib release publishing.

This script is the release interface; the Makefile has no publishing targets.

Non-interactive verbs (used by contributors, CI and agents):

    status      Print version / tag / changelog / asset / tool readiness
    preflight   Validate the selected channels; exit 1 when a blocker is found
    build       Build the multi-API release bundle and its zip
    github      Publish the GitHub Release (missing assets are built first)
    nexus       Publish to Nexus Mods
    steam       Stage and upload the Steam Workshop item
    all         github, then nexus, then steam

Run without a verb on an interactive terminal to get the channel picker. Without a
TTY the help is printed and the exit code is 2, so scripts and CI never block on a
prompt. The menu is a thin layer over the verbs above: every step it takes is shown
as the equivalent non-interactive command.

Examples:
    python3 scripts/release_cli.py status
    python3 scripts/release_cli.py preflight --channels github,nexus
    python3 scripts/release_cli.py build
    python3 scripts/release_cli.py all --version 0.53.0 --dry-run
    python3 scripts/release_cli.py steam --skip-build --change-note "hotfix"
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shlex
import shutil
import subprocess
import sys
from dataclasses import dataclass
from pathlib import Path

_SCRIPT_DIR = Path(__file__).resolve().parent
_REPO = _SCRIPT_DIR.parent
if str(_SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(_SCRIPT_DIR))

from lib.dotenv import load_release_config  # noqa: E402
from lib.mod_uploader_paths import (  # noqa: E402
    candidate_uploaders,
    default_install_dir,
    executable_name,
)
from lib.release_assets import (  # noqa: E402
    github_release_assets,
    mod_zip_path,
    tools_rid,
)
from lib.release_build import (  # noqa: E402
    ReleaseBuildError,
    build_mcp_zip,
    build_mod_zip,
)

CHANNELS = ("github", "nexus", "steam")
CHANNEL_LABELS = {"github": "GitHub", "nexus": "Nexus", "steam": "Steam"}
VERBS = ("status", "preflight", "build") + CHANNELS + ("all",)
LEVEL_MARKS = {"ok": "ok  ", "warn": "WARN", "fail": "FAIL"}


@dataclass(frozen=True)
class Check:
    """One readiness fact shown by ``status`` / ``preflight`` / the menu."""

    level: str
    name: str
    detail: str


# ---------------------------------------------------------------------------
# Small helpers
# ---------------------------------------------------------------------------


def _script_command(script: str, *args: str) -> tuple[list[str], str]:
    """Exec argv for one of the sibling release scripts, plus a readable display form."""
    cmd = [sys.executable, str(_SCRIPT_DIR / script), *args]
    display = " ".join(["python3", f"scripts/{script}", *(shlex.quote(arg) for arg in args)])
    return cmd, display


def _git(*args: str) -> subprocess.CompletedProcess[str]:
    return subprocess.run(["git", *args], cwd=_REPO, capture_output=True, text=True, check=False)


def _read_manifest_version() -> str:
    raw = (_REPO / "KitLib.json").read_text(encoding="utf-8")
    return str(json.loads(raw)["version"])


def _resolve_version(cli_value: str) -> str:
    version = cli_value.strip()
    if version:
        return version
    version = _read_manifest_version()
    print(f"Version from KitLib.json: {version}")
    return version


def _tag_exists(version: str) -> bool:
    proc = _git("rev-parse", "--verify", "--quiet", f"refs/tags/v{version}")
    return proc.returncode == 0


def _changelog_has_section(path: Path, version: str) -> bool:
    if not path.is_file():
        return False
    header = re.compile(rf"^## \[{re.escape(version)}\]")
    return any(header.match(line) for line in path.read_text(encoding="utf-8").splitlines())


def _dotnet_status() -> tuple[str, str]:
    if shutil.which("dotnet") is None:
        return "warn", "not on PATH (needed to build assets)"
    proc = subprocess.run(["dotnet", "--list-sdks"], capture_output=True, text=True, check=False)
    versions = [line.split()[0] for line in (proc.stdout or "").splitlines() if line.split()]
    net9 = [version for version in versions if version.startswith("9.")]
    if net9:
        return "ok", f".NET {net9[-1]} SDK"
    found = ", ".join(versions) or "none"
    return "warn", f"no .NET 9 SDK on PATH (found: {found}) — release builds target net9.0"


def _gh_status() -> tuple[bool, str]:
    if shutil.which("gh") is None:
        return False, "GitHub CLI not found — https://cli.github.com/"
    try:
        proc = subprocess.run(
            ["gh", "auth", "status"],
            capture_output=True,
            text=True,
            timeout=20,
            check=False,
        )
    except (OSError, subprocess.TimeoutExpired) as ex:
        return False, f"gh auth status failed: {ex}"
    if proc.returncode == 0:
        return True, "installed and authenticated"
    lines = (proc.stderr or proc.stdout).strip().splitlines()
    reason = lines[0] if lines else f"exit {proc.returncode}"
    return False, f"not authenticated — {reason} (run: gh auth login)"


def _uploader_path() -> Path | None:
    raw = os.environ.get("STS2_MOD_UPLOADER", "").strip()
    if raw:
        path = Path(os.path.expandvars(raw)).expanduser()
        if path.is_dir():
            for name in (executable_name(), "ModUploader.exe", "ModUploader"):
                candidate = path / name
                if candidate.is_file():
                    return candidate
            return None
        return path if path.is_file() else None
    for candidate in candidate_uploaders(_REPO):
        if candidate.is_file():
            return candidate
    return None


# ---------------------------------------------------------------------------
# Readiness checks
# ---------------------------------------------------------------------------


def collect_checks(
    channels: tuple[str, ...],
    version: str,
    *,
    with_mcp: bool,
    rid: str,
) -> list[Check]:
    checks: list[Check] = [Check("ok", "version", f"KitLib.json = {version}")]

    branch = _git("rev-parse", "--abbrev-ref", "HEAD").stdout.strip() or "unknown"
    if _git("status", "--porcelain").stdout.strip():
        checks.append(Check("warn", "git tree", f"uncommitted changes on {branch} (they ship in the build)"))
    else:
        checks.append(Check("ok", "git tree", f"clean on {branch}"))

    if _tag_exists(version):
        checks.append(Check("ok", "tag", f"v{version} exists"))
    else:
        level = "fail" if "github" in channels else "warn"
        checks.append(Check(level, "tag", f"v{version} not found — tag the release before publishing"))

    for label, path in (
        ("changelog", _REPO / "CHANGELOG.md"),
        ("changelog zh", _REPO / "CHANGELOG.zh-CN.md"),
    ):
        if _changelog_has_section(path, version):
            checks.append(Check("ok", label, f"{path.name} has a [{version}] section"))
        else:
            checks.append(Check("warn", label, f"{path.name} has no [{version}] section — notes fall back"))

    missing = [path for path in github_release_assets(_REPO, version, rid, include_mcp=with_mcp) if not path.is_file()]
    if missing:
        names = ", ".join(path.name for path in missing)
        checks.append(Check("warn", "assets", f"missing {names} — run: release_cli.py build"))
    else:
        checks.append(Check("ok", "assets", "release assets present"))

    level, detail = _dotnet_status()
    checks.append(Check(level, "dotnet", detail))

    if "github" in channels:
        authenticated, detail = _gh_status()
        checks.append(Check("ok" if authenticated else "fail", "gh", detail))

    if "nexus" in channels:
        if os.environ.get("NEXUS_API_KEY", "").strip():
            checks.append(Check("ok", "nexus key", "NEXUS_API_KEY is set"))
        else:
            checks.append(Check("fail", "nexus key", "NEXUS_API_KEY not set (put it in .env)"))

    if "steam" in channels:
        uploader = _uploader_path()
        if uploader is not None:
            checks.append(Check("ok", "steam uploader", str(uploader)))
        else:
            expected = default_install_dir() / executable_name()
            checks.append(
                Check(
                    "fail",
                    "steam uploader",
                    f"ModUploader missing — run: python3 scripts/setup_steam_uploader.py --write-env (expected {expected})",
                )
            )

    return checks


def render_checks(checks: list[Check]) -> None:
    width = max(len(check.name) for check in checks)
    for check in checks:
        mark = LEVEL_MARKS[check.level]
        print(f"  [{mark}] {check.name:<{width}}  {check.detail}")


def summarize(checks: list[Check]) -> tuple[int, int]:
    fails = sum(1 for check in checks if check.level == "fail")
    warns = sum(1 for check in checks if check.level == "warn")
    return fails, warns


# ---------------------------------------------------------------------------
# Channel commands
# ---------------------------------------------------------------------------


def run_build(*, with_mcp: bool, rid: str, version: str, dry_run: bool) -> int:
    zip_path = mod_zip_path(_REPO, version)
    if dry_run:
        print(f"$ python3 scripts/package_bundle.py --zip-all -c Release  ->  {zip_path.name}")
        if with_mcp:
            print(f"$ dotnet publish tools/KitLib.Mcp/KitLib.Mcp.csproj -r {rid} (self-contained)")
            print("  (dry run: not executed)")
        else:
            print("  (dry run: not executed)")
        return 0
    try:
        build_mod_zip(_REPO, version)
        if with_mcp:
            build_mcp_zip(_REPO, version, rid)
    except ReleaseBuildError as ex:
        print(f"ERROR: {ex}", file=sys.stderr)
        return 1
    return 0


def run_github(version: str, *, dry_run: bool, with_mcp: bool, rid: str) -> int:
    args = ["--version", version]
    if with_mcp:
        args += ["--with-mcp", "--tools-rid", rid]
    cmd, display = _script_command("publish_release.py", *args)
    return _run(cmd, display=display, dry_run=dry_run)


def run_nexus(version: str, *, dry_run: bool, with_mcp: bool, rid: str) -> int:
    args = ["--version", version]
    if with_mcp:
        args += ["--mcp", "--tools-rid", rid]
    cmd, display = _script_command("publish_nexus.py", *args)
    return _run(cmd, display=display, dry_run=dry_run)


def run_steam(
    *,
    dry_run: bool,
    optional: bool,
    skip_build: bool,
    change_note: str,
    unreleased: bool,
    branch_targeting: bool,
    product: str,
) -> int:
    sync_args = ["sync", "--product", product]
    if skip_build:
        sync_args.append("--skip-build")
    if change_note:
        sync_args += ["--change-note", change_note]
    if unreleased:
        sync_args.append("--unreleased")
    if branch_targeting:
        sync_args.append("--branch-targeting")
    sync_cmd, sync_display = _script_command("publish_steam.py", *sync_args)
    rc = _run(sync_cmd, display=sync_display, dry_run=dry_run)
    if rc != 0:
        return rc

    upload_args = ["upload"]
    if optional:
        upload_args.append("--optional")
    if branch_targeting:
        upload_args.append("--branch-targeting")
    upload_cmd, upload_display = _script_command("publish_steam.py", *upload_args)
    return _run(upload_cmd, display=upload_display, dry_run=dry_run)


def _run(cmd: list[str], *, display: str, dry_run: bool) -> int:
    print(f"$ {display}", flush=True)
    if dry_run:
        print("  (dry run: not executed)")
        return 0
    return subprocess.run(cmd, cwd=_REPO, check=False).returncode


def run_channel(channel: str, args: argparse.Namespace, version: str, *, steam_optional: bool | None = None) -> int:
    rid = tools_rid(args.tools_rid)
    if channel == "github":
        return run_github(version, dry_run=args.dry_run, with_mcp=args.with_mcp, rid=rid)
    if channel == "nexus":
        return run_nexus(version, dry_run=args.dry_run, with_mcp=args.with_mcp, rid=rid)
    if channel == "steam":
        return run_steam(
            dry_run=args.dry_run,
            optional=args.optional if steam_optional is None else steam_optional,
            skip_build=args.skip_build,
            change_note=args.change_note,
            unreleased=args.unreleased,
            branch_targeting=args.branch_targeting,
            product=args.product,
        )
    raise SystemExit(f"Unknown channel: {channel}")


def run_all(args: argparse.Namespace, version: str) -> int:
    for channel in CHANNELS:
        print(f"\n── {CHANNEL_LABELS[channel]} ──")
        # `all` mirrors the old upload-all: a missing Steam uploader only warns.
        rc = run_channel(channel, args, version, steam_optional=True)
        if rc != 0:
            print(f"{CHANNEL_LABELS[channel]} failed (exit {rc}); stopping.", file=sys.stderr)
            return rc
    return 0


# ---------------------------------------------------------------------------
# Interactive menu
# ---------------------------------------------------------------------------


def _ask_yes_no(prompt: str, *, default: bool) -> bool:
    suffix = " [Y/n]: " if default else " [y/N]: "
    while True:
        raw = input(prompt + suffix).strip().lower()
        if not raw:
            return default
        if raw in {"y", "yes"}:
            return True
        if raw in {"n", "no"}:
            return False
        print("Please answer y or n.")


def _ask_channels() -> list[str]:
    keys = {"g": "github", "n": "nexus", "s": "steam"}
    while True:
        raw = input("Channels — [g]ithub [n]exus [s]team [a]ll (e.g. 'gns', empty = cancel): ")
        raw = re.sub(r"[\s,]+", "", raw.strip().lower())
        if not raw:
            return []
        if raw in {"a", "all"}:
            return list(CHANNELS)
        unknown = [char for char in raw if char not in keys]
        if unknown:
            print(f"Unknown selection {raw!r}; use g, n, s or a.")
            continue
        selected: list[str] = []
        for char in raw:
            channel = keys[char]
            if channel not in selected:
                selected.append(channel)
        return selected


def _equivalent_command(channel: str, version: str, args: argparse.Namespace) -> str:
    cmd = ["python3", "scripts/release_cli.py", channel, "--version", version]
    if args.dry_run:
        cmd.append("--dry-run")
    if args.with_mcp and channel in ("github", "nexus"):
        cmd.append("--with-mcp")
    return " ".join(cmd)


def interactive_menu(args: argparse.Namespace, version: str) -> int:
    rid = tools_rid(args.tools_rid)
    print(f"KitLib release helper — v{version}\n")

    try:
        while True:
            channels = _ask_channels()
            if not channels:
                print("Cancelled.")
                return 0

            checks = collect_checks(tuple(channels), version, with_mcp=args.with_mcp, rid=rid)
            render_checks(checks)
            blockers = [check for check in checks if check.level == "fail"]
            if not blockers:
                break

            print("\nBlocked for the selected channels (see FAIL lines above).")
            if not _ask_yes_no("Pick different channels?", default=False):
                return 1

        dry_run = _ask_yes_no("Dry run (print commands, run nothing)?", default=False)
        if dry_run:
            args.dry_run = True

        print(f"\nPlan: v{version} -> {', '.join(CHANNEL_LABELS[channel] for channel in channels)}")
        for channel in channels:
            print(f"  $ {_equivalent_command(channel, version, args)}")

        if not args.yes and not _ask_yes_no("Proceed?", default=False):
            print("Cancelled.")
            return 0
    except (EOFError, KeyboardInterrupt):
        print("\nCancelled.")
        return 130

    for channel in channels:
        print(f"\n── {CHANNEL_LABELS[channel]} ──")
        rc = run_channel(channel, args, version)
        if rc != 0:
            print(f"\n{CHANNEL_LABELS[channel]} failed (exit {rc}); stopping.", file=sys.stderr)
            return rc

    print("\nDone.")
    return 0


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------


def build_parser() -> argparse.ArgumentParser:
    ap = argparse.ArgumentParser(
        prog="release_cli.py",
        description="KitLib release helper: one entry point for build, GitHub, Nexus and Steam publishing.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=(
            "verbs:\n"
            "  status     readiness summary (never modifies anything)\n"
            "  preflight  same checks for --channels; exit 1 on blockers\n"
            "  build      build the release bundle and zip\n"
            "  github     publish the GitHub Release\n"
            "  nexus      publish to Nexus Mods\n"
            "  steam      stage + upload the Steam Workshop item\n"
            "  all        github, nexus, steam (Steam uploader missing only warns)\n"
            "\n"
            "no verb + TTY: interactive channel picker\n"
        ),
    )
    ap.add_argument("verb", nargs="?", choices=VERBS, help="action to run (omit for the interactive menu)")
    ap.add_argument("--version", default="", metavar="VERSION", help="release version (default: KitLib.json)")
    ap.add_argument(
        "--channels",
        default="github,nexus,steam",
        help="preflight only: comma-separated channels to check (default: all)",
    )
    ap.add_argument("--dry-run", action="store_true", help="print the commands without running them")
    ap.add_argument("--yes", "-y", action="store_true", help="menu only: skip the confirmation prompt")
    ap.add_argument("--with-mcp", action="store_true", help="legacy: also build/attach the KitLib.Mcp stdio proxy")
    ap.add_argument("--tools-rid", default="", help="runtime ID for the legacy MCP proxy")
    ap.add_argument("--product", default="KitLib", help="steam: product to stage (default: KitLib)")
    ap.add_argument("--skip-build", action="store_true", help="steam: reuse existing build artifacts")
    ap.add_argument("--change-note", default="", help="steam: override the Workshop change note")
    ap.add_argument("--unreleased", action="store_true", help="steam: use the ## [Unreleased] changelog section")
    ap.add_argument("--branch-targeting", action="store_true", help="steam: keep minBranch/maxBranch in workshop.json")
    ap.add_argument(
        "--optional",
        action="store_true",
        help="steam: warn instead of failing when ModUploader is missing (implied by 'all')",
    )
    return ap


def parse_channels(raw: str) -> tuple[str, ...]:
    aliases = {
        "g": "github",
        "gh": "github",
        "github": "github",
        "n": "nexus",
        "nexus": "nexus",
        "s": "steam",
        "steam": "steam",
    }
    channels: list[str] = []
    for part in re.split(r"[,\s]+", raw.strip().lower()):
        if not part:
            continue
        if part in {"a", "all"}:
            return CHANNELS
        channel = aliases.get(part)
        if channel is None:
            raise SystemExit(f"Unknown channel: {part} (choose github, nexus or steam)")
        if channel not in channels:
            channels.append(channel)
    if not channels:
        raise SystemExit("No channels selected")
    return tuple(channels)


def main(argv: list[str] | None = None) -> int:
    load_release_config(_REPO)
    parser = build_parser()
    args = parser.parse_args(argv)

    if args.verb is None and not (sys.stdin.isatty() and sys.stdout.isatty()):
        parser.print_help()
        return 2

    version = _resolve_version(args.version)
    rid = tools_rid(args.tools_rid)

    if args.verb is None:
        return interactive_menu(args, version)

    if args.verb == "status":
        print(f"KitLib release status — v{version}\n")
        checks = collect_checks(CHANNELS, version, with_mcp=args.with_mcp, rid=rid)
        render_checks(checks)
        fails, warns = summarize(checks)
        print(f"\n{fails} blocker(s), {warns} warning(s)")
        return 0

    if args.verb == "preflight":
        channels = parse_channels(args.channels)
        print(f"Preflight for v{version} — {', '.join(CHANNEL_LABELS[channel] for channel in channels)}\n")
        checks = collect_checks(channels, version, with_mcp=args.with_mcp, rid=rid)
        render_checks(checks)
        fails, warns = summarize(checks)
        print(f"\nPreflight: {fails} blocker(s), {warns} warning(s)")
        return 1 if fails else 0

    if args.verb == "build":
        return run_build(with_mcp=args.with_mcp, rid=rid, version=version, dry_run=args.dry_run)

    if args.verb == "all":
        return run_all(args, version)

    return run_channel(args.verb, args, version)


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except KeyboardInterrupt:
        print("\nInterrupted.", file=sys.stderr)
        raise SystemExit(130) from None
