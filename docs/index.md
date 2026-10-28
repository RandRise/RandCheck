---
layout: home

hero:
  name: "RandCheck"
  text: "Review the plan before we build"
  tagline: Extract, reconcile, and verify business documents with traceable evidence.
  actions:
    - theme: brand
      text: Review the current workflow
      link: /workflow-review
    - theme: alt
      text: Review delivery phases
      link: /phases/

features:
  - title: The problem
    details: Finance and procurement teams compare supplier invoices with purchase orders and check the source of each difference.
  - title: The differentiator
    details: The platform connects related documents, explains discrepancies, and links every finding to evidence.
  - title: The delivery strategy
    details: Begin with a narrow English invoice workflow, measure it, then add reconciliation, RAG, security, Arabic, and deployment.
---

::: info Start with the current workflow
The overview below records the earlier planning baseline. For the current invoice/PO process, illustrated explanations and proposals awaiting review, open the [Workflow review room](/workflow-review).
:::

## Working product statement

> An enterprise platform that extracts and reconciles UAE business documents, flags financial discrepancies, and provides traceable evidence for human review.

The product is named **RandCheck**, approved on October 28, 2026. The GitHub repository name is `randcheck`. Earlier names in historical records describe the same project.

## Earlier planning baseline

| Decision | Current direction |
| --- | --- |
| First user | Accounts-payable or procurement reviewer |
| First document | English UAE supplier invoice |
| Core workflow | Invoice → extraction → human review |
| Earlier differentiating workflow | Quotation ↔ purchase order ↔ invoice reconciliation — retained for existing work; superseded for new reviews on October 12, 2026 |
| UAE relevance | AED, UAE VAT, TRN, local procurement workflow |
| Arabic | Planned after the English workflow is evaluated |
| Product UI | Next.js |
| Enterprise API | ASP.NET Core |
| AI processing | Python/FastAPI |
| Data | PostgreSQL via EF Core Code First; local host port `55432` |

## How to use these documents

1. Review the [product specification](/product-spec).
2. Confirm the open decisions in [Phase 0](/phases/phase-0-product-definition).
3. Review the acceptance criteria for each phase.
4. Record agreed changes in the [decision log](/decisions).
5. Only then move the relevant phase from **Proposed** to **Approved**.

Use the [local setup guide](/setup) and review the [P1-T08 acceptance verification](/evidence/phase-1/P1-T08-acceptance-verification). Phase 1 is **Complete**; later document processing, evaluation, reconciliation, RAG, security, Arabic, and deployment phases remain planned.
