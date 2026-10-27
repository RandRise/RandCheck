import { API, apiFetch } from "../../security/api-fetch";
import type { Member } from "./member.types";

const MEMBERS_URL = `${API}/api/organization/memberships`;

const rejectionMessages: Record<string, string> = {
  last_administrator_required: "Keep at least one active administrator. Add another administrator before removing this access.",
  membership_already_exists: "This identity already has a membership. Edit its existing membership, or ask the provisioning operator to check its organization.",
  identity_has_active_organization: "This identity already has access to another organization.",
  membership_unchanged: "There are no changes to save.",
};

export class MemberSaveError extends Error {
  constructor(message: string, readonly reloadRequired: boolean) {
    super(message);
  }
}

export async function readMembers(signal?: AbortSignal): Promise<Member[]> {
  const response = await apiFetch(MEMBERS_URL, { signal });
  if (!response.ok) throw new Error("People could not be loaded. Try refreshing the list.");
  return response.json();
}

/** Never retry a write automatically: a lost response may follow a committed change. */
export async function saveMember(id: string | null, body: object): Promise<Member> {
  try {
    const response = await apiFetch(`${MEMBERS_URL}${id ? `/${id}` : ""}`, {
      method: id ? "PUT" : "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(body),
    });
    if (!response.ok) {
      const problem = await response.json().catch(() => ({}));
      if (problem.code === "membership_version_conflict") {
        throw new MemberSaveError("This member was changed by another administrator. Reload the member before making a new change.", true);
      }
      if (response.status >= 500 || response.status === 408) throw new Error("Uncertain save");
      throw new MemberSaveError(rejectionMessages[problem.code] ?? "Access could not be saved. Check the details and try again.", false);
    }
    const saved = await response.json() as Member;
    if (!saved || typeof saved.id !== "string" || typeof saved.version !== "string" ||
      typeof saved.actorId !== "string" || typeof saved.displayName !== "string" ||
      typeof saved.isActive !== "boolean" || !["Staff", "Admin"].includes(saved.role)) {
      throw new Error("Incomplete save response");
    }
    return saved;
  } catch (error) {
    if (error instanceof MemberSaveError) throw error;
    throw new MemberSaveError("The save result could not be confirmed. Reload the people list and check this person's access before trying again.", true);
  }
}
