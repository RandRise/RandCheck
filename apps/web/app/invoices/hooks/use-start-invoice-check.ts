import { useCallback, useEffect, useRef, useState } from "react";
import { documentRoleIssue } from "../document-role";
import { roles } from "../workflow.constants";
import type { Document, Role } from "../workflow.types";
import { loadDocuments } from "../services/invoice-service";
import { prepareCheck } from "../services/prepared-check";
import { clearRecoveredCreation, creationKey, emptyStartDraft, readStartDraft, saveStartDraft, type StartDraft } from "../services/start-check-storage";

/** The unsaved pair has no case ID until an explicit Continue. It never pins reviews. */
export function useStartInvoiceCheck(identity: string, allowed: boolean, onCreated: (id: string) => void) {
  const [draft, setDraft] = useState<StartDraft>(emptyStartDraft);
  const [documents, setDocuments] = useState<Document[]>([]);
  const [loading, setLoading] = useState(true);
  const [listError, setListError] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const state = useRef(draft);
  const active = useRef(false);
  const epoch = useRef(0);
  const listEpoch = useRef(0);
  const locked = useRef(false);

  const refresh = useCallback(async () => {
    const owner = ++listEpoch.current;
    setLoading(true); setListError("");
    try {
      const result = await loadDocuments();
      if (active.current && owner === listEpoch.current) setDocuments(result.value);
    } catch (error) {
      if (active.current && owner === listEpoch.current) setListError(error instanceof Error ? error.message : "Could not load saved documents.");
    } finally { if (active.current && owner === listEpoch.current) setLoading(false); }
  }, []);

  useEffect(() => {
    active.current = true;
    let disposed = false;
    const requests = epoch, lists = listEpoch;
    // Restore browser-only storage after hydration; a replaced account must not
    // apply its queued initialization to the next mounted workspace.
    void Promise.resolve().then(() => {
      if (disposed) return;
      const restored = readStartDraft(identity);
      state.current = restored; setDraft(restored);
      if (allowed) void refresh(); else setLoading(false);
    });
    return () => { disposed = true; active.current = false; requests.current++; lists.current++; };
  }, [identity, allowed, refresh]);

  function update(next: StartDraft) {
    state.current = next; setDraft(next); saveStartDraft(identity, next);
  }

  function select(role: Role, id: string) {
    if (!allowed || locked.current || state.current.intent) return;
    update({ selections: { ...state.current.selections, [role]: id } });
    setMessage("");
  }

  async function uploaded(role: Role, document: Document) {
    if (!active.current) return;
    // Keep the accepted receipt visible even while the work-list catches up.
    setDocuments(values => [...values.filter(value => value.id !== document.id), document]);
    const issue = documentRoleIssue(document.documentType, role);
    if (issue) throw new Error(issue);
    select(role, document.id);
  }

  const ready = roles.every(role => {
    const doc = documents.find(value => value.id === draft.selections[role]);
    return !!doc && !documentRoleIssue(doc.documentType, role);
  }) && draft.selections.Invoice !== draft.selections.PurchaseOrder;

  async function continueToReview() {
    // An uncertain creation must stay recoverable if extraction changes a type
    // or a file disappears. Resolve its original key, but do not save new links.
    if (!allowed || locked.current || loading || !ready && !state.current.intent) return;
    locked.current = true; setBusy(true); setMessage("");
    const owner = ++epoch.current;
    const current = () => active.current && epoch.current === owner;
    const intent = state.current.intent ?? {
      selections: { ...state.current.selections }, creationKey: creationKey(identity),
      draftKeys: { Invoice: crypto.randomUUID(), PurchaseOrder: crypto.randomUUID() },
    };
    update({ selections: intent.selections, intent });
    try {
      const result = await prepareCheck(intent, current, receipt => update({ selections: receipt.selections, intent: receipt }), ready);
      if (!current() || !result) return;
      clearRecoveredCreation(identity, intent.creationKey);
      saveStartDraft(identity, null);
      onCreated(result.id);
    } catch (error) {
      if (current()) setMessage(`${error instanceof Error ? error.message : "Could not prepare this check."} Your files are kept. Retry preparation checks the same request and saved progress.`);
    } finally { if (current()) { locked.current = false; setBusy(false); } }
  }

  function openSaved() {
    const intent = state.current.intent;
    if (locked.current || !intent?.caseId) return;
    clearRecoveredCreation(identity, intent.creationKey);
    saveStartDraft(identity, null);
    onCreated(intent.caseId);
  }

  return { draft, documents, loading, listError, message, busy, ready, refresh, select, uploaded, continueToReview, openSaved };
}
