"use client";

import { createContext, useContext, useSyncExternalStore, type ReactNode } from "react";
import { usePathname } from "next/navigation";
import "./appearance.css";

type Appearance = "dark" | "light";
const storageKey = "document-intelligence:appearance";
const changeEvent = "document-intelligence:appearance-change";
let fallback: Appearance = "dark";
let volatileChoice: Appearance | undefined;
const AppearanceContext = createContext<Appearance>("dark");

function snapshot(): Appearance {
  if (volatileChoice) return volatileChoice;
  try {
    const stored = window.localStorage.getItem(storageKey);
    return stored === "light" || stored === "dark" ? stored : "dark";
  } catch { return fallback; }
}
function subscribe(notify: () => void) {
  const onStorage = (event: StorageEvent) => {
    try { if (event.storageArea && event.storageArea !== window.localStorage) return; } catch { return; }
    if (event.key === storageKey || event.key === null) { volatileChoice = undefined; notify(); }
  };
  window.addEventListener("storage", onStorage);
  window.addEventListener(changeEvent, notify);
  return () => {
    window.removeEventListener("storage", onStorage);
    window.removeEventListener(changeEvent, notify);
  };
}
function choose(appearance: Appearance) {
  fallback = appearance;
  try { window.localStorage.setItem(storageKey, appearance); volatileChoice = undefined; }
  catch { volatileChoice = appearance; }
  window.dispatchEvent(new Event(changeEvent));
}

export function AppearanceToggle() {
  const appearance = useContext(AppearanceContext);
  return <label className="appearance-control">Appearance
    <select aria-label="Appearance" value={appearance} onChange={event => choose(event.target.value as Appearance)}>
      <option value="dark">Dark</option><option value="light">Light</option>
    </select>
  </label>;
}

export function AppearanceProvider({ children, showControl = true }: { children: ReactNode; showControl?: boolean }) {
  const path = usePathname();
  // The identity redirect bridge remains independent of the application frame.
  if (path === "/auth/callback") return children;
  return <WorkspaceAppearance showControl={showControl}>{children}</WorkspaceAppearance>;
}
function WorkspaceAppearance({ children, showControl }: { children: ReactNode; showControl: boolean }) {
  const appearance = useSyncExternalStore(subscribe, snapshot, () => "dark" as const);
  return <AppearanceContext.Provider value={appearance}>
    <div className="app-appearance" data-theme={appearance}>
      {showControl && <div className="appearance-toolbar"><AppearanceToggle/></div>}
      {children}
    </div>
  </AppearanceContext.Provider>;
}
