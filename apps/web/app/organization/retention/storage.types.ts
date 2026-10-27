export type StoragePolicy = {
  version: string;
  documentDays: number | null;
  quarantineHours: number | null;
};

export type StorageHold = {
  id: string;
  documentId: string | null;
  reason: string;
  createdAtUtc: string;
  releasedAtUtc: string | null;
  version: string;
};

export type StoragePreview = {
  documentDeletionEnabled: boolean;
  documents: { documentId: string; createdAtUtc: string; disposition: string }[];
  nextOffset: number | null;
};

export type CleanupResult = {
  executed: boolean;
  eligible: number;
  deleted: number;
  preserved: number;
  truncated: boolean;
};
