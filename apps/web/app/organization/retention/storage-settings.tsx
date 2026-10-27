import type { StorageController } from "./use-storage";

export function StorageSettings({ storage }: { storage: StorageController }) {
  return <section className="security-panel storage-section" aria-labelledby="storage-settings-title">
    <div className="security-panel-heading"><div><h3 id="storage-settings-title">How long to keep files</h3><p>Set when files should be checked for removal.</p></div></div>
    <form className="retention-form" onSubmit={event => { event.preventDefault(); void storage.savePolicy(); }}>
      <label>Check stored documents after (days)<input type="number" min="1" max="36500" step="1" value={storage.days} disabled={storage.locked} onChange={event => storage.setDays(event.target.value)} placeholder="Keep without a time limit"/></label>
      <p>Leave blank to keep files without a time limit. A number only marks older files for review. It does not delete them.</p>
      <label>Check unfinished uploads after (hours)<input type="number" min="1" max="87600" step="1" value={storage.hours} disabled={storage.locked} onChange={event => storage.setHours(event.target.value)} placeholder="Cleanup off"/></label>
      <p>Leave blank to turn cleanup off. An Admin must preview and confirm cleanup; it never runs just because you save these settings.</p>
      {storage.settingsChangedElsewhere && <p role="status">Saved settings changed. Your entries are still here. Load saved settings before making a new change.</p>}
      <div className="security-actions"><button className="security-button" disabled={storage.locked || storage.settingsChangedElsewhere}>Save settings</button>
        {(storage.settingsEdited || storage.settingsChangedElsewhere) && <button type="button" className="security-button secondary" disabled={storage.locked} onClick={storage.reloadSettings}>Discard entries and load saved settings</button>}</div>
    </form>
  </section>;
}
