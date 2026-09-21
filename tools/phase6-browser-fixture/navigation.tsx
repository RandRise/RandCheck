import React from "react";
export function usePathname() { return window.location.pathname; }
const router = { replace: (url: string) => window.location.replace(url) };
export function useRouter() { return router; }
export default function Link({ children, href, ...props }: React.AnchorHTMLAttributes<HTMLAnchorElement>) { return <a href={href} {...props}>{children}</a>; }
