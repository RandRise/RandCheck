// Test-only composition. Never imported by Next.js or the production application.
import React from "react";
import { createRoot } from "react-dom/client";
import { EnterpriseProvider } from "../../apps/web/app/security/enterprise-provider";
import OrganizationPage from "../../apps/web/app/organization/page";
import AuditPage from "../../apps/web/app/organization/audit/page";
import Home from "../../apps/web/app/page";
import "../../apps/web/app/styles.css";
const config = { mode: "Entra", tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", clientId: "cccccccc-cccc-cccc-cccc-cccccccccccc",
  scope: "api://bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/DocumentIntelligence.Access", api: "http://127.0.0.1:3017" };
const factory = async () => ({
  initialize: async () => sessionStorage.getItem("fixture-signed-out") !== "yes",
  token: async () => `fictional-${sessionStorage.getItem("fixture-person") ?? "admin"}`,
  signIn: async () => { sessionStorage.removeItem("fixture-signed-out"); window.location.assign("/organization"); },
  signOut: async () => { sessionStorage.setItem("fixture-signed-out", "yes"); window.location.assign("/signin"); }
});
function person(name: string) { sessionStorage.setItem("fixture-person", name); sessionStorage.removeItem("fixture-signed-out"); window.location.assign(name === "viewer" ? "/" : "/organization"); }
createRoot(document.getElementById("root")!).render(<>
  <div style={{background:"#eee5d3",padding:"9px 24px",fontSize:11,display:"flex",gap:18,alignItems:"center",flexWrap:"wrap"}}>
    <strong>LOCAL PREVIEW · FICTIONAL ORGANIZATION</strong><span>UI fixture; live Microsoft sign-in is not connected.</span>
    <button onClick={() => person("admin")}>Preview administrator</button><button onClick={() => person("viewer")}>Preview viewer</button>
  </div>
  <EnterpriseProvider config={config} identityFactory={factory}>{window.location.pathname.includes("/audit") ? <AuditPage/> : window.location.pathname === "/organization" ? <OrganizationPage/> : <Home/>}</EnterpriseProvider>
</>);
