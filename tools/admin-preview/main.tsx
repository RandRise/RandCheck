// Actual application components, with a fictional identity and isolated sample API.
import { createRoot } from "react-dom/client";
import { AppearanceProvider } from "../../apps/web/app/appearance";
import { EnterpriseProvider } from "../../apps/web/app/security/enterprise-provider";
import Layout from "../../apps/web/app/organization/layout";
import People from "../../apps/web/app/organization/page";
import Audit from "../../apps/web/app/organization/audit/page";
import Storage from "../../apps/web/app/organization/retention/page";
import "../../apps/web/app/styles.css";
import "./preview.css";
import { createActivityPreviewFetch } from "./activity-fetch.mjs";
import { createStoragePreviewFetch } from "./storage-fetch.mjs";

window.fetch = createActivityPreviewFetch(createStoragePreviewFetch(window.fetch.bind(window), window.location.origin, window.localStorage), window.location.origin);

const config = { mode: "Entra", tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", clientId: "cccccccc-cccc-cccc-cccc-cccccccccccc",
  scope: "api://bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/DocumentIntelligence.Access", api: window.location.origin };
const identity = async () => ({ initialize: async () => true, token: async () => "fictional-admin-preview",
  signIn: async () => {}, signOut: async () => {} });
const path = window.location.pathname;
const content = path === "/organization" ? <People/> : path === "/organization/audit" ? <Audit/>
  : path === "/organization/retention" ? <Storage/> : <main className="organization-page"><h2>Outside this sample</h2><p>This sample covers administration only.</p></main>;

createRoot(document.getElementById("root")!).render(<AppearanceProvider showControl={false}>
  <div className="sample-admin-banner" role="note"><strong>Sample Admin view</strong><span>Fictional people, activity and storage controls. Saves never change your workspace. Storage examples stay in this browser; restarting the sample clears its people changes.</span></div>
  <EnterpriseProvider config={config} identityFactory={identity}><Layout>{content}</Layout></EnterpriseProvider>
</AppearanceProvider>);
