import os
from dataclasses import dataclass, field
from functools import lru_cache


@dataclass(frozen=True)
class Settings:
    app_env: str
    extraction_provider: str
    storage_shared_root: str
    enable_legacy_extract: bool
    worker_shared_secret: str = field(repr=False)
    service_auth_mode: str = ""
    service_keys_json: str = field(default="[]", repr=False)

@lru_cache
def get_settings() -> Settings:
    return Settings(
        app_env=os.getenv("APP_ENV", "development"),
        extraction_provider=os.getenv("EXTRACTION_PROVIDER", "mock"),
        storage_shared_root=os.getenv("STORAGE_SHARED_ROOT", "/data/uploads"),
        enable_legacy_extract=os.getenv("ENABLE_LEGACY_EXTRACT", "false").lower()
        == "true",
        worker_shared_secret=os.getenv("WORKER_SHARED_SECRET", ""),
        service_auth_mode=os.getenv("SERVICE_AUTH_MODE", ""),
        service_keys_json=os.getenv("SERVICE_KEYS_JSON", "[]"),
    )
