"""Resolve STS2 beta ref roots and sts2.dll paths for compile checks."""

from __future__ import annotations

import hashlib
import json
import os
import shutil
import sys
from pathlib import Path

from lib.steam import _sts2_game_root_valid, read_sts2_dir_from_local_props, resolve_sts2_dir

ProfileName = str  # "stable" | "beta"

_PINNED_VERSIONS: dict[str, str] = {
    "stable": "0.107.1",
    "beta": "0.109.0",
}
PINNED_VERSION = _PINNED_VERSIONS["beta"]
DEFAULT_PROFILE: ProfileName = "beta"

VARIANT_TARGETS: list[tuple[str, ProfileName]] = [
    (_PINNED_VERSIONS["stable"], "stable"),
    (_PINNED_VERSIONS["beta"], "beta"),
]


def variant_profile(compat_target: str) -> ProfileName:
    normalized = compat_target.lstrip("vV").strip()
    for profile, pinned in _PINNED_VERSIONS.items():
        if normalized == pinned:
            return profile  # type: ignore[return-value]
    raise ValueError(f"Unknown personal compat target: {compat_target!r}")


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


def read_sts2_profile_env() -> ProfileName | None:
    value = os.environ.get("STS2_PROFILE", "").strip().lower()
    if value in _PINNED_VERSIONS:
        return value  # type: ignore[return-value]
    return None


def _file_sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as handle:
        for chunk in iter(lambda: handle.read(1 << 20), b""):
            digest.update(chunk)
    return digest.hexdigest()


def resolve_compile_profile(
    *,
    repo_root: Path | None = None,
    sts2_dir: Path | None = None,
    allow_game_inference: bool = True,
) -> ProfileName:
    env = read_sts2_profile_env()
    if env:
        return env
    _ = repo_root, sts2_dir, allow_game_inference
    return DEFAULT_PROFILE


def assert_capture_source_matches_profile(profile: ProfileName, source: Path) -> None:
    if profile not in _PINNED_VERSIONS:
        raise RuntimeError(f"Unknown profile {profile!r}.")
    version = read_release_version(source)
    pinned = pinned_version(profile)
    if not version:
        raise RuntimeError(
            f"No release_info.json under {source}. "
            "Use a full STS2 install root (with data_sts2_*/sts2.dll), not a source dump folder."
        )
    normalized = version.lstrip("vV").strip()
    if normalized != pinned:
        raise RuntimeError(
            f"Ref capture expects v{pinned}, but {source} reports {version!r}. "
            "Switch Steam to the public-beta branch or pass --source explicitly."
        )


def resolve_capture_source(
    profile: ProfileName,
    *,
    explicit: Path | None = None,
    repo_root: Path | None = None,
) -> Path:
    if profile not in _PINNED_VERSIONS:
        raise RuntimeError(f"Unknown profile {profile!r}.")
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


def pinned_version(profile: ProfileName = DEFAULT_PROFILE) -> str:
    if profile not in _PINNED_VERSIONS:
        raise ValueError(f"Unknown profile: {profile!r}")
    return _PINNED_VERSIONS[profile]


def refs_base(repo_root: Path) -> Path:
    return repo_root / "eng" / "sts2-refs"


def ref_root(repo_root: Path, profile: ProfileName = DEFAULT_PROFILE) -> Path:
    if profile not in _PINNED_VERSIONS:
        raise ValueError(f"Unknown profile: {profile!r}")
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


def resolve_profile_dir(profile: ProfileName = DEFAULT_PROFILE, *, repo_root: Path | None = None) -> Path:
    if profile not in _PINNED_VERSIONS:
        raise ValueError(f"Unknown profile: {profile!r}")

    root = repo_root or Path(__file__).resolve().parents[2]
    ref = ref_root(root, profile)
    if ref_is_valid(ref):
        return ref

    raise RuntimeError(
        f"No STS2 {profile} ref at {ref}. "
        f"Run: make capture-sts2-ref PROFILE={profile} "
        "(see eng/sts2-refs/README.md)."
    )


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
    profile: ProfileName = DEFAULT_PROFILE,
    *,
    repo_root: Path | None = None,
    source_root: Path | None = None,
) -> Path:
    root = repo_root or Path(__file__).resolve().parents[2]
    source = resolve_capture_source(profile, explicit=source_root, repo_root=root)

    dest_root = ref_root(root)
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


def format_profile_paths(repo_root: Path) -> dict[str, Path]:
    return {
        profile: resolve_sts2_dll(resolve_profile_dir(profile, repo_root=repo_root))
        for profile in _PINNED_VERSIONS
        if ref_is_valid(ref_root(repo_root, profile))
    }


def main(argv: list[str] | None = None) -> int:
    import argparse

    from lib.dotenv import load_dotenv

    ap = argparse.ArgumentParser(description="Print STS2 beta ref / install paths.")
    ap.add_argument(
        "--repo-root",
        type=Path,
        default=Path(__file__).resolve().parents[2],
    )
    ap.add_argument(
        "--profile",
        choices=tuple(_PINNED_VERSIONS),
        default=DEFAULT_PROFILE,
    )
    ap.add_argument("--game-root-only", action="store_true")
    args = ap.parse_args(argv)

    load_dotenv(args.repo_root / ".env")

    try:
        game_root = resolve_profile_dir(repo_root=args.repo_root)
        if args.game_root_only:
            print(game_root)
            return 0
        dll = resolve_sts2_dll(game_root)
        pinned = pinned_version(args.profile)
        print(f"{args.profile}\tpinned={pinned}")
        print(f"  root={game_root}")
        print(f"  dll={dll}")
    except RuntimeError as ex:
        print(str(ex), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
