export type LibraryDocument = {
  id: string;
  originalFilename: string;
  sizeBytes: number;
  status: "Processing" | "Ready" | "Failed" | "Cancelled";
  createdAt: string;
  documentType?: string;
  reviewState?: string | null;
  latestExtractionStatus?: string | null;
  reviewUsesOlderExtraction?: boolean;
};

export type LibraryQuery = { search: string; type: "All" | "Invoice" | "PurchaseOrder"; includeQuotations: boolean; offset: number };
export type LibraryPage = { documents: LibraryDocument[]; total: number; offset: number; pageSize: number };
export const LIBRARY_PAGE_SIZE = 10;
export const EMPTY_LIBRARY_QUERY: LibraryQuery = { search: "", type: "All", includeQuotations: false, offset: 0 };
