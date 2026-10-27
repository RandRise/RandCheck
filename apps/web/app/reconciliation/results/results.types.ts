export type Resolution = { id: string; sequenceNumber: number; action: string; reason: string; createdAt: string; actorId?: string };
export type Finding = {
  id: string; ruleCode: string; severity: string; leftRole?: string; rightRole?: string;
  fieldKey?: string; occurrence?: number; leftFieldSnapshotId?: string; rightFieldSnapshotId?: string;
  leftValue?: string; rightValue?: string; difference?: number; tolerance?: number;
  explanation: string; resolutions: Resolution[];
};
export type CaseRun = {
  coverage?: { status: string; conclusion: string; couldNotCheck: number; passedChecks?: number; differences?: number; totalChecks?: number; outcome?: string };
  checks?: ResultCheck[];
  id: string; runNumber: number; status: string; ruleSetVersion: string;
  sources?: { id: string; documentId: string; role: string; reviewSessionId?: string; extractionRunId?: string }[];
  fields: { documentSnapshotId?: string; role?: string; id: string; key: string; occurrence: number; value: string; page?: number; quote?: string }[];
  findings: Finding[];
};

export type ResultCheck = {
  id: string; code?: string; fieldKey: string; occurrence?: number | null; lineIdentity?: string | null;
  status: string; reasonCode?: string; explanation: string; findingId?: string | null;
  leftFieldSnapshotId?: string | null; rightFieldSnapshotId?: string | null;
  leftValue?: string | null; rightValue?: string | null; signedDifference?: number | null;
  tolerance?: number | null; unit?: string | null; currency?: string | null; basis?: string | null;
};
