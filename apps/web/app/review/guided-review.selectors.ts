import { isWorkspaceId } from "../workspace-location";
import { fieldIdentity, fieldValue } from "./review-fields";
import type { ReviewField } from "./review.types";

// This only chooses a display mode. Pair ownership and every write remain server-checked.
export function guidedReviewFromUrl(documentId?: string) {
  const query = new URLSearchParams(window.location.search);
  return !!documentId && query.get("document") === documentId &&
    isWorkspaceId(query.get("pairCase") ?? "") &&
    ["Invoice", "PurchaseOrder"].includes(query.get("pairRole") ?? "");
}

export function guidedReviewQueue(
  fields: ReviewField[],
  drafts: Record<string, string>,
  reasons: Record<string, string>,
) {
  const dirty = (field: ReviewField) => {
    const key = fieldIdentity(field);
    return (drafts[key] ?? fieldValue(field)) !== fieldValue(field) || !!reasons[key];
  };

  // Keep an unfinished edit first, then source warnings. Confidence never confirms a value.
  const priority = (field: ReviewField) => dirty(field) ? 0 : field.cues?.length ? 1 : 2;
  return fields.filter(field => !!field.machine && (field.decisionStatus === "Unreviewed" || dirty(field)))
    .sort((left, right) => priority(left) - priority(right));
}
