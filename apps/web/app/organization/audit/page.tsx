"use client";

import { useEnterprise } from "../../security/enterprise-provider";
import { ActivityWorkspace } from "./activity-workspace";
import "../../security/security.css";
import "./activity.css";

export default function AuditPage() {
  const { session } = useEnterprise();
  if (session.role !== "Admin") return null;

  return <ActivityWorkspace key={`${session.organizationId}:${session.actorId}:${session.role}`}/>;
}
