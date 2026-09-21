import asyncio
from datetime import datetime, timedelta, timezone
import hashlib
import json
import os
import unittest
from unittest.mock import patch

from fastapi.testclient import TestClient
from app.config import get_settings
from app.main import app
from app.service_security import PrivateServiceBoundary
from app.cited_answer_routes import get_answer_adapter, get_prompt_counter


TOKEN = "test_service_credential_" + "x" * 32
PATH = "/internal/cited/lexical-plan"


def key(**changes):
    now = datetime.now(timezone.utc)
    return {"keyId": "retrieval-202609", "serviceId": "api-retrieval",
            "tokenSha256": hashlib.sha256(TOKEN.encode()).hexdigest(),
            "permissions": ["retrieval"], "notBeforeUtc": (now - timedelta(days=1)).isoformat(),
            "expiresAtUtc": (now + timedelta(days=1)).isoformat(), **changes}


class ServiceSecurityTests(unittest.TestCase):
    def setUp(self):
        self.env = patch.dict(os.environ, {"APP_ENV": "production", "SERVICE_AUTH_MODE": "ServiceKeys",
            "SERVICE_KEYS_JSON": json.dumps([key()]), "WORKER_SHARED_SECRET": "old-secret",
            "ENABLE_LEGACY_EXTRACT": "true"})
        self.env.start()
        get_settings.cache_clear()
        self.client = TestClient(app, base_url="https://private.test")
        self.headers = {"X-Worker-Token": TOKEN, "X-Service-Key-Id": "retrieval-202609"}

    def tearDown(self):
        self.client.close()
        self.env.stop()
        get_settings.cache_clear()

    def configure(self, **values):
        os.environ.update(values)
        get_settings.cache_clear()

    def post(self, path=PATH, headers=None):
        return self.client.post(path, headers=self.headers if headers is None else headers,
                                json={"question": "invoice total", "topK": 5})

    def test_scoped_key_calls_real_retrieval_and_denies_other_operations(self):
        response = self.post()
        self.assertEqual(200, response.status_code)
        self.assertEqual("no-store", response.headers["Cache-Control"])
        for path in ("/internal/extractions", "/internal/cited/chunks", "/internal/cited/answer-prepare",
                     "/internal/cited/answer-proposal", "/internal/cited/embedding-proposal"):
            with self.subTest(path=path):
                self.assertEqual(404, self.post(path).status_code)

    def test_rotation_overlap_then_revocation_after_configuration_reload(self):
        second_token = "second_service_credential_" + "y" * 32
        second = key(keyId="retrieval-next", tokenSha256=hashlib.sha256(second_token.encode()).hexdigest())
        self.configure(SERVICE_KEYS_JSON=json.dumps([key(), second]))
        headers = {"X-Worker-Token": second_token, "X-Service-Key-Id": "retrieval-next"}
        self.assertEqual(200, self.post().status_code)
        self.assertEqual(200, self.post(headers=headers).status_code)
        self.configure(SERVICE_KEYS_JSON=json.dumps([second]))
        self.assertEqual(404, self.post().status_code)
        self.assertEqual(200, self.post(headers=headers).status_code)

    def test_expired_future_and_overlong_credentials_fail_closed(self):
        now = datetime.now(timezone.utc)
        for item in (key(expiresAtUtc=(now - timedelta(hours=1)).isoformat()),
                     key(notBeforeUtc=(now + timedelta(hours=1)).isoformat()),
                     key(expiresAtUtc=(now + timedelta(days=91)).isoformat()),
                     key(expiresAtUtc=now.replace(tzinfo=None).isoformat())):
            self.configure(SERVICE_KEYS_JSON=json.dumps([item]))
            self.assertEqual(404, self.post().status_code)

    def test_malformed_policy_never_uses_legacy_fallback(self):
        for raw in ("[]", "{}", "invalid", json.dumps([key(), key()]),
                    json.dumps([key(permissions=["retrieval", "*"])]),
                    json.dumps([key(permissions=["retrieval", "retrieval"])]),
                    json.dumps([key(unknown="private")]),
                    '[{"keyId":"first","keyId":"second"}]'):
            with self.subTest(raw=raw):
                self.configure(SERVICE_KEYS_JSON=raw)
                self.assertEqual(404, self.post().status_code)
                self.assertEqual(404, self.post(headers={"X-Worker-Token": "old-secret"}).status_code)

    def test_missing_wrong_and_duplicate_headers_fail_closed(self):
        for headers in ({}, {"Authorization": "Bearer human-token"},
                        {**self.headers, "X-Worker-Token": "z" * 43},
                        {**self.headers, "X-Service-Key-Id": "unknown"},
                        {"X-Worker-Token": TOKEN},
                        list(self.headers.items()) + [("X-Worker-Token", TOKEN)],
                        list(self.headers.items()) + [("X-Service-Key-Id", "retrieval-202609")]):
            self.assertEqual(404, self.post(headers=headers).status_code)

    def test_plain_http_unknown_routes_queries_and_methods_are_rejected(self):
        with TestClient(app, base_url="http://private.test") as client:
            self.assertEqual(404, client.post(PATH, headers=self.headers, content="bad").status_code)
        for path in (PATH + "?token=private", PATH + "/", "/internal/unknown", "/extract", "/docs", "/openapi.json"):
            self.assertEqual(404, self.post(path).status_code)
        self.assertEqual(404, self.client.get(PATH, headers=self.headers).status_code)
        self.assertEqual(200, self.client.get("/health").status_code)

    def test_enterprise_default_and_development_override_are_locked(self):
        for selected in ("", "Development", "misspelled", "Locked"):
            self.configure(SERVICE_AUTH_MODE=selected)
            self.assertEqual(404, self.post(headers={"X-Worker-Token": "old-secret"}).status_code)

    def test_explicit_local_development_still_supports_existing_shared_token(self):
        self.configure(APP_ENV="development", SERVICE_AUTH_MODE="")
        self.assertEqual(200, self.post(headers={"X-Worker-Token": "old-secret"}).status_code)
        self.assertEqual(404, self.post().status_code)

    def test_denial_does_not_read_body_or_dispatch_application(self):
        async def forbidden(*args):
            self.fail("Unauthenticated request reached body/application")
        messages = []
        async def send(message):
            messages.append(message)
        boundary = PrivateServiceBoundary(forbidden, get_settings)
        asyncio.run(boundary({"type": "http", "path": "/internal/extractions", "scheme": "https",
            "method": "POST", "headers": [], "query_string": b""}, forbidden, send))
        self.assertEqual(404, messages[0]["status"])

    def test_denial_does_not_construct_provider_dependencies(self):
        def forbidden():
            self.fail("Unauthenticated request constructed a provider")
        app.dependency_overrides[get_answer_adapter] = forbidden
        app.dependency_overrides[get_prompt_counter] = forbidden
        try:
            for path in ("answer-prepare", "answer-proposal"):
                response = self.client.post("/internal/cited/" + path, content="invalid secret body")
                self.assertEqual(404, response.status_code)
        finally:
            app.dependency_overrides.pop(get_answer_adapter, None)
            app.dependency_overrides.pop(get_prompt_counter, None)

    def test_access_telemetry_uses_verified_identity_and_never_incoming_payloads(self):
        with self.assertLogs("document_intelligence.service_security", level="INFO") as capture:
            self.post()
            self.post(headers={"X-Service-Key-Id": "untrusted-secret-id", "X-Worker-Token": "secret-token"})
            self.post(PATH + "?secret-query=private")
        records = [vars(record) for record in capture.records]
        serialized = str(records)
        for secret in (TOKEN, "secret-token", "untrusted-secret-id", "secret-query", "invoice total", "old-secret"):
            self.assertNotIn(secret, serialized)
        self.assertEqual(["Succeeded", "Denied", "Denied"], [r["outcome"] for r in records])
        self.assertEqual("api-retrieval", records[0]["service_id"])
        self.assertEqual("unattributed", records[1]["service_id"])
        self.assertEqual(3, len({r["service_request_id"] for r in records}))

    def test_secrets_are_not_in_settings_repr(self):
        self.assertNotIn("old-secret", repr(get_settings()))
        self.assertNotIn(key()["tokenSha256"], repr(get_settings()))

    def test_expiry_is_checked_on_every_request_without_reloading_settings(self):
        self.assertEqual(200, self.post().status_code)
        with patch("app.service_security.datetime", wraps=datetime) as clock:
            clock.now.return_value = datetime.now(timezone.utc) + timedelta(days=2)
            self.assertEqual(404, self.post().status_code)

    def test_authenticated_validation_failure_is_safe_and_logged_as_failed(self):
        self.configure(SERVICE_KEYS_JSON=json.dumps([key(permissions=["extraction"])]))
        with self.assertLogs("document_intelligence.service_security", level="INFO") as capture:
            response = self.client.post("/internal/extractions", headers=self.headers, content="private invalid body")
        self.assertEqual(422, response.status_code)
        self.assertNotIn("private invalid body", response.text)
        self.assertEqual("Failed", capture.records[0].outcome)
        self.assertEqual("no-store", response.headers["Cache-Control"])
