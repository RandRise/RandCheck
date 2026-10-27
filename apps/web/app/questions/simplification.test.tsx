import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import QuestionsPage from "./page";

const account = vi.hoisted(() => ({ role: "Staff", organizationId: "org", actorId: "actor" }));
vi.mock("../security/enterprise-provider", async importOriginal => {
  const original = await importOriginal<typeof import("../security/enterprise-provider")>();
  return { ...original, useEnterprise: () => ({ session: { ...original.developmentSession, ...account } }) };
});

const id = (n: number) => `aaaaaaaa-aaaa-4aaa-8aaa-${String(n).padStart(12, "0")}`;
const source = { documentId: id(1), filename: "Reviewed invoice.pdf", reviewSessionId: id(2), extractionRunId: id(3), revisionNumber: 4 };
const saved = { id: id(8), question: "What was the reviewed total?", createdAt: "2026-10-02T08:00:00Z", sourceCount: 1, mode: "reviewed" };
const result = { question: saved.question, indexIds: [id(40)], stage: "ReviewedAnswer", answerGenerationEnabled: false,
  answer: null, retrieval: { id: id(8), status: "Completed", policyVersion: "reviewed-test", replayed: true, hits: [], failureCode: null },
  reviewedAnswer: { status: "InsufficientEvidence", reasonCode: "unsupported_scope", missingParts: [], claims: [] } };
const json = (value: unknown, status = 200) => new Response(JSON.stringify(value), { status });
function setup(override?: (url: URL, init?: RequestInit) => Response | Promise<Response> | undefined) {
  const fetcher = vi.fn((raw: string, init?: RequestInit) => {
    const url = new URL(raw); const answer = override?.(url, init); if (answer) return Promise.resolve(answer);
    if (url.pathname === "/api/cited-sources") return Promise.resolve(json({ sources: [source], nextOffset: null }));
    if (url.pathname === `/api/cited-sources/${id(1)}/reviews/${id(2)}`) return Promise.resolve(json(source));
    if (url.pathname === "/api/reconciliation-cases") return Promise.resolve(json({ cases: [{ id: id(22), name: "October supplies", reference: "REC-2026-000022", documentCount: 3 }], nextOffset: null }));
    if (url.pathname === "/api/cited-questions" && init?.method === "GET") return Promise.resolve(json({ questions: [saved] }));
    if (url.pathname === "/api/cited-indexes") return Promise.resolve(json({ id: id(4), status: "Ready" }));
    if (url.pathname === `/api/cited-questions/${id(8)}`) return Promise.resolve(json(result));
    if (url.pathname === "/api/cited-questions/reviewed" || url.pathname === "/api/cited-questions" && init?.method === "POST") return Promise.resolve(json(result));
    if (url.pathname === `/api/cited-cases/${id(22)}/sources`) return Promise.resolve(json({ id: id(22), name: "October supplies", sources: [{ ...source, indexId: id(4) }] }));
    throw new Error(`Unexpected ${init?.method ?? "GET"} ${url.pathname}`);
  });
  vi.stubGlobal("fetch", fetcher); render(<QuestionsPage/>); return fetcher;
}
beforeEach(() => { account.role = "Staff"; window.history.replaceState(null, "", "/questions"); });
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/questions"); });

describe("named question workflows", () => {
  it("defaults to reviewed answers and submits the exact explicitly prepared review", async () => {
    const fetcher = setup(); await screen.findByRole("option", { name: /Reviewed invoice.pdf/ });
    expect((screen.getByLabelText("Question mode") as HTMLSelectElement).value).toBe("reviewed");
    expect(screen.queryByText("Experimental search settings")).toBeNull();
    fireEvent.change(screen.getByLabelText("Completed review"), { target: { value: source.reviewSessionId } });
    fireEvent.click(screen.getByRole("button", { name: "Add source" })); await screen.findByRole("button", { name: "Remove Reviewed invoice.pdf" });
    fireEvent.change(screen.getByLabelText("Your question"), { target: { value: "What is the total?" } });
    fireEvent.click(screen.getByRole("button", { name: "Answer from selected reviews" })); await screen.findByRole("heading", { name: saved.question });
    const posts = fetcher.mock.calls.filter(([, init]) => init?.method === "POST");
    expect(JSON.parse(String(posts[0][1]?.body))).toEqual({ documentId: source.documentId, reviewSessionId: source.reviewSessionId });
    expect(new URL(posts[1][0]).pathname).toBe("/api/cited-questions/reviewed");
    expect(JSON.parse(String(posts[1][1]?.body)).indexIds).toEqual([id(4)]);
  });

  it("uses named case selection while case IDs remain secondary", async () => {
    const fetcher = setup(); await screen.findByRole("option", { name: /October supplies.*REC-2026-000022/ });
    expect(screen.getByText("Open a case by ID").closest("details")?.open).toBe(false);
    fireEvent.change(screen.getByLabelText("Saved case"), { target: { value: id(22) } });
    fireEvent.click(screen.getByRole("button", { name: "Use this case's documents" })); await screen.findByRole("heading", { name: "October supplies" });
    expect(screen.getByRole("button", { name: "Remove Reviewed invoice.pdf" })).toBeTruthy();
    expect(fetcher.mock.calls.filter(([, init]) => init?.method === "POST")).toHaveLength(1);
  });

  it.each(["Staff", "Admin"])("opens an exact review outside the source page without writes for %s", async role => {
    account.role = role; window.history.replaceState(null, "", `/questions?document=${id(1)}&review=${id(2)}`);
    const fetcher = setup(url => url.pathname === "/api/cited-sources" ? json({ sources: [], nextOffset: 10 }) : undefined);
    await screen.findByRole("heading", { name: source.filename });
    expect((screen.getByLabelText("Completed review") as HTMLSelectElement).value).toBe(source.reviewSessionId);
    expect(screen.getByRole("button", { name: "Add source" }).hasAttribute("disabled")).toBe(false);
    expect(fetcher.mock.calls.every(([, init]) => init?.method === "GET")).toBe(true);
  });

  it("accepts uppercase GUID links while rejecting a mismatched returned review", async () => {
    window.history.replaceState(null, "", `/questions?document=${id(1).toUpperCase()}&review=${id(2).toUpperCase()}`);
    setup(url => url.pathname.startsWith("/api/cited-sources/") ? json(source) : undefined);
    await screen.findByRole("heading", { name: source.filename });
    act(() => { window.history.pushState(null, "", `/questions?document=${id(1)}&review=${id(9)}`); window.dispatchEvent(new PopStateEvent("popstate")); });
    await screen.findByRole("alert"); expect(screen.queryByRole("region", { name: "Document context" })).toBeNull();
  });

  it("opens a saved question by name with GET and reuses only its frozen indexes", async () => {
    account.role = "Staff"; const fetcher = setup();
    await screen.findByRole("button", { name: saved.question });
    fireEvent.click(screen.getByRole("button", { name: saved.question })); await screen.findByRole("heading", { name: saved.question });
    await waitFor(() => expect(document.activeElement).toBe(screen.getByRole("heading", { name: "3. Read the answer and evidence" })));
    expect(fetcher.mock.calls.every(([, init]) => init?.method === "GET")).toBe(true);
    fireEvent.click(screen.getByRole("button", { name: "Use saved sources" }));
    fireEvent.change(screen.getByLabelText("Your question"), { target: { value: "When is it due?" } });
    fireEvent.click(screen.getByRole("button", { name: "Answer from selected reviews" })); await screen.findByRole("heading", { name: saved.question });
    const posts = fetcher.mock.calls.filter(([, init]) => init?.method === "POST");
    expect(posts).toHaveLength(1); expect(JSON.parse(String(posts[0][1]?.body)).indexIds).toEqual(result.indexIds);
  });

  it("keeps typing focus when an abandoned saved-result response arrives", async () => {
    let finish!: (response: Response) => void;
    setup(url => url.pathname === `/api/cited-questions/${id(8)}` ? new Promise(resolve => { finish = resolve; }) : undefined);
    fireEvent.click(await screen.findByRole("button", { name: saved.question }));
    const question = screen.getByLabelText("Your question");
    question.focus();
    fireEvent.change(question, { target: { value: "A new question" } });
    await act(async () => finish(json(result)));
    expect(screen.queryByRole("heading", { name: saved.question })).toBeNull();
    expect(document.activeElement).toBe(question);
  });

  it("does not move focus for a rendered result after the user starts another question", async () => {
    let focusFrame!: FrameRequestCallback;
    vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => { focusFrame = callback; return 1; });
    setup();
    fireEvent.click(await screen.findByRole("button", { name: saved.question }));
    await screen.findByRole("heading", { name: saved.question });
    const question = screen.getByLabelText("Your question");
    question.focus();
    fireEvent.change(question, { target: { value: "Keep my new draft" } });
    act(() => focusFrame(0));
    expect(document.activeElement).toBe(question);
    expect(screen.queryByRole("heading", { name: saved.question })).toBeNull();
  });

  it("keeps keyword search default and algorithms in a closed experimental disclosure", async () => {
    setup(); await screen.findByRole("option", { name: /Reviewed invoice.pdf/ });
    fireEvent.change(screen.getByLabelText("Question mode"), { target: { value: "evidence" } });
    expect(screen.getByText("Experimental search settings").closest("details")?.open).toBe(false);
    expect((screen.getByLabelText("Search method") as HTMLSelectElement).value).toBe("lexical");
  });

  it("ignores a late exact review after browser history changes the context", async () => {
    let finish!: (response: Response) => void;
    window.history.replaceState(null, "", `/questions?document=${id(1)}&review=${id(2)}`);
    const fetcher = setup(url => url.pathname === `/api/cited-sources/${id(1)}/reviews/${id(2)}` ? new Promise(resolve => { finish = resolve; }) : undefined);
    await waitFor(() => expect(fetcher.mock.calls.some(([raw]) => new URL(raw).pathname.endsWith(`/reviews/${id(2)}`))).toBe(true));
    act(() => { window.history.pushState(null, "", `/questions?result=${id(8)}`); window.dispatchEvent(new PopStateEvent("popstate")); });
    await screen.findByRole("heading", { name: saved.question }); await act(async () => finish(json(source)));
    expect(screen.queryByRole("region", { name: "Document context" })).toBeNull();
    expect(window.location.search).toBe(`?result=${id(8)}`);
  });

  it("refreshes only saved questions after asking and keeps case pagination coherent", async () => {
    const fetcher = setup(url => url.pathname === "/api/reconciliation-cases" ? json(url.searchParams.has("offset")
      ? { cases: [{ id: id(23), name: "Second supplies case", reference: "REC-2026-000023", documentCount: 2 }], nextOffset: null }
      : { cases: [{ id: id(22), name: "October supplies", reference: "REC-2026-000022", documentCount: 3 }], nextOffset: 10 }) : undefined);
    await screen.findByRole("option", { name: /October supplies/ });
    fireEvent.change(screen.getByLabelText("Completed review"), { target: { value: source.reviewSessionId } });
    fireEvent.click(screen.getByRole("button", { name: "Add source" })); await screen.findByRole("button", { name: "Remove Reviewed invoice.pdf" });
    fireEvent.change(screen.getByLabelText("Your question"), { target: { value: "What is the total?" } });
    fireEvent.click(screen.getByRole("button", { name: "Answer from selected reviews" })); await screen.findByRole("heading", { name: saved.question });
    fireEvent.click(screen.getByRole("button", { name: "Load more cases" })); await screen.findByRole("option", { name: /Second supplies case/ });
    expect(screen.getByRole("option", { name: /October supplies/ })).toBeTruthy();
    expect(fetcher.mock.calls.filter(([raw]) => new URL(raw).pathname === "/api/reconciliation-cases" && !new URL(raw).search)).toHaveLength(1);
    expect(fetcher.mock.calls.filter(([raw, init]) => new URL(raw).pathname === "/api/cited-questions" && init?.method === "GET")).toHaveLength(2);
  });

  it("owns a saved-case retry synchronously while its first page is loading", async () => {
    let reads = 0; let finish!: (response: Response) => void;
    const fetcher = setup(url => url.pathname === "/api/reconciliation-cases" ? ++reads === 1 ? json({}, 503) : new Promise(resolve => { finish = resolve; }) : undefined);
    await screen.findByRole("button", { name: "Retry saved cases" });
    const retry = screen.getByRole("button", { name: "Retry saved cases" });
    act(() => { fireEvent.click(retry); fireEvent.click(retry); });
    await waitFor(() => expect(reads).toBe(2));
    expect(retry.hasAttribute("disabled")).toBe(true);
    await act(async () => finish(json({ cases: [{ id: id(22), name: "Recovered case", reference: "REC-2026-000022", documentCount: 3 }], nextOffset: 10 })));
    await screen.findByRole("option", { name: /Recovered case/ });
    expect(screen.getByRole("button", { name: "Load more cases" }).hasAttribute("disabled")).toBe(false);
    expect(fetcher.mock.calls.filter(([raw]) => new URL(raw).pathname === "/api/reconciliation-cases")).toHaveLength(2);
  });

  it("replaces linked document guidance when adopting a different saved corpus", async () => {
    window.history.replaceState(null, "", `/questions?document=${id(1)}&review=${id(2)}`);
    setup(); await screen.findByRole("heading", { name: source.filename });
    fireEvent.click(screen.getByRole("button", { name: "Add source" })); await screen.findByText("This exact review is pinned for your question.", { exact: false });
    fireEvent.click(screen.getByRole("button", { name: saved.question })); await screen.findByRole("heading", { name: saved.question });
    fireEvent.click(screen.getByRole("button", { name: "Use saved sources" }));
    expect(screen.queryByRole("region", { name: "Document context" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Remove Reviewed invoice.pdf" })).toBeNull();
    expect(screen.getByText("1 source from your saved result.", { exact: false })).toBeTruthy();
  });
});
