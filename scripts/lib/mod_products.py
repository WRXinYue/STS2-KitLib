"""KitLib product catalog (game mod folder + owned satellite DLLs).

KitLib ships as a single published mod. The mod panel, in-game panel and dev tools
are built as satellite DLLs and staged under ``mods/KitLib/lib/<api>/modules/``.

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
        satellite_dlls=("KitModPanel", "KitLib.Panel", "KitLib.Dev"),
        manifest_path=_REPO / "KitLib.json",
        entry_dll="KitLib.dll",
        loader_csproj=None,
        dependency_ids=(),
    ),
}

PRODUCT_ORDER = ("KitLib",)

# Satellite assembly → owning product id
SATELLITE_TO_PRODUCT: dict[str, str] = {dll: product.id for product in PRODUCTS.values() for dll in product.satellite_dlls}

KITLIB_CORE_PROJECTS = [
    "src/KitLib/Abstractions/KitLib.Abstractions.csproj",
    "src/KitLib/Core/KitLib.Core.csproj",
    "src/KitLib/Loader/KitLib.Loader.csproj",
]

SATELLITE_PROJECTS = {
    "KitModPanel": "src/KitLib/Modules/ModPanel/KitModPanel.csproj",
    "KitLib.Panel": "src/KitLib/Modules/DevTools/KitLib.Panel.csproj",
    "KitLib.Dev": "src/KitLib/Modules/DevTools/KitLib.Dev.csproj",
}


def all_bundle_projects() -> list[str]:
    return bundle_projects_for("KitLib")


def bundle_projects_for(product_id: str | None) -> list[str]:
    if product_id not in (None, "KitLib"):
        raise ValueError(f"Unknown product: {product_id}")

    projects: list[str] = list(KITLIB_CORE_PROJECTS)
    product = PRODUCTS["KitLib"]
    if product.loader_csproj:
        projects.append(product.loader_csproj)
    for dll in product.satellite_dlls:
        projects.append(SATELLITE_PROJECTS[dll])
    return projects


def product_build_dir(product_id: str) -> Path:
    return _REPO / "build" / product_id
