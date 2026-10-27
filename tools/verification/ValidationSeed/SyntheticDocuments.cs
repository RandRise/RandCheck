using System.Text;
using UaeDocumentIntelligence.Domain.Model;
using UaeDocumentIntelligence.Infrastructure.Services;

namespace ValidationSeed;

internal static class SyntheticDocuments
{
    // Confirmations are fixture setup for known fictional values, never a production workflow shortcut.
    public static List<Document> Pair()
    {
        var org = DevelopmentIdentity.OrganizationId;
        var actor = DevelopmentIdentity.ActorId;
        var now = DateTimeOffset.UtcNow;
        var documents = new List<Document>();
        foreach (var role in new[] { ReconciliationDocumentRole.PurchaseOrder, ReconciliationDocumentRole.Invoice })
        {
            var document = new Document(Guid.NewGuid(), org, actor, "Synthetic workflow " + role + ".pdf", Guid.NewGuid().ToString("N") + ".pdf", "application/pdf", 100,
                new string(role == ReconciliationDocumentRole.Invoice ? 'a' : 'b', 64), now);
            var extraction = document.QueueExtraction(Guid.NewGuid(), 1, "local", "local-1.6.0", "2.0", Guid.NewGuid().ToString("N"), now);
            extraction.Start(now);
            var values = new List<(string Key, FieldValueType Type, string Value, int Occurrence)> {
                (role == ReconciliationDocumentRole.Invoice ? "invoice_number" : "purchase_order_number", FieldValueType.Identifier, role == ReconciliationDocumentRole.Invoice ? "SINV-0001" : "SPO-0001", 0),
                ("supplier_trn", FieldValueType.Identifier, "000000000000701", 0), ("currency", FieldValueType.Currency, "AED", 0),
                ("total", FieldValueType.Decimal, role == ReconciliationDocumentRole.Invoice ? "1261.00" : "1260.00", 0),
                ("vat", FieldValueType.Decimal, "60", 0), ("tax_currency", FieldValueType.Currency, "AED", 0),
                ("tax_basis", FieldValueType.String, "net", 0), ("document_total_basis", FieldValueType.String, "gross", 0),
                ("line_item_code", FieldValueType.Identifier, "SRV-01", 1), ("line_description", FieldValueType.String, "Training", 1),
                ("line_quantity", FieldValueType.Decimal, "2", 1), ("line_unit", FieldValueType.String, "EA", 1),
                ("line_unit_price", FieldValueType.Decimal, "600", 1), ("line_price_basis", FieldValueType.String, "1 EA", 1),
                ("line_price_amount_basis", FieldValueType.String, "net", 1), ("line_total", FieldValueType.Decimal, "1260", 1),
                ("line_total_basis", FieldValueType.String, "gross", 1), ("line_tax", FieldValueType.Decimal, "60", 1) };
            if (role == ReconciliationDocumentRole.Invoice) values.Add(("purchase_order_number", FieldValueType.Identifier, "SPO-0001", 0));
            extraction.AddPage(Guid.NewGuid(), 1, string.Join("\n", values.Select(value => value.Value)), 0);
            var offset = 0;
            foreach (var value in values)
            {
                var field = extraction.AddField(Guid.NewGuid(), value.Key, value.Occurrence, value.Type, value.Value, .95m, now,
                    role == ReconciliationDocumentRole.Invoice && value.Key == "total" ? value.Value : value.Value);
                var end = offset + Encoding.UTF8.GetByteCount(value.Value);
                extraction.AddEvidence(field, Guid.NewGuid(), 1, value.Value, offset, end);
                offset = end + 1;
            }
            extraction.RecordProviderMetrics(1, 1, null, null, null);
            extraction.Succeed(role == ReconciliationDocumentRole.Invoice ? "invoice" : "purchase_order", "en", now, "fixture", "independent-inputs");
            document.RecordExtractionSuccess(extraction, now);
            var review = document.OpenReview(Guid.NewGuid(), actor, now);
            var sequence = 0;
            foreach (var field in extraction.Fields)
                review.AddAction(Guid.NewGuid(), actor, ++sequence, ReviewActionType.Confirmed, field.FieldKey, field.OccurrenceIndex, field.Id, null, null, null, now);
            review.Complete(extraction.Fields, now);
            documents.Add(document);
        }
        return documents;
    }

    public static Document Library(int index)
    {
        var now = DateTimeOffset.UtcNow.AddSeconds(-index);
        var invoice = index % 2 == 0;
        var document = new Document(Guid.NewGuid(), DevelopmentIdentity.OrganizationId, DevelopmentIdentity.ActorId,
            $"Synthetic {(invoice ? "invoice" : "purchase_order")} {index:D5}.pdf", Guid.NewGuid() + ".pdf", "application/pdf", 2048,
            new string('c', 64), now);
        var extraction = document.QueueExtraction(Guid.NewGuid(), 1, "local", "local-1.6.0", "2.0", Guid.NewGuid().ToString(), now);
        extraction.Start(now);
        var values = new[] { (Key: "supplier_name", Value: $"Validation supplier {index:D5}"),
            (Key: invoice ? "invoice_number" : "purchase_order_number", Value: $"{(invoice ? "INV" : "PO")}-{index:D5}"),
            (Key: "currency", Value: "AED") };
        extraction.AddPage(Guid.NewGuid(), 1, string.Join("\n", values.Select(v => v.Value)), 0);
        var offset = 0;
        foreach (var value in values)
        {
            var field = extraction.AddField(Guid.NewGuid(), value.Key, 0, value.Key == "currency" ? FieldValueType.Currency : FieldValueType.String, value.Value, .9m, now);
            extraction.AddEvidence(field, Guid.NewGuid(), 1, value.Value, offset, offset + value.Value.Length);
            offset += value.Value.Length + 1;
        }
        extraction.RecordProviderMetrics(1, 1, null, null, null);
        extraction.Succeed(invoice ? "invoice" : "purchase_order", "en", now, "synthetic-scale", "1");
        document.RecordExtractionSuccess(extraction, now);
        if (index % 3 == 0)
        {
            var review = document.OpenReview(Guid.NewGuid(), DevelopmentIdentity.ActorId, now);
            var sequence = 0;
            foreach (var field in extraction.Fields)
                review.AddAction(Guid.NewGuid(), DevelopmentIdentity.ActorId, ++sequence, ReviewActionType.Confirmed,
                    field.FieldKey, field.OccurrenceIndex, field.Id, null, null, null, now);
            review.Complete(extraction.Fields, now);
        }
        return document;
    }
}
