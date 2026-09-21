"""Versioned, provider-free lexical planning. SQL and corpus authorization stay in ASP.NET."""
import re
from .cited_chunking import ChunkingError, content_hash

POLICY_VERSION = "lexical-or-english-v1"


def lexical_plan(question: str, top_k: int) -> dict:
    if not isinstance(question, str) or not question.strip() or len(question) > 4000:
        raise ChunkingError("Invalid question")
    if type(top_k) is not int or not 1 <= top_k <= 20:
        raise ChunkingError("Invalid hit limit")
    # Literal terms only, not SQL or user-supplied tsquery operators. PostgreSQL
    # applies its English dictionary/stop words. No corpus-specific synonyms.
    terms = list(dict.fromkeys(re.findall(r"[A-Za-z0-9]+(?:[-./][A-Za-z0-9]+)*", question.lower())))
    if len(terms) > 64 or any(len(term) > 100 for term in terms):
        raise ChunkingError("Question exceeds lexical term limits")
    return {"schemaVersion": "1.0", "policyVersion": POLICY_VERSION,
            "questionSha256": content_hash(question), "configuration": "english",
            "searchText": " OR ".join('"' + term + '"' for term in terms), "topK": top_k}
