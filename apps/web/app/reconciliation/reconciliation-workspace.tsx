"use client";
import { ReportDownload } from "./export/report-download";
import "./comparison-settings.css";
import "./history.css";
import { PageWorkflow } from "../page-workflow";

import { CaseRun, Finding } from "./results/results.types";
import { ResultSummary, CheckCards } from "./results/check-results";
import { checkTitle, decisionReasons } from "./results/result-presentation";
import { DecisionActor } from "./results/decision-actor";
import { ReasonInput } from "../review/reason-input";

import Link from "next/link";
import { API, apiFetch } from "../security/api-fetch";
import { FormEvent, useCallback, useEffect, useRef, useState } from "react";
import { isWorkspaceId, selectionFromUrl } from "../workspace-location";
import { BidiText } from "../bidi-text";

type DocumentSummary = { id: string; originalFilename: string; status: string; documentType?: string };
type ReviewWorkspace = { session?: { id: string; status: string } };
type CaseLink = { id: string; role: string; documentId: string; reviewSessionId: string; extractionRunId?: string };
type ReconciliationCase = {
  id: string; name: string; status: string; concurrencyToken: string;
  reference: string; category: string;
  workflowKind?: string;
  documents: CaseLink[]; runs: CaseRun[];
};
type CaseSummary = { id: string; name: string; reference: string; category: string; status: string; createdAt: string; updatedAt: string; documentCount: number; runCount: number };
type CasePage = { cases: CaseSummary[]; nextOffset: number | null };


const roles = ["Quotation", "PurchaseOrder", "Invoice"] as const;
const categories: Record<string, string> = { Uncategorized: "Uncategorized", OfficeSupplies: "Office supplies", Services: "Services", Equipment: "Equipment" };
const emptyFilters = { q: "", category: "", status: "", createdFrom: "", createdTo: "" };
const defaultTolerances: Record<string, string> = { quantity: "0", unitPrice: "0.01", tax: "0.01", lineTotal: "0.01", documentTotal: "0.01" };
const roleLabels: Record<string, string> = {
  Quotation: "Quotation", PurchaseOrder: "Purchase order", Invoice: "Invoice",
};

function writeCaseLocation(caseId: string | null, runId: string | null = null) {
  const url = new URL(window.location.href);
  if (caseId) url.searchParams.set("case", caseId); else url.searchParams.delete("case");
  if (caseId && runId) url.searchParams.set("run", runId); else url.searchParams.delete("run");
  if (url.href !== window.location.href) window.history.pushState(null, "", `${url.pathname}${url.search}${url.hash}`);
}

async function readResponse<T>(response: Response): Promise<{ value: T; etag: string | null }> {
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { detail?: string } | null;
    throw new Error(problem?.detail ?? `Request failed (${response.status})`);
  }
  return { value: await response.json() as T, etag: response.headers.get("ETag") };
}

export default function ReconciliationWorkspace({ canReview, creationScope, embedded = false, fixedCaseId,
  onDirty, onBusy, onFix, onSaved, onLocationChange, syncLocation = true }: { canReview: boolean; creationScope: string; embedded?: boolean; fixedCaseId?: string;
  syncLocation?: boolean;
  onLocationChange?: () => void;
  onSaved?: (id: string, status: string, token: string | null, previousToken: string | null) => void;
  onDirty?: (dirty: boolean) => void; onBusy?: (busy: boolean) => void; onFix?: (role: string, key?: string, occurrence?: number, documentId?: string, extractionRunId?: string) => void }) {
  const [documents, setDocuments] = useState<DocumentSummary[]>([]);
  const [item, setItem] = useState<ReconciliationCase | null>(null);
  const [etag, setEtag] = useState<string | null>(null);
  const [selected, setSelected] = useState<Record<string, string>>({});
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState<string | null>(null);
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const [cases, setCases] = useState<CaseSummary[]>([]);
  const [nextOffset, setNextOffset] = useState<number | null>(null);
  const [listBusy, setListBusy] = useState(false);
  const [listError, setListError] = useState("");
  const [filterDraft, setFilterDraft] = useState(emptyFilters);
  const appliedFilters = useRef(emptyFilters);
  const [filtersActive, setFiltersActive] = useState(false);
  const [editingDetails, setEditingDetails] = useState(false);
  const [detailsDraft, setDetailsDraft] = useState({ name: "", category: "Uncategorized" });
  const [detailsStale, setDetailsStale] = useState(false);
  const [caseReloadNeeded, setCaseReloadNeeded] = useState(false);
  const [replacing, setReplacing] = useState<Record<string, boolean>>({});
  const [revising, setRevising] = useState<Record<string, boolean>>({});
  const [newComparison, setNewComparison] = useState(false);
  const [tolerances, setTolerances] = useState(defaultTolerances);
  const creationIntent = useRef<{ fingerprint: string; requestKey: string; workflowKind?: "InvoicePurchaseOrder" } | null>(null);
  const [opening, setOpening] = useState(true);
  const [openError, setOpenError] = useState("");
  const [selectedRunId, setSelectedRunId] = useState<string | null>(null);
  const [expandedHistory, setExpandedHistory] = useState<Record<string, boolean>>({});
  const [runError, setRunError] = useState("");
  const caseGeneration = useRef(0);
  const listGeneration = useRef(0);
  const mutation = useRef<number | null>(null);
  const observedSelection = useRef<string | null>(null);

  function writeLocation(id: string | null, runId: string | null = null) {
    if (!syncLocation) return;
    writeCaseLocation(id, runId);
    observedSelection.current = JSON.stringify([id, runId]);
    // The containing workflow must restore this exact run URL if it cancels a later Back action.
    onLocationChange?.();
  }

  const loadCases = useCallback(async (offset = 0) => {
    const generation = ++listGeneration.current;
    setListBusy(true); setListError("");
    if (offset === 0) { setCases([]); setNextOffset(null); }
    try {
      const query = new URLSearchParams({ offset: String(offset) });
      for (const [key, value] of Object.entries(appliedFilters.current)) if (value) query.set(key, value);
      const result = await readResponse<CasePage>(await apiFetch(`${API}/api/reconciliation-cases?${query}`));
      if (generation !== listGeneration.current) return;
      setCases(previous => offset === 0 ? result.value.cases : [...previous, ...result.value.cases.filter(next => !previous.some(old => old.id === next.id))]);
      setNextOffset(result.value.nextOffset);
    } catch (error) { if (generation === listGeneration.current) setListError(error instanceof Error ? error.message : "Could not load cases. Try Refresh cases."); }
    finally { if (generation === listGeneration.current) setListBusy(false); }
  }, []);

  const openCase = useCallback(async (id: string | null) => {
    const generation = ++caseGeneration.current;
    const requestedRunId = new URLSearchParams(window.location.search).get("run");
    observedSelection.current = JSON.stringify([id, requestedRunId]);
    mutation.current = null;
    setItem(null); setEtag(null); setSelected({}); setReasons({}); setSelectedRunId(null); setRunError(""); setExpandedHistory({}); setMessage(""); setBusy(null); setOpenError("");
    setEditingDetails(false); setDetailsStale(false); setCaseReloadNeeded(false); setReplacing({}); setRevising({}); setNewComparison(false); setTolerances(defaultTolerances);
    setOpening(id !== null);
    if (id === null) return;
    if (!isWorkspaceId(id)) { setOpening(false); setOpenError("This case link is invalid."); return; }
    try {
      const response = await apiFetch(`${API}/api/reconciliation-cases/${id}`);
      if (response.status === 404) throw new Error("Case not found or access unavailable.");
      const result = await readResponse<ReconciliationCase>(response);
      if (generation === caseGeneration.current) {
        setItem(result.value); setEtag(result.etag);
        if (requestedRunId !== null) {
          const run = result.value.runs.find(value => value.id.toLowerCase() === requestedRunId.toLowerCase());
          if (run) setSelectedRunId(run.id);
          else setRunError("The requested saved comparison is invalid or unavailable in this case.");
        }
      }
    } catch (error) { if (generation === caseGeneration.current) setOpenError(error instanceof Error ? error.message : "Could not open this case."); }
    finally { if (generation === caseGeneration.current) setOpening(false); }
  }, []);

  function navigateCase(id: string | null) {
    writeLocation(id);
    void openCase(id);
    if (id === null) void loadCases();
  }

  useEffect(() => {
    let active = true;
    const caseRequests = caseGeneration, listRequests = listGeneration;
    const restore = () => {
      const id = fixedCaseId ?? selectionFromUrl("case");
      const signature = JSON.stringify([id, new URLSearchParams(window.location.search).get("run")]);
      if (signature === observedSelection.current) return;
      void openCase(id);
      if (id === null) void loadCases();
    };
    // Read the browser location after hydration; cancel this initialization on unmount.
    void Promise.resolve().then(() => { if (active) { void openCase(fixedCaseId ?? selectionFromUrl("case")); void loadCases(); } });
    if (syncLocation) window.addEventListener("popstate", restore);
    return () => { active = false; window.removeEventListener("popstate", restore); caseRequests.current++; listRequests.current++; };
  }, [loadCases, openCase, fixedCaseId, syncLocation]);

  useEffect(() => {
    let cancelled = false;
    apiFetch(`${API}/api/documents`).then(response => readResponse<DocumentSummary[]>(response))
      .then(result => { if (!cancelled) setDocuments(result.value.filter(document => document.status === "Ready")); })
      .catch(() => { if (!cancelled) setMessage("Document sources could not be loaded. Refresh the page to try again."); });
    return () => { cancelled = true; };
  }, []);

  function applyCase(next: ReconciliationCase, nextEtag: string | null) {
    setItem(next); setEtag(nextEtag);
    onSaved?.(next.id, next.status, nextEtag, etag);
  }

  async function createCase(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const name = String(new FormData(form).get("name") ?? "").trim();
    const category = String(new FormData(form).get("category") ?? "Uncategorized");
    if (!canReview || !name || mutation.current !== null) return;
    const generation = caseGeneration.current;
    mutation.current = generation;
    setBusy("create"); setMessage("");
    try {
      const fingerprint = JSON.stringify([name, category]);
      const storageKey = `case-creation:${creationScope}`;
      if (creationIntent.current?.fingerprint !== fingerprint) {
        try {
          const saved = JSON.parse(sessionStorage.getItem(storageKey) ?? "null");
          if (typeof saved?.fingerprint === "string" && isWorkspaceId(saved.requestKey)) creationIntent.current = saved;
        } catch { /* In-memory retries still work when session storage is unavailable. */ }
      }
      if (creationIntent.current?.fingerprint !== fingerprint) {
        setMessage("No matching earlier request was found in this browser. Use Home to start a new check with both files.");
        return;
      }
      try { sessionStorage.setItem(storageKey, JSON.stringify(creationIntent.current)); } catch { /* Storage is optional. */ }
      // A stored pre-change request keeps its original payload for safe recovery.
      // New work starts with both files on Home; History only replays an existing intent.
      const submittedKey = creationIntent.current.requestKey;
      const result = await readResponse<ReconciliationCase>(await apiFetch(`${API}/api/reconciliation-cases`, {
        method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ name, category, requestKey: submittedKey, workflowKind: creationIntent.current.workflowKind }),
      }));
      if (creationIntent.current?.requestKey === submittedKey) creationIntent.current = null;
      try { if (JSON.parse(sessionStorage.getItem(storageKey) ?? "null")?.requestKey === submittedKey) sessionStorage.removeItem(storageKey); } catch { /* Storage is optional. */ }
      if (generation !== caseGeneration.current) return;
      applyCase(result.value, result.etag); form.reset(); writeLocation(result.value.id);
      setMessage(result.value.workflowKind === "InvoicePurchaseOrder"
        ? "Invoice check recovered. Open its document choices below."
        : "Earlier case recovered. Its saved sources and history remain available.");
    } catch (error) { if (generation === caseGeneration.current) setMessage(`${error instanceof Error ? error.message : "Could not create case"} Retry with the same name and category to recover this creation without making another case.`); }
    finally { if (generation === caseGeneration.current) { mutation.current = null; setBusy(null); } }
  }

  async function sendCaseMutation(path: string, init: RequestInit, generation: number, metadata = false) {
    let response: Response;
    const requireReload = () => { if (generation === caseGeneration.current) { if (metadata) setDetailsStale(true); else setCaseReloadNeeded(true); setEtag(null); } };
    try { response = await apiFetch(`${API}${path}`, init); }
    catch { requireReload(); throw new Error("The result could not be confirmed. Reload case to check what was saved before trying again."); }
    if (response.status === 412 || response.status >= 500) {
      requireReload();
      throw new Error(response.status === 412 ? "This case changed elsewhere. Reload case and check its saved sources, comparisons and decisions before trying again." : "The result could not be confirmed. Reload case to check what was saved before trying again.");
    }
    if (!response.ok) return readResponse<ReconciliationCase>(response);
    try { return await readResponse<ReconciliationCase>(response); }
    catch { requireReload(); throw new Error("The saved response could not be read. Reload case to check what was saved before trying again."); }
  }

  function applyFilters(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (filterDraft.createdFrom && filterDraft.createdTo && filterDraft.createdFrom > filterDraft.createdTo) {
      setListError("Created from must be on or before Created to."); return;
    }
    appliedFilters.current = { ...filterDraft, q: filterDraft.q.trim() };
    setFiltersActive(Object.values(appliedFilters.current).some(Boolean));
    void loadCases();
  }

  async function saveDetails(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canReview || !item || item.status === "Closed" || !etag || detailsStale || mutation.current !== null) return;
    const generation = caseGeneration.current;
    mutation.current = generation; setBusy("metadata"); setMessage("");
    try {
      const result = await sendCaseMutation(`/api/reconciliation-cases/${item.id}`, {
        method: "PUT", headers: { "Content-Type": "application/json", "If-Match": etag },
        body: JSON.stringify({ name: detailsDraft.name.trim(), category: detailsDraft.category }),
      }, generation, true);
      if (generation !== caseGeneration.current) return;
      applyCase(result.value, result.etag); setEditingDetails(false); setMessage("Case details saved. Its reference and comparison history are unchanged.");
    } catch (error) { if (generation === caseGeneration.current) setMessage(error instanceof Error ? error.message : "Could not save case details."); }
    finally { if (generation === caseGeneration.current) { mutation.current = null; setBusy(null); } }
  }

  async function reloadDetails() {
    if (!item || mutation.current !== null) return;
    const generation = caseGeneration.current; mutation.current = generation; setBusy("metadata-reload");
    try {
      const result = await readResponse<ReconciliationCase>(await apiFetch(`${API}/api/reconciliation-cases/${item.id}`));
      if (generation !== caseGeneration.current) return;
      applyCase(result.value, result.etag); setDetailsStale(false); setCaseReloadNeeded(false);
      setMessage(result.value.status === "Closed" ? "This case is now closed. Your unsaved details remain below for copying; they cannot be saved." : "Latest saved case loaded. Your drafts are preserved; check the saved sources, comparisons and decisions before submitting again.");
    } catch (error) { if (generation === caseGeneration.current) setMessage(error instanceof Error ? error.message : "Could not reload case details."); }
    finally { if (generation === caseGeneration.current) { mutation.current = null; setBusy(null); } }
  }

  async function linkDocument(role: string) {
    if (!canReview || !item || item.status === "Closed" || !etag || !selected[role] || mutation.current !== null) return;
    const generation = caseGeneration.current;
    mutation.current = generation;
    setBusy(`link-${role}`); setMessage("");
    try {
      const review = await readResponse<ReviewWorkspace>(await apiFetch(`${API}/api/documents/${selected[role]}/review`));
      if (generation !== caseGeneration.current) return;
      if (!review.value.session || review.value.session.status !== "Completed")
        throw new Error(`${roleLabels[role]} requires a completed review.`);
      const existing = item.documents.find(link => link.role === role);
      if (existing?.documentId === selected[role] && existing.reviewSessionId === review.value.session.id) {
        setReplacing(current => ({ ...current, [role]: false })); setMessage("That review version is already pinned. No change was needed."); return;
      }
      const result = await sendCaseMutation(
        `/api/reconciliation-cases/${item.id}/documents/${role}`, {
          method: "PUT", headers: { "Content-Type": "application/json", "If-Match": etag },
          body: JSON.stringify({ documentId: selected[role], reviewSessionId: review.value.session.id }),
        }, generation);
      if (generation === caseGeneration.current) { applyCase(result.value, result.etag); setReplacing(current => ({ ...current, [role]: false })); setMessage(existing ? `${roleLabels[role]} pinned review replaced. Saved comparisons keep their original sources.` : `${roleLabels[role]} review pinned to the case.`); }
    } catch (error) { if (generation === caseGeneration.current) setMessage(error instanceof Error ? error.message : "Could not link document"); }
    finally { if (generation === caseGeneration.current) { mutation.current = null; setBusy(null); } }
  }

  async function evaluate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canReview || !item || item.status === "Closed" || !etag || mutation.current !== null || (item.runs.length > 0 && !newComparison)) return;
    const generation = caseGeneration.current;
    mutation.current = generation;
    const form = new FormData(event.currentTarget);
    const numeric = (key: string) => {
      const raw = String(form.get(key) ?? "").trim(); return raw === "" ? null : Number(raw);
    };
    setBusy("evaluate"); setMessage("");
    setNewComparison(true);
    try {
      const result = await sendCaseMutation(
        `/api/reconciliation-cases/${item.id}/evaluations`, {
          method: "POST", headers: { "Content-Type": "application/json", "If-Match": etag },
          body: JSON.stringify({
            quantityTolerance: numeric("quantity"), unitPriceTolerance: numeric("unitPrice"),
            taxTolerance: numeric("tax"), lineTotalTolerance: numeric("lineTotal"),
            documentTotalTolerance: numeric("documentTotal"),
          }),
        }, generation);
      if (generation !== caseGeneration.current) return;
      applyCase(result.value, result.etag);
      setNewComparison(false);
      setSelectedRunId(result.value.runs[0]?.id ?? null);
      setRunError(""); writeLocation(result.value.id, result.value.runs[0]?.id ?? null);
      setMessage(`Evaluation ${result.value.runs[0]?.runNumber ?? ""} completed with ${result.value.runs[0]?.findings.length ?? 0} findings.`);
    } catch (error) { if (generation === caseGeneration.current) setMessage(error instanceof Error ? error.message : "Evaluation failed"); }
    finally { if (generation === caseGeneration.current) { mutation.current = null; setBusy(null); } }
  }

  async function resolveFinding(runId: string, finding: Finding, action: string) {
    if (!canReview || !item || item.status === "Closed" || !etag || mutation.current !== null) return;
    const reason = reasons[finding.id]?.trim();
    if (!reason) { setMessage("Add a reason before deciding a finding."); return; }
    const previous = finding.resolutions.at(-1);
    if (previous && (!revising[finding.id] || previous.action === action && previous.reason.trim() === reason)) return;
    const generation = caseGeneration.current;
    mutation.current = generation;
    setBusy(finding.id); setMessage("");
    try {
      const result = await sendCaseMutation(
        `/api/reconciliation-cases/${item.id}/runs/${runId}/findings/${finding.id}/resolutions`, {
          method: "POST", headers: { "Content-Type": "application/json", "If-Match": etag },
          body: JSON.stringify({ action, reason }),
        }, generation);
      if (generation !== caseGeneration.current) return;
      applyCase(result.value, result.etag);
      setReasons(current => ({ ...current, [finding.id]: "" }));
      setRevising(current => ({ ...current, [finding.id]: false }));
      setMessage(`${finding.ruleCode} recorded as ${action.toLowerCase()}.`);
    } catch (error) { if (generation === caseGeneration.current) setMessage(error instanceof Error ? error.message : "Could not record decision"); }
    finally { if (generation === caseGeneration.current) { mutation.current = null; setBusy(null); } }
  }

  useEffect(() => { onDirty?.(Object.values(reasons).some(reason => !!reason.trim())); }, [reasons, onDirty]);
  useEffect(() => { onBusy?.(busy !== null); }, [busy, onBusy]);

  const Container = embedded ? "section" : "main";
  const latest = runError ? undefined : item?.runs.find(run => run.id === selectedRunId) ?? item?.runs[0];
  const canChange = canReview && item?.status !== "Closed";
  const pairWorkflow = item?.workflowKind === "InvoicePurchaseOrder";
  function renderDecision(finding: Finding) {
    if (!latest) return null;
    const resolution = finding.resolutions.at(-1);
    const check = latest.checks?.find(value => value.findingId === finding.id);
    const reasonLabel = check ? `Reason for ${checkTitle(check)}` : `Reason for ${finding.ruleCode}`;
    const decisionLabels: Record<string, string> = check ? { Resolved: "Record follow-up", Accepted: check.status === "CouldNotCheck" ? "Acknowledge missing check" : "Accept difference", Dismissed: "No action needed" } : {};
    return <div className="result-decision"><p>A decision records your follow-up. It does not change this check or make a missing check pass.</p>
            {resolution && <p className="resolution-history">Latest decision: {resolution.action} — <bdi>{resolution.reason}</bdi></p>}
            {finding.resolutions.length > 0 && <details onToggle={event => { const open = event.currentTarget.open; setExpandedHistory(current => ({ ...current, [finding.id]: open })); }}><summary>Decision history ({finding.resolutions.length})</summary>{expandedHistory[finding.id] && <ol>{finding.resolutions.map(decision => <li key={decision.id}><strong>{decision.action}</strong> · <bdi>{decision.reason}</bdi><p>Decision {decision.sequenceNumber} · {new Date(decision.createdAt).toLocaleString()}{decision.actorId && <> · <DecisionActor actorId={decision.actorId} /></>}</p></li>)}</ol>}</details>}
            {canChange && resolution && !revising[finding.id] && <button className="secondary" disabled={busy !== null || !etag} onClick={() => { setReasons(current => ({ ...current, [finding.id]: current[finding.id] || resolution.reason })); setRevising(current => ({ ...current, [finding.id]: true })); }}>Revise decision</button>}
            {canChange && (!resolution || revising[finding.id]) && <div className="resolution-controls"><ReasonInput disabled={busy !== null || !etag} label={reasonLabel} choices={decisionReasons(finding, check)} value={reasons[finding.id] ?? ""} onChange={value => setReasons(current => ({ ...current, [finding.id]: value }))} required/>
              {(["Resolved", "Accepted", "Dismissed"] as const).map(action => <button key={action} disabled={busy !== null || !etag || !reasons[finding.id]?.trim() || resolution?.action === action && resolution.reason.trim() === reasons[finding.id]?.trim()}
                className={action === "Dismissed" ? "secondary" : ""} onClick={() => resolveFinding(latest.id, finding, action)}>{decisionLabels[action] ?? action}</button>)}
              {resolution && <><p>Change the decision or its reason to add a revision. Earlier decisions remain in history.</p><button className="secondary" disabled={busy !== null} onClick={() => { setRevising(current => ({ ...current, [finding.id]: false })); setReasons(current => ({ ...current, [finding.id]: "" })); }}>Cancel revision</button></>}</div>}</div>;
  }

  return <Container className="reconciliation-page">
    {!embedded && <>
    <header className="page-heading reconciliation-hero"><h1>Earlier cases and history</h1>
      <p>Open a saved case to read its comparisons, evidence and decisions. For a new invoice check, start from Home.</p>
    </header>
    <PageWorkflow label="How to read case history" steps={[
      { title: "Find a case", description: "Search by its name or reference, then open it." },
      { title: "Choose a comparison", description: "Select the saved version you want to read." },
      { title: "Read evidence and decisions", description: "Check the sources, differences and recorded reasons." },
    ]}/>

    {(item || opening || openError) && <div className="case-navigation-actions">
      <button className="secondary" onClick={() => navigateCase(null)}>All cases</button>
      {item && <Link className="app-button" href={`/questions?case=${item.id}`}>Ask about this case’s documents →</Link>}
    </div>}
    {opening && <p role="status">Opening case…</p>}
    {openError && <div role="alert"><p>{openError}</p><button onClick={() => void openCase(fixedCaseId ?? selectionFromUrl("case"))}>Retry opening case</button></div>}
    {!item && !opening && !openError && <>      <section className="saved-cases" aria-label="Saved cases"><div className="saved-cases-heading"><h2>Saved cases</h2><button className="secondary" disabled={listBusy} onClick={() => void loadCases()}>Refresh cases</button></div>
        <form className="case-filters" onSubmit={applyFilters}>
          <label>Search cases<input type="search" dir="auto" maxLength={200} placeholder="Reference or name" value={filterDraft.q} onChange={event => setFilterDraft(current => ({ ...current, q: event.target.value }))}/></label>
          <label>Category filter<select value={filterDraft.category} onChange={event => setFilterDraft(current => ({ ...current, category: event.target.value }))}><option value="">All categories</option>{Object.entries(categories).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
          <label>Status filter<select value={filterDraft.status} onChange={event => setFilterDraft(current => ({ ...current, status: event.target.value }))}><option value="">All statuses</option>{["Draft", "Evaluated", "Closed"].map(value => <option key={value}>{value}</option>)}</select></label>
          <label>Created from (UTC)<input type="date" value={filterDraft.createdFrom} onChange={event => setFilterDraft(current => ({ ...current, createdFrom: event.target.value }))}/></label>
          <label>Created to (UTC)<input type="date" value={filterDraft.createdTo} onChange={event => setFilterDraft(current => ({ ...current, createdTo: event.target.value }))}/></label>
          <div><button>Apply filters</button><button type="button" className="secondary" onClick={() => { appliedFilters.current = emptyFilters; setFilterDraft(emptyFilters); setFiltersActive(false); void loadCases(); }}>Clear filters</button></div>
        </form>
        {listError && <p role="alert">{listError}</p>}
        {listBusy && <p role="status">Loading cases…</p>}
        {!listBusy && !listError && cases.length === 0 && <p>{filtersActive ? "No cases match these filters." : canReview ? "No saved cases yet. Start an invoice check from Home." : "No saved cases are available to this account yet."}</p>}
        <ul>{cases.map(value => <li key={value.id}>
          <a href={`/reconciliation?case=${value.id}`} onClick={event => { if (event.button === 0 && !event.metaKey && !event.ctrlKey && !event.shiftKey && !event.altKey) { event.preventDefault(); navigateCase(value.id); } }}><bdi>{value.name}</bdi></a>
          <span><bdi>{value.reference}</bdi> · {categories[value.category] ?? "Uncategorized"} · {value.status} · {value.documentCount} saved sources · {value.runCount} comparisons</span><small>Created {new Date(value.createdAt).toLocaleString()}</small>
        </li>)}</ul>
        {nextOffset !== null && <button disabled={listBusy} onClick={() => void loadCases(nextOffset)}>Load more cases</button>}
      </section>
      {canReview && <details className="case-create"><summary>Recover an earlier request</summary><h2>Recover an interrupted save</h2>
      <p>Use the same name and category as your earlier request in this browser. Its saved documents and history stay unchanged. To start a new check, use Home and choose both files first.</p>
      <form onSubmit={createCase}><label>Case name<input dir="auto" name="name" placeholder="Office supplies · September" maxLength={200} disabled={busy !== null} required/></label>
        <label>Category<select name="category" defaultValue="Uncategorized" disabled={busy !== null}>{Object.entries(categories).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
        <button disabled={busy === "create"}>Recover request</button></form></details>}</>}

    {item && <>
      {caseReloadNeeded && <div role="alert"><p>The saved case must be checked before another change. Reloading keeps your drafts and does not repeat the action.</p><button className="secondary" disabled={busy !== null} onClick={reloadDetails}>Reload case</button></div>}
      <section className="case-summary"><div><h2 dir="auto">{item.name}</h2>
        <p>{item.reference && <><bdi>{item.reference}</bdi> · </>}{categories[item.category] ?? "Uncategorized"} · {item.status} · {item.documents.length}/{pairWorkflow ? 2 : 3} reviewed documents pinned</p></div><details><summary>Case identifier</summary><code>{item.id}</code></details></section>
      {!pairWorkflow && <><p>This earlier case keeps its quotation workflow. Its reviews, saved results and decisions remain available.</p><div className="case-return-action"><Link className="app-button" href="/invoices">Start a separate invoice check</Link> for new work.</div></>}
      {pairWorkflow && <div className="case-return-action"><Link className="app-button" href={`/invoices?case=${item.id}`}>Return to invoice check</Link><p>Select documents or create a new comparison from their completed reviews.</p></div>}
      {canChange && !editingDetails && <button className="secondary" disabled={busy !== null} onClick={() => { setDetailsDraft({ name: item.name, category: item.category ?? "Uncategorized" }); setEditingDetails(true); }}>Edit case details</button>}
      {editingDetails && <form className="case-details-form" aria-label="Case details" onSubmit={saveDetails}><h3>Edit case details</h3><p>The reference stays the same. Renaming or changing category does not change saved comparisons.</p>
        <label>Case name<input dir="auto" required maxLength={200} readOnly={!canChange} disabled={busy !== null} value={detailsDraft.name} onChange={event => setDetailsDraft(current => ({ ...current, name: event.target.value }))}/></label>
        <label>Category<select disabled={busy !== null || !canChange} value={detailsDraft.category} onChange={event => setDetailsDraft(current => ({ ...current, category: event.target.value }))}>{Object.entries(categories).map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
        {detailsStale && <button type="button" className="secondary" disabled={busy !== null} onClick={reloadDetails}>Reload case details</button>}
        <div><button disabled={!canChange || !etag || detailsStale || busy !== null || !detailsDraft.name.trim()}>Save case details</button><button type="button" className="secondary" disabled={busy !== null} onClick={() => setEditingDetails(false)}>Cancel editing</button></div>
      </form>}
      {runError && <div role="alert"><p>{runError}</p><button className="secondary" onClick={() => { setRunError(""); setSelectedRunId(null); writeLocation(item.id); }}>Show latest comparison</button></div>}
      {!canChange && <p>{canReview ? "This case is closed. Its saved comparisons and decisions are read only." : "Read-only access. A reviewer or operator can change this case."}</p>}
      {canReview && item.status === "Closed" && Object.values(reasons).some(value => value.trim()) && <section aria-label="Unsaved decision drafts"><h3>Unsaved decision drafts</h3><p>These inputs were retained. Check the saved decision history before copying anything you still need.</p>{item.runs.flatMap(run => run.findings.filter(finding => reasons[finding.id]?.trim()).map(finding => <p key={finding.id}>Evaluation {run.runNumber} · {finding.ruleCode}: <bdi>{reasons[finding.id]}</bdi></p>))}</section>}
      {canReview && item.status === "Closed" && Object.entries(replacing).some(([role, open]) => open && selected[role]) && <section aria-label="Unsaved source selections"><h3>Unsaved source selections</h3><p>These selections were retained. Check the pinned reviews to see what was saved.</p>{Object.entries(replacing).filter(([role, open]) => open && selected[role]).map(([role]) => <p key={role}>{roleLabels[role]}: <bdi>{documents.find(document => document.id === selected[role])?.originalFilename ?? selected[role]}</bdi></p>)}</section>}
      {canReview && item.status === "Closed" && newComparison && <section aria-label="Unsaved comparison settings"><h3>Unsaved comparison settings</h3><p>No new comparison can be submitted to this closed case. Check saved comparisons before reusing these settings elsewhere.</p>{Object.entries(tolerances).map(([key, value]) => <p key={key}>{key}: {value}</p>)}</section>}
      {!pairWorkflow && <section className="link-grid"><div className="section-heading"><h2>Current case sources</h2><p>These are the reviews currently pinned to the case. Opening a document shows its current workspace; it may contain a newer review than the pinned version.</p></div>
        {roles.map(role => {
          const linked = item.documents.find(document => document.role === role);
          const source = documents.find(document => document.id === linked?.documentId);
          return <article key={role} className={linked ? "source-card linked" : "source-card"}>
            <span>{roleLabels[role]}</span>{linked ? <><strong dir="auto">{source?.originalFilename ?? linked.documentId}</strong>
              <small>Review {linked.reviewSessionId.slice(0, 8)}… pinned</small><details><summary>Pinned version identifiers</summary><p>Review ID: <bdi>{linked.reviewSessionId}</bdi></p>{linked.extractionRunId && <p>Extraction ID: <bdi>{linked.extractionRunId}</bdi></p>}</details><Link className="app-button" href={`/?document=${linked.documentId}`}>Open document</Link>
              {canChange && !replacing[role] && <button className="secondary" disabled={busy !== null || !etag} onClick={() => { setSelected(current => ({ ...current, [role]: linked.documentId })); setReplacing(current => ({ ...current, [role]: true })); }}>Replace pinned review</button>}</> : !canChange && <p>No review pinned.</p>}
            {canChange && (!linked || replacing[role]) && <>
              {linked && <p>Choose a completed review to use in future comparisons. Saved comparisons keep their original sources.</p>}
              <select disabled={busy !== null} aria-label={`${roleLabels[role]} document`} value={selected[role] ?? ""}
                onChange={event => setSelected(current => ({ ...current, [role]: event.target.value }))}>
                <option value="">Select a ready document</option>{documents.map(document =>
                  <option key={document.id} value={document.id}>{document.originalFilename}</option>)}</select>
              <button disabled={!selected[role] || busy !== null || !etag} onClick={() => linkDocument(role)}>{linked ? "Pin replacement" : "Pin review"}</button>
              {linked && <button className="secondary" disabled={busy !== null} onClick={() => setReplacing(current => ({ ...current, [role]: false }))}>Cancel replacement</button>}</>}
          </article>;
        })}
      </section>}
      {!pairWorkflow && canChange && item.runs.length > 0 && !newComparison && <section><p>A comparison is already saved. Open it below, or deliberately create another using the current pinned reviews and your chosen tolerances.</p><button className="secondary" disabled={busy !== null || !etag} onClick={() => { setTolerances(defaultTolerances); setNewComparison(true); }}>New comparison</button></section>}
      {!pairWorkflow && canChange && (item.runs.length === 0 || newComparison) && <section className="tolerance-panel"><div><h2>Compare reviewed documents</h2>
        <p>The default settings allow a quantity difference of 0 and an amount difference of 0.01.{item.runs.length > 0 && " Saved comparisons keep their original settings."}</p></div><form className="comparison-form" onSubmit={evaluate}><details className="comparison-settings" onInvalid={event => { event.currentTarget.open = true; }}><summary>Settings</summary>
          <p>Absolute tolerances: a difference equal to the tolerance passes.</p><div className="comparison-settings-fields">{[
          ["quantity", "Quantity", "0"], ["unitPrice", "Unit price", "0.01"], ["tax", "Tax / VAT", "0.01"],
          ["lineTotal", "Line total", "0.01"], ["documentTotal", "Document total", "0.01"],
        ].map(([name, label]) => <label key={name}>{label}<input disabled={busy !== null || !etag} name={name} type="number" min="0" step="0.01" value={tolerances[name]} onChange={event => setTolerances(current => ({ ...current, [name]: event.target.value }))}/></label>)}</div></details>
          <div className="comparison-submit"><button disabled={busy !== null || !etag || item.documents.length === 0}>Run comparison</button>{item.runs.length > 0 && <button type="button" className="secondary" disabled={busy !== null} onClick={() => setNewComparison(false)}>Cancel new comparison</button>}</div></form></section>}
    </>}

    </>}
    {embedded && opening && <p role="status">Opening saved results...</p>}
    {embedded && openError && <p role="alert">{openError}</p>}
    {embedded && (caseReloadNeeded || runError) && <div role="alert"><p>{runError || "The save needs checking. Reload before making another decision."}</p><button onClick={reloadDetails}>Reload results</button></div>}
    {message && <p className="reconciliation-message" role="status">{message}</p>}

    {latest && <section className="findings"><div className="section-heading">
      <h2>Saved comparison {latest.runNumber}</h2><p>Results from the saved reviewed documents</p></div>
      <label>Saved comparison<select value={latest.id} disabled={busy !== null} onChange={event => { setSelectedRunId(event.target.value); if (item) writeLocation(item.id, event.target.value); }}>{item?.runs.map(run => <option key={run.id} value={run.id}>Comparison {run.runNumber}{run.id === item.runs[0]?.id ? " · Latest" : ""}</option>)}</select></label>
      <p>Values, supporting text and allowed differences below belong to comparison {latest.runNumber}. Later document edits do not update this comparison. Decisions are recorded separately and do not change source documents.</p>
      <ResultSummary run={latest}/>
      {item && <ReportDownload caseId={item.id} runId={latest.id} version={item.concurrencyToken} disabled={busy !== null}/>}
      {!!latest.checks?.length && <CheckCards run={latest} onFix={embedded ? onFix : undefined} busy={busy !== null} renderDecision={renderDecision}/>}
      {latest.findings.length === 0 ? (!latest.checks?.length && <div className="match-state"><strong>No recorded differences</strong><p>Read the check coverage above. Missing checks are not a complete match.</p></div>) :
        <div className="finding-list">{latest.findings.filter(finding => !latest.checks?.some(check => check.findingId === finding.id)).map(finding => {
          const left = latest.fields.find(field => field.id === finding.leftFieldSnapshotId);
          const right = latest.fields.find(field => field.id === finding.rightFieldSnapshotId);
          const resolution = finding.resolutions.at(-1);
          return <article key={finding.id} className={`finding finding-${finding.severity.toLowerCase()}`}>
            <div className="finding-head"><span>{finding.ruleCode}</span><i>{finding.severity}</i>{resolution && <b>{resolution.action}</b>}</div>
            <h3>{finding.explanation}</h3><div className="comparison-values"><div><small>{roleLabels[finding.leftRole ?? ""] ?? finding.leftRole ?? "First document"}</small>
              <strong dir="auto">{finding.leftValue ?? "Unavailable"}</strong><em>{left?.page != null ? `Page ${left.page}` : "Source page unavailable"}</em>{left?.quote ? <blockquote dir="auto"><BidiText text={left.quote}/></blockquote> : <p>No source excerpt was saved.</p>}</div><div><small>{roleLabels[finding.rightRole ?? ""] ?? finding.rightRole ?? "Second document"}</small>
              <strong dir="auto">{finding.rightValue ?? "Unavailable"}</strong><em>{right?.page != null ? `Page ${right.page}` : "Source page unavailable"}</em>{right?.quote ? <blockquote dir="auto"><BidiText text={right.quote}/></blockquote> : <p>No source excerpt was saved.</p>}</div>{finding.difference != null && <div className="delta"><small>Difference / tolerance</small>
              <strong>{finding.difference} / {finding.tolerance}</strong></div>}</div>
            {embedded && onFix && <button className="secondary" disabled={busy !== null} onClick={() => onFix(right?.role ?? finding.rightRole ?? "Invoice", right?.key, right?.occurrence, latest.sources?.find(source => source.id === right?.documentSnapshotId)?.documentId, latest.sources?.find(source => source.id === right?.documentSnapshotId)?.extractionRunId)}>Check this value</button>}
            {renderDecision(finding)}
          </article>;
        })}</div>}
    </section>}
  </Container>;
}
