export const documentRoles = ["Staff", "Admin"] as const;
export type DocumentRole = typeof documentRoles[number];

export const documentRoleDescriptions: Record<DocumentRole, string> = {
  Staff: "Upload documents, check values, compare documents, record decisions and use available downloads.",
  Admin: "Everything Staff can do, plus manage people, audit history and document retention and deletion settings.",
};

export function isDocumentRole(role: string): role is DocumentRole {
  return documentRoles.some(value => value === role);
}

export function documentRoleLabel(role: string) {
  return isDocumentRole(role) ? role : "Access needs attention";
}
