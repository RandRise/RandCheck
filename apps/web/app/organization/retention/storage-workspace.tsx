import { DocumentDeletionControls } from "./document-deletion";
import { StorageCleanup } from "./storage-cleanup";
import { StorageProtection } from "./storage-protection";
import { StorageSettings } from "./storage-settings";
import { StorageStatus } from "./storage-status";
import { useStorage } from "./use-storage";

export function StorageWorkspace() {
  const storage = useStorage();
  return <main className="organization-page storage-page">
    <div className="security-heading"><div><h2>Document storage</h2><p>Choose how long to keep files and protect records from removal.</p></div>
      <button className="security-button secondary" disabled={storage.busy} onClick={() => void storage.refresh()}>Refresh controls</button></div>
    <p className="storage-intro">Saving settings never deletes a document. Removal needs its own check and confirmation.</p>
    {storage.error && <p className="security-alert" role="alert">{storage.error}</p>}
    {storage.message && <p className="security-success" role="status">{storage.message}</p>}
    {storage.busy && !storage.policy && <p role="status">Loading storage controls…</p>}
    {storage.policy && storage.preview && <>
      <div className="storage-overview" role="note"><strong>{storage.preview.documentDeletionEnabled ? "Stored file removal requires inspection" : "Stored file removal is disabled"}</strong><span>Protections and saved history still apply.</span></div>
      <div className="storage-primary"><StorageSettings storage={storage}/><StorageProtection storage={storage}/></div>
      <StorageStatus storage={storage}/>
      <details className="storage-removal"><summary><strong>Removal controls</strong><span>For unfinished uploads and eligible stored files. Check carefully before confirming.</span></summary>
        <div className="storage-removal-sections"><StorageCleanup storage={storage}/>
          <DocumentDeletionControls key={storage.revision} enabled={storage.preview.documentDeletionEnabled} locked={storage.locked} onBusyChange={storage.setDeletionBusy} onRemoved={storage.onRemoved}/></div>
      </details>
      <p className="storage-footnote">Activity history keeps a record of storage changes. Restoring a backup must not bring back content that was removed; the separate recovery safeguards still apply.</p>
    </>}
  </main>;
}
