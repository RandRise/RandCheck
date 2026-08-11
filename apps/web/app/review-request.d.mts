export class ApiRequestError extends Error {
  constructor(status: number, code: string | undefined, message: string);
  status: number;
  code: string | undefined;
}

export function requireSuccess(response: Response): Promise<void>;

export function readReviewResponse<T>(response: Response): Promise<{
  workspace: T;
  etag: string | null;
}>;

export function sendReviewMutation<T>(options: {
  fetcher?: typeof fetch;
  url: string;
  init: RequestInit;
  onStale: () => void | Promise<void>;
}): Promise<{
  workspace: T;
  etag: string | null;
}>;
