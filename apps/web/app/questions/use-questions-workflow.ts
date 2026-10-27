"use client";

import { request } from "./questions-api";
import { useReviewedSourceBrowser } from "./use-reviewed-source-browser";
import type { Source, PinnedSource } from "./questions-sources.types";
import { FormEvent, useEffect, useRef, useState } from "react";

type CaseSummary = { id: string; name: string; reference: string; documentCount: number };
type CaseList = { cases: CaseSummary[]; nextOffset: number | null };
type SavedQuestion = { id: string; question: string; createdAt: string; sourceCount: number; mode: string };
type Hit = { hitId: string; indexId: string; chunkId: string; documentId: string; filename: string;
  reviewSessionId: string; extractionRunId: string; rank: number; kind: string; content: string;
  reviewActionId: string | null; spans: { id: string; pageNumber: number; quote: string }[] };
type QuestionResult = { stage: string; answerGenerationEnabled: boolean; question: string; indexIds: string[];
  retrieval: { id: string; status: string; policyVersion: string; replayed: boolean; hits: Hit[]; failureCode: string | null };
  answer: { status: string; reasonCode: string; message: string } | null;
  reviewedAnswer?: { status: string; reasonCode: string | null; missingParts: string[];
    claims: { text: string; citations: { hitId: string; spanId: string | null }[] }[] } | null };
type Location = { retrievalRunId: string; hitId: string; indexId: string; documentId: string; filename: string;
  extractionRunId: string; reviewSessionId: string; kind: string; reviewedFact: string | null;
  review: { actionId: string; action: string; fieldKey: string; occurrence: number; reason: string | null } | null;
  page: { pageNumber: number; byteStart: number; byteEnd: number; before: string; quote: string; after: string } | null };

const guid = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
export function resultUrl(id: string) { return `/questions?result=${encodeURIComponent(id)}`; }
function rememberResult(id: string) {
  const url = resultUrl(id);
  if (`${window.location.pathname}${window.location.search}` !== url) window.history.pushState(null, "", url);
}


export function useQuestionsWorkflow(canPrepare: boolean) {
  const canRead = true;
  const [browseSources, setBrowseSources] = useState(false);
  const sourceBrowser = useReviewedSourceBrowser(browseSources);
  const { sources, nextOffset, sourceLoading, sourceError, retrySources } = sourceBrowser;
  const [chosen, setChosen] = useState("");
  const [linkedSource, setLinkedSource] = useState<Source | null>(null);
  const [cases, setCases] = useState<CaseSummary[]>([]);
  const [caseOffset, setCaseOffset] = useState<number | null>(null);
  const [caseListError, setCaseListError] = useState(false);
  const [caseLoading, setCaseLoading] = useState(true);
  const caseLoadingOwner = useRef(true);
  const [recent, setRecent] = useState<SavedQuestion[]>([]);
  const [recentError, setRecentError] = useState(false);
  const [caseAttempt, setCaseAttempt] = useState(0);
  const [recentAttempt, setRecentAttempt] = useState(0);
  const [caseId, setCaseId] = useState("");
  const [caseName, setCaseName] = useState("");
  const [caseContext, setCaseContext] = useState<{ id: string; name: string; version?: string } | null>(null);
  const [savedCorpus, setSavedCorpus] = useState<{ id: string; indexIds: string[] } | null>(null);
  const [pinned, setPinned] = useState<PinnedSource[]>([]);
  const [question, setQuestion] = useState("");
  const [mode, setMode] = useState("reviewed");
  const [retrievalMode, setRetrievalMode] = useState("lexical");
  const [result, setResult] = useState<QuestionResult | null>(null);
  const [location, setLocation] = useState<Location | null>(null);
  const [savedId, setSavedId] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState<string | null>(null);
  const [retryAvailable, setRetryAvailable] = useState(false);
  const epoch = useRef(0);
  const operation = useRef<number | null>(null);
  const mounted = useRef(false);
  const pending = useRef<{ fingerprint: string; requestKey: string } | null>(null);
  const abort = useRef<AbortController | null>(null);
  const evidenceHeading = useRef<HTMLHeadingElement>(null);
  const resultHeading = useRef<HTMLHeadingElement>(null);

  useEffect(() => {
    mounted.current = true;
    const requests = epoch;
    return () => { mounted.current = false; requests.current++; operation.current = null; abort.current?.abort(); };
  }, []);


  useEffect(() => {
    const controller = new AbortController();
    caseLoadingOwner.current = true;
    request<CaseList>("/api/reconciliation-cases", undefined, controller.signal)
      .then(data => { if (!controller.signal.aborted) { setCases(data.cases); setCaseOffset(data.nextOffset); setCaseListError(false); } })
      .catch(() => { if (!controller.signal.aborted) setCaseListError(true); })
      .finally(() => { if (!controller.signal.aborted) { caseLoadingOwner.current = false; setCaseLoading(false); } });
    return () => controller.abort();
  }, [caseAttempt]);

  useEffect(() => {
    const controller = new AbortController();
    request<{ questions: SavedQuestion[] }>("/api/cited-questions", undefined, controller.signal)
      .then(data => { if (!controller.signal.aborted) { setRecent(data.questions); setRecentError(false); } })
      .catch(() => { if (!controller.signal.aborted) setRecentError(true); });
    return () => controller.abort();
  }, [recentAttempt]);

  useEffect(() => {
    if (!canRead) return;
    const requests = epoch;
    const restore = async () => {
      operation.current = null;
      abort.current?.abort(); const controller = new AbortController(); abort.current = controller;
      const version = ++epoch.current; const current = () => mounted.current && version === epoch.current;
      operation.current = version;
      const query = new URLSearchParams(window.location.search);
      const saved = query.get("result"); const linkedCase = query.get("case");
      const document = query.get("document"); const review = query.get("review");
      setBrowseSources(!linkedCase);
      setResult(null); setLocation(null); setPinned([]); setSavedCorpus(null); setCaseContext(null);
      setLinkedSource(null); setChosen("");
      setCaseId(linkedCase ?? ""); setCaseName(""); setSavedId(saved ?? ""); setQuestion("");
      setMessage(""); setBusy(saved || linkedCase || document || review ? "restore" : null); pending.current = null; setRetryAvailable(false);
      try {
        if (saved) {
          if (!guid.test(saved)) throw new Error("This saved result link is invalid.");
          const data = await request<QuestionResult>(`/api/cited-questions/${saved}`, undefined, controller.signal);
          if (current()) setResult(data);
        } else if (linkedCase) {
          if (!guid.test(linkedCase)) throw new Error("This case link is invalid.");
          const data = await request<{ id: string; name: string }>(`/api/reconciliation-cases/${linkedCase}`, undefined, controller.signal);
          if (current()) setCaseContext({ id: linkedCase, name: data.name });
        } else if (document || review) {
          if (!document || !review || !guid.test(document) || !guid.test(review)) throw new Error("This document review link is invalid.");
          const data = await request<Source>(`/api/cited-sources/${document}/reviews/${review}`, undefined, controller.signal);
          if (data.documentId.toLowerCase() !== document.toLowerCase() || data.reviewSessionId.toLowerCase() !== review.toLowerCase()) throw new Error("This document review is unavailable.");
          if (current()) { setLinkedSource(data); setChosen(data.reviewSessionId); }
        }
      } catch (error) { if (current()) setMessage(error instanceof Error ? error.message : "Could not reopen this work."); }
      finally { if (operation.current === version) { operation.current = null; if (current()) setBusy(null); } }
    };
    void restore(); window.addEventListener("popstate", restore);
    return () => { requests.current++; abort.current?.abort(); window.removeEventListener("popstate", restore); };
  }, [canRead]);

  function invalidate() {
    operation.current = null;
    epoch.current++; abort.current?.abort(); setBusy(null);
    setResult(null); setLocation(null); setMessage(""); pending.current = null; setRetryAvailable(false);
  }

  async function perform(label: string, action: (signal: AbortSignal, current: () => boolean) => Promise<void>) {
    if (operation.current !== null) return;
    abort.current?.abort();
    const controller = new AbortController(); abort.current = controller;
    const version = ++epoch.current;
    operation.current = version;
    const current = () => mounted.current && version === epoch.current;
    setBusy(label); setMessage("");
    const timer = setTimeout(() => controller.abort(), 50000);
    try { await action(controller.signal, current); }
    catch (error) {
      if (current()) {
        if (label === "case") setBrowseSources(true);
        setMessage(controller.signal.aborted ? "The request timed out. You can retry the same request." :
          (error instanceof Error ? error.message : "The request failed.") + (label === "case" ? " Your previous source selection is unchanged; check it before asking." : ""));
        if (label === "search") setRetryAvailable(true);
      }
    } finally { clearTimeout(timer); if (operation.current === version) { operation.current = null; if (current()) setBusy(null); } }
  }

  async function pinSource() {
    if (!canPrepare || busy !== null || operation.current !== null) return;
    const source = availableSources.find(x => x.reviewSessionId === chosen);
    if (!source || pinned.some(x => x.documentId === source.documentId) || pinned.length >= 100) return;
    invalidate();
    await perform("pin", async (signal, current) => {
      const index = await request<{ id: string; status: string }>("/api/cited-indexes",
        { documentId: source.documentId, reviewSessionId: source.reviewSessionId }, signal);
      if (index.status !== "Ready" || !guid.test(index.id)) throw new Error("The source index is not ready.");
      if (current()) { setPinned(items => [...items, { ...source, indexId: index.id }]); setSavedCorpus(null); setChosen(""); setCaseName(""); setCaseContext(null); }
    });
  }

  async function loadCase() {
    if (!canPrepare || busy !== null || operation.current !== null) return;
    if (!guid.test(caseId.trim())) { setMessage("Enter a valid reconciliation case ID."); return; }
    invalidate(); setCaseName(""); setCaseContext(null);
    await perform("case", async (signal, current) => {
      const data = await request<{ id?: string; name: string; version?: string; sources: PinnedSource[] }>(`/api/cited-cases/${caseId.trim()}/sources`, {}, signal);
      if (current()) { setPinned(data.sources); setSavedCorpus(null); setLinkedSource(null); setCaseName(data.name); setCaseContext({ id: data.id ?? caseId.trim(), name: data.name, version: data.version }); setChosen(""); setBrowseSources(false); }
    });
  }

  async function ask(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!canRead || busy !== null || operation.current !== null || !question.trim() || !sourceIds.length) return;
    if (mode === "reviewed" && sourceIds.length > 10) { setMessage("Select up to ten documents for reviewed answers."); return; }
    const indexIds = [...sourceIds].sort();
    const fingerprint = JSON.stringify([mode, retrievalMode, question.trim(), indexIds]);
    if (pending.current?.fingerprint !== fingerprint)
      pending.current = { fingerprint, requestKey: crypto.randomUUID() };
    const body = { question: question.trim(), indexIds, topK: 5, mode: retrievalMode, requestKey: pending.current.requestKey };
    setResult(null); setLocation(null); setRetryAvailable(false);
    await perform("search", async (signal, current) => {
      const path = mode === "reviewed" ? "/api/cited-questions/reviewed" :
        retrievalMode === "lexical" ? "/api/cited-questions" : "/api/cited-questions/semantic";
      const data = await request<QuestionResult>(path, body, signal);
      if (current()) {
        setResult(data); setSavedId(data.retrieval.id); rememberResult(data.retrieval.id);
        setRecentAttempt(value => value + 1);
      }
    });
  }

  async function loadSaved(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    await openSaved(savedId.trim());
  }

  async function openSaved(id: string) {
    if (!canRead || busy !== null || operation.current !== null) return;
    if (!guid.test(id)) { setMessage("Enter a valid saved retrieval ID."); return; }
    invalidate();
    await perform("reload", async (signal, current) => {
      const data = await request<QuestionResult>(`/api/cited-questions/${id}`, undefined, signal);
      if (current()) {
        setResult(data); setSavedId(data.retrieval.id); rememberResult(data.retrieval.id);
        // Saved-question controls sit below the answer. Reveal it after rendering,
        // but never steal focus after navigation, new typing or another request.
        requestAnimationFrame(() => { if (current()) resultHeading.current?.focus(); });
      }
    });
  }

  async function moreCases() {
    if (busy !== null || operation.current !== null || caseLoadingOwner.current || caseOffset === null) return;
    await perform("cases", async (signal, current) => {
      const data = await request<CaseList>(`/api/reconciliation-cases?offset=${caseOffset}`, undefined, signal);
      if (current()) { setCases(items => [...new Map([...items, ...data.cases].map(item => [item.id, item])).values()]); setCaseOffset(data.nextOffset); }
    });
  }

  async function openEvidence(hit: Hit, spanId?: string) {
    if (!canRead || busy !== null || operation.current !== null) return;
    if (!result || ![result.retrieval.id, hit.hitId, ...(spanId ? [spanId] : [])].every(x => guid.test(x))) return;
    setLocation(null);
    await perform("evidence", async (signal, current) => {
      const data = await request<Location>(`/api/cited-questions/${result.retrieval.id}/hits/${hit.hitId}/evidence${spanId ? `?spanId=${spanId}` : ""}`, undefined, signal);
      if (current()) { setLocation(data); requestAnimationFrame(() => { if (current()) evidenceHeading.current?.focus(); }); }
    });
  }

  function retryCases() {
    if (caseLoadingOwner.current || operation.current !== null) return;
    caseLoadingOwner.current = true; setCaseLoading(true); setCaseAttempt(value => value + 1);
  }
  function retryRecent() { setRecentAttempt(value => value + 1); }
  function removeSource(indexId: string) {
    invalidate(); setCaseName(""); setCaseContext(null); setBrowseSources(true); setPinned(items => items.filter(source => source.indexId !== indexId));
  }
  function clearSavedSources() { invalidate(); setSavedCorpus(null); }
  function useSavedSources() {
    if (!result) return;
    const saved = { id: result.retrieval.id, indexIds: [...result.indexIds] };
    invalidate(); setPinned([]); setSavedCorpus(saved); setLinkedSource(null); setChosen(""); setCaseName(""); setCaseContext(null); setBrowseSources(true);
  }
  function changeMode(value: string) { invalidate(); setMode(value); }
  function changeRetrievalMode(value: string) { invalidate(); setRetrievalMode(value); }
  function changeQuestion(value: string) { invalidate(); setQuestion(value); }
  function changeSavedId(value: string) { invalidate(); setSavedId(value); }

  function searchReviews(clear = false) {
    if (busy !== null || operation.current !== null) return;
    setChosen("");
    sourceBrowser.searchSources(clear);
  }

  function pageReviews(next: boolean) {
    if (busy !== null || operation.current !== null) return;
    setChosen("");
    sourceBrowser.sourcePage(next);
  }

  const sourceIds = savedCorpus?.indexIds ?? pinned.map(source => source.indexId);
  const availableSources = [...new Map([...sources, ...(linkedSource ? [linkedSource] : [])].map(source => [source.reviewSessionId, source])).values()];
  const linkedPinned = linkedSource && pinned.some(source => source.documentId === linkedSource.documentId && source.reviewSessionId === linkedSource.reviewSessionId);
  return {
    canRead, caseContext, caseName, linkedSource, linkedPinned,
    sourceLoading, sourceError, chosen, setChosen, caseId, setCaseId, cases,
    caseListError, caseLoading, caseOffset, nextOffset, availableSources, pinned, savedCorpus,
    question, mode, retrievalMode, sourceIds, busy, retryAvailable,
    result, location, setLocation, message, savedId, recent, recentError, evidenceHeading, resultHeading,
    pinSource, loadCase, ask, loadSaved, openSaved, openEvidence, moreCases,
    browseSources, setBrowseSources, sourceBrowser, searchReviews, pageReviews,
    retrySources, retryCases, retryRecent, removeSource, clearSavedSources, useSavedSources,
    changeMode, changeRetrievalMode, changeQuestion, changeSavedId,
  };
}
