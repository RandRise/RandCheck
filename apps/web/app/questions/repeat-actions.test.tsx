import { act, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import QuestionsPage from "./page";

const id = (n: number) => `00000000-0000-4000-8000-${String(n).padStart(12, "0")}`;
const source = { documentId: id(1), filename: "invoice.pdf", reviewSessionId: id(2), extractionRunId: id(3), revisionNumber: 1 };
const result = { question: "What is the total?", stage: "EvidenceReady", answerGenerationEnabled: false, indexIds: [id(4)],
  answer: null, retrieval: { id: id(8), status: "Retrieved", policyVersion: "test", replayed: false, hits: [], failureCode: null } };
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status, headers: { "Content-Type": "application/json" } });
function deferred() { let resolve!: (value: Response) => void; const promise = new Promise<Response>(done => resolve = done); return { promise, resolve }; }
function requests(override: (url: string, init?: RequestInit) => Response | Promise<Response> | undefined) {
  const fetcher = vi.fn((url: string, init?: RequestInit) => {
    const answer = override(url, init); if (answer) return Promise.resolve(answer);
    if (url.endsWith("/api/reconciliation-cases")) return Promise.resolve(json({ cases: [], nextOffset: null }));
    if (url.endsWith("/api/cited-questions") && init?.method === "GET") return Promise.resolve(json({ questions: [] }));
    if (url.includes("/api/cited-sources")) return Promise.resolve(json({ sources: [source], nextOffset: 10 }));
    if (url.endsWith("/api/cited-indexes")) return Promise.resolve(json({ id: id(4), status: "Ready" }));
    if (url.endsWith("/api/cited-questions/reviewed")) return Promise.resolve(json(result));
    throw new Error(`Unexpected request ${url}`);
  }); vi.stubGlobal("fetch", fetcher); return fetcher;
}
beforeEach(() => window.history.replaceState(null, "", "/questions"));
afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); });

describe("question submission ownership", () => {
  it("prepares a source once when rapid clicks happen before React renders disabled controls", async () => {
    const delayed = deferred(); const fetcher = requests(url => url.endsWith("/api/cited-indexes") ? delayed.promise : undefined);
    render(<QuestionsPage/>); await screen.findByRole("option", { name: /invoice.pdf/ });
    fireEvent.change(screen.getByLabelText("Completed review"), { target: { value: source.reviewSessionId } });
    const add = screen.getByRole("button", { name: "Add source" }); act(() => { fireEvent.click(add); fireEvent.click(add); });
    expect(fetcher.mock.calls.filter(([url]) => url.endsWith("/api/cited-indexes"))).toHaveLength(1);
    await act(async () => delayed.resolve(json({ id: id(4), status: "Ready" })));
    expect(screen.getAllByRole("button", { name: "Remove invoice.pdf" })).toHaveLength(1);
  });

  it("submits once synchronously and retries the same identity after an uncertain response", async () => {
    let writes = 0; const delayed = deferred();
    const fetcher = requests(url => url.endsWith("/api/cited-questions/reviewed") ? ++writes === 1 ? delayed.promise : json(result) : undefined);
    render(<QuestionsPage/>); await screen.findByRole("option", { name: /invoice.pdf/ });
    fireEvent.change(screen.getByLabelText("Completed review"), { target: { value: source.reviewSessionId } });
    fireEvent.click(screen.getByRole("button", { name: "Add source" })); await screen.findByRole("button", { name: "Remove invoice.pdf" });
    fireEvent.change(screen.getByLabelText("Your question"), { target: { value: result.question } });
    const form = screen.getByLabelText("Your question").closest("form")!;
    act(() => { fireEvent.submit(form); fireEvent.submit(form); }); expect(writes).toBe(1);
    await act(async () => delayed.resolve(json({}, 504))); await screen.findByRole("button", { name: "Retry same request" });
    fireEvent.submit(form); await screen.findByRole("heading", { name: result.question });
    const calls = fetcher.mock.calls.filter(([url]) => url.endsWith("/api/cited-questions/reviewed"));
    expect(JSON.parse(String(calls[0][1]?.body)).requestKey).toBe(JSON.parse(String(calls[1][1]?.body)).requestKey);
  });

  it("does not let source paging abort an in-flight answer", async () => {
    const delayed = deferred(); const fetcher = requests(url => url.endsWith("/api/cited-questions/reviewed") ? delayed.promise : undefined);
    render(<QuestionsPage/>); await screen.findByRole("option", { name: /invoice.pdf/ });
    fireEvent.change(screen.getByLabelText("Completed review"), { target: { value: source.reviewSessionId } });
    fireEvent.click(screen.getByRole("button", { name: "Add source" })); await screen.findByRole("button", { name: "Remove invoice.pdf" });
    fireEvent.change(screen.getByLabelText("Your question"), { target: { value: result.question } });
    const form = screen.getByLabelText("Your question").closest("form")!;
    const more = screen.getByRole("button", { name: "Next reviews" });
    act(() => { fireEvent.submit(form); fireEvent.click(more); });
    expect(fetcher.mock.calls.some(([url]) => url.includes("offset=10"))).toBe(false);
    await act(async () => delayed.resolve(json(result))); await screen.findByRole("heading", { name: result.question });
  });

  it("ignores an abandoned answer and gives an edited question a new intent", async () => {
    const delayed = deferred(); let writes = 0;
    const fetcher = requests(url => url.endsWith("/api/cited-questions/reviewed") ? ++writes === 1 ? delayed.promise : json({ ...result, question: "What is the VAT?" }) : undefined);
    render(<QuestionsPage/>); await screen.findByRole("option", { name: /invoice.pdf/ });
    fireEvent.change(screen.getByLabelText("Completed review"), { target: { value: source.reviewSessionId } });
    fireEvent.click(screen.getByRole("button", { name: "Add source" })); await screen.findByRole("button", { name: "Remove invoice.pdf" });
    const input = screen.getByLabelText("Your question"); fireEvent.change(input, { target: { value: result.question } }); fireEvent.submit(input.closest("form")!);
    fireEvent.change(input, { target: { value: "What is the VAT?" } }); fireEvent.submit(input.closest("form")!);
    await screen.findByRole("heading", { name: "What is the VAT?" });
    await act(async () => delayed.resolve(json(result)));
    await waitFor(() => expect(screen.queryByRole("heading", { name: result.question })).toBeNull());
    const calls = fetcher.mock.calls.filter(([url]) => url.endsWith("/api/cited-questions/reviewed"));
    expect(JSON.parse(String(calls[0][1]?.body)).requestKey).not.toBe(JSON.parse(String(calls[1][1]?.body)).requestKey);
  });
});
