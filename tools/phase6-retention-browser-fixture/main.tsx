import React from "react";
import { createRoot } from "react-dom/client";
import { EnterpriseProvider } from "../../apps/web/app/security/enterprise-provider";
import RetentionPage from "../../apps/web/app/organization/retention/page";
import AuditPage from "../../apps/web/app/organization/audit/page";
import "../../apps/web/app/styles.css";
const config = { mode: "Entra", tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", clientId: "cccccccc-cccc-cccc-cccc-cccccccccccc",
  scope: "api://bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/DocumentIntelligence.Access", api: "http://127.0.0.1:3019" };
const identityFactory = async () => ({ initialize: async () => true, token: async () => "fictional-retention-admin", signIn: async () => {}, signOut: async () => {} });
createRoot(document.getElementById("root")!).render(<>
  <div style={{background:"#eee5d3",padding:"10px 24px",fontSize:12}}><strong>FICTIONAL RETENTION PREVIEW</strong> · Test identity and in-memory records. No stored files are deleted.</div>
  <EnterpriseProvider config={config} identityFactory={identityFactory}>{window.location.pathname.endsWith("/audit") ? <AuditPage/> : <RetentionPage/>}</EnterpriseProvider>
</>);
