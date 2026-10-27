import { act, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import Home from "../document-workspace";
import type { ReviewField, ReviewWorkspace } from "./review.types";
import { unableToVerifyReason } from "./guided-review.constants";

const access = vi.hoisted(() => ({ role: "Staff" }));
vi.mock("../security/enterprise-provider", async importOriginal => {
  const original = await importOriginal<typeof import("../security/enterprise-provider")>();
  return { ...original, useEnterprise: () => ({ session: { ...original.developmentSession, role: access.role } }) };
});

const documentId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const caseId = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
const pairUrl = `/?document=${documentId}&pairCase=${caseId}&pairRole=Invoice`;
const documentItem = { id: documentId, originalFilename: "invoice.txt", sizeBytes: 128,
  status: "Ready", documentType: "Invoice", createdAt: "2026-10-12T08:00:00Z" };

function field(key: string, value: string, cues: string[] = []): ReviewField {
  return { fieldKey: key, occurrenceIndex: 0, decisionStatus: "Unreviewed", cues,
    machine: { id: `machine-${key}`, value, valueType: "String", confidence: cues.length ? .4 : .98,
      evidence: [{ page: 2, quotedText: `المصدر ${value}` }] },
    effective: { isPresent: true, value, valueType: "String", source: "MachinePending" } };
}

function workspace(fields = [field("invoice_number", "INV-10"), field("total", "4400", ["LowConfidence"])]): ReviewWorkspace {
  return { documentId, extractionRunId: "run-1", selectedExtractionRunId: "run-1",
    session: { id: "review-1", revisionNumber: 1, status: "InProgress", concurrencyToken: "version-1" }, fields };
}

function json(value: unknown, status = 200, etag = '"version-1"') {
  return new Response(JSON.stringify(value), { status, headers: { "Content-Type": "application/json", ETag: etag } });
}

function harness(initial = workspace(), override?: (url: URL, init?: RequestInit) => Response | Promise<Response> | undefined) {
  let current = structuredClone(initial);
  const fetcher = vi.fn(async (raw: string, init?: RequestInit) => {
    const url = new URL(raw);
    const response = override?.(url, init);
    if (response) return response;
    if (url.pathname === "/api/documents/work-list") return json([documentItem]);
    if (url.pathname === `/api/reconciliation-cases/${caseId}`) return json({ id: caseId, name: "Office chairs",
      workflowKind: "InvoicePurchaseOrder", draftDocuments: [{ role: "Invoice", documentId }] });
    if (url.pathname.endsWith("/review")) return json(current);
    if (url.pathname.endsWith("/actions") && init?.method === "POST") {
      const body = JSON.parse(String(init.body));
      current = { ...current, fields: current.fields.map(value => value.machine?.id !== body.machineFieldValueId ? value : {
        ...value, decisionStatus: body.action,
        latestAction: { id: `action-${value.fieldKey}`, action: body.action, sequenceNumber: 1, reason: body.reason },
        effective: { ...value.effective, isPresent: body.action !== "Cleared", source: "Reviewer",
          value: body.action === "Cleared" ? undefined : body.action === "Corrected" ? body.value : value.machine!.value },
      }) };
      return json(current, 200, '"saved-version"');
    }
    throw new Error(`Unexpected request ${init?.method ?? "GET"} ${url.pathname}`);
  });
  vi.stubGlobal("fetch", fetcher);
  return { fetcher, writes: () => fetcher.mock.calls.filter(([, init]) => init?.method === "POST") };
}

const guided = () => within(screen.getByRole("region", { name: "Check one value at a time" }));

describe("low-effort value review", () => {
  beforeEach(() => { access.role = "Staff"; window.history.replaceState(null, "", pairUrl); });
  afterEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/"); });

  it("starts pair review with one warning value, no edit boxes and no automatic writes", async () => {
    const api = harness(); render(<Home/>);
    await screen.findByRole("button", { name: "Keep this value" });
    expect(guided().getAllByRole("article")).toHaveLength(1);
    expect(guided().getByRole("article", { name: "Total" })).toBeTruthy();
    expect(guided().getByRole("blockquote").textContent).toBe("المصدر 4400");
    expect(guided().getByText("Page 2")).toBeTruthy();
    expect(guided().queryByRole("textbox")).toBeNull();
    expect(screen.getByRole("button", { name: "Complete review" }).hasAttribute("disabled")).toBe(true);
    expect((await screen.findByRole("link", { name: "Return to invoice check" })).getAttribute("href")).toBe(`/invoices?case=${caseId}`);
    expect(api.writes()).toHaveLength(0);
  });

  it("keeps exactly the displayed machine value then focuses the next unchecked value", async () => {
    const api = harness(); render(<Home/>);
    fireEvent.click(await screen.findByRole("button", { name: "Keep this value" }));
    await screen.findByText("Total saved as confirmed.");
    expect(JSON.parse(String(api.writes()[0][1]?.body))).toEqual({ action: "Confirmed", machineFieldValueId: "machine-total" });
    expect(new Headers(api.writes()[0][1]?.headers).get("If-Match")).toBe('"version-1"');
    expect(guided().getByRole("heading", { name: "Invoice number" })).toBe(document.activeElement);
    expect(guided().getByText("1 value needs your review.")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Complete review" }).hasAttribute("disabled")).toBe(true);
  });

  it("offers short reasons without typing and saves the explicit correction with its source identity", async () => {
    const api = harness(); render(<Home/>);
    fireEvent.click(await screen.findByRole("button", { name: "Change it" }));
    fireEvent.change(screen.getByLabelText("Correct value"), { target: { value: "شركة النور 4400" } });
    expect(screen.getByRole("button", { name: "Save correction" }).hasAttribute("disabled")).toBe(true);
    fireEvent.click(screen.getByRole("button", { name: "The text was read wrong." }));
    fireEvent.click(screen.getByRole("button", { name: "Save correction" }));
    await screen.findByText("Total saved as corrected.");
    expect(JSON.parse(String(api.writes()[0][1]?.body))).toEqual({ action: "Corrected", machineFieldValueId: "machine-total",
      valueType: "String", value: "شركة النور 4400", reason: "The text was read wrong." });
  });

  it("does not save I can't tell or Cancel; explicit exclusion preserves the source and shows the gap", async () => {
    const original = field("invoice_number", "INV-10");
    const api = harness(workspace([original])); render(<Home/>);
    fireEvent.click(await screen.findByRole("button", { name: "I can't tell" }));
    expect(guided().getByText(/checks that need it may not run/)).toBeTruthy();
    expect(api.writes()).toHaveLength(0);
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    expect(api.writes()).toHaveLength(0);
    fireEvent.click(screen.getByRole("button", { name: "I can't tell" }));
    fireEvent.click(screen.getByRole("button", { name: "Leave this value out" }));
    await screen.findByText("Invoice number saved as cleared.");
    expect(JSON.parse(String(api.writes()[0][1]?.body))).toEqual({ action: "Cleared", machineFieldValueId: original.machine!.id, reason: unableToVerifyReason });
    expect(guided().getByText("0 missing · 1 left out. Checks that need these values may not run.")).toBeTruthy();
    expect(guided().getByText(/This does not mean the documents match/)).toBeTruthy();
    expect(screen.getByRole("button", { name: "Complete review" }).hasAttribute("disabled")).toBe(false);
    fireEvent.click(screen.getByRole("button", { name: "Show all fields and saved decisions" }));
    expect(screen.getByRole("blockquote").textContent).toBe("المصدر INV-10");
    expect(screen.getByText("Excluded")).toBeTruthy();
    expect(api.writes()).toHaveLength(1);
  });

  it("keeps a value in place and locks repeat clicks while a save is pending", async () => {
    let finish!: (response: Response) => void;
    const pending = new Promise<Response>(resolve => { finish = resolve; });
    const api = harness(workspace(), (url, init) => url.pathname.endsWith("/actions") && init?.method === "POST" ? pending : undefined);
    render(<Home/>);
    const keep = await screen.findByRole("button", { name: "Keep this value" });
    fireEvent.click(keep); fireEvent.click(keep);
    expect(api.writes()).toHaveLength(1);
    expect(keep.hasAttribute("disabled")).toBe(true);
    expect(guided().getByRole("article", { name: "Total" })).toBeTruthy();
    await act(async () => { finish(json({ detail: "Could not save." }, 503)); });
    expect(guided().getByRole("article", { name: "Total" })).toBeTruthy();
  });

  it.each(["network", "server"])("requires read-only recovery after an uncertain %s exclusion", async failure => {
    const api = harness(workspace(), (url, init) => url.pathname.endsWith("/actions") && init?.method === "POST"
      ? failure === "network" ? Promise.reject(new Error("Reply lost")) : json({ detail: "Save unavailable" }, 503) : undefined);
    render(<Home/>);
    fireEvent.click(await screen.findByRole("button", { name: "I can't tell" }));
    fireEvent.click(screen.getByRole("button", { name: "Leave this value out" }));
    const retry = await screen.findByRole("button", { name: "Retry review load" });
    expect(screen.getByRole("button", { name: "Leave this value out" }).hasAttribute("disabled")).toBe(true);
    expect(guided().getByRole("article", { name: "Total" })).toBeTruthy();
    fireEvent.click(retry);
    await waitFor(() => expect(screen.getByRole("button", { name: "Leave this value out" }).hasAttribute("disabled")).toBe(false));
    expect(api.writes()).toHaveLength(1);
  });

  it("retains a correction through a stale reload and uses the refreshed version on deliberate retry", async () => {
    let reads = 0; let writes = 0;
    const api = harness(workspace(), (url, init) => {
      if (url.pathname.endsWith("/review")) return json(workspace(), 200, ++reads === 1 ? '"old"' : '"fresh"');
      if (url.pathname.endsWith("/actions") && init?.method === "POST" && ++writes === 1) return json({ detail: "Review changed." }, 412);
    });
    render(<Home/>);
    fireEvent.click(await screen.findByRole("button", { name: "Change it" }));
    fireEvent.change(screen.getByLabelText("Correct value"), { target: { value: "4300" } });
    fireEvent.click(screen.getByRole("button", { name: "The number was read wrong." }));
    fireEvent.click(screen.getByRole("button", { name: "Save correction" }));
    await screen.findByText("Review changed.");
    expect((screen.getByLabelText("Correct value") as HTMLInputElement).value).toBe("4300");
    expect((screen.getByLabelText("Why are you changing it?") as HTMLInputElement).value).toBe("The number was read wrong.");
    fireEvent.click(screen.getByRole("button", { name: "Save correction" }));
    await screen.findByText("Total saved as corrected.");
    expect(new Headers(api.writes()[1][1]?.headers).get("If-Match")).toBe('"fresh"');
  });

  it("preserves drafts between views and cancels only the current edit without saving", async () => {
    const api = harness(); render(<Home/>);
    fireEvent.click(await screen.findByRole("button", { name: "Change it" }));
    fireEvent.change(screen.getByLabelText("Correct value"), { target: { value: "4300" } });
    fireEvent.click(screen.getByRole("button", { name: "The number was read wrong." }));
    fireEvent.click(screen.getByRole("button", { name: "All fields" }));
    const all = within(screen.getByRole("article", { name: "Total" }));
    expect((all.getByLabelText("Reviewed value") as HTMLInputElement).value).toBe("4300");
    fireEvent.click(screen.getByRole("button", { name: "Check one value at a time" }));
    expect((screen.getByLabelText("Correct value") as HTMLInputElement).value).toBe("4300");
    fireEvent.click(screen.getByRole("button", { name: "Cancel change" }));
    expect(guided().queryByRole("textbox")).toBeNull();
    expect(api.writes()).toHaveLength(0);
  });

  it("shows missing source evidence and missing values without inventing text or claiming a match", async () => {
    const noSource = field("total", "4400"); noSource.machine!.evidence = [];
    const missing: ReviewField = { fieldKey: "currency", occurrenceIndex: 0, decisionStatus: "Missing",
      cues: ["MissingMachineValue"], effective: { isPresent: false, source: "Missing" } };
    harness(workspace([noSource, missing])); render(<Home/>);
    await screen.findByRole("button", { name: "Keep this value" });
    expect(guided().getByText(/No source was returned/)).toBeTruthy();
    expect(guided().getByText(/1 missing · 0 left out/)).toBeTruthy();
    expect(guided().queryByText(/Page /)).toBeNull();
    expect(screen.getByRole("button", { name: "Complete review" }).hasAttribute("disabled")).toBe(true);
  });

  it("keeps earlier corrected values out of the queue and reachable as saved decisions", async () => {
    const saved = field("invoice_number", "WRONG-10");
    saved.decisionStatus = "Corrected"; saved.effective.value = "RIGHT-20";
    saved.latestAction = { id: "saved-correction", action: "Corrected", sequenceNumber: 1 };
    harness(workspace([saved, field("total", "4400")])); render(<Home/>);
    await screen.findByRole("button", { name: "Keep this value" });
    expect(guided().queryByRole("article", { name: "Invoice number" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "All fields" }));
    const invoice = within(screen.getByRole("article", { name: "Invoice number" }));
    expect(invoice.getByText("RIGHT-20")).toBeTruthy();
    expect(invoice.queryByRole("button", { name: "Confirm AI value" })).toBeNull();
  });

  it("does not offer guided writes to a viewer even with a pair URL", async () => {
    access.role = "Viewer";
    const api = harness(); render(<Home/>);
    await screen.findByRole("article", { name: "Total" });
    expect(screen.queryByRole("button", { name: "Keep this value" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Complete review" })).toBeNull();
    expect(api.writes()).toHaveLength(0);
  });

  it("never reconfirms an original machine value over an earlier correction with an unsaved revision", async () => {
    const saved = field("invoice_number", "WRONG-10");
    saved.decisionStatus = "Corrected"; saved.effective.value = "RIGHT-20";
    saved.latestAction = { id: "saved-correction", action: "Corrected", sequenceNumber: 1 };
    const api = harness(workspace([saved])); render(<Home/>);
    fireEvent.click(await screen.findByRole("button", { name: "All fields" }));
    fireEvent.click(screen.getByRole("button", { name: "Revise decision" }));
    fireEvent.change(screen.getByLabelText("Reviewed value"), { target: { value: "NEW-30" } });
    fireEvent.click(screen.getByRole("button", { name: "Check one value at a time" }));
    expect(guided().getByText(/already has a saved decision/)).toBeTruthy();
    expect(guided().queryByRole("button", { name: "Keep this value" })).toBeNull();
    expect(screen.getByRole("button", { name: "Complete review" }).hasAttribute("disabled")).toBe(true);
    fireEvent.click(screen.getByRole("button", { name: "All fields" }));
    expect((screen.getByLabelText("Reviewed value") as HTMLInputElement).value).toBe("NEW-30");
    expect(api.writes()).toHaveLength(0);
  });

  it("drops a draft belonging to a replaced extraction after a stale response", async () => {
    let reads = 0;
    const replacement = workspace([field("total", "5000")]);
    replacement.extractionRunId = replacement.selectedExtractionRunId = "replacement-run";
    replacement.fields[0].machine!.id = "replacement-machine";
    harness(workspace(), (url, init) => {
      if (url.pathname.endsWith("/review")) return json(++reads === 1 ? workspace() : replacement);
      if (url.pathname.endsWith("/actions") && init?.method === "POST") return json({ detail: "Source changed." }, 412);
    });
    render(<Home/>);
    fireEvent.click(await screen.findByRole("button", { name: "Change it" }));
    fireEvent.change(screen.getByLabelText("Correct value"), { target: { value: "OLD-DRAFT" } });
    fireEvent.click(screen.getByRole("button", { name: "The number was read wrong." }));
    fireEvent.click(screen.getByRole("button", { name: "Save correction" }));
    await screen.findByText("Source changed.");
    expect(guided().queryByRole("textbox")).toBeNull();
    expect(guided().getByRole("button", { name: "Keep this value" })).toBeTruthy();
    expect(guided().queryByText("OLD-DRAFT")).toBeNull();
    expect(guided().getAllByText("5000")).toHaveLength(2);
    expect(guided().getByRole("blockquote").textContent).toBe("المصدر 5000");
  });

  it("offers guided review as a choice outside a saved pair without changing historical default view", async () => {
    window.history.replaceState(null, "", "/");
    harness(); render(<Home/>);
    await screen.findByRole("article", { name: "Invoice number" });
    expect(screen.queryByRole("button", { name: "Keep this value" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Check one value at a time" }));
    expect(guided().getAllByRole("article")).toHaveLength(1);
  });
});
