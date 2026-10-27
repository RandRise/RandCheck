import type { Metadata } from "next";
import "./styles.css";
import { EnterpriseProvider } from "./security/enterprise-provider";
import { AppearanceProvider } from "./appearance";

export const metadata: Metadata = {
  title: "UAE Enterprise Document Intelligence",
  description: "Bilingual document review for UAE operations teams"
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  const mode = process.env.AUTH_MODE ?? (process.env.NODE_ENV === "development" ? "Development" : "Locked");
  const config = { mode: mode === "Development" && process.env.NODE_ENV !== "development" ? "Locked" : mode,
    tenantId: process.env.NEXT_PUBLIC_ENTRA_TENANT_ID ?? "", clientId: process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID ?? "",
    scope: process.env.NEXT_PUBLIC_ENTRA_SCOPE ?? "", api: process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:8080" };
  return <html lang="en"><body><AppearanceProvider showControl={false}><EnterpriseProvider config={config}>{children}</EnterpriseProvider></AppearanceProvider></body></html>;
}
