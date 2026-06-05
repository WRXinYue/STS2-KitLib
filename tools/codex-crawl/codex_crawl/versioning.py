from __future__ import annotations

import re

_BUILD_RE = re.compile(r"^v(?P<major>\d+)\.(?P<minor>\d+)\.(?P<patch>\d+)$")


def parse_build_id(build_id: str | None) -> tuple[int, int, int] | None:
    if not build_id:
        return None
    match = _BUILD_RE.match(build_id.strip())
    if not match:
        return None
    return int(match["major"]), int(match["minor"]), int(match["patch"])


def build_id_at_least(build_id: str | None, minimum: str | None) -> bool:
    if not minimum:
        return True
    left = parse_build_id(build_id)
    right = parse_build_id(minimum)
    if left is None or right is None:
        return True
    return left >= right
