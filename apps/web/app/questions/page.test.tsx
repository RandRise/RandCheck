import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import QuestionsPage from "./page";

const id = (n: number) => `00000000-0000-4000-8000-${n.toString().padStart(12, "0")}`;
const source = { documentId: id(1), filename: "invoice.pdf", reviewSessionId: id(2), extractionRunId: id(3), revisionNumber: 1 };
const hit = { ...source, indexId: id(4), chunkId: id(5), hitId: id(6), rank: 1, kind: "PageText",
  content: "Total: 100.00", reviewActionId: null, spans: [{ id: id(7), pageNumber: 2, quote: "100.00" }] };
const result = { stage: "EvidenceReady", answerGenerationEnabled: false, question: "What is the total?", indexIds: [id(4)],
  retrieval: { id: id(8), status: "Retrieved", policyVersion: "lexical-or-english-v1", replayed: false, hits: [hit], failureCode: null }, answer: null };
const location = { ...source, retrievalRunId: id(8), hitId: id(6), indexId: id(4), kind: "PageText", reviewedFact: null, review: null,
  page: { pageNumber: 2, byteStart: 8, byteEnd: 14, before: "é VAT: ", quote: "100.00", after: "\nThank you" } };
const json = (value: unknown, status = 200) => ({ ok: status < 400, status, json: async () => value } as Response);

const reviewedResult = { ...result, stage: "ReviewedAnswer", answer: null,
  retrieval: { ...result.retrieval, status: "Completed", hits: [{ ...hit, kind: "ReviewedFact", content: "total[0]: 100.00" }] },
  reviewedAnswer: { status: "Answered", reasonCode: null, missingParts: [],
    claims: [{ text: "Invoice total: 100.00 AED.", citations: [{ hitId: id(6), spanId: null }] }] } };

describe("reviewed answer mode", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("submits the reviewed mode and opens the saved claim citation", async () => {
    const fetch = mockApi({ reviewed: async () => json(reviewedResult), evidence: async () => json({ ...location,
      kind: "ReviewedFact", reviewedFact: "total[0]: 100.00", page: null,
      review: { actionId: id(9), action: "Confirmed", fieldKey: "total", occurrence: 0, reason: null } }) });
    render(<QuestionsPage />); await pinAndAsk(true);
    await screen.findByText("Invoice total: 100.00 AED.");
    await userEvent.click(screen.getByRole("button", { name: /Source: invoice.pdf/ }));
    await screen.findByText("Reviewed fact — Confirmed");
    expect(fetch.mock.calls.some(([url]) => String(url).endsWith("/api/cited-questions/reviewed"))).toBe(true);
    expect(fetch.mock.calls.some(([url]) => String(url).endsWith(`/api/cited-questions/${id(8)}/hits/${id(6)}/evidence`))).toBe(true);
  });

  it("reopens the stored answer without issuing another POST", async () => {
    const fetch = mockApi({ saved: { ...reviewedResult, retrieval: { ...reviewedResult.retrieval, replayed: true } } });
    render(<QuestionsPage />);
    await userEvent.click(screen.getByText("Reopen a saved result"));
    await userEvent.type(screen.getByLabelText("Retrieval ID"), id(8));
    await userEvent.click(screen.getByRole("button", { name: "Load saved result" }));
    await screen.findByText("Invoice total: 100.00 AED.");
    expect(fetch.mock.calls.every(([, init]) => init?.method === "GET")).toBe(true);
  });

  it("shows an unsupported refusal without fabricated claims", async () => {
    mockApi({ reviewed: async () => json({ ...reviewedResult, retrieval: { ...reviewedResult.retrieval, hits: [] },
      reviewedAnswer: { status: "InsufficientEvidence", reasonCode: "unsupported_scope", missingParts: [], claims: [] } }) });
    render(<QuestionsPage />); await pinAndAsk(true);
    await screen.findByText("No supported answer");
    expect(screen.queryByText("Invoice total: 100.00 AED.")).toBeNull();
    expect(screen.getByText(/outside the supported reviewed-field questions/)).toBeTruthy();
  });

  it("creates a new request identity when switching modes and renders claim text inertly", async () => {
    const payload = '<img src=x onerror="alert(1)">';
    const fetch = mockApi({ reviewed: async () => json({ ...reviewedResult,
      reviewedAnswer: { ...reviewedResult.reviewedAnswer, claims: [{ text: payload, citations: [] }] } }) });
    render(<QuestionsPage />); await pinAndAsk();
    await screen.findByText("1 evidence hit · Not a generated answer");
    await userEvent.selectOptions(screen.getByLabelText("Question mode"), "reviewed");
    await userEvent.click(screen.getByRole("button", { name: "Answer from selected reviews" }));
    await screen.findByText(payload);
    expect(document.querySelector(".q-reviewed-answer img")).toBeNull();
    const posts = fetch.mock.calls.filter(([url, init]) => String(url).includes("/api/cited-questions") && init?.method === "POST");
    expect(JSON.parse(String(posts[0][1]?.body)).requestKey).not.toBe(JSON.parse(String(posts[1][1]?.body)).requestKey);
  });
});

describe("case sources and semantic search", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("loads a reconciliation case's frozen sources and submits those indexes", async () => {
    const fetch = mockApi(); render(<QuestionsPage />);
    await userEvent.type(screen.getByLabelText("Reconciliation case ID"), id(22));
    await userEvent.click(screen.getByRole("button", { name: "Use case sources" }));
    await screen.findByText(/Sources loaded from Procurement case/);
    await userEvent.type(screen.getByLabelText("Your question"), "What is the total?");
    await userEvent.click(screen.getByRole("button", { name: "Search selected sources" }));
    await screen.findByText("1 evidence hit · Not a generated answer");
    const post = fetch.mock.calls.find(([url]) => String(url).endsWith("/api/cited-questions"));
    expect(JSON.parse(String(post?.[1]?.body)).indexIds).toEqual([id(4)]);
  });

  it("switches to hybrid search with a new request identity", async () => {
    const fetch = mockApi(); render(<QuestionsPage />); await pinAndAsk();
    await screen.findByText("1 evidence hit · Not a generated answer");
    await userEvent.selectOptions(screen.getByLabelText("Search method"), "hybrid-nonredundant");
    await userEvent.click(screen.getByRole("button", { name: "Search selected sources" }));
    await screen.findByText("1 evidence hit · Not a generated answer");
    const posts = fetch.mock.calls.filter(([url, init]) => String(url).includes("/api/cited-questions") && init?.method === "POST");
    expect(String(posts[1][0])).toContain("/semantic");
    expect(JSON.parse(String(posts[1][1]?.body)).mode).toBe("hybrid-nonredundant");
    expect(JSON.parse(String(posts[0][1]?.body)).requestKey).not.toBe(JSON.parse(String(posts[1][1]?.body)).requestKey);
  });

  it("shows unavailable semantic search without silently falling back", async () => {
    const fetch = mockApi({ semantic: async () => json({}, 422) });
    render(<QuestionsPage />); await pinAndAsk();
    await screen.findByText("1 evidence hit · Not a generated answer");
    await userEvent.selectOptions(screen.getByLabelText("Search method"), "vector");
    await userEvent.click(screen.getByRole("button", { name: "Search selected sources" }));
    await screen.findByText(/Semantic search is not available for this question/);
    expect(fetch.mock.calls.filter(([url]) => String(url).endsWith("/api/cited-questions")).length).toBe(1);
  });
});

function mockApi(overrides: { question?: () => Promise<Response>; reviewed?: () => Promise<Response>; semantic?: () => Promise<Response>; caseSources?: () => Promise<Response>; saved?: unknown; evidence?: () => Promise<Response>; sources?: unknown } = {}) {
  return vi.spyOn(globalThis, "fetch").mockImplementation(async (input, init) => {
    const url = String(input);
    if (url.includes("/evidence")) return overrides.evidence?.() ?? json(location);
    if (url.endsWith("/api/cited-sources")) return json(overrides.sources ?? { sources: [source], nextOffset: null });
    if (url.includes("/api/cited-cases/")) return overrides.caseSources?.() ?? json({ name: "Procurement case", sources: [{ ...source, indexId: id(4) }] });
    if (url.endsWith("/api/cited-questions/semantic")) return overrides.semantic?.() ?? json(result);
    if (url.endsWith("/api/cited-indexes")) return json({ id: id(4), status: "Ready" });
    if (url.endsWith("/api/cited-questions/reviewed")) return overrides.reviewed?.() ?? json(result);
    if (url.endsWith("/api/cited-questions") && init?.method === "POST") return overrides.question?.() ?? json(result);
    if (url.endsWith(`/api/cited-questions/${id(8)}`)) return json(overrides.saved ?? { ...result, retrieval: { ...result.retrieval, replayed: true } });
    throw new Error(`Unexpected fixture route ${url}`);
  });
}
async function pinAndAsk(reviewed = false) {
  await screen.findByRole("option", { name: "invoice.pdf · review 1" });
  await userEvent.selectOptions(await screen.findByLabelText("Completed review"), id(2));
  await userEvent.click(screen.getByRole("button", { name: "Add source" }));
  await screen.findByRole("button", { name: "Remove invoice.pdf" });
    await userEvent.type(screen.getByLabelText("Your question"), "What is the total?");
  if (reviewed) await userEvent.selectOptions(screen.getByLabelText("Question mode"), "reviewed");
  await userEvent.click(screen.getByRole("button", { name: reviewed ? "Answer from selected reviews" : "Search selected sources" }));
}

describe("evidence question workspace", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("requires a selected source and pins the exact completed review before searching", async () => {
    const fetch = mockApi(); render(<QuestionsPage />);
    expect(screen.getByRole("button", { name: "Search selected sources" }).hasAttribute("disabled")).toBe(true);
    await pinAndAsk();
    await screen.findByText("1 evidence hit · Not a generated answer");
    const indexCall = fetch.mock.calls.find(([url]) => String(url).endsWith("/api/cited-indexes"))!;
    expect(JSON.parse(String(indexCall[1]?.body))).toEqual({ documentId: id(1), reviewSessionId: id(2) });
    const askCall = fetch.mock.calls.find(([url]) => String(url).endsWith("/api/cited-questions"))!;
    expect(JSON.parse(String(askCall[1]?.body))).toMatchObject({ question: "What is the total?", indexIds: [id(4)], topK: 5 });
    expect(screen.getByText("Answers from reviewed fields")).toBeTruthy();
  });

  it("opens server-authorized canonical text and highlights the exact quote", async () => {
    const fetch = mockApi(); render(<QuestionsPage />); await pinAndAsk();
    await userEvent.click(await screen.findByRole("button", { name: "Open page 2" }));
    await screen.findByText("Page 2 · canonical extracted text");
    expect(document.querySelector("mark")?.textContent).toBe("100.00");
    expect(document.querySelector(".q-canonical")?.textContent).toBe("é VAT: 100.00\nThank you");
    expect(fetch.mock.calls.some(([url]) => String(url).endsWith(`/api/cited-questions/${id(8)}/hits/${id(6)}/evidence?spanId=${id(7)}`))).toBe(true);
  });

  it("shows insufficient evidence without inventing an answer or claiming the fact is absent", async () => {
    mockApi({ question: async () => json({ ...result, stage: "InsufficientEvidence",
      retrieval: { ...result.retrieval, status: "Completed", hits: [] },
      answer: { status: "InsufficientEvidence", reasonCode: "no_relevant_evidence", message: "No matching evidence was retrieved." } }) });
    render(<QuestionsPage />); await pinAndAsk();
    await screen.findByText("Insufficient evidence");
    expect(screen.getByText(/does not prove the fact is absent/)).toBeTruthy();
    expect(screen.queryByRole("button", { name: /Open page/ })).toBeNull();
  });

  it("renders hostile source text as text and never uses source-supplied HTML or links", async () => {
    const payload = '<img src=x onerror="alert(1)"><script>alert(1)</script>';
    mockApi({ question: async () => json({ ...result, retrieval: { ...result.retrieval, hits: [{ ...hit, filename: payload, content: payload }] } }) });
    render(<QuestionsPage />); await pinAndAsk();
    await screen.findByRole("heading", { name: payload });
    expect(document.querySelector(".q-hit img,.q-hit script,.q-hit a")).toBeNull();
  });

  it("retains a request key on network retry, but creates a new one for changed question inputs", async () => {
    let attempts = 0;
    const fetch = mockApi({ question: async () => { if (++attempts === 1) throw new Error("Network unavailable"); return json(result); } });
    render(<QuestionsPage />); await pinAndAsk();
    await userEvent.click(await screen.findByRole("button", { name: "Retry same request" }));
    await screen.findByText("1 evidence hit · Not a generated answer");
    await userEvent.type(screen.getByLabelText("Your question"), " Again");
    await userEvent.click(screen.getByRole("button", { name: "Search selected sources" }));
    await screen.findByText("1 evidence hit · Not a generated answer");
    const bodies = fetch.mock.calls.filter(([url]) => String(url).endsWith("/api/cited-questions")).map(([, init]) => JSON.parse(String(init?.body)));
    expect(bodies[0].requestKey).toBe(bodies[1].requestKey);
    expect(bodies[2].requestKey).not.toBe(bodies[0].requestKey);
  });

  it("ignores a late search response after the user changes the question", async () => {
    let finish!: (value: Response) => void;
    mockApi({ question: () => new Promise(resolve => { finish = resolve; }) });
    render(<QuestionsPage />); await pinAndAsk();
    fireEvent.change(screen.getByLabelText("Your question"), { target: { value: "New question" } });
    finish(json(result));
    await waitFor(() => expect(screen.queryByText("1 evidence hit · Not a generated answer")).toBeNull());
  });

  it("clears old evidence and shows a safe error when a source becomes unavailable", async () => {
    let opens = 0;
    mockApi({ evidence: async () => ++opens === 1 ? json(location) : json({ detail: "sensitive internals" }, 409) });
    render(<QuestionsPage />); await pinAndAsk();
    await userEvent.click(await screen.findByRole("button", { name: "Open page 2" }));
    await screen.findByText("Page 2 · canonical extracted text");
    await userEvent.click(screen.getByRole("button", { name: "Open page 2" }));
    await screen.findByRole("alert");
    expect(document.querySelector("mark")).toBeNull();
    expect(screen.queryByText("sensitive internals")).toBeNull();
  });

  it("shows review-only provenance without inventing a page", async () => {
    mockApi({ question: async () => json({ ...result, retrieval: { ...result.retrieval, hits: [{ ...hit, kind: "ReviewedFact", spans: [], content: "terms[0]: Net 30" }] } }),
      evidence: async () => json({ ...location, kind: "ReviewedFact", page: null, reviewedFact: "terms[0]: Net 30",
        review: { actionId: id(9), action: "Added", fieldKey: "terms", occurrence: 0, reason: "Reviewed agreement" } }) });
    render(<QuestionsPage />); await pinAndAsk();
    await userEvent.click(await screen.findByRole("button", { name: "Open review provenance" }));
    await screen.findByText("Reviewed fact — Added");
    expect(screen.queryByText(/canonical extracted text/)).toBeNull();
    expect(screen.getByText(/No page quotation is being asserted/)).toBeTruthy();
  });

  it("reopens history by ID with a read-only request and shows its saved question", async () => {
    const fetch = mockApi(); render(<QuestionsPage />);
    await userEvent.click(screen.getByText("Reopen a saved result"));
    await userEvent.type(screen.getByLabelText("Retrieval ID"), id(8));
    await userEvent.click(screen.getByRole("button", { name: "Load saved result" }));
    await screen.findByText("Loaded from saved history");
    expect(screen.getByRole("heading", { name: "What is the total?" })).toBeTruthy();
    expect(fetch.mock.calls.every(([, init]) => init?.method === "GET")).toBe(true);
  });

  it("guides users when no completed reviews exist", async () => {
    mockApi({ sources: { sources: [], nextOffset: null } }); render(<QuestionsPage />);
    await screen.findByText(/No completed reviews are available/);
    expect(screen.getByRole("button", { name: "Add source" }).hasAttribute("disabled")).toBe(true);
  });
});
