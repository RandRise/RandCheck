import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";

import ReconciliationPage from "./page";

const readyDocument = {
  id: "document-1", originalFilename: "quotation.pdf", status: "Ready", documentType: "Quotation",
};

const emptyCase = {
  id: "11111111-1111-4111-8111-111111111111", name: "August procurement", status: "Draft", concurrencyToken: "case-v1",
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

// List refreshes are independent of the mutation response sequence under test.
function installRequests(fetchMock: (url: string, init?: RequestInit) => unknown) {
  vi.stubGlobal("fetch", vi.fn((url: string, init?: RequestInit) => {
    if (/\/api\/reconciliation-cases(?:\?|$)/.test(url) && (!init?.method || init.method === "GET"))
      return Promise.resolve(jsonResponse({ cases: [], nextOffset: null }));
    return fetchMock(url, init);
  }));
}

async function openExistingCase(fetchMock: ReturnType<typeof vi.fn>) {
  window.history.replaceState(null, "", `/reconciliation?case=${emptyCase.id}`);
  render(<ReconciliationPage />);
  await screen.findByRole("heading", { name: emptyCase.name });
  expect(fetchMock.mock.calls[1][0]).toContain("/api/reconciliation-cases");
}

describe("reconciliation workspace", () => {
  beforeEach(() => { vi.restoreAllMocks(); window.history.replaceState(null, "", "/reconciliation"); });

  it("opens an earlier case and pins only a completed review with the current ETag", async () => {
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
    installRequests(fetchMock);

    await openExistingCase(fetchMock);
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
    installRequests(fetchMock);

    await openExistingCase(fetchMock);
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

  it.each(["Approved variance", "اختبار عرض فقط — AED 50.00 / PO-260801 / 2026-09-22"])("records a finding decision with reason %s and the latest ETag", async (reason) => {
    const resolvedCase = {
      ...evaluatedCase,
      concurrencyToken: "case-v3",
      runs: evaluatedCase.runs.map(run => ({ ...run, findings: run.findings.map(finding => ({
        ...finding,
        resolutions: [{
          id: "resolution-1", sequenceNumber: 1, action: "Accepted", reason, createdAt: "2026-08-19T10:00:00Z",
        }],
      })) })),
    };
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse([readyDocument]))
      .mockResolvedValueOnce(jsonResponse(emptyCase, 201, '"case-v1"'))
      .mockResolvedValueOnce(jsonResponse(evaluatedCase, 200, '"case-v2"'))
      .mockResolvedValueOnce(jsonResponse(resolvedCase, 200, '"case-v3"'));
    installRequests(fetchMock);

    await openExistingCase(fetchMock);
    fireEvent.submit(screen.getByRole("button", { name: "Run comparison" }).closest("form")!);
    await screen.findByText("AMT-002");
    await userEvent.click(screen.getByRole("button", { name: "Accepted" }));
    expect((screen.getByRole("button", { name: "Accepted" }) as HTMLButtonElement).disabled).toBe(true);
    expect(fetchMock).toHaveBeenCalledTimes(3);

    fireEvent.change(screen.getByLabelText("Reason for AMT-002 choices"), { target: { value: "other" } });
    const reasonInput = screen.getByLabelText("Reason for AMT-002: Your explanation");
    expect(reasonInput.getAttribute("dir")).toBe("auto");
    await userEvent.type(reasonInput, reason);
    await userEvent.click(screen.getByRole("button", { name: "Accepted" }));

    const savedReason = await screen.findByText(reason);
    expect(savedReason.tagName).toBe("BDI");
    expect(savedReason.closest("p")?.textContent).toBe(`Latest decision: Accepted — ${reason}`);
    await waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(4));
    expect(fetchMock.mock.calls[3][1]?.headers).toMatchObject({ "If-Match": '"case-v2"' });
    expect(JSON.parse(String(fetchMock.mock.calls[3][1]?.body))).toEqual({ action: "Accepted", reason });
  }, 15_000);
});
