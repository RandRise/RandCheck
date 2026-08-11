import assert from "node:assert/strict";
import test from "node:test";

import {
  ApiRequestError,
  readReviewResponse,
  sendReviewMutation,
} from "./review-request.mjs";

const jsonResponse = (body, status, etag) => new Response(JSON.stringify(body), {
  status,
  headers: {
    "Content-Type": "application/json",
    ...(etag ? { ETag: etag } : {}),
  },
});

test("reads the exposed ETag from a review response", async () => {
  const result = await readReviewResponse(jsonResponse({ session: { id: "review-1" } }, 201, '"version-1"'));

  assert.equal(result.etag, '"version-1"');
  assert.equal(result.workspace.session.id, "review-1");
});

test("rotates If-Match through confirm, correct, add, clear, and complete", async () => {
  const actions = ["Confirmed", "Corrected", "Added", "Cleared"];
  const requests = [];
  let responseNumber = 1;
  const fetcher = async (url, init) => {
    requests.push({ url, init });
    responseNumber += 1;
    return jsonResponse({ session: { id: "review-1" } }, 200, `"version-${responseNumber}"`);
  };

  let etag = '"version-1"';
  for (const action of actions) {
    const result = await sendReviewMutation({
      fetcher,
      url: "http://localhost:8080/api/documents/document-1/reviews/review-1/actions",
      init: {
        method: "POST",
        headers: { "Content-Type": "application/json", "If-Match": etag },
        body: JSON.stringify({ action }),
      },
      onStale: async () => assert.fail("fresh ETag should not reload"),
    });
    etag = result.etag;
  }

  await sendReviewMutation({
    fetcher,
    url: "http://localhost:8080/api/documents/document-1/reviews/review-1/complete",
    init: { method: "POST", headers: { "If-Match": etag } },
    onStale: async () => assert.fail("fresh ETag should not reload"),
  });

  assert.deepEqual(
    requests.map(request => request.init.headers["If-Match"]),
    ['"version-1"', '"version-2"', '"version-3"', '"version-4"', '"version-5"'],
  );
  assert.deepEqual(
    requests.slice(0, 4).map(request => JSON.parse(request.init.body).action),
    actions,
  );
  assert.match(requests.at(-1).url, /\/complete$/);
});

test("reloads the workspace after a stale ETag on every mutation path", async () => {
  for (const path of ["actions", "complete"]) {
    let reloadCount = 0;
    await assert.rejects(
      sendReviewMutation({
        fetcher: async () => jsonResponse(
          { code: "review_concurrency_conflict", detail: "The review changed." },
          412,
        ),
        url: `http://localhost:8080/${path}`,
        init: { method: "POST", headers: { "If-Match": '"stale"' } },
        onStale: async () => { reloadCount += 1; },
      }),
      error => error instanceof ApiRequestError && error.status === 412,
    );
    assert.equal(reloadCount, 1);
  }
});
