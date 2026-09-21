-- Read-only export. Run only against the retained synthetic pilot database.
SELECT jsonb_pretty(jsonb_build_object(
 'recorded_at_utc',clock_timestamp(),'database',current_database(),
 'budget',(SELECT jsonb_build_object('ceiling_usd',ceiling_usd,'settled_usd',settled_usd,
     'requires_reconciliation',requires_reconciliation) FROM cited_pilot_budgets),
 'artifact_count',(SELECT count(*) FROM cited_generation_artifacts),
 'pending_count',(SELECT count(*) FROM cited_spend_reservations WHERE state IN ('Reserved','Dispatched','Uncertain')),
 'operations',(SELECT jsonb_agg(jsonb_build_object(
    'reservation_id',r.id,'operation_id',r.operation_id,'workload',r.workload,
    'retrieval_id',CASE WHEN r.workload='Generation' THEN r.operation_id ELSE NULL END,'state',r.state,
    'reserved_usd',r.reserved_usd,'settled_usd',r.actual_usd,'provider',r.provider,'model',r.model_version,
    'created_at_utc',r.created_at_utc,'completed_at_utc',r.completed_at_utc,
    'reservation_to_settlement_ms',extract(epoch FROM (r.completed_at_utc-r.created_at_utc))*1000,
    'prompt_version',prep.p#>>'{preparation,promptVersion}',
    'reasoning_effort',prep.p#>>'{preparation,openAi,reasoningEffort}',
    'prompt_sha256',prep.p#>>'{preparation,promptSha256}',
    'input_tokens',outcome.p->'inputTokens','output_tokens',outcome.p->'outputTokens',
    'count_request_id',outcome.p#>>'{openAi,countRequestId}',
    'measured_input_tokens',outcome.p#>'{openAi,measuredInputTokens}',
    'provider_request_id',outcome.p#>>'{openAi,providerRequestId}',
    'response_id',outcome.p#>>'{openAi,responseId}',
    'response_sha256',outcome.p#>>'{openAi,responseSha256}',
    'cached_input_tokens',outcome.p#>'{openAi,cachedInputTokens}',
    'reasoning_tokens',outcome.p#>'{openAi,reasoningTokens}',
    'proposal',CASE WHEN length(outcome.p->>'proposalUtf8')>0
        THEN convert_from(decode(outcome.p->>'proposalUtf8','base64'),'UTF8')::jsonb ELSE NULL END
 ) ORDER BY r.created_at_utc)
 FROM cited_spend_reservations r
 LEFT JOIN (SELECT reservation_id,convert_from(payload_utf8,'UTF8')::jsonb p
    FROM cited_generation_artifacts WHERE kind='Outcome') outcome ON outcome.reservation_id=r.id
 LEFT JOIN (SELECT reservation_id,convert_from(payload_utf8,'UTF8')::jsonb p
    FROM cited_generation_artifacts WHERE kind='Preparation') prep ON prep.reservation_id=r.id)));
