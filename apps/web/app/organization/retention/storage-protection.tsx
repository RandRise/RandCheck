import { activityTime } from "../audit/activity-labels";
import { STORAGE_MAX_OFFSET, STORAGE_PAGE_SIZE } from "./storage-api";
import type { StorageController } from "./use-storage";

const reasons: Record<string, string> = { AdministrativeReview: "Administrative review", Investigation: "Investigation", LegalHold: "Legal hold" };

export function StorageProtection({ storage }: { storage: StorageController }) {
  return <section className="security-panel storage-section" aria-labelledby="storage-protection-title">
    <div className="security-panel-heading"><div><h3 id="storage-protection-title">Protect files from removal</h3><p>A protection hold keeps files until an Admin releases it.</p></div></div>
    <form className="retention-form" onSubmit={event => { event.preventDefault(); void storage.placeHold(); }}>
      <label>Protect<select value={storage.scope} disabled={storage.locked} onChange={event => storage.setScope(event.target.value)}><option value="organization">All files in this workspace</option><option value="document">One document</option></select></label>
      {storage.scope === "document" && <label>Document ID<input required pattern="[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}" value={storage.document} disabled={storage.locked} onChange={event => storage.setDocument(event.target.value)}/></label>}
      <label>Reason<select value={storage.reason} disabled={storage.locked} onChange={event => storage.setReason(event.target.value)}>{Object.entries(reasons).map(([value, label]) => <option value={value} key={value}>{label}</option>)}</select></label>
      <p>Protecting all files also stops unfinished-upload cleanup. Protections do not expire by themselves. Every change is recorded in Activity history.</p>
      <button className="security-button" disabled={storage.locked}>Add protection</button>
    </form>
    <div className="storage-subheading"><h4>Protection history</h4><span>Active and released</span></div>
    {!storage.holds.length && <p className="retention-note">No protections on this page.</p>}
    {storage.holds.map(hold => <div className="retention-row" key={hold.id}>
      <div><strong>{hold.documentId ? "One document" : "All workspace files"}</strong>
        {hold.documentId && <small>{hold.documentId}</small>}
        <small>{reasons[hold.reason] ?? hold.reason}</small><small>{activityTime(hold.createdAtUtc) ?? "Time unavailable"} · UAE time</small></div>
      <span className={`security-badge ${hold.releasedAtUtc ? "" : "active"}`}>{hold.releasedAtUtc ? "Released" : "Protected"}</span>
      {!hold.releasedAtUtc && <button className="security-button secondary" disabled={storage.locked} onClick={() => storage.setRelease(hold)}>Release protection</button>}
    </div>)}
    {storage.release && <div className="retention-confirm" role="group" aria-label="Confirm protection release">
      <p>Release this protection for {storage.release.documentId ? "one document" : "all workspace files"}? Other protections still apply. This does not delete a file.</p>
      <div className="storage-actions"><button className="security-button" disabled={storage.locked} onClick={() => void storage.releaseHold()}>Confirm release</button>
        <button className="security-button secondary" disabled={storage.locked} onClick={() => storage.setRelease(null)}>Cancel</button></div>
    </div>}
    <nav className="audit-pagination" aria-label="Protection pages"><button className="security-button secondary" disabled={storage.locked || !storage.holdOffset} onClick={() => storage.setHoldOffset(storage.holdOffset - STORAGE_PAGE_SIZE)}>Previous protections</button>
      <span>Page {storage.holdOffset / STORAGE_PAGE_SIZE + 1}</span><button className="security-button secondary" disabled={storage.locked || storage.holds.length < STORAGE_PAGE_SIZE || storage.holdOffset >= STORAGE_MAX_OFFSET} onClick={() => storage.setHoldOffset(storage.holdOffset + STORAGE_PAGE_SIZE)}>Next protections</button></nav>
  </section>;
}
