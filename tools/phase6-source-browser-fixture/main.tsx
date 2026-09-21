// Test-only composition. Never imported by the production Next.js application.
import React from "react";
import { createRoot } from "react-dom/client";
import { EnterpriseProvider } from "../../apps/web/app/security/enterprise-provider";
import Home from "../../apps/web/app/page";
import AuditPage from "../../apps/web/app/organization/audit/page";
import "../../apps/web/app/styles.css";
const config = { mode: "Entra", tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", clientId: "cccccccc-cccc-cccc-cccc-cccccccccccc",
  scope: "api://bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/DocumentIntelligence.Access", api: "http://127.0.0.1:3018" };
const identityFactory = async () => ({ initialize: async () => true, token: async () => "fictional-source-user",
  signIn: async () => {}, signOut: async () => {} });
async function mode(outcome: string) { await fetch("/api/fixture/scan-mode", { method: "POST",
  headers: { Authorization: "Bearer fictional-source-user", "Content-Type": "application/json" }, body: JSON.stringify({ outcome }) }); }
createRoot(document.getElementById("root")!).render(<>
  <div style={{background:"#eee5d3",padding:"9px 24px",fontSize:12,display:"flex",gap:18,alignItems:"center",flexWrap:"wrap"}}>
    <strong>FICTIONAL SOURCE ACCESS PREVIEW</strong><span>Test identity and scanner responses; no live Entra or ClamAV.</span>
    <button onClick={() => mode("clean")}>Simulate clean scan</button><button onClick={() => mode("unavailable")}>Simulate scan outage</button>
  </div>
  <EnterpriseProvider config={config} identityFactory={identityFactory}>
    {window.location.pathname.includes("/audit") ? <AuditPage/> : <Home/>}
  </EnterpriseProvider>
</>);
