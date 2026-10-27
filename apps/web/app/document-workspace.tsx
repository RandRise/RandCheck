"use client";
import { GuidedReview } from "./review/guided-review";
import { ReasonInput } from "./review/reason-input";
import { additionReasons, changeOrClearReasons, clearAddedReasons } from "./review/reason-options";
import { guidedReviewFromUrl } from "./review/guided-review.selectors";
import { cueLabels, fieldIdentity, fieldValue, labels } from "./review/review-fields";
import type { ReviewField, ReviewWorkspace } from "./review/review.types";
import { BidiText } from "./bidi-text";
import { InvoiceReviewContext } from "./invoices/review-context";
import Link from "next/link";

import { API, apiFetch } from "./security/api-fetch";
import { can, useEnterprise } from "./security/enterprise-provider";
import { FormEvent, useCallback, useEffect, useRef, useState } from "react";
import { ApiRequestError, readReviewResponse, requireSuccess, sendReviewMutation } from "./review-request.mjs";
import { SourceDownload } from "./source-download";
import { LibraryReview } from "./documents/library-review";
import { LibraryList } from "./documents/library-list";
import { isQuotationDocument } from "./document-types";
import { isWorkspaceId, selectionFromUrl, writeSelection } from "./workspace-location";
import "./review-experience.css";
import { reviewFieldsForDocument, reviewStateLabels } from "./document-fields";
import { cancelQueuedProcessing, clearProcessingIntent, needsSeparateProcessingIntent, prepareReprocessIntent,
  prepareUploadIntent, processingIntentStorageKey, queueReprocessing, uploadFingerprint, uploadToQueue,
  type ProcessingIntent, type ProcessingStatus } from "./document-processing";

type Field = { name: string; value: string; confidence: number; page: number };
type Document = {
  id: string; originalFilename: string; sizeBytes: number; status: "Processing" | "Ready" | "Failed" | "Cancelled";
  documentType?: string; language?: string; extractor?: string; processingMs?: number;
  createdAt: string; fields?: Field[]; errorMessage?: string; reviewState?: string | null;
  selectedExtractionRunId?: string; reviewExtractionRunId?: string; latestCompletedReviewId?: string;
  reviewUsesOlderExtraction?: boolean; latestExtractionStatus?: string | null;
};
const emptyAddition = { fieldKey: "", position: "1", value: "", reason: "" };

export type DocumentWorkspaceState = { review: ReviewWorkspace | null; dirty: boolean; busy: boolean; unavailable: boolean };
type Props = { library?: boolean; readOnly?: boolean; documentId?: string; focusField?: { key: string; occurrence: number; extractionRunId?: string }; embedded?: boolean; onState?: (state: DocumentWorkspaceState) => void };

export default function DocumentWorkspace({ documentId, focusField, embedded = false, readOnly = false, library = false, onState }: Props = {}) {
  const { session } = useEnterprise();
  // The library can recover processing, but review decisions belong to the workflow.
  const canReview = !library && !readOnly && can(session, "Review");
  const canOperate = !readOnly && can(session, "Operate");
  const [documents, setDocuments] = useState<Document[]>([]);
  const [selected, setSelected] = useState<Document | null>(null);
  const [review, setReview] = useState<ReviewWorkspace | null>(null);
  const [etag, setEtag] = useState<string | null>(null);
  const [drafts, setDrafts] = useState<Record<string, string>>({});
  const [reasons, setReasons] = useState<Record<string, string>>({});
  const [busy, setBusy] = useState(false);
  const [reviewBusy, setReviewBusy] = useState<string | null>(null);
  const [message, setMessage] = useState("");
  const [processing, setProcessing] = useState<ProcessingStatus | null>(null);
  const [historyDownload, setHistoryDownload] = useState<{ documentId: string; url: string; filename: string } | null>(null);
  const [documentSearch, setDocumentSearch] = useState("");
  const [libraryRefreshVersion, setLibraryRefreshVersion] = useState(0);
  const [documentOpening, setDocumentOpening] = useState(false);
  const [guided, setGuided] = useState(false);
  const [fieldFilter, setFieldFilter] = useState("all");
  const [reviewLoadFailed, setReviewLoadFailed] = useState(false);
  const [addition, setAddition] = useState(emptyAddition);
  const [additionVersion, setAdditionVersion] = useState(0);
  const [reasonVersions, setReasonVersions] = useState<Record<string, number>>({});

  // Explicit resets also close an empty Other editor, whose text has not changed.
  const resetAddition = useCallback(() => {
    setAddition(emptyAddition);
    setAdditionVersion(version => version + 1);
  }, []);

  const resetReason = useCallback((key: string) => {
    setReasons(current => ({ ...current, [key]: "" }));
    setReasonVersions(current => ({ ...current, [key]: (current[key] ?? 0) + 1 }));
  }, []);

  const [revising, setRevising] = useState<Record<string, boolean>>({});
  const [uploadNeedsNewIntent, setUploadNeedsNewIntent] = useState(false);
  const [reprocessNeedsNewIntent, setReprocessNeedsNewIntent] = useState(false);
  const operation = useRef<symbol | null>(null);
  const uploadIntent = useRef<ProcessingIntent | null>(null);
  const reprocessIntent = useRef<ProcessingIntent | null>(null);
  const reviewBaseline = useRef<ReviewWorkspace | null>(null);
  const fieldElements = useRef(new Map<string, HTMLElement>());
  const nextFieldButton = useRef<HTMLButtonElement>(null);
  const lastFocusedField = useRef<string | null>(null);
  // A selection generation also distinguishes A -> B -> A from the first visit to A.
  const selection = useRef({ id: null as string | null, generation: 0 });
  const reviewRequest = useRef(0);
  const processingRequest = useRef(0);
  const listRequest = useRef(0);
  const reviewMutation = useRef<number | null>(null);
  const mounted = useRef(true);
  const current = useCallback((generation: number) => selection.current.generation === generation, []);

  const selectDocument = useCallback((document: Document | null) => {
    const generation = selection.current.generation + 1;
    selection.current = { id: document?.id ?? null, generation };
    reviewRequest.current++; processingRequest.current++; reviewMutation.current = null;
    setGuided(!focusField && (embedded || guidedReviewFromUrl(document?.id)));
    setSelected(document); setReview(null); setEtag(null); setProcessing(null);
    setDrafts({}); setReasons({}); setReviewBusy(null); setMessage(""); setHistoryDownload(null);
    setDocumentOpening(false); setReprocessNeedsNewIntent(false);
    reviewBaseline.current = null; lastFocusedField.current = null; resetAddition(); setRevising({}); setFieldFilter("all"); setReviewLoadFailed(false);
    return generation;
  }, [resetAddition, embedded, focusField]);

  useEffect(() => () => { if (historyDownload) URL.revokeObjectURL(historyDownload.url); }, [historyDownload]);

  const applyReview = useCallback((workspace: ReviewWorkspace, nextEtag: string | null, savedKey?: string) => {
    if (workspace.documentId !== selection.current.id) return;
    const previous = reviewBaseline.current;
    const sameContext = previous?.documentId === workspace.documentId &&
      previous?.session?.id === workspace.session?.id && previous?.extractionRunId === workspace.extractionRunId;
    const retained = new Map(workspace.fields.flatMap(field => {
      const key = fieldIdentity(field);
      const prior = previous?.fields.find(item => fieldIdentity(item) === key);
      return sameContext && key !== savedKey && prior && prior.machine?.id === field.machine?.id ? [[key, prior] as const] : [];
    }));
    reviewBaseline.current = workspace;
    setReview(workspace);
    const state = workspace.session ? workspace.session.status === "Completed" ? "Reviewed" : "InProgress" : "NeedsReview";
    const update = (item: Document) => item.id === workspace.documentId ? { ...item, reviewState: state,
      selectedExtractionRunId: workspace.selectedExtractionRunId, reviewExtractionRunId: workspace.extractionRunId,
      latestCompletedReviewId: workspace.session?.status === "Completed" ? workspace.session.id : item.latestCompletedReviewId,
      reviewUsesOlderExtraction: workspace.extractionRunId !== workspace.selectedExtractionRunId } : item;
    setDocuments(items => items.map(update));
    setSelected(item => item ? update(item) : item);
    setEtag(nextEtag);
    setDrafts(current => Object.fromEntries(workspace.fields.map(field => {
      const key = fieldIdentity(field); const prior = retained.get(key);
      return [key, prior && current[key] !== undefined && current[key] !== fieldValue(prior) ? current[key] : fieldValue(field)];
    })));
    setReasons(current => Object.fromEntries([...retained.keys()].map(key => [key, current[key] ?? ""])));
    if (!sameContext) { resetAddition(); setRevising({}); }
  }, [resetAddition]);

  const applyMutation = useCallback((workspace: ReviewWorkspace, nextEtag: string | null, savedKey?: string) => {
    // A refresh issued while the write was pending may carry pre-commit values.
    reviewRequest.current++;
    applyReview(workspace, nextEtag, savedKey);
  }, [applyReview]);

  const loadReview = useCallback(async (document: Document, generation: number) => {
    if (!current(generation) || selection.current.id !== document.id) return;
    const request = ++reviewRequest.current;
    const ownsResponse = () => current(generation) && request === reviewRequest.current;
    setEtag(null); setReviewLoadFailed(false);
    if (document.status !== "Ready" && !document.selectedExtractionRunId && !document.fields?.length) return;
    try {
      const response = await apiFetch(`${API}/api/documents/${document.id}/review`);
      const result = await readReviewResponse<ReviewWorkspace>(response);
      if (ownsResponse()) applyReview(result.workspace, result.etag);
    } catch (error) {
      if (ownsResponse()) { setReviewLoadFailed(true); setMessage(error instanceof Error ? error.message : "Could not load review"); }
    }
  }, [applyReview, current]);

  const chooseDocument = useCallback(async (document: Document) => {
    writeSelection("document", document.id);
    if (selection.current.id === document.id) return;
    const generation = selectDocument(document);
    await loadReview(document, generation);
  }, [loadReview, selectDocument]);

  const restoreDocument = useCallback(async (available: Document[]) => {
    const id = documentId ?? selectionFromUrl("document");
    const generation = selectDocument(null);
    if (id !== null && !isWorkspaceId(id)) { setMessage("This document link is invalid."); return; }
    setDocumentOpening(true);
    try {
      let document = id === null ? library ? undefined : available[0] : available.find(value => value.id.toLowerCase() === id.toLowerCase());
      if (id !== null && !document) {
        const response = await apiFetch(`${API}/api/documents/${id}`);
        if (response.status === 404) throw new Error("Document not found or access unavailable.");
        await requireSuccess(response);
        document = await response.json() as Document;
      }
      if (!current(generation)) return;
      if (document) {
        const nextGeneration = selectDocument(document);
        await loadReview(document, nextGeneration);
      }
    } catch (error) { if (current(generation)) setMessage(error instanceof Error ? error.message : "Could not open this document."); }
    finally { if (current(generation)) setDocumentOpening(false); }
  }, [current, loadReview, selectDocument, documentId, library]);

  useEffect(() => {
    if (embedded) return;
    const restore = () => { void restoreDocument(documents); };
    window.addEventListener("popstate", restore);
    return () => window.removeEventListener("popstate", restore);
  }, [documents, restoreDocument, embedded]);

  const loadProcessing = useCallback(async (documentId: string, generation: number) => {
    if (!current(generation)) return null;
    const request = ++processingRequest.current;
    const response = await apiFetch(`${API}/api/documents/${documentId}/processing`);
    if (response.status === 404) return null;
    await requireSuccess(response);
    const state: ProcessingStatus = await response.json();
    if (!current(generation) || request !== processingRequest.current) return null;
    setProcessing(state);
    return state;
  }, [current]);

  const loadDocuments = useCallback(async (preferredId: string, generation: number, refreshListAfterSelectionChange = false) => {
    if (!mounted.current || (!current(generation) && !refreshListAfterSelectionChange)) return null;
    const request = ++listRequest.current;
    if (library) setLibraryRefreshVersion(value => value + 1);
    const response = await apiFetch(library ? `${API}/api/documents/${preferredId}` : `${API}/api/documents/work-list`);
    if (!response.ok) throw new Error("API unavailable");
    const data: Document[] = library ? [await response.json()] : await response.json();
    if (!mounted.current || request !== listRequest.current) return null;
    if (!current(generation) && !refreshListAfterSelectionChange) return null;
    setDocuments(data);
    if (!current(generation)) return null;
    let next = data.find(item => item.id === preferredId) ?? null;
    if (!next) {
      const detail = await apiFetch(`${API}/api/documents/${preferredId}`);
      if (!current(generation) || request !== listRequest.current) return null;
      if (detail.status === 404) { selectDocument(null); setMessage("Document not found or access unavailable."); return null; }
      await requireSuccess(detail);
      next = await detail.json() as Document;
      if (!current(generation) || request !== listRequest.current) return null;
    }
    const nextGeneration = next?.id === selection.current.id ? generation : selectDocument(next);
    setSelected(next);
    if (next) await loadReview(next, nextGeneration); else { setReview(null); setEtag(null); }
    return current(nextGeneration) ? nextGeneration : null;
  }, [loadReview, current, selectDocument, library]);

  useEffect(() => {
    mounted.current = true;
    const generation = selection.current.generation;
    const request = ++listRequest.current;
    (library ? Promise.resolve([]) : apiFetch(`${API}/api/documents/work-list`)
      .then(response => response.ok ? response.json() : Promise.reject(new Error("API unavailable"))))
      .then(async (data: Document[]) => {
        if (!mounted.current || request !== listRequest.current) return;
        setDocuments(data);
        if (current(generation)) await restoreDocument(data);
      })
      .catch(() => { if (current(generation)) setMessage("Documents could not be loaded. Refresh the page to try again."); });
    return () => { mounted.current = false; selection.current.generation++; };
  }, [current, restoreDocument, library]);

  useEffect(() => {
    const latestIsActive = ["Queued", "Processing"].includes(selected?.latestExtractionStatus ?? "");
    if (!selected || (selected.status !== "Processing" && !latestIsActive && !processing) ||
        !latestIsActive && processing && ["Ready", "Partial", "Failed", "Cancelled"].includes(processing.status)) return;
    let cancelled = false;
    let polling = false;
    const generation = selection.current.generation;
    const poll = async () => {
      if (polling || cancelled || !current(generation)) return;
      polling = true;
      try {
        const state = await loadProcessing(selected.id, generation);
        if (!cancelled && current(generation) && state && ["Ready", "Partial", "Failed", "Cancelled"].includes(state.status))
          await loadDocuments(selected.id, generation);
      } catch { if (!cancelled && current(generation)) setMessage("Processing status is temporarily unavailable. Retrying…"); }
      finally { polling = false; }
    };
    void poll();
    const timer = window.setInterval(poll, 1500);
    return () => { cancelled = true; window.clearInterval(timer); };
  }, [selected, processing, loadDocuments, loadProcessing, current]);

  const terminalProcessingDocumentId = selected?.id;
  const latestExtractionStatus = selected?.latestExtractionStatus;
  useEffect(() => {
    if (!terminalProcessingDocumentId || !["Failed", "Cancelled", "Partial"].includes(latestExtractionStatus ?? "")) return;
    const generation = selection.current.generation;
    void loadProcessing(terminalProcessingDocumentId, generation).catch(() => { if (current(generation)) setMessage("Processing status could not be loaded. Refresh to try again."); });
  }, [terminalProcessingDocumentId, latestExtractionStatus, current, loadProcessing]);

  async function upload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canOperate || operation.current !== null) return;
    const formElement = event.currentTarget;
    const input = formElement.elements.namedItem("file") as HTMLInputElement;
    if (!input.files?.[0]) return;
    const file = input.files[0];
    const ownership = Symbol(); operation.current = ownership;
    const generation = selection.current.generation;
    setBusy(true); setUploadNeedsNewIntent(false); setMessage("Uploading securely to the processing queue…");
    let acknowledged = false;
    try {
      const { fingerprint, durable } = await uploadFingerprint(file);
      if (!mounted.current || operation.current !== ownership) return;
      const storageKey = processingIntentStorageKey("upload", session.organizationId, session.actorId);
      uploadIntent.current = prepareUploadIntent(uploadIntent.current, file, fingerprint, durable, storageKey);
      const submittedKey = uploadIntent.current!.requestKey;
      const result = await uploadToQueue<Document>(file, submittedKey);
      acknowledged = true;
      if (uploadIntent.current?.requestKey === submittedKey) uploadIntent.current = null;
      clearProcessingIntent(storageKey, submittedKey);
      if (!mounted.current) return;
      if (input.files?.[0] === file) formElement.reset();
      if (current(generation)) setProcessing(null);
      const loaded = await loadDocuments(result.id, generation, true);
      if (loaded !== null && current(loaded)) { writeSelection("document", selection.current.id); setMessage("Document queued. You can leave it processing in the background."); }
    } catch (error) { if (current(generation)) {
      const needsNew = needsSeparateProcessingIntent(error);
      setUploadNeedsNewIntent(needsNew);
      setMessage(`${error instanceof Error ? error.message : "Upload failed"} ${acknowledged ? "The document was accepted. Refresh the page to reopen it." : needsNew ? "This request cannot be retried. You can explicitly start a separate upload below." : "Retry the same file to check this upload without creating a duplicate."}`);
    } }
    finally { if (operation.current === ownership) { operation.current = null; if (mounted.current) setBusy(false); } }
  }

  function startSeparateUpload() {
    if (!canOperate || operation.current !== null || !uploadNeedsNewIntent) return;
    const submittedKey = uploadIntent.current?.requestKey;
    const storageKey = processingIntentStorageKey("upload", session.organizationId, session.actorId);
    clearProcessingIntent(storageKey, submittedKey);
    if (uploadIntent.current?.requestKey === submittedKey) uploadIntent.current = null;
    setUploadNeedsNewIntent(false); setMessage("The next upload will create a separate document. Select Upload & extract when ready.");
  }

  async function cancelProcessing() {
    if (!canOperate || !processing || !selected || operation.current !== null) return;
    const ownership = Symbol(); operation.current = ownership;
    const generation = selection.current.generation;
    setBusy(true);
    try {
      await cancelQueuedProcessing(processing.jobId);
      if (!current(generation)) return;
      await loadProcessing(selected.id, generation); await loadDocuments(selected.id, generation);
      if (current(generation)) setMessage("Processing cancelled.");
    } catch (error) {
      if (current(generation)) {
        try { await loadProcessing(selected.id, generation); await loadDocuments(selected.id, generation); } catch { /* Keep the original failure visible. */ }
        if (current(generation)) setMessage(`${error instanceof Error ? error.message : "Could not cancel processing"} Check the processing status before trying again.`);
      }
    }
    finally { if (operation.current === ownership) { operation.current = null; if (mounted.current) setBusy(false); } }
  }

  async function reprocess() {
    if (!canOperate || !selected || operation.current !== null) return;
    const ownership = Symbol(); operation.current = ownership;
    const generation = selection.current.generation;
    processingRequest.current++;
    setBusy(true); setReprocessNeedsNewIntent(false);
    let acknowledged = false;
    try {
      const storageKey = processingIntentStorageKey("reprocess", session.organizationId, session.actorId, selected.id);
      reprocessIntent.current = prepareReprocessIntent(reprocessIntent.current, selected.id, storageKey);
      const submittedKey = reprocessIntent.current!.requestKey;
      const state = await queueReprocessing(selected.id, submittedKey);
      acknowledged = true;
      if (reprocessIntent.current?.requestKey === submittedKey) reprocessIntent.current = null;
      clearProcessingIntent(storageKey, submittedKey);
      if (!current(generation)) return;
      setProcessing(state);
      if (state.replayed) await loadProcessing(selected.id, generation);
      await loadDocuments(selected.id, generation);
      if (current(generation)) setMessage(state.replayed ? "Existing processing request found. Latest processing status is shown; no new run was started." : "Reprocessing queued. Existing reviewable output stays visible until replacement succeeds.");
    } catch (error) { if (current(generation)) {
      const needsNew = needsSeparateProcessingIntent(error);
      setReprocessNeedsNewIntent(needsNew);
      setMessage(`${error instanceof Error ? error.message : "Could not queue reprocessing"} ${acknowledged ? "The processing request was accepted. Refresh the page to check its status." : needsNew ? "This request cannot be retried. You can explicitly start a new processing request." : "Retry to check this processing request without starting another run."}`);
    } }
    finally { if (operation.current === ownership) { operation.current = null; if (mounted.current) setBusy(false); } }
  }

  function startNewProcessingRequest() {
    if (!canOperate || !selected || operation.current !== null || !reprocessNeedsNewIntent) return;
    const submittedKey = reprocessIntent.current?.fingerprint === selected.id ? reprocessIntent.current.requestKey : undefined;
    const storageKey = processingIntentStorageKey("reprocess", session.organizationId, session.actorId, selected.id);
    clearProcessingIntent(storageKey, submittedKey);
    if (reprocessIntent.current?.requestKey === submittedKey) reprocessIntent.current = null;
    setReprocessNeedsNewIntent(false); void reprocess();
  }

  async function startReview() {
    if (!canReview || !selected || reviewMutation.current !== null) return;
    const generation = selection.current.generation;
    reviewMutation.current = generation; reviewRequest.current++;
    setReviewBusy("start");
    try {
      const response = await apiFetch(`${API}/api/documents/${selected.id}/reviews`, { method: "POST" });
      const result = await readReviewResponse<ReviewWorkspace>(response);
      if (current(generation)) { applyMutation(result.workspace, result.etag); setMessage("Review started. Machine values remain unchanged."); }
    } catch (error) { if (current(generation)) setMessage(error instanceof Error ? error.message : "Could not start review"); }
    finally { if (current(generation)) { reviewMutation.current = null; setReviewBusy(null); } }
  }

  async function recordAction(field: ReviewField, action: "Confirmed" | "Corrected" | "Cleared", explicitReason?: string) {
    if (!canReview || !selected || !review?.session || !etag || reviewMutation.current !== null) return;
    if (field.latestAction && !revising[fieldIdentity(field)]) return;
    const generation = selection.current.generation;
    reviewMutation.current = generation; reviewRequest.current++;
    const key = fieldIdentity(field); setReviewBusy(key);
    const body: Record<string, unknown> = { action, machineFieldValueId: field.machine?.id };
    if (!field.machine) Object.assign(body, { fieldKey: field.fieldKey, occurrenceIndex: field.occurrenceIndex });
    if (action === "Corrected") Object.assign(body, { valueType: field.machine?.valueType ?? "String", value: drafts[key], reason: reasons[key] });
    const actionReason = explicitReason ?? reasons[key];
    if (action === "Cleared") body.reason = actionReason;
    let submitted = false;
    try {
      if (action !== "Confirmed" && !actionReason?.trim()) throw new Error("Add a reason before changing or clearing a value.");
      submitted = true;
      const result = await sendReviewMutation<ReviewWorkspace>({
        fetcher: apiFetch,
        url: `${API}/api/documents/${selected.id}/reviews/${review.session.id}/actions`,
        init: { method: "POST", headers: { "Content-Type": "application/json", "If-Match": etag }, body: JSON.stringify(body) },
        onStale: () => loadReview(selected, generation),
      });
      if (current(generation)) {
        applyMutation(result.workspace, result.etag, key);
        setRevising(current => ({ ...current, [key]: false }));
        if (fieldFilter === "pending") nextFieldButton.current?.focus();
        setMessage(`${labels[field.fieldKey] ?? field.fieldKey} saved as ${action.toLowerCase()}.`);
      }
    } catch (error) {
      if (current(generation)) {
        const uncertain = submitted && (!(error instanceof ApiRequestError) || error.status >= 500);
        if (uncertain) { setEtag(null); setReviewLoadFailed(true); }
        setMessage(`${error instanceof Error ? error.message : "Review action failed"}${uncertain ? " Reload the review to check whether this decision was saved before submitting again." : ""}`);
      }
    } finally { if (current(generation)) { reviewMutation.current = null; setReviewBusy(null); } }
  }

  async function addField(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canReview || !selected || !review?.session || !etag || reviewMutation.current !== null) return;
    const generation = selection.current.generation;
    reviewMutation.current = generation; reviewRequest.current++;
    setReviewBusy("add");
    let submitted = false;
    try {
      const fieldKey = addition.fieldKey.trim();
      const value = addition.value.trim();
      const reason = addition.reason.trim();
      const definition = availableReviewFields.find(([key]) => key === fieldKey);
      if (!definition) throw new Error("Choose a field from the list.");
      const valueType = definition[2];
      const position = Number(addition.position);
      if (!value || !reason) throw new Error("Value and reason are required.");
      if (!Number.isSafeInteger(position) || position < 1 || position > 500)
        throw new Error("Line number must be a whole number from 1 to 500.");
      submitted = true;
      const result = await sendReviewMutation<ReviewWorkspace>({
        fetcher: apiFetch,
        url: `${API}/api/documents/${selected.id}/reviews/${review.session.id}/actions`,
        init: {
          method: "POST", headers: { "Content-Type": "application/json", "If-Match": etag },
          body: JSON.stringify({ action: "Added", fieldKey, occurrenceIndex: fieldKey.startsWith("line_") ? position : 0, valueType, value, reason }),
        },
        onStale: () => loadReview(selected, generation),
      });
      if (current(generation)) { applyMutation(result.workspace, result.etag); resetAddition(); setMessage("Reviewer-added field saved."); }
    } catch (error) {
      if (current(generation)) {
        const uncertain = submitted && (!(error instanceof ApiRequestError) || error.status >= 500);
        if (uncertain) { setEtag(null); setReviewLoadFailed(true); }
        setMessage(`${error instanceof Error ? error.message : "Could not add field"}${uncertain ? " Reload the review to check whether this field was saved before submitting again." : ""}`);
      }
    }
    finally { if (current(generation)) { reviewMutation.current = null; setReviewBusy(null); } }
  }

  async function completeReview() {
    if (!canReview || !selected || !review?.session || !etag || reviewMutation.current !== null) return;
    if (hasUnsavedChanges || pendingCount > 0) { setMessage("Save or discard your drafts and check each extracted value before completing the review."); return; }
    const generation = selection.current.generation;
    reviewMutation.current = generation; reviewRequest.current++;
    setReviewBusy("complete");
    try {
      const result = await sendReviewMutation<ReviewWorkspace>({
        fetcher: apiFetch,
        url: `${API}/api/documents/${selected.id}/reviews/${review.session.id}/complete`,
        init: { method: "POST", headers: { "If-Match": etag } },
        onStale: () => loadReview(selected, generation),
      });
      if (current(generation)) { applyMutation(result.workspace, result.etag); setMessage("Review completed and locked."); }
    } catch (error) {
      if (current(generation)) {
        const uncertain = !(error instanceof ApiRequestError) || error.status >= 500;
        if (uncertain) { setEtag(null); setReviewLoadFailed(true); }
        setMessage(`${error instanceof Error ? error.message : "Could not complete review"}${uncertain ? " Reload the review to check whether completion was saved before submitting again." : ""}`);
      }
    }
    finally { if (current(generation)) { reviewMutation.current = null; setReviewBusy(null); } }
  }

  async function exportHistory() {
    if (!selected) return;
    const generation = selection.current.generation;
    try {
      const response = await apiFetch(`${API}/api/documents/${selected.id}/review-history`);
      await requireSuccess(response);
      const blob = await response.blob();
      if (!current(generation)) return;
      const url = URL.createObjectURL(blob);
      const filename = `review-history-${selected.id}.json`;
      setHistoryDownload({ documentId: selected.id, url, filename });
      const link = window.document.createElement("a"); link.href = url; link.download = filename;
      window.document.body.appendChild(link);
      try { link.click(); setMessage("History is ready. If the download did not start, use Save history JSON."); }
      finally { link.remove(); }
    } catch { if (current(generation)) setMessage("Review history could not be downloaded."); }
  }

  const active = canReview && review?.session?.status === "InProgress";
  const listedDocuments = documents.filter(doc =>
    `${doc.originalFilename} ${doc.id}`.toLocaleLowerCase().includes(documentSearch.toLocaleLowerCase()));

  const pendingCount = review?.fields.filter(field => field.machine && field.decisionStatus === "Unreviewed").length ?? 0;
  const extractedCount = review?.fields.filter(field => field.machine).length ?? 0;
  const isDirty = (field: ReviewField) => (drafts[fieldIdentity(field)] ?? fieldValue(field)) !== fieldValue(field) || !!reasons[fieldIdentity(field)];
  const additionDirty = Object.entries(emptyAddition).some(([key, value]) => addition[key as keyof typeof addition] !== value);
  const hasUnsavedChanges = additionDirty || !!review?.fields.some(isDirty);
  const needsReview = (field: ReviewField) => !!field.machine && field.decisionStatus === "Unreviewed" || isDirty(field);
  const visibleFields = review?.fields.filter(field => fieldFilter === "all" || needsReview(field)) ?? [];
  const editingDisabled = reviewBusy !== null || !etag;
  const showGuided = guided && active;
  const availableReviewFields = reviewFieldsForDocument(selected?.documentType, review?.fields.some(field => field.fieldKey === "quotation_number"));

  useEffect(() => {
    onState?.({ review, dirty: hasUnsavedChanges, busy: busy || reviewBusy !== null,
      unavailable: reviewLoadFailed || !etag });
  }, [review, hasUnsavedChanges, busy, reviewBusy, reviewLoadFailed, etag, onState]);

  useEffect(() => {
    if (!focusField || !review) return;
    if (!focusField.extractionRunId || focusField.extractionRunId !== review.extractionRunId) return;
    const timer = window.setTimeout(() => {
      const field = review.fields.find(value => value.fieldKey === focusField.key && value.occurrenceIndex === focusField.occurrence);
      if (field) fieldElements.current.get(fieldIdentity(field))?.focus();
    }, 0);
    return () => window.clearTimeout(timer);
  }, [focusField, review]);

  function focusNextField() {
    const pending = review?.fields.filter(needsReview) ?? [];
    const focused = pending.findIndex(field => fieldIdentity(field) === lastFocusedField.current);
    const next = pending[(focused + 1) % pending.length];
    if (next) fieldElements.current.get(fieldIdentity(next))?.focus();
  }

  const Container = embedded ? "section" : "main";
  return <Container className={library ? "document-library" : embedded ? "embedded-document" : undefined}>
    {!embedded && <header className="page-heading"><h1>{library ? "Document library" : "Documents"}</h1>
      <p>{library ? "Find a saved file, read its values or download its original. To add files or check an invoice, use Home in the side menu." : "Choose a document to check its details against the source. Arabic and mixed-language extraction is experimental and requires review."}</p></header>}
    {!embedded && !library && <InvoiceReviewContext documentId={selected?.id}/>}
    {!embedded && !library && (canOperate ? <section className="upload-card">
      <div><h2>Add an invoice or purchase order</h2><p>For a new check, add these 2 documents. Earlier quotation documents stay in the document list. The file type is checked here; the document contents are identified after upload.</p><p>PDF, PNG, JPEG, or TXT · maximum 10 MB</p></div>
      <form onSubmit={upload}><fieldset disabled={!canOperate || busy} style={{border:0,padding:0,display:"contents"}}><input name="file" type="file" accept=".pdf,.png,.jpg,.jpeg,.txt" required/><button disabled={!canOperate || busy}>{busy ? "Processing…" : "Upload & extract"}</button></fieldset></form>
      {canOperate && uploadNeedsNewIntent && <div><p>A separate upload creates a new document. Your earlier request will keep its original identity.</p><button className="secondary" disabled={busy} onClick={startSeparateUpload}>Start a separate upload</button></div>}
    </section> : <p className="workspace-next-action">{canReview ? "Choose a document needing review. An operator adds and processes documents." : "Choose a document to view its saved values and evidence. A reviewer checks and completes reviews."}</p>)}
    {focusField && review && (!focusField.extractionRunId || focusField.extractionRunId !== review.extractionRunId) &&
      <p role="status">This result uses an earlier or unknown extraction. Check the saved result evidence; the current item rows may be different.</p>}
    {message && <p className="message review-feedback" role="status">{message}</p>}
    <div className="workspace">
      {!embedded && (library ? <LibraryList key={`${session.organizationId}:${session.actorId}:${session.role}`}
        selectedId={selected?.id} onSelect={chooseDocument} refreshVersion={libraryRefreshVersion} /> : <aside><h2>{"Document list"} <b>{documents.length}</b></h2>
        <label className="document-search">Find a document<input type="search" dir="auto" value={documentSearch} onChange={event => setDocumentSearch(event.target.value)}/></label>
        <div className="document-list">{documents.length === 0 && <p className="empty">No documents yet. {library ? "Files added through Home will appear here." : canOperate ? "Add a document above to get started." : "An operator can add documents for you to view here."}</p>}
          {documents.length > 0 && listedDocuments.length === 0 && <div><p>No documents match this search.</p></div>}
          {listedDocuments.map(doc => <button key={doc.id} aria-pressed={selected?.id === doc.id} className={selected?.id === doc.id ? "document active" : "document"} onClick={() => chooseDocument(doc)}>
            {!library && <span className="file-icon">{doc.documentType?.slice(0, 3).toUpperCase() || "DOC"}</span>}<span><strong><bdi>{doc.originalFilename}</bdi></strong><small>{new Date(doc.createdAt).toLocaleString("en-GB")} · {(doc.sizeBytes / 1024).toFixed(1)} KB</small>{doc.latestExtractionStatus && doc.latestExtractionStatus !== "Ready" && <small>Extraction: {doc.latestExtractionStatus}</small>}{doc.reviewUsesOlderExtraction && <small>Review uses an earlier extraction</small>}</span><i className={`status ${doc.reviewState === "Reviewed" ? "ready" : doc.status.toLowerCase()}`}>{reviewStateLabels[doc.reviewState ?? ""] ?? doc.status}</i>
          </button>)}</div>
      </aside>)}
      <section className="review">
        {!selected ? <div className="placeholder"><h2>{documentOpening ? "Opening document…" : "Choose a document"}</h2><p>Select an item from the list to see its details and source evidence.</p>
          {library && <ul className="library-reading-guide"><li><strong>Read saved values</strong><span>See what was found and what has been checked.</span></li><li><strong>Check the source</strong><span>Read the evidence or download the original file.</span></li><li><strong>Keep the history</strong><span>Earlier reviews and decisions stay available.</span></li></ul>}
        </div> : <>
          <div className="review-head"><div><h2 dir="auto">{selected.originalFilename}</h2><p>{selected.documentType ?? "Unknown type"} · {selected.language ?? "—"}</p></div></div>
          {library && isQuotationDocument(selected.documentType) && <p>This earlier quotation is open for reading. Use Show earlier quotations when searching for older files.</p>}
          {session.tenantId && can(session, "Read") && <SourceDownload key={selected.id} documentId={selected.id}/>}
          {(selected.status === "Processing" || processing) && <div className={`processing-panel state-${(processing?.status ?? "Queued").toLowerCase()}`} aria-live="polite">
            <div><strong>{processing?.status ?? "Queued"}</strong><small>{processing?.status === "Processing" ? processing.attemptCount === undefined ? "Extraction is running." : `Extraction attempt ${processing.attemptCount} is running.` : processing?.status === "Ready" ? "Extraction completed." : processing?.status === "Partial" ? "Extraction completed with warnings." : processing?.status === "Failed" ? "Extraction failed." : processing?.status === "Cancelled" ? "Processing was cancelled." : processing?.retryAt ? `Retry scheduled for ${new Date(processing.retryAt).toLocaleTimeString()}.` : "Waiting for a durable worker claim."}</small></div>
            {canOperate && processing && ["Queued", "Processing"].includes(processing.status) && <button className="secondary" disabled={busy} onClick={cancelProcessing}>Cancel</button>}
          </div>}
          {processing?.status === "Partial" && <p className="warning">Extraction completed with warnings. Review the available fields and evidence carefully.</p>}
          {(selected.status === "Failed" || selected.status === "Cancelled" || processing?.status === "Failed" || processing?.status === "Cancelled") && <div className="terminal-state"><p>{processing?.errorMessage ?? selected.errorMessage ?? (processing?.status === "Cancelled" ? "Processing was cancelled." : "Processing did not complete.")}</p>{canOperate ? <button disabled={busy} onClick={reprocess}>Try processing again</button> : <p>An operator can retry processing.</p>}</div>}
          <details className="processing-details"><summary>Processing details</summary><p>Extractor: {selected.extractor ?? "pending"} · Duration: {selected.processingMs ?? 0} ms</p>
            {canOperate && selected.status === "Ready" && <div className="reprocess-row"><p>Processing again creates a new extraction. Saved reviews and evidence keep their original versions.</p><button className="secondary" disabled={busy || !!processing && ["Queued", "Processing"].includes(processing.status)} onClick={reprocess}>Reprocess document</button></div>}
          </details>
          {canOperate && reprocessNeedsNewIntent && <div><p>A new processing request uses the current extractor settings and creates a separate run.</p><button className="secondary" disabled={busy} onClick={startNewProcessingRequest}>Start a new processing request</button></div>}
          {selected.errorMessage && <p className="error">{selected.errorMessage}</p>}
          {!review && reviewLoadFailed && <button className="secondary" onClick={() => loadReview(selected, selection.current.generation)}>Retry review load</button>}
          {library && review && reviewLoadFailed && <p role="status">The latest saved values could not be loaded.
            <button className="secondary" onClick={() => loadReview(selected, selection.current.generation)}>Retry review load</button>
          </p>}
          {library && review && <LibraryReview key={`${selected.id}:${review.session?.id}`} review={review}
            historyDownload={historyDownload?.documentId === selected.id ? historyDownload : undefined}
            hasHistory={!!(review.session || selected.latestCompletedReviewId)} onExportHistory={() => void exportHistory()}/>}
          {!library && review && <>
            {reviewLoadFailed && reviewBusy === null && <div className="review-refresh"><p>Reload the latest review before saving. Your current drafts will stay available for this review.</p><button className="secondary" onClick={() => loadReview(selected, selection.current.generation)}>Retry review load</button></div>}
            <div className="review-toolbar">
              <div><strong>{review.session?.status === "Completed" ? "Review completed" : review.session ? "Review in progress" : "Review not started"}</strong><small>{extractedCount - pendingCount} of {extractedCount} extracted values checked</small></div>
              {canReview && !review.session && <button onClick={startReview} disabled={reviewBusy === "start"}>Start review</button>}
              {active && <button onClick={completeReview} disabled={editingDisabled || pendingCount > 0 || hasUnsavedChanges}>Complete review</button>}
              {review.session?.status === "Completed" && <span className="locked">Read only</span>}
              {review.session?.status === "Completed" && <Link className="history-link" href={`/questions?document=${selected.id}&review=${review.session.id}`}>Questions from this review</Link>}
              {(review.session || selected.latestCompletedReviewId) && <button className="history-link" onClick={exportHistory}>Export history</button>}
              {historyDownload?.documentId === selected.id && <a className="history-link" href={historyDownload.url} download={historyDownload.filename}>Save history JSON</a>}
            </div>
            {review.extractionRunId !== review.selectedExtractionRunId && <p className="warning">This active review uses an earlier extraction. Finish it before starting a review of the newer output.</p>}
            {selected.latestCompletedReviewId && selected.latestCompletedReviewId !== review.session?.id && <p>A previous completed review is preserved in review history.</p>}
            <div className="review-progress">
              <progress aria-label="Extracted values checked" value={extractedCount - pendingCount} max={extractedCount || 1}/>
              <p>{active ? reviewBusy ? "Saving… Please wait before making another change." : hasUnsavedChanges ? "Save or discard your unsaved changes before completing the review." : pendingCount > 0 ? "All changes saved. Check the source, then confirm, correct, or clear each remaining value." : "All changes saved. You can now complete the review to lock your decisions." : review.session?.status === "Completed" ? hasUnsavedChanges ? "This review was completed elsewhere. Your unsaved drafts below were not submitted; copy anything you need before discarding them." : "Your decisions are saved and locked. Review history is available above." : canReview ? "Start review to check each extracted value against its source." : "These extracted values are awaiting a reviewer. You can inspect their source evidence."}</p>
            </div>
            {!active && hasUnsavedChanges && <section className="draft-recovery" aria-label="Unsaved draft recovery"><h3>Unsaved drafts</h3>
              {review.fields.filter(isDirty).map(field => <div key={fieldIdentity(field)}><strong>{labels[field.fieldKey] ?? field.fieldKey}</strong><p>Draft value: <bdi>{drafts[fieldIdentity(field)]}</bdi></p><p>Draft reason: <bdi>{reasons[fieldIdentity(field)]}</bdi></p></div>)}
              {additionDirty && <div><strong>New field draft</strong>{Object.entries(addition).map(([key, value]) => <p key={key}>{key}: <bdi>{value}</bdi></p>)}</div>}
              <button className="secondary" onClick={() => { setDrafts(Object.fromEntries(review.fields.map(field => [fieldIdentity(field), fieldValue(field)]))); setReasons({}); resetAddition(); }}>Discard unsaved drafts</button>
            </section>}
            {active && <div className="review-navigation" aria-label="Review view">
              <button className="secondary" aria-pressed={showGuided} disabled={editingDisabled || additionDirty}
                onClick={() => setGuided(true)}>Check one value at a time</button>
              <button className="secondary" aria-pressed={!showGuided} disabled={editingDisabled}
                onClick={() => { setGuided(false); setFieldFilter("all"); }}>All fields</button>
              {additionDirty && <p>Save or discard your new field before switching to one value at a time.</p>}
            </div>}
            {showGuided ? <GuidedReview
              key={`${review.documentId}:${review.session?.id}:${review.extractionRunId}`}
              fields={review.fields}
              drafts={drafts}
              reasons={reasons}
              disabled={editingDisabled}
              onValue={(key, value) => setDrafts(current => ({ ...current, [key]: value }))}
              onReason={(key, value) => setReasons(current => ({ ...current, [key]: value }))}
              onDiscard={field => {
                const key = fieldIdentity(field);
                setDrafts(current => ({ ...current, [key]: fieldValue(field) }));
                resetReason(key);
              }}
              onAction={(field, action, reason) => void recordAction(field, action, reason)}
              onShowAll={() => { setGuided(false); setFieldFilter("all"); }}
            /> : <>
            <div className="review-navigation">
              <label>Show fields<select value={fieldFilter} onChange={event => setFieldFilter(event.target.value)}><option value="all">All fields</option><option value="pending">Needs review</option></select></label>
              <button ref={nextFieldButton} className="secondary" onClick={focusNextField} aria-disabled={!review.fields.some(needsReview)}>Next field needing review</button>
            </div>
            {visibleFields.length === 0 && <p>No fields need review. Choose All fields to see saved decisions.</p>}
            <div className="review-fields">{visibleFields.map(field => {
              const key = fieldIdentity(field); const isBusy = editingDisabled; const cues = field.cues ?? [];
              const dirty = isDirty(field); const valueDirty = (drafts[key] ?? fieldValue(field)) !== fieldValue(field);
              return <article tabIndex={-1} onFocus={() => { lastFocusedField.current = key; }} aria-label={labels[field.fieldKey] ?? field.fieldKey} ref={element => { if (element) fieldElements.current.set(key, element); else fieldElements.current.delete(key); }} className={`review-field decision-${field.decisionStatus.toLowerCase()} ${cues.length ? "needs-attention" : ""}`} key={key}>
                <div className="field-title"><span>{labels[field.fieldKey] ?? field.fieldKey}{field.fieldKey.startsWith("line_") ? ` · Line ${field.occurrenceIndex}` : field.occurrenceIndex > 0 ? ` · Position ${field.occurrenceIndex + 1}` : ""}</span><i>{field.decisionStatus}</i></div>
                {cues.length > 0 && <div className="review-cues" role="alert">{cues.map(cue => <span key={cue}>{cueLabels[cue] ?? cue}</span>)}</div>}
                <div className="review-field-context"><div>
                {field.machine ? <div className="machine-value"><small>AI output · {field.machine.confidence === null ? "confidence unavailable" : `${Math.round(field.machine.confidence * 100)}% confidence`}</small><p dir={field.fieldKey.endsWith("_ar") ? "rtl" : "auto"}>{field.machine.value}</p></div> : <div className="machine-value"><small>No machine value</small><p>{field.decisionStatus === "Missing" ? "Expected field was not extracted" : "Reviewer-added field"}</p></div>}
                <div className="effective-value"><small>{field.decisionStatus === "Unreviewed" || field.effective.source === "MachinePending" ? "Extracted value · not yet checked" : "Saved reviewed value"}</small><p dir="auto">{field.effective.isPresent ? field.effective.value : field.decisionStatus === "Missing" ? "Not extracted" : "Excluded"}</p>{field.latestAction?.reason && <em>Reason: <bdi><BidiText text={field.latestAction.reason}/></bdi></em>}</div>
                </div><div className="source-evidence"><h3>Source evidence</h3>{field.machine?.evidence.length ? field.machine.evidence.map((evidence, index) => <div key={index}><small>Page {evidence.page}</small>{evidence.quotedText ? <blockquote dir="auto"><BidiText text={evidence.quotedText}/></blockquote> : <p>No text excerpt is available. Check this page in the original document.</p>}</div>) : <p>No source evidence was returned for this value.</p>}</div></div>
                {active && dirty && <p className="draft-state">Unsaved changes <button className="secondary" disabled={isBusy} onClick={() => { setDrafts(current => ({ ...current, [key]: fieldValue(field) })); resetReason(key); if (fieldFilter === "pending") nextFieldButton.current?.focus(); }}>Discard draft</button></p>}
                {active && field.latestAction && !revising[key] && <button className="secondary" disabled={isBusy} onClick={() => setRevising(current => ({ ...current, [key]: true }))}>Revise decision</button>}
                {active && field.latestAction && revising[key] && <button className="secondary" disabled={isBusy} onClick={() => { setRevising(current => ({ ...current, [key]: false })); setDrafts(current => ({ ...current, [key]: fieldValue(field) })); resetReason(key); }}>Cancel revision</button>}
                {active && field.machine && (!field.latestAction || revising[key]) && <div className="field-controls">
                  <label>Reviewed value<input disabled={isBusy} dir="auto" value={drafts[key] ?? ""} onChange={event => setDrafts(current => ({ ...current, [key]: event.target.value }))}/></label>
                  <ReasonInput key={reasonVersions[key] ?? 0} label="Reason for change or clear" disabled={isBusy} value={reasons[key] ?? ""}
                    choices={changeOrClearReasons} onChange={value => setReasons(current => ({ ...current, [key]: value }))}/>
                  <div><button disabled={isBusy || valueDirty} onClick={() => recordAction(field, "Confirmed")}>Confirm AI value</button><button disabled={isBusy} onClick={() => recordAction(field, "Corrected")}>Save correction</button><button className="secondary" disabled={isBusy} onClick={() => recordAction(field, "Cleared")}>Clear</button></div>
                  <p className="field-action-help">{valueDirty ? "Use Save correction to keep your edited value. Add a reason for the change." : "Confirm keeps the original AI value. To change or clear it, add a reason. Clear excludes this value from reviewed output."}</p>
                </div>}
                {active && !field.machine && field.decisionStatus === "Added" && (!field.latestAction || revising[key]) && <div className="field-controls single">
                  <ReasonInput key={reasonVersions[key] ?? 0} label="Reason to clear" disabled={isBusy} value={reasons[key] ?? ""}
                    choices={clearAddedReasons} onChange={value => setReasons(current => ({ ...current, [key]: value }))}/>
                  <div><button className="secondary" disabled={isBusy} onClick={() => recordAction(field, "Cleared")}>Clear added field</button></div>
                </div>}
              </article>;
            })}</div>
            </>}
            {active && !showGuided && <form className="add-field" onSubmit={addField}>
              <div className="add-field-heading"><strong>Add a missing field</strong><small>Choose a named field, enter its value, and explain what you checked. Reviewer additions have no AI confidence or fabricated evidence.</small></div>
              <label>Field<select disabled={editingDisabled} value={addition.fieldKey} onChange={event => {
                setAddition({ ...addition, fieldKey: event.target.value, position: "1" });
              }} required><option value="">Choose a field</option>{availableReviewFields.map(([key, label]) => <option key={key} value={key} disabled={!key.startsWith("line_") && review.fields.some(field => field.fieldKey === key && field.occurrenceIndex === 0 && field.machine)}>{label}</option>)}</select></label>
              {addition.fieldKey.startsWith("line_") && <label>Line number<input disabled={editingDisabled} type="number" min="1" max="500" step="1" value={addition.position} onChange={event => setAddition({ ...addition, position: event.target.value })} required/></label>}
              <label>Value<input disabled={editingDisabled} dir="auto" name="value" placeholder="Value" value={addition.value} onChange={event => setAddition({ ...addition, value: event.target.value })} required/></label>
              <ReasonInput key={additionVersion} label="Reason" disabled={editingDisabled} name="reason" placeholder="Reason" value={addition.reason}
                choices={additionReasons} onChange={reason => setAddition({ ...addition, reason })} required/>
              <button disabled={editingDisabled}>Add field</button>
              {additionDirty && <div className="addition-draft"><span>Unsaved changes</span><button type="button" className="secondary" disabled={editingDisabled} onClick={() => { resetAddition(); }}>Discard new field</button></div>}
            </form>}
          </>}
        </>}
      </section>
    </div>
  </Container>;
}
