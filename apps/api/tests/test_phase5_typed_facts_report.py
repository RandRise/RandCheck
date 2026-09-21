import copy
import hashlib
import importlib.util
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location('typed_report', Path(__file__).resolve().parents[3] / 'tools/phase5_typed_facts_report.py')
report = importlib.util.module_from_spec(spec)
spec.loader.exec_module(report)


def fixture():
    index = report.ROLES[0]
    text = 'quotation_number[0]: Q-260801'
    sha = hashlib.sha256(text.encode()).hexdigest()
    citation = dict(IndexId=index, ChunkId='c', FieldKey='quotation_number', Occurrence=0,
                    ReviewedValue='Q-260801', SupportText=text, ContentSha256=sha, OriginalPageEvidence=[])
    operand = dict(IndexId=index, Role=0, FieldKey='quotation_number', Occurrence=0, Value='Q-260801',
                   Number=None, Currency=None, Citations=[copy.deepcopy(citation)])
    answer = dict(Status='Answered', ReasonCode=None, PublicationAllowed=False, ModelInvoked=False,
                  GeneralSemanticSupportEstablished=False, Operands=[operand],
                  Claims=[dict(Text='Quotation number: Q-260801.', Citations=[citation])], Calculation=None, MissingParts=[])
    data = dict(outcome='typed-unpublished', result=dict(RetrievalRunId='r', Answer=answer), evidenceReferencesVerified=1)
    case = dict(caseId='p5-q-001', runId='r', indexIds=[index], expected=dict(status='Answered', reason_code=None))
    return case, data, {(index, 'c'): dict(text=text, sha256=sha, spans=[])}


class TypedFactsReportTests(unittest.TestCase):
    def test_bound_fact_passes(self):
        self.assertTrue(report.check(*fixture())['passes'])

    def test_wrong_field_is_a_quality_failure_even_with_valid_citation(self):
        case, data, chunks = fixture()
        data['result']['Answer']['Operands'][0]['Value'] = 'WRONG'
        data['result']['Answer']['Operands'][0]['Citations'][0]['ReviewedValue'] = 'WRONG'
        with self.assertRaises(ValueError):
            report.check(case, data, chunks)

    def test_currency_on_identifier_fails(self):
        case, data, chunks = fixture()
        data['result']['Answer']['Operands'][0]['Currency'] = 'AED'
        self.assertFalse(report.check(case, data, chunks)['passes'])

    def test_citation_text_or_span_tampering_is_rejected(self):
        for key, value in [('SupportText', 'quotation_number[0]: WRONG'),
                           ('OriginalPageEvidence', [dict(PageNumber=1, ByteStart=0, ByteEnd=1, Quote='X')])]:
            case, data, chunks = fixture()
            data['result']['Answer']['Claims'][0]['Citations'][0][key] = value
            with self.subTest(key=key), self.assertRaises(ValueError):
                report.check(case, data, chunks)

    def test_missing_requested_operand_remains_failure(self):
        case, data, chunks = fixture()
        data['result']['Answer']['Operands'] = []
        self.assertFalse(report.check(case, data, chunks)['passes'])

    def test_identifier_must_not_become_numeric(self):
        case, data, chunks = fixture()
        data['result']['Answer']['Operands'][0]['Number'] = 260801
        with self.assertRaises(ValueError):
            report.check(case, data, chunks)

    def test_navigation_or_publication_change_is_rejected(self):
        for field in ('navigation', 'publication'):
            case, data, chunks = fixture()
            if field == 'navigation':
                data['evidenceReferencesVerified'] = 0
            else:
                data['result']['Answer']['PublicationAllowed'] = True
            with self.subTest(field=field), self.assertRaises(ValueError):
                report.check(case, data, chunks)
