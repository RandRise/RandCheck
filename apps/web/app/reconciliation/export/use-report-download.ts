import { useEffect, useRef, useState } from "react";
import { loadReport } from "./report-api";
import { reportFilename } from "./report-content";
import { reportCsv } from "./report-csv";
import { reportHtml } from "./report-html";
import type { ReportFormat, ReviewReport } from "./report.types";

export function useReportDownload(caseId: string, runId: string, version: string) {
  const [format, setFormat] = useState<ReportFormat>("html");
  const [snapshot, setSnapshot] = useState<ReviewReport | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const pending = useRef(false);
  const lifetime = useRef<AbortController | null>(null);
  const urls = useRef(new Set<string>());

  useEffect(() => {
    const abort = new AbortController();
    lifetime.current = abort;
    const downloads = urls.current;
    return () => { abort.abort(); downloads.forEach(url => URL.revokeObjectURL(url)); downloads.clear(); };
  }, []);

  async function download() {
    const signal = lifetime.current?.signal;
    if (pending.current || !signal || signal.aborted) return;
    pending.current = true; setBusy(true); setError("");
    try {
      // Reauthorize each download and reject a changed case version. Reuse the first
      // captured DTO for both formats, including its snapshot ID and decision cutoff.
      const authorized = await loadReport(caseId, runId, version, signal);
      if (signal.aborted) return;
      const report = snapshot ?? authorized;
      const content = format === "html" ? reportHtml(report) : reportCsv(report);
      const url = URL.createObjectURL(new Blob([content], { type: format === "html" ? "text/html;charset=utf-8" : "text/csv;charset=utf-8" }));
      urls.current.add(url);
      const link = document.createElement("a"); link.href = url; link.download = reportFilename(report, format);
      document.body.append(link); link.click(); link.remove();
      setSnapshot(report);
      // Allow the browser to start saving, then release memory. Account changes revoke sooner.
      setTimeout(() => { URL.revokeObjectURL(url); urls.current.delete(url); }, 1000);
    } catch (failure) {
      if (!signal.aborted) { setSnapshot(null); setError(failure instanceof Error && /^(Results or decisions changed|The export)/.test(failure.message) ? failure.message : "The export could not be prepared. Try again after checking access."); }
    } finally {
      pending.current = false;
      if (!signal.aborted) setBusy(false);
    }
  }

  return { format, setFormat, snapshot, busy, error, download };
}
