# Contributing to KitLib

### Collaboration

- Use **Conventional Commits** for PR titles and commit messages (`feat:`, `fix:`, `chore:`, `refactor:`, `docs:`, …).
- Keep changes **scoped** to the feature or fix; avoid drive-by reformatting or unrelated file churn.
- Run **`make init`** once per clone — generates `local.props`, `.vscode`, and installs pre-commit hooks via [uv](https://docs.astral.sh/uv/) (`make hooks-install` if hooks are missing).
- Before opening a PR: **`dotnet build KitLib.sln`** (or `make build-all`), then **`make check`** (`format-check` + `lint-scripts`). If you changed C# formatting, run **`make format`** first. With hooks installed, staged commits run the same checks automatically.
- **CI (GitHub):** push/PR to `main` runs [`.github/workflows/ci.yml`](.github/workflows/ci.yml) (`format-check`, `lint-scripts`). Full mod `dotnet build` is not run in CI (requires a local game `sts2.dll`).
- Do **not** commit `.env`, `local.props`, `build/`, or other generated artifacts listed in [`.gitignore`](.gitignore).

### Code style (C#)

- Formatting: [`.editorconfig`](.editorconfig) (K&R braces, file-scoped namespaces, 4-space indent). Run **`make format`** before PRs; CI runs **`format-check`**.
- Match patterns in `src/` and `tools/` (C# 12, nullable). Fix analyzer warnings; no blanket `#pragma` disables.

### Python (`scripts/`)

- Utility scripts should keep working with system **`python` / `python3`** on `PATH`; prefer the standard library.
- Editor formatting: **Black** (see [`.vscode/settings.json`](.vscode/settings.json)). Optional lint: **`flake8 scripts`** when flake8 is installed ([`setup.cfg`](setup.cfg)).
- Repo dev tooling (pre-commit) is managed in [`pyproject.toml`](pyproject.toml) with **`uv sync`**.

### Localization

- New user-visible strings: add keys to [`src/KitLib.Core/Localization/eng.json`](src/KitLib.Core/Localization/eng.json) and [`src/KitLib.Core/Localization/zhs.json`](src/KitLib.Core/Localization/zhs.json).
