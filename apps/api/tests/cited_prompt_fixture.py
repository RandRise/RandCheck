"""Synthetic counting units only: NOT GPT tokens and never production billing."""
class FixturePromptCounter:
    version = "test-utf8-bytes-v1"

    async def count(self, request):
        return len(request.prompt_json)
