"""Freeze the source-context/excerpt candidate with unchanged references and evidence."""
import copy
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]


def main():
    original = (ROOT / 'samples/phase-5/answer-low-fixed-v0.3.1/plan.json').read_bytes()
    assert hashlib.sha256(original).hexdigest() == '9695a66bdc7fc3394525d10731537f8d33c326f9a1cc3b1b00d9479cf9a44632'
    plan = json.loads(original)
    old = [json.loads(x) for x in (ROOT / 'outputs/phase-5/answer-low-fixed-20260910/retrievals.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    raw = (ROOT / 'outputs/phase-5/answer-source-20260911/retrievals.jsonl').read_bytes()
    new = [json.loads(x) for x in raw.decode('utf-8-sig').splitlines()]
    assert len(old) == len(new) == len(plan['cases']) == 30
    for case, previous, current in zip(plan['cases'], old, new):
        assert case['caseId'] == previous['questionId'] == current['questionId']
        left, right = copy.deepcopy(previous['result']['hits']), copy.deepcopy(current['result']['hits'])
        for hit in left + right:
            hit.pop('hitId')
        assert left == right and current['result']['policyVersion'] == case['policyVersion']
        assert current['result']['id'] != case['runId'] and current['result']['status'] == 'Retrieved'
        case.update(runId=current['result']['id'], requestKey='answer-source-v0.4.0-' + case['caseId'])
    plan.update(version='answer-source-v0.4.0', reasoningEffort='low', promptVersion='cited-answer-prompt-v4',
                answerSchemaVersion='1.1', validatorVersion='citation-excerpt-binding-v2',
                scopePolicyVersion='cited-scope-restriction-v1', paymentPolicyVersion='payment-event-restriction-v1',
                previousPlanSha256=hashlib.sha256(original).hexdigest(),
                retrievalResultsSha256=hashlib.sha256(raw).hexdigest(),
                changes=['Per-hit document identity and exact span metadata', 'Required literal citation supportText',
                         'Prompt instructions for document coverage and comparison operands',
                         'Existing scope restriction now applies to this new evaluation'])
    output = ROOT / 'samples/phase-5/answer-source-v0.4.0'
    output.mkdir(exist_ok=True)
    data = (json.dumps(plan, indent=2) + '\n').encode()
    with (output / 'plan.json').open('xb') as stream:
        stream.write(data)
    print(hashlib.sha256(data).hexdigest())


if __name__ == '__main__':
    main()
