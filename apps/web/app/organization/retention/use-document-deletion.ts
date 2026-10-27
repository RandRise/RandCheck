import { useEffect, useRef, useState } from "react";
import { useEnterprise } from "../../security/enterprise-provider";
import { deletionPath, deletionRequest, DeletionRequestError, type Deletion, type Plan } from "./deletion-api";
import { clearStorageRecovery, needsStorageRecovery, trackStorageWrite, waitForStorageWrite } from "./storage-recovery";

export type DeletionProps = {
  enabled: boolean;
  locked?: boolean;
  onBusyChange?: (busy: boolean) => void;
  onRemoved?: (documentId: string) => void;
};

export function useDocumentDeletion({ enabled, locked = false, onBusyChange, onRemoved }: DeletionProps) {
  const { session } = useEnterprise();
  const scope = `${session.organizationId}:${session.actorId}:deletion`;
  const [id, setId] = useState("");
  const [plan, setPlan] = useState<Plan | null>(null);
  const [resume, setResume] = useState<Deletion | null>(null);
  const [records, setRecords] = useState<Deletion[]>([]);
  const [offset, setOffset] = useState(0);
  const [revision, setRevision] = useState(0);
  const [confirmed, setConfirmed] = useState(false);
  const [busy, setBusy] = useState(false);
  const [loading, setLoading] = useState(enabled);
  const [needsRefresh, setNeedsRefresh] = useState(() => needsStorageRecovery(scope));
  const [historyError, setHistoryError] = useState(false);
  const [error, setError] = useState("");
  const [message, setMessage] = useState("");
  const pending = useRef(false);
  const mounted = useRef(false);

  useEffect(() => { mounted.current = true; return () => { mounted.current = false; }; }, []);

  useEffect(() => {
    if (!enabled) return;
    const abort = new AbortController();
    void (async () => {
      await waitForStorageWrite(scope);
      if (abort.signal.aborted) return;
      const rows = await deletionRequest<Deletion[]>(`/api/organization/retention/deletions?offset=${offset}`, { signal: abort.signal });
      if (abort.signal.aborted) return;
      setRecords(rows);
      if (revision > 0) clearStorageRecovery(scope);
      const recovery = needsStorageRecovery(scope);
      setNeedsRefresh(recovery);
      setError(recovery ? "An earlier removal needs checking. Refresh deletion history and inspect the current record before choosing a new action." : "");
    })().catch(() => {
      if (!abort.signal.aborted) { setHistoryError(true); setNeedsRefresh(true); setError("Deletion history could not be loaded. Refresh before retrying a deletion."); }
    }).finally(() => { if (!abort.signal.aborted) setLoading(false); });
    return () => abort.abort();
  }, [enabled, offset, revision, scope]);

  async function act(action: () => Promise<void>) {
    if (!enabled || pending.current || loading || locked || needsRefresh) return;
    pending.current = true; setBusy(true); onBusyChange?.(true); setError(""); setMessage("");
    try { await action(); }
    catch (failure) {
      if (!mounted.current) return;
      setError(failure instanceof DeletionRequestError ? failure.message : "Removal could not be confirmed. Refresh deletion history and check Activity history before trying again.");
      setNeedsRefresh(true); setPlan(null); setResume(null);
    } finally {
      pending.current = false; onBusyChange?.(false);
      if (mounted.current) { setBusy(false); setConfirmed(false); }
    }
  }

  function refreshHistory() {
    if (pending.current || locked || loading) return;
    setPlan(null); setResume(null); setConfirmed(false); setRecords([]);
    setHistoryError(false); setError(""); setLoading(true); setRevision(value => value + 1);
  }

  const inspect = () => act(async () => {
    setPlan(null); setResume(null);
    const inspected = await deletionRequest<Plan>(deletionPath(id));
    if (inspected.documentId.toLowerCase() !== id.toLowerCase()) throw new DeletionRequestError("The returned document does not match the one selected. Refresh and inspect again.");
    if (mounted.current) setPlan(inspected);
  });

  const execute = () => act(async () => {
    const target = resume ?? plan;
    if (!target || !confirmed || !resume && (!plan?.enabled || plan.blockers.length)) return;
    const result = await trackStorageWrite(scope, () => deletionRequest<Deletion>(deletionPath(target.documentId), {
      method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ expectedDocumentVersion: target.documentVersion, expectedPolicyVersion: target.policyVersion }),
    }));
    if (!mounted.current) return;
    setMessage(result.state === "Completed" ? "Document deletion completed and audited." : "Database content removed. File removal is pending.");
    onRemoved?.(result.documentId);
    setPlan(null); setResume(null); setRecords([]); setHistoryError(false); setLoading(true); setRevision(value => value + 1);
  });

  function changePage(next: number) {
    if (pending.current || locked || loading || needsRefresh || next === offset || !Number.isInteger(next) || next < 0 || next > 10000 || next % 100) return;
    setPlan(null); setResume(null); setConfirmed(false); setRecords([]); setHistoryError(false); setError(""); setLoading(true); setOffset(next);
  }

  return { id, plan, resume, target: resume ?? plan, records, offset, confirmed, busy, loading, needsRefresh, historyError, error, message,
    locked: busy || locked || loading || needsRefresh, inspect, execute, refreshHistory, changePage, setConfirmed,
    changeTarget: (value: string) => { if (pending.current) return; setId(value); setPlan(null); setResume(null); setConfirmed(false); },
    reviewPending: (record: Deletion) => { if (pending.current || needsRefresh || loading || locked) return; setPlan(null); setResume(record); setConfirmed(false); setError(""); setMessage(""); },
  };
}
