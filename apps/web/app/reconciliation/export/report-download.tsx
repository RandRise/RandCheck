import { useEnterprise } from "../../security/enterprise-provider";
import { useReportDownload } from "./use-report-download";
import type { ReportFormat } from "./report.types";
import "./report-download.css";

type Props = { caseId: string; runId: string; version: string; disabled?: boolean };

export function ReportDownload(props: Props) {
  const { session } = useEnterprise();
  return <DownloadControls key={`${session.tenantId}:${session.organizationId}:${session.actorId}:${session.role}:${props.caseId}:${props.runId}:${props.version}`} {...props}/>;
}

function DownloadControls({ caseId, runId, version, disabled = false }: Props) {
  const report = useReportDownload(caseId, runId, version);
  return <section className="report-download" aria-label="Download reviewed results">
    <div><h3>Keep a copy of these results</h3><p>Includes saved values, evidence, missing checks and recorded decisions.</p></div>
    <div className="report-download-actions"><label>File type<select value={report.format} disabled={report.busy || disabled} onChange={event => report.setFormat(event.target.value as ReportFormat)}>
      <option value="html">Readable report (HTML)</option><option value="csv">Spreadsheet (CSV)</option>
    </select></label><button type="button" disabled={report.busy || disabled} onClick={() => void report.download()}>{report.busy ? "Preparing file…" : "Download results"}</button></div>
    {report.error && <p role="alert">{report.error}</p>}
    {report.snapshot && <p role="status">File prepared from saved comparison {report.snapshot.run.runNumber}. Both file types use the same saved version until results are refreshed.</p>}
    <p className="report-download-note">Open the HTML report in a browser to read or print it. A decision does not change the check result.</p>
  </section>;
}
