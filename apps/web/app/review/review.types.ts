export type MachineValue = { id: string; value: string; valueType: string; confidence: number | null; evidence: { page: number; quotedText?: string }[] };
export type ReviewDecision = { id: string; action: string; value?: string; valueType?: string; reason?: string; sequenceNumber: number };
export type ReviewField = {
  fieldKey: string; occurrenceIndex: number; machine?: MachineValue; latestAction?: ReviewDecision;
  effective: { isPresent: boolean; value?: string; valueType?: string; source: string }; decisionStatus: string; cues: string[];
};
export type ReviewWorkspace = {
  documentId: string; selectedExtractionRunId: string; extractionRunId: string;
  session?: { id: string; revisionNumber: number; status: string; concurrencyToken: string };
  fields: ReviewField[];
};
