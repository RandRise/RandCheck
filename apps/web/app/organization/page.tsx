"use client";

import { useEnterprise } from "../security/enterprise-provider";
import { PeopleWorkspace } from "./people/people-workspace";
import "../security/security.css";
import "./administration.css";

export type { Member } from "./people/member.types";

export default function OrganizationPage() {
  const { session } = useEnterprise();
  if (session.role !== "Admin") return null;

  return <PeopleWorkspace key={`${session.organizationId}:${session.actorId}:${session.role}`}/>;
}
