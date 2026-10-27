import { act, cleanup, fireEvent, render, renderHook, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import AuditPage from "./page";
import { activityActor, activityResult, activityTime, activityTitle, activityUtc } from "./activity-labels";
import { readActivity } from "./activity-api";
import type { ActivityEvent } from "./activity.types";
import { useActivity } from "./use-activity";

const session = vi.hoisted(() => ({ organizationId: "org-a", actorId: "admin-a", role: "Admin" }));
vi.mock("../../security/enterprise-provider", () => ({ useEnterprise: () => ({ session }) }));
const event: ActivityEvent = {
  id: "event-a", actorId: "22222222-2222-2222-2222-222222222222", actorKind: "Human",
  action: "membership.changed", targetType: "membership", targetId: "member-a", outcome: "Succeeded",
  reasonCode: "committed", requestId: "request-a", beforeState: "Staff;active=True", afterState: "Admin;active=True",
  recordedAtUtc: "2026-10-26T10:05:00Z",
};
const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status });
const pageOf = (name: string): ActivityEvent[] => Array.from({ length: 100 }, (_, index) => ({ ...event, id: `${name}-${index}`, targetId: name }));
beforeEach(() => { session.organizationId = "org-a"; session.actorId = "admin-a"; session.role = "Admin"; });
afterEach(() => { cleanup(); vi.unstubAllGlobals(); vi.restoreAllMocks(); });

describe("activity wording and saved evidence", () => {
  it("does not invent a person name or reinterpret repeated IDs as a system identity", () => {
    expect(activityActor(event)).toBe("Person · name not recorded");
    expect(activityActor({ ...event, actorKind: "System" })).toBe("System");
    expect(activityActor({ ...event, actorKind: "Anonymous", actorId: null })).toBe("Unidentified caller");
    expect(activityActor({ ...event, actorKind: "FutureType" })).toBe("Actor type not recognized");
  });

  it.each(["Denied", "Failed", "Unknown"])("does not claim a completed access grant for %s", outcome => {
    expect(activityTitle({ ...event, action: "membership.granted", outcome })).toBe("Access grant");
    expect(activityResult(outcome).tone).not.toBe("success");
  });

  it("keeps comparison events separate from a match and unknown action codes neutral", () => {
    expect(activityTitle({ ...event, action: "reconciliation.evaluated" })).toBe("Comparison recorded");
    expect(activityTitle({ ...event, action: "new-provider.finished" })).toBe("Other recorded activity");
    expect(activityTitle({ ...event, action: "document.source-download-authorized" })).toBe("Original file download allowed");
    expect(activityTitle({ ...event, action: "document.source-purge-authorized" })).not.toContain("removed");
  });

  it("uses UAE time and English digits, including dates crossing midnight", () => {
    expect(activityTime("2026-10-26T22:05:00Z")).toBe("27 Oct 2026, 02:05");
    expect(activityTime("2026-10-27T02:05:00+04:00")).toBe("27 Oct 2026, 02:05");
    expect(activityUtc("2026-10-27T02:05:00+04:00")).toBe("2026-10-26T22:05:00.000Z");
    expect(activityTime("not-a-date")).toBeNull();
    expect(activityTime("2026-10-26T22:05:00")).toBeNull();
  });

  it("retains every saved field and Arabic as inert text in Details", async () => {
    const row = { ...event, action: "custom.event", actorKind: "Unknown", outcome: "FutureOutcome",
      beforeState: "<script>alert('no')</script>", afterState: "مراجعة الفاتورة 123 — Staff;active=False" };
    vi.stubGlobal("fetch", vi.fn(async () => json([row])));
    render(<AuditPage/>);
    await screen.findByText("1 event on this page");
    const details = document.querySelector("details")!;
    fireEvent.click(details.querySelector("summary")!);
    expect(details.open).toBe(true);
    for (const [label, expected] of [["Action code", row.action], ["Actor type", row.actorKind], ["Actor ID", row.actorId],
      ["Target type", row.targetType], ["Target ID", row.targetId], ["Recorded result", row.outcome],
      ["Reason code", row.reasonCode], ["Request ID", row.requestId], ["Before", row.beforeState], ["After", row.afterState],
      ["Event ID", row.id], ["Saved timestamp (original)", row.recordedAtUtc]]) {
      expect(within(details).getByText(label!).nextElementSibling?.textContent).toBe(expected);
    }
    expect(details.querySelector("script")).toBeNull();
    expect(screen.getByText("Result not recognized")).toBeTruthy();
  });
});

describe("activity loading and page ownership", () => {
  it("denies Staff before any audit request", async () => {
    session.role = "Staff";
    const fetcher = vi.fn(); vi.stubGlobal("fetch", fetcher);
    render(<AuditPage/>);
    await act(async () => {});
    expect(fetcher).not.toHaveBeenCalled();
    expect(screen.queryByRole("heading", { name: "Activity history" })).toBeNull();
  });

  it("keeps server order and reads only bounded GET pages", async () => {
    const fetcher = vi.fn(async (url: string) => json(url.endsWith("offset=0") ? pageOf("first") : [{ ...event, id: "second" }]));
    vi.stubGlobal("fetch", fetcher); render(<AuditPage/>);
    await screen.findByText("100 events on this page");
    expect(screen.getByRole("button", { name: "Newer" }).matches(":disabled")).toBe(true);
    expect([...document.querySelectorAll(".activity-evidence dt")].filter(e => e.textContent === "Event ID").map(e => e.nextElementSibling?.textContent)).toEqual(pageOf("first").map(e => e.id));
    fireEvent.click(screen.getByRole("button", { name: "Older" }));
    await screen.findByText("1 event on this page");
    expect(fetcher.mock.calls[1][0]).toMatch(/offset=100$/);
    expect(screen.getByRole("button", { name: "Older" }).matches(":disabled")).toBe(true);
    fireEvent.click(screen.getByRole("button", { name: "Refresh activity" }));
    await screen.findByText("100 events on this page");
    expect(fetcher.mock.calls[2][0]).toMatch(/offset=0$/);
  });

  it("retries a failed older page in place without claiming an empty history", async () => {
    let fail = true;
    const fetcher = vi.fn(async (url: string) => url.endsWith("offset=0") ? json(pageOf("first")) : fail ? json({}, 503) : json([]));
    vi.stubGlobal("fetch", fetcher); render(<AuditPage/>);
    await screen.findByText("100 events on this page");
    fireEvent.click(screen.getByRole("button", { name: "Older" }));
    await screen.findByRole("alert");
    expect(screen.queryByText("No events on this page.")).toBeNull();
    expect(screen.getByRole("status").textContent).toBe("Page unavailable");
    fail = false;
    fireEvent.click(screen.getByRole("button", { name: "Try again" }));
    await screen.findByText("No events on this page.");
    expect(fetcher.mock.calls.at(-1)![0]).toMatch(/offset=100$/);
    expect(screen.getByRole("button", { name: "Newer" }).matches(":disabled")).toBe(false);
  });

  it("does not show old organization rows after a delayed response", async () => {
    let finish!: (response: Response) => void;
    const fetcher = vi.fn().mockImplementationOnce(() => new Promise<Response>(resolve => { finish = resolve; }))
      .mockResolvedValue(json([{ ...event, id: "new-org-event", action: "document.uploaded" }]));
    vi.stubGlobal("fetch", fetcher);
    const view = render(<AuditPage/>);
    await waitFor(() => expect(fetcher).toHaveBeenCalledTimes(1));
    session.organizationId = "org-b"; view.rerender(<AuditPage/>);
    await screen.findByText("Document uploaded");
    await act(async () => finish(json([event])));
    expect(screen.queryByText("Access changed")).toBeNull();
    expect(screen.getByText("Document uploaded")).toBeTruthy();
    expect(fetcher.mock.calls[0][1].signal.aborted).toBe(true);
  });

  it("keeps page controls disabled until the current read settles", async () => {
    let finish!: (response: Response) => void;
    const fetcher = vi.fn().mockResolvedValueOnce(json(pageOf("first"))).mockImplementationOnce(() => new Promise<Response>(resolve => { finish = resolve; }));
    vi.stubGlobal("fetch", fetcher); render(<AuditPage/>);
    await screen.findByText("100 events on this page");
    fireEvent.click(screen.getByRole("button", { name: "Older" }));
    for (const name of ["Newer", "Older", "Refresh activity"]) expect(screen.getByRole("button", { name }).matches(":disabled")).toBe(true);
    expect(document.querySelectorAll(".activity-event")).toHaveLength(0);
    await waitFor(() => expect(finish).toBeDefined());
    await act(async () => finish(json([])));
    expect(screen.getByText("No events on this page.")).toBeTruthy();
  });

  it.each([
    { label: "null", rows: null }, { label: "object", rows: {} },
    { label: "invalid state", rows: [{ ...event, beforeState: {} }] },
    { label: "too many rows", rows: pageOf("overflow").concat(event) },
  ])("rejects malformed activity data: $label", async ({ rows }) => {
    vi.stubGlobal("fetch", vi.fn(async () => json(rows)));
    render(<AuditPage/>);
    await screen.findByRole("alert");
    expect(screen.queryByText("No events on this page.")).toBeNull();
    expect(document.querySelectorAll(".activity-event")).toHaveLength(0);
  });

  it.each([-100, 1, 10100, NaN])("does not request an invalid offset %s", async offset => {
    const fetcher = vi.fn(); vi.stubGlobal("fetch", fetcher);
    await expect(readActivity(offset, new AbortController().signal)).rejects.toThrow("Invalid activity page");
    expect(fetcher).not.toHaveBeenCalled();
  });

  it("stops at page 101 instead of requesting beyond the backend limit", async () => {
    const fetcher = vi.fn(async (_url: string) => json(pageOf("full")));
    vi.stubGlobal("fetch", fetcher);
    const view = renderHook(() => useActivity());
    await waitFor(() => expect(view.result.current.loading).toBe(false));
    for (let page = 1; page <= 100; page++) await act(async () => view.result.current.older());
    expect(view.result.current.page).toBe(101);
    expect(fetcher).toHaveBeenCalledTimes(101);
    expect(fetcher.mock.calls.at(-1)?.[0]).toMatch(/offset=10000$/);
    await act(async () => view.result.current.older());
    expect(fetcher).toHaveBeenCalledTimes(101);
  });
});
