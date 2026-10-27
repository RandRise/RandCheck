export type Member = {
  id: string;
  actorId: string;
  tenantId: string;
  objectId: string;
  displayName: string;
  role: string;
  isActive: boolean;
  version: string;
};

export type MemberSelection = Member | "new" | null;
