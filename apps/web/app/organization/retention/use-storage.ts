import { useCallback, useEffect, useRef, useState } from "react";
import { useEnterprise } from "../../security/enterprise-provider";
import { readStorage, STORAGE_BASE, storageBody, storageRequest, StorageRequestError } from "./storage-api";
import type { CleanupResult, StorageHold, StoragePolicy, StoragePreview } from "./storage.types";
import { clearStorageRecovery, needsStorageRecovery, trackStorageWrite, waitForStorageWrite } from "./storage-recovery";

const RECOVERY_MESSAGE = "An earlier storage change needs checking. Refresh controls and check Activity history before choosing another change. Keep this page open if browser storage is blocked.";

export function useStorage() {
  const { session } = useEnterprise();
  const recoveryScope = `${session.organizationId}:${session.actorId}:storage`;
  const [policy, setPolicy] = useState<StoragePolicy | null>(null);
  const [holds, setHolds] = useState<StorageHold[]>([]);
  const [preview, setPreview] = useState<StoragePreview | null>(null);
  const [offset, setOffset] = useState(0);
  const [holdOffset, setHoldOffset] = useState(0);
  const [days, setDays] = useState("");
  const [hours, setHours] = useState("");
  const [document, setDocument] = useState("");
  const [scope, setScope] = useState("organization");
  const [reason, setReason] = useState("AdministrativeReview");
  const [release, setRelease] = useState<StorageHold | null>(null);
  const [cleanup, setCleanup] = useState<CleanupResult | null>(null);
  const [cleanupVersion, setCleanupVersion] = useState("");
  const [confirmCleanup, setConfirmCleanup] = useState(false);
  const [busy, setBusy] = useState(true);
  const [deletionBusy, setDeletionBusy] = useState(false);
  const [needsRefresh, setNeedsRefresh] = useState(() => needsStorageRecovery(recoveryScope));
  const [error, setError] = useState(() => needsStorageRecovery(recoveryScope) ? RECOVERY_MESSAGE : "");
  const [revision, setRevision] = useState(0);
  const [message, setMessage] = useState("");
  const pending = useRef(false);
  const mounted = useRef(false);
  const request = useRef<AbortController | null>(null);
  // Draft values stay tied to the version the administrator actually edited.
  const draftPolicy = useRef<StoragePolicy | null>(null);
  const [draftSnapshot, setDraftSnapshot] = useState<StoragePolicy | null>(null);

  const load = useCallback(async (recover = false) => {
    if (!mounted.current) return false;
    request.current?.abort();
    const abort = new AbortController();
    request.current = abort;
    await waitForStorageWrite(recoveryScope);
    if (!mounted.current || abort.signal.aborted) return false;
    const result = await readStorage(offset, holdOffset, abort.signal);
    if (!mounted.current || abort.signal.aborted) return false;
    setPolicy(result.policy); setHolds(result.holds); setPreview(result.preview);
    if (!draftPolicy.current) {
      draftPolicy.current = result.policy;
      setDraftSnapshot(result.policy);
      setDays(result.policy.documentDays?.toString() ?? "");
      setHours(result.policy.quarantineHours?.toString() ?? "");
    }
    setCleanup(null); setCleanupVersion(""); setConfirmCleanup(false); setRelease(null);
    if (recover) clearStorageRecovery(recoveryScope);
    const recovery = needsStorageRecovery(recoveryScope);
    setNeedsRefresh(recovery);
    if (recovery) setError(RECOVERY_MESSAGE);
    setRevision(value => value + 1);
    return true;
  }, [offset, holdOffset, recoveryScope]);

  useEffect(() => {
    mounted.current = true;
    let disposed = false;
    queueMicrotask(() => {
      if (disposed) return;
      pending.current = true; setBusy(true); setError("");
      void load().catch(() => {
        if (!disposed) { setPolicy(null); setPreview(null); setError("Storage controls could not be loaded. Refresh to try again."); }
      }).finally(() => { if (!disposed) { pending.current = false; setBusy(false); } });
    });
    return () => { disposed = true; mounted.current = false; request.current?.abort(); };
  }, [load]);

  async function act(action: () => Promise<void>, recoveryRead = false) {
    if (pending.current || deletionBusy || needsRefresh && !recoveryRead) return;
    pending.current = true; setBusy(true); setError(""); setMessage("");
    try { await action(); }
    catch (failure) {
      if (!mounted.current) return;
      setError(failure instanceof StorageRequestError ? failure.message : "The result could not be confirmed. Refresh controls and check Activity history before trying again.");
      setNeedsRefresh(true);
      setCleanup(null); setConfirmCleanup(false); setRelease(null);
    } finally {
      pending.current = false;
      if (mounted.current) setBusy(false);
    }
  }

  const write = <T,>(path: string, options: RequestInit) => trackStorageWrite(recoveryScope, () => storageRequest<T>(path, options));
  const refresh = () => act(async () => { await load(true); }, true);
  const savePolicy = () => act(async () => {
    if (!draftPolicy.current) return;
    const saved = await write<StoragePolicy>(STORAGE_BASE, storageBody("PUT", {
      expectedVersion: draftPolicy.current.version, documentDays: days === "" ? null : Number(days), quarantineHours: hours === "" ? null : Number(hours),
    }));
    if (!mounted.current) return;
    draftPolicy.current = saved;
    setDraftSnapshot(saved);
    await load(); if (mounted.current) setMessage("Storage settings saved and recorded.");
  });
  const placeHold = () => act(async () => {
    await write(`${STORAGE_BASE}/holds`, storageBody("POST", { documentId: scope === "document" ? document.trim() : null, reason }));
    if (!mounted.current) return;
    setDocument(""); await load(); if (mounted.current) setMessage("Protection added. It stays active until an Admin releases it.");
  });
  const releaseHold = () => act(async () => {
    if (!release) return;
    await write(`${STORAGE_BASE}/holds/${release.id}/release`, storageBody("POST", { expectedVersion: release.version }));
    await load(); if (mounted.current) setMessage("Protection released and recorded. No document was deleted.");
  });
  const previewCleanup = () => act(async () => {
    if (!policy) return;
    const result = await storageRequest<CleanupResult>("/api/organization/quarantine/cleanup", storageBody("POST", { expectedPolicyVersion: policy.version, execute: false }));
    if (!mounted.current) return;
    setCleanup(result); setCleanupVersion(policy.version); setConfirmCleanup(false);
  });
  const runCleanup = () => act(async () => {
    if (!cleanup || cleanup.executed || !confirmCleanup) return;
    const result = await write<CleanupResult>("/api/organization/quarantine/cleanup", storageBody("POST", { expectedPolicyVersion: cleanupVersion, execute: true }));
    if (!mounted.current) return;
    setCleanup(result); setConfirmCleanup(false); setMessage("Abandoned upload cleanup finished. Activity history records the individual actions.");
  });

  function changePage(value: number, holdsPage: boolean) {
    const current = holdsPage ? holdOffset : offset;
    if (pending.current || deletionBusy || needsRefresh || value === current || !Number.isInteger(value) || value < 0 || value > 10000 || value % 100) return;
    pending.current = true; setBusy(true);
    if (holdsPage) setHoldOffset(value); else setOffset(value);
  }

  function reloadSettings() {
    if (pending.current || deletionBusy || needsRefresh || !policy) return;
    draftPolicy.current = policy;
    setDraftSnapshot(policy);
    setDays(policy.documentDays?.toString() ?? "");
    setHours(policy.quarantineHours?.toString() ?? "");
  }

  return { session, policy, holds, preview, offset, holdOffset, days, hours, document, scope, reason, release, revision,
    cleanup, confirmCleanup, busy: busy || deletionBusy, locked: busy || deletionBusy || needsRefresh,
    needsRefresh, error, message, setDays, setHours, setDocument, setScope, setReason, setRelease, setConfirmCleanup,
    setOffset: (value: number) => changePage(value, false), setHoldOffset: (value: number) => changePage(value, true),
    setDeletionBusy, refresh, savePolicy, placeHold, releaseHold, previewCleanup, runCleanup, reloadSettings,
    settingsChangedElsewhere: !!policy && !!draftSnapshot && policy.version !== draftSnapshot.version,
    settingsEdited: days !== (draftSnapshot?.documentDays?.toString() ?? "") || hours !== (draftSnapshot?.quarantineHours?.toString() ?? ""),
    onRemoved: (id: string) => setPreview(current => current && ({ ...current, documents: current.documents.filter(item => item.documentId !== id) })),
  };
}

export type StorageController = ReturnType<typeof useStorage>;
