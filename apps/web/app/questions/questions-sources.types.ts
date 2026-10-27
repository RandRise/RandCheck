export type Source = {
  documentId: string;
  filename: string;
  reviewSessionId: string;
  extractionRunId: string;
  revisionNumber: number;
};

export type PinnedSource = Source & { indexId: string };
export type SourceList = { sources: Source[]; nextOffset: number | null };
