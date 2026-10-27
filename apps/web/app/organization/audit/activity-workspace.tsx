import { ACTIVITY_MAX_OFFSET, ACTIVITY_PAGE_SIZE } from "./activity-api";
import { ActivityRow } from "./activity-row";
import { useActivity } from "./use-activity";

export function ActivityWorkspace() {
  const { loading, events, error, offset, page, newer, older, refresh, retry } = useActivity();

  return <main className="organization-page activity-page">
    <div className="security-heading">
      <div><h2>Activity history</h2><p>See recorded changes and requests in this workspace.</p></div>
      <button className="security-button secondary" disabled={loading} onClick={refresh}>Refresh activity</button>
    </div>
    <p className="activity-help">Newest first. Open Details to read the saved record. Times use UAE time (UTC+4).</p>
    <section className="security-panel activity-list" aria-label="Recorded activity" aria-busy={loading}>
      <div className="activity-list-heading">
        <h3>Recorded activity</h3>
        <span role="status">{loading ? "Loading activity…" : error ? "Page unavailable" : `${events.length} ${events.length === 1 ? "event" : "events"} on this page`}</span>
      </div>
      {error ? <div className="activity-message"><p className="security-alert" role="alert">{error}</p>
        <button className="security-button secondary" onClick={retry}>Try again</button></div>
        : !loading && !events.length ? <div className="activity-message"><h3>No events on this page.</h3>
          <p>{offset ? "Use Newer to return to earlier pages in this list." : "Supported changes and requests will appear here when they are recorded."}</p></div>
        : !loading && <>
          <div className="activity-columns" aria-hidden="true"><span>What happened</span><span>Who</span><span>When · UAE time</span><span>Result</span><span/></div>
          <ul className="activity-events">{events.map(event => <ActivityRow key={event.id} event={event}/>)}</ul>
        </>}
      <nav className="activity-pagination" aria-label="Activity pages">
        <button className="security-button secondary" disabled={loading || offset === 0} onClick={newer}>Newer</button>
        <span>Page {page} · up to {ACTIVITY_PAGE_SIZE} events</span>
        <button className="security-button secondary" disabled={loading || !!error || events.length < ACTIVITY_PAGE_SIZE || offset >= ACTIVITY_MAX_OFFSET} onClick={older}>Older</button>
      </nav>
      {offset >= ACTIVITY_MAX_OFFSET && <p className="activity-limit">This view has reached its page limit. Older records may still exist.</p>}
    </section>
    <p className="activity-footnote">New activity can move entries between pages. Refresh to see the latest records.</p>
    <p className="activity-footnote">This history covers supported changes and requests, not every visit. A successful event does not mean an invoice matched its purchase order.</p>
  </main>;
}
