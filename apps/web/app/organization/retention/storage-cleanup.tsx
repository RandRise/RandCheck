import type { StorageController } from "./use-storage";

export function StorageCleanup({ storage }: { storage: StorageController }) {
  return <section className="security-panel storage-section" aria-labelledby="storage-cleanup-title">
    <div className="security-panel-heading"><div><h3 id="storage-cleanup-title">Clean up unfinished uploads</h3><p>{storage.policy?.quarantineHours == null ? "Cleanup is off in the saved settings." : `Check abandoned uploads older than ${storage.policy.quarantineHours} hours.`}</p></div></div>
    <div className="retention-form">
      <p>Preview what can be removed first. Active uploads, protected items and files linked to stored documents stay safe. The system checks again before removal.</p>
      <button className="security-button secondary" disabled={storage.locked || storage.policy?.quarantineHours == null} onClick={() => void storage.previewCleanup()}>Preview cleanup</button>
      {storage.cleanup && <div className="storage-cleanup-result" role="status"><strong>{storage.cleanup.eligible} eligible · {storage.cleanup.preserved} kept · {storage.cleanup.deleted} removed</strong>
        {storage.cleanup.truncated && <p>This is one limited batch. More unfinished uploads may remain.</p>}</div>}
      {storage.cleanup && !storage.cleanup.executed && storage.cleanup.eligible > 0 && <>
        <label className="security-check"><input type="checkbox" checked={storage.confirmCleanup} disabled={storage.locked} onChange={event => storage.setConfirmCleanup(event.target.checked)}/><span>Remove the eligible unfinished uploads. This cannot be undone.</span></label>
        <button className="security-button" disabled={storage.locked || !storage.confirmCleanup} onClick={() => void storage.runCleanup()}>Remove unfinished uploads</button>
      </>}
    </div>
  </section>;
}
