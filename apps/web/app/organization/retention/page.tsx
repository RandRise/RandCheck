"use client";

import { useEnterprise } from "../../security/enterprise-provider";
import { StorageWorkspace } from "./storage-workspace";
import "../../security/security.css";
import "./retention.css";
import "./storage.css";

export default function RetentionPage() {
  const { session } = useEnterprise();
  if (session.role !== "Admin") return null;

  return <StorageWorkspace key={`${session.organizationId}:${session.actorId}:${session.role}`}/>;
}
