"""KitLib multi-product catalog (game mod folders + owned satellite DLLs).

Keep in sync with KitLib.Abstractions Host.KitLibProductIds.
"""

from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path

_REPO = Path(__file__).resolve().parents[2]

MODULES_SUBDIR = "modules"


@dataclass(frozen=True)
class ModProduct:
    id: str
    """Game mods/ folder id and manifest id."""

    display_name: str
    satellite_dlls: tuple[str, ...]
    """Assembly names staged under modules/ (without .dll)."""

    manifest_path: Path
    """Source JSON copied to mod_manifest.json on deploy."""

    entry_dll: str
    """Primary has_dll entry assembly file name."""

    loader_csproj: str | None
    """Thin product loader csproj relative to repo root; None for KitLib (uses src loader)."""

    dependency_ids: tuple[str, ...]
    """mod_manifest dependency ids (informational; JSON is source of truth)."""


PRODUCTS: dict[str, ModProduct] = {
    "KitLib": ModProduct(
        id="KitLib",
        display_name="KitLib",
        satellite_dlls=(),
        manifest_path=_REPO / "KitLib.json",
        entry_dll="KitLib.dll",
        loader_csproj=None,
        dependency_ids=(),
    ),
    "KitModPanel": ModProduct(
        id="KitModPanel",
        display_name="KitLib Mod Panel",
        satellite_dlls=(),
        manifest_path=_REPO / "mods" / "KitModPanel" / "KitModPanel.json",
        entry_dll="KitModPanel.dll",
        loader_csproj="mods/KitModPanel/KitModPanel.csproj",
        dependency_ids=("KitLib",),
    ),
    "KitDevTools": ModProduct(
        id="KitDevTools",
        display_name="KitLib Dev Tools",
        satellite_dlls=("KitLib.Panel", "KitLib.Dev"),
        manifest_path=_REPO / "mods" / "KitDevTools" / "KitDevTools.json",
        entry_dll="KitDevTools.dll",
        loader_csproj="mods/KitDevTools/KitDevTools.csproj",
        dependency_ids=("KitLib",),
    ),
    "KitAI": ModProduct(
        id="KitAI",
        display_name="KitLib AI",
        satellite_dlls=("KitLib.AI",),
        manifest_path=_REPO / "mods" / "KitAI" / "KitAI.json",
        entry_dll="KitAI.dll",
        loader_csproj="mods/KitAI/KitAI.csproj",
        dependency_ids=("KitLib",),
    ),
}

PRODUCT_ORDER = ("KitLib", "KitModPanel", "KitDevTools", "KitAI")

# Satellite assembly → owning product id
SATELLITE_TO_PRODUCT: dict[str, str] = {dll: product.id for product in PRODUCTS.values() for dll in product.satellite_dlls}

KITLIB_CORE_PROJECTS = [
    "src/KitLib.Abstractions/KitLib.Abstractions.csproj",
    "src/KitLib.Core/KitLib.Core.csproj",
    "src/KitLib.Loader/KitLib.Loader.csproj",
]

SATELLITE_PROJECTS = {
    "KitLib.Panel": "mods/KitDevTools/KitLib.Panel.csproj",
    "KitLib.Dev": "mods/KitDevTools/KitLib.Dev.csproj",
    "KitLib.AI": "mods/KitAI/KitLib.AI.csproj",
}


def all_bundle_projects() -> list[str]:
    projects = list(KITLIB_CORE_PROJECTS)
    for product_id in PRODUCT_ORDER:
        product = PRODUCTS[product_id]
        if product.loader_csproj:
            projects.append(product.loader_csproj)
        for dll in product.satellite_dlls:
            projects.append(SATELLITE_PROJECTS[dll])
    return projects


def product_build_dir(product_id: str) -> Path:
    return _REPO / "build" / product_id
