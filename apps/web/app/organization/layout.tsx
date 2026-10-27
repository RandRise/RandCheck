"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";
import { useEnterprise } from "../security/enterprise-provider";
import "./administration.css";

const sections = [
  { href: "/organization", label: "People & access" },
  { href: "/organization/audit", label: "Activity history" },
  { href: "/organization/retention", label: "Document storage" },
];

export default function OrganizationLayout({ children }: { children: ReactNode }) {
  const { session } = useEnterprise();
  const path = usePathname();
  if (session.role !== "Admin") return null;

  return <div className="administration-workspace">
    <header className="administration-header">
      <h1>Administration</h1><p dir="auto">{session.organizationName}</p>
      <nav aria-label="Administration sections">
        {sections.map(section => <Link key={section.href} href={section.href} aria-current={path === section.href ? "page" : undefined}>{section.label}</Link>)}
      </nav>
    </header>
    {children}
  </div>;
}
