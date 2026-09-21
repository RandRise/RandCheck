-- Read-only, fixed synthetic comparison; no raw embedding output.
\set ON_ERROR_STOP on
SELECT jsonb_agg(jsonb_build_object('id',r.id,'key',r.request_key,'policy',r.policy_json,
 'status',r.status,'hits',(SELECT jsonb_agg(jsonb_build_object('chunkId',h.chunk_id,'rank',h.rank,
   'lexicalScore',h.lexical_score,'vectorScore',h.vector_score,'fusedScore',h.fused_score) ORDER BY h.rank)
 FROM retrieval_hits h WHERE h.organization_id=r.organization_id AND h.retrieval_run_id=r.id)) ORDER BY r.request_key)
FROM cited_retrieval_runs r WHERE r.organization_id='11111111-1111-1111-1111-111111111111'
 AND r.actor_id='22222222-2222-2222-2222-222222222222'
 AND r.request_key IN ('embedding-pilot-v0.1.0-lexical-retrieval-v1',
 'embedding-pilot-v0.1.0-vector-retrieval-v1','embedding-pilot-v0.1.0-hybrid-retrieval-v1');
