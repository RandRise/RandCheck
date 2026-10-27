# Product specification

The focused prototype direction is described in the
[supplier invoice product plan](design/supplier-invoice-product-plan.md), proposed
October 6, 2026. Its [task sequence](tasks/supplier-invoice-product.md) starts with
the active bounded document-input stage. The broader specification below remains available
as background; it does not expand the focused prototype's scope.

## Product summary

The UAE Enterprise Document Intelligence Platform helps finance and procurement teams review supplier documents. It extracts structured information, connects related documents, identifies discrepancies, and provides evidence for every finding.

The working title is descriptive. A final brand name will be chosen later.

## The business problem

Businesses receive invoices, quotations, purchase orders, and contracts in PDFs, scans, and email attachments. Reviewers often need to:

- retype supplier, date, tax, and amount fields;
- check whether UAE VAT and totals are internally consistent;
- compare an invoice with the approved quotation and purchase order;
- find payment, delivery, renewal, or termination terms;
- explain where a value or conclusion came from; and
- reconstruct who reviewed or changed the result.

This work is repetitive and difficult to audit when it is performed across inboxes and spreadsheets.

## The proposed solution

The platform provides a controlled review workflow:

1. A user uploads a business document.
2. The platform validates and stores it.
3. The AI service identifies its type and extracts structured fields.
4. A reviewer confirms or corrects the result.
5. Related documents are linked.
6. Explainable rules flag mismatches and missing information.
7. Users can ask questions and receive page-level evidence.
8. Security and audit controls record access and changes.

The platform supports human decisions. It does not autonomously approve payments or provide accounting or legal advice.

## Primary users

### Accounts-payable reviewer

Checks supplier identity, invoice number, dates, TRN, VAT, totals, duplicate risk, and supporting approvals.

### Procurement analyst

Links quotations, purchase orders, and invoices, then investigates price, quantity, currency, or supplier mismatches.

### Finance or operations manager

Reviews exceptions, monitors processing outcomes, and measures how much manual correction remains.

### Auditor or administrator

Reviews access and change history, configures permissions, and verifies evidence without relying on undocumented AI output.

## First portfolio scenario

A fictional UAE company receives three related documents:

- quotation: AED 10,000;
- purchase order: AED 10,000; and
- invoice: AED 11,500.

The platform should extract the values, connect the documents, and produce an explainable exception:

> Invoice total exceeds the purchase order by AED 1,500. Evidence: Purchase Order page 1 and Invoice page 2.

## MVP boundary

### Included

- English UAE supplier invoices, quotations, and purchase orders
- Secure upload and metadata storage
- Structured field and line-item extraction
- Human confirmation and correction
- Document linking
- Explainable discrepancy detection
- Evidence linked to document and page
- Authentication, roles, and audit history
- Measured extraction and workflow results
- A deployed portfolio demonstration using synthetic or licensed data

### Deferred

- Arabic and mixed-language processing
- Broad contract analysis
- Automatic payment approval
- Integrations with production ERP or banking systems
- Claims of regulatory certification

## UAE-specific scope

The initial UAE focus comes from:

- AED currency handling;
- UAE Tax Registration Number fields;
- VAT calculation checks;
- supplier and procurement workflows; and
- eventual Arabic and mixed-language support.

Bilingual processing is not required for the first MVP.

## Success measures

All measurements require a versioned dataset and documented methodology.

- Exact match, precision, and recall by extracted field
- Line-item extraction accuracy
- Reviewer correction rate
- Processing latency p50 and p95
- Processing failure rate
- Discrepancy rule precision and recall
- Retrieval recall and citation correctness
- Answer groundedness
- Processing cost per document
- Review time compared with an explicitly measured baseline

No result becomes a portfolio claim until it has actually been measured.

## Non-goals

The platform is not an accounting system, ERP, legal-advice service, compliance certification, or autonomous payment approver.
