import { activityTime } from "../audit/activity-labels";
import { STORAGE_MAX_OFFSET, STORAGE_PAGE_SIZE } from "./storage-api";
import type { StorageController } from "./use-storage";

const dispositions: Record<string, string> = {
  Held: "Protected from removal", PastRetentionWindow: "Ready for a retention check",
  Retained: "Keep for now",
};

export function StorageStatus({ storage }: { storage: StorageController }) {
  return <details className="security-panel storage-status">
    <summary><strong>Check stored file status</strong><span>Read why a file is being kept. This list does not remove files.</span></summary>
    {storage.preview?.documents.map(document => <div className="retention-row" key={document.documentId}>
      <div><strong>Document</strong><small>{document.documentId}</small><small>Uploaded {activityTime(document.createdAtUtc) ?? "at an unavailable time"} · UAE time</small></div>
      <span className={`security-badge ${document.disposition === "Held" ? "active" : ""}`}>{dispositions[document.disposition] ?? document.disposition}</span>
    </div>)}
    {!storage.preview?.documents.length && <p className="retention-note">No stored files on this page.</p>}
    {storage.offset >= STORAGE_MAX_OFFSET && storage.preview?.nextOffset != null && <p role="note">More files are stored. This inspection list has reached its page limit. Use the Document library to find a specific file.</p>}
    <nav className="audit-pagination" aria-label="Stored file pages"><button className="security-button secondary" disabled={storage.locked || !storage.offset} onClick={() => storage.setOffset(storage.offset - STORAGE_PAGE_SIZE)}>Previous files</button>
      <span>Page {storage.offset / STORAGE_PAGE_SIZE + 1}</span><button className="security-button secondary" disabled={storage.locked || storage.preview?.nextOffset == null || storage.offset >= STORAGE_MAX_OFFSET} onClick={() => storage.setOffset(storage.preview!.nextOffset!)}>Next files</button></nav>
  </details>;
}
