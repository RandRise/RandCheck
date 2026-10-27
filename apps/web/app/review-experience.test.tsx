import { act, cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import Home from "./document-workspace";

const documentId = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa";
const documentItem = { id: documentId, originalFilename: "review-example.txt", sizeBytes: 128, status: "Ready", documentType: "Invoice",
  language: "English", extractor: "mock", processingMs: 1, createdAt: "2026-09-30T08:00:00Z", fields: [] };
function field(key: string, value: string, decisionStatus = "Confirmed", evidence: { page: number; quotedText?: string }[] = []) {
  return { fieldKey: key, occurrenceIndex: 0, decisionStatus, cues: [] as string[],
    machine: { id: `machine-${key}`, value, valueType: "String", confidence: .95, evidence },
    effective: { isPresent: true, value, valueType: "String", source: "Reviewer" } };
}
const initialFields = () => [field("invoice_number", "INV-10"), field("supplier", "Original supplier")];
function workspace(fields = initialFields(), status = "InProgress") { return { documentId, extractionRunId: "run-1", selectedExtractionRunId: "run-1",
  session: { id: "review-1", revisionNumber: 1, status, concurrencyToken: "version-1" }, fields }; }
function json(value: unknown, status = 200, etag = '"version-1"') { return new Response(JSON.stringify(value), { status,
  headers: { "Content-Type": "application/json", ETag: etag } }); }
function requests(initial = workspace(), override?: (url: URL, init?: RequestInit) => Response | Promise<Response> | undefined) {
  const fetcher = vi.fn((raw: string, init?: RequestInit) => {
    const url = new URL(raw); const result = override?.(url, init); if (result) return Promise.resolve(result);
    if (url.pathname === "/api/documents/work-list") return Promise.resolve(json([documentItem]));
    if (url.pathname.endsWith("/review")) return Promise.resolve(json(initial));
    throw new Error(`Unexpected request ${init?.method ?? "GET"} ${url}`);
  });
  vi.stubGlobal("fetch", fetcher); return fetcher;
}
function article(label: string) { return screen.getByRole("article", { name: label }); }
function edit(label: string, value: string, reason: string) {
  const block = within(article(label));
  fireEvent.change(block.getByLabelText("Reviewed value"), { target: { value } });
  fireEvent.change(block.getByLabelText("Reason for change or clear choices"), { target: { value: "other" } });
  fireEvent.change(block.getByRole("textbox", { name: "Reason for change or clear: Your explanation" }), { target: { value: reason } });
}
const scrollDescriptor = Object.getOwnPropertyDescriptor(HTMLElement.prototype, "scrollIntoView");

describe("guided review and unsaved corrections", () => {
  beforeEach(() => {
    window.history.replaceState(null, "", "/");
    Object.defineProperty(HTMLElement.prototype, "scrollIntoView", { configurable: true, value: vi.fn() });
  });
  afterEach(() => {
    cleanup(); vi.restoreAllMocks(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/");
    if (scrollDescriptor) Object.defineProperty(HTMLElement.prototype, "scrollIntoView", scrollDescriptor);
    else Reflect.deleteProperty(HTMLElement.prototype, "scrollIntoView");
  });

  it("retries an initial failed review load without requiring a browser refresh", async () => {
    let reads = 0;
    requests(workspace(), url => url.pathname.endsWith("/review")
      ? ++reads === 1 ? json({ detail: "Review temporarily unavailable." }, 503) : json(workspace())
      : undefined);
    render(<Home/>);
    const retry = await screen.findByRole("button", { name: "Retry review load" });
    expect(screen.queryByRole("article", { name: "Invoice number" })).toBeNull();
    fireEvent.click(retry);
    await screen.findByRole("article", { name: "Invoice number" });
    expect(reads).toBe(2);
    expect(screen.queryByRole("button", { name: "Retry review load" })).toBeNull();
    expect(screen.getByRole("button", { name: "Complete review" }).hasAttribute("disabled")).toBe(false);
  });

  it("does not present a failed-load retry for a normal unstarted review without an ETag", async () => {
    const unstarted = { ...workspace([field("invoice_number", "INV-10", "Unreviewed")]), session: null };
    requests(workspace(), url => url.pathname.endsWith("/review")
      ? new Response(JSON.stringify(unstarted), { headers: { "Content-Type": "application/json" } }) : undefined);
    render(<Home/>);
    await screen.findByText("Review not started");
    expect(screen.getByRole("button", { name: "Start review" }).hasAttribute("disabled")).toBe(false);
    expect(screen.queryByRole("button", { name: "Retry review load" })).toBeNull();
    expect(screen.queryByText(/Reload the latest review before saving/)).toBeNull();
  });

  it("preserves the value and reason drafted in another field when one correction saves", async () => {
    const saved = workspace([field("invoice_number", "INV-11", "Corrected"), initialFields()[1]]);
    requests(workspace(), (url, init) => url.pathname.endsWith("/actions") && init?.method === "POST" ? json(saved, 200, '"version-2"') : undefined);
    render(<Home/>); await screen.findByRole("article", { name: "Invoice number" });
    edit("Invoice number", "INV-11", "Invoice number checked");
    edit("Supplier", "Corrected supplier", "اسم المورد من المصدر");
    fireEvent.click(within(article("Invoice number")).getByRole("button", { name: "Save correction" }));
    await screen.findByText("Invoice number saved as corrected.");
    expect((within(article("Supplier")).getByLabelText("Reviewed value") as HTMLInputElement).value).toBe("Corrected supplier");
    expect((within(article("Supplier")).getByRole("textbox", { name: "Reason for change or clear: Your explanation" }) as HTMLInputElement).value).toBe("اسم المورد من المصدر");
    expect(within(article("Supplier")).getByText("Unsaved changes")).toBeTruthy();
    expect((within(article("Invoice number")).getByLabelText("Reason for change or clear choices") as HTMLInputElement).value).toBe("");
    expect(within(article("Invoice number")).queryByText("Unsaved changes")).toBeNull();
    expect(screen.getByRole("button", { name: "Complete review" }).hasAttribute("disabled")).toBe(true);
  });

  it("keeps a rejected correction through a stale reload and retries it with the refreshed ETag", async () => {
    let reads = 0; let actions = 0;
    const remote = workspace([field("invoice_number", "REMOTE-20"), initialFields()[1]]);
    const fetcher = requests(workspace(), (url, init) => {
      if (url.pathname.endsWith("/review")) return ++reads === 1 ? json(workspace()) : json(remote, 200, '"remote-version"');
      if (url.pathname.endsWith("/actions") && init?.method === "POST") return ++actions === 1
        ? json({ detail: "Review changed in another window." }, 412)
        : json(workspace([field("invoice_number", "LOCAL-30", "Corrected"), initialFields()[1]]), 200, '"saved-version"');
    });
    render(<Home/>); await screen.findByRole("article", { name: "Invoice number" });
    edit("Invoice number", "LOCAL-30", "Checked source locally");
    fireEvent.click(within(article("Invoice number")).getByRole("button", { name: "Save correction" }));
    await screen.findByText("Review changed in another window.");
    expect((within(article("Invoice number")).getByLabelText("Reviewed value") as HTMLInputElement).value).toBe("LOCAL-30");
    expect((within(article("Invoice number")).getByRole("textbox", { name: "Reason for change or clear: Your explanation" }) as HTMLInputElement).value).toBe("Checked source locally");
    fireEvent.click(within(article("Invoice number")).getByRole("button", { name: "Save correction" }));
    await screen.findByText("Invoice number saved as corrected.");
    const writes = fetcher.mock.calls.filter(([url, init]) => url.endsWith("/actions") && init?.method === "POST");
    expect(writes).toHaveLength(2);
    expect(new Headers(writes[1][1]?.headers).get("If-Match")).toBe('"remote-version"');
    expect(JSON.parse(String(writes[1][1]?.body))).toMatchObject({ value: "LOCAL-30", reason: "Checked source locally" });
  });

  it.each(["value", "reason"])("blocks completion for an unsaved field %s and permits it after discarding", async change => {
    requests(); render(<Home/>); await screen.findByRole("article", { name: "Invoice number" });
    expect(screen.getByRole("button", { name: "Complete review" }).hasAttribute("disabled")).toBe(false);
    const block = within(article("Invoice number"));
    if (change === "reason") fireEvent.change(block.getByLabelText("Reason for change or clear choices"), { target: { value: "other" } });
    fireEvent.change(block.getByRole("textbox", { name: change === "value" ? "Reviewed value" : "Reason for change or clear: Your explanation" }), { target: { value: "Uncommitted edit" } });
    expect(block.getByText("Unsaved changes")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Complete review" }).hasAttribute("disabled")).toBe(true);
    fireEvent.click(block.getByRole("button", { name: "Discard draft" }));
    expect((block.getByLabelText("Reviewed value") as HTMLInputElement).value).toBe("INV-10");
    expect((block.getByLabelText("Reason for change or clear choices") as HTMLInputElement).value).toBe("");
    expect(screen.getByRole("button", { name: "Complete review" }).hasAttribute("disabled")).toBe(false);
  });

  it("recovers a failed stale reload without losing the correction or reusing the old ETag", async () => {
    let reads = 0; let writes = 0;
    const fetcher = requests(workspace(), (url, init) => {
      if (url.pathname.endsWith("/review")) {
        reads++;
        return reads === 2 ? json({ detail: "Review service temporarily unavailable." }, 503)
          : json(workspace(), 200, reads === 1 ? '"version-1"' : '"retry-version"');
      }
      if (url.pathname.endsWith("/actions") && init?.method === "POST") return ++writes === 1
        ? json({ detail: "The review changed." }, 412)
        : json(workspace([field("invoice_number", "LOCAL-50", "Corrected"), initialFields()[1]]), 200, '"saved-version"');
    });
    render(<Home/>); await screen.findByRole("article", { name: "Invoice number" });
    edit("Invoice number", "LOCAL-50", "Retain while retrying");
    fireEvent.click(within(article("Invoice number")).getByRole("button", { name: "Save correction" }));
    const retry = await screen.findByRole("button", { name: "Retry review load" });
    await waitFor(() => expect(retry.hasAttribute("disabled")).toBe(false));
    expect((within(article("Invoice number")).getByLabelText("Reviewed value") as HTMLInputElement).value).toBe("LOCAL-50");
    expect(within(article("Invoice number")).getByRole("button", { name: "Save correction" }).hasAttribute("disabled")).toBe(true);
    fireEvent.click(retry);
    await waitFor(() => expect(within(article("Invoice number")).getByRole("button", { name: "Save correction" }).hasAttribute("disabled")).toBe(false));
    expect((within(article("Invoice number")).getByRole("textbox", { name: "Reason for change or clear: Your explanation" }) as HTMLInputElement).value).toBe("Retain while retrying");
    fireEvent.click(within(article("Invoice number")).getByRole("button", { name: "Save correction" }));
    await screen.findByText("Invoice number saved as corrected.");
    const mutations = fetcher.mock.calls.filter(([url, init]) => url.endsWith("/actions") && init?.method === "POST");
    expect(mutations).toHaveLength(2);
    expect(new Headers(mutations[1][1]?.headers).get("If-Match")).toBe('"retry-version"');
  });

  it("exposes unsent drafts read-only when another reviewer completed the session and permits explicit discard", async () => {
    let reads = 0;
    requests(workspace(), (url, init) => {
      if (url.pathname.endsWith("/review")) return json(workspace(initialFields(), ++reads === 1 ? "InProgress" : "Completed"));
      if (url.pathname.endsWith("/actions") && init?.method === "POST") return json({ detail: "Another reviewer completed this review." }, 412);
    });
    render(<Home/>); await screen.findByRole("article", { name: "Invoice number" });
    edit("Invoice number", "UNSENT-60", "Local reason not submitted");
    fireEvent.change(screen.getByLabelText("Field", { exact: true }), { target: { value: "payment_terms" } });
    fireEvent.change(screen.getByLabelText("Value", { exact: true }), { target: { value: "Unsent addition" } });
    fireEvent.click(within(article("Invoice number")).getByRole("button", { name: "Save correction" }));
    const recovery = within(await screen.findByRole("region", { name: "Unsaved draft recovery" }));
    expect(recovery.getByText("UNSENT-60")).toBeTruthy();
    expect(recovery.getByText("Local reason not submitted")).toBeTruthy();
    expect(recovery.getByText(/Unsent addition/)).toBeTruthy();
    expect(screen.getByText("This review was completed elsewhere. Your unsaved drafts below were not submitted; copy anything you need before discarding them.")).toBeTruthy();
    expect(screen.queryByRole("button", { name: "Save correction" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Complete review" })).toBeNull();
    expect(screen.queryByText(/All changes saved/)).toBeNull();
    fireEvent.click(recovery.getByRole("button", { name: "Discard unsaved drafts" }));
    expect(screen.queryByRole("region", { name: "Unsaved draft recovery" })).toBeNull();
    expect(screen.getByText("Review completed")).toBeTruthy();
  });

  it.each(["session", "extraction", "machine"])("does not transfer an old correction to a replacement %s after a stale response", async changed => {
    let reads = 0;
    const replacement = workspace([field("invoice_number", "REPLACEMENT-40"), initialFields()[1]]);
    if (changed === "session") replacement.session.id = "replacement-review";
    if (changed === "extraction") replacement.extractionRunId = replacement.selectedExtractionRunId = "replacement-run";
    if (changed === "machine") replacement.fields[0].machine.id = "replacement-machine";
    requests(workspace(), (url, init) => {
      if (url.pathname.endsWith("/review")) return ++reads === 1 ? json(workspace()) : json(replacement, 200, '"replacement-version"');
      if (url.pathname.endsWith("/actions") && init?.method === "POST") return json({ detail: "The review changed." }, 412);
    });
    render(<Home/>); await screen.findByRole("article", { name: "Invoice number" });
    edit("Invoice number", "OLD-DRAFT", "Reason belonging to old source");
    fireEvent.click(within(article("Invoice number")).getByRole("button", { name: "Save correction" }));
    await screen.findByText("The review changed.");
    const block = within(article("Invoice number"));
    expect((block.getByLabelText("Reviewed value") as HTMLInputElement).value).toBe("REPLACEMENT-40");
    expect((block.getByLabelText("Reason for change or clear choices") as HTMLInputElement).value).toBe("");
    expect(block.queryByText("Unsaved changes")).toBeNull();
  });

  it.each(["Field", "Line number", "Value", "Reason"])("treats a changed new-field %s as unsaved work", async label => {
    requests(); render(<Home/>);
    await screen.findByLabelText("Field", { exact: true });
    if (label === "Line number") fireEvent.change(screen.getByLabelText("Field", { exact: true }), { target: { value: "line_quantity" } });
    if (label === "Reason") fireEvent.change(screen.getByLabelText("Reason choices"), { target: { value: "other" } });
    fireEvent.change(label === "Reason" ? screen.getByRole("textbox", { name: "Reason: Your explanation" }) : screen.getByLabelText(label, { exact: true }), { target: { value: label === "Field" ? "payment_terms" : label === "Line number" ? "2" : "Draft text" } });
    expect(screen.getByRole("button", { name: "Complete review" }).hasAttribute("disabled")).toBe(true);
    fireEvent.click(screen.getByRole("button", { name: "Discard new field" }));
    expect((screen.getByLabelText("Field", { exact: true }) as HTMLSelectElement).value).toBe("");
    expect((screen.getByLabelText("Value", { exact: true }) as HTMLInputElement).value).toBe("");
    expect((screen.getByLabelText("Reason choices", { exact: true }) as HTMLInputElement).value).toBe("");
    expect(screen.queryByLabelText("Line number")).toBeNull();
    expect(screen.getByRole("button", { name: "Complete review" }).hasAttribute("disabled")).toBe(false);
  });

  it("prevents editing other values and the add-field form while a correction is pending", async () => {
    let resolve!: (value: Response) => void; const pending = new Promise<Response>(done => { resolve = done; });
    requests(workspace(), (url, init) => url.pathname.endsWith("/actions") && init?.method === "POST" ? pending : undefined);
    render(<Home/>); await screen.findByRole("article", { name: "Invoice number" });
    edit("Invoice number", "INV-11", "Checked original");
    fireEvent.click(within(article("Invoice number")).getByRole("button", { name: "Save correction" }));
    for (const label of ["Reviewed value", "Your explanation", "Field", "Value", "Reason choices"]) {
      for (const input of screen.getAllByLabelText(label, { exact: true })) expect((input as HTMLInputElement).disabled || input.closest("fieldset")?.disabled).toBe(true);
    }
    for (const select of screen.getAllByRole("combobox", { name: /choices$/ })) {
      expect((select as HTMLSelectElement).disabled).toBe(true);
    }
    await act(async () => { resolve(json(workspace([field("invoice_number", "INV-11", "Corrected"), initialFields()[1]]))); });
    expect((within(article("Supplier")).getByLabelText("Reviewed value") as HTMLInputElement).disabled).toBe(false);
  });

  it("closes an empty Other explanation when a changed value is discarded", async () => {
    const fetcher = requests(); render(<Home/>);
    const block = within(await screen.findByRole("article", { name: "Invoice number" }));
    fireEvent.change(block.getByRole("combobox", { name: "Reason for change or clear choices" }), { target: { value: "other" } });
    fireEvent.change(block.getByLabelText("Reviewed value"), { target: { value: "Draft" } });
    fireEvent.click(block.getByRole("button", { name: "Discard draft" }));
    expect((block.getByRole("combobox", { name: "Reason for change or clear choices" }) as HTMLSelectElement).value).toBe("");
    expect(block.queryByRole("textbox", { name: "Reason for change or clear: Your explanation" })).toBeNull();
    expect(fetcher.mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
  });

  it.each(["Corrected", "Cleared"])("records a selected reason only after explicit %s save", async action => {
    const fetcher = requests(workspace(), (url, init) => url.pathname.endsWith("/actions") && init?.method === "POST" ? json(workspace()) : undefined);
    render(<Home/>); const block = within(await screen.findByRole("article", { name: "Invoice number" }));
    const selectedReason = action === "Corrected" ? "Read incorrectly by the system." : "I cannot verify this value.";
    if (action === "Corrected") fireEvent.change(block.getByLabelText("Reviewed value"), { target: { value: "INV-11" } });
    fireEvent.change(block.getByLabelText("Reason for change or clear choices"), { target: { value: selectedReason } });
    expect((block.getByLabelText("Reason for change or clear choices") as HTMLSelectElement).value).toBe(selectedReason);
    expect(block.queryByRole("textbox", { name: "Reason for change or clear: Your explanation" })).toBeNull();
    expect(fetcher.mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
    fireEvent.click(block.getByRole("button", { name: action === "Corrected" ? "Save correction" : "Clear" }));
    await waitFor(() => expect(fetcher.mock.calls.some(([, init]) => init?.method === "POST")).toBe(true));
    const call = fetcher.mock.calls.find(([, init]) => init?.method === "POST")!;
    const payload = JSON.parse(String(call[1]?.body));
    expect(payload.action).toBe(action);
    expect(payload.reason).toBe(selectedReason);
    expect(new Headers(call[1]?.headers).get("If-Match")).toBe('"version-1"');
  });

  it("offers a suitable reason for clearing an earlier added field without changing it on selection", async () => {
    const legacy = { fieldKey: "legacy_note", occurrenceIndex: 0, decisionStatus: "Added",
      effective: { isPresent: true, value: "Old note", valueType: "String", source: "Reviewer" } };
    const fetcher = requests(workspace(), (url, init) => {
      if (url.pathname.endsWith("/review")) return json({ ...workspace(), fields: [legacy] });
      if (url.pathname.endsWith("/actions") && init?.method === "POST") return json({ ...workspace(), fields: [{ ...legacy, decisionStatus: "Cleared", effective: { ...legacy.effective, isPresent: false } }] });
    });
    render(<Home/>); const block = within(await screen.findByRole("article", { name: "legacy_note" }));
    fireEvent.change(block.getByLabelText("Reason to clear choices"), { target: { value: "Added by mistake." } });
    expect(block.getByText("Old note")).toBeTruthy();
    expect(fetcher.mock.calls.some(([, init]) => init?.method === "POST")).toBe(false);
    fireEvent.click(block.getByRole("button", { name: "Clear added field" }));
    await waitFor(() => expect(fetcher.mock.calls.some(([, init]) => init?.method === "POST")).toBe(true));
    const call = fetcher.mock.calls.find(([, init]) => init?.method === "POST")!;
    expect(JSON.parse(String(call[1]?.body))).toMatchObject({ action: "Cleared", fieldKey: "legacy_note", reason: "Added by mistake." });
  });

  it("shows only real returned quotations and source pages without inventing a missing page", async () => {
    requests(workspace([field("invoice_number", "INV-10", "Confirmed", [{ page: 4, quotedText: "رقم الفاتورة INV-10" }, { page: 8, quotedText: "Additional invoice evidence" }]), initialFields()[1]]));
    render(<Home/>); await screen.findByRole("article", { name: "Invoice number" });
    const invoice = within(article("Invoice number"));
    expect(invoice.getAllByRole("blockquote").map(quote => quote.textContent)).toEqual(["رقم الفاتورة INV-10", "Additional invoice evidence"]);
    expect(invoice.getByText("Page 4")).toBeTruthy(); expect(invoice.getByText("Page 8")).toBeTruthy();
    const supplier = within(article("Supplier"));
    expect(supplier.getByText("No source evidence was returned for this value.")).toBeTruthy();
    expect(supplier.queryByText(/page 1/i)).toBeNull();
  });

  it("filters unresolved fields, focuses the next one, and keeps a dirty checked field discoverable", async () => {
    requests(workspace([initialFields()[0], field("supplier", "Original supplier", "Unreviewed")]));
    render(<Home/>); await screen.findByRole("article", { name: "Invoice number" });
    expect(screen.getByText("1 of 2 extracted values checked")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Show fields"), { target: { value: "pending" } });
    expect(screen.queryByRole("article", { name: "Invoice number" })).toBeNull();
    expect(article("Supplier")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Next field needing review" }));
    await waitFor(() => expect(document.activeElement).toBe(article("Supplier")));
    fireEvent.change(screen.getByLabelText("Show fields"), { target: { value: "all" } });
    fireEvent.change(within(article("Invoice number")).getByLabelText("Reviewed value"), { target: { value: "Needs another check" } });
    fireEvent.change(screen.getByLabelText("Show fields"), { target: { value: "pending" } });
    expect(article("Invoice number")).toBeTruthy(); expect(article("Supplier")).toBeTruthy();
  });

  it("advances between pending fields when the Next button receives focus for each activation", async () => {
    requests(workspace([field("invoice_number", "INV-10", "Unreviewed"), field("supplier", "Original supplier", "Unreviewed")]));
    render(<Home/>); await screen.findByRole("article", { name: "Invoice number" });
    const next = screen.getByRole("button", { name: "Next field needing review" });
    next.focus(); fireEvent.click(next);
    await waitFor(() => expect(document.activeElement).toBe(article("Invoice number")));
    next.focus(); fireEvent.click(next);
    await waitFor(() => expect(document.activeElement).toBe(article("Supplier")));
  });
});
