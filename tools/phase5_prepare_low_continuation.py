"""Preserve the failed low attempt and freeze its separately charged replacement."""
import copy,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
old=(ROOT/'samples/phase-5/answer-low-v0.3.0/plan.json').read_bytes()
assert hashlib.sha256(old).hexdigest()=='ffca5a39729d0f5a57f72212afa54618824322e0e000bbed9183ff5d41ab7321'
plan=json.loads(old)
rows=[json.loads(x) for x in (ROOT/'outputs/phase-5/answer-low-20260910/retrievals.jsonl').read_text(encoding='utf-8-sig').splitlines()]
out=ROOT/'outputs/phase-5/answer-low-fixed-20260910'
replacement=json.loads((out/'replacement-ready.jsonl').read_text(encoding='utf-8-sig'))
assert rows[0]['questionId']==replacement['questionId']=='p5-q-001'
left,right=copy.deepcopy(rows[0]['result']['hits']),copy.deepcopy(replacement['result']['hits'])
for hit in left+right: hit.pop('hitId')
assert left==right
failed=rows[0]['result']['id']
rows[0]=replacement
retrieval_bytes=('\n'.join(json.dumps(x,separators=(',',':')) for x in rows)+'\n').encode()
with (out/'retrievals.jsonl').open('xb') as f: f.write(retrieval_bytes)
plan['cases'][0].update(runId=replacement['result']['id'],requestKey='answer-low-fixed-v0.3.1-p5-q-001')
plan.update(version='answer-low-fixed-v0.3.1',previousPlanSha256=hashlib.sha256(old).hexdigest(),
    retainedFailedOperation=failed,retrievalResultsSha256=hashlib.sha256(retrieval_bytes).hexdigest(),
    retainedGenerationUsd='0.002000',transportFix='bind-reasoning-effort-before-http-v1')
directory=ROOT/'samples/phase-5/answer-low-fixed-v0.3.1'
directory.mkdir(exist_ok=True)
raw=(json.dumps(plan,indent=2)+'\n').encode()
with (directory/'plan.json').open('xb') as f: f.write(raw)
print(hashlib.sha256(raw).hexdigest())
