import copy,hashlib,importlib.util,json,unittest
from pathlib import Path
spec=importlib.util.spec_from_file_location('answer_followup_report',Path(__file__).resolve().parents[3]/'tools/phase5_answer_followup_report.py')
report=importlib.util.module_from_spec(spec)
spec.loader.exec_module(report)

def fixture():
    case=dict(caseId='q',runId='r',expected=dict(status='Answered',reason_code=None))
    span=dict(Id='s',PageNumber=1,ByteStart=0,ByteEnd=9,Quote='Total 100')
    citation=dict(HitId='h',SpanId='s',IndexId='i',ChunkId='c',OriginalPageEvidence=span)
    generation=dict(outcome='validated-unpublished',ReservationId='reserve',PublicationAllowed=False,
        Validation=dict(Status=0,ReasonCode=None,SemanticSupportEstablished=False,
        Claims=[dict(Text='Total 100',Citations=[citation])]))
    raw=json.dumps(generation).encode()
    operation=dict(retrieval_id='r',state='Settled',prompt_version='cited-answer-prompt-v3',reservation_id='reserve',settled_usd=.001,reasoning_effort='low')
    manual=dict(generationSha256=hashlib.sha256(raw).hexdigest(),claimSupport=[True],complete=True,notes='Cited page supports total.')
    retrieval=dict(hits=[dict(hitId='h',indexId='i',chunkId='c',kind='PageText',spans=[{k[0].lower()+k[1:]:v for k,v in span.items()}])])
    return case,raw,operation,manual,retrieval

class AnswerFollowupReportTests(unittest.TestCase):
    def test_source_context_report_checks_excerpt_and_preserves_semantic_failure(self):
        c,raw,o,m,r=fixture()
        g=json.loads(raw)
        g['ScopePolicyVersion']='cited-scope-restriction-v1'
        g['Validation']['ValidatorVersion']='citation-excerpt-binding-v2'
        cite=g['Validation']['Claims'][0]['Citations'][0]
        cite['SupportText']='100'
        o['prompt_version']='cited-answer-prompt-v4'
        def assess():
            changed=json.dumps(g).encode()
            m['generationSha256']=hashlib.sha256(changed).hexdigest()
            return report.assess(c,changed,o,m,r,'low','cited-answer-prompt-v4')
        self.assertTrue(assess()['endToEndPass'])
        m['complete']=False
        self.assertFalse(assess()['endToEndPass'])
        for excerpt in ('999',None,''):
            cite['SupportText']=excerpt
            with self.subTest(excerpt=excerpt),self.assertRaises(ValueError): assess()
        cite['SupportText']='100'
        g['Validation']['ValidatorVersion']='citation-structure-v1'
        with self.assertRaises(ValueError): assess()

    def test_valid_source_assessment_passes(self):
        result=report.assess(*fixture(),expected_effort='low')
        self.assertTrue(result['endToEndPass'])
        self.assertEqual(1,result['citationCount'])

    def test_structural_acceptance_does_not_override_manual_failure(self):
        for field in ('claimSupport','complete'):
            args=list(fixture())
            args[3][field]=[False] if field=='claimSupport' else False
            self.assertFalse(report.assess(*args)['endToEndPass'])

    def test_irrelevant_extra_citation_is_reported_even_when_another_supports_claim(self):
        args=list(fixture())
        args[3]['citationIssues']=['An extra citation does not support this claim.']
        result=report.assess(*args)
        self.assertEqual(1,result['supportedClaimCount'])
        self.assertFalse(result['endToEndPass'])

    def test_wrong_reference_reason_fails_even_with_correct_refusal(self):
        c,raw,o,m,r=fixture()
        g=json.loads(raw)
        c['expected']=dict(status='InsufficientEvidence',reason_code='unsupported_scope')
        g['Validation'].update(Status=2,ReasonCode='incomplete_evidence',Claims=[])
        raw=json.dumps(g).encode()
        m.update(generationSha256=hashlib.sha256(raw).hexdigest(),claimSupport=[])
        result=report.assess(c,raw,o,m,r)
        self.assertTrue(result['statusMatches'])
        self.assertFalse(result['endToEndPass'])

    def test_tampered_output_scope_quote_and_incomplete_assessment_rejected(self):
        for defect in ('digest','quote','index','state','run','profile','assessment'):
            c,raw,o,m,r=fixture()
            if defect=='digest': raw+=b' '
            elif defect=='quote': r['hits'][0]['spans'][0]['quote']='Forged'
            elif defect=='index': r['hits'][0]['indexId']='foreign'
            elif defect=='state': o['state']='HeldUnknown'
            elif defect=='run': o['retrieval_id']='different'
            elif defect=='profile': o['reasoning_effort']='minimal'
            else: m['claimSupport']=[]
            with self.subTest(defect=defect),self.assertRaises(ValueError): report.assess(c,raw,o,m,r,'low')
