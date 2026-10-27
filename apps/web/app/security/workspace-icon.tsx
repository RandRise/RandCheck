import { Archive, Files, History, House, MessagesSquare, ShieldCheck, UsersRound } from "lucide-react";

const navigationIcons = {
  "/": House,
  "/documents": Files,
  "/reconciliation": History,
  "/questions": MessagesSquare,
  "/organization": UsersRound,
  "/organization/audit": ShieldCheck,
  "/organization/retention": Archive,
};

/** Decorative icons accompany the existing link names; permissions stay in the provider. */
export function WorkspaceIcon({ href }: { href: string }) {
  const Icon = navigationIcons[href as keyof typeof navigationIcons];
  return Icon ? <Icon className="workspace-nav-icon" size={18} strokeWidth={1.75} aria-hidden="true" focusable="false" /> : null;
}
