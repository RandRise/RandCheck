import type { CaseRun, Finding, ResultCheck } from "../results/results.types";

export type ReportCheck = Omit<ResultCheck, "signedDifference" | "tolerance"> & { signedDifference?: string | null; tolerance?: string | null };
export type ReportFinding = Omit<Finding, "difference" | "tolerance"> & { difference?: string | null; tolerance?: string | null };
export type ReportSource = NonNullable<CaseRun["sources"]>[number] & { sha256: string; outcome?: string; rowCoverageStatus?: string };
export type ReportField = CaseRun["fields"][number] & { sourceKind?: string | null; reviewActionId?: string | null; machineFieldValueId?: string | null };
export type ReportRun = Omit<CaseRun, "checks" | "findings" | "sources" | "fields"> & {
  checks: ReportCheck[]; findings: ReportFinding[]; sources: ReportSource[]; fields: ReportField[];
  evaluatedAt?: string; evaluationActorId?: string; orderScope?: string;
  tolerances: Record<string, string>;
};
export type ReviewReport = {
  schemaVersion: 1; snapshotId: string; caseId: string; reference: string; name: string;
  caseVersion: string; capturedAtUtc: string; run: ReportRun;
};
export type ReportFormat = "html" | "csv";
