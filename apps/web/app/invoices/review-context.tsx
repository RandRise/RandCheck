"use client";

import Link from "next/link";
import { useEffect, useRef, useState } from "react";
import { API, apiFetch } from "../security/api-fetch";
import { isWorkspaceId } from "../workspace-location";

// A same-origin return link, never a caller-supplied redirect or a write.
export function InvoiceReviewContext({ documentId }: { documentId?: string }) {
  const [context, setContext] = useState<{ id: string; name: string; role: string; documentId?: string; requestedDocumentId?: string } | null>(null);
  const generation = useRef(0);
  useEffect(() => {
    const requests = generation;
    const load = async () => {
      const owner = ++generation.current;
      setContext(null);
      const query = new URLSearchParams(window.location.search);
      const id = query.get("pairCase"), role = query.get("pairRole");
      if (!id || !isWorkspaceId(id) || !["Invoice", "PurchaseOrder"].includes(role ?? "")) { if (owner === generation.current) setContext(null); return; }
      try {
        const response = await apiFetch(`${API}/api/reconciliation-cases/${id}`);
        if (!response.ok) throw new Error();
        const item = await response.json() as { id: string; name: string; workflowKind: string; draftDocuments?: { role: string; documentId: string }[] };
        if (owner !== generation.current) return;
        if (item.workflowKind !== "InvoicePurchaseOrder" || item.id.toLowerCase() !== id.toLowerCase()) { setContext(null); return; }
        setContext({ id: item.id, name: item.name, role: role!, requestedDocumentId: documentId, documentId: item.draftDocuments?.find(source => source.role === role)?.documentId });
      } catch { if (owner === generation.current) setContext(null); }
    };
    void load();
    const restore = () => { void load(); };
    window.addEventListener("popstate", restore);
    return () => { requests.current++; window.removeEventListener("popstate", restore); };
  }, [documentId]);
  const query = context ? new URLSearchParams(window.location.search) : null;
  const visible = context && context.requestedDocumentId === documentId && query?.get("pairCase")?.toLowerCase() === context.id.toLowerCase() && query?.get("pairRole") === context.role;
  if (!visible) return <p><Link href="/invoices">Check a supplier invoice against its purchase order</Link></p>;
  return <section aria-label="Invoice check context"><p>Invoice check: <bdi>{context.name}</bdi></p>
    <p>{documentId && documentId === context.documentId ? `Review this ${context.role === "Invoice" ? "invoice" : "purchase order"}, then return to choose its completed revision.` : "Return to the saved invoice check to select the document you want to use."}</p>
    <Link href={`/invoices?case=${context.id}`}>Return to invoice check</Link>
  </section>;
}
