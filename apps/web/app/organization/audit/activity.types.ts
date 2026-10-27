export type ActivityEvent = {
  id: string;
  actorId: string | null;
  actorKind: string;
  action: string;
  targetType: string;
  targetId: string | null;
  outcome: string;
  reasonCode: string;
  requestId: string;
  beforeState: string | null;
  afterState: string | null;
  recordedAtUtc: string;
};
