import asyncio
from datetime import datetime, timedelta, timezone
import hashlib
from pathlib import Path
import tempfile
import time
import types
import unittest
from unittest.mock import patch
from uuid import uuid4

from app.extraction import (
    EvidenceResult,
    ExtractionResult,
    FieldResult,
    MetricsResult,
    PageResult,
    ProviderResult,
)
from app.main import WorkerExtractRequest, _resolve_worker_storage, app


WORKER_SECRET = "test-worker-secret"


class WorkerExtractionApiTests(unittest.TestCase):
    def test_valid_trusted_request_resolves_storage_dispatches_exact_alias_and_propagates_deadline(self):
        async def exercise() -> None:
            with tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                document_id = uuid4()
                source, request = create_stored_request(root, document_id)
                provider = CapturingProvider("local")

                response = await post_worker(
                    request,
                    root,
                    headers={"x-worker-token": WORKER_SECRET},
                    provider=provider,
                )

                self.assertEqual(200, response.status_code)
                self.assertEqual(request["correlation_id"], response.headers["x-request-id"])
                self.assertEqual(source.resolve(), provider.path)
                self.assertEqual("application/pdf", provider.content_type)
                self.assertIsNotNone(provider.deadline_monotonic)
                self.assertGreater(provider.deadline_monotonic, time.monotonic())
                self.assertEqual("local", response.json()["provider"]["alias"])

        asyncio.run(exercise())

    def test_internal_route_is_indistinguishable_404_without_exact_worker_secret(self):
        async def exercise() -> None:
            with tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                _, request = create_stored_request(root, uuid4())
                provider = CapturingProvider("local")
                responses = []
                for headers in ({}, {"x-worker-token": "wrong-secret"}):
                    responses.append(
                        await post_worker(request, root, headers=headers, provider=provider)
                    )

                self.assertEqual([404, 404], [item.status_code for item in responses])
                self.assertEqual(responses[0].json(), responses[1].json())
                self.assertEqual(0, provider.call_count)

        asyncio.run(exercise())

    def test_invalid_storage_keys_fail_before_provider_dispatch(self):
        async def exercise() -> None:
            with tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                document_id = uuid4()
                _, baseline = create_stored_request(root, document_id)
                invalid = [
                    "/absolute/source.pdf",
                    "../source.pdf",
                    "organizations/../documents/source.pdf",
                    baseline["storage_key"].replace("/", "\\"),
                    baseline["storage_key"].replace("/source.pdf", "//source.pdf"),
                    f"organizations/{uuid4().hex}/documents/{uuid4().hex}/source.pdf",
                    f"organizations/{uuid4().hex}/documents/{document_id.hex}/missing.pdf",
                ]
                provider = CapturingProvider("local")
                for storage_key in invalid:
                    with self.subTest(storage_key=storage_key):
                        payload = dict(baseline, storage_key=storage_key)
                        response = await post_worker(
                            payload,
                            root,
                            headers={"x-worker-token": WORKER_SECRET},
                            provider=provider,
                        )
                        self.assertEqual(422, response.status_code)
                        self.assertEqual(
                            "invalid_storage_key", response.json()["error"]["code"]
                        )
                self.assertEqual(0, provider.call_count)

        asyncio.run(exercise())

    def test_size_and_sha_mismatch_fail_before_provider_dispatch(self):
        async def exercise() -> None:
            with tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                _, baseline = create_stored_request(root, uuid4())
                provider = CapturingProvider("local")
                variants = [
                    dict(baseline, bytes=baseline["bytes"] + 1),
                    dict(baseline, sha256="f" * 64),
                ]
                for payload in variants:
                    response = await post_worker(
                        payload,
                        root,
                        headers={"x-worker-token": WORKER_SECRET},
                        provider=provider,
                    )
                    self.assertEqual(422, response.status_code)
                    self.assertEqual(
                        "content_integrity_mismatch", response.json()["error"]["code"]
                    )
                self.assertEqual(0, provider.call_count)

        asyncio.run(exercise())

    def test_symlink_escape_is_rejected_before_provider_dispatch(self):
        async def exercise() -> None:
            with tempfile.TemporaryDirectory() as directory, tempfile.TemporaryDirectory() as outside:
                root = Path(directory)
                document_id = uuid4()
                source, request = create_stored_request(root, document_id)
                outside_file = Path(outside) / "source.pdf"
                outside_file.write_bytes(source.read_bytes())
                source.unlink()
                try:
                    source.symlink_to(outside_file)
                except OSError:
                    settings = types.SimpleNamespace(storage_shared_root=str(root))
                    with (
                        patch("app.main.get_settings", return_value=settings),
                        patch("app.main.Path", return_value=SymlinkTraversalPath()),
                    ):
                        with self.assertRaisesRegex(Exception, "stored document reference"):
                            _resolve_worker_storage(
                                WorkerExtractRequest.model_validate(request)
                            )
                    return
                provider = CapturingProvider("local")

                response = await post_worker(
                    request,
                    root,
                    headers={"x-worker-token": WORKER_SECRET},
                    provider=provider,
                )

                self.assertEqual(422, response.status_code)
                self.assertEqual("invalid_storage_key", response.json()["error"]["code"])
                self.assertEqual(0, provider.call_count)

        asyncio.run(exercise())

    def test_unknown_schema_expired_deadline_and_unknown_provider_are_safe_and_do_not_read_content(self):
        async def exercise() -> None:
            with tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                _, baseline = create_stored_request(root, uuid4())
                cases = [
                    (dict(baseline, schema_version="3.0"), "unsupported_schema_version", 422),
                    (
                        dict(
                            baseline,
                            deadline_utc=(datetime.now(timezone.utc) - timedelta(seconds=1)).isoformat(),
                        ),
                        "provider_timeout",
                        504,
                    ),
                    (dict(baseline, provider_alias="unknown"), "provider_configuration_error", 422),
                ]
                for payload, code, status in cases:
                    with self.subTest(code=code):
                        response = await post_worker(
                            payload,
                            root,
                            headers={"x-worker-token": WORKER_SECRET},
                            provider_error=ValueError("not configured")
                            if code == "provider_configuration_error"
                            else None,
                        )
                        self.assertEqual(status, response.status_code)
                        envelope = response.json()
                        self.assertEqual(code, envelope["error"]["code"])
                        self.assertNotIn(str(root), envelope["error"]["summary"])

        asyncio.run(exercise())

    def test_legacy_extract_is_path_independent_404_when_disabled(self):
        async def exercise() -> None:
            import httpx

            settings = types.SimpleNamespace(
                extraction_provider="mock",
                storage_shared_root="unused",
                enable_legacy_extract=False,
                worker_shared_secret=WORKER_SECRET,
            )
            transport = httpx.ASGITransport(app=app, raise_app_exceptions=False)
            with patch("app.main.get_settings", return_value=settings):
                async with httpx.AsyncClient(
                    transport=transport, base_url="http://test"
                ) as client:
                    existing = await client.post(
                        "/extract",
                        json={"filePath": __file__, "contentType": "application/pdf"},
                    )
                    missing = await client.post(
                        "/extract",
                        json={"filePath": "missing.pdf", "contentType": "application/pdf"},
                    )
            self.assertEqual(404, existing.status_code)
            self.assertEqual(existing.json(), missing.json())

        asyncio.run(exercise())


async def post_worker(
    payload: dict,
    root: Path,
    *,
    headers: dict[str, str],
    provider: "CapturingProvider | None" = None,
    provider_error: Exception | None = None,
):
    import httpx

    settings = types.SimpleNamespace(
        extraction_provider="mock",
        storage_shared_root=str(root),
        enable_legacy_extract=False,
        worker_shared_secret=WORKER_SECRET,
    )
    transport = httpx.ASGITransport(app=app, raise_app_exceptions=False)
    with patch("app.main.get_settings", return_value=settings):
        if provider_error is not None:
            extractor_patch = patch("app.main.get_extractor", side_effect=provider_error)
        else:
            extractor_patch = patch(
                "app.main.get_extractor", return_value=provider or CapturingProvider("local")
            )
        with extractor_patch:
            async with httpx.AsyncClient(
                transport=transport, base_url="http://test"
            ) as client:
                return await client.post(
                    "/internal/extractions",
                    headers={"x-request-id": payload["correlation_id"], **headers},
                    json=payload,
                )


def create_stored_request(root: Path, document_id):
    organization_id = uuid4()
    source = (
        root
        / "organizations"
        / organization_id.hex
        / "documents"
        / document_id.hex
        / "source.pdf"
    )
    source.parent.mkdir(parents=True)
    content = b"synthetic worker test document"
    source.write_bytes(content)
    request = {
        "schema_version": "2.0",
        "job_id": str(uuid4()),
        "document_id": str(document_id),
        "extraction_run_id": str(uuid4()),
        "storage_key": source.relative_to(root).as_posix(),
        "sha256": hashlib.sha256(content).hexdigest(),
        "bytes": len(content),
        "declared_content_type": "application/pdf",
        "provider_alias": "local",
        "attempt": 1,
        "deadline_utc": (datetime.now(timezone.utc) + timedelta(seconds=120)).isoformat(),
        "correlation_id": "request-123",
    }
    return source, request


class CapturingProvider:
    def __init__(self, alias: str) -> None:
        self.alias = alias
        self.path = None
        self.content_type = None
        self.deadline_monotonic = None
        self.call_count = 0

    def extract(self, path: Path, content_type: str, deadline_monotonic=None):
        self.path = path
        self.content_type = content_type
        self.deadline_monotonic = deadline_monotonic
        self.call_count += 1
        text = "105.00"
        return ExtractionResult(
            schema_version="2.0",
            outcome="Ready",
            document_type="invoice",
            language="en",
            provider=ProviderResult(self.alias, "local-1.0.0", "test-model", "v1"),
            metrics=MetricsResult(1, 1),
            pages=[PageResult(1, text, 0)],
            fields=[
                FieldResult(
                    "total",
                    0,
                    "Decimal",
                    text,
                    text,
                    1.0,
                    [
                        EvidenceResult(
                            1,
                            text,
                            0,
                            len(text.encode("utf-8")),
                            [[0.1, 0.1], [0.9, 0.1], [0.9, 0.9], [0.1, 0.9]],
                            "normalized",
                        )
                    ],
                )
            ],
            warnings=[],
        )


class SymlinkTraversalPath:
    def __init__(self, name: str = "root") -> None:
        self.name = name

    def resolve(self, strict: bool = False):
        del strict
        return self

    def __truediv__(self, part: str):
        return SymlinkTraversalPath(part)

    def is_dir(self) -> bool:
        return self.name == "root"

    def is_symlink(self) -> bool:
        return self.name == "source.pdf"


if __name__ == "__main__":
    unittest.main()
