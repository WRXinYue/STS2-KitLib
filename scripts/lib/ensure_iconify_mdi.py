"""Download @iconify-json/mdi into icons/mdi/icons.json if missing (no npm)."""

from __future__ import annotations

import tarfile
import tempfile
import urllib.request
from pathlib import Path

_USER_AGENT = "KitLib-ensure-iconify-mdi/1"

PACKAGE_VERSION = "1.2.3"


def ensure_mdi_icons(repo_root: Path, package_version: str = PACKAGE_VERSION) -> None:
    icons_dir = repo_root / "icons" / "mdi"
    icons_json = icons_dir / "icons.json"
    if icons_json.is_file():
        return

    url = f"https://registry.npmjs.org/@iconify-json/mdi/-/mdi-{package_version}.tgz"
    print(f"Ensure-IconifyMdi: downloading @iconify-json/mdi@{package_version} (first run)...")
    icons_dir.mkdir(parents=True, exist_ok=True)

    with tempfile.TemporaryDirectory(prefix="iconify-mdi-") as tmp:
        tgz = Path(tmp) / f"mdi-{package_version}.tgz"
        req = urllib.request.Request(url, headers={"User-Agent": _USER_AGENT})
        with urllib.request.urlopen(req, timeout=120) as resp:  # noqa: S310 — fixed npm registry URL
            tgz.write_bytes(resp.read())

        with tarfile.open(tgz, "r:gz") as tf:
            try:
                ic = tf.extractfile("package/icons.json")
            except KeyError as e:
                raise RuntimeError("package/icons.json missing in npm tarball") from e
            if ic is None:
                raise RuntimeError("package/icons.json could not be read from tarball")
            icons_json.write_bytes(ic.read())

            try:
                info = tf.extractfile("package/info.json")
                if info:
                    (icons_dir / "info.json").write_bytes(info.read())
            except KeyError:
                pass

    if not icons_json.is_file():
        raise RuntimeError(f"Failed to create icons.json at {icons_json}")
    print(f"Ensure-IconifyMdi: wrote {icons_json}")
