"use client";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { createContext, useCallback, useContext, useEffect, useRef, useState } from "react";
import { API, installTransport, secureTransport } from "./api-fetch";
import { createIdentityClient, validIdentityConfig, type IdentityClient, type IdentityConfig } from "./identity-client";
import { SignInScreen } from "./sign-in-screen";
import { AccessFrame, WorkspaceFrame, type WorkspaceLink } from "./workspace-frame";
import "./security.css";

export type Session = { organizationId: string; organizationName: string; actorId: string; displayName: string;
  tenantId: string | null; role: "Staff" | "Admin" };
export const developmentSession: Session = { organizationId: "", organizationName: "Local development", actorId: "",
  displayName: "Development identity", tenantId: null, role: "Staff" };
type Access = { session: Session; refresh: () => Promise<void> };
const Context = createContext<Access>({ session: developmentSession, refresh: async () => {} });
export const useEnterprise = () => useContext(Context);
export const SESSION_READ_TIMEOUT_MS = 15000;
export function can(session: Session, permission: string) {
  if (permission === "ManageMembers" || permission === "Audit" || permission === "Retention") return session.role === "Admin";
  if (["Read", "Ask", "SourceRead", "Operate", "Review"].includes(permission)) return session.role === "Staff" || session.role === "Admin";
  return false;
}
type ProviderProps = { children: React.ReactNode; config: IdentityConfig; identityFactory?: typeof createIdentityClient };
export function EnterpriseProvider(props: ProviderProps) {
  const path = usePathname();
  // MSAL v5's redirect bridge must run without another authentication client or gate.
  return path === "/auth/callback" ? props.children : <EnterpriseSession key={JSON.stringify(props.config)} {...props}/>;
}
function EnterpriseSession({ children, config, identityFactory = createIdentityClient }: ProviderProps) {
  const path = usePathname(); const router = useRouter();
  const local = config.mode === "Development";
  const configured = validIdentityConfig(config);
  const [session, setSession] = useState<Session | null>(local ? developmentSession : null);
  const [state, setState] = useState(local ? "ready" : configured ? "loading" : "signedOut"); const [message, setMessage] = useState("");
  const [revision, setRevision] = useState(0);
  const client = useRef<IdentityClient | null>(null); const lifetime = useRef<AbortController | null>(null);
  const clientPromise = useRef<Promise<IdentityClient> | null>(null);
  const initialization = useRef<Promise<boolean> | null>(null);
  const refreshRef = useRef<() => Promise<void>>(async () => {});
  const invalidate = useCallback(() => {
    lifetime.current?.abort(); setSession(null); setState("denied");
    setMessage("Your session or access could not be verified. Check access to continue, or sign out and use another account.");
  }, []);
  useEffect(() => {
    if (local || !configured) return;
    let disposed = false; let remove: (() => void) | undefined;
    const abort = new AbortController(); lifetime.current = abort;
    let latestSessionRead = 0; let activeSessionRead: AbortController | null = null;
    const run = async () => {
      clientPromise.current ??= identityFactory(config);
      client.current = await clientPromise.current;
      initialization.current ??= client.current.initialize();
      if (!await initialization.current) { if (!disposed) setState("signedOut"); return; }
      if (disposed) return;
      const transport = secureTransport(config.api, () => client.current!.token(), invalidate, abort.signal);
      remove = installTransport(transport);
      const refresh = async () => {
        if (disposed || abort.signal.aborted) return;
        // A newer check owns access even when an older fetch ignores cancellation.
        const version = ++latestSessionRead;
        activeSessionRead?.abort();
        const readAbort = new AbortController(); activeSessionRead = readAbort;
        const current = () => !disposed && !abort.signal.aborted && version === latestSessionRead;
        let interrupt!: () => void;
        const interrupted = new Promise<never>((_, reject) => { interrupt = () => reject(new Error("Access check interrupted")); });
        const cancel = () => readAbort.abort();
        readAbort.signal.addEventListener("abort", interrupt, { once: true });
        abort.signal.addEventListener("abort", cancel, { once: true });
        const timer = window.setTimeout(cancel, SESSION_READ_TIMEOUT_MS);
        try {
          // Workspace requests retain immediate invalidation. Session checks must
          // gate that transport callback too, before it can clear newer access.
          const sessionTransport = secureTransport(config.api, () => client.current!.token(),
            () => { if (current()) invalidate(); }, abort.signal);
          const read = async () => {
            const response = await sessionTransport(`${config.api}/api/session`, { signal: readAbort.signal });
            if (!response.ok) throw new Error("Access unavailable");
            return await response.json() as Session;
          };
          const next = await Promise.race([read(), interrupted]);
          if (!current() || readAbort.signal.aborted) return;
          if (!["Staff", "Admin"].includes(next.role) || "canManageMembers" in next || !next.organizationId || !next.actorId ||
              next.tenantId?.toLowerCase() !== config.tenantId.toLowerCase()) throw new Error("Invalid session");
          setSession(previous => {
            if (previous && JSON.stringify(previous) !== JSON.stringify(next)) {
              // Unmount all workspace state after a membership/organization change.
              setRevision(v => v + 1);
            }
            return next;
          });
          setState("ready"); setMessage("");
        } catch { if (current()) invalidate(); }
        finally {
          window.clearTimeout(timer);
          readAbort.signal.removeEventListener("abort", interrupt);
          abort.signal.removeEventListener("abort", cancel);
          if (activeSessionRead === readAbort) activeSessionRead = null;
        }
      };
      refreshRef.current = refresh;
      await refreshRef.current();
    };
    void run().catch(() => { if (!disposed) { setState("signedOut"); setMessage("Sign-in could not be completed. Please try again."); } });
    const focus = () => { if (document.visibilityState === "visible") void refreshRef.current(); };
    window.addEventListener("focus", focus);
    const timer = window.setInterval(focus, 60000);
    return () => { disposed = true; abort.abort(); remove?.(); refreshRef.current = async () => {}; window.removeEventListener("focus", focus); window.clearInterval(timer); };
  }, [config, configured, identityFactory, invalidate, local]);
  useEffect(() => {
    if (state === "ready" && !local && (path === "/signin" || path === "/auth/callback")) router.replace("/");
  }, [state, local, path, router, session]);
  async function signIn() {
    setState("connecting"); setMessage("");
    try { client.current ??= await identityFactory(config); await client.current.signIn(); }
    catch { setState("signedOut"); setMessage("Sign-in could not be started. Please try again."); }
  }
  async function signOut() {
    lifetime.current?.abort(); setSession(null); setState("signedOut"); setMessage("");
    try { await client.current?.signOut(); } catch { setState("logoutFailed"); setMessage("Workspace access has been cleared. Retry sign-out to end the Microsoft session."); }
  }
  if (state === "denied") return <AccessFrame><main className="access-message"><h1>Access needs attention.</h1><p role="alert">{message}</p><button className="security-button" onClick={() => window.location.reload()}>Check access</button> <button className="security-button secondary" onClick={signOut}>Sign out</button></main></AccessFrame>;
  if ((!session && state !== "loading") || path === "/signin" || (!configured && !local))
    return <AccessFrame><SignInScreen configured={configured} busy={state === "connecting"} message={message} onSignIn={signIn} onSignOut={state === "logoutFailed" ? signOut : undefined}/></AccessFrame>;
  if (!session || state === "loading") return <AccessFrame><main className="access-message" role="status"><h1>Opening your workspace…</h1><p>Checking identity and organization access.</p></main></AccessFrame>;
  const permission = path.startsWith("/organization") ? "ManageMembers" : "Read";
  const links: WorkspaceLink[] = [];
  if (can(session, "Read")) links.push({ href: "/", label: "Home", group: "Work" });
  if (can(session, "Read")) links.push({ href: "/documents", label: "Document library", group: "Work" });
  if (can(session, "Read")) links.push({ href: "/reconciliation", label: "Earlier cases and history", group: "Other tools" });
  if (can(session, "Ask")) links.push({ href: "/questions", label: "Ask about documents", group: "Other tools" });
  if (session.role === "Admin") links.push({ href: "/organization", label: "Administration", group: "Administration" });
  return <Context.Provider value={{ session, refresh: () => refreshRef.current() }}>
    <WorkspaceFrame session={session} path={path} links={links} local={local} onSignOut={signOut} allowHelp={can(session, permission)}>
      {can(session, permission) ? <div key={revision}>{children}</div> : <main className="access-message"><h1>{permission === "ManageMembers" ? "Administration access required." : "Your role does not include this workspace."}</h1><p>Ask your organization’s administrator to update your access.</p>{can(session, "Read") && <Link href="/">Open document workspace →</Link>}{(session.role === "Admin") && <Link href="/organization">Manage organization →</Link>}</main>}
    </WorkspaceFrame>
  </Context.Provider>;
}
