"use client";

import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { isWorkspaceId, selectionFromUrl, writeSelection } from "../../workspace-location";
import * as invoiceService from "../services/invoice-service";
import { labels } from "../workflow.constants";
import { savedSelections, isPairReady, hasUnsavedSelections } from "../workflow.selectors";
import type { Case, Document, Role, ReviewChoice } from "../workflow.types";
import { documentRoleIssue } from "../document-role";

export function useInvoiceWorkflow(canReview: boolean, identity: string) {
  const [item, setItem] = useState<Case | null>(null);
  const [etag, setEtag] = useState<string | null>(null);
  const [documents, setDocuments] = useState<Document[]>([]);
  const [cases, setCases] = useState<Case[]>([]);
  const [nextOffset, setNextOffset] = useState<number | null>(null);
  const [selections, setSelections] = useState<Record<string, string>>({});
  const [choices, setChoices] = useState<Partial<Record<Role, ReviewChoice>>>({});
  const [message, setMessage] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [listLoading, setListLoading] = useState(false);
  const [listError, setListError] = useState("");
  const [documentError, setDocumentError] = useState("");
  const [busy, setBusy] = useState(false);
  const [reloadNeeded, setReloadNeeded] = useState(false);
  const [scope, setScope] = useState("");
  const [newComparison, setNewComparison] = useState(false);
  const [name, setName] = useState("");
  const openedId = useRef<string | null>(null);
  const generation = useRef(0);
  const listGeneration = useRef(0);
  const documentGeneration = useRef(0);
  const mutation = useRef(false);
  const createIntent = useRef<{
    name: string;
    key: string;
  } | null>(null);

  const loadDocuments = useCallback(async () => {
    const owner = ++documentGeneration.current;
    setDocumentError("");
    try {
      const result = await invoiceService.loadDocuments();
      if (owner === documentGeneration.current)
        setDocuments(result.value);
    }
    catch (failure) {
      if (owner === documentGeneration.current)
        setDocumentError(failure instanceof Error ? failure.message : "Could not load documents.");
    }
  }, []);

  const loadCases = useCallback(async (offset = 0) => {
    const owner = ++listGeneration.current;
    setListLoading(true);
    setListError("");
    try {
      const result = await invoiceService.loadCases(offset);
      if (owner !== listGeneration.current)
        return;
      setCases(previous => offset === 0 ? result.value.cases : [...previous, ...result.value.cases.filter(next => !previous.some(old => old.id === next.id))]);
      setNextOffset(result.value.nextOffset);
    }
    catch (failure) {
      if (owner === listGeneration.current)
        setListError(failure instanceof Error ? failure.message : "Could not load invoice checks.");
    }
    finally {
      if (owner === listGeneration.current)
        setListLoading(false);
    }
  }, []);

  const openCase = useCallback(async (id: string | null) => {
    openedId.current = id;
    const owner = ++generation.current;
    mutation.current = false;
    setItem(null);
    setEtag(null);
    setSelections({});
    setChoices({});
    setError("");
    setMessage("");
    setBusy(false);
    setReloadNeeded(false);
    setScope("");
    setNewComparison(false);
    setLoading(id !== null);
    if (id === null)
      return;
    if (!isWorkspaceId(id)) {
      setError("This invoice check link is invalid.");
      setLoading(false);
      return;
    }
    try {
      const result = await invoiceService.loadCase(id);
      if (owner !== generation.current)
        return;
      if (result.value.workflowKind !== "InvoicePurchaseOrder")
        throw new Error("This is an existing comparison case. Open it from Compare documents.");
      setItem(result.value);
      setEtag(result.etag);
      setSelections(savedSelections(result.value));
    }
    catch (failure) {
      if (owner === generation.current)
        setError(failure instanceof Error ? failure.message : "Could not open invoice check.");
    }
    finally {
      if (owner === generation.current)
        setLoading(false);
    }
  }, []);

  useEffect(() => {
    let active = true;
    const caseRequests = generation, listRequests = listGeneration, documentRequests = documentGeneration;
    const restore = () => {
      const id = selectionFromUrl("case");
      if (id !== openedId.current || generation.current === 0) void openCase(id);
      void loadDocuments();
      void loadCases();
    };
    void Promise.resolve().then(() => {
      if (active)
        restore();
    });
    window.addEventListener("popstate", restore);
    return () => {
      active = false;
      caseRequests.current++;
      listRequests.current++;
      documentRequests.current++;
      window.removeEventListener("popstate", restore);
    };
  }, [openCase, loadCases, loadDocuments]);

  function navigate(id: string | null) {
    writeSelection("case", id);
    void openCase(id);
    void loadCases();
    void loadDocuments();
  }

  function start() {
    if (mutation.current)
      return null;
    mutation.current = true;
    setBusy(true);
    setMessage("");
    return generation.current;
  }

  function finish(owner: number) {
    if (owner === generation.current) {
      mutation.current = false;
      setBusy(false);
    }
  }

  function apply(next: Case, token: string | null) {
    setItem(next);
    setEtag(token);
  }
  // Keep recovery ownership with React state; the service never retries a write.

  function mutationOptions(owner: number) {
    return {
      etag,
      onUncertain: () => {
        if (owner === generation.current) {
          setReloadNeeded(true);
          setEtag(null);
          setChoices({});
        }
      },
    };
  }

  async function create(event: FormEvent, automaticName?: string) {
    event.preventDefault();
    if (!canReview || !(automaticName ?? name).trim())
      return;
    const owner = start();
    if (owner === null)
      return;
    const title = (automaticName ?? name).trim();
    const storageKey = `invoice-check-creation:${identity}`;
    try {
      if (!createIntent.current) {
        try {
          const saved = JSON.parse(sessionStorage.getItem(storageKey) ?? "null");
          if (typeof saved?.name === "string" && isWorkspaceId(saved.key))
            createIntent.current = saved;
        }
        catch {
        }
      }
      if (createIntent.current?.name !== title)
        createIntent.current = { name: title, key: crypto.randomUUID() };
      try {
        sessionStorage.setItem(storageKey, JSON.stringify(createIntent.current));
      }
      catch {
      }
      const result = await invoiceService.createCase(title, createIntent.current.key);
      if (owner !== generation.current)
        return;
      createIntent.current = null;
      try {
        sessionStorage.removeItem(storageKey);
      }
      catch {
      }
      apply(result.value, result.etag);
      setSelections(savedSelections(result.value));
      setName("");
      openedId.current = result.value.id;
      writeSelection("case", result.value.id);
      setMessage("Invoice check saved. Select the invoice and purchase order next.");
      void loadCases();
      return result.value;
    }
    catch (failure) {
      if (owner === generation.current)
        setMessage(`${failure instanceof Error ? failure.message : "Could not create invoice check."} Retry the same name to recover this creation.`);
    }
    finally {
      finish(owner);
    }
  }

  async function save(role: Role, selectedId?: string) {
    if (!canReview || !item || item.status === "Closed" || !etag || reloadNeeded)
      return;
    const documentId = selectedId ?? (selections[role] || null);
    const issue = documentRoleIssue(documents.find(doc => doc.id === documentId)?.documentType, role);
    if (issue) { setMessage(issue); return; }
    const owner = start();
    if (owner === null)
      return;
    try {
      const result = await invoiceService.saveDraft(item.id, role, documentId, crypto.randomUUID(), mutationOptions(owner));
      if (owner !== generation.current)
        return;
      apply(result.value, result.etag);
      setChoices(current => ({ ...current, [role]: undefined }));
      setSelections(current => ({ ...current, [role]: documentId ?? "" }));
      setMessage(`${labels[role]} selection saved. Saved comparisons keep their original sources.`);
      return true;
    }
    catch (failure) {
      if (owner === generation.current)
        setMessage(failure instanceof Error ? failure.message : "Could not save selection.");
    }
    finally {
      finish(owner);
    }
  }

  async function reload() {
    if (!item)
      return;
    const owner = start();
    if (owner === null)
      return;
    try {
      const result = await invoiceService.loadCase(item.id);
      if (owner !== generation.current)
        return;
      apply(result.value, result.etag);
      setReloadNeeded(false);
      setChoices({});
      setNewComparison(false);
      setScope("");
      setMessage("Saved check reloaded. Your unsaved selections remain below; compare them with the saved documents before saving.");
      void loadDocuments();
    }
    catch (failure) {
      if (owner === generation.current)
        setMessage(failure instanceof Error ? failure.message : "Could not reload saved check.");
    }
    finally {
      finish(owner);
    }
  }

  async function chooseRevision(role: Role, documentId: string) {
    if (!canReview || !item || !etag || item.status === "Closed" || reloadNeeded)
      return;
    const owner = start();
    if (owner === null)
      return;
    setChoices(current => ({ ...current, [role]: undefined }));
    try {
      const result = await invoiceService.loadReview(documentId);
      if (owner !== generation.current)
        return;
      const review = invoiceService.requireCompletedReview(result.value, documentId, role);
      setChoices(current => ({ ...current, [role]: { documentId, reviewSessionId: review.id, revisionNumber: review.revisionNumber } }));
    }
    catch (failure) {
      if (owner === generation.current)
        setMessage(failure instanceof Error ? failure.message : "Could not load completed review.");
    }
    finally {
      finish(owner);
    }
  }

  async function pin(role: Role, inspected?: ReviewChoice, showResults = false) {
    const choice = inspected ?? choices[role];
    if (!canReview || !item || !choice || !etag || item.status === "Closed" || reloadNeeded || item.draftDocuments?.find(source => source.role === role)?.documentId !== choice.documentId)
      return;
    const issue = documentRoleIssue(documents.find(doc => doc.id === choice.documentId)?.documentType, role);
    if (issue) { setMessage(issue); return; }
    const owner = start();
    if (owner === null)
      return;
    try {
      const result = await invoiceService.pinReview(item.id, role, choice, mutationOptions(owner));
      if (owner !== generation.current)
        return;
      apply(result.value, result.etag);
      setChoices(current => ({ ...current, [role]: undefined }));
      setMessage(inspected ? `${labels[role]} checked details saved for comparison.` : `${labels[role]} reviewed revision saved for comparison.`);
      if (showResults) {
        if (!isPairReady(result.value)) throw new Error("Check both documents before showing results.");
        // The second write uses the first write's ETag and the exact inspected review IDs.
        // A lost response stops this sequence; it is never automatically replayed.
        const evaluated = await invoiceService.compare(item.id, "", { ...mutationOptions(owner), etag: result.etag }, true);
        if (owner !== generation.current) return;
        apply(evaluated.value, evaluated.etag);
        setMessage("Results saved. Missing checks remain visible.");
      }
      return true;
    }
    catch (failure) {
      if (owner === generation.current)
        setMessage(failure instanceof Error ? failure.message : "Could not use reviewed revision.");
    }
    finally {
      finish(owner);
    }
  }

  const pairReady = isPairReady(item);
  const selectionsDirty = hasUnsavedSelections(item, selections);

  async function compare(event: FormEvent, factual = false) {
    event.preventDefault();
    if (!canReview || !item || item.status === "Closed" || !etag || reloadNeeded || !pairReady || selectionsDirty || (!factual && (!scope || (item.runs.length > 0 && !newComparison))))
      return;
    const owner = start();
    if (owner === null)
      return;
    try {
      const result = await invoiceService.compare(item.id, scope, mutationOptions(owner), factual);
      if (owner !== generation.current)
        return;
      apply(result.value, result.etag);
      setNewComparison(false);
      setScope("");
      setMessage("Comparison saved. Inspect its differences and unavailable checks.");
      return true;
    }
    catch (failure) {
      if (owner === generation.current)
        setMessage(failure instanceof Error ? failure.message : "Could not compare reviewed pair.");
    }
    finally {
      finish(owner);
    }
  }

  const mutable = canReview && item?.status !== "Closed";
  const disabled = busy || !etag || reloadNeeded;

  function selectDocument(role: Role, documentId: string) {
    setSelections(current => ({ ...current, [role]: documentId }));
    setChoices(current => ({ ...current, [role]: undefined }));
  }

  function discardSelections() {
    if (item) setSelections(savedSelections(item));
    setChoices({});
  }

  function cancelReviewChoice(role: Role) {
    setChoices(current => ({ ...current, [role]: undefined }));
  }

  function startComparison() {
    setNewComparison(true);
    setScope("");
  }

  function cancelComparison() {
    setNewComparison(false);
  }

  // Inline result decisions change the case token without changing its document pins.
  const acceptResultSave = useCallback((id: string, status: string, token: string | null, previousToken: string | null) => {
    if (openedId.current !== id) return;
    if (!etag || previousToken !== etag) {
      setEtag(null);
      setReloadNeeded(true);
      setMessage("The saved documents changed while results were open. Check saved progress and inspect both documents again.");
      return;
    }
    setItem(current => current?.id === id ? { ...current, status } : current);
    setEtag(token);
  }, [etag]);

  function retryOpen() {
    void openCase(selectionFromUrl("case"));
  }
  return {
    item, documents, cases, nextOffset, selections, choices, message, error,
    loading, listLoading, listError, documentError, busy, reloadNeeded, scope,
    newComparison, name, pairReady, selectionsDirty, mutable, disabled,
    loadDocuments, loadCases, navigate, create, save, reload, chooseRevision,
    pin, compare, setName, setScope, selectDocument, cancelReviewChoice,
    startComparison, cancelComparison, retryOpen, discardSelections, acceptResultSave,
  };
}

export type InvoiceWorkflowState = ReturnType<typeof useInvoiceWorkflow>;
