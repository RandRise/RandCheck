import os
from dataclasses import dataclass
from functools import lru_cache


@dataclass(frozen=True)
class Settings:
    app_env: str
    extraction_provider: str
    storage_shared_root: str
    enable_legacy_extract: bool
    worker_shared_secret: str

@lru_cache
def get_settings() -> Settings:
    return Settings(
        app_env=os.getenv("APP_ENV", "development"),
        extraction_provider=os.getenv("EXTRACTION_PROVIDER", "mock"),
        storage_shared_root=os.getenv("STORAGE_SHARED_ROOT", "/data/uploads"),
        enable_legacy_extract=os.getenv("ENABLE_LEGACY_EXTRACT", "false").lower()
        == "true",
        worker_shared_secret=os.getenv("WORKER_SHARED_SECRET", ""),
    )
