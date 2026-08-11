export class ApiRequestError extends Error {
  constructor(status, code, message) {
    super(message);
    this.name = "ApiRequestError";
    this.status = status;
    this.code = code;
  }
}

export async function requireSuccess(response) {
  if (response.ok) return;

  const problem = await response.json().catch(() => ({}));
  throw new ApiRequestError(
    response.status,
    problem.code,
    problem.detail ?? `Request failed (${response.status})`,
  );
}

export async function readReviewResponse(response) {
  await requireSuccess(response);
  return {
    workspace: await response.json(),
    etag: response.headers.get("etag"),
  };
}

export async function sendReviewMutation({ fetcher = fetch, url, init, onStale }) {
  try {
    return await readReviewResponse(await fetcher(url, init));
  } catch (error) {
    if (error instanceof ApiRequestError && error.status === 412) {
      await onStale();
    }

    throw error;
  }
}
