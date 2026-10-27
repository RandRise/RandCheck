import { useCallback, useEffect, useRef, useState, type FormEvent } from "react";
import { useEnterprise } from "../../security/enterprise-provider";
import { isDocumentRole } from "../../security/document-roles";
import { MemberSaveError, readMembers, saveMember } from "./member-api";
import type { Member, MemberSelection } from "./member.types";
import { clearMemberRecovery, needsMemberRecovery, trackMemberSave, waitForMemberSave } from "./member-recovery";

const RECOVERY_MESSAGE = "An earlier save needs checking. Reload the people list, check the current access, then choose any further change. Do not repeat the earlier save without checking.";

export function usePeople() {
  const { session, refresh } = useEnterprise();
  const scope = `${session.organizationId}:${session.actorId}`;
  const [members, setMembers] = useState<Member[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");
  const [selection, setSelection] = useState<MemberSelection>(null);
  const [role, setRole] = useState("");
  const [active, setActive] = useState(true);
  const [busy, setBusy] = useState(false);
  const [message, setMessage] = useState("");
  const [error, setError] = useState(() => needsMemberRecovery(scope) ? RECOVERY_MESSAGE : "");
  const [recovery, setRecovery] = useState(() => needsMemberRecovery(scope));
  const [confirm, setConfirm] = useState(false);
  const saving = useRef(false);
  const reading = useRef(false);
  const mounted = useRef(false);
  const read = useRef<AbortController | null>(null);

  const load = useCallback(async () => {
    read.current?.abort();
    const request = new AbortController();
    read.current = request;
    reading.current = true;
    setLoading(true);
    setLoadError("");
    try {
      // A remounted page must not read ahead of its own outstanding SPA write.
      await waitForMemberSave(scope);
      const next = await readMembers(request.signal);
      if (!mounted.current || request.signal.aborted) return false;
      setMembers(next);
      return true;
    } catch {
      if (mounted.current && !request.signal.aborted) setLoadError("People could not be loaded. Try refreshing the list.");
      return false;
    } finally {
      if (mounted.current && !request.signal.aborted) { reading.current = false; setLoading(false); }
    }
  }, [scope]);

  useEffect(() => {
    mounted.current = true;
    // Initial loading is already rendered. Schedule the external read after setup.
    let disposed = false;
    queueMicrotask(() => { if (!disposed) void load(); });
    return () => { disposed = true; mounted.current = false; read.current?.abort(); };
  }, [load]);

  // Warn on full reload and block links during a write. Back/Forward recovery
  // uses the account-scoped marker and pending promise, not a replayed request.
  useEffect(() => {
    const unload = (event: BeforeUnloadEvent) => {
      if (saving.current) { event.preventDefault(); event.returnValue = ""; }
    };
    const navigate = (event: MouseEvent) => {
      if (saving.current && event.target instanceof Element && event.target.closest("a[href]")) event.preventDefault();
    };
    window.addEventListener("beforeunload", unload);
    document.addEventListener("click", navigate, true);
    return () => { window.removeEventListener("beforeunload", unload); document.removeEventListener("click", navigate, true); };
  }, []);

  function edit(member: Member | "new") {
    if (saving.current || reading.current || recovery) return;
    setSelection(member);
    setRole(member === "new" ? "" : member.role);
    setActive(member === "new" || member.isActive);
    setError(""); setMessage(""); setConfirm(false);
  }

  function close() {
    if (!saving.current && !reading.current && !recovery) { setSelection(null); setError(""); }
  }

  async function reload() {
    if (saving.current || reading.current) return;
    // Keep the draft and write lock if recovery itself fails.
    if (await load() && recovery) {
      clearMemberRecovery(scope);
      setSelection(null); setError(""); setRecovery(false); setConfirm(false);
    }
  }

  async function save(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!selection || recovery || saving.current || reading.current) return;
    if (!isDocumentRole(role)) { setError("Choose a role before saving."); return; }
    if (selection !== "new" && selection.isActive && !active && !confirm) { setConfirm(true); return; }
    const form = new FormData(event.currentTarget);
    const body = selection === "new"
      ? { tenantId: session.tenantId, objectId: form.get("objectId"), displayName: form.get("displayName"), role }
      : { expectedVersion: selection.version, role, isActive: active };
    saving.current = true;
    setBusy(true); setError("");
    try {
      const saved = await trackMemberSave(scope, () => saveMember(selection === "new" ? null : selection.id, body));
      if (!mounted.current) return;
      setSelection(null);
      setMessage(`${saved.displayName}: ${saved.isActive ? "access saved" : "access revoked"}. The change is recorded in the audit trail.`);
      if (saved.actorId === session.actorId) await refresh();
      if (mounted.current) await load();
    } catch (failure) {
      if (!mounted.current) return;
      setError(failure instanceof Error ? failure.message : "Access could not be saved.");
      setRecovery(failure instanceof MemberSaveError && failure.reloadRequired);
    } finally {
      saving.current = false;
      if (mounted.current) setBusy(false);
    }
  }

  return { session, members, loading, loadError, selection, role, active, busy, message, error, recovery, confirm,
    edit, close, reload, save,
    changeRole: (value: string) => { setRole(value); setConfirm(false); },
    changeActive: (value: boolean) => { setActive(value); setConfirm(false); } };
}

export type PeopleController = ReturnType<typeof usePeople>;
