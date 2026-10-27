// Browser navigation shim for the component preview, not a Next router test.
import type { AnchorHTMLAttributes } from "react";
export function usePathname() { return window.location.pathname; }
const router = { replace: (url: string) => window.location.replace(url), push: (url: string) => window.location.assign(url) };
export function useRouter() { return router; }
export default function Link({ children, href, ...props }: AnchorHTMLAttributes<HTMLAnchorElement>) {
  return <a href={href} {...props}>{children}</a>;
}
