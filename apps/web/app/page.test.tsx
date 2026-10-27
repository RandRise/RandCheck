import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import Home from "./document-workspace";

const document = {
  id: "document-1",
  originalFilename: "invoice.txt",
  sizeBytes: 512,
  status: "Ready",
  documentType: "Invoice",
  language: "English",
  extractor: "mock-invoice-v1",
  processingMs: 12,
  createdAt: "2026-08-11T08:00:00Z",
  fields: [],
} as const;

const workspace = {
  documentId: document.id,
  selectedExtractionRunId: "run-1",
  extractionRunId: "run-1",
  fields: [],
};

const activeWorkspace = {
  ...workspace,
  session: {
    id: "review-1",
    revisionNumber: 1,
    status: "InProgress",
    concurrencyToken: "version-1",
  },
};

function jsonResponse(body: unknown, status = 200, etag?: string): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: new Headers(etag ? { ETag: etag } : {}),
    json: async () => body,
  } as Response;
}

describe("review workspace forms", () => {
  it("preserves Arabic values and correction reasons with automatic text direction", async () => {
    const value = "خلال ٣٠ يوماً — AED";
    const field = { fieldKey: "payment_terms", occurrenceIndex: 0, decisionStatus: "Pending", cues: [],
      machine: { id: "field-1", value, valueType: "String", confidence: .8, evidence: [{ page: 1 }] },
      effective: { isPresent: true, value, valueType: "String", source: "Machine" } };
    const fetchMock = vi.fn().mockResolvedValueOnce(jsonResponse([document]))
      .mockResolvedValueOnce(jsonResponse({ ...activeWorkspace, fields: [field] }, 200, '"version-1"'))
      .mockResolvedValueOnce(jsonResponse({ ...activeWorkspace, fields: [field] }, 200, '"version-2"'));
    vi.stubGlobal("fetch", fetchMock);
    render(<Home />);
    const input = await screen.findByLabelText("Reviewed value");
    expect(input.getAttribute("dir")).toBe("auto");
    expect(screen.getAllByText(value).every(node => node.getAttribute("dir") === "auto")).toBe(true);
    fireEvent.change(input, { target: { value: "خلال ٦٠ يوماً" } });
    fireEvent.change(screen.getByLabelText("Reason for change or clear choices"), { target: { value: "other" } });
    const reason = screen.getByRole("textbox", { name: "Reason for change or clear: Your explanation" });
    expect(reason.getAttribute("dir")).toBe("auto");
    fireEvent.change(reason, { target: { value: "تصحيح حسب المصدر" } });
    fireEvent.click(screen.getByRole("button", { name: "Save correction" }));
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(3));
    expect(JSON.parse(fetchMock.mock.calls[2][1].body)).toMatchObject({
      value: "خلال ٦٠ يوماً", reason: "تصحيح حسب المصدر" });
  });
  beforeEach(() => { vi.restoreAllMocks(); vi.unstubAllGlobals(); window.history.replaceState(null, "", "/"); });

  it("uploads a document and resets the real form after async processing", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse([]))
      .mockResolvedValueOnce(jsonResponse(document, 201))
      .mockResolvedValueOnce(jsonResponse([document]))
      .mockResolvedValueOnce(jsonResponse(workspace, 200, '"version-1"'));
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();

    const { container } = render(<Home />);
    await screen.findByText(/No documents yet/);
    const fileInput = container.querySelector<HTMLInputElement>('input[type="file"]');
    expect(fileInput).not.toBeNull();
    const formElement = fileInput!.closest("form")!;
    const resetSpy = vi.spyOn(formElement, "reset");

    await user.upload(fileInput!, new File(["synthetic invoice"], "invoice.txt", { type: "text/plain" }));
    fireEvent.submit(formElement);

    await screen.findByText(/Document queued/);
    expect(resetSpy).toHaveBeenCalledOnce();
    expect(fetchMock).toHaveBeenCalledTimes(4);
  });

  it("renders queued processing honestly and offers cancellation", async () => {
    const queued = { ...document, status: "Processing", fields: [] };
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse([queued]))
      .mockResolvedValueOnce(jsonResponse({
        jobId: "job-1", extractionRunId: "run-1", status: "Queued", attemptCount: 0,
      }));
    vi.stubGlobal("fetch", fetchMock);

    render(<Home />);

    await screen.findByText("Waiting for a durable worker claim.");
    expect(await screen.findByRole("button", { name: "Cancel" })).toBeTruthy();
    expect(fetchMock.mock.calls[1][0]).toContain("/api/documents/document-1/processing");
  });

  it("polls a second upload after the previous document has finished", async () => {
    const second = { ...document, id: "document-2", originalFilename: "mixed.pdf" };
    let uploaded = false;
    const fetchMock = vi.fn(async (url: string, init?: RequestInit) => {
      if (init?.method === "POST") { uploaded = true; return jsonResponse(second, 202); }
      if (url.endsWith("/api/documents/work-list")) return jsonResponse(uploaded
        ? [{ ...second, status: "Processing" }, document] : [{ ...document, status: "Processing" }]);
      if (url.endsWith("/processing")) return jsonResponse({ jobId: "job", extractionRunId: "run",
        status: url.includes("document-2") ? "Partial" : "Ready", attemptCount: 1 });
      return jsonResponse(workspace);
    });
    vi.stubGlobal("fetch", fetchMock);
    const { container } = render(<Home />);
    await screen.findByText("Extraction completed.");
    const input = container.querySelector<HTMLInputElement>('input[type="file"]')!;
    await userEvent.upload(input, new File(["test"], "mixed.pdf", { type: "application/pdf" }));
    fireEvent.submit(input.closest("form")!);
    await screen.findByText("Extraction completed with warnings.");
    expect(fetchMock.mock.calls.some(([url]) => url.endsWith("/document-2/processing"))).toBe(true);
    expect(screen.queryByText("Waiting for a durable worker claim.")).toBeNull();
  });

  it("keeps an explicit history save link until the export is replaced or unmounted", async () => {
    const blob = new Blob(['{"reason":"تصحيح — AED 400.00"}'], { type: "application/json" });
    const create = vi.fn().mockReturnValueOnce("blob:history-1").mockReturnValueOnce("blob:history-2");
    const revoke = vi.fn();
    vi.stubGlobal("URL", class extends URL {
      static createObjectURL = create;
      static revokeObjectURL = revoke;
    });
    const click = vi.spyOn(HTMLAnchorElement.prototype, "click").mockImplementation(() => {});
    vi.stubGlobal("fetch", vi.fn()
      .mockResolvedValueOnce(jsonResponse([document]))
      .mockResolvedValueOnce(jsonResponse(activeWorkspace))
      .mockResolvedValue({ ok: true, blob: async () => blob }));
    const { unmount } = render(<Home />);
    await userEvent.click(await screen.findByRole("button", { name: "Export history" }));
    const link = await screen.findByRole("link", { name: "Save history JSON" });
    expect(link.getAttribute("href")).toBe("blob:history-1");
    expect(link.getAttribute("download")).toBe("review-history-document-1.json");
    expect(create).toHaveBeenCalledWith(blob);
    expect(click).toHaveBeenCalledOnce();
    expect(revoke).not.toHaveBeenCalled();
    await userEvent.click(screen.getByRole("button", { name: "Export history" }));
    await waitFor(() => expect(link.getAttribute("href")).toBe("blob:history-2"));
    expect(revoke).toHaveBeenCalledWith("blob:history-1");
    unmount();
    expect(revoke).toHaveBeenCalledWith("blob:history-2");
  });

  it("shows an export error without offering a file when the history request fails", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValueOnce(jsonResponse([document]))
      .mockResolvedValueOnce(jsonResponse(activeWorkspace)).mockResolvedValueOnce(jsonResponse({}, 503)));
    render(<Home />);
    await userEvent.click(await screen.findByRole("button", { name: "Export history" }));
    await screen.findByText("Review history could not be downloaded.");
    expect(screen.queryByRole("link", { name: "Save history JSON" })).toBeNull();
  });

  it("adds a reviewed field with the current ETag and resets the rendered form", async () => {
    const savedWorkspace = {
      ...activeWorkspace,
      fields: [{
        fieldKey: "payment_terms",
        occurrenceIndex: 0,
        latestAction: { id: "action-1", action: "Added", value: "Net 30", valueType: "String", reason: "On invoice", sequenceNumber: 1 },
        effective: { isPresent: true, value: "Net 30", valueType: "String", source: "Reviewer" },
        decisionStatus: "Added",
      }],
    };
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse([document]))
      .mockResolvedValueOnce(jsonResponse(activeWorkspace, 200, '"version-1"'))
      .mockResolvedValueOnce(jsonResponse(savedWorkspace, 200, '"version-2"'));
    vi.stubGlobal("fetch", fetchMock);
    const user = userEvent.setup();

    render(<Home />);
    await screen.findByText("Add a missing field");
    const fieldKey = screen.getByLabelText("Field", { exact: true });
    const value = screen.getByPlaceholderText("Value");
    await user.selectOptions(screen.getByLabelText("Reason choices"), "other");
    const reason = screen.getByPlaceholderText("Reason");
    await user.selectOptions(fieldKey, "payment_terms");
    await user.type(value, "Net 30");
    await user.type(reason, "On invoice");
    await user.click(screen.getByRole("button", { name: "Add field" }));

    await screen.findByText("Reviewer-added field saved.");
    expect((fieldKey as HTMLInputElement).value).toBe("");
    expect((value as HTMLInputElement).value).toBe("");
    expect((screen.getByLabelText("Reason choices") as HTMLSelectElement).value).toBe("");
    expect(screen.queryByPlaceholderText("Reason")).toBeNull();
    const mutation = fetchMock.mock.calls[2];
    expect(mutation[1]?.headers).toMatchObject({ "If-Match": '"version-1"' });
    await waitFor(() => expect(screen.getByText("Net 30")).toBeTruthy());
  });

  it("adds a numeric price on the second line without replacing the first line", async () => {
    const price = (occurrenceIndex: number) => ({ fieldKey: "line_unit_price", occurrenceIndex,
      decisionStatus: "Added", effective: { isPresent: true, value: "400.00", valueType: "Decimal", source: "Reviewer" } });
    const fetchMock = vi.fn().mockResolvedValueOnce(jsonResponse([document]))
      .mockResolvedValueOnce(jsonResponse({ ...activeWorkspace, fields: [price(1)] }, 200, '"version-1"'))
      .mockResolvedValueOnce(jsonResponse({ ...activeWorkspace, fields: [price(1), price(2)] }, 200, '"version-2"'));
    vi.stubGlobal("fetch", fetchMock);
    render(<Home />);
    fireEvent.change(await screen.findByLabelText("Field", { exact: true }), { target: { value: "line_unit_price" } });
    fireEvent.change(screen.getByLabelText("Line number"), { target: { value: "2" } });
    fireEvent.change(screen.getByLabelText("Value", { exact: true }), { target: { value: "400.00" } });
    fireEvent.change(screen.getByLabelText("Reason choices"), { target: { value: "other" } });
    fireEvent.change(screen.getByRole("textbox", { name: "Reason: Your explanation" }), { target: { value: "حسب المستند — AED" } });
    fireEvent.click(screen.getByRole("button", { name: "Add field" }));
    await screen.findByText("Reviewer-added field saved.");
    expect(JSON.parse(fetchMock.mock.calls[2][1].body)).toEqual({ action: "Added",
      fieldKey: "line_unit_price", occurrenceIndex: 2, valueType: "Decimal", value: "400.00", reason: "حسب المستند — AED" });
    expect(screen.getByText("Line unit price · Line 1")).toBeTruthy();
    expect(screen.getByText("Line unit price · Line 2")).toBeTruthy();
    expect((screen.getByLabelText("Field", { exact: true }) as HTMLSelectElement).value).toBe("");
    expect(screen.queryByLabelText("Line number")).toBeNull();
  });

  it("shows missing and low-confidence cues while preserving machine output", async () => {
    const cueWorkspace = {
      ...workspace,
      fields: [
        {
          fieldKey: "invoice_number", occurrenceIndex: 0,
          machine: { id: "field-1", value: "INV-42", valueType: "String", confidence: 0.42, evidence: [{ page: 1 }] },
          effective: { isPresent: true, value: "INV-42", valueType: "String", source: "MachinePending" },
          decisionStatus: "Unreviewed", cues: ["LowConfidence"],
        },
        {
          fieldKey: "supplier_trn", occurrenceIndex: 0,
          effective: { isPresent: false, source: "MissingMachineValue" },
          decisionStatus: "Missing", cues: ["MissingMachineValue"],
        },
      ],
    };
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse([document]))
      .mockResolvedValueOnce(jsonResponse(cueWorkspace));
    vi.stubGlobal("fetch", fetchMock);

    render(<Home />);

    expect(await screen.findByText("Low confidence")).toBeTruthy();
    expect(screen.getAllByText("INV-42")).toHaveLength(2);
    expect(screen.getByText("Missing extraction")).toBeTruthy();
    expect(screen.getByText("Expected field was not extracted")).toBeTruthy();
  });
});
