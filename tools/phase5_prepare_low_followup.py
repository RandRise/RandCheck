"""Freeze a controlled reasoning-effort comparison; reuse questions and evidence."""
import copy,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]

def main():
    original=(ROOT/'samples/phase-5/answer-followup-v0.2.0/plan.json').read_bytes()
    assert hashlib.sha256(original).hexdigest()=='f342359d97a8cb176013d4aed01a1833116b0a1fe510a294162586b494b8b752'
    plan=json.loads(original)
    old=[json.loads(x) for x in (ROOT/'outputs/phase-5/nonredundant-retrieval-20260910/results.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    raw=(ROOT/'outputs/phase-5/answer-low-20260910/retrievals.jsonl').read_bytes()
    new=[json.loads(x) for x in raw.decode('utf-8-sig').splitlines()]
    assert len(old)==len(new)==len(plan['cases'])==30
    for c,a,b in zip(plan['cases'],old,new):
        assert c['caseId']==a['questionId']==b['questionId']
        # New run/hit IDs for a separately recorded attempt; all actual evidence/ranks are identical.
        left,right=copy.deepcopy(a['result']['hits']),copy.deepcopy(b['result']['hits'])
        for hit in left+right: hit.pop('hitId')
        assert left==right and a['result']['policyVersion']==b['result']['policyVersion']
        c.update(runId=b['result']['id'],requestKey='answer-low-v0.3.0-'+c['caseId'])
    plan.update(version='answer-low-v0.3.0',reasoningEffort='low',previousPlanSha256=hashlib.sha256(original).hexdigest(),
                retrievalResultsSha256=hashlib.sha256(raw).hexdigest())
    out=ROOT/'samples/phase-5/answer-low-v0.3.0'
    out.mkdir(exist_ok=True)
    data=(json.dumps(plan,indent=2)+'\n').encode()
    with (out/'plan.json').open('xb') as f: f.write(data)
    print(hashlib.sha256(data).hexdigest())

if __name__=='__main__': main()
