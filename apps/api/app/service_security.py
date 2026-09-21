"""Private service credentials authorize operations, never human membership.

Bearer credentials require TLS. Configuration stores SHA-256 verifiers for
random 256-bit tokens; this is not a password authentication mechanism.
"""
from dataclasses import dataclass
from datetime import datetime, timezone, timedelta
import hashlib
import hmac
import json
import logging
import re
from uuid import uuid4

from fastapi import HTTPException
from starlette.responses import JSONResponse

OPERATIONS = {
    "/internal/extractions": "extraction",
    "/internal/cited/chunks": "chunking",
    "/internal/cited/lexical-plan": "retrieval",
    "/internal/cited/answer-prepare": "answer_prepare",
    "/internal/cited/answer-proposal": "answer_propose",
    "/internal/cited/embedding-prepare": "embedding_prepare",
    "/internal/cited/embedding-proposal": "embedding_propose",
}
LOG = logging.getLogger("document_intelligence.service_security")
IDENTIFIER = re.compile(r"[A-Za-z0-9_-]{1,64}")
TOKEN = re.compile(r"[A-Za-z0-9_-]{43,128}")


@dataclass(frozen=True)
class ServicePrincipal:
    service_id: str
    key_id: str
    operation: str


def mode(settings):
    environment = getattr(settings, "app_env", "development").lower()
    configured = getattr(settings, "service_auth_mode", "")
    selected = configured or ("Development" if environment in ("development", "testing") else "Locked")
    if selected == "Development" and environment not in ("development", "testing"):
        return "Locked"
    return selected if selected in ("Development", "ServiceKeys", "Locked") else "Locked"


def _unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError()
        result[key] = value
    return result


def _utc(value):
    if not isinstance(value, str):
        raise ValueError()
    parsed = datetime.fromisoformat(value.replace("Z", "+00:00"))
    if parsed.tzinfo is None or parsed.utcoffset() != timedelta(0):
        raise ValueError()
    return parsed


def _keys(settings):
    raw = settings.service_keys_json
    if not isinstance(raw, str) or len(raw) > 65536:
        raise ValueError()
    keys = json.loads(raw, object_pairs_hook=_unique)
    if not isinstance(keys, list) or not 1 <= len(keys) <= 16:
        raise ValueError()
    ids, hashes = set(), set()
    for key in keys:
        if not isinstance(key, dict) or set(key) != {"keyId", "serviceId", "tokenSha256", "permissions", "notBeforeUtc", "expiresAtUtc"}:
            raise ValueError()
        if any(not isinstance(key[x], str) or not IDENTIFIER.fullmatch(key[x]) for x in ("keyId", "serviceId")):
            raise ValueError()
        if not isinstance(key["tokenSha256"], str) or not re.fullmatch(r"[0-9a-f]{64}", key["tokenSha256"]):
            raise ValueError()
        permissions = key["permissions"]
        if not isinstance(permissions, list) or not permissions or any(not isinstance(p, str) or p not in OPERATIONS.values() for p in permissions):
            raise ValueError()
        if len(set(permissions)) != len(permissions) or key["keyId"] in ids or key["tokenSha256"] in hashes:
            raise ValueError()
        ids.add(key["keyId"])
        hashes.add(key["tokenSha256"])
        start, end = _utc(key["notBeforeUtc"]), _utc(key["expiresAtUtc"])
        if not timedelta(0) < end - start <= timedelta(days=90):
            raise ValueError()
    return keys


def authenticate(scope, settings):
    operation = OPERATIONS.get(scope.get("path", ""))
    if not operation or scope.get("method") != "POST" or scope.get("query_string"):
        return None
    headers = scope.get("headers", [])
    values = lambda name: [v for k, v in headers if k.lower() == name]
    tokens, ids = values(b"x-worker-token"), values(b"x-service-key-id")
    if len(tokens) != 1 or len(ids) > 1:
        return None
    selected = mode(settings)
    if selected == "Development":
        secret = getattr(settings, "worker_shared_secret", "")
        if secret and not ids and hmac.compare_digest(tokens[0], secret.encode("utf-8")):
            return ServicePrincipal("local-development", "legacy", operation)
        return None
    if selected != "ServiceKeys" or scope.get("scheme") != "https" or len(ids) != 1:
        return None
    try:
        token, key_id = tokens[0].decode("ascii"), ids[0].decode("ascii")
        if not TOKEN.fullmatch(token) or not IDENTIFIER.fullmatch(key_id):
            return None
        keys = _keys(settings)
        now = datetime.now(timezone.utc)
        digest = hashlib.sha256(tokens[0]).hexdigest()
        for key in keys:
            if key["keyId"] == key_id and hmac.compare_digest(digest, key["tokenSha256"]) and \
                    _utc(key["notBeforeUtc"]) <= now < _utc(key["expiresAtUtc"]) and operation in key["permissions"]:
                return ServicePrincipal(key["serviceId"], key_id, operation)
    except (ValueError, TypeError, KeyError, AttributeError, RecursionError):
        pass
    return None


def authorize(request, settings):
    principal = request.scope.get("state", {}).get("private_service_principal") or authenticate(request.scope, settings)
    if principal is None:
        raise HTTPException(404, "Not found")
    return principal


class PrivateServiceBoundary:
    """Authenticate at ASGI entry, before routing, dependencies or body parsing."""
    def __init__(self, app, settings):
        self.app, self.settings = app, settings

    async def __call__(self, scope, receive, send):
        if scope["type"] != "http":
            return await self.app(scope, receive, send)
        settings = self.settings()
        path = scope.get("path", "")
        private = path.startswith("/internal/")
        restricted = mode(settings) != "Development"
        if not private and (not restricted or path == "/health" and scope.get("method") == "GET"):
            return await self.app(scope, receive, send)
        principal = authenticate(scope, settings) if private else None
        event = {"service_id": principal.service_id if principal else "unattributed",
                 "service_key_id": principal.key_id if principal else "unattributed",
                 "operation": OPERATIONS.get(path, "unknown"), "service_request_id": uuid4().hex}
        if principal is None:
            LOG.info("Private service access", extra={**event, "outcome": "Denied"})
            return await JSONResponse({"detail": "Not found"}, 404, headers={"Cache-Control": "no-store"})(scope, receive, send)
        scope.setdefault("state", {})["private_service_principal"] = principal
        outcome = "Failed"
        response_status = 500
        async def private_send(message):
            nonlocal outcome, response_status
            if message["type"] == "http.response.start":
                response_status = message["status"]
                message["headers"] = [(k, v) for k, v in message.get("headers", []) if k.lower() != b"cache-control"] + [(b"cache-control", b"no-store")]
            await send(message)
            if message["type"] == "http.response.body" and not message.get("more_body", False):
                outcome = "Succeeded" if 200 <= response_status < 300 else "Failed"
        try:
            await self.app(scope, receive, private_send)
        finally:
            LOG.info("Private service access", extra={**event, "outcome": outcome})
