export type Role = "Invoice" | "PurchaseOrder";

export type Draft = {
  role: Role;
  documentId: string;
};

export type Pin = Draft & {
  reviewSessionId: string;
  extractionRunId?: string;
};

export type Run = {
  id: string;
  runNumber: number;
  ruleSetVersion: string;
  status: string;
  coverage?: {
    status: string;
    conclusion: string;
    couldNotCheck: number;
  };
};

export type Case = {
  id: string;
  name: string;
  reference: string;
  status: string;
  workflowKind: string;
  draftDocuments?: Draft[];
  documents: Pin[];
  runs: Run[];
};

export type CasePage = {
  cases: Case[];
  nextOffset: number | null;
};

export type Document = {
  id: string;
  originalFilename: string;
  status: string;
  documentType?: string;
  reviewState?: string;
  latestExtractionStatus?: string | null;
};

export type ReviewChoice = {
  documentId: string;
  reviewSessionId: string;
  revisionNumber?: number;
};
