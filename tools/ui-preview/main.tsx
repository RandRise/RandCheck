// Tooling-only composition of the ACTUAL app components. No production imports this file.
import { useEffect, useState } from "react";
import { createRoot } from "react-dom/client";
import { EnterpriseProvider } from "../../apps/web/app/security/enterprise-provider";
import Home from "../../apps/web/app/page";
import Documents from "../../apps/web/app/documents/page";
import Reconciliation from "../../apps/web/app/reconciliation/page";
import Invoices from "../../apps/web/app/invoices/page";
import NewInvoiceCheck from "../../apps/web/app/invoices/new/page";
import Questions from "../../apps/web/app/questions/page";
import Organization from "../../apps/web/app/organization/page";
import Audit from "../../apps/web/app/organization/audit/page";
import Retention from "../../apps/web/app/organization/retention/page";
import "../../apps/web/app/styles.css";
import "./preview.css";
import DesignProposal from "./design-proposal";
import { installPreviewFetchBridge } from "./comparison-bridge.mjs";
import { createPreviewLibraryFetch } from "./library-search.mjs";
import { AppearanceProvider } from "../../apps/web/app/appearance";

installPreviewFetchBridge(window);
window.fetch = createPreviewLibraryFetch(window.fetch, window.location.origin);

const config = { mode: "Entra", tenantId: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", clientId: "cccccccc-cccc-cccc-cccc-cccccccccccc",
  scope: "api://bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb/DocumentIntelligence.Access", api: window.location.origin };
const factory = async () => ({
  initialize: async () => sessionStorage.getItem("ui-preview-person") !== "signed-out",
  token: async () => `preview-${sessionStorage.getItem("ui-preview-person") ?? "operator"}`,
  signIn: async () => { sessionStorage.setItem("ui-preview-person", "operator"); window.location.assign("/"); },
  signOut: async () => { sessionStorage.setItem("ui-preview-person", "signed-out"); window.location.assign("/signin"); }
});
function PreviewControls() {
  const [scenario, setScenario] = useState("normal");
  const [error, setError] = useState("");
  const [confirmReset, setConfirmReset] = useState(false);
  useEffect(() => { void fetch("/__preview/scenario").then(r => r.json()).then(s => setScenario(s.scenario)).catch(() => setError("Preview server unavailable. Start preview again.")); }, []);
  async function change(path: string, body: object) {
    try {
      const response = await fetch(path, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(body) });
      if (!response.ok) throw new Error();
      window.location.reload();
    } catch { setError("Preview control failed. Check that the preview server is running."); }
  }
  return <aside className="preview-controls" aria-label="Preview controls">
    <div className="preview-controls-row"><strong>Live UI preview</strong><span>Sample data · no Docker · no real Microsoft login</span>
      <label>View as <select aria-label="Preview account" value={sessionStorage.getItem("ui-preview-person") ?? "operator"} onChange={e => {
        sessionStorage.setItem("ui-preview-person", e.target.value);
        window.location.assign(e.target.value === "signed-out" ? "/signin" : "/");
      }}>{[["operator", "Staff"], ["admin", "Admin"], ["revoked", "Disabled account"], ["signed-out", "Signed out"]].map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
      <label>Data <select aria-label="Preview scenario" value={scenario} onChange={e => void change("/__preview/scenario", { scenario: e.target.value })}>
        <option value="normal">Sample documents</option><option value="empty">Empty workspace</option><option value="unavailable">Service unavailable</option>
      </select></label><button onClick={() => setConfirmReset(true)}>Reset sample data</button>
    </div>
    {confirmReset && <p>Discard all simulated changes in this preview session? <button onClick={() => void change("/__preview/reset", {})}>Confirm reset</button> <button onClick={() => setConfirmReset(false)}>Cancel</button></p>}
    <p id="preview-role-compatibility" hidden>Existing sample data is preserved. User management needs the updated preview server; this running session cannot test role migration.</p>
    <p>Sample comparisons only: Show results uses the older simulation with unknown scope. Missing checks are not a match. This does not run the current backend rules.</p>
    <p>Library search is simulated from retained sample files. Real server paging and search require the .NET backend.</p>
    {error && <p role="alert">{error}</p>}
    <details><summary>What you can test</summary><p>These are the actual app screens. Saved sample changes survive refresh and navigation, but reset when you stop the preview. Unsaved forms may reset after code edits.</p>
      <p><a href="/design-preview">Archived A/B design proposal</a> records the earlier design choice.</p>
      <p>Try correcting the first invoice, or start an invoice check with an invoice and purchase order. Earlier quotation cases remain under Other tools → Earlier cases and history for history and follow-up. Questions about “total” or “amount” have sample answers. Switch to Admin to inspect simulated audit events.</p>
      <p>Uploads, OCR, semantic search, real identity/security, Next.js server rendering and database durability need the integration environment. The retained preview server uses older comparison simulations and cannot verify invoice-po-2.0. Do not reset it to test the new rules. This preview does not verify those systems.</p>
      <p>Use the app's side menu to move between screens. Home holds invoice checks; Document library holds saved files.</p>
    </details>
  </aside>;
}
const pages: Record<string, typeof Home> = { "/": Home, "/documents": Documents, "/invoices": Invoices, "/invoices/new": NewInvoiceCheck, "/reconciliation": Reconciliation, "/questions": Questions,
  "/organization": Organization, "/organization/audit": Audit, "/organization/retention": Retention, "/signin": Home };
const Page = pages[window.location.pathname];
createRoot(document.getElementById("root")!).render(window.location.pathname === "/design-preview" ? <DesignProposal/> : <><PreviewControls/>
  {Page ? <AppearanceProvider showControl={false}><EnterpriseProvider config={config} identityFactory={factory}><Page/></EnterpriseProvider></AppearanceProvider>
    : <main><h1>This route is not in the UI preview.</h1><a href="/">Open Home</a></main>}
</>);
