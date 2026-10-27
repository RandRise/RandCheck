import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import Home from "./document-workspace";

const account = vi.hoisted(() => ({ role: "Staff", organizationId: "org", actorId: "actor" }));
vi.mock("./security/enterprise-provider", async importOriginal => {
  const original = await importOriginal<typeof import("./security/enterprise-provider")>();
  return { ...original, useEnterprise: () => ({ session: { ...original.developmentSession, ...account } }) };
});

const id = (n: number) => `00000000-0000-4000-8000-${String(n).padStart(12, "0")}`;
const item = (n: number, reviewState = "NeedsReview", latestExtractionStatus = "Ready") => ({
  id: id(n), originalFilename: `Document ${n}.txt`, sizeBytes: 128, status: "Ready", createdAt: "2026-10-02T08:00:00Z",
  reviewState, latestExtractionStatus, selectedExtractionRunId: id(n + 100)
});
function json(value: unknown, status = 200) { return new Response(JSON.stringify(value), { status, headers: { ETag: '"version"' } }); }
function setup(items = [item(1)], status: string | null = "InProgress", override?: (path: string, init?: RequestInit) => Response | undefined) {
  const fetcher = vi.fn(async (raw: string, init?: RequestInit) => {
    const path = new URL(raw).pathname;
    const overridden = override?.(path, init); if (overridden) return overridden;
    if (path === "/api/documents/work-list") return json(items);
    const document = items.find(x => path.startsWith(`/api/documents/${x.id}/`));
    if (!document) throw new Error(`Unexpected ${path}`);
    const workspace = { documentId: document.id, selectedExtractionRunId: document.selectedExtractionRunId,
      extractionRunId: document.selectedExtractionRunId, fields: [], session: status ? { id: id(300), status, revisionNumber: 1 } : null };
    if (path.endsWith("/review")) return json(workspace);
    if (path.endsWith("/reviews")) return json({ ...workspace, session: { id: id(300), status: "InProgress", revisionNumber: 1 } });
    if (path.endsWith("/complete")) return json({ ...workspace, session: { id: id(300), status: "Completed", revisionNumber: 1 } });
    if (path.endsWith("/processing")) return json({ jobId: id(400), extractionRunId: id(500), status: document.latestExtractionStatus, attemptCount: 1 });
    if (path.endsWith("/actions")) return json(workspace);
    throw new Error(`Unexpected ${init?.method ?? "GET"} ${path}`);
  });
  vi.stubGlobal("fetch", fetcher); return fetcher;
}
beforeEach(() => { account.role = "Staff"; window.history.replaceState(null, "", "/"); });
afterEach(() => { cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/"); });

describe("simplified document work", () => {
  it("shows review work independently from replacement extraction and updates after completing", async () => {
    setup([item(1, "InProgress"), item(2, "Reviewed", "Queued"), item(3, "NeedsReview", "Failed")]);
    render(<Home/>); await screen.findByRole("button", { name: "Complete review" });
    expect(screen.getByRole("button", { name: /Document 2.*Extraction: Queued.*Reviewed/ })).toBeTruthy();
    expect(screen.getByRole("button", { name: /Document 3.*Extraction: Failed.*Needs review/ })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Complete review" }));
    await screen.findByRole("button", { name: /Document 1.*Reviewed/ });
  });

  it("polls a replacement extraction after refresh while older completed review remains readable", async () => {
    const fetcher = setup([item(1, "Reviewed", "Queued")], "Completed");
    render(<Home/>);
    await screen.findByText("Waiting for a durable worker claim.");
    expect(screen.getByRole("button", { name: /Document 1.*Reviewed/ })).toBeTruthy();
    expect(fetcher.mock.calls.some(([raw]) => raw.endsWith("/processing"))).toBe(true);
    expect(screen.getByText("Your decisions are saved and locked. Review history is available above.")).toBeTruthy();
    expect(screen.getByRole("link", { name: "Questions from this review" }).getAttribute("href")).toBe(`/questions?document=${id(1)}&review=${id(300)}`);
  });

  it("lets a refreshed active extraction summary override an older terminal processing response", async () => {
    const items = [item(1, "Reviewed", "Failed")]; let lists = 0; let reads = 0;
    setup(items, "Completed", (path, init) => {
      if (path === "/api/documents/work-list" && ++lists > 1) { items[0].latestExtractionStatus = "Queued"; return json(items); }
      if (path.endsWith("/processing")) return json({ jobId: id(400), extractionRunId: id(500), status: ++reads > 2 ? "Queued" : "Failed", attemptCount: 1 });
      if (path.endsWith("/reprocess") && init?.method === "POST") return json({ jobId: id(400), extractionRunId: id(500), status: "Failed", replayed: true }, 202);
    });
    render(<Home/>); fireEvent.click(await screen.findByRole("button", { name: "Try processing again" }));
    await screen.findByText("Waiting for a durable worker claim.");
    expect(screen.getByRole("button", { name: /Document 1.*Extraction: Queued.*Reviewed/ })).toBeTruthy();
    expect(reads).toBeGreaterThan(2);
  });

  it.each(["Staff", "Admin"])("offers daily document work to %s", async role => {
    account.role = role; setup([item(1)], null); render(<Home/>);
    await screen.findByRole("heading", { name: "Document 1.txt" });
    expect(screen.getByRole("button", { name: "Upload & extract" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Reprocess document" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Start review" })).toBeTruthy();
  });

  it.each([["due_date", "Date", 0], ["line_quantity", "Decimal", 2]])("infers %s type and correct occurrence without technical choices", async (field, type, occurrence) => {
    const fetcher = setup(); render(<Home/>); await screen.findByText("Add a missing field");
    fireEvent.change(screen.getByLabelText("Field", { exact: true }), { target: { value: field } });
    if (occurrence > 0) fireEvent.change(screen.getByLabelText("Line number"), { target: { value: String(occurrence) } });
    fireEvent.change(screen.getByLabelText("Value", { exact: true }), { target: { value: type === "Date" ? "2026-10-02" : "2.50" } });
    fireEvent.change(screen.getByLabelText("Reason choices"), { target: { value: "other" } });
    fireEvent.change(screen.getByRole("textbox", { name: "Reason: Your explanation" }), { target: { value: "Checked source" } });
    expect(screen.queryByText("Custom field settings")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Add field" }));
    await waitFor(() => expect(fetcher.mock.calls.some(([raw]) => raw.endsWith("/actions"))).toBe(true));
    const call = fetcher.mock.calls.find(([raw]) => raw.endsWith("/actions"))!;
    expect(JSON.parse(String(call[1]?.body))).toMatchObject({ fieldKey: field, valueType: type, occurrenceIndex: occurrence });
  });

  it.each(["InProgress", "Completed"])("keeps existing custom values accessible in a %s review without custom creation controls", async status => {
    const saved = { documentId: id(1), selectedExtractionRunId: id(101), extractionRunId: id(101),
      session: { id: id(300), status, revisionNumber: 1 }, fields: [{ fieldKey: "custom_reference", occurrenceIndex: 0,
        decisionStatus: "Added", effective: { isPresent: true, value: "REFERENCE-1", valueType: "Identifier", source: "Reviewer" },
        latestAction: { id: id(500), action: "Added", reason: "Earlier checked reference" } }] };
    const fetcher = setup([item(1)], status, path => path.endsWith("/review") ? json(saved) : undefined);
    render(<Home/>);
    await screen.findByText("REFERENCE-1");
    expect(screen.getByText("Earlier checked reference")).toBeTruthy();
    for (const label of ["Field key", "Field position", "Value type"]) expect(screen.queryByLabelText(label)).toBeNull();
    expect(screen.queryByText("Custom field settings")).toBeNull();
    expect(screen.queryByRole("option", { name: "Custom field" })).toBeNull();
    if (status === "InProgress") expect(screen.getByRole("option", { name: "Invoice number" })).toBeTruthy();
    else expect(screen.queryByRole("button", { name: "Add field" })).toBeNull();
    expect(fetcher.mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
  });

  it("resets the line number when changing to a header field and derives the new type", async () => {
    const fetcher = setup(); render(<Home/>); await screen.findByText("Add a missing field");
    fireEvent.change(screen.getByLabelText("Field", { exact: true }), { target: { value: "line_quantity" } });
    fireEvent.change(screen.getByLabelText("Line number"), { target: { value: "5" } });
    fireEvent.change(screen.getByLabelText("Field", { exact: true }), { target: { value: "supplier_trn" } });
    expect(screen.queryByLabelText("Line number")).toBeNull();
    fireEvent.change(screen.getByLabelText("Value", { exact: true }), { target: { value: "100000000000003" } });
    fireEvent.change(screen.getByLabelText("Reason choices"), { target: { value: "other" } });
    fireEvent.change(screen.getByRole("textbox", { name: "Reason: Your explanation" }), { target: { value: "Checked source" } });
    fireEvent.click(screen.getByRole("button", { name: "Add field" }));
    await waitFor(() => expect(fetcher.mock.calls.some(([raw]) => raw.endsWith("/actions"))).toBe(true));
    const call = fetcher.mock.calls.find(([raw]) => raw.endsWith("/actions"))!;
    expect(JSON.parse(String(call[1]?.body))).toMatchObject({ fieldKey: "supplier_trn", occurrenceIndex: 0, valueType: "Identifier" });
  });

  it.each([false, true])("saves only the explicitly selected missing-field reason, custom=%s", async custom => {
    const fetcher = setup(); render(<Home/>); await screen.findByText("Add a missing field");
    const choices = screen.getByLabelText("Reason choices") as HTMLSelectElement;
    expect(choices.value).toBe("");
    expect(screen.queryByRole("textbox", { name: "Reason: Your explanation" })).toBeNull();
    fireEvent.change(screen.getByLabelText("Field", { exact: true }), { target: { value: "payment_terms" } });
    fireEvent.change(screen.getByLabelText("Value", { exact: true }), { target: { value: "Net 30" } });
    fireEvent.change(choices, { target: { value: "Shown on the document but missed." } });
    expect(choices.value).toBe("Shown on the document but missed.");
    expect(screen.queryByRole("textbox", { name: "Reason: Your explanation" })).toBeNull();
    expect(fetcher.mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
    if (custom) {
      fireEvent.change(choices, { target: { value: "other" } });
      const reason = screen.getByRole("textbox", { name: "Reason: Your explanation" }) as HTMLInputElement;
      expect(reason.value).toBe("");
      expect(document.activeElement).toBe(reason);
      fireEvent.submit(screen.getByRole("button", { name: "Add field" }).closest("form")!);
      await screen.findByText("Value and reason are required.");
      expect(fetcher.mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
      fireEvent.change(reason, { target: { value: "أضيف من المستند" } });
      fireEvent.change(choices, { target: { value: "other" } });
      expect(reason.value).toBe("أضيف من المستند");
    }
    fireEvent.click(screen.getByRole("button", { name: "Add field" }));
    await waitFor(() => expect(fetcher.mock.calls.some(([raw]) => raw.endsWith("/actions"))).toBe(true));
    const call = fetcher.mock.calls.find(([raw]) => raw.endsWith("/actions"))!;
    expect(JSON.parse(String(call[1]?.body))).toEqual({ action: "Added", fieldKey: "payment_terms", occurrenceIndex: 0,
      valueType: "String", value: "Net 30", reason: custom ? "أضيف من المستند" : "Shown on the document but missed." });
    expect((screen.getByRole("combobox", { name: "Reason choices" }) as HTMLSelectElement).value).toBe("");
    expect(screen.queryByRole("textbox", { name: "Reason: Your explanation" })).toBeNull();
  });

  it("closes an empty Other explanation when the new-field draft is discarded", async () => {
    const fetcher = setup(); render(<Home/>); await screen.findByText("Add a missing field");
    fireEvent.change(screen.getByRole("combobox", { name: "Reason choices" }), { target: { value: "other" } });
    expect(screen.getByRole("textbox", { name: "Reason: Your explanation" })).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Value", { exact: true }), { target: { value: "Draft" } });
    fireEvent.click(screen.getByRole("button", { name: "Discard new field" }));
    expect((screen.getByRole("combobox", { name: "Reason choices" }) as HTMLSelectElement).value).toBe("");
    expect(screen.queryByRole("textbox", { name: "Reason: Your explanation" })).toBeNull();
    expect(fetcher.mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
  });
});
