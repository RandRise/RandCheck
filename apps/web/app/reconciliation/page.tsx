"use client";

import { can, useEnterprise } from "../security/enterprise-provider";
import ReconciliationWorkspace from "./reconciliation-workspace";

export default function ReconciliationPage() {
  const { session } = useEnterprise();
  if (!can(session, "Read")) return <main><h1>Comparison access required</h1></main>;
  return <ReconciliationWorkspace key={`${session.organizationId}:${session.actorId}:${session.role}`}
    canReview={can(session, "Review")} creationScope={`${session.organizationId}:${session.actorId}`} />;
}
