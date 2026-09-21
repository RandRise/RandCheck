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
          { text: "Disposable local demo", link: "/local-demo" },
          { text: "CI checks", link: "/testing/ci" },
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
          { text: "P2-T04 verification", link: "/evidence/phase-2/P2-T04-local-provider-verification" },
          { text: "P2-T06 verification", link: "/evidence/phase-2/P2-T06-queue-worker-verification" }
        ]
      },
      {
        text: "Phase 3 evaluation",
        collapsed: false,
        items: [
          { text: "Phase 3 specification", link: "/phases/phase-3-review-evaluation" },
          { text: "Phase 3 tasks", link: "/tasks/phase-3" },
          { text: "Evaluation contract", link: "/design/phase-3-evaluation-contract" },
          { text: "Dataset plan", link: "/data/phase-3-dataset-plan" },
          { text: "Public source research", link: "/data/phase-3-public-source-research" },
          { text: "Source intake", link: "/data/phase-3-source-intake" },
          { text: "Annotation guide", link: "/data/phase-3-annotation-guide" },
          { text: "Phase 3 quality plan", link: "/testing/phase-3-quality-plan" },
          { text: "Startup verification", link: "/evidence/phase-3/P3-start-verification" },
          { text: "P3-T03 intake checkpoint", link: "/evidence/phase-3/P3-T03-intake-readiness-verification" },
          { text: "P3-T07 development acceptance", link: "/evidence/phase-3/P3-T07-development-acceptance-verification" }
        ]
      },
      {
        text: "Phase 4 reconciliation",
        collapsed: false,
        items: [
          { text: "Phase 4 specification", link: "/phases/phase-4-reconciliation" },
          { text: "Phase 4 tasks", link: "/tasks/phase-4" },
          { text: "Reconciliation contract", link: "/design/phase-4-reconciliation-contract" },
          { text: "Phase 4 quality plan", link: "/testing/phase-4-quality-plan" },
          { text: "P4-T01–P4-T05 verification", link: "/evidence/phase-4/P4-T01-T05-backend-foundation-verification" },
          { text: "P4-T06–P4-T08 verification", link: "/evidence/phase-4/P4-T06-T08-acceptance-verification" }
        ]
      },
      {
        text: "Phase 5 cited questions",
        collapsed: false,
        items: [
          { text: "Phase 5 specification", link: "/phases/phase-5-cited-questions" },
          { text: "Phase 5 tasks", link: "/tasks/phase-5" },
            { text: "Cited-question contract", link: "/design/phase-5-cited-question-contract" },
            { text: "Chunking contract", link: "/design/phase-5-chunking-contract" },
            { text: "Lexical retrieval contract", link: "/design/phase-5-lexical-retrieval-contract" },
            { text: "Answer validation contract", link: "/design/phase-5-answer-validation-contract" },
            { text: "Evidence workspace contract", link: "/design/phase-5-workspace-contract" },
          { text: "Phase 5 quality plan", link: "/testing/phase-5-quality-plan" },
          { text: "RAG threat model", link: "/security/phase-5-rag-threat-model" },
          { text: "Server-log policy proposal", link: "/security/phase-5-server-log-policy" },
          { text: "Model pilot proposal", link: "/design/phase-5-model-pilot-proposal" },
          { text: "P5-T06/T08 provider-free boundaries", link: "/evidence/phase-5/P5-T06-T08-provider-free-boundary-verification" },
          { text: "P5-T08 spending ledger", link: "/evidence/phase-5/P5-T08-spending-ledger-verification" },
          { text: "P5-T06/T08 answer bridge", link: "/evidence/phase-5/P5-T06-T08-answer-bridge-verification" },
          { text: "P5-T06/T08 prompt preflight", link: "/evidence/phase-5/P5-T06-T08-prompt-preflight-verification" },
          { text: "P5-T08 process crash recovery", link: "/evidence/phase-5/P5-T08-crash-recovery-verification" },
          { text: "P5-T08 recovery assessment", link: "/evidence/phase-5/P5-T08-recovery-assessment-verification" },
          { text: "P5-T08 recovery execution", link: "/evidence/phase-5/P5-T08-recovery-execution-verification" },
          { text: "OpenAI access readiness", link: "/evidence/phase-5/P5-live-model-readiness-check" },
          { text: "OpenAI request/accounting protocol", link: "/evidence/phase-5/P5-openai-protocol-verification" },
          { text: "Generation journal and transport", link: "/evidence/phase-5/P5-generation-journal-transport-verification" },
          { text: "First live synthetic generation", link: "/evidence/phase-5/P5-live-synthetic-generation-verification" },
          { text: "Live answer-quality baseline", link: "/evidence/phase-5/P5-live-quality-baseline-verification" },
          { text: "Spending recovery contract", link: "/design/phase-5-recovery-contract" },
            { text: "P5-T02 question-set verification", link: "/evidence/phase-5/P5-T02-question-set-verification" },
            { text: "P5-T03 persistence verification", link: "/evidence/phase-5/P5-T03-persistence-verification" },
            { text: "P5-T04 chunking verification", link: "/evidence/phase-5/P5-T04-chunking-verification" },
            { text: "P5-T05 lexical verification", link: "/evidence/phase-5/P5-T05-lexical-verification" },
            { text: "P5-T06 validation verification", link: "/evidence/phase-5/P5-T06-validation-verification" },
            { text: "P5-T07 workspace verification", link: "/evidence/phase-5/P5-T07-workspace-verification" },
            { text: "P5-T08 security and real-stack checkpoint", link: "/evidence/phase-5/P5-T08-security-verification" },
            { text: "P5-T08 request boundary checkpoint", link: "/evidence/phase-5/P5-T08-request-boundary-verification" },
            { text: "P5-T08 workflow logging checkpoint", link: "/evidence/phase-5/P5-T08-workflow-log-verification" },
            { text: "P5-T08 database logging checkpoint", link: "/evidence/phase-5/P5-T08-database-log-verification" }
        ]
      },
      {
        text: "Phase 6 enterprise security",
        collapsed: false,
        items: [
          { text: "Phase 6 tasks", link: "/tasks/phase-6" },
          { text: "Identity and access contract", link: "/design/phase-6-identity-contract" },
          { text: "Organization membership and audit", link: "/design/phase-6-membership-audit-contract" },
          { text: "Private-service access", link: "/design/phase-6-private-service-access" },
          { text: "Worker database and audit", link: "/design/phase-6-worker-security" },
          { text: "Worker security verification", link: "/evidence/phase-6/P6-worker-security-20260912" },
          { text: "API database access", link: "/design/phase-6-api-database-access" },
          { text: "API database verification", link: "/evidence/phase-6/P6-api-database-20260914" },
          { text: "Request and resource limits", link: "/design/phase-6-request-limits" },
          { text: "Request-limit verification", link: "/evidence/phase-6/P6-request-limits-20260914" },
          { text: "Organization capacity", link: "/design/phase-6-organization-capacity" },
          { text: "Capacity verification", link: "/evidence/phase-6/P6-capacity-20260914" },
          { text: "Offline source recovery", link: "/design/phase-6-orphan-recovery" },
          { text: "Local backup and recovery", link: "/design/phase-6-local-recovery" },
          { text: "Offline recovery verification", link: "/evidence/phase-6/P6-orphan-recovery-20260914" },
          { text: "Deployment readiness", link: "/design/phase-6-deployment-readiness" },
          { text: "Protected source keys", link: "/design/phase-6-source-key-protection" },
          { text: "Runtime security baseline", link: "/design/phase-6-runtime-baseline" },
          { text: "GitHub portfolio delivery", link: "/design/github-portfolio-delivery" },
          { text: "Audit operations", link: "/design/phase-6-audit-operations" },
          { text: "Operational audit coverage", link: "/security/phase-6-audit-coverage" },
          { text: "Audit export and retention", link: "/design/phase-6-audit-export-retention" },
          { text: "Runtime verification", link: "/evidence/phase-6/P6-runtime-20260915" },
          { text: "Source key verification", link: "/evidence/phase-6/P6-source-keys-20260915" },
          { text: "Readiness verification", link: "/evidence/phase-6/P6-readiness-20260914" },
          { text: "Storage admission and scanning", link: "/design/phase-6-storage-safety" },
          { text: "Original-file access", link: "/design/phase-6-source-access" },
          { text: "Source access verification", link: "/evidence/phase-6/P6-source-access-20260912" },
          { text: "Retention controls and holds", link: "/design/phase-6-retention-controls" },
          { text: "Document deletion and restore", link: "/design/phase-6-document-deletion" },
            { text: "Deletion and recovery verification", link: "/evidence/phase-6/P6-document-deletion-20260913" },
            { text: "Retention verification", link: "/evidence/phase-6/P6-retention-controls-20260912" },
          { text: "Enterprise threat model", link: "/security/phase-6-threat-model" },
          { text: "Data-access inventory", link: "/security/phase-6-data-access-inventory" },
          { text: "Phase 6 quality plan", link: "/testing/phase-6-quality-plan" },
          { text: "Identity foundation verification", link: "/evidence/phase-6/P6-identity-foundation-20260911" },
          { text: "Membership and audit verification", link: "/evidence/phase-6/P6-membership-audit-20260911" }
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
          { text: "8 — Repository delivery and local operations", link: "/phases/phase-8-operations-deployment" }
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
