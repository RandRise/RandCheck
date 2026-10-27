import { createRoot } from "react-dom/client";
import { AppearanceProvider } from "../../apps/web/app/appearance";
import { EnterpriseProvider } from "../../apps/web/app/security/enterprise-provider";
import Layout from "../../apps/web/app/organization/layout";
import People from "../../apps/web/app/organization/page";
import Audit from "../../apps/web/app/organization/audit/page";
import Storage from "../../apps/web/app/organization/retention/page";
import Documents from "../../apps/web/app/documents/page";
import "../../apps/web/app/styles.css";

declare const __ADMIN_ACCEPTANCE__: { api: string; token: string; tenantId: string; clientId: string; scope: string };
const config = { ...__ADMIN_ACCEPTANCE__, mode: "Entra" };
// Only sign-in is substituted. Session, authorization, CORS, reads and writes go
// through normal secureTransport to Kestrel, real repositories and disposable PG.
const identityFactory = async () => ({ initialize: async () => true, token: async () => config.token,
  signIn: async () => {}, signOut: async () => {} });
const page = location.pathname.endsWith("/audit") ? <Audit/> : location.pathname.endsWith("/retention") ? <Storage/> : <People/>;
createRoot(document.getElementById("root")!).render(<AppearanceProvider showControl={false}>
  <p style={{ margin: 0, padding: 10, color: "#342813", background: "#fff1c9" }}>Offline test identity · Real local API · Disposable fictional data</p>
  <EnterpriseProvider config={config} identityFactory={identityFactory}>
    {location.pathname === "/documents" ? <Documents/> : <Layout>{page}</Layout>}
  </EnterpriseProvider>
</AppearanceProvider>);
