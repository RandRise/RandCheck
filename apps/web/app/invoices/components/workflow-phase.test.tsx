import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, expect, it } from "vitest";
import { WorkflowPhase } from "./workflow-phase";

afterEach(cleanup);

it("keeps the current step distinct even when its saved work is reopened", () => {
  render(<ol><WorkflowPhase number={1} title="Add documents" description="Choose both files."
    summary="Two saved files." active completed completedLabel="Files saved" disabled={false} /></ol>);
  const heading = screen.getByRole("heading", { name: "1. Add documents" });
  expect(heading.closest("li")?.getAttribute("data-progress")).toBe("current");
  expect(heading.closest("li")?.getAttribute("aria-current")).toBe("step");
  expect(screen.getByText("Current step")).toBeTruthy();
  expect(screen.queryByText("Files saved")).toBeNull();
});

it("labels saved setup separately from upcoming work without claiming a match", () => {
  render(<ol><WorkflowPhase number={2} title="Check details" description="Check values."
    summary="Missing facts may remain." active={false} completed completedLabel="Reviews saved" disabled={false} />
    <WorkflowPhase number={3} title="See results" description="Read the gaps."
      summary="A previous result exists." active={false} pendingLabel="Saved results available" disabled={false} /></ol>);
  expect(screen.getByText("Reviews saved").closest("li")?.getAttribute("data-progress")).toBe("saved");
  expect(screen.getByText("Reviews saved").querySelector('svg[aria-hidden="true"]')).toBeTruthy();
  expect(screen.getByText("Saved results available").closest("li")?.getAttribute("data-progress")).toBe("upcoming");
  expect(screen.queryByText(/passed|matched/i)).toBeNull();
});
