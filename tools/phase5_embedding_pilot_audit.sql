-- Read-only summaries of the retained synthetic embedding pilot; no raw vectors or credentials.
\set ON_ERROR_STOP on
SELECT jsonb_build_object(
 'migrations',(SELECT count(*) FROM "__EFMigrationsHistory"),
 'vectors',(SELECT count(*) FROM cited_chunk_embeddings),
 'budget',(SELECT jsonb_build_object('ceilingUsd',ceiling_usd,'settledUsd',settled_usd,
   'requiresReconciliation',requires_reconciliation) FROM cited_pilot_budgets WHERE id=1),
 'pending',(SELECT count(*) FROM cited_spend_reservations WHERE state IN ('Reserved','Dispatched','Uncertain')),
 'retainedUnknownUsd',(SELECT coalesce(sum(reserved_usd),0) FROM cited_spend_reservations WHERE state='HeldUnknown'),
 'retainedUnknownOperations',(SELECT count(*) FROM cited_spend_reservations WHERE state='HeldUnknown'),
 'reservations',(SELECT count(*) FROM cited_spend_reservations),
 'artifacts',(SELECT count(*) FROM cited_generation_artifacts),
 'embeddingOperations',(SELECT coalesce(jsonb_agg(jsonb_build_object(
   'operationId',r.operation_id,'state',r.state,'reservedUsd',r.reserved_usd,'settledUsd',r.actual_usd,
   'inputTokens',o.p->'inputTokens','measuredInputTokens',o.p->'measuredInputTokens',
   'responseSha256',o.p->'responseSha256','outcome',o.p->'outcome',
   'purpose',convert_from(decode(p.p->>'requestUtf8','base64'),'UTF8')::jsonb#>>'{context,purpose}')
   ORDER BY r.operation_id),'[]'::jsonb)
 FROM cited_spend_reservations r
 LEFT JOIN (SELECT reservation_id,convert_from(payload_utf8,'UTF8')::jsonb p
   FROM cited_generation_artifacts WHERE kind='Outcome') o ON o.reservation_id=r.id
 LEFT JOIN (SELECT reservation_id,convert_from(payload_utf8,'UTF8')::jsonb p
   FROM cited_generation_artifacts WHERE kind='Preparation') p ON p.reservation_id=r.id
 WHERE r.workload='Embedding'));
