"use client";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { createContext, useCallback, useContext, useEffect, useRef, useState } from "react";
import { API, installTransport, secureTransport } from "./api-fetch";
import { createIdentityClient, validIdentityConfig, type IdentityClient, type IdentityConfig } from "./identity-client";
import { SignInScreen } from "./sign-in-screen";
import "./security.css";

export type Session = { organizationId: string; organizationName: string; actorId: string; displayName: string;
  tenantId: string | null; role: "None" | "Viewer" | "Reviewer" | "Operator"; canManageMembers: boolean };
export const developmentSession: Session = { organizationId: "", organizationName: "Local development", actorId: "",
  displayName: "Development identity", tenantId: null, role: "Operator", canManageMembers: false };
type Access = { session: Session; refresh: () => Promise<void> };
const Context = createContext<Access>({ session: developmentSession, refresh: async () => {} });
export const useEnterprise = () => useContext(Context);
export function can(session: Session, permission: string) {
  if (permission === "ManageMembers" || permission === "Audit") return session.canManageMembers;
  if (permission === "Operate") return session.role === "Operator";
  if (permission === "Review") return session.role === "Reviewer" || session.role === "Operator";
  return ["Viewer", "Reviewer", "Operator"].includes(session.role);
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
    setMessage("Your session or access has changed. Check access to continue, or sign out and use another account.");
  }, []);
  useEffect(() => {
    if (local || !configured) return;
    let disposed = false; let remove: (() => void) | undefined;
    const abort = new AbortController(); lifetime.current = abort;
    const run = async () => {
      clientPromise.current ??= identityFactory(config);
      client.current = await clientPromise.current;
      initialization.current ??= client.current.initialize();
      if (!await initialization.current) { if (!disposed) setState("signedOut"); return; }
      if (disposed) return;
      const transport = secureTransport(config.api, () => client.current!.token(), invalidate, abort.signal);
      remove = installTransport(transport);
      const refresh = async () => {
        const response = await transport(`${config.api}/api/session`);
        if (!response.ok) throw new Error("Access unavailable");
        const next = await response.json() as Session;
        if (disposed || abort.signal.aborted) return;
        if (!["None", "Viewer", "Reviewer", "Operator"].includes(next.role) || !next.organizationId || !next.actorId ||
            next.tenantId?.toLowerCase() !== config.tenantId.toLowerCase()) throw new Error("Invalid session");
        setSession(previous => {
          if (previous && JSON.stringify(previous) !== JSON.stringify(next)) {
            // Unmount all workspace state after a membership/organization change.
            setRevision(v => v + 1);
          }
          return next;
        });
        setState("ready"); setMessage("");
      };
      refreshRef.current = async () => { try { await refresh(); } catch { if (!disposed) invalidate(); } };
      await refreshRef.current();
    };
    void run().catch(() => { if (!disposed) { setState("signedOut"); setMessage("Sign-in could not be completed. Please try again."); } });
    const focus = () => { if (document.visibilityState === "visible") void refreshRef.current(); };
    window.addEventListener("focus", focus);
    const timer = window.setInterval(focus, 60000);
    return () => { disposed = true; abort.abort(); remove?.(); refreshRef.current = async () => {}; window.removeEventListener("focus", focus); window.clearInterval(timer); };
  }, [config, configured, identityFactory, invalidate, local]);
  useEffect(() => {
    if (state === "ready" && !local && (path === "/signin" || path === "/auth/callback")) router.replace(session?.role === "None" && session.canManageMembers ? "/organization" : "/");
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
  if (state === "denied") return <main className="access-message"><span className="security-eyebrow">WORKSPACE ACCESS</span><h1>Access needs attention.</h1><p role="alert">{message}</p><button className="security-button" onClick={() => window.location.reload()}>Check access</button> <button className="security-button secondary" onClick={signOut}>Sign out</button></main>;
  if ((!session && state !== "loading") || path === "/signin" || (!configured && !local))
    return <SignInScreen configured={configured} busy={state === "connecting"} message={message} onSignIn={signIn} onSignOut={state === "logoutFailed" ? signOut : undefined}/>;
  if (!session || state === "loading") return <main className="access-message" role="status"><h1>Opening your workspace…</h1><p>Checking identity and organization access.</p></main>;
  const permission = path.startsWith("/organization") ? "ManageMembers" : path === "/reconciliation" ? "Review" : "Read";
  return <Context.Provider value={{ session, refresh: () => refreshRef.current() }}>
    <div className="enterprise-bar"><Link className="enterprise-brand" href="/">DI <span>DOCUMENT INTELLIGENCE</span></Link>
      <div className="enterprise-identity"><strong>{session.organizationName}</strong><span>{session.displayName} · {session.role === "None" ? "No document access" : session.role}{session.canManageMembers ? " · Administrator" : ""}</span></div>
      {session.canManageMembers && <Link href="/organization">Organization</Link>}
      {local ? <span className="security-badge">Local development</span> : <button className="security-button secondary" onClick={signOut}>Sign out</button>}
    </div>
    {can(session, permission) ? <div key={revision}>{children}</div> : <main className="access-message"><span className="security-eyebrow">YOUR ACCESS</span><h1>{permission === "ManageMembers" ? "Administration access required." : "Your role does not include this workspace."}</h1><p>Ask your organization’s administrator to update your access.</p>{can(session, "Read") && <Link href="/">Open document workspace →</Link>}{session.canManageMembers && <Link href="/organization">Manage organization →</Link>}</main>}
  </Context.Provider>;
}
