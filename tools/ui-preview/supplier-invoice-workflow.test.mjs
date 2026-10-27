// Preview simulation only: these checks do not execute extraction or the product rule engine.
import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createServer} from 'node:http';
import {createFixtureApi} from './fixtures.mjs';

async function setup(t) {
  let api;
  const server = createServer((req, res) => api(req, res).catch(e => res.writeHead(500).end(e.message)));
  await new Promise(resolve => server.listen(0, '127.0.0.1', resolve));
  const origin = `http://127.0.0.1:${server.address().port}`; api = createFixtureApi(origin);
  t.after(() => new Promise(resolve => server.close(resolve)));
  return async (path, {method='GET',body,etag,role='operator'}={}) => {
    const response = await fetch(origin+path, {method, headers:{Authorization:`Bearer preview-${role}`,Origin:origin,'Content-Type':'application/json',...(etag?{'If-Match':etag}:{})}, body:body===undefined?undefined:JSON.stringify(body)});
    return {status:response.status,etag:response.headers.get('etag'),value:await response.json()};
  };
}
async function pair(call) {
  const docs = (await call('/api/documents')).value;
  const invoice = docs.find(d=>d.originalFilename==='Completed invoice.txt');
  const po = docs.find(d=>d.originalFilename==='Completed purchase order.txt');
  const created = await call('/api/reconciliation-cases',{method:'POST',body:{name:'Synthetic supplier pair',workflowKind:'InvoicePurchaseOrder',requestKey:'create-pair'}});
  return {created,invoice,po,path:`/api/reconciliation-cases/${created.value.id}`};
}

test("an invoice in the PO slot is rejected without changes; choosing the actual PO permits comparison", async t => {
  const call = await setup(t);
  const { created, invoice, po, path } = await pair(call);
  const invoiceReview = (await call(`/api/documents/${invoice.id}/review`)).value;
  let current = await call(path + "/draft-documents/PurchaseOrder", {
    method: "PUT", etag: created.etag, body: { documentId: invoice.id, requestKey: "wrong-po" },
  });
  const rejected = await call(path + "/documents/PurchaseOrder", {
    method: "PUT", etag: current.etag, body: { documentId: invoice.id, reviewSessionId: invoiceReview.session.id },
  });
  assert.equal(rejected.status, 400);
  assert.match(rejected.value.detail, /document type does not match/);
  assert.deepEqual((await call(path)).value, current.value);
  assert.deepEqual((await call(`/api/documents/${invoice.id}/review`)).value, invoiceReview);

  for (const [role, doc] of [["PurchaseOrder", po], ["Invoice", invoice]]) {
    current = await call(path + "/draft-documents/" + role, {
      method: "PUT", etag: current.etag, body: { documentId: doc.id, requestKey: "correct-" + role },
    });
    const review = (await call(`/api/documents/${doc.id}/review`)).value;
    current = await call(path + "/documents/" + role, {
      method: "PUT", etag: current.etag, body: { documentId: doc.id, reviewSessionId: review.session.id },
    });
    assert.equal(current.status, 200);
  }
  const compared = await call(path + "/evaluations", {
    method: "POST", etag: current.etag, body: { ruleSetVersion: "invoice-po-1.0", orderScope: "Unknown" },
  });
  assert.equal(compared.status, 200);
  assert.equal(compared.value.runs.length, 1);
  assert.deepEqual((await call(`/api/documents/${invoice.id}/review`)).value, invoiceReview);
});
test('new synthetic pair saves drafts before review pins, exact retry read-only and ABA refused',async t=>{
  const call=await setup(t); const {created,invoice,po,path}=await pair(call);
  const initial=created.etag;
  let current=await call(path+'/draft-documents/Invoice',{method:'PUT',etag:initial,body:{documentId:invoice.id,requestKey:'A'}});
  assert.equal(current.status,200); assert.equal(current.value.documents.length,0);assert.equal(current.value.runs.length,0);
  current=await call(path+'/draft-documents/PurchaseOrder',{method:'PUT',etag:current.etag,body:{documentId:po.id,requestKey:'PO'}});
  const replay=await call(path+'/draft-documents/Invoice',{method:'PUT',etag:initial,body:{documentId:invoice.id,requestKey:'A'}});
  assert.equal(replay.status,200);assert.equal(replay.etag,current.etag);
  assert.equal((await call(path+'/draft-documents/Invoice',{method:'PUT',etag:initial,body:{documentId:null,requestKey:'A'}})).status,409);
  current=await call(path+'/draft-documents/Invoice',{method:'PUT',etag:current.etag,body:{documentId:null,requestKey:'clear'}});
  current=await call(path+'/draft-documents/Invoice',{method:'PUT',etag:current.etag,body:{documentId:invoice.id,requestKey:'A2'}});
  assert.equal((await call(path+'/draft-documents/Invoice',{method:'PUT',etag:initial,body:{documentId:invoice.id,requestKey:'A'}})).status,409);
  assert.equal((await call(path+'?unused=1')).value.draftDocuments.length,2);
  const filtered=(await call('/api/reconciliation-cases?workflowKind=InvoicePurchaseOrder')).value;
  assert.equal(filtered.cases.length,1);assert.equal(filtered.cases[0].workflowKind,'InvoicePurchaseOrder');
});
test('synthetic comparison requires exact completed pins and explicit scope; old result is frozen',async t=>{
  const call=await setup(t);const {created,invoice,po,path}=await pair(call);let current=created;
  assert.equal((await call(path+'/evaluations',{method:'POST',etag:current.etag,body:{ruleSetVersion:'invoice-po-1.0',orderScope:'FullOrder'}})).status,400);
  for(const [role,doc] of [['Invoice',invoice],['PurchaseOrder',po]]){
    current=await call(path+'/draft-documents/'+role,{method:'PUT',etag:current.etag,body:{documentId:doc.id,requestKey:role}});
    const review=(await call(`/api/documents/${doc.id}/review`)).value;
    current=await call(path+'/documents/'+role,{method:'PUT',etag:current.etag,body:{documentId:doc.id,reviewSessionId:review.session.id}});
    assert.equal(current.status,200);
  }
  for(const body of [{},{ruleSetVersion:'invoice-po-1.0'},{ruleSetVersion:'1.0',orderScope:'FullOrder'}])
    assert.equal((await call(path+'/evaluations',{method:'POST',etag:current.etag,body})).status,400);
  current=await call(path+'/evaluations',{method:'POST',etag:current.etag,body:{ruleSetVersion:'invoice-po-1.0',orderScope:'FullOrder'}});
  assert.equal(current.status,200);const frozen=structuredClone(current.value.runs[0]);
  assert.equal(frozen.ruleSetVersion,'synthetic-total-only');assert.equal(frozen.coverage.status,'NotRecorded');assert.equal(frozen.orderScope,'FullOrder');
  assert.match(frozen.coverage.conclusion,/Synthetic/);assert.equal(frozen.sources.length,2);
  current=await call(path+'/draft-documents/Invoice',{method:'PUT',etag:current.etag,body:{documentId:null,requestKey:'replace'}});
  assert.deepEqual(current.value.runs[0],frozen);assert.equal(current.value.documents.length,1);
});
test('unknown/disabled mutations refused and legacy default retains simulated total-only contract',async t=>{
  const call=await setup(t);const {created,invoice,path}=await pair(call);
  for(const role of ['unknown','revoked'])assert.equal((await call(path+'/draft-documents/Invoice',{method:'PUT',role,etag:created.etag,body:{documentId:invoice.id,requestKey:role}})).status,403);
  assert.equal((await call(path+'/draft-documents/Invoice',{method:'PUT',body:{documentId:invoice.id,requestKey:'no-etag'}})).status,428);
  const legacy=await call('/api/reconciliation-cases',{method:'POST',body:{name:'Legacy unchanged',requestKey:'legacy'}});
  assert.equal(legacy.status,201);assert.equal(legacy.value.workflowKind??'Legacy','Legacy');
  assert.equal((await call(`/api/reconciliation-cases/${legacy.value.id}/draft-documents/Invoice`,{method:'PUT',etag:legacy.etag,body:{documentId:invoice.id,requestKey:'legacy-draft'}})).status,400);
  assert.equal((await call('/api/reconciliation-cases?workflowKind=Legacy')).value.cases.length,1);
});

for (const scenario of ['foreign', 'cleared', 'wrong-type', 'AED']) {
  test(`synthetic AED gate keeps state on refusal: ${scenario}`, async t => {
    const call = await setup(t);
    const { created, po, path } = await pair(call);
    const invoice = (await call('/api/documents')).value.find(doc => doc.originalFilename === 'Invoice to review.txt');
    let reviewed = await call(`/api/documents/${invoice.id}/reviews`, { method: 'POST', body: {} });
    const reviewPath = `/api/documents/${invoice.id}/reviews/${reviewed.value.session.id}`;
    for (const field of reviewed.value.fields) {
      const currency = field.fieldKey === 'currency';
      const action = currency && scenario === 'cleared' ? 'Cleared' : currency && scenario !== 'AED' ? 'Corrected' : 'Confirmed';
      reviewed = await call(reviewPath + '/actions', { method: 'POST', etag: reviewed.etag, body: {
        action, fieldKey: field.fieldKey, occurrenceIndex: field.occurrenceIndex, machineFieldValueId: field.machine.id,
        ...(action !== 'Confirmed' ? { reason: 'Synthetic currency test' } : {}),
        ...(action === 'Corrected' ? { value: scenario === 'foreign' ? 'USD' : 'AED', valueType: scenario === 'wrong-type' ? 'String' : 'Currency' } : {}),
      } });
      assert.equal(reviewed.status, 200);
    }
    reviewed = await call(reviewPath + '/complete', { method: 'POST', etag: reviewed.etag, body: {} });
    assert.equal(reviewed.status, 200);
    let current = created;
    for (const [role, doc, review] of [['Invoice', invoice, reviewed.value], ['PurchaseOrder', po, (await call(`/api/documents/${po.id}/review`)).value]]) {
      current = await call(path + '/draft-documents/' + role, { method: 'PUT', etag: current.etag, body: { documentId: doc.id, requestKey: role } });
      current = await call(path + '/documents/' + role, { method: 'PUT', etag: current.etag, body: { documentId: doc.id, reviewSessionId: review.session.id } });
      assert.equal(current.status, 200);
    }
    const response = await call(path + '/evaluations', { method: 'POST', etag: current.etag, body: { ruleSetVersion: 'invoice-po-1.0', orderScope: 'Unknown' } });
    if (scenario === 'AED') {
      assert.equal(response.status, 200);
      assert.equal(response.value.runs.length, 1);
    } else {
      assert.equal(response.status, 400);
      assert.equal(response.value.code, scenario === 'foreign' ? 'aed_currency_required' : 'aed_currency_unverified');
      const unchanged = await call(path);
      assert.equal(unchanged.etag, current.etag);
      assert.deepEqual(unchanged.value, current.value);
      assert.deepEqual((await call(`/api/documents/${invoice.id}/review`)).value, reviewed.value);
    }
  });
}
