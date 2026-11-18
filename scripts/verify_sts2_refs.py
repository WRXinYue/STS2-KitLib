#!/usr/bin/env python3
"""Verify pinned STS2 identities, managed DLLs, and configuration consistency."""

from __future__ import annotations

import argparse
import json
import re
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

from lib.sts2_profiles import _PINNED_VERSIONS, _REF_FILES, _file_sha256, normalize_release_version, ref_root


def verify_refs(root: Path) -> None:
    props = ET.parse(root / "eng/Sts2Refs.props").getroot()
    bundle_props = ET.parse(root / "eng/KitLib.PersonalCompat.props").getroot()
    bundle_targets = bundle_props.findtext(".//KitLibPersonalCompatTargets", "").split(";")
    if bundle_targets != list(_PINNED_VERSIONS.values()):
        raise ValueError("Bundle compat targets differ from the pinned versions")
    runtime_map = (root / "src/KitLib/Abstractions/Compat/Sts2ProfileMap.cs").read_text()
    if f'PinnedGameVersion = "{_PINNED_VERSIONS["beta"]}"' not in runtime_map:
        raise ValueError("Runtime profile pin differs from the compile pin")
    manifest_text = (root / "eng/api_touchpoints.yaml").read_text()
    profiles_section = manifest_text.split("touchpoints:", 1)[0]
    manifest_profiles = dict(re.findall(r'^  (\w+): "([^"]+)"$', profiles_section, re.MULTILINE))
    if manifest_profiles != _PINNED_VERSIONS:
        raise ValueError("API manifest pins are stale. Run make extract-touchpoints.")
    for profile, version in _PINNED_VERSIONS.items():
        prop = props.find(f".//KitLibSts2Refs{profile.title()}Version")
        if prop is None or prop.text != version:
            raise ValueError(f"MSBuild pin for {profile} differs from {version}")
        snapshot = ref_root(root, profile)
        release = json.loads((snapshot / "release_info.json").read_text())
        metadata = json.loads((snapshot / "ref-manifest.json").read_text())
        if normalize_release_version(release["version"]) != version:
            raise ValueError(f"Release version mismatch in {snapshot}")
        if metadata["profile"] != profile or metadata["version"] != version or metadata["commit"] != release["commit"]:
            raise ValueError(f"Snapshot identity mismatch in {snapshot}")
        data_dir = snapshot / "data_sts2_windows_x86_64"
        for name in _REF_FILES:
            path = data_dir / name
            with path.open("rb") as handle:
                if handle.read(2) != b"MZ":
                    raise ValueError(f"{path} is not a PE assembly. Check git lfs pull.")
            if metadata["files"].get(name) != _file_sha256(path):
                raise ValueError(f"Checksum mismatch for {path}")
        print(f"Verified {profile} v{version} ({release['commit']}), {len(_REF_FILES)} assemblies")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo-root", type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args()
    try:
        verify_refs(args.repo_root)
    except (OSError, ValueError, KeyError, ET.ParseError) as exc:
        print(f"STS2 reference verification failed: {exc}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
