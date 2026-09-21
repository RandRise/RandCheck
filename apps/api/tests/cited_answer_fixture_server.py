"""Explicit test-only server entry point. Never imported by production app startup."""
import json
from app.main import app
from app.cited_answer_routes import get_answer_adapter, get_prompt_counter
from app.cited_answers import AnswerProposalEnvelope
from cited_prompt_fixture import FixturePromptCounter


class FixtureAnswerAdapter:
    async def propose(self, request):
        evidence = json.loads(request.evidence_json)["evidence"][0]
        source_aware = request.prompt_version == "cited-answer-prompt-v4"
        proposal = {"schemaVersion": "1.1" if source_aware else "1.0", "status": "Answered", "reasonCode": None,
                    "claims": [{"text": "Synthetic fixture total is 100.00.", "citations": [
                        {"hitId": evidence["hitId"], "spanId": evidence["spanIds"][0]}]}],
                    "unansweredParts": []}
        if source_aware:
            proposal["claims"][0]["citations"][0]["supportText"] = evidence["content"]
        return AnswerProposalEnvelope(request.model, request.request_sha256,
                                      json.dumps(proposal).encode(), len(request.prompt_json), 50)


app.dependency_overrides[get_answer_adapter] = FixtureAnswerAdapter
app.dependency_overrides[get_prompt_counter] = FixturePromptCounter
