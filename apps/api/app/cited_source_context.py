"""Immutable source labels supplied by the authorized .NET evidence loader.

Labels and quotes remain untrusted document data, not instructions or proof of
claim entailment. No provider-supplied metadata becomes authoritative.
"""
from dataclasses import dataclass, field
from uuid import UUID


@dataclass(frozen=True)
class AnswerSpan:
    span_id: str
    page_number: int
    byte_start: int
    byte_end: int
    quote: str = field(repr=False)


@dataclass(frozen=True)
class AnswerSource:
    index_id: str
    document_id: str
    review_session_id: str
    extraction_run_id: str
    filename: str = field(repr=False)
    chunk_kind: str
    review_action_id: str | None
    spans: tuple[AnswerSpan, ...] = field(repr=False)


def source_from_wire(value):
    if type(value) is not dict or set(value) != set(
        "indexId documentId reviewSessionId extractionRunId filename chunkKind reviewActionId spans".split()
    ) or type(value["spans"]) is not list:
        raise ValueError()
    spans = []
    for span in value["spans"]:
        if type(span) is not dict or set(span) != set("spanId pageNumber byteStart byteEnd quote".split()):
            raise ValueError()
        spans.append(AnswerSpan(span["spanId"], span["pageNumber"], span["byteStart"], span["byteEnd"], span["quote"]))
    return AnswerSource(value["indexId"], value["documentId"], value["reviewSessionId"],
                        value["extractionRunId"], value["filename"], value["chunkKind"],
                        value["reviewActionId"], tuple(spans))


def source_to_wire(source, span_ids, content):
    if type(source) is not AnswerSource or type(source.spans) is not tuple:
        raise ValueError()
    for identity in (source.index_id, source.document_id, source.review_session_id, source.extraction_run_id):
        if type(identity) is not str or str(UUID(identity)) != identity or UUID(identity).int == 0:
            raise ValueError()
    if (type(source.filename) is not str or not source.filename.strip() or len(source.filename) > 255
            or any(ord(c) < 32 or ord(c) == 127 for c in source.filename)
            or source.chunk_kind not in ("PageText", "ReviewedFact")
            or tuple(span.span_id for span in source.spans) != span_ids):
        raise ValueError()
    if source.chunk_kind == "PageText":
        if source.review_action_id is not None or len(source.spans) != 1 or source.spans[0].quote != content:
            raise ValueError()
    elif (type(source.review_action_id) is not str
          or str(UUID(source.review_action_id)) != source.review_action_id or UUID(source.review_action_id).int == 0):
        raise ValueError()
    spans = []
    for span in source.spans:
        if (type(span) is not AnswerSpan or type(span.page_number) is not int or span.page_number < 1
                or type(span.byte_start) is not int or type(span.byte_end) is not int
                or not 0 <= span.byte_start < span.byte_end <= 2_147_483_647
                or type(span.quote) is not str or len(span.quote.encode("utf-8")) != span.byte_end - span.byte_start):
            raise ValueError()
        spans.append(dict(spanId=span.span_id, pageNumber=span.page_number, byteStart=span.byte_start,
                          byteEnd=span.byte_end, quote=span.quote))
    return dict(indexId=source.index_id, documentId=source.document_id, reviewSessionId=source.review_session_id,
                extractionRunId=source.extraction_run_id, filename=source.filename, chunkKind=source.chunk_kind,
                reviewActionId=source.review_action_id, spans=spans)
