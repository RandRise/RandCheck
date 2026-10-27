"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { AppearanceToggle } from "../appearance";
import type { Session } from "./enterprise-provider";
import { documentRoleLabel } from "./document-roles";
import { WorkspaceGuide } from "./workspace-guide";
import { WorkspaceIcon } from "./workspace-icon";
import "./workspace-frame.css";

export type WorkspaceLink = { href: string; label: string; group: "Work" | "Other tools" | "Administration" };

export function WorkspaceFrame({ session, path, links, local, onSignOut, allowHelp, children }: {
  session: Session; path: string; links: WorkspaceLink[]; local: boolean;
  onSignOut: () => Promise<void>; allowHelp: boolean; children: ReactNode;
}) {
  return <div className="workspace-shell">
    <a className="workspace-skip" href="#workspace-content">Skip to content</a>
    <aside className="workspace-sidebar">
      <div className="workspace-brand"><span aria-hidden="true">d.</span>Document review</div>
      <div className="workspace-organization"><strong dir="auto">{session.organizationName}</strong><small>Workspace</small></div>
      <nav aria-label="Workspace">
        {(["Work", "Other tools", "Administration"] as const).map(group => {
          const items = links.filter(link => link.group === group);
          if (group === "Other tools") return items.length ? <details className="workspace-nav-group" key={group}
            open={items.some(link => link.href === path)}>
            <summary>Other tools</summary>
            <div className="workspace-tool-links">{items.map(link => <Link key={link.href} href={link.href} aria-current={path === link.href ? "page" : undefined}><WorkspaceIcon href={link.href}/><span>{link.label}</span></Link>)}</div>
          </details> : null;
          return items.length ? <div className="workspace-nav-group" key={group}>
            {group !== "Administration" && <span>{group}</span>}
            {items.map(link => <Link key={link.href} href={link.href} aria-current={path === link.href || link.href === "/organization" && path.startsWith("/organization/") || link.href === "/" && (path === "/invoices" || path === "/invoices/new") ? "page" : undefined}><WorkspaceIcon href={link.href}/><span>{link.label}</span></Link>)}
          </div> : null;
        })}
      </nav>
      {local && <span className="workspace-local">Local development</span>}
    </aside>
    <div className="workspace-body">
      <div className="workspace-accountbar">
        <div className="workspace-account"><strong dir="auto">{session.displayName}</strong><span>{documentRoleLabel(session.role)}</span></div>
        <AppearanceToggle/>
        {!local && <button className="security-button secondary" onClick={onSignOut}>Sign out</button>}
        {allowHelp && <WorkspaceGuide key={`${session.organizationId}:${session.actorId}:${session.role}`} session={session} path={path}/>}
      </div>
      <div id="workspace-content" tabIndex={-1}>{children}</div>
    </div>
  </div>;
}

export function AccessFrame({ children }: { children: ReactNode }) {
  return <><div className="appearance-toolbar"><AppearanceToggle/></div>{children}</>;
}
