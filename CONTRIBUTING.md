# Contributing to DevMode

### Collaboration

- Use **Conventional Commits** for PR titles and commit messages (`feat:`, `fix:`, `chore:`, `refactor:`, `docs:`, …).
- Keep changes **scoped** to the feature or fix; avoid drive-by reformatting or unrelated file churn.
- Before opening a PR: `dotnet build` on `KitLib.sln`, and run **`make format`** (or `dotnet format KitLib.sln`) so C# matches [`.editorconfig`](.editorconfig). If you touch `scripts/`, run **`flake8 scripts`** when flake8 is installed.
- Do **not** commit `.env`, `local.props`, or anything under `icons/` that comes from npm; they are local or generated.

### Code style (C#)

- **Braces — K&R (1TBS):** put the opening `{` on the **same line** as the declaration or keyword (`class`, `record`, `struct`, `namespace`, methods, `if`, `else`, `for`, `foreach`, `while`, `using`, `lock`, `switch`, lambdas whose body is a block, etc.). Closing `}` stays on its own line. Match surrounding files rather than introducing Allman-style braces on a new line.
- **Indentation:** **4 spaces** per `[*.cs]` in [`.editorconfig`](.editorconfig). Line endings **LF** (set at repo root).
- **Language level:** C# 12, nullable enabled, file-scoped namespaces — follow existing patterns (`partial`, `internal`, etc.).
- **Analyzers:** `dotnet build` runs Roslyn / CA rules from the SDK; fix or narrowly justify suppressions instead of broad `#pragma` disables.

### Python (`scripts/`)

- Formatter: **Black** ([`pyproject.toml`](pyproject.toml)). Lint: **flake8** ([`setup.cfg`](setup.cfg)). Prefer the standard library; keep scripts working with `python` / `python3` on `PATH`.

### Localization

- New user-visible strings: add keys to [`src/Localization/eng.json`](src/Localization/eng.json) and [`src/Localization/zhs.json`](src/Localization/zhs.json).
