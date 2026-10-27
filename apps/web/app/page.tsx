"use client";

import { useEffect, useState } from "react";
import DocumentWorkspace from "./document-workspace";
import { InvoiceHome } from "./invoices/components/invoice-home";

export default function Home() {
  const [legacyDocument, setLegacyDocument] = useState<boolean | null>(null);
  useEffect(() => {
    const restore = () => setLegacyDocument(new URLSearchParams(window.location.search).has("document") || new URLSearchParams(window.location.search).has("pairCase"));
    restore();
    window.addEventListener("popstate", restore);
    return () => window.removeEventListener("popstate", restore);
  }, []);
  if (legacyDocument === null) return <main role="status">Opening workspace...</main>;
  return legacyDocument ? <DocumentWorkspace /> : <InvoiceHome />;
}
