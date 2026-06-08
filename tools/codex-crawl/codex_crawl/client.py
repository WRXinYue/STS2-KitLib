"""HTTP client for the Spire Codex public API."""

from __future__ import annotations

import time
from typing import Any

import httpx

DEFAULT_BASE_URL = "https://spire-codex.com"
DEFAULT_RPM = 55


class CodexClient:
    def __init__(self, base_url: str = DEFAULT_BASE_URL, rpm: float = DEFAULT_RPM) -> None:
        self.base_url = base_url.rstrip("/")
        self._min_interval = 60.0 / rpm if rpm > 0 else 0.0
        self._last_request_at = 0.0
        self._client = httpx.Client(
            base_url=self.base_url,
            headers={
                "Accept": "application/json",
                "User-Agent": "KitLib-codex-crawl/0.1 (+https://github.com/STS2-DevMode)",
            },
            timeout=120.0,
        )

    def close(self) -> None:
        self._client.close()

    def __enter__(self) -> CodexClient:
        return self

    def __exit__(self, *args: object) -> None:
        self.close()

    def get_json(self, path: str, params: dict[str, Any] | None = None) -> Any:
        self._throttle()
        query = {k: v for k, v in (params or {}).items() if v is not None}
        response = self._client.get(path, params=query)
        try:
            response.raise_for_status()
        except httpx.HTTPStatusError as exc:
            body = exc.response.text[:500]
            raise RuntimeError(f"HTTP {exc.response.status_code} for {path}: {body}") from exc
        return response.json()

    def _throttle(self) -> None:
        if self._min_interval <= 0:
            return
        now = time.monotonic()
        wait = self._min_interval - (now - self._last_request_at)
        if wait > 0:
            time.sleep(wait)
        self._last_request_at = time.monotonic()
