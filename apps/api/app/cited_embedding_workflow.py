"""Unregistered private embedding preparation/outcome contract.

ASP.NET must authorize current sources, commit preparation/reservation/dispatch,
and verify raw usage before settlement. This module cannot reserve or publish.
"""
import base64
import hashlib
import json

from .cited_embeddings import EmbeddingInput
from .cited_embedding_protocol import MODEL, EmbeddingScope, EmbeddingProtocolError, prepare_embedding_request
from .cited_embedding_tokens import LocalEmbeddingTokenizer, inspect_measured_embedding_reply
from .cited_embedding_transport import EmbeddingHttpResponse, EmbeddingTransportError, MAX_EMBEDDING_HTTP_BYTES

SCHEMA_VERSION = "embedding-pilot-v1"
MAX_SOURCE_BYTES = 49152
MAX_DISPATCH_BYTES = 98304
MAX_OUTCOME_BYTES = 524288
MAX_OUTCOME_METADATA_BYTES = 8192
MODEL_WIRE = {"providerAlias": MODEL.provider_alias, "model": MODEL.model, "version": MODEL.version,
              "dimension": MODEL.dimension, "distance": MODEL.distance}


def _json(value):
    return json.dumps(value, ensure_ascii=False, allow_nan=False, separators=(",", ":")).encode("utf-8")


def _exact(value, keys):
    if type(value) is not dict or set(value) != set(keys.split()):
        raise ValueError()
    return value


def _unique(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError()
        result[key] = value
    return result


def _invalid_constant(_):
    raise ValueError()


def _load(raw, limit):
    if type(raw) is not bytes or not 1 <= len(raw) <= limit:
        raise ValueError()
    return json.loads(raw.decode("utf-8"), object_pairs_hook=_unique, parse_constant=_invalid_constant)


def prepare(body: bytes, tokenizer: LocalEmbeddingTokenizer):
    try:
        if type(tokenizer) is not LocalEmbeddingTokenizer:
            raise ValueError()
        value = _exact(_load(body, MAX_SOURCE_BYTES), "schemaVersion context model inputs")
        if (value["schemaVersion"] != SCHEMA_VERSION
                or json.dumps(value["model"], sort_keys=True) != json.dumps(MODEL_WIRE, sort_keys=True)):
            raise ValueError()
        context = _exact(value["context"], "organizationId actorId operationId purpose indexIds")
        if type(context["indexIds"]) is not list or type(value["inputs"]) is not list:
            raise ValueError()
        scope = EmbeddingScope(context["organizationId"], context["actorId"], context["operationId"],
                               context["purpose"], tuple(context["indexIds"]))
        inputs = []
        for candidate in value["inputs"]:
            candidate = _exact(candidate, "inputId text inputSha256")
            inputs.append(EmbeddingInput(candidate["inputId"], candidate["text"], candidate["inputSha256"]))
        measured = tokenizer.measure(prepare_embedding_request(scope, tuple(inputs)))
        receipt = {"schemaVersion": SCHEMA_VERSION, "sourceSha256": hashlib.sha256(body).hexdigest(),
                   "model": MODEL_WIRE.copy(), "bodySha256": measured.body_sha256,
                   "bindingSha256": measured.binding_sha256, "wireVersion": measured.wire_version,
                   "counterVersion": measured.counter_version, "vocabularySha256": measured.vocabulary_sha256,
                   "inputTokens": measured.input_tokens, "tokenCounts": measured.token_counts,
                   "maximumOutputTokens": 0, "maximumResponseBytes": MAX_EMBEDDING_HTTP_BYTES}
        # Check the exact saved dispatch size before a coordinator can reserve it.
        dispatch = _json({"requestUtf8": base64.b64encode(body).decode("ascii"), "preparation": receipt})
        if len(dispatch) > MAX_DISPATCH_BYTES:
            raise ValueError()
        return receipt, measured
    except Exception:
        raise EmbeddingProtocolError("invalid_embedding_preparation") from None


async def propose(dispatch_bytes: bytes, tokenizer: LocalEmbeddingTokenizer, transport):
    try:
        dispatch = _exact(_load(dispatch_bytes, MAX_DISPATCH_BYTES), "requestUtf8 preparation")
        body = base64.b64decode(dispatch["requestUtf8"], validate=True)
        receipt, measured = prepare(body, tokenizer)
        if json.dumps(dispatch["preparation"], sort_keys=True) != json.dumps(receipt, sort_keys=True):
            raise ValueError()
    except Exception:
        raise EmbeddingProtocolError("invalid_embedding_dispatch") from None
    result = {"schemaVersion": SCHEMA_VERSION, "sourceSha256": receipt["sourceSha256"],
              "bodySha256": measured.body_sha256, "bindingSha256": measured.binding_sha256,
              "model": MODEL_WIRE.copy(), "counterVersion": measured.counter_version,
              "wireVersion": measured.wire_version, "measuredInputTokens": measured.input_tokens,
              "inputTokens": None, "outputTokens": 0, "accountingKnown": False,
              "outcome": "transport_uncertain", "requiresReconciliation": True,
              "providerRequestId": None, "httpStatus": None, "responseSha256": None,
              "responseUtf8": None, "publicationAllowed": False}
    try:
        reply = await transport.send(measured)  # Exactly one attempt in this invocation.
        if (type(reply) is not EmbeddingHttpResponse or type(reply.body_utf8) is not bytes
                or not 1 <= len(reply.body_utf8) <= MAX_EMBEDDING_HTTP_BYTES):
            raise ValueError()
        audited = inspect_measured_embedding_reply(tokenizer, measured, reply.body_utf8, reply.provider_request_id)
        result.update(inputTokens=audited.input_tokens, accountingKnown=audited.accounting_known,
                      outcome=audited.outcome, requiresReconciliation=(not audited.accounting_known
                                                                     or audited.outcome == "usage_mismatch"),
                      providerRequestId=audited.provider_request_id, httpStatus=200,
                      responseSha256=audited.response_sha256,
                      responseUtf8=base64.b64encode(audited.response_utf8).decode("ascii"))
    except EmbeddingTransportError as error:
        result.update(httpStatus=error.http_status, providerRequestId=error.provider_request_id)
    except Exception:
        pass  # Unknown outcome is not a zero-cost outcome. Cancellation still propagates.
    # Save raw vectors once, in the raw response. Do not duplicate arrays in this
    # envelope; ASP.NET/storage can decode the immutable payload after validation.
    metadata = {key: value for key, value in result.items() if key != "responseUtf8"}
    if len(_json(metadata)) > MAX_OUTCOME_METADATA_BYTES or len(_json(result)) > MAX_OUTCOME_BYTES:
        raise EmbeddingProtocolError("embedding_outcome_too_large")
    return result
