import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import ReconciliationPage from "./page";

const readyDocument = {
  id: "document-1", originalFilename: "quotation.pdf", status: "Ready", documentType: "Quotation",
};

const emptyCase = {
  id: "case-1", name: "August procurement", status: "Draft", concurrencyToken: "case-v1",
  documents: [], runs: [],
};

const evaluatedCase = {
  ...emptyCase,
  concurrencyToken: "case-v2",
  documents: [
    { id: "link-1", role: "Quotation", documentId: "document-1", reviewSessionId: "review-1" },
    { id: "link-2", role: "PurchaseOrder", documentId: "document-2", reviewSessionId: "review-2" },
    { id: "link-3", role: "Invoice", documentId: "document-3", reviewSessionId: "review-3" },
  ],
  runs: [{
    id: "run-1", runNumber: 1, status: "Completed", ruleSetVersion: "1.0",
    fields: [
      { id: "field-left", key: "total", occurrence: 0, value: "1000.00", page: 1 },
      { id: "field-right", key: "total", occurrence: 0, value: "1050.00", page: 2 },
    ],
    findings: [{
      id: "finding-1", ruleCode: "AMT-002", severity: "Error", leftRole: "PurchaseOrder", rightRole: "Invoice",
      fieldKey: "total", occurrence: 0, leftFieldSnapshotId: "field-left", rightFieldSnapshotId: "field-right",
      leftValue: "1000.00", rightValue: "1050.00", difference: 50, tolerance: 0.01,
      explanation: "Invoice total differs from the purchase order total.", resolutions: [],
    }],
  }],
};

function jsonResponse(body: unknown, status = 200, etag?: string): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: new Headers(etag ? { ETag: etag } : {}),
    json: async () => body,
  } as Response;
}

async function createCase(fetchMock: ReturnType<typeof vi.fn>) {
  render(<ReconciliationPage />);
  await screen.findByRole("heading", { name: "Start a reconciliation case" });
  await userEvent.type(screen.getByLabelText("Case name"), "August procurement");
  await userEvent.click(screen.getByRole("button", { name: "Create case" }));
  await screen.findByText("Case created. Link completed review revisions next.");
  expect(fetchMock.mock.calls[1][0]).toContain("/api/reconciliation-cases");
}

describe("reconciliation workspace", () => {
  beforeEach(() => vi.restoreAllMocks());

  it("creates a case and pins only a completed review with the current ETag", async () => {
    const linkedCase = {
      ...emptyCase,
      concurrencyToken: "case-v2",
      documents: [{ id: "link-1", role: "Quotation", documentId: "document-1", reviewSessionId: "review-1" }],
    };
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse([readyDocument]))
      .mockResolvedValueOnce(jsonResponse(emptyCase, 201, '"case-v1"'))
      .mockResolvedValueOnce(jsonResponse({ session: { id: "review-1", status: "Completed" } }))
      .mockResolvedValueOnce(jsonResponse(linkedCase, 200, '"case-v2"'));
    vi.stubGlobal("fetch", fetchMock);

    await createCase(fetchMock);
    await userEvent.selectOptions(screen.getByLabelText("Quotation document"), "document-1");
    await userEvent.click(screen.getAllByRole("button", { name: "Pin review" })[0]);

    await screen.findByText("Quotation review pinned to the case.");
    expect(fetchMock.mock.calls[3][1]?.headers).toMatchObject({ "If-Match": '"case-v1"' });
    expect(JSON.parse(String(fetchMock.mock.calls[3][1]?.body))).toEqual({ documentId: "document-1", reviewSessionId: "review-1" });
  });

  it("renders rule, compared values, tolerance, and both source pages", async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse([readyDocument]))
      .mockResolvedValueOnce(jsonResponse(emptyCase, 201, '"case-v1"'))
      .mockResolvedValueOnce(jsonResponse(evaluatedCase, 200, '"case-v2"'));
    vi.stubGlobal("fetch", fetchMock);

    await createCase(fetchMock);
    fireEvent.submit(screen.getByRole("button", { name: "Run comparison" }).closest("form")!);

    expect(await screen.findByText("AMT-002")).toBeTruthy();
    expect(screen.getByText("1000.00")).toBeTruthy();
    expect(screen.getByText("1050.00")).toBeTruthy();
    expect(screen.getByText("50 / 0.01")).toBeTruthy();
    expect(screen.getByText("Page 1")).toBeTruthy();
    expect(screen.getByText("Page 2")).toBeTruthy();
    const evaluation = fetchMock.mock.calls[2];
    expect(evaluation[1]?.headers).toMatchObject({ "If-Match": '"case-v1"' });
  });

  it("requires a reason and records an append-only finding decision with the latest ETag", async () => {
    const resolvedCase = {
      ...evaluatedCase,
      concurrencyToken: "case-v3",
      runs: evaluatedCase.runs.map(run => ({ ...run, findings: run.findings.map(finding => ({
        ...finding,
        resolutions: [{
          id: "resolution-1", sequenceNumber: 1, action: "Accepted", reason: "Approved variance", createdAt: "2026-08-19T10:00:00Z",
        }],
      })) })),
    };
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse([readyDocument]))
      .mockResolvedValueOnce(jsonResponse(emptyCase, 201, '"case-v1"'))
      .mockResolvedValueOnce(jsonResponse(evaluatedCase, 200, '"case-v2"'))
      .mockResolvedValueOnce(jsonResponse(resolvedCase, 200, '"case-v3"'));
    vi.stubGlobal("fetch", fetchMock);

    await createCase(fetchMock);
    fireEvent.submit(screen.getByRole("button", { name: "Run comparison" }).closest("form")!);
    await screen.findByText("AMT-002");
    await userEvent.click(screen.getByRole("button", { name: "Accepted" }));
    expect(await screen.findByText("Add a reason before deciding a finding.")).toBeTruthy();
    expect(fetchMock).toHaveBeenCalledTimes(3);

    await userEvent.type(screen.getByLabelText("Reason for AMT-002"), "Approved variance");
    await userEvent.click(screen.getByRole("button", { name: "Accepted" }));

    expect(await screen.findByText("Latest decision: Accepted — Approved variance")).toBeTruthy();
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(4));
    expect(fetchMock.mock.calls[3][1]?.headers).toMatchObject({ "If-Match": '"case-v2"' });
    expect(JSON.parse(String(fetchMock.mock.calls[3][1]?.body))).toEqual({ action: "Accepted", reason: "Approved variance" });
  }, 15_000);
});
