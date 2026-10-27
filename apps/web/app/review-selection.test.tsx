import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import Home from "./document-workspace";

const documents = ["a", "b"].map(id => ({ id, originalFilename: `invoice-${id}.txt`,
  sizeBytes: 512, status: "Ready", documentType: "Invoice", language: "English",
  extractor: "mock", processingMs: 1, createdAt: "2026-09-26T08:00:00Z", fields: [] }));

function workspace(id: string, status: string | null = "InProgress", value = `VALUE-${id}`) {
  return { documentId: id, extractionRunId: `run-${id}`, selectedExtractionRunId: `run-${id}`,
    session: status ? { id: `review-${id}`, revisionNumber: 1, status, concurrencyToken: `version-${id}` } : undefined,
    fields: [{ fieldKey: "invoice_number", occurrenceIndex: 0, decisionStatus: "Confirmed", cues: [],
      machine: { id: `field-${id}`, value, valueType: "String", confidence: 1, evidence: [{ page: 1 }] },
      effective: { isPresent: true, value, valueType: "String", source: "Reviewer" } }] };
}

function json(body: unknown, status = 200, etag = '"version"') {
  return { ok: status >= 200 && status < 300, status,
    headers: new Headers({ ETag: etag }), json: async () => body } as Response;
}

function deferred() {
  let resolve!: (response: Response) => void;
  const promise = new Promise<Response>(done => { resolve = done; });
  return { promise, resolve };
}

async function select(id: string) {
  fireEvent.click(await screen.findByRole("button", { name: new RegExp(`invoice-${id}\\.txt`) }));
}

function expectB() {
  expect(screen.getByRole("heading", { name: "invoice-b.txt" })).toBeTruthy();
  expect((screen.getByLabelText("Reviewed value") as HTMLInputElement).value).toBe("VALUE-b");
  expect(screen.queryByText("VALUE-a")).toBeNull();
}

describe("document selection isolates asynchronous review work", () => {
  beforeEach(() => window.history.replaceState(null, "", "/"));
  afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });

  it("ignores the initial document review when it arrives after another selection", async () => {
    const delayed = deferred();
    const fetcher = vi.fn((url: string) => {
      if (url.endsWith("/api/documents/work-list")) return Promise.resolve(json(documents));
      if (url.endsWith("/a/review")) return delayed.promise;
      return Promise.resolve(json(workspace("b")));
    });
    vi.stubGlobal("fetch", fetcher);
    render(<Home/>);
    await waitFor(() => expect(fetcher.mock.calls.some(([url]) => url.endsWith("/a/review"))).toBe(true));
    await select("b");
    await screen.findByDisplayValue("VALUE-b");
    await act(async () => { delayed.resolve(json(workspace("a"))); });
    expectB();
  });

  it("ignores a delayed review selection after a newer document has loaded", async () => {
    const delayed = deferred();
    let aReads = 0;
    vi.stubGlobal("fetch", vi.fn((url: string) => {
      if (url.endsWith("/api/documents/work-list")) return Promise.resolve(json(documents));
      if (url.endsWith("/a/review")) return ++aReads === 1 ? Promise.resolve(json(workspace("a"))) : delayed.promise;
      return Promise.resolve(json(workspace("b")));
    }));
    render(<Home/>);
    await screen.findByDisplayValue("VALUE-a");
    await select("b"); await screen.findByDisplayValue("VALUE-b");
    await select("a");
    await select("b"); await screen.findByDisplayValue("VALUE-b");
    await act(async () => { delayed.resolve(json(workspace("a"))); });
    expectB();
  });

  it("does not mistake a revisit to the same document for the abandoned selection", async () => {
    const delayed = deferred();
    let aReads = 0;
    const fetcher = vi.fn((url: string) => {
      if (url.endsWith("/api/documents/work-list")) return Promise.resolve(json(documents));
      if (url.endsWith("/a/review")) return ++aReads === 1 ? delayed.promise : Promise.resolve(json(workspace("a", "InProgress", "LATEST-a")));
      return Promise.resolve(json(workspace("b")));
    });
    vi.stubGlobal("fetch", fetcher);
    render(<Home/>);
    await waitFor(() => expect(aReads).toBe(1));
    await select("b"); await screen.findByDisplayValue("VALUE-b");
    await select("a"); await screen.findByDisplayValue("LATEST-a");
    await act(async () => { delayed.resolve(json(workspace("a"))); });
    expect(screen.getByRole("heading", { name: "invoice-a.txt" })).toBeTruthy();
    expect((screen.getByLabelText("Reviewed value") as HTMLInputElement).value).toBe("LATEST-a");
  });

  it.each(["Start review", "Save correction", "Complete review"])(
    "keeps the current selection when a pending %s finishes for the previous document", async action => {
      const delayed = deferred();
      const fetcher = vi.fn((url: string, init?: RequestInit) => {
        if (init?.method === "POST") return delayed.promise;
        if (url.endsWith("/api/documents/work-list")) return Promise.resolve(json(documents));
        return Promise.resolve(json(workspace(url.includes("/a/") ? "a" : "b",
          url.includes("/a/") && action === "Start review" ? null : "InProgress")));
      });
      vi.stubGlobal("fetch", fetcher);
      render(<Home/>);
      const control = await screen.findByRole("button", { name: action });
      if (action === "Save correction") {
        fireEvent.change(screen.getByLabelText("Reviewed value"), { target: { value: "CORRECTED-a" } });
        fireEvent.change(screen.getByLabelText("Reason for change or clear choices"), { target: { value: "other" } });
        fireEvent.change(screen.getByRole("textbox", { name: "Reason for change or clear: Your explanation" }), { target: { value: "Checked original" } });
      }
      fireEvent.click(control);
      await waitFor(() => expect(fetcher.mock.calls.some(([, init]) => init?.method === "POST")).toBe(true));
      await select("b"); await screen.findByDisplayValue("VALUE-b");
      expect(screen.getByRole("button", { name: "Complete review" }).hasAttribute("disabled")).toBe(false);
      await act(async () => { delayed.resolve(json(workspace("a", action === "Complete review" ? "Completed" : "InProgress", "CORRECTED-a"))); });
      expectB();
      expect(screen.queryByText(/Review started\.|Review completed and locked\.|saved as corrected\./)).toBeNull();
      expect(screen.getByRole("button", { name: "Complete review" }).hasAttribute("disabled")).toBe(false);
    });

  it("does not reload an abandoned review or show its conflict after a stale mutation", async () => {
    const delayed = deferred();
    const fetcher = vi.fn((url: string, init?: RequestInit) => {
      if (init?.method === "POST") return delayed.promise;
      if (url.endsWith("/api/documents/work-list")) return Promise.resolve(json(documents));
      return Promise.resolve(json(workspace(url.includes("/a/") ? "a" : "b")));
    });
    vi.stubGlobal("fetch", fetcher);
    render(<Home/>);
    fireEvent.click(await screen.findByRole("button", { name: "Confirm AI value" }));
    await select("b"); await screen.findByDisplayValue("VALUE-b");
    await act(async () => { delayed.resolve(json({ detail: "A has changed in another window" }, 412)); });
    expectB();
    expect(screen.queryByText("A has changed in another window")).toBeNull();
    expect(fetcher.mock.calls.filter(([url]) => url.endsWith("/a/review"))).toHaveLength(1);
  });

  it("does not attach an old processing status to the newly selected document", async () => {
    const delayed = deferred();
    const fetcher = vi.fn((url: string) => {
      if (url.endsWith("/api/documents/work-list")) return Promise.resolve(json([{ ...documents[0], status: "Processing" }, documents[1]]));
      if (url.endsWith("/a/processing")) return delayed.promise;
      if (url.endsWith("/processing")) return Promise.resolve(json({}, 404));
      return Promise.resolve(json(workspace("b")));
    });
    vi.stubGlobal("fetch", fetcher);
    render(<Home/>);
    await waitFor(() => expect(fetcher.mock.calls.some(([url]) => url.endsWith("/a/processing"))).toBe(true));
    await select("b"); await screen.findByDisplayValue("VALUE-b");
    await act(async () => { delayed.resolve(json({ jobId: "job-a", extractionRunId: "run-a", status: "Queued", attemptCount: 0 })); });
    expectB();
    expect(screen.queryByText("Waiting for a durable worker claim.")).toBeNull();
    expect(screen.queryByRole("button", { name: /^Cancel$/ })).toBeNull();
  });

  it("shows a completed background upload in the list without replacing the current review", async () => {
    const delayedUpload = deferred();
    const added = { ...documents[0], id: "c", originalFilename: "invoice-c.txt" };
    let uploaded = false;
    const fetcher = vi.fn((url: string, init?: RequestInit) => {
      if (url.endsWith("/api/documents") && init?.method === "POST") return delayedUpload.promise;
      if (url.endsWith("/api/documents/work-list")) return Promise.resolve(json(uploaded ? [...documents, added] : documents));
      return Promise.resolve(json(workspace(url.includes("/a/") ? "a" : url.includes("/b/") ? "b" : "c")));
    });
    vi.stubGlobal("fetch", fetcher);
    const { container } = render(<Home/>);
    await screen.findByDisplayValue("VALUE-a");
    const input = container.querySelector<HTMLInputElement>('input[type="file"]')!;
    const form = input.closest("form")!;
    const reset = vi.spyOn(form, "reset");
    await userEvent.upload(input, new File(["New invoice"], "invoice-c.txt", { type: "text/plain" }));
    fireEvent.submit(form);
    await waitFor(() => expect(fetcher.mock.calls.some(([, init]) => init?.method === "POST")).toBe(true));
    await select("b"); await screen.findByDisplayValue("VALUE-b");
    await act(async () => { uploaded = true; delayedUpload.resolve(json(added, 201)); });
    await screen.findByRole("button", { name: /invoice-c\.txt/ });
    expectB();
    // user-event installs its own file value getter; jsdom's native reset cannot
    // remove it. Verify the submitted form receives the browser reset operation.
    expect(reset).toHaveBeenCalledOnce();
  });

  it("preserves a correction and its ETag when a background refresh returns older review data", async () => {
    const delayedReprocess = deferred();
    const delayedCorrection = deferred();
    const delayedReview = deferred();
    let reviewReads = 0;
    let actionPosts = 0;
    const corrected = workspace("a", "InProgress", "CORRECTED-a");
    const fetcher = vi.fn((url: string, init?: RequestInit) => {
      if (url.endsWith("/reprocess")) return delayedReprocess.promise;
      if (url.endsWith("/actions")) return ++actionPosts === 1 ? delayedCorrection.promise : Promise.resolve(json(corrected, 200, '"confirmed-version"'));
      if (url.endsWith("/api/documents/work-list")) return Promise.resolve(json(documents));
      if (url.endsWith("/a/review")) return ++reviewReads === 1 ? Promise.resolve(json(workspace("a"))) : delayedReview.promise;
      throw new Error(`Unexpected request: ${url}`);
    });
    vi.stubGlobal("fetch", fetcher);
    render(<Home/>);
    await screen.findByDisplayValue("VALUE-a");
    fireEvent.click(screen.getByText("Processing details", { selector: "summary" }));
    fireEvent.click(screen.getByRole("button", { name: "Reprocess document" }));
    fireEvent.change(screen.getByLabelText("Reviewed value"), { target: { value: "CORRECTED-a" } });
    fireEvent.change(screen.getByLabelText("Reason for change or clear choices"), { target: { value: "other" } });
    fireEvent.change(screen.getByRole("textbox", { name: "Reason for change or clear: Your explanation" }), { target: { value: "Checked original" } });
    fireEvent.click(screen.getByRole("button", { name: "Save correction" }));
    await waitFor(() => expect(actionPosts).toBe(1));
    await act(async () => { delayedReprocess.resolve(json({ jobId: "job-a", extractionRunId: "run-a", status: "Ready", attemptCount: 1 })); });
    await waitFor(() => expect(reviewReads).toBe(2));
    await act(async () => { delayedCorrection.resolve(json(corrected, 200, '"corrected-version"')); });
    await screen.findByDisplayValue("CORRECTED-a");
    await act(async () => { delayedReview.resolve(json(workspace("a"), 200, '"old-version"')); });
    expect((screen.getByLabelText("Reviewed value") as HTMLInputElement).value).toBe("CORRECTED-a");
    fireEvent.click(screen.getByRole("button", { name: "Confirm AI value" }));
    await waitFor(() => expect(actionPosts).toBe(2));
    const latest = fetcher.mock.calls.filter(([url]) => url.endsWith("/actions"))[1];
    expect(new Headers(latest[1]?.headers).get("If-Match")).toBe('"corrected-version"');
  });
});
