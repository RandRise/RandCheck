"use client";
import { useEffect, useState } from "react";
let bridge: Promise<void> | undefined;
export default function CallbackPage() {
  const [failed, setFailed] = useState(false);
  useEffect(() => {
    bridge ??= import("@azure/msal-browser/redirect-bridge").then(module => module.broadcastResponseToMainFrame());
    void bridge.catch(() => setFailed(true));
  }, []);
  return <main className="access-message"><h1>{failed ? "Sign-in could not be completed." : "Completing sign-in…"}</h1>
    {failed && <a href="/signin">Return to sign-in</a>}</main>;
}
