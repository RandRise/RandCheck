"use client";

import { useEffect, useRef, useState } from "react";
import { API, apiFetch } from "./security/api-fetch";

class SourceDownloadFailure extends Error {}
function failure(status: number) {
  return new SourceDownloadFailure(status === 422 ? "The original file did not pass malware screening."
    : status === 404 ? "Original file access expired or is unavailable. Try again."
    : "Original file access is temporarily unavailable.");
}

export async function fetchOriginal(documentId: string, signal: AbortSignal, fetcher: typeof fetch = apiFetch) {
  const path = `/api/documents/${encodeURIComponent(documentId)}/source`;
  const issue = await fetcher(`${API}${path}-access`, { method: "POST", signal });
  if (!issue.ok) throw failure(issue.status);
  const grant = await issue.json();
  // Never send a credential to a server-selected URL or put it into navigation/history.
  if (grant.url !== path || typeof grant.accessToken !== "string" || !/^[A-Za-z0-9_-]{1,4096}$/.test(grant.accessToken))
    throw failure(503);
  signal.throwIfAborted();
  const response = await fetcher(`${API}${path}`, { headers: { "X-Document-Access": grant.accessToken }, signal });
  if (!response.ok) throw failure(response.status);
  const blob = await response.blob();
  signal.throwIfAborted();
  const disposition = response.headers.get("Content-Disposition") ?? "";
  const extension = disposition.match(/filename="?source-[a-f0-9]{32}(\.(?:pdf|png|jpe?g|txt|bin))(?:"|;|$)/i)?.[1] ?? ".bin";
  return { blob, filename: `source-${documentId.replaceAll("-", "")}${extension}` };
}

export function SourceDownload({ documentId }: { documentId: string }) {
  const controller = useRef<AbortController | null>(null);
  const [busy, setBusy] = useState(false); const [message, setMessage] = useState("");
  useEffect(() => () => controller.current?.abort(), []);
  async function download() {
    if (controller.current) return;
    const request = new AbortController(); controller.current = request;
    setBusy(true); setMessage("");
    try {
      const file = await fetchOriginal(documentId, request.signal);
      request.signal.throwIfAborted();
      const url = URL.createObjectURL(file.blob);
      try {
        const link = document.createElement("a"); link.href = url; link.download = file.filename; link.click();
        setMessage("Original file download started.");
      } finally { setTimeout(() => URL.revokeObjectURL(url), 1000); }
    } catch (error) {
      if (!request.signal.aborted) setMessage(error instanceof SourceDownloadFailure ? error.message : "Original file access is temporarily unavailable.");
    } finally { if (!request.signal.aborted) setBusy(false); controller.current = null; }
  }
  return <div className="source-download">
    <button className="secondary" onClick={download} disabled={busy}>{busy ? "Checking file…" : "Download original"}</button>
    {message && <p role="status">{message}</p>}
  </div>;
}
