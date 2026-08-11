import type { Metadata } from "next";
import "./styles.css";

export const metadata: Metadata = {
  title: "UAE Enterprise Document Intelligence",
  description: "Bilingual document review for UAE operations teams"
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return <html lang="en"><body>{children}</body></html>;
}
