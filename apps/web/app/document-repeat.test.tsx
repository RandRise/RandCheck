import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import Home from "./document-workspace";

const account = vi.hoisted(() => ({ role: "Staff", organizationId: "org", actorId: "actor" }));
vi.mock("./security/enterprise-provider", async importOriginal => {
  const original = await importOriginal<typeof import("./security/enterprise-provider")>();
  return { ...original, useEnterprise: () => ({ session: { ...original.developmentSession, ...account } }) };
});

const docs = ["a", "b"].map(id => ({ id, originalFilename: `invoice-${id}.txt`, sizeBytes: 2,
  status: "Ready", createdAt: "2026-10-02T00:00:00Z", fields: [] }));
const field = { fieldKey: "invoice_number", occurrenceIndex: 0, decisionStatus: "Unreviewed", cues: [],
  machine: { id: "machine", value: "INV-1", valueType: "String", confidence: 1, evidence: [] },
  effective: { isPresent: true, value: "INV-1", valueType: "String", source: "MachinePending" } };
function workspace(id = "a", saved = false) { return { documentId: id, selectedExtractionRunId: "run", extractionRunId: "run",
  session: { id: "review", status: "InProgress", revisionNumber: 1, concurrencyToken: "version" },
  fields: [{ ...field, ...(saved ? { decisionStatus: "Confirmed", latestAction: { id: "action", action: "Confirmed", sequenceNumber: 1 } } : {}) }] }; }
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status,
  headers: { "Content-Type": "application/json", ETag: '"version"' } });
function deferred() { let resolve!: (value: Response) => void; const promise = new Promise<Response>(done => resolve = done); return { promise, resolve }; }
function requests(override: (url: string, init?: RequestInit) => Response | Promise<Response> | undefined, saved = false) {
  const fetcher = vi.fn((url: string, init?: RequestInit) => {
    const answer = override(url, init); if (answer) return Promise.resolve(answer);
    if (url.endsWith("/api/documents/work-list")) return Promise.resolve(json(docs));
    if (url.endsWith("/review")) return Promise.resolve(json(workspace(url.includes("/b/") ? "b" : "a", saved)));
    throw new Error(`Unexpected request ${url}`);
  }); vi.stubGlobal("fetch", fetcher); return fetcher;
}
function postCalls(fetcher: ReturnType<typeof requests>, suffix: string) {
  return fetcher.mock.calls.filter(([url, init]) => url.endsWith(suffix) && init?.method === "POST");
}
const key = (call: unknown[]) => new Headers((call[1] as RequestInit).headers).get("X-Operation-Key");
beforeEach(() => {
  account.role = "Staff"; window.history.replaceState(null, "", "/"); sessionStorage.clear();
  let count = 0; vi.stubGlobal("crypto", { randomUUID: () => `00000000-0000-4000-8000-${String(++count).padStart(12, "0")}` });
});
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });

describe("document repeat action protection", () => {
  it("rejects rapid upload submissions synchronously and keeps a new file after a late acknowledgement", async () => {
    const delayed = deferred(); const fetcher = requests((url, init) => url.endsWith("/api/documents") && init?.method === "POST" ? delayed.promise : undefined);
    const { container } = render(<Home/>); await screen.findByRole("article", { name: "Invoice number" });
    const input = container.querySelector<HTMLInputElement>('input[type="file"]')!;
    await userEvent.upload(input, new File(["aa"], "first.txt", { type: "text/plain" }));
    const form = input.closest("form")!; const reset = vi.spyOn(form, "reset");
    act(() => { fireEvent.submit(form); fireEvent.submit(form); });
    await waitFor(() => expect(postCalls(fetcher, "/api/documents")).toHaveLength(1));
    fireEvent.click(screen.getByRole("button", { name: /invoice-b/ }));
    await screen.findByRole("heading", { name: "invoice-b.txt" });
    fireEvent.change(input, { target: { files: [new File(["bb"], "next.txt", { type: "text/plain" })] } });
    await act(async () => delayed.resolve(json(docs[0], 202)));
    expect(reset).not.toHaveBeenCalled(); expect(input.files?.[0].name).toBe("next.txt");
    expect(screen.getByRole("heading", { name: "invoice-b.txt" })).toBeTruthy();
    expect(postCalls(fetcher, "/api/documents")).toHaveLength(1);
  });

  it("reuses the exact mounted file intent after an uncertain upload, then rotates after acknowledgement", async () => {
    let writes = 0; const fetcher = requests((url, init) => url.endsWith("/api/documents") && init?.method === "POST"
      ? ++writes === 1 ? Promise.reject(new Error("Connection lost")) : json(docs[0], 202) : undefined);
    const { container } = render(<Home/>); await screen.findByRole("article", { name: "Invoice number" });
    const input = container.querySelector<HTMLInputElement>('input[type="file"]')!; const file = new File(["aa"], "same.txt", { type: "text/plain" });
    await userEvent.upload(input, file); fireEvent.submit(input.closest("form")!);
    await screen.findByText(/Retry the same file/);
    fireEvent.submit(input.closest("form")!); await screen.findByText(/Document queued/);
    await userEvent.upload(input, file); fireEvent.submit(input.closest("form")!); await waitFor(() => expect(writes).toBe(3));
    const posts = postCalls(fetcher, "/api/documents"); expect(key(posts[0])).toBe(key(posts[1])); expect(key(posts[2])).not.toBe(key(posts[1]));
  });

  it("uses a new mounted intent for a different File object with identical metadata when hashing is unavailable", async () => {
    const fetcher = requests((url, init) => url.endsWith("/api/documents") && init?.method === "POST" ? json({ detail: "Service unavailable" }, 503) : undefined);
    const { container } = render(<Home/>); await screen.findByRole("article", { name: "Invoice number" });
    const input = container.querySelector<HTMLInputElement>('input[type="file"]')!;
    await userEvent.upload(input, new File(["aa"], "same.txt", { type: "text/plain" })); fireEvent.submit(input.closest("form")!);
    await screen.findByText(/Retry the same file/);
    await userEvent.upload(input, new File(["bb"], "same.txt", { type: "text/plain" })); fireEvent.submit(input.closest("form")!);
    await waitFor(() => expect(postCalls(fetcher, "/api/documents")).toHaveLength(2));
    const posts = postCalls(fetcher, "/api/documents"); expect(key(posts[1])).not.toBe(key(posts[0]));
    expect(sessionStorage.getItem("document-upload:org:actor")).toBeNull();
  });

  it("uses file bytes for durable intent recovery across remount and distinguishes equal metadata", async () => {
    let count = 0;
    const digest = vi.fn(async (_algorithm: string, bytes: ArrayBuffer) => {
      // A deterministic digest seam verifies the bytes supplied to WebCrypto.
      return new Uint8Array(32).fill(new Uint8Array(bytes)[0]).buffer;
    });
    vi.stubGlobal("crypto", { subtle: { digest }, randomUUID: () => `00000000-0000-4000-8000-${String(++count).padStart(12, "0")}` });
    const fetcher = requests((url, init) => url.endsWith("/api/documents") && init?.method === "POST" ? json({ detail: "Service unavailable" }, 503) : undefined);
    async function submit(container: HTMLElement, contents: string) {
      await screen.findByRole("article", { name: "Invoice number" });
      const input = container.querySelector<HTMLInputElement>('input[type="file"]')!;
      await userEvent.upload(input, new File([contents], "same.txt", { type: "text/plain" }));
      fireEvent.submit(input.closest("form")!); await screen.findByText(/Retry the same file/);
    }
    let view = render(<Home/>); await submit(view.container, "aa");
    const stored = JSON.parse(sessionStorage.getItem("document-upload:org:actor")!);
    expect(stored.fingerprint).toContain(":616161"); view.unmount();
    view = render(<Home/>); await submit(view.container, "aa");
    const posts = postCalls(fetcher, "/api/documents"); expect(key(posts[0])).toBe(key(posts[1]));
    await submit(view.container, "bb"); const changed = postCalls(fetcher, "/api/documents");
    expect(key(changed[2])).not.toBe(key(changed[1]));
    expect(Array.from(new Uint8Array(digest.mock.calls[0][1]))).toEqual([97, 97]);
    expect(Array.from(new Uint8Array(digest.mock.calls[2][1]))).toEqual([98, 98]);
  });

  it("keeps the original reprocess key on uncertainty and blocks overlapping processing writes", async () => {
    let writes = 0; const delayed = deferred();
    const fetcher = requests((url, init) => url.endsWith("/reprocess") && init?.method === "POST"
      ? ++writes === 1 ? json({ detail: "Service unavailable" }, 503) : delayed.promise : undefined);
    render(<Home/>); fireEvent.click(await screen.findByText("Processing details", { selector: "summary" })); const button = screen.getByRole("button", { name: "Reprocess document" });
    fireEvent.click(button); await screen.findByText(/Retry to check this processing request/);
    act(() => { fireEvent.click(button); fireEvent.click(button); });
    expect(writes).toBe(2); const posts = postCalls(fetcher, "/reprocess"); expect(key(posts[0])).toBe(key(posts[1]));
    await act(async () => delayed.resolve(json({ jobId: "job", extractionRunId: "run", status: "Ready", attemptCount: 1 })));
    await screen.findByText(/Reprocessing queued/); expect(sessionStorage.getItem("document-reprocess:org:actor:a")).toBeNull();
  });

  it("reports a terminal processing replay honestly and reloads latest status without queuing again", async () => {
    const fetcher = requests((url, init) => url.endsWith("/reprocess") && init?.method === "POST"
      ? json({ jobId: "old-job", extractionRunId: "old-run", status: "Ready", replayed: true })
      : url.endsWith("/processing") ? json({ jobId: "old-job", extractionRunId: "old-run", status: "Ready", attemptCount: 1 }) : undefined);
    render(<Home/>); fireEvent.click(await screen.findByText("Processing details", { selector: "summary" })); fireEvent.click(screen.getByRole("button", { name: "Reprocess document" }));
    await screen.findByText(/Existing processing request found/);
    expect(screen.queryByText(/Reprocessing queued/)).toBeNull(); expect(screen.getByText("Extraction completed.")).toBeTruthy();
    expect(postCalls(fetcher, "/reprocess")).toHaveLength(1);
    expect(fetcher.mock.calls.filter(([url]) => url.endsWith("/processing"))).toHaveLength(1);
  });

  it("starts a new reprocess intent only after an explicit definitive-conflict recovery action", async () => {
    let writes = 0; const fetcher = requests((url, init) => url.endsWith("/reprocess") && init?.method === "POST"
      ? ++writes === 1 ? json({ code: "operation_payload_changed", detail: "Extractor settings changed." }, 409)
      : json({ jobId: "job", extractionRunId: "run", status: "Ready" }) : undefined);
    render(<Home/>); fireEvent.click(await screen.findByText("Processing details", { selector: "summary" })); fireEvent.click(screen.getByRole("button", { name: "Reprocess document" }));
    const fresh = await screen.findByRole("button", { name: "Start a new processing request" });
    expect(writes).toBe(1); expect(screen.getByText(/This request cannot be retried/)).toBeTruthy();
    fireEvent.click(fresh); await screen.findByText(/Reprocessing queued/);
    const posts = postCalls(fetcher, "/reprocess"); expect(key(posts[1])).not.toBe(key(posts[0]));
  });

  it("permits a separate upload after a tombstone conflict without silently replacing retry identity", async () => {
    let writes = 0; const fetcher = requests((url, init) => url.endsWith("/api/documents") && init?.method === "POST"
      ? ++writes === 1 ? json({ code: "operation_source_unavailable", detail: "Original source was removed." }, 409) : json(docs[0], 202) : undefined);
    const { container } = render(<Home/>); await screen.findByRole("article", { name: "Invoice number" });
    const input = container.querySelector<HTMLInputElement>('input[type="file"]')!;
    await userEvent.upload(input, new File(["aa"], "same.txt", { type: "text/plain" })); fireEvent.submit(input.closest("form")!);
    const fresh = await screen.findByRole("button", { name: "Start a separate upload" });
    expect(writes).toBe(1); fireEvent.click(fresh); expect(writes).toBe(1);
    fireEvent.submit(input.closest("form")!); await screen.findByText(/Document queued/);
    const posts = postCalls(fetcher, "/api/documents"); expect(key(posts[1])).not.toBe(key(posts[0]));
  });

  it.each(["Reviewer", "Viewer", "None"])("keeps upload controls outside the %s work path", async role => {
    account.role = role; const fetcher = requests(() => undefined);
    const { container } = render(<Home/>); await screen.findByRole("heading", { name: "Documents" });
    expect(container.querySelector('input[type="file"]')).toBeNull();
    expect(screen.queryByRole("button", { name: "Upload & extract" })).toBeNull();
    expect(postCalls(fetcher, "/api/documents")).toHaveLength(0);
  });

  it("requires an explicit revision of a saved decision and cancellation preserves saved history", async () => {
    const fetcher = requests(() => undefined, true); render(<Home/>);
    const block = within(await screen.findByRole("article", { name: "Invoice number" }));
    expect(block.queryByLabelText("Reviewed value")).toBeNull();
    fireEvent.click(block.getByRole("button", { name: "Revise decision" }));
    fireEvent.change(block.getByLabelText("Reviewed value"), { target: { value: "LOCAL" } });
    fireEvent.click(block.getByRole("button", { name: "Cancel revision" }));
    expect(block.queryByLabelText("Reviewed value")).toBeNull();
    expect(postCalls(fetcher, "/actions")).toHaveLength(0); expect(screen.getByRole("button", { name: "Complete review" }).hasAttribute("disabled")).toBe(false);
  });

  it("blocks uncertain review resubmission until a read reload, preserving the correction draft", async () => {
    const fetcher = requests((url, init) => url.endsWith("/actions") && init?.method === "POST" ? Promise.reject(new Error("Connection lost")) : undefined);
    render(<Home/>); const block = within(await screen.findByRole("article", { name: "Invoice number" }));
    fireEvent.change(block.getByLabelText("Reviewed value"), { target: { value: "تصحيح INV-2" } });
    fireEvent.change(block.getByLabelText("Reason for change or clear choices"), { target: { value: "other" } });
    fireEvent.change(block.getByRole("textbox", { name: "Reason for change or clear: Your explanation" }), { target: { value: "حسب المصدر" } });
    fireEvent.click(block.getByRole("button", { name: "Save correction" }));
    const reload = await screen.findByRole("button", { name: "Retry review load" });
    expect(block.getByRole("button", { name: "Save correction" }).hasAttribute("disabled")).toBe(true);
    fireEvent.click(block.getByRole("button", { name: "Save correction" })); expect(postCalls(fetcher, "/actions")).toHaveLength(1);
    fireEvent.click(reload); await waitFor(() => expect(block.getByRole("button", { name: "Save correction" }).hasAttribute("disabled")).toBe(false));
    expect((block.getByLabelText("Reviewed value") as HTMLInputElement).value).toBe("تصحيح INV-2");
    expect((block.getByRole("textbox", { name: "Reason for change or clear: Your explanation" }) as HTMLInputElement).value).toBe("حسب المصدر");
    expect(postCalls(fetcher, "/actions")).toHaveLength(1);
  });
});
