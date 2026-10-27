"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { can, useEnterprise } from "../../security/enterprise-provider";
import { useInvoiceWorkflow } from "../hooks/use-invoice-workflow";
import "../workflow.css";
import "../journey.css";
import "../home.css";

/** Home shows only server-saved work; counts are not inferred from extraction confidence. */
export function InvoiceHome({ all = false }: { all?: boolean }) {
  const { session } = useEnterprise();
  return <InvoiceHomeContent key={`${session.organizationId}:${session.actorId}:${session.role}`}
    all={all} allowed={can(session, "Review")} identity={`${session.organizationId}:${session.actorId}`} />;
}

function InvoiceHomeContent({ all, allowed, identity }: { all: boolean; allowed: boolean; identity: string }) {
  const router = useRouter();
  const work = useInvoiceWorkflow(allowed, identity);

  return <main className="invoice-home">
    <header className="home-heading">
      <h1>{all ? "All invoice checks" : "Invoice checks"}</h1>
      <p>Check what your supplier charged against your purchase order.</p>
    </header>
    {allowed && <section className="home-start" aria-labelledby="home-start-title">
      <div className="home-start-heading">
        <div className="home-start-copy">
          <svg className="home-pair-icon" viewBox="0 0 52 58" fill="none" aria-hidden="true" focusable="false">
            <rect x="3" y="3" width="33" height="43" rx="5" fill="var(--home-step-1-bg)" stroke="currentColor" strokeWidth="1.5"/>
            <rect x="15" y="13" width="33" height="43" rx="5" fill="var(--home-start-bg)" stroke="currentColor" strokeWidth="1.5"/>
            <path d="M23 25h17M23 32h17M23 39h10M23 46h13" stroke="currentColor" strokeWidth="2" strokeLinecap="round"/>
          </svg>
          <div><h2 id="home-start-title">Start here</h2><p>Have your invoice and purchase order ready.</p></div>
        </div>
        <button className="home-primary" onClick={() => router.push("/invoices/new")}>Start with my files</button>
      </div>
      <ol className="home-steps">
        <li><span className="home-step-number" aria-hidden="true">1</span><strong>Add documents</strong><small>Upload or use saved files.</small></li>
        <li><span className="home-step-number" aria-hidden="true">2</span><strong>Check details</strong><small>Check values against the source.</small></li>
        <li><span className="home-step-number" aria-hidden="true">3</span><strong>See results</strong><small>Read differences and decide.</small></li>
      </ol>
    </section>}
    {work.message && <p className="home-notice" role="status">{work.message}</p>}
    <section className="home-saved" aria-labelledby="home-saved-title">
      <div className="home-saved-heading">
        <h2 id="home-saved-title">{all ? "Saved checks" : "Continue your work"}</h2>
        <button className="home-refresh" disabled={work.listLoading} onClick={() => void work.loadCases()}>Refresh checks</button>
      </div>
      {work.listLoading && <p className="home-notice" role="status">Loading saved checks…</p>}
      {work.listError && <p className="home-notice home-error" role="alert">{work.listError}</p>}
      {!work.listLoading && !work.listError && work.cases.length === 0 && <p className="home-notice">No saved checks yet. Your work will appear here after you choose both files and continue to review.</p>}
      <ul className="home-saved-list">
        {work.cases.map(saved => <li key={saved.id}>
          <div className="home-saved-name"><strong><bdi>{saved.name}</bdi></strong><span className="home-reference">{saved.reference}</span></div>
          <span className="home-saved-state">{saved.status === "Closed" ? "Closed · saved history" : "Saved work"}</span>
          <Link className="home-continue" href={`/invoices?case=${saved.id}`}>{saved.status === "Closed" ? "View results" : "Continue"}<span className="sr-only"> {saved.reference}</span></Link>
        </li>)}
      </ul>
      {work.nextOffset !== null && <button disabled={work.listLoading} onClick={() => void work.loadCases(work.nextOffset!)}>Load more checks</button>}
    </section>
  </main>;
}
