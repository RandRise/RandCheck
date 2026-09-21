"""Fail-closed prompt counter boundary; test counters are not provider billing."""
import asyncio
from typing import Protocol
from .cited_answers import AnswerBoundaryError, PreparedAnswerRequest, MAX_INPUT_TOKENS


class PromptCounter(Protocol):
    version: str
    async def count(self, request: PreparedAnswerRequest) -> int: ...


class DisabledPromptCounter:
    version = "disabled"
    async def count(self, request: PreparedAnswerRequest) -> int:
        raise AnswerBoundaryError("prompt_counter_unavailable")


async def measure_prompt(request: PreparedAnswerRequest, counter: PromptCounter):
    # No live counter installed or configurable. Endpoint-specific counting (and
    # its privacy/cost behavior) needs separate readiness before lifting this gate.
    if request.model.provider_alias != "test-only":
        raise AnswerBoundaryError("prompt_counter_unavailable")
    try:
        if (not isinstance(counter.version, str) or not counter.version.startswith("test-")
                or len(counter.version) > 100 or not counter.version.isascii()
                or any(not (c.isalnum() or c in "-_.") for c in counter.version)):
            raise ValueError()
        async with asyncio.timeout(10):
            count = await counter.count(request)
        if type(count) is not int or not 1 <= count <= MAX_INPUT_TOKENS:
            raise ValueError()
        return count
    except asyncio.CancelledError:
        raise
    except Exception:
        raise AnswerBoundaryError("prompt_measurement_rejected") from None
