import type { AnchorHTMLAttributes } from "react";

// Vite hosts actual page components for this transport test; links perform real
// browser navigation. Next.js server routing is verified by the main demo.
export function usePathname() { return window.location.pathname; }
const router = {
  replace: (url: string) => window.location.replace(url),
  push: (url: string) => window.location.assign(url),
  refresh: () => window.location.reload(),
  back: () => window.history.back(),
};
export function useRouter() { return router; }
export default function Link(props: AnchorHTMLAttributes<HTMLAnchorElement>) { return <a {...props}/>; }
