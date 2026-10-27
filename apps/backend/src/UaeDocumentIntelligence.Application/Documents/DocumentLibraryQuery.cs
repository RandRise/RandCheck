namespace UaeDocumentIntelligence.Application.Documents;

/// <summary>Library-only paging. Search is a literal substring, never a SQL pattern.</summary>
public sealed record DocumentLibraryQuery(string Search, string Type, bool IncludeQuotations, int Offset)
{
    public const int PageSize = 10;
    public const int MaximumSearchLength = 200;

    public static DocumentLibraryQuery Create(string? search, string? type, bool includeQuotations, int offset)
    {
        var term = (search ?? "").Trim();
        var kind = type ?? "All";
        if (term.Length > MaximumSearchLength || offset < 0 || offset > 1_000_000 ||
            kind is not ("All" or "Invoice" or "PurchaseOrder"))
            throw new ArgumentException("Choose a valid document type, page and search of at most 200 characters.");

        return new(term, kind, includeQuotations, offset);
    }
}

public sealed record DocumentLibraryPage(IReadOnlyList<DocumentWorkItem> Documents, int Total, int Offset, int PageSize);
