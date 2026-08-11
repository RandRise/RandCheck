import { defineConfig } from "vitepress";

export default defineConfig({
  title: "UAE Document Intelligence",
  description: "Product plan, architecture, phases, and review decisions",
  cleanUrls: true,
  themeConfig: {
    nav: [
      { text: "Overview", link: "/" },
      { text: "Setup", link: "/setup" },
      { text: "Product", link: "/product-spec" },
      { text: "Phases", link: "/phases/" },
      { text: "Roadmap", link: "/roadmap" },
      { text: "Architecture", link: "/architecture" }
    ],
    sidebar: [
      {
        text: "Product",
        items: [
          { text: "Overview", link: "/" },
          { text: "Product specification", link: "/product-spec" },
          { text: "Architecture", link: "/architecture" },
          { text: "Local setup", link: "/setup" },
          { text: "Database design", link: "/data/database-design" },
          { text: "Phase 1 tasks", link: "/tasks/phase-1" },
          { text: "Phase 1 database evidence", link: "/evidence/phase-1/P1-T04-database-verification" },
          { text: "Phase 1 acceptance", link: "/evidence/phase-1/P1-T08-acceptance-verification" },
          { text: "Decision log", link: "/decisions" },
          { text: "Review process", link: "/review-process" },
          { text: "Roadmap", link: "/roadmap" }
        ]
      },
      {
        text: "Phase 2 planning",
        collapsed: false,
        items: [
          { text: "Phase 2 specification", link: "/phases/phase-2-document-processing" },
          { text: "Phase 2 tasks", link: "/tasks/phase-2" },
          { text: "Extraction provider contract", link: "/design/phase-2-extraction-provider-contract" },
          { text: "Synthetic sample plan", link: "/data/phase-2-sample-plan" },
          { text: "Phase 2 quality plan", link: "/testing/phase-2-quality-plan" },
          { text: "P2-T02 verification", link: "/evidence/phase-2/P2-T02-contract-persistence-verification" },
          { text: "P2-T03 verification", link: "/evidence/phase-2/P2-T03-synthetic-fixture-verification" },
          { text: "P2-T04 verification", link: "/evidence/phase-2/P2-T04-local-provider-verification" }
        ]
      },
      {
        text: "Delivery phases",
        collapsed: false,
        items: [
          { text: "All phases", link: "/phases/" },
          { text: "0 — Product definition", link: "/phases/phase-0-product-definition" },
          { text: "1 — Invoice foundation", link: "/phases/phase-1-invoice-foundation" },
          { text: "2 — Real document processing", link: "/phases/phase-2-document-processing" },
          { text: "3 — Review and evaluation", link: "/phases/phase-3-review-evaluation" },
          { text: "4 — Reconciliation", link: "/phases/phase-4-reconciliation" },
          { text: "5 — Cited questions", link: "/phases/phase-5-cited-questions" },
          { text: "6 — Enterprise security", link: "/phases/phase-6-enterprise-security" },
          { text: "7 — Arabic support", link: "/phases/phase-7-arabic-support" },
          { text: "8 — Operations and deployment", link: "/phases/phase-8-operations-deployment" }
        ]
      }
    ],
    search: { provider: "local" },
    outline: { level: [2, 3] },
    footer: {
      message: "Planning documentation for a portfolio project. No client or performance claims.",
      copyright: "Rand Issa"
    }
  }
});
