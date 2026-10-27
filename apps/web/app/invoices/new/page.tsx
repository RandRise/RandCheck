"use client";

import { can, useEnterprise } from "../../security/enterprise-provider";
import { StartInvoiceCheck } from "../components/start-invoice-check";

export default function NewInvoiceCheckPage() {
  const { session } = useEnterprise();
  return <StartInvoiceCheck key={`${session.organizationId}:${session.actorId}:${session.role}`}
    identity={`${session.organizationId}:${session.actorId}`} canReview={can(session, "Review")} canOperate={can(session, "Operate")}/>;
}
