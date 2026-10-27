import { useState } from "react";
import { reviewStateLabels } from "../document-fields";
import { LibraryFilter } from "./library-filter";
import type { LibraryDocument, LibraryQuery } from "./library.types";
import { useLibrarySearch } from "./use-library-search";

export function LibraryList({ selectedId, onSelect, refreshVersion }: {
  selectedId?: string; onSelect: (document: LibraryDocument) => void; refreshVersion: number;
}) {
  const work = useLibrarySearch(refreshVersion);
  const [text, setText] = useState("");
  const { page } = work;

  return <aside aria-label="Saved files"><h2>Saved files</h2>
    <form className="library-search" onSubmit={event => { event.preventDefault(); work.search({ search: text.trim() }); }}>
      <label>Find a document<input type="search" dir="auto" value={text} maxLength={200}
        placeholder="File, supplier, invoice or PO number" onChange={event => setText(event.target.value)} /></label>
      <div className="library-search-actions"><button type="submit">Search</button>
        <button type="button" className="secondary" onClick={() => { setText(""); work.search({ search: "" }); }}>Clear search</button></div>
      <label>Document type<select value={work.query.type} onChange={event => work.search({ search: text.trim(), type: event.target.value as LibraryQuery["type"] })}>
        <option value="All">All</option><option value="Invoice">Invoices</option><option value="PurchaseOrder">Purchase orders</option>
      </select></label>
    </form>
    <LibraryFilter showQuotations={work.query.includeQuotations}
      onShowQuotations={includeQuotations => work.search({ search: text.trim(), includeQuotations })} />
    <p className="library-search-help">Search includes saved corrections and extracted values that may still need checking. Missing values will not match.</p>
    {work.loading && <p role="status">Searching documents…</p>}
    {work.error && <div role="alert"><p>{work.error}</p><button onClick={work.retry}>Try search again</button></div>}
    {page && <>
      <p role="status" className="library-count">{page.total ? `Showing ${page.offset + 1}–${page.offset + page.documents.length} of ${page.total}` : "No matching files"}</p>
      {page.documents.length === 0 && <p>No documents match this search. Try another name or number, change the type, or select Show earlier quotations. Files added through Home will appear here.</p>}
      <div className="document-list">{page.documents.map(doc => <button key={doc.id} aria-pressed={selectedId === doc.id}
        className={selectedId === doc.id ? "document active" : "document"} onClick={() => onSelect(doc)}>
        <span><strong><bdi>{doc.originalFilename}</bdi></strong><small>{new Date(doc.createdAt).toLocaleString("en-GB")} · {(doc.sizeBytes / 1024).toFixed(1)} KB</small>
          {doc.latestExtractionStatus && doc.latestExtractionStatus !== "Ready" && <small>Extraction: {doc.latestExtractionStatus}</small>}
          {doc.reviewUsesOlderExtraction && <small>Review uses an earlier extraction</small>}</span>
        <i className={`status ${doc.reviewState === "Reviewed" ? "ready" : doc.status.toLowerCase()}`}>{reviewStateLabels[doc.reviewState ?? ""] ?? doc.status}</i>
      </button>)}</div>
      <nav className="library-pages" aria-label="Document pages">
        <button className="secondary" disabled={page.offset === 0} onClick={() => work.move(Math.max(0, page.offset - page.pageSize))}>Previous</button>
        <span>Page {Math.floor(page.offset / page.pageSize) + 1}</span>
        <button className="secondary" disabled={page.offset + page.pageSize >= page.total} onClick={() => work.move(page.offset + page.pageSize)}>Next</button>
      </nav>
    </>}
  </aside>;
}
