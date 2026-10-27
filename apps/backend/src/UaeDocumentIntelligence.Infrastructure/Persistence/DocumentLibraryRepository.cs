using Microsoft.EntityFrameworkCore;
using Npgsql;
using UaeDocumentIntelligence.Application.Documents;

namespace UaeDocumentIntelligence.Infrastructure.Persistence;

internal sealed partial class DocumentRepository
{
    private static readonly string[] LibraryIdentityFields =
    ["supplier", "supplier_name", "vendor_name", "invoice_number", "purchase_order_number", "po_number", "purchase_order_reference", "document_number"];
    private static readonly string[] QuotationTypes = ["quotation", "quote", "عرضسعر", "عرضاسعار", "عرضأسعار"];

    public async Task<DocumentLibraryPage> SearchAsync(Guid organizationId, DocumentLibraryQuery query, CancellationToken ct)
    {
        // PostgreSQL lateral reads follow ReviewWorkspaceView without loading
        // all documents/actions. Parameters and strpos keep search text literal.
        var source = db.Database.SqlQueryRaw<LibraryMatch>(LibrarySearchSql,
            new NpgsqlParameter("organization", organizationId), new NpgsqlParameter("term", query.Search.ToLowerInvariant()),
            new NpgsqlParameter("kind", query.Type), new NpgsqlParameter("include_quotes", query.IncludeQuotations),
            new NpgsqlParameter("identity_fields", LibraryIdentityFields), new NpgsqlParameter("quote_types", QuotationTypes));
        var total = await source.CountAsync(ct);
        var offset = total == 0 ? 0 : Math.Min(query.Offset, (total - 1) / DocumentLibraryQuery.PageSize * DocumentLibraryQuery.PageSize);
        var ids = await source.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip(offset).Take(DocumentLibraryQuery.PageSize).Select(x => x.Id).ToArrayAsync(ct);
        var items = await ReadWorkItemsAsync(db.Documents.AsNoTracking().Where(d => d.OrganizationId == organizationId && ids.Contains(d.Id)), ct);
        return new(items, total, offset, DocumentLibraryQuery.PageSize);
    }

    private sealed class LibraryMatch
    {
        public Guid Id { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
    }

    // Active reviews may use an older extraction. Otherwise use the completed
    // review of the latest successful/partial extraction, when available.
    private const string LibrarySearchSql = """
        WITH context AS (
            SELECT d.id, d.original_filename, d.created_at_utc,
                coalesce(review.extraction_run_id, selected.id) AS run_id,
                review.id AS session_id,
                regexp_replace(lower(trim(coalesce(selected.document_type, ''))), '[[:space:]_-]+', '', 'g') AS kind
            FROM documents d
            LEFT JOIN LATERAL (
                SELECT r.id, r.document_type FROM extraction_runs r
                WHERE r.organization_id=@organization AND r.document_id=d.id AND r.status IN ('Succeeded','Partial')
                ORDER BY r.run_number DESC LIMIT 1
            ) selected ON true
            LEFT JOIN LATERAL (
                SELECT s.id, s.extraction_run_id FROM review_sessions s
                WHERE s.organization_id=@organization AND s.document_id=d.id
                  AND (s.status IN ('Pending','InProgress') OR (s.status='Completed' AND s.extraction_run_id=selected.id))
                ORDER BY CASE WHEN s.status IN ('Pending','InProgress') THEN 0 ELSE 1 END, s.revision_number DESC LIMIT 1
            ) review ON true
            WHERE d.organization_id=@organization
        )
        SELECT c.id AS "Id", c.created_at_utc AS "CreatedAtUtc" FROM context c
        WHERE (@include_quotes OR NOT (c.kind=ANY(@quote_types)))
          AND (@kind='All' OR (@kind='Invoice' AND c.kind='invoice') OR (@kind='PurchaseOrder' AND c.kind IN ('purchaseorder','po')))
          AND (@term='' OR strpos(lower(c.original_filename), @term)>0 OR EXISTS (
            SELECT 1 FROM machine_field_values f
            LEFT JOIN LATERAL (
                SELECT a.action, a.corrected_value_text FROM review_actions a
                WHERE a.organization_id=@organization AND a.review_session_id=c.session_id AND a.machine_field_value_id=f.id
                ORDER BY a.sequence_number DESC LIMIT 1
            ) latest ON true
            WHERE f.organization_id=@organization AND f.extraction_run_id=c.run_id AND f.field_key=ANY(@identity_fields)
              AND strpos(lower(CASE WHEN latest.action='Cleared' THEN NULL WHEN latest.action='Corrected'
                  THEN latest.corrected_value_text ELSE f.value_text END), @term)>0
          ) OR EXISTS (
            SELECT 1 FROM review_actions a
            WHERE a.organization_id=@organization AND a.review_session_id=c.session_id AND a.machine_field_value_id IS NULL
              AND a.field_key=ANY(@identity_fields) AND a.action='Added' AND strpos(lower(a.corrected_value_text), @term)>0
              AND NOT EXISTS (
                SELECT 1 FROM review_actions newer WHERE newer.organization_id=@organization AND newer.review_session_id=a.review_session_id
                  AND newer.machine_field_value_id IS NULL AND newer.field_key=a.field_key AND newer.occurrence_index=a.occurrence_index
                  AND newer.sequence_number>a.sequence_number
              )
          ))
        """;
}
