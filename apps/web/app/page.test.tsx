import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import Home from "./page";

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
  beforeEach(() => vi.restoreAllMocks());

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
    const fieldKey = screen.getByPlaceholderText("field_key");
    const value = screen.getByPlaceholderText("Value");
    const reason = screen.getByPlaceholderText("Reason");
    await user.type(fieldKey, "payment_terms");
    await user.type(value, "Net 30");
    await user.type(reason, "On invoice");
    await user.click(screen.getByRole("button", { name: "Add field" }));

    await screen.findByText("Reviewer-added field saved.");
    expect((fieldKey as HTMLInputElement).value).toBe("");
    expect((value as HTMLInputElement).value).toBe("");
    expect((reason as HTMLInputElement).value).toBe("");
    const mutation = fetchMock.mock.calls[2];
    expect(mutation[1]?.headers).toMatchObject({ "If-Match": '"version-1"' });
    await waitFor(() => expect(screen.getByText("Net 30")).toBeTruthy());
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
