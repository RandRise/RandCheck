import { act, cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import DesignProposal from "./design-proposal";

function openFirst() { fireEvent.click(screen.getByRole("button", { name: /Review document INV-2026-041/ })); }
function totalField() { return screen.getByText("Total", { selector: ".proposal-field-label strong" }).closest(".proposal-field") as HTMLElement; }
function keepRemaining() {
  for (const button of screen.queryAllByRole("button", { name: "Keep this value" })) fireEvent.click(button);
}
function changeRole(role: string) { fireEvent.change(screen.getByLabelText("Proposal role"), { target: { value: role } }); }

describe("isolated design proposal behavior", () => {
  beforeEach(() => localStorage.clear());
  afterEach(cleanup);

  it("persists both appearance choices through a new visit", () => {
    const first = render(<DesignProposal/>);
    fireEvent.change(screen.getByLabelText("Design direction"), { target: { value: "neutral" } });
    fireEvent.change(screen.getByLabelText("Appearance"), { target: { value: "dark" } });
    expect(first.container.querySelector(".proposal")?.getAttribute("data-direction")).toBe("neutral");
    expect(first.container.querySelector(".proposal")?.getAttribute("data-theme")).toBe("dark");
    first.unmount();
    const next = render(<DesignProposal/>);
    expect((screen.getByLabelText("Design direction") as HTMLSelectElement).value).toBe("neutral");
    expect((screen.getByLabelText("Appearance") as HTMLSelectElement).value).toBe("dark");
    expect(next.container.querySelector(".proposal")?.getAttribute("data-theme")).toBe("dark");
  });

  it("requires a reason for corrections, keeps original evidence, and finishes only after all checks", () => {
    render(<DesignProposal/>); openFirst();
    expect(screen.getByRole("button", { name: "Finish review" }).hasAttribute("disabled")).toBe(true);
    fireEvent.click(within(totalField()).getByRole("button", { name: "Correct" }));
    fireEvent.change(screen.getByLabelText("Corrected total"), { target: { value: "1,050.00" } });
    expect(screen.getByRole("button", { name: "Save correction" }).hasAttribute("disabled")).toBe(true);
    fireEvent.change(screen.getByLabelText("Reason for correction"), { target: { value: "Verified against supporting evidence" } });
    fireEvent.click(screen.getByRole("button", { name: "Save correction" }));
    expect(within(totalField()).getByText("1,050.00")).toBeTruthy();
    expect(within(totalField()).getByText("Originally extracted: 1,155.00")).toBeTruthy();
    expect(screen.getByText("1,155.00 AED")).toBeTruthy();
    keepRemaining();
    fireEvent.click(screen.getByRole("button", { name: "Finish review" }));
    expect(screen.getByRole("button", { name: "Review finished" }).hasAttribute("disabled")).toBe(true);
    expect(screen.queryByRole("button", { name: "Keep this value" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Activity" }));
    expect(screen.getByText("Total corrected: 1,050.00. Reason: Verified against supporting evidence")).toBeTruthy();
    expect(screen.getByText("Review finished. This revision is read only.")).toBeTruthy();
  });

  it("preserves a correction draft when help and other panels open", () => {
    render(<DesignProposal/>); openFirst();
    fireEvent.click(within(totalField()).getByRole("button", { name: "Correct" }));
    fireEvent.change(screen.getByLabelText("Corrected total"), { target: { value: "1,234.00" } });
    fireEvent.change(screen.getByLabelText("Reason for correction"), { target: { value: "Still checking the original" } });
    fireEvent.click(screen.getByRole("button", { name: "Help with this page" }));
    expect(screen.getByRole("heading", { name: "Your guide to reviewing" })).toBeTruthy();
    expect(document.activeElement).toBe(screen.getByRole("button", { name: "Close role guide" }));
    fireEvent.click(screen.getByRole("button", { name: "Close role guide" }));
    expect(document.activeElement).toBe(screen.getByRole("button", { name: "Help with this page" }));
    fireEvent.click(screen.getByRole("button", { name: "Activity" }));
    fireEvent.click(screen.getByRole("button", { name: "Details" }));
    expect((screen.getByLabelText("Corrected total") as HTMLInputElement).value).toBe("1,234.00");
    expect((screen.getByLabelText("Reason for correction") as HTMLInputElement).value).toBe("Still checking the original");
  });

  it("shows role-specific help and keeps Viewer and administrator-only actions separate", () => {
    render(<DesignProposal/>);
    changeRole("Viewer");
    expect(screen.queryByRole("button", { name: "New comparison case" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: /Open document INV-2026-041/ }));
    expect(screen.queryByRole("button", { name: "Finish review" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Correct" })).toBeNull();
    expect(screen.queryByRole("button", { name: "Compare" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Help with this page" }));
    expect(screen.getByRole("heading", { name: "Your guide to viewing" })).toBeTruthy();
    changeRole("Administrator");
    expect(screen.getByRole("heading", { level: 1, name: "Members and access" })).toBeTruthy();
    expect(screen.queryByRole("button", { name: /Open document/ })).toBeNull();
    expect(screen.queryByRole("button", { name: /My work/ })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Help with this page" }));
    expect(screen.getByRole("heading", { name: "Your guide to administration" })).toBeTruthy();
    changeRole("Operator");
    fireEvent.click(screen.getByRole("button", { name: "Open my guide" }));
    expect(screen.getByRole("heading", { name: "Your guide to processing" })).toBeTruthy();
  });

  it("assigns distinct sample case references and ignores duplicate submission of one form", () => {
    render(<DesignProposal/>);
    fireEvent.click(screen.getByRole("button", { name: "New comparison case" }));
    fireEvent.change(screen.getByLabelText("Case title"), { target: { value: "Sample Trading · Supplies" } });
    fireEvent.change(screen.getByLabelText(/Category/), { target: { value: "Office supplies" } });
    const form = screen.getByLabelText("Case title").closest("form")!;
    act(() => { fireEvent.submit(form); fireEvent.submit(form); });
    expect(screen.getAllByText("REC-2026-000123")).toHaveLength(1);
    fireEvent.click(screen.getByRole("button", { name: "New comparison case" }));
    fireEvent.change(screen.getByLabelText("Case title"), { target: { value: "شركة النور · Services" } });
    fireEvent.click(screen.getByRole("button", { name: "Create case" }));
    expect(screen.getByText("REC-2026-000124")).toBeTruthy();
    expect(screen.getByText("Sample Trading · Supplies")).toBeTruthy();
    expect(screen.getByText("شركة النور · Services").getAttribute("dir")).toBe("auto");
    expect(screen.getByText("Uncategorized")).toBeTruthy();
  });

  it("combines document search with status filters and recovers from an empty search", () => {
    render(<DesignProposal/>); openFirst(); keepRemaining();
    fireEvent.click(screen.getByRole("button", { name: "Finish review" }));
    fireEvent.click(screen.getByRole("button", { name: "← Back to my work" }));
    fireEvent.click(screen.getByRole("button", { name: "Completed" }));
    expect(screen.getByRole("button", { name: /Open document INV-2026-041/ })).toBeTruthy();
    expect(screen.queryByText("فاتورة خدمات — Services invoice")).toBeNull();
    fireEvent.change(screen.getByLabelText("Find a document"), { target: { value: "شركة النور" } });
    expect(screen.getByRole("heading", { name: "No documents here" })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Show all work" }));
    expect(screen.getByText("فاتورة خدمات — Services invoice")).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Find a document"), { target: { value: "inv-2026-042" } });
    expect(screen.queryByText("Office supplies invoice")).toBeNull();
    expect(screen.getByRole("button", { name: /Review document INV-2026-042/ })).toBeTruthy();
  });

  it("requires saving or cancelling a pending correction before finishing the review", () => {
    render(<DesignProposal/>); openFirst(); keepRemaining();
    fireEvent.click(within(totalField()).getByRole("button", { name: "Correct value" }));
    fireEvent.change(screen.getByLabelText("Corrected total"), { target: { value: "1,050.00" } });
    expect(screen.getByRole("button", { name: "Finish review" }).hasAttribute("disabled")).toBe(true);
    fireEvent.click(screen.getByRole("button", { name: "Finish review" }));
    expect(screen.queryByRole("button", { name: "Review finished" })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    expect(screen.getByRole("button", { name: "Finish review" }).hasAttribute("disabled")).toBe(false);
  });

  it("keeps a comparison and revised decision when reopening the completed document", () => {
    render(<DesignProposal/>); openFirst(); keepRemaining();
    fireEvent.click(screen.getByRole("button", { name: "Finish review" }));
    fireEvent.click(screen.getByRole("button", { name: "Compare" }));
    fireEvent.click(screen.getByRole("button", { name: "Compare documents" }));
    fireEvent.change(screen.getByLabelText("Reason for accepting the difference"), { target: { value: "Delivery surcharge agreed" } });
    fireEvent.click(screen.getByRole("button", { name: "Accept difference" }));
    fireEvent.click(screen.getByRole("button", { name: "Change decision" }));
    fireEvent.change(screen.getByLabelText("Reason for accepting the difference"), { target: { value: "Updated agreement checked" } });
    fireEvent.click(screen.getByRole("button", { name: "Accept difference" }));
    fireEvent.click(screen.getByRole("button", { name: "← Back to my work" }));
    fireEvent.click(screen.getByRole("button", { name: /Open document INV-2026-041/ }));
    fireEvent.click(screen.getByRole("button", { name: "Compare" }));
    expect(screen.queryByRole("button", { name: "Compare documents" })).toBeNull();
    expect(screen.getByText("Decision: Accepted difference")).toBeTruthy();
    expect(screen.getByText(/Updated agreement checked/)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Activity" }));
    expect(screen.getAllByText("Example comparison completed.")).toHaveLength(1);
    expect(screen.getByText("Difference accepted. Reason: Delivery surcharge agreed")).toBeTruthy();
    expect(screen.getByText("Difference accepted. Reason: Updated agreement checked")).toBeTruthy();
  });

  it.each(["Save correction", "Cancel"])("protects an unfinished correction and restores its control focus after %s", async action => {
    render(<DesignProposal/>); openFirst();
    fireEvent.click(within(totalField()).getByRole("button", { name: "Correct" }));
    fireEvent.change(screen.getByLabelText("Corrected total"), { target: { value: "1,050.00" } });
    fireEvent.change(screen.getByLabelText("Reason for correction"), { target: { value: "Supporting evidence checked" } });
    const otherCorrections = screen.getAllByRole("button", { name: "Correct" });
    expect(otherCorrections).toHaveLength(4);
    expect(otherCorrections.every(button => button.hasAttribute("disabled"))).toBe(true);
    expect(screen.getByLabelText("Proposal role").hasAttribute("disabled")).toBe(true);
    fireEvent.click(otherCorrections[0]);
    expect((screen.getByLabelText("Corrected total") as HTMLInputElement).value).toBe("1,050.00");
    if (action === "Save correction") {
      fireEvent.click(screen.getByRole("button", { name: "Activity" }));
      fireEvent.click(screen.getByRole("button", { name: /My work 2/ }));
    } else {
      fireEvent.click(screen.getByRole("button", { name: "← Back to my work" }));
    }
    expect(screen.getByRole("heading", { level: 1, name: "Office supplies invoice" })).toBeTruthy();
    expect(screen.getByText("Save or cancel your correction before leaving this document.")).toBeTruthy();
    expect((screen.getByLabelText("Corrected total") as HTMLInputElement).value).toBe("1,050.00");
    expect((screen.getByLabelText("Reason for correction") as HTMLInputElement).value).toBe("Supporting evidence checked");
    await waitFor(() => expect(document.activeElement).toBe(screen.getByLabelText("Corrected total")));
    fireEvent.click(screen.getByRole("button", { name: action }));
    const restored = within(totalField()).getByRole("button", { name: action === "Save correction" ? "Correct value" : "Correct" });
    await waitFor(() => expect(document.activeElement).toBe(restored));
    expect(screen.getByLabelText("Proposal role").hasAttribute("disabled")).toBe(false);
    expect(screen.queryByLabelText("Corrected total")).toBeNull();
  });
});
