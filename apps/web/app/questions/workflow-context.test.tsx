import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { EnterpriseProvider, type Session } from "../security/enterprise-provider";
import QuestionsPage from "./page";

vi.mock("next/navigation", () => ({ usePathname: () => "/questions", useRouter: () => ({ replace: vi.fn() }) }));
const id = (n: number) => `00000000-0000-4000-8000-${n.toString().padStart(12, "0")}`;
const config = { mode: "Entra", tenantId: id(101), clientId: id(102),
  scope: `api://${id(103)}/DocumentIntelligence.Access`, api: "http://localhost:8080" };
const source = { documentId: id(1), filename: "Current invoice.pdf", reviewSessionId: id(2), extractionRunId: id(3), revisionNumber: 20 };
const result = { stage: "InsufficientEvidence", answerGenerationEnabled: false, question: "Original saved question?", indexIds: [id(40), id(41)],
  retrieval: { id: id(8), status: "Completed", policyVersion: "lexical-or-english-v1", replayed: true, hits: [], failureCode: null },
  answer: { status: "InsufficientEvidence", reasonCode: "no_relevant_evidence", message: "No matching evidence was retrieved." } };
function json(value: unknown, status = 200) { return new Response(JSON.stringify(value), { status, headers: { "Content-Type": "application/json" } }); }
function setup(role: string = "Admin", admin = false,
  override?: (url: URL, init?: RequestInit) => Response | Promise<Response> | undefined) {
  const member = { organizationId: id(104), organizationName: "Example organization", actorId: id(105), displayName: "Test user",
    tenantId: config.tenantId, role };
  const fetcher = vi.fn((raw: string | URL | Request, init?: RequestInit) => {
    const url = new URL(String(raw));
    if (url.pathname.endsWith("/session")) return Promise.resolve(json(member));
    const response = override?.(url, init); if (response) return Promise.resolve(response);
    if (url.pathname === "/api/reconciliation-cases") return Promise.resolve(json({ cases: [], nextOffset: null }));
    if (url.pathname === "/api/cited-questions") return Promise.resolve(json({ questions: [] }));
    if (url.pathname === "/api/cited-sources") return Promise.resolve(json({ sources: [source], nextOffset: null }));
    if (url.pathname === "/api/cited-indexes") return Promise.resolve(json({ id: id(4), status: "Ready" }));
    if (url.pathname === `/api/cited-questions/${id(8)}`) return Promise.resolve(json(result));
    throw new Error(`Unexpected fixture ${init?.method ?? "GET"} ${url.pathname}`);
  });
  vi.stubGlobal("fetch", fetcher);
  const identity = { initialize: vi.fn(async () => true), token: vi.fn(async () => "test-token"), signIn: vi.fn(async () => {}), signOut: vi.fn(async () => {}) };
  render(<EnterpriseProvider config={config} identityFactory={async () => identity}><QuestionsPage/></EnterpriseProvider>);
  return fetcher;
}

describe("questions source context and saved recovery", () => {
  beforeEach(() => window.history.replaceState(null, "", "/questions"));
  afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/questions"); });

  it.each([
    { role: "Staff", admin: false, prepare: true }, { role: "Staff", admin: true, prepare: true },
    { role: "Staff", admin: false, prepare: true }, { role: "Staff", admin: true, prepare: true },
    { role: "Staff", admin: false, prepare: true }, { role: "Staff", admin: true, prepare: true },
  ] as const)("keeps $role preparation access independent of administration=$admin", async ({ role, admin, prepare }) => {
    setup(role, admin);
    await screen.findByRole("option", { name: "Current invoice.pdf · review 20" });
    fireEvent.change(screen.getByLabelText("Completed review"), { target: { value: source.reviewSessionId } });
    expect(screen.getByRole("button", { name: "Add source" }).hasAttribute("disabled")).toBe(!prepare);
    fireEvent.change(screen.getByLabelText("Reconciliation case ID"), { target: { value: id(22) } });
    expect(screen.getByRole("button", { name: "Use this case's documents" }).hasAttribute("disabled")).toBe(!prepare);
  });

  it.each([false, true])("denies a None document role regardless of administration=%s without loading private data", async admin => {
    window.history.replaceState(null, "", `/questions?result=${id(8)}`);
    const fetcher = setup("None", admin);
    await screen.findByRole("heading", { name: "Access needs attention." });
    expect(fetcher.mock.calls.some(([url]) => String(url).includes("/api/cited-"))).toBe(false);
    expect(screen.queryByRole("heading", { name: result.question })).toBeNull();
  });

  it("reopens a result URL with GET only and preserves the saved index identities even without hits", async () => {
    window.history.replaceState(null, "", `/questions?result=${id(8)}`);
    const fetcher = setup();
    await screen.findByRole("heading", { name: result.question });
    expect(screen.getByRole("link", { name: "Reopen this result" }).getAttribute("href")).toBe(`/questions?result=${id(8)}`);
    expect(fetcher.mock.calls.every(([, init]) => !init?.method || init.method === "GET")).toBe(true);
    expect(screen.getByText(id(40), { exact: false })).toBeTruthy();
    expect(screen.getByText(id(41), { exact: false })).toBeTruthy();
  });

  it("lets a Viewer reuse exactly the saved source indexes without preparing new sources", async () => {
    window.history.replaceState(null, "", `/questions?result=${id(8)}`);
    const fetcher = setup("Staff", false, (url, init) => url.pathname === "/api/cited-questions/reviewed" && init?.method === "POST"
      ? json({ ...result, question: "Follow-up question?", retrieval: { ...result.retrieval, id: id(9), replayed: false } }) : undefined);
    await screen.findByRole("heading", { name: result.question });
    fireEvent.click(screen.getByRole("button", { name: "Use saved sources" }));
    fireEvent.change(screen.getByLabelText("Your question"), { target: { value: "Follow-up question?" } });
    fireEvent.click(screen.getByRole("button", { name: "Answer from selected reviews" }));
    await screen.findByRole("heading", { name: "Follow-up question?" });
    const posts = fetcher.mock.calls.filter(([, init]) => init?.method === "POST");
    expect(posts).toHaveLength(1);
    expect(String(posts[0][0])).toContain("/api/cited-questions");
    expect(JSON.parse(String(posts[0][1]?.body)).indexIds).toEqual(result.indexIds);
  });

  it("keeps a saved result separate from the manually selected corpus until sources are explicitly reused", async () => {
    const fetcher = setup("Admin", false, (url, init) => url.pathname === "/api/cited-questions/reviewed" && init?.method === "POST"
      ? json({ ...result, question: "Question for current invoice?", indexIds: [id(4)], retrieval: { ...result.retrieval, id: id(9) } }) : undefined);
    await screen.findByRole("option", { name: "Current invoice.pdf · review 20" });
    fireEvent.change(screen.getByLabelText("Completed review"), { target: { value: source.reviewSessionId } });
    fireEvent.click(screen.getByRole("button", { name: "Add source" }));
    await screen.findByRole("button", { name: "Remove Current invoice.pdf" });
    fireEvent.click(screen.getByText("Reopen a saved result"));
    fireEvent.change(screen.getByLabelText("Retrieval ID"), { target: { value: id(8) } });
    fireEvent.click(screen.getByRole("button", { name: "Load saved result" }));
    await screen.findByRole("heading", { name: result.question });
    expect(screen.getByRole("button", { name: "Remove Current invoice.pdf" })).toBeTruthy();
    expect(screen.getByText(id(40), { exact: false })).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Your question"), { target: { value: "Question for current invoice?" } });
    fireEvent.click(screen.getByRole("button", { name: "Answer from selected reviews" }));
    await screen.findByRole("heading", { name: "Question for current invoice?" });
    const ask = fetcher.mock.calls.find(([url, init]) => String(url).endsWith("/api/cited-questions/reviewed") && init?.method === "POST");
    expect(JSON.parse(String(ask?.[1]?.body)).indexIds).toEqual([id(4)]);
  });

  it("ignores a late result A after browser history has opened result B", async () => {
    let finish!: (response: Response) => void;
    window.history.replaceState(null, "", `/questions?result=${id(8)}`);
    const fetcher = setup("Admin", false, url => url.pathname === `/api/cited-questions/${id(8)}`
      ? new Promise(resolve => { finish = resolve; })
      : url.pathname === `/api/cited-questions/${id(9)}` ? json({ ...result, question: "Result B?", retrieval: { ...result.retrieval, id: id(9) } }) : undefined);
    await waitFor(() => expect(fetcher.mock.calls.some(([url]) => String(url).endsWith(`/api/cited-questions/${id(8)}`))).toBe(true));
    act(() => { window.history.pushState(null, "", `/questions?result=${id(9)}`); window.dispatchEvent(new PopStateEvent("popstate")); });
    await screen.findByRole("heading", { name: "Result B?" });
    await act(async () => { finish(json(result)); });
    expect(screen.queryByRole("heading", { name: result.question })).toBeNull();
    expect(screen.getByRole("heading", { name: "Result B?" })).toBeTruthy();
    expect(window.location.search).toBe(`?result=${id(9)}`);
  });

  it("does not request an invalid saved-result URL or fall back to unrelated results", async () => {
    window.history.replaceState(null, "", "/questions?result=invalid-result");
    const fetcher = setup();
    await screen.findByRole("alert");
    expect(fetcher.mock.calls.some(([url]) => String(url).includes("/api/cited-questions/"))).toBe(false);
    expect(screen.queryByRole("heading", { name: result.question })).toBeNull();
  });

  it("clears result A when browser history opens an inaccessible saved result B", async () => {
    window.history.replaceState(null, "", `/questions?result=${id(8)}`);
    setup("Admin", false, url => url.pathname === `/api/cited-questions/${id(9)}` ? json({ detail: "Private server details" }, 404) : undefined);
    await screen.findByRole("heading", { name: result.question });
    act(() => { window.history.pushState(null, "", `/questions?result=${id(9)}`); window.dispatchEvent(new PopStateEvent("popstate")); });
    await screen.findByRole("alert");
    expect(screen.queryByRole("heading", { name: result.question })).toBeNull();
    expect(screen.queryByText("Private server details")).toBeNull();
    expect(window.location.search).toBe(`?result=${id(9)}`);
  });

  it("opens linked case metadata without automatically preparing its source indexes", async () => {
    window.history.replaceState(null, "", `/questions?case=${id(22)}`);
    const fetcher = setup("Admin", false, url => url.pathname === `/api/reconciliation-cases/${id(22)}` ? json({ id: id(22), name: "Linked procurement case" }) : undefined);
    await screen.findByRole("heading", { name: "Linked procurement case" });
    expect(screen.queryByLabelText("Completed review")).toBeNull();
    expect(fetcher.mock.calls.some(([url]) => new URL(String(url)).pathname === "/api/cited-sources")).toBe(false);
    expect(screen.getByRole("link", { name: "Back to comparison" }).getAttribute("href")).toBe(`/reconciliation?case=${id(22)}`);
    fireEvent.click(screen.getByRole("button", { name: "Choose other documents" }));
    fireEvent.change(screen.getByLabelText("Reconciliation case ID"), { target: { value: id(23) } });
    expect(screen.getByRole("link", { name: "Back to comparison" }).getAttribute("href")).toBe(`/reconciliation?case=${id(22)}`);
    expect(fetcher.mock.calls.every(([, init]) => !init?.method || init.method === "GET")).toBe(true);
  });

  it("prepares both exact case reviews with one explicit action and no global review browsing", async () => {
    window.history.replaceState(null, "", `/questions?case=${id(22)}`);
    const po = { ...source, filename: "Saved purchase order.pdf", documentId: id(6), reviewSessionId: id(7), extractionRunId: id(10), revisionNumber: 3 };
    const fetcher = setup("Staff", false, (url, init) => {
      if (url.pathname === `/api/reconciliation-cases/${id(22)}`) return json({ id: id(22), name: "Saved invoice pair" });
      if (url.pathname === `/api/cited-cases/${id(22)}/sources`) return json({ id: id(22), name: "Saved invoice pair", version: id(24), sources: [{ ...source, indexId: id(4) }, { ...po, indexId: id(5) }] });
      if (url.pathname === "/api/cited-questions/reviewed" && init?.method === "POST") return json(result);
    });
    await screen.findByRole("heading", { name: "Saved invoice pair" });
    expect(fetcher.mock.calls.every(([, init]) => !init?.method || init.method === "GET")).toBe(true);
    fireEvent.click(screen.getByRole("button", { name: "Use this case's documents" }));
    await screen.findByRole("button", { name: "Remove Saved purchase order.pdf" });
    expect(screen.getByText("Review 20")).toBeTruthy();
    expect(screen.getByText("Review 3")).toBeTruthy();
    expect(screen.queryByLabelText("Completed review")).toBeNull();
    fireEvent.change(screen.getByLabelText("Your question"), { target: { value: "Compare the totals" } });
    fireEvent.click(screen.getByRole("button", { name: "Answer from selected reviews" }));
    await screen.findByRole("heading", { name: result.question });
    const posts = fetcher.mock.calls.filter(([, init]) => init?.method === "POST");
    expect(posts).toHaveLength(2);
    expect(String(posts[0][0])).toContain(`/api/cited-cases/${id(22)}/sources`);
    expect(JSON.parse(String(posts[1][1]?.body)).indexIds).toEqual([id(4), id(5)]);
    expect(fetcher.mock.calls.some(([url]) => new URL(String(url)).pathname === "/api/cited-sources")).toBe(false);
  });

  it("keeps a linked case preparation failure recoverable without using unrelated reviews", async () => {
    window.history.replaceState(null, "", `/questions?case=${id(22)}`);
    setup("Staff", false, url => url.pathname === `/api/reconciliation-cases/${id(22)}`
      ? json({ id: id(22), name: "Saved invoice pair" })
      : url.pathname === `/api/cited-cases/${id(22)}/sources` ? json({}, 409) : undefined);
    await screen.findByRole("heading", { name: "Saved invoice pair" });
    fireEvent.click(screen.getByRole("button", { name: "Use this case's documents" }));
    await screen.findByRole("alert");
    expect(screen.getByRole("button", { name: "Use this case's documents" }).hasAttribute("disabled")).toBe(false);
    expect(screen.getByText("0 sources selected")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Answer from selected reviews" }).hasAttribute("disabled")).toBe(true);
  });

  it("leaves case-only controls when reusing and then clearing a saved question's sources", async () => {
    window.history.replaceState(null, "", `/questions?case=${id(22)}`);
    setup("Staff", false, url => url.pathname === `/api/reconciliation-cases/${id(22)}`
      ? json({ id: id(22), name: "Saved invoice pair" }) : undefined);
    await screen.findByRole("heading", { name: "Saved invoice pair" });
    fireEvent.click(screen.getByText("Reopen a saved result"));
    fireEvent.change(screen.getByLabelText("Retrieval ID"), { target: { value: id(8) } });
    fireEvent.click(screen.getByRole("button", { name: "Load saved result" }));
    await screen.findByRole("heading", { name: result.question });
    fireEvent.click(screen.getByRole("button", { name: "Use saved sources" }));
    await screen.findByLabelText("Find a reviewed document");
    expect(screen.queryByText("Use the completed reviews currently saved with this case. An older comparison may use different reviews.")).toBeNull();
    expect(screen.getByText("2 sources selected")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Clear saved sources" }));
    expect(screen.getByText("0 sources selected")).toBeTruthy();
    expect(screen.getByLabelText("Completed review")).toBeTruthy();
  });

  it("replaces review pages and searches the server while retaining already selected versions", async () => {
    const second = { ...source, filename: "Later invoice.pdf", documentId: id(6), reviewSessionId: id(7) };
    const fetcher = setup("Staff", false, url => {
      if (url.pathname !== "/api/cited-sources") return undefined;
      return url.searchParams.has("q") || url.searchParams.has("offset")
        ? json({ sources: [second], nextOffset: null }) : json({ sources: [source], nextOffset: 10 });
    });
    await screen.findByRole("option", { name: "Current invoice.pdf · review 20" });
    fireEvent.change(screen.getByLabelText("Completed review"), { target: { value: source.reviewSessionId } });
    fireEvent.click(screen.getByRole("button", { name: "Add source" }));
    await screen.findByRole("button", { name: "Remove Current invoice.pdf" });
    fireEvent.click(screen.getByRole("button", { name: "Next reviews" }));
    await screen.findByRole("option", { name: "Later invoice.pdf · review 20" });
    expect(screen.queryByRole("option", { name: "Current invoice.pdf · review 20" })).toBeNull();
    expect(screen.getByRole("button", { name: "Remove Current invoice.pdf" })).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Find a reviewed document"), { target: { value: "Later invoice" } });
    fireEvent.click(screen.getByRole("button", { name: "Search reviews" }));
    await screen.findByRole("option", { name: "Later invoice.pdf · review 20" });
    const search = fetcher.mock.calls.map(([url]) => new URL(String(url))).find(url => url.searchParams.get("q") === "Later invoice");
    expect(search).toBeTruthy();
    expect(search!.searchParams.has("offset")).toBe(false);
    expect(screen.getByRole("button", { name: "Previous reviews" }).hasAttribute("disabled")).toBe(true);
    expect(fetcher.mock.calls.filter(([, init]) => init?.method === "POST")).toHaveLength(1);
  });

  it("keeps old sources after failed case switching but removes their old case association", async () => {
    const fetcher = setup("Admin", false, (url, init) => {
      if (url.pathname === `/api/cited-cases/${id(22)}/sources`) return json({ id: id(22), name: "Old procurement case", version: id(24), sources: [{ ...source, indexId: id(4) }] });
      if (url.pathname === `/api/cited-cases/${id(23)}/sources`) return json({}, 409);
      if (url.pathname === "/api/cited-questions/reviewed" && init?.method === "POST") return json(result);
    });
    await screen.findByLabelText("Reconciliation case ID");
    fireEvent.change(screen.getByLabelText("Reconciliation case ID"), { target: { value: id(22) } });
    fireEvent.click(screen.getByRole("button", { name: "Use this case's documents" }));
    await screen.findByRole("region", { name: "Case context" });
    fireEvent.click(screen.getByRole("button", { name: "Choose other documents" }));
    fireEvent.change(screen.getByLabelText("Reconciliation case ID"), { target: { value: id(23) } });
    fireEvent.click(screen.getByRole("button", { name: "Use this case's documents" }));
    await screen.findByText(/Your previous source selection is unchanged/);
    expect(screen.queryByRole("region", { name: "Case context" })).toBeNull();
    expect(screen.getByRole("button", { name: "Remove Current invoice.pdf" })).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Your question"), { target: { value: "Use the retained source?" } });
    fireEvent.click(screen.getByRole("button", { name: "Answer from selected reviews" }));
    await screen.findByRole("heading", { name: result.question });
    const ask = fetcher.mock.calls.find(([url, init]) => String(url).endsWith("/api/cited-questions/reviewed") && init?.method === "POST");
    expect(JSON.parse(String(ask?.[1]?.body)).indexIds).toEqual([id(4)]);
  });

  it("retries loading available reviews without losing a drafted question", async () => {
    let reads = 0;
    setup("Admin", false, url => url.pathname === "/api/cited-sources"
      ? ++reads === 1 ? json({}, 503) : json({ sources: [source], nextOffset: null }) : undefined);
    await screen.findByRole("button", { name: "Retry loading reviews" });
    fireEvent.change(screen.getByLabelText("Your question"), { target: { value: "Keep this draft" } });
    fireEvent.click(screen.getByRole("button", { name: "Retry loading reviews" }));
    await screen.findByRole("option", { name: "Current invoice.pdf · review 20" });
    expect((screen.getByLabelText("Your question") as HTMLTextAreaElement).value).toBe("Keep this draft");
    expect(reads).toBe(2);
  });
});
