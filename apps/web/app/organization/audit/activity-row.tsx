import { ChevronDown } from "lucide-react";
import { activityActor, activityResult, activityTime, activityTitle, activityUtc } from "./activity-labels";
import type { ActivityEvent } from "./activity.types";

export function ActivityRow({ event }: { event: ActivityEvent }) {
  const title = activityTitle(event);
  const result = activityResult(event.outcome);
  const time = activityTime(event.recordedAtUtc);
  const details = [
    ["Action code", event.action], ["Actor type", event.actorKind],
    ["Actor ID", event.actorId ?? "Not recorded"], ["Target type", event.targetType],
    ["Target ID", event.targetId ?? "Not recorded"], ["Recorded result", event.outcome],
    ["Reason code", event.reasonCode], ["Request ID", event.requestId],
    ["Before", event.beforeState ?? "Not recorded"], ["After", event.afterState ?? "Not recorded"],
    ["Event ID", event.id], ["Saved timestamp (original)", event.recordedAtUtc], ["Time in UTC", activityUtc(event.recordedAtUtc)],
  ];

  return <li><details className="activity-event">
    <summary>
      <span className="activity-what"><span className="activity-mobile-label">What happened</span><strong>{title}</strong></span>
      <span className="activity-who"><span className="activity-mobile-label">Who</span>{activityActor(event)}
        {event.actorId && <small title={event.actorId}>ID: {event.actorId.slice(0, 8)}{event.actorId.length > 8 ? "…" : ""}</small>}
      </span>
      <span><span className="activity-mobile-label">When · UAE time</span>{time ? <time dateTime={event.recordedAtUtc}>{time}</time> : <span>Time not available</span>}</span>
      <span><span className="activity-mobile-label">Result</span><span className={`activity-result ${result.tone}`}>{result.label}</span></span>
      <span className="activity-details-label">Details<ChevronDown size={16} aria-hidden="true" focusable="false"/></span>
    </summary>
    <div className="audit-detail activity-evidence">
      <p>These are the saved details for this event. IDs are references, not person names.</p>
      <dl>{details.map(([label, value]) => <div key={label}><dt>{label}</dt><dd dir="auto">{value}</dd></div>)}</dl>
    </div>
  </details></li>;
}
