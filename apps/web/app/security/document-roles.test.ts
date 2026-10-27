import { describe, expect, it } from "vitest";
import { can, type Session } from "./enterprise-provider";
import { documentRoleLabel, documentRoles } from "./document-roles";

describe("two-role access contract", () => {
  it.each(["Staff", "Admin", "Viewer", "Reviewer", "Operator", "None", "", "10"])("checks permissions for %s without legacy fallback", role => {
    const session = { role } as Session;
    for (const permission of ["Read", "Review", "Operate", "Ask", "SourceRead"]) {
      expect(can(session, permission)).toBe(role === "Staff" || role === "Admin");
    }
    for (const permission of ["ManageMembers", "Audit", "Retention"]) expect(can(session, permission)).toBe(role === "Admin");
    expect(can(session, "UnknownPermission")).toBe(false);
  });

  it("offers exactly Staff and Admin and does not relabel old values", () => {
    expect(documentRoles).toEqual(["Staff", "Admin"]);
    expect(documentRoleLabel("Operator")).toBe("Access needs attention");
  });
});
