import type { ActivityEvent } from "./activity.types";

// These labels describe persisted audit codes, not inferred document outcomes.
// Keep unknown codes in Details rather than inventing a successful action.
const actions: Record<string, [activity: string, success: string]> = {
  "organization.created": ["Workspace setup", "Workspace created"],
  "membership.granted": ["Access grant", "Access granted"],
  "membership.changed": ["Access change", "Access changed"],
  "document.uploaded": ["Document upload", "Document uploaded"],
  "document.changed": ["Document update", "Document updated"],
  "document.database-content-removed": ["Stored document content removal", "Stored document content removed"],
  "document.deletion-completed": ["Document removal", "Document removal finished"],
  "document.source-purge-authorized": ["Original file removal permission", "Original file removal allowed"],
  "document.source-access-issued": ["Original file access", "Original file access issued"],
  "document.source-download-authorized": ["Original file download permission", "Original file download allowed"],
  "document.source-access-denied": ["Original file access", "Original file access"],
  "document.scan-clean": ["File safety scan", "File safety scan passed"],
  "processing.queued": ["Document processing", "Document queued for processing"],
  "processing.changed": ["Processing status change", "Processing status updated"],
  "review.started": ["Document review", "Document review started"],
  "review.changed": ["Review update", "Review updated"],
  "review.action-recorded": ["Review decision", "Review decision recorded"],
  "reconciliation.draft-changed": ["Comparison document selection", "Comparison document selection changed"],
  "reconciliation.draft-intent-recorded": ["Document selection request", "Document selection request recorded"],
  "reconciliation.metadata-changed": ["Comparison details", "Comparison details changed"],
  "reconciliation.case-changed": ["Comparison update", "Comparison updated"],
  "reconciliation.document-linked": ["Document link", "Document linked to a comparison"],
  "reconciliation.document-unlinked": ["Document link removal", "Document link removed from a comparison"],
  "reconciliation.evaluated": ["Comparison", "Comparison recorded"],
  "reconciliation.finding-resolved": ["Comparison decision", "Comparison decision recorded"],
  "evidence.index-changed": ["Search evidence update", "Search evidence updated"],
  "question.saved": ["Document question", "Document question recorded"],
  "retention.policy-changed": ["File-keeping settings", "File-keeping settings changed"],
  "retention.hold-placed": ["File protection", "File protection added"],
  "retention.hold-released": ["File protection release", "File protection released"],
  "quarantine.cleanup-authorized": ["Abandoned upload cleanup permission", "Abandoned upload cleanup allowed"],
  "quarantine.cleanup-failed": ["Abandoned upload cleanup", "Abandoned upload cleanup"],
  "quarantine.cleaned": ["Abandoned upload cleanup", "Abandoned upload cleaned up"],
  "request.admitted": ["Request access", "Request allowed"],
  "access.denied": ["Access request", "Access request"],
  "request.failed": ["Request", "Request"],
};

export function activityTitle(event: ActivityEvent) {
  const known = actions[event.action.toLowerCase()];
  if (!known) return "Other recorded activity";
  return event.outcome === "Succeeded" ? known[1] : known[0];
}

export function activityResult(outcome: string) {
  switch (outcome) {
    case "Succeeded": return { label: "Successful", tone: "success" };
    case "Denied": return { label: "Not allowed", tone: "denied" };
    case "Failed": return { label: "Failed", tone: "failed" };
    default: return { label: "Result not recognized", tone: "unknown" };
  }
}

export function activityActor(event: ActivityEvent) {
  switch (event.actorKind) {
    case "Human": return event.actorId ? "Person · name not recorded" : "Person · ID not recorded";
    case "System": return "System";
    case "Anonymous": return "Unidentified caller";
    default: return "Actor type not recognized";
  }
}

// Fix both timezone and numbering; browser locale must not alter saved-time meaning.
const uaeTime = new Intl.DateTimeFormat("en-GB-u-nu-latn", {
  timeZone: "Asia/Dubai", dateStyle: "medium", timeStyle: "short", hourCycle: "h23",
});

export function activityTime(timestamp: string) {
  // Reject zone-less values rather than interpreting them in the browser's timezone.
  if (!/(?:Z|[+-]\d{2}:\d{2})$/i.test(timestamp)) return null;
  const date = new Date(timestamp);
  return Number.isNaN(date.getTime()) ? null : uaeTime.format(date);
}

export function activityUtc(timestamp: string) {
  return activityTime(timestamp) ? new Date(timestamp).toISOString() : "Time not available";
}
