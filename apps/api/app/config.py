import os
from dataclasses import dataclass
from functools import lru_cache


@dataclass(frozen=True)
class Settings:
    app_env: str
    extraction_provider: str

@lru_cache
def get_settings() -> Settings:
    return Settings(
        app_env=os.getenv("APP_ENV", "development"),
        extraction_provider=os.getenv("EXTRACTION_PROVIDER", "mock"),
    )
