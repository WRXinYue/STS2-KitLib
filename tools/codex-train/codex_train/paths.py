from pathlib import Path

TOOL_DIR = Path(__file__).resolve().parent.parent
REPO_ROOT = TOOL_DIR.parent.parent
DEFAULT_PARQUET = REPO_ROOT / "tools" / "codex-crawl" / "data" / "macro_samples.parquet"
DEFAULT_SCORES_DIR = REPO_ROOT / "tools" / "codex-crawl" / "data" / "scores"
DEFAULT_PRIORS_DIR = REPO_ROOT / "tools" / "codex-crawl" / "data" / "priors"
DEFAULT_PRIORS_JSON = DEFAULT_PRIORS_DIR / "codex-priors.json"
MOD_PRIORS_JSON = REPO_ROOT / "src" / "AI" / "Data" / "codex-priors.json"
