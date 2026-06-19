"""Resolve STS2 stable/beta ref roots and sts2.dll paths for dual-profile checks."""

from __future__ import annotations

import hashlib
import json
import os
import re
import shutil
import sys
from pathlib import Path

from lib.steam import _sts2_game_root_valid, read_sts2_dir_from_local_props, resolve_sts2_dir

ProfileName = str  # "stable" | "beta"

# Public (stable) and Steam beta currently ship the same v0.107.1 API; pins may diverge later.
_PINNED_VERSIONS: dict[str, str] = {
    "stable": "0.107.1",
    "beta": "0.107.1",
}

_REF_FILES = ("sts2.dll", "sts2.dylib", "0Harmony.dll")


def _read_release_version_file(path: Path) -> str | None:
    try:
        data = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, json.JSONDecodeError):
        return None
    version = data.get("version")
    if not isinstance(version, str) or not version.strip():
        return None
    return version.strip()


def read_release_version(sts2_dir: Path) -> str | None:
    root_file = sts2_dir / "release_info.json"
    if root_file.is_file():
        version = _read_release_version_file(root_file)
        if version:
            return version

    for data_dir in list_ref_data_dirs(sts2_dir):
        candidate = data_dir / "release_info.json"
        if not candidate.is_file():
            continue
        version = _read_release_version_file(candidate)
        if version:
            return version

    return None


def compile_profile_from_game_version(raw: str | None) -> ProfileName | None:
    if not raw:
        return None
    normalized = raw.lstrip("vV").strip()
    match = re.match(r"(\d+)\.(\d+)", normalized)
    if not match:
        return None
    major = int(match.group(1))
    minor = int(match.group(2))
    if major == 0 and minor >= 106:
        # Same install path/version for public + beta today; default to stable (public).
        return "stable"
    return None


def read_local_props_profile(repo_root: Path) -> ProfileName | None:
    path = repo_root / "local.props"
    if not path.is_file():
        return None
    text = path.read_text(encoding="utf-8", errors="replace")
    match = re.search(r"<Sts2Profile>([^<]+)</Sts2Profile>", text)
    if not match:
        return None
    value = match.group(1).strip().lower()
    return value if value in ("stable", "beta") else None


def read_sts2_profile_env() -> ProfileName | None:
    value = os.environ.get("STS2_PROFILE", "").strip().lower()
    return value if value in ("stable", "beta") else None


def _file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def infer_profile_from_ref_hash(game_root: Path, *, repo_root: Path) -> ProfileName | None:
    try:
        install_dll = resolve_sts2_dll(game_root)
        install_hash = _file_sha256(install_dll)
    except OSError:
        return None

    matched: ProfileName | None = None
    for profile in _PINNED_VERSIONS:
        ref = ref_root(repo_root, profile)
        if not ref_is_valid(ref):
            continue
        try:
            ref_dll = resolve_sts2_dll(ref)
            if _file_sha256(ref_dll) == install_hash:
                matched = profile
                if profile == "beta":
                    return profile
        except OSError:
            continue

    return matched


def resolve_compile_profile(
    *,
    repo_root: Path | None = None,
    sts2_dir: Path | None = None,
    allow_game_inference: bool = True,
) -> ProfileName:
    """Resolve MSBuild Sts2Profile for compile (stable|beta ref channel).

    Precedence: STS2_PROFILE env, then game install (release_info / ref hash),
    then local.props Sts2Profile, then stable.

    When Sts2Dir points at a live install, the game branch wins over a stale
    local.props Sts2Profile (e.g. after switching Steam from beta to release).
    """
    root = repo_root or Path(__file__).resolve().parents[2]

    env_profile = read_sts2_profile_env()
    if env_profile:
        return env_profile

    game_inferred: ProfileName | None = None
    if allow_game_inference:
        game_root = sts2_dir or read_sts2_dir_from_local_props(root) or resolve_sts2_dir()
        if game_root is not None:
            game_inferred = compile_profile_from_game_version(read_release_version(game_root))
            if not game_inferred:
                game_inferred = infer_profile_from_ref_hash(game_root, repo_root=root)

    props_profile = read_local_props_profile(root)
    if game_inferred:
        if props_profile and props_profile != game_inferred:
            print(
                f"Note: local.props Sts2Profile={props_profile} but STS2 install looks like "
                f"{game_inferred} — compiling for {game_inferred}. Run make init to refresh local.props.",
                file=sys.stderr,
            )
        return game_inferred

    if props_profile:
        return props_profile

    return "stable"


def assert_capture_source_matches_profile(profile: ProfileName, source: Path) -> None:
    version = read_release_version(source)
    pinned = pinned_version(profile)
    if not version:
        raise RuntimeError(f"No release_info.json under {source}. " "Use a full STS2 install root (with data_sts2_*/sts2.dll), not a source dump folder.")
    normalized = version.lstrip("vV").strip()
    if normalized != pinned:
        inferred = compile_profile_from_game_version(version) or "unknown"
        raise RuntimeError(
            f"Ref capture profile mismatch: PROFILE={profile} expects v{pinned}, "
            f"but {source} reports {version!r} (looks like {inferred}). "
            "Switch Steam branch (same Sts2Dir) or pass --source explicitly."
        )


def resolve_capture_source(
    profile: ProfileName,
    *,
    explicit: Path | None = None,
    repo_root: Path | None = None,
) -> Path:
    if explicit is not None:
        source = Path(os.path.expandvars(str(explicit))).expanduser().resolve()
        if not ref_is_valid(source):
            raise RuntimeError(f"--source is not a valid STS2 install: {source}")
        assert_capture_source_matches_profile(profile, source)
        return source

    root = repo_root or Path(__file__).resolve().parents[2]
    fallback = read_sts2_dir_from_local_props(root) or resolve_sts2_dir()
    if fallback is None:
        raise RuntimeError("STS2 install not found. Run make init (local.props Sts2Dir) or pass --source.")
    assert_capture_source_matches_profile(profile, fallback)
    return fallback


def pinned_version(profile: ProfileName) -> str:
    return _PINNED_VERSIONS[profile]


def refs_base(repo_root: Path) -> Path:
    return repo_root / "eng" / "sts2-refs"


def ref_root(repo_root: Path, profile: ProfileName) -> Path:
    return refs_base(repo_root) / profile / pinned_version(profile)


def list_ref_data_dirs(game_root: Path) -> list[Path]:
    dirs: list[Path] = []
    try:
        for child in sorted(game_root.iterdir()):
            if child.is_dir() and child.name.startswith("data_sts2_"):
                dirs.append(child)
    except OSError:
        return []
    return dirs


def ref_is_valid(game_root: Path) -> bool:
    if _sts2_game_root_valid(game_root):
        return True
    for data_dir in list_ref_data_dirs(game_root):
        if any((data_dir / name).is_file() for name in _REF_FILES[:2]):
            return True
    return False


def resolve_profile_dir(profile: ProfileName, *, repo_root: Path | None = None) -> Path:
    if profile not in _PINNED_VERSIONS:
        raise ValueError(f"Unknown profile: {profile!r}")

    root = repo_root or Path(__file__).resolve().parents[2]
    ref = ref_root(root, profile)
    if ref_is_valid(ref):
        return ref

    raise RuntimeError(f"No STS2 {profile} ref at {ref}. " f"Run: make capture-sts2-ref PROFILE={profile} " "(with Steam on the matching branch; see eng/sts2-refs/README.md).")


def resolve_sts2_dll(game_root: Path) -> Path:
    mac_dylib = game_root / "SlayTheSpire2.app" / "Contents" / "Resources" / "data_sts2_macos_arm64" / "sts2.dylib"
    if mac_dylib.is_file():
        return mac_dylib

    mac_dylib_x64 = game_root / "SlayTheSpire2.app" / "Contents" / "Resources" / "data_sts2_macos_x86_64" / "sts2.dylib"
    if mac_dylib_x64.is_file():
        return mac_dylib_x64

    candidates: list[Path] = []
    for data_dir in list_ref_data_dirs(game_root):
        for name in ("sts2.dll", "sts2.dylib"):
            dll = data_dir / name
            if dll.is_file():
                candidates.append(dll)

    if not candidates:
        raise RuntimeError(f"No sts2.dll/sts2.dylib under {game_root}")

    candidates.sort(key=lambda p: (0 if "windows_x86_64" in p.as_posix() else 1, p.name))
    return candidates[0]


def capture_profile_ref(
    profile: ProfileName,
    *,
    repo_root: Path | None = None,
    source_root: Path | None = None,
) -> Path:
    root = repo_root or Path(__file__).resolve().parents[2]
    source = resolve_capture_source(profile, explicit=source_root, repo_root=root)

    dest_root = ref_root(root, profile)
    copied = 0
    for data_dir in list_ref_data_dirs(source):
        rel = data_dir.name
        dest_dir = dest_root / rel
        dest_dir.mkdir(parents=True, exist_ok=True)
        for name in _REF_FILES:
            src_file = data_dir / name
            if not src_file.is_file():
                continue
            shutil.copy2(src_file, dest_dir / name)
            copied += 1

    if copied == 0:
        raise RuntimeError(f"No ref DLLs copied from {source}")

    return dest_root


def read_game_version(dll_path: Path) -> str | None:
    _ = dll_path
    return None


def format_profile_paths(repo_root: Path) -> dict[str, Path]:
    return {profile: resolve_sts2_dll(resolve_profile_dir(profile, repo_root=repo_root)) for profile in ("stable", "beta")}


def main(argv: list[str] | None = None) -> int:
    import argparse

    from lib.dotenv import load_dotenv

    ap = argparse.ArgumentParser(description="Print STS2 profile ref / install paths.")
    ap.add_argument(
        "--repo-root",
        type=Path,
        default=Path(__file__).resolve().parents[2],
    )
    ap.add_argument(
        "--profile",
        choices=("stable", "beta", "all"),
        default="all",
    )
    ap.add_argument("--game-root-only", action="store_true")
    args = ap.parse_args(argv)

    load_dotenv(args.repo_root / ".env")
    profiles = ("stable", "beta") if args.profile == "all" else (args.profile,)

    try:
        for profile in profiles:
            game_root = resolve_profile_dir(profile, repo_root=args.repo_root)
            if args.game_root_only:
                print(game_root)
                continue
            dll = resolve_sts2_dll(game_root)
            pinned = pinned_version(profile)
            print(f"{profile}\tpinned={pinned}")
            print(f"  root={game_root}")
            print(f"  dll={dll}")
    except RuntimeError as ex:
        print(str(ex), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
