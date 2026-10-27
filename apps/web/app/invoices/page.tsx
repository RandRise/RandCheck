"use client";

import { can, useEnterprise } from "../security/enterprise-provider";
import { useEffect, useState } from "react";
import { InvoiceJourney } from "./components/invoice-journey";
import { InvoiceHome } from "./components/invoice-home";
import "./workflow.css";

export default function InvoicesPage() {
  const { session } = useEnterprise();
  const [hasCase, setHasCase] = useState<boolean | null>(null);
  useEffect(() => {
    const restore = () => setHasCase(new URLSearchParams(window.location.search).has("case"));
    restore(); window.addEventListener("popstate", restore);
    return () => window.removeEventListener("popstate", restore);
  }, []);

  if (!can(session, "Read")) {
    return (
      <main>
        <h1>Invoice access required</h1>
        <p>Your document role does not include invoice checks.</p>
      </main>
    );
  }

  if (hasCase === null) return <main role="status">Opening invoice checks...</main>;
  if (!hasCase) return <InvoiceHome all />;
  return (
    <InvoiceJourney
      key={`${session.organizationId}:${session.actorId}:${session.role}`}
      canReview={can(session, "Review")}
      canOperate={can(session, "Operate")}
      identity={`${session.organizationId}:${session.actorId}`}
    />
  );
}
