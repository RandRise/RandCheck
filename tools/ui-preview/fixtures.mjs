// Synthetic UI behavior only. Never imported by the application or its API.
import { randomUUID } from "node:crypto";
import { aedCurrencyIssue } from "./aed-currency.mjs";
import { searchPreviewLibrary } from "./library-search.mjs";

export const tenant = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa";
export const organizationId = "11111111-1111-1111-1111-111111111111";
const now = () => new Date().toISOString();
const id = n => `00000000-0000-4000-8000-${String(n).padStart(12, "0")}`;
const clone = value => structuredClone(value);
const caseCategories = ["Uncategorized", "OfficeSupplies", "Services", "Equipment"];
const caseStatuses = ["Draft", "Evaluated", "Closed"];
const validDate = value => /^\d{4}-\d{2}-\d{2}$/.test(value) && Number.isFinite(Date.parse(`${value}T00:00:00Z`)) && new Date(`${value}T00:00:00Z`).toISOString().slice(0, 10) === value;

export function seed() {
  // The operator token key stays stable for the existing preview; it is not a role.
  const members = [
    ["operator", "Demo Staff", "Staff"],
    ["staff-two", "Second Staff account", "Staff"],
    ["staff-three", "Third Staff account", "Staff"],
    ["admin", "Demo Admin", "Admin"],
    ["admin-two", "Second Admin account", "Admin"],
    ["revoked", "Demo disabled account", "Staff"],
  ].map(([key, displayName, role], n) => ({ key, id: id(100+n), actorId: id(200+n),
    objectId: id(300+n), tenantId: tenant, displayName, role,
    isActive: key !== "revoked", version: randomUUID() }));
  const documents = [
    ["Invoice to review.txt", "invoice", "1155.00", false],
    ["Completed quotation.txt", "quotation", "1050.00", true],
    ["Completed purchase order.txt", "purchase_order", "1050.00", true],
    ["Completed invoice.txt", "invoice", "1155.00", true],
    ["فاتورة تجريبية — Arabic invoice.txt", "invoice", "210.50", false],
  ].map(([originalFilename, documentType, total, complete], n) => {
    const documentId = id(n+1), run = id(n+11);
    const fields = [["invoice_number", `DEMO-${n+1}`, "Identifier"], ["supplier", "Sample Trading", "String"],
      ["currency", "AED", "Currency"], ["total", total, "Decimal"],
      ["payment_terms", n === 4 ? "خلال 30 يوماً" : "Net 30", "String"]]
      .map(([fieldKey, value, valueType], f) => ({ fieldKey, occurrenceIndex: 0,
        machine: { id: id(1000+n*10+f), value, valueType, confidence: f === 3 ? .64 : .98,
          evidence: [{ page: 1, quotedText: value }] },
        effective: { isPresent: true, value, valueType, source: complete ? "Review" : "Machine" },
        decisionStatus: complete ? "Confirmed" : "Unreviewed", cues: f === 3 && !complete ? ["LowConfidence"] : [] }));
    return { id: documentId, originalFilename, documentType, sizeBytes: 1024, status: "Ready",
      language: n === 4 ? "ar-en" : "en", extractor: "synthetic-preview", processingMs: 0,
      createdAt: now(), fields: [], review: { documentId, selectedExtractionRunId: run, extractionRunId: run,
        session: complete ? { id: id(n+21), revisionNumber: 1, status: "Completed", concurrencyToken: randomUUID() } : null,
        fields }, actions: [] };
  });
  return { documents, members, events: [], cases: [], caseRequests: {}, draftRequests: {}, draftVersions: {}, results: {}, questionRequests: {}, indexes: {}, scenario: "normal",
    policy: { version: randomUUID(), documentDays: null, quarantineHours: null }, holds: [] };
}

export function createFixtureApi(origin) {
  let state = seed();
  const event = (actor, action, targetId, beforeState = null, afterState = null) => state.events.unshift({
    id: randomUUID(), actorId: actor.actorId, actorKind: "Human", action: `Preview.${action}`,
    targetType: action.startsWith("Membership") ? "OrganizationMembership" : "Workflow",
    targetId, outcome: "Succeeded", reasonCode: "synthetic_preview", requestId: randomUUID(),
    beforeState: beforeState && JSON.stringify(beforeState), afterState: afterState && JSON.stringify(afterState), recordedAtUtc: now() });
  const source = doc => ({ documentId: doc.id, filename: doc.originalFilename,
    reviewSessionId: doc.review.session.id, extractionRunId: doc.review.extractionRunId, revisionNumber: 1 });
  const index = doc => {
    const existing = Object.values(state.indexes).find(x => x.documentId === doc.id);
    if (existing) return existing;
    const next = { ...source(doc), indexId: randomUUID() }; state.indexes[next.indexId] = next; return next;
  };
  return async function fixture(req, res, next = () => {}) {
    const url = new URL(req.url, origin), path = url.pathname, method = req.method;
    if (!path.startsWith("/api/") && !path.startsWith("/__preview/")) return next();
    const send = (value, status = 200, etag) => {
      res.writeHead(status, { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store",
        ...(etag ? { ETag: `"${etag}"` } : {}) }); res.end(JSON.stringify(value));
    };
    const fail = (detail, status = 400, code = "preview_error") => send({ detail, code }, status);
    const write = method !== "GET";
    if (req.headers.host !== new URL(origin).host) return fail("Preview host required", 403);
    if (write && req.headers.origin !== origin) return fail("Preview origin required", 403);
    let body = {};
    if (write) {
      if (req.headers["content-type"]?.startsWith("multipart/")) {
        req.resume(); return fail("File upload and OCR require the real environment. Use the sample documents in this preview.", 501);
      }
      try {
        let raw = "";
        for await (const chunk of req) { raw += chunk; if (Buffer.byteLength(raw) > 65536) { req.resume(); return fail("Preview request too large", 413); } }
        body = JSON.parse(raw || "{}");
        if (!body || Array.isArray(body) || typeof body !== "object") return fail("Expected a JSON object");
      } catch { return fail("Invalid preview JSON"); }
    }
    if (path === "/__preview/reset" && method === "POST") { state = seed(); return send({ reset: true }); }
    if (path === "/__preview/scenario" && method === "POST") {
      if (!["normal", "empty", "unavailable"].includes(body.scenario)) return fail("Unknown scenario");
      state.scenario = body.scenario; return send({ scenario: state.scenario });
    }
    if (path === "/__preview/scenario" && method === "GET") return send({ scenario: state.scenario });
    if (path.startsWith("/__preview/")) return fail("Unknown preview control", 404);
    const actor = state.members.find(m => req.headers.authorization === `Bearer preview-${m.key}`);
    if (!actor?.isActive) return fail("Simulated account is signed out or revoked", 403);
    if (path === "/api/session" && method === "GET") return send({ organizationId, organizationName: "Sample organization · Preview",
      actorId: actor.actorId, displayName: actor.displayName, tenantId: tenant, role: actor.role });
    const admin = path.startsWith("/api/organization/");
    if (!["Staff", "Admin"].includes(actor.role) || (admin && actor.role !== "Admin"))
      return fail("Simulated role does not allow this action", 403);
    if (state.scenario === "unavailable" && !admin) return fail("Simulated service unavailable. Change the preview scenario to recover.", 503);
    const documents = state.scenario === "empty" ? [] : state.documents;
    if (path === "/api/documents/library" && method === "GET") {
      try { return send(searchPreviewLibrary(documents, url.searchParams)); }
      catch { return fail("Invalid library search."); }
    }
    if (path === "/api/documents/work-list" && method === "GET") return send(documents.map(({ review, actions, fields, ...doc }) => ({
      ...doc, reviewState: review.session?.status === "Completed" ? "Reviewed" : review.session ? "InProgress" : "NeedsReview",
      selectedExtractionRunId: review.selectedExtractionRunId, reviewExtractionRunId: review.extractionRunId,
      latestCompletedReviewId: review.session?.status === "Completed" ? review.session.id : null,
      reviewUsesOlderExtraction: review.extractionRunId !== review.selectedExtractionRunId, latestExtractionStatus: "Ready"
    })));
    if (path === "/api/documents" && method === "GET") return send(documents.map(({ review, actions, ...doc }) => doc));
    const parts = path.split("/").filter(Boolean);
    if (parts[1] === "documents") {
      const doc = documents.find(d => d.id === parts[2]);
      if (!doc) return fail("Preview document not found", 404);
      const review = doc.review;
      if (parts.length === 3 && method === "GET") { const { review: unusedReview, actions, ...summary } = doc; return send(summary); }
      const respond = () => send(review, 200, review.session?.concurrencyToken);
      if (parts[3] === "review" && method === "GET") return respond();
      if (parts[3] === "review-history" && method === "GET") return send({ preview: true, actions: doc.actions });
      if (parts[3] === "processing" && method === "GET") return send({ status: "Ready", attemptCount: 1 });
      if (parts[3] === "source-access" && method === "POST") return send({ url: `/api/documents/${doc.id}/source`, accessToken: "synthetic-preview-source" });
      if (parts[3] === "source" && method === "GET") {
        if (req.headers["x-document-access"] !== "synthetic-preview-source") return fail("Preview source grant required", 403);
        res.writeHead(200, { "Content-Type": "text/plain; charset=utf-8", "Cache-Control": "no-store",
          "Content-Disposition": `attachment; filename="source-${doc.id.replaceAll("-", "")}.txt"` });
        return res.end("Synthetic preview source — not an uploaded file\n" + review.fields.filter(f => f.machine).map(f => `${f.fieldKey}: ${f.machine.value}`).join("\n"));
      }
      if (parts[3] === "reviews" && method === "POST") {
        if (parts.length === 4) {
          if (review.session?.status === "Completed") return fail("This extraction already has a completed review", 409);
          review.session ??= { id: randomUUID(), revisionNumber: 1, status: "InProgress", concurrencyToken: randomUUID() };
          return respond();
        }
        if (!review.session || parts[4] !== review.session.id) return fail("Preview review not found", 404);
        if (req.headers["if-match"] !== `"${review.session.concurrencyToken}"`) return fail("Preview changed. Reload before saving.", 412);
        if (review.session.status !== "InProgress") return fail("Completed preview review is read only", 409);
        if (parts[5] === "complete") {
          if (review.fields.some(f => f.machine && f.decisionStatus === "Unreviewed")) return fail("Review all fields first", 409);
          review.session.status = "Completed"; event(actor, "Review.Completed", doc.id);
        } else if (parts[5] === "actions") {
          if (!["Added", "Confirmed", "Corrected", "Cleared"].includes(body.action)) return fail("Unknown review action");
          const reason = body.reason?.trim() || null;
          const hasValue = body.value != null || body.valueType != null;
          if (body.action !== "Confirmed" && !reason || reason?.length > 500) return fail("A reason up to 500 characters is required");
          if (["Added", "Corrected"].includes(body.action) && (!body.value?.trim() || !["String", "Decimal", "Date", "Currency", "Identifier"].includes(body.valueType)))
            return fail("A value and supported type are required");
          if (["Confirmed", "Cleared"].includes(body.action) && hasValue) return fail("This action cannot carry a value");
          const fieldKey = body.fieldKey?.trim();
          let field = review.fields.find(f => body.machineFieldValueId ? f.machine?.id === body.machineFieldValueId : f.fieldKey === fieldKey && f.occurrenceIndex === body.occurrenceIndex);
          if (["Confirmed", "Corrected"].includes(body.action) && !body.machineFieldValueId || body.machineFieldValueId && !field?.machine)
            return fail("This action requires the selected machine field");
          if (!body.machineFieldValueId && field?.machine) return fail("Use the machine field identifier");
          if (body.action === "Added") {
            if (body.machineFieldValueId || !body.fieldKey?.trim() || body.fieldKey.trim().length > 100 || !Number.isInteger(body.occurrenceIndex) || body.occurrenceIndex < 0)
              return fail("Choose a reviewer field and value");
            if (!field) { field = { fieldKey: body.fieldKey.trim(), occurrenceIndex: body.occurrenceIndex, cues: [] }; review.fields.push(field); }
          }
          if (!field || (body.action === "Corrected" && !body.value?.trim())) return fail("Choose a field and value");
          const latest = field.latestAction;
          if (latest && latest.action === body.action && (latest.valueType ?? null) === (body.valueType ?? null) &&
              (latest.value ?? null) === (body.value ?? null) && (latest.reason ?? null) === reason) return respond();
          if (body.action === "Cleared" && !body.machineFieldValueId && latest?.action !== "Added") return fail("Only an active added field can be cleared");
          const before = clone(field.effective);
          field.decisionStatus = body.action;
          field.effective = { isPresent: body.action !== "Cleared", value: body.action === "Confirmed" ? field.machine?.value : body.action === "Cleared" ? undefined : body.value,
            valueType: body.valueType ?? field.machine?.valueType ?? "String", source: "Review" };
          field.latestAction = { ...body, reason, id: randomUUID(), sequenceNumber: doc.actions.length+1 };
          doc.actions.push(field.latestAction); event(actor, `Review.${body.action}`, doc.id, before, field.effective);
        } else return fail("This review action is not simulated", 501);
        review.session.concurrencyToken = randomUUID(); return respond();
      }
    }
    if (path === "/api/reconciliation-cases" && method === "GET") {
      const offset = Number(url.searchParams.get("offset") ?? 0);
      if (!Number.isInteger(offset) || offset < 0 || offset > 10000) return fail("Offset must be between 0 and 10000");
      const q = (url.searchParams.get("q") ?? "").trim(), category = url.searchParams.get("category"), status = url.searchParams.get("status");
      const from = url.searchParams.get("createdFrom"), to = url.searchParams.get("createdTo");
      const workflowKind = url.searchParams.get("workflowKind");
      if (workflowKind && !["Legacy", "InvoicePurchaseOrder"].includes(workflowKind)) return fail("Invalid workflow kind");
      if (q.length > 200 || category && !caseCategories.includes(category) || status && !caseStatuses.includes(status) ||
          from && !validDate(from) || to && !validDate(to) || from && to && from > to) return fail("Invalid case filters");
      const ordered = state.cases.filter(item => (!q || item.reference.toLowerCase().includes(q.toLowerCase()) || item.name.toLowerCase().includes(q.toLowerCase())) &&
        (!category || item.category === category) && (!status || item.status === status) &&
        (!from || item.createdAt.slice(0, 10) >= from) && (!to || item.createdAt.slice(0, 10) <= to) && (!workflowKind || (item.workflowKind ?? "Legacy") === workflowKind))
        .sort((a, b) => (b.createdAt ?? "").localeCompare(a.createdAt ?? "") || b.id.localeCompare(a.id));
      return send({ cases: ordered.slice(offset, offset + 50).map(item => ({ id: item.id, name: item.name, reference: item.reference, category: item.category, status: item.status,
        createdAt: item.createdAt, updatedAt: item.updatedAt, workflowKind: item.workflowKind ?? "Legacy", documentCount: item.documents.length, runCount: item.runs.length })),
        nextOffset: offset + 50 < ordered.length && offset + 50 <= 10000 ? offset + 50 : null });
    }
    if (path === "/api/reconciliation-cases" && method === "POST") {
      const category = body.category ?? "Uncategorized";
      const workflowKind = body.workflowKind ?? "Legacy";
      if (!["Legacy", "InvoicePurchaseOrder"].includes(workflowKind)) return fail("Invalid workflow kind");
      if (typeof body.name !== "string" || !body.name.trim() || body.name.trim().length > 200 || !caseCategories.includes(category)) return fail("A case name up to 200 characters and a valid category are required");
      if (body.requestKey != null && (typeof body.requestKey !== "string" || !body.requestKey.trim() || body.requestKey.length > 100 || body.requestKey !== body.requestKey.trim())) return fail("Use a request key up to 100 characters without surrounding spaces");
      const scope = body.requestKey == null ? null : JSON.stringify([actor.actorId, body.requestKey]);
      const fingerprint = JSON.stringify(workflowKind === "Legacy" ? [body.name.trim(), category] : [body.name.trim(), category, workflowKind]);
      const previous = scope && state.caseRequests[scope];
      if (previous) {
        if (previous.fingerprint !== fingerprint) return fail("The request key belongs to different case inputs.", 409, "request_key_reused");
        const saved = state.cases.find(item => item.id === previous.caseId);
        return saved ? send(saved, 201, saved.concurrencyToken) : fail("Preview case not found", 404);
      }
      const createdAt = now(), year = createdAt.slice(0, 4);
      const sequence = state.cases.filter(item => item.createdAt.startsWith(`${year}-`)).length + 1;
      const item = { id: randomUUID(), name: body.name.trim(), reference: `REC-${year}-${String(sequence).padStart(6, "0")}`, category, status: "Draft", createdAt, updatedAt: createdAt, concurrencyToken: randomUUID(), documents: [], runs: [] };
      if (workflowKind === "InvoicePurchaseOrder") { item.workflowKind = workflowKind; item.draftDocuments = []; }
      state.cases.push(item); if (scope) state.caseRequests[scope] = { fingerprint, caseId: item.id };
      event(actor, "Case.Created", item.id); return send(item, 201, item.concurrencyToken);
    }
    if (parts[1] === "reconciliation-cases" && parts[2]) {
      const item = state.cases.find(c => c.id === parts[2]);
      if (!item) return fail("Preview case not found", 404);
      if (method === "GET") return send(item, 200, item.concurrencyToken);
      if (parts[3] === "draft-documents" && method === "PUT") {
        const role = parts[4], documentId = body.documentId ?? null;
        if (parts.length !== 5 || !["Invoice", "PurchaseOrder"].includes(role) || item.workflowKind !== "InvoicePurchaseOrder") return fail("Choose a role in an invoice check");
        if (item.status === "Closed") return fail("A closed invoice check cannot change", 409);
        if (typeof body.requestKey !== "string" || !body.requestKey.trim() || body.requestKey !== body.requestKey.trim() || body.requestKey.length > 100) return fail("A valid request key is required");
        if (!req.headers["if-match"]) return fail("If-Match is required", 428);
        if (documentId !== null && !documents.some(doc => doc.id === documentId)) return fail("Preview document not found", 404);
        if (documentId && item.draftDocuments.some(source => source.role !== role && source.documentId === documentId)) return fail("Choose different documents for invoice and purchase order");
        const requestScope = JSON.stringify([actor.actorId, body.requestKey]);
        const fingerprint = JSON.stringify([item.id, role, documentId, req.headers["if-match"]]);
        const previous = state.draftRequests[requestScope], versionScope = JSON.stringify([item.id, role]);
        if (previous) {
          if (previous.fingerprint !== fingerprint) return fail("The request key belongs to another selection", 409);
          if (previous.version !== (state.draftVersions[versionScope] ?? null)) return fail("The saved selection was superseded", 409);
          return send(item, 200, item.concurrencyToken);
        }
        if (req.headers["if-match"] !== `"${item.concurrencyToken}"`) return fail("Preview case changed", 412);
        const existing = item.draftDocuments.find(source => source.role === role);
        const selectionChanged = (existing?.documentId ?? null) !== documentId;
        if (selectionChanged) {
          item.draftDocuments = item.draftDocuments.filter(source => source.role !== role);
          if (documentId) item.draftDocuments.push({ role, documentId });
          item.documents = item.documents.filter(source => source.role !== role);
          state.draftVersions[versionScope] = randomUUID();
        }
        item.concurrencyToken = randomUUID(); item.updatedAt = now();
        event(actor, "Case.DraftSelectionSaved", item.id, null, { role, selected: documentId !== null, selectionChanged });
        state.draftRequests[requestScope] = { fingerprint, version: state.draftVersions[versionScope] ?? null };
        return send(item, 200, item.concurrencyToken);
      }
      if (parts.length === 3 && method === "PUT" && !req.headers["if-match"]) return fail("If-Match is required", 428);
      if (req.headers["if-match"] !== `"${item.concurrencyToken}"`) return fail("Preview case changed", 412);
      if (item.status === "Closed") return fail("A closed reconciliation case cannot change", 409);
      if (parts.length === 3 && method === "PUT") {
        if (typeof body.name !== "string" || !body.name.trim() || body.name.trim().length > 200 || !caseCategories.includes(body.category)) return fail("A case name up to 200 characters and a valid category are required");
        if (item.name === body.name.trim() && item.category === body.category) return send(item, 200, item.concurrencyToken);
        const titleChanged = item.name !== body.name.trim();
        const before = { status: item.status, category: item.category };
        item.name = body.name.trim(); item.category = body.category;
        item.concurrencyToken = randomUUID(); item.updatedAt = now();
        event(actor, "Case.MetadataChanged", item.id, before, { status: item.status, category: item.category, titleChanged });
        return send(item, 200, item.concurrencyToken);
      } else if (parts[3] === "documents" && method === "PUT") {
        if (item.workflowKind === "InvoicePurchaseOrder" && (!["Invoice", "PurchaseOrder"].includes(parts[4]) || !item.draftDocuments.some(source => source.role === parts[4] && source.documentId === body.documentId))) return fail("Choose the saved draft document before its reviewed revision");
        const doc = documents.find(d => d.id === body.documentId && d.review.session?.id === body.reviewSessionId && d.review.session.status === "Completed");
        if (!doc || !["Quotation", "PurchaseOrder", "Invoice"].includes(parts[4])) return fail("Choose a completed preview review");
        const types = { Quotation: ["quotation", "quote"], PurchaseOrder: ["purchase_order", "po"], Invoice: ["invoice"] };
        if (!types[parts[4]].includes(doc.documentType)) return fail("The reviewed document type does not match this source role");
        const previous = item.documents.find(link => link.role === parts[4]);
        if (previous?.documentId === doc.id && previous.reviewSessionId === body.reviewSessionId) return send(item, 200, item.concurrencyToken);
        item.documents = item.documents.filter(d => d.role !== parts[4]);
        item.documents.push({ id: randomUUID(), role: parts[4], documentId: doc.id, reviewSessionId: body.reviewSessionId, extractionRunId: doc.review.extractionRunId });
      } else if (parts[3] === "evaluations" && method === "POST") {
        if (item.workflowKind === "InvoicePurchaseOrder" && (body.ruleSetVersion !== "invoice-po-1.0" || !["Unknown", "FullOrder", "Partial"].includes(body.orderScope) || !["Invoice", "PurchaseOrder"].every(role => item.draftDocuments.some(source => source.role === role && item.documents.some(pin => pin.role === role && pin.documentId === source.documentId))))) return fail("Choose both saved reviewed revisions and an invoice scope");
        if (!["PurchaseOrder", "Invoice"].every(role => item.documents.some(d => d.role === role))) return fail("This preview comparison needs a purchase order and an invoice");
        if (item.workflowKind === "InvoicePurchaseOrder") {
          for (const role of ["Invoice", "PurchaseOrder"]) {
            const pin = item.documents.find(link => link.role === role);
            const review = documents.find(doc => doc.id === pin.documentId).review;
            const issue = aedCurrencyIssue(review, role === "Invoice" ? "invoice" : "purchase order");
            if (issue) return fail(issue.detail, 400, issue.code);
          }
        }
        const total = role => documents.find(d => d.id === item.documents.find(link => link.role === role).documentId).review.fields.find(f => f.fieldKey === "total").effective.value;
        const left = total("PurchaseOrder"), right = total("Invoice"), difference = Math.abs(Number(right)-Number(left));
        const tolerance = Number(body.documentTotalTolerance ?? .01);
        const leftId = randomUUID(), rightId = randomUUID();
        item.runs.unshift({ id: randomUUID(), runNumber: item.runs.length+1, status: "Completed", ruleSetVersion: "synthetic-total-only",
          fields: [{ id: leftId, key: "total", occurrence: 0, value: left, page: 1 }, { id: rightId, key: "total", occurrence: 0, value: right, page: 1 }],
          findings: difference <= tolerance ? [] : [{ id: randomUUID(), ruleCode: "Preview.TotalMismatch", severity: "Warning", leftRole: "PurchaseOrder", rightRole: "Invoice",
            leftFieldSnapshotId: leftId, rightFieldSnapshotId: rightId, leftValue: left, rightValue: right, difference, tolerance,
            explanation: `Synthetic example: invoice and purchase order totals differ by AED ${difference.toFixed(2)}.`, resolutions: [] }] });
        item.status = "Evaluated";
        if (item.workflowKind === "InvoicePurchaseOrder") {
          const run = item.runs[0]; run.orderScope = body.orderScope;
          run.sources = clone(item.documents);
          run.coverage = { status: "NotRecorded", couldNotCheck: 0, conclusion: "Synthetic total-only preview. Product check coverage is not computed here." };
          if (body.orderScope !== "FullOrder") run.findings = [{ id: randomUUID(), ruleCode: "Preview.ScopeUnavailable", severity: "Warning", explanation: "Synthetic example: quantity and total checks are unavailable for uncertain or partial scope.", resolutions: [] }];
        }
      } else if (parts[3] === "runs" && parts[5] === "findings" && parts[7] === "resolutions" && method === "POST") {
        const finding = item.runs.find(r => r.id === parts[4])?.findings.find(f => f.id === parts[6]);
        if (!finding || !body.reason?.trim() || !["Resolved", "Accepted", "Dismissed"].includes(body.action)) return fail("Finding, action and reason required");
        const previous = finding.resolutions.at(-1);
        if (previous?.action === body.action && previous.reason === body.reason.trim()) return send(item, 200, item.concurrencyToken);
        finding.resolutions.push({ ...body, reason: body.reason.trim(), actorId: actor.actorId, id: randomUUID(), sequenceNumber: finding.resolutions.length+1, createdAt: now() });
      } else return fail("This comparison action is not simulated", 501);
      item.concurrencyToken = randomUUID(); item.updatedAt = now(); event(actor, "Case.Changed", item.id); return send(item, 200, item.concurrencyToken);
    }
    if (path === "/api/cited-sources" && method === "GET") return send({ sources: documents.filter(d => d.review.session?.status === "Completed").map(source), nextOffset: null });
    if (parts[1] === "cited-sources" && parts[3] === "reviews" && method === "GET") {
      const doc = documents.find(d => d.id === parts[2]?.toLowerCase() && d.review.session?.id === parts[4]?.toLowerCase() && d.review.session.status === "Completed");
      return doc ? send(source(doc)) : fail("Completed preview review not found", 404);
    }
    if (path === "/api/cited-indexes" && method === "POST") {
      const doc = documents.find(d => d.id === body.documentId && d.review.session?.id === body.reviewSessionId && d.review.session.status === "Completed");
      if (!doc) return fail("Completed preview review required");
      return send({ id: index(doc).indexId, status: "Ready" });
    }
    if (parts[1] === "cited-cases" && parts[3] === "sources" && method === "POST") {
      const item = state.cases.find(c => c.id === parts[2]); if (!item) return fail("Preview case not found", 404);
      return send({ id: item.id, name: item.name, version: item.concurrencyToken,
        sources: item.documents.map(link => index(documents.find(d => d.id === link.documentId))) });
    }
    if (parts[1] === "cited-questions") {
      if (parts.length === 2 && method === "GET") return send({ questions: Object.entries(state.results)
        .filter(([, saved]) => saved.actorId === actor.actorId && saved.result.indexIds.every(key => state.indexes[key]))
        .sort(([leftId, left], [rightId, right]) => right.createdAt.localeCompare(left.createdAt) || leftId.localeCompare(rightId))
        .slice(0, 20).map(([id, saved]) => ({ id, question: saved.result.question, createdAt: saved.createdAt,
          sourceCount: saved.result.indexIds.length, mode: saved.result.reviewedAnswer ? "reviewed" : "evidence" })) });
      if (method === "POST") {
        if (parts[2] === "semantic") return fail("Semantic retrieval is not simulated", 422);
        if (!body.question?.trim() || !Array.isArray(body.indexIds) || !body.indexIds.length || body.indexIds.some(x => !state.indexes[x])) return fail("Choose preview sources and a question");
        if (typeof body.requestKey !== "string" || !body.requestKey.trim() || body.requestKey.length > 100 || body.requestKey !== body.requestKey.trim())
          return fail("A bounded request key is required");
        const reviewed = parts[2] === "reviewed", supported = /total|amount|إجمالي/i.test(body.question);
        const requestScope = JSON.stringify([actor.actorId, body.requestKey]);
        const fingerprint = JSON.stringify([reviewed, body.question.trim(), [...body.indexIds].sort(), reviewed ? null : body.topK ?? 5]);
        const previous = state.questionRequests[requestScope];
        if (previous) {
          if (previous.fingerprint !== fingerprint) return fail("The request key belongs to different inputs.", 409);
          const saved = state.results[previous.resultId];
          if (!saved || saved.actorId !== actor.actorId) return fail("Preview result not found", 404);
          return send({ ...saved.result, retrieval: { ...saved.result.retrieval, replayed: true } });
        }
        const hits = supported ? body.indexIds.map((key, n) => {
          const src = state.indexes[key], doc = documents.find(d => d.id === src.documentId), field = doc.review.fields.find(f => f.fieldKey === "total");
          return { ...src, hitId: randomUUID(), chunkId: randomUUID(), rank: n+1, kind: reviewed ? "ReviewedFact" : "PageText", reviewActionId: null,
            content: `total[0]: ${field.effective.value} AED`, spans: [{ id: randomUUID(), pageNumber: 1, quote: `Total: ${field.machine.value} AED` }] };
        }) : [];
        const result = { question: body.question.trim(), indexIds: body.indexIds, stage: reviewed ? "ReviewedAnswer" : hits.length ? "EvidenceReady" : "InsufficientEvidence", answerGenerationEnabled: false,
          retrieval: { id: randomUUID(), status: reviewed || !hits.length ? "Completed" : "Retrieved", policyVersion: "synthetic-preview-total-only", replayed: false, hits, failureCode: null },
          answer: !reviewed && !hits.length ? { status: "InsufficientEvidence", reasonCode: "no_relevant_evidence", message: "This preview supports total/amount questions only. No search or AI service was called." } : null,
          reviewedAnswer: reviewed ? { status: hits.length ? "Answered" : "InsufficientEvidence", reasonCode: hits.length ? null : "unsupported_scope", missingParts: [],
            claims: hits.map(h => ({ text: `Synthetic preview — ${h.filename}: ${h.content}`, citations: [{ hitId: h.hitId, spanId: h.spans[0].id }] })) } : null };
        state.results[result.retrieval.id] = { actorId: actor.actorId, createdAt: now(), result };
        state.questionRequests[requestScope] = { fingerprint, resultId: result.retrieval.id };
        return send(result);
      }
      const saved = state.results[parts[2]];
      if (!saved || saved.actorId !== actor.actorId) return fail("Preview result not found", 404);
      const result = saved.result;
      if (parts.length === 3 && method === "GET") return send({ ...result, retrieval: { ...result.retrieval, replayed: true } });
      const hit = result.retrieval.hits.find(h => h.hitId === parts[4]);
      if (parts[3] === "hits" && parts[5] === "evidence" && hit && method === "GET") return send({ ...hit, retrievalRunId: result.retrieval.id,
        reviewedFact: hit.kind === "ReviewedFact" ? hit.content : null, review: null,
        page: { pageNumber: 1, byteStart: 0, byteEnd: Buffer.byteLength(hit.spans[0].quote), before: "", quote: hit.spans[0].quote, after: "\nSynthetic preview document." } });
    }
    if (path === "/api/organization/memberships" && method === "GET") return send(state.members);
    if (path === "/api/organization/memberships" && method === "POST") {
      if (!["Staff", "Admin"].includes(body.role) || "canManageMembers" in body) return fail("Choose Staff or Admin");
      if (!body.displayName?.trim() || !body.objectId?.trim()) return fail("Name and object ID required");
      if (state.members.some(m => m.objectId === body.objectId)) return fail("Already exists", 409, "membership_already_exists");
      const member = { ...body, id: randomUUID(), actorId: randomUUID(), version: randomUUID(), isActive: true };
      state.members.push(member); event(actor, "Membership.Added", member.id, null, member); return send(member, 201);
    }
    if (parts[2] === "memberships" && method === "PUT") {
      if (!["Staff", "Admin"].includes(body.role) || typeof body.isActive !== "boolean" || "canManageMembers" in body) return fail("Invalid membership change");
      const member = state.members.find(m => m.id === parts[3]); if (!member) return fail("Member not found", 404);
      if (member.version !== body.expectedVersion) return fail("Membership changed", 409, "membership_version_conflict");
      if (member.role === "Admin" && member.isActive && (body.role !== "Admin" || !body.isActive) && state.members.filter(m => m.isActive && m.role === "Admin").length <= 1)
        return fail("Keep an administrator", 409, "last_administrator_required");
      const before = clone(member); Object.assign(member, { role: body.role, isActive: body.isActive, version: randomUUID() });
      event(actor, "Membership.Changed", member.id, before, member); return send(member);
    }
    if (path === "/api/organization/audit" && method === "GET") {
      const offset = Math.max(0, Number(url.searchParams.get("offset")) || 0); return send(state.events.slice(offset, offset+100));
    }
    if (path === "/api/organization/retention") {
      if (method === "GET") return send(state.policy);
      if (method === "PUT") {
        if (body.expectedVersion !== state.policy.version) return fail("Policy changed", 409);
        state.policy = { version: randomUUID(), documentDays: body.documentDays, quarantineHours: body.quarantineHours };
        event(actor, "Retention.Changed", organizationId); return send(state.policy);
      }
    }
    if (path === "/api/organization/retention/holds") {
      if (method === "GET") return send(state.holds);
      if (method === "POST") {
        const hold = { ...body, id: randomUUID(), version: randomUUID(), createdAtUtc: now(), releasedAtUtc: null };
        state.holds.push(hold); event(actor, "Hold.Placed", hold.id); return send(hold);
      }
    }
    if (parts[3] === "holds" && parts[5] === "release" && method === "POST") {
      const hold = state.holds.find(h => h.id === parts[4]);
      if (!hold || hold.version !== body.expectedVersion) return fail("Hold changed", 409);
      hold.releasedAtUtc = now(); hold.version = randomUUID(); event(actor, "Hold.Released", hold.id); return send(hold);
    }
    if (path === "/api/organization/retention/preview" && method === "GET") return send({ documentDeletionEnabled: false, nextOffset: null,
      documents: documents.map(d => ({ documentId: d.id, createdAtUtc: d.createdAt, disposition: state.holds.some(h => !h.releasedAtUtc && (!h.documentId || h.documentId === d.id)) ? "Held" : "Retained" })) });
    if (path === "/api/organization/quarantine/cleanup" && method === "POST") return send({ executed: false, eligible: 0, deleted: 0, preserved: 0, truncated: false });
    return fail("This operation is not simulated. Use the real integration environment to test it.", 501);
  };
}
