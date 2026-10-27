// Read-only examples; these are never appended to the running preview's saved events.
const base = {
  actorId: "11111111-1111-1111-1111-111111111111", actorKind: "Human", targetType: "membership",
  targetId: "sample-membership", reasonCode: "committed", requestId: "sample-activity-request",
  beforeState: null, afterState: null,
};

export const activityExamples = [
  { ...base, id: "activity-example-success", action: "membership.changed", outcome: "Succeeded",
    beforeState: "Staff;active=True", afterState: "Admin;active=True", recordedAtUtc: "2026-10-26T16:00:00Z" },
  { ...base, id: "activity-example-denied", actorId: null, actorKind: "Anonymous", action: "access.denied",
    targetType: "request", targetId: null, outcome: "Denied", reasonCode: "membership_required", recordedAtUtc: "2026-10-26T15:59:00Z" },
  { ...base, id: "activity-example-failed", actorKind: "System", action: "request.failed", targetType: "request",
    targetId: null, outcome: "Failed", reasonCode: "service_unavailable", recordedAtUtc: "2026-10-26T15:58:00Z" },
  { ...base, id: "activity-example-unknown", action: "sample.earlier-event", outcome: "Succeeded",
    afterState: "مراجعة الفاتورة 123", recordedAtUtc: "2026-10-26T15:57:00Z" },
];
