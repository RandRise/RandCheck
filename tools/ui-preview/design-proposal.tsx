import { FormEvent, useEffect, useRef, useState } from "react";
import "./design-proposal.css";

type Role = "Reviewer" | "Operator" | "Viewer" | "Administrator";
type Panel = "Details" | "Compare" | "Questions" | "Activity";
type Field = { label: string; value: string; original: string; checked: boolean; attention?: boolean };
type Item = { id: number; name: string; number: string; supplier: string; status: string; fields: Field[]; activity: string[] };
const makeFields = (number: string, supplier: string, total: string): Field[] => [
  { label: "Invoice number", value: number, original: number, checked: false },
  { label: "Supplier", value: supplier, original: supplier, checked: false },
  { label: "Invoice date", value: "26 Sep 2026", original: "26 Sep 2026", checked: false },
  { label: "Currency", value: "AED", original: "AED", checked: false },
  { label: "Total", value: total, original: total, checked: false, attention: true },
];
const initialItems = (): Item[] => [
  { id: 1, name: "Office supplies invoice", number: "INV-2026-041", supplier: "Sample Trading", status: "Needs review", fields: makeFields("INV-2026-041", "Sample Trading", "1,155.00"), activity: ["Document processed. Ready for a person to check."] },
  { id: 2, name: "فاتورة خدمات — Services invoice", number: "INV-2026-042", supplier: "شركة النور", status: "Needs review", fields: makeFields("INV-2026-042", "شركة النور", "2,100.00"), activity: ["Document processed. Arabic text available for review."] },
];
const instructions: Record<Role, { title: string; steps: string[] }> = {
  Reviewer: { title: "Your guide to reviewing", steps: ["Open an item marked Needs review.", "Compare each value with the document. Keep it or correct it with a reason.", "Finish when every value is checked. Compare related documents or open Activity for saved decisions."] },
  Operator: { title: "Your guide to processing", steps: ["Add documents and check their processing status. Uploads need the real application; they are not part of this proposal.", "Open a ready document and check each extracted value against its source.", "Finish the review, then compare related documents. Reprocess only when a new extraction is needed."] },
  Viewer: { title: "Your guide to viewing", steps: ["Open a document to inspect its details and source.", "Review saved activity. Your role cannot change or finish a review.", "Ask a reviewer to prepare question sources. Prepared-source selection is a planned integration, not available in this proposal."] },
  Administrator: { title: "Your guide to administration", steps: ["Use Members and access to check who can use the workspace.", "Use Audit history to understand access and workflow changes.", "Administration does not automatically grant document access. This example account has administration only."] },
};
function preference(key: string, fallback: string) { try { return localStorage.getItem(`design-proposal:${key}`) ?? fallback; } catch { return fallback; } }
function remember(key: string, value: string) { try { localStorage.setItem(`design-proposal:${key}`, value); } catch { /* preferences are optional */ } }

export default function DesignProposal() {
  const [direction, setDirection] = useState(() => preference("direction", "blue") === "neutral" ? "neutral" : "blue");
  const [theme, setTheme] = useState(() => preference("theme", "light") === "dark" ? "dark" : "light");
  const [role, setRole] = useState<Role>("Reviewer");
  const [items, setItems] = useState(initialItems);
  const [selected, setSelected] = useState<number | null>(null);
  const [panel, setPanel] = useState<Panel>("Details");
  const [help, setHelp] = useState(false);
  const [filter, setFilter] = useState("All work");
  const [search, setSearch] = useState("");
  const [editing, setEditing] = useState<string | null>(null);
  const [draft, setDraft] = useState(""); const [reason, setReason] = useState("");
  const [message, setMessage] = useState("");
  const [question, setQuestion] = useState(""); const [answer, setAnswer] = useState("");
  const [comparison, setComparison] = useState(false); const [decision, setDecision] = useState("");
  const [decisionReason, setDecisionReason] = useState(""); const [revising, setRevising] = useState(false);
  const comparisonStates = useRef<Record<number, { comparison: boolean; decision: string; reason: string; revising: boolean }>>({});
  const [caseForm, setCaseForm] = useState(false); const [caseTitle, setCaseTitle] = useState(""); const [category, setCategory] = useState("Uncategorized");
  const [cases, setCases] = useState<{ reference: string; title: string; category: string }[]>([]);
  const [adminPanel, setAdminPanel] = useState("Members and access");
  const casePending = useRef(false);
  const heading = useRef<HTMLHeadingElement>(null);
  const editInput = useRef<HTMLInputElement>(null);
  const correctionButtons = useRef<Record<string, HTMLButtonElement | null>>({});
  const helpClose = useRef<HTMLButtonElement>(null);
  const helpTrigger = useRef<HTMLButtonElement>(null);
  const item = items.find(i => i.id === selected);
  const readOnly = role === "Viewer" || role === "Administrator";
  const canReview = !readOnly && item?.status !== "Completed";
  const remaining = item?.fields.filter(f => !f.checked).length ?? 0;
  useEffect(() => { remember("direction", direction); }, [direction]);
  useEffect(() => { remember("theme", theme); }, [theme]);
  useEffect(() => { if (editing) editInput.current?.focus(); }, [editing]);
  useEffect(() => { if (help) helpClose.current?.focus(); }, [help]);
  function updateItem(change: (item: Item) => Item) { setItems(all => all.map(i => i.id === selected ? change(i) : i)); }
  function openItem(id: number | null) {
    if (editing) { setPanel("Details"); setMessage("Save or cancel your correction before leaving this document."); requestAnimationFrame(() => editInput.current?.focus()); return; }
    if (selected !== null) comparisonStates.current[selected] = { comparison, decision, reason: decisionReason, revising };
    const savedComparison = id === null ? undefined : comparisonStates.current[id];
    setSelected(id); setPanel("Details"); setEditing(null); setMessage(""); setQuestion(""); setAnswer("");
    setComparison(savedComparison?.comparison ?? false); setDecision(savedComparison?.decision ?? ""); setDecisionReason(savedComparison?.reason ?? ""); setRevising(savedComparison?.revising ?? false);
    requestAnimationFrame(() => heading.current?.focus());
  }
  function keepValue(label: string) {
    if (!canReview) return;
    updateItem(i => ({ ...i, status: "In progress", fields: i.fields.map(f => f.label === label ? { ...f, checked: true } : f),
      activity: [...i.activity, `${label} checked against the source.`] }));
    setMessage(`${label} checked.`);
  }
  function saveCorrection(e: FormEvent) {
    e.preventDefault(); if (!editing || !canReview || !draft.trim() || !reason.trim()) return;
    if (draft.trim() === item?.fields.find(f => f.label === editing)?.value) { setMessage("The value is unchanged. Choose Keep this value instead."); return; }
    updateItem(i => ({ ...i, status: "In progress", fields: i.fields.map(f => f.label === editing ? { ...f, value: draft.trim(), checked: true } : f),
      activity: [...i.activity, `${editing} corrected: ${draft.trim()}. Reason: ${reason.trim()}`] }));
    setMessage(`${editing} correction saved in this proposal.`); closeCorrection();
  }
  function closeCorrection() {
    const label = editing;
    setEditing(null);
    requestAnimationFrame(() => { if (label) correctionButtons.current[label]?.focus(); });
  }
  function finish() {
    if (!canReview || remaining || editing) return;
    updateItem(i => ({ ...i, status: "Completed", activity: [...i.activity, "Review finished. This revision is read only."] }));
    setMessage("Review finished. You can return to My work or inspect the saved activity.");
  }
  function createCase(e: FormEvent) {
    e.preventDefault(); if (casePending.current || !caseTitle.trim() || readOnly) return;
    casePending.current = true;
    setCases(all => [...all, { reference: `REC-2026-${String(all.length+123).padStart(6, "0")}`, title: caseTitle.trim(), category }]);
    setCaseForm(false); setCaseTitle(""); setCategory("Uncategorized"); setMessage("Sample case created. The reference is assigned automatically.");
  }
  const filtered = items.filter(i => (filter === "All work" || i.status === filter) && `${i.name} ${i.supplier} ${i.number}`.toLowerCase().includes(search.toLowerCase()));
  const currentGuide = instructions[role];
  return <div className="proposal" data-direction={direction} data-theme={theme}>
    <div className="proposal-lab">
      <div><strong>Design proposal</strong><span>Try the workflow, then compare the appearance. Sample changes reset on refresh.</span></div>
      <div className="proposal-lab-controls">
        <label>Style<select aria-label="Design direction" value={direction} onChange={e => setDirection(e.target.value)}><option value="blue">A · Neutral with blue</option><option value="neutral">B · Soft neutral</option></select></label>
        <label>Appearance<select aria-label="Appearance" value={theme} onChange={e => setTheme(e.target.value)}><option value="light">Light</option><option value="dark">Dark</option></select></label>
        <label>Role<select aria-label="Proposal role" disabled={!!editing} value={role} onChange={e => { setRole(e.target.value as Role); openItem(null); setCaseForm(false); setHelp(false); }}>
          {["Reviewer", "Operator", "Viewer", "Administrator"].map(r => <option key={r}>{r}</option>)}</select></label>
        <a href="/">Back to current app</a>
      </div>
    </div>
    <div className="proposal-frame">
      <aside className="proposal-sidebar">
        <div className="proposal-brand"><span aria-hidden="true">d.</span><strong>Document review</strong></div>
        <div className="proposal-org"><span className="proposal-avatar" aria-hidden="true">ST</span><div><strong>Sample Trading</strong><small>Example workspace</small></div></div>
        <nav aria-label="Workspace">
          {role !== "Administrator" ? <button className="proposal-nav-active" onClick={() => { openItem(null); setCaseForm(false); }}><span aria-hidden="true">▤</span> My work <span className="proposal-count">{items.filter(i => i.status !== "Completed").length}</span></button>
            : ["Members and access", "Audit history", "Retention"].map(name => <button key={name} className={adminPanel === name ? "proposal-nav-active" : ""} onClick={() => setAdminPanel(name)}>{name}</button>)}
        </nav>
        <div className="proposal-sidebar-bottom"><span className="proposal-avatar" aria-hidden="true">R</span><div><strong>Example user</strong><small>{role === "Administrator" ? "Administrator · no document access" : role}</small></div></div>
      </aside>
      <div className="proposal-main">
        <div className="proposal-topbar"><span>{role === "Administrator" ? "Administration" : "My work"}{item ? ` / ${item.number}` : ""}</span><button ref={helpTrigger} className="proposal-btn" onClick={() => setHelp(true)}>Help with this page</button></div>
        <div className={`proposal-content ${help ? "proposal-with-help" : ""}`}>
          <div className="proposal-workspace">
            <div className="proposal-title"><div><h1 ref={heading} tabIndex={-1}>{role === "Administrator" ? adminPanel : item ? item.name : "My work"}</h1>
              <p>{role === "Administrator" ? "Manage the workspace with clear permissions and recorded changes." : item ? "Check the details against the source. Your changes stay with this document." : "A clear place to see what needs your attention."}</p></div>
              {!item && !readOnly && <button className="proposal-btn proposal-primary" onClick={() => { casePending.current = false; setCaseForm(true); }}>New comparison case</button>}
            </div>
            {message && <p className="proposal-notice" role="status">{message}</p>}
            {role === "Administrator" ? <section className="proposal-card">
              <h2>{adminPanel}</h2>
              {adminPanel === "Members and access" ? <><p className="proposal-muted">Example access summary. Changes to real memberships happen in the current application.</p>
                <div className="proposal-table-scroll"><table><thead><tr><th>Person</th><th>Document access</th><th>Administration</th><th>Status</th></tr></thead><tbody>
                  {[["Example user", "None", "Administrator"], ["Mariam · Example", "Reviewer", "None"], ["Omar · Example", "Operator", "None"]].map(row => <tr key={row[0]}>{row.map(v => <td key={v}>{v}</td>)}<td><span className="proposal-status">Active</span></td></tr>)}
                </tbody></table></div></> : adminPanel === "Audit history" ? <><p>Who changed what, when, and why.</p><ol className="proposal-timeline"><li><strong>Example member added</strong><span>Administrator · Today, 09:00 · Successful</span></li><li><strong>Example access changed to Reviewer</strong><span>Administrator · Today, 09:05 · Successful</span></li></ol></>
                : <><p>Retention and preservation holds will appear here.</p><div className="proposal-callout"><strong>No automatic deletion</strong><p>Explain retention and holds before presenting any removal action. This proposal performs no deletion.</p></div></>}
            </section> : caseForm ? <section className="proposal-card proposal-case-form"><h2>New comparison case</h2><p className="proposal-muted">Give it a recognizable title. We’ll assign its reference.</p>
              <form onSubmit={createCase}><div className="proposal-reference"><span>Automatic reference</span><strong>REC-2026-{String(cases.length+123).padStart(6, "0")}</strong><small>Example format · final reference is created on save</small></div>
                <label>Case title<input autoFocus value={caseTitle} onChange={e => setCaseTitle(e.target.value)} placeholder="Sample Trading · Office supplies · September" maxLength={200} required dir="auto"/></label>
                <label>Category <span className="proposal-muted">(optional)</span><select value={category} onChange={e => setCategory(e.target.value)}>{["Uncategorized", "Office supplies", "Services", "Equipment"].map(c => <option key={c}>{c}</option>)}</select></label>
                <p className="proposal-muted">The reference stays the same if you rename or recategorize the case.</p>
                <div className="proposal-actions"><button type="submit" className="proposal-btn proposal-primary" disabled={!caseTitle.trim()}>Create case</button><button type="button" className="proposal-btn" onClick={() => setCaseForm(false)}>Cancel</button></div>
              </form></section> : !item ? <>
              <section className="proposal-welcome"><div className="proposal-welcome-icon" aria-hidden="true">?</div><div><h2>New to this workspace?</h2><p>See the steps for your role, right beside your work.</p></div><button className="proposal-btn" onClick={() => setHelp(true)}>Open my guide</button></section>
              <section className="proposal-card proposal-list-card"><div className="proposal-list-toolbar"><div className="proposal-filters" aria-label="Work status">{["All work", "Needs review", "In progress", "Completed"].map(f => <button className={filter === f ? "selected" : ""} aria-pressed={filter === f} key={f} onClick={() => setFilter(f)}>{f}</button>)}</div><label className="proposal-search"><span className="proposal-sr">Find a document</span><input placeholder="Find a document…" value={search} onChange={e => setSearch(e.target.value)}/></label></div>
                <div className="proposal-table-scroll"><table><thead><tr><th>Document</th><th>Supplier</th><th>Status</th><th><span className="proposal-sr">Action</span></th></tr></thead><tbody>{filtered.map(i => <tr key={i.id}><td><strong dir="auto">{i.name}</strong><small>{i.number} · 26 Sep 2026</small></td><td dir="auto">{i.supplier}</td><td><span className={`proposal-status ${i.status === "Needs review" ? "attention" : ""}`}>{i.status}</span></td><td><button className="proposal-btn" onClick={() => openItem(i.id)}>{readOnly || i.status === "Completed" ? "Open document" : "Review document"}<span className="proposal-sr"> {i.number}</span><span aria-hidden="true"> →</span></button></td></tr>)}</tbody></table></div>
                {!filtered.length && <div className="proposal-empty"><h2>No documents here</h2><p>Try another status or clear your search.</p><button className="proposal-btn" onClick={() => { setSearch(""); setFilter("All work"); }}>Show all work</button></div>}
              </section>
              {!!cases.length && <section className="proposal-card"><h2>Comparison cases</h2><p className="proposal-muted">Created in this proposal only. Case reopening will be connected during implementation.</p>{cases.map(c => <div className="proposal-case-row" key={c.reference}><strong>{c.reference}</strong><span dir="auto">{c.title}</span><span className="proposal-status">{c.category}</span></div>)}</section>}
              <p className="proposal-footnote">Nothing moves forward without a reviewer’s decision.</p>
            </> : <>
              <div className="proposal-item-toolbar"><button className="proposal-btn proposal-quiet" onClick={() => openItem(null)}>← Back to my work</button><span className="proposal-status">{item.status}</span><span className="proposal-muted">{editing ? "Save or cancel the correction before finishing" : `${remaining} of ${item.fields.length} values left to check`}</span>
                {!readOnly && <button className="proposal-btn proposal-primary" disabled={!canReview || remaining > 0 || !!editing} onClick={finish}>{item.status === "Completed" ? "Review finished" : "Finish review"}</button>}</div>
              <div className="proposal-tabs" role="group" aria-label="Document workspace">{(["Details", "Compare", "Questions", "Activity"] as Panel[]).filter(p => !readOnly || p === "Details" || p === "Activity").map(p => <button key={p} aria-pressed={panel === p} onClick={() => { setPanel(p); setMessage(""); }}>{p}</button>)}</div>
              {panel === "Details" && <div className="proposal-review-grid"><section className="proposal-source"><div className="proposal-section-bar"><h2>Source document</h2><span>Page 1 of 1</span></div><div className="proposal-paper"><div className="proposal-paper-heading"><strong dir="auto">{item.supplier}</strong><span>Invoice</span></div><p dir="auto">{item.number}</p><dl><div><dt>Date</dt><dd>26 September 2026</dd></div><div><dt>Bill to</dt><dd>Sample company</dd></div></dl><div className="proposal-paper-line"><strong>Description</strong><strong>Amount (AED)</strong></div><div className="proposal-paper-line"><span>{item.id === 1 ? "Office supplies" : "Consulting services"}</span><span>{item.id === 1 ? "1,100.00" : "2,000.00"}</span></div><div className="proposal-paper-line"><span>VAT</span><span>{item.id === 1 ? "55.00" : "100.00"}</span></div><div className="proposal-paper-total"><strong>Total</strong><strong>{item.fields.find(f => f.label === "Total")?.original} AED</strong></div><p className="proposal-paper-note" dir="auto">{item.id === 1 ? "Payment within 30 days." : "يرجى الدفع خلال 30 يوماً"}</p></div><p className="proposal-source-caption">Illustrative source for the design proposal.</p></section>
                <section className="proposal-fields"><div className="proposal-section-bar"><h2>{readOnly ? "Document details" : "Check the details"}</h2><span>{item.fields.filter(f => f.checked).length}/{item.fields.length} checked</span></div>
                  {!readOnly && remaining > 0 && <p className="proposal-inline-help">Compare each value with the source, then keep or correct it.</p>}
                  {item.fields.map(f => <div className={`proposal-field ${f.attention && !f.checked ? "proposal-needs-check" : ""}`} key={f.label}>
                    <div className="proposal-field-label"><strong>{f.label}</strong><span className={f.checked ? "proposal-checked" : "proposal-muted"}>{f.checked ? "✓ Checked" : f.attention ? "Needs a closer look" : "Not checked"}</span></div>
                    {editing === f.label ? <form onSubmit={saveCorrection}><label>Corrected {f.label.toLowerCase()}<input ref={editInput} dir="auto" value={draft} onChange={e => setDraft(e.target.value)} required/></label><label>Reason for correction<input dir="auto" value={reason} onChange={e => setReason(e.target.value)} required placeholder="What did you find in the source?"/></label><div className="proposal-actions"><button className="proposal-btn proposal-primary" disabled={!draft.trim() || !reason.trim() || draft.trim() === f.value}>Save correction</button><button className="proposal-btn" type="button" onClick={closeCorrection}>Cancel</button></div></form>
                      : <><p className="proposal-field-value" dir="auto">{f.value}</p>{f.value !== f.original && <small className="proposal-muted" dir="auto">Originally extracted: {f.original}</small>}
                        {canReview && <div className="proposal-field-actions"><button className="proposal-btn" disabled={f.checked} onClick={() => keepValue(f.label)}>{f.checked ? "Checked" : "Keep this value"}</button><button className="proposal-btn proposal-quiet" disabled={!!editing} ref={el => { correctionButtons.current[f.label] = el; }} onClick={() => { setEditing(f.label); setDraft(f.value); setReason(""); }}>Correct{f.checked ? " value" : ""}</button></div>}</>}
                  </div>)}
                </section></div>}
              {panel === "Compare" && <section className="proposal-card"><h2>Compare related documents</h2><p className="proposal-muted">Unapproved screen proposal · {item.supplier}. New checks use an invoice and purchase order. The result stays beside your review.</p><div className="proposal-related">{["Purchase order", "Invoice"].map(t => <div key={t}><span className="proposal-file-symbol" aria-hidden="true">▤</span><strong>{t}</strong><small>{t === "Invoice" ? item.number : "Example linked document"}</small><span className="proposal-status">{t === "Invoice" ? item.status : "Review complete"}</span></div>)}</div>
                {item.status !== "Completed" ? <div className="proposal-callout"><strong>Finish this invoice review first</strong><p>A comparison uses the exact values you have checked.</p><button className="proposal-btn" onClick={() => setPanel("Details")}>Return to details</button></div> : !comparison ? <button className="proposal-btn proposal-primary" onClick={() => { setComparison(true); updateItem(i => ({ ...i, activity: [...i.activity, "Example comparison completed."] })); }}>Compare documents</button>
                  : <div className="proposal-callout"><strong>{item.id === 1 ? "Invoice is AED 105.00 higher than the purchase order" : "Invoice is AED 100.00 higher than the purchase order"}</strong><p>Illustrative result from the original sample amounts. Changed values and real rules are not evaluated in this design proposal.</p>
                    {decision && !revising ? <><p><strong>Decision: {decision}</strong> · {decisionReason}</p><button className="proposal-btn" onClick={() => setRevising(true)}>Change decision</button></>
                      : <form onSubmit={e => { e.preventDefault(); if (!decisionReason.trim()) return; setDecision("Accepted difference"); setRevising(false); updateItem(i => ({ ...i, activity: [...i.activity, `Difference accepted. Reason: ${decisionReason}`] })); }}><label>Reason for accepting the difference<input value={decisionReason} onChange={e => setDecisionReason(e.target.value)} placeholder="Explain why this difference is acceptable" required/></label><button className="proposal-btn proposal-primary" disabled={!decisionReason.trim()}>Accept difference</button></form>}
                  </div>}
              </section>}
              {panel === "Questions" && <section className="proposal-card"><h2>Ask about this document</h2><p className="proposal-muted">Source: {item.number} · {item.status === "Completed" ? "Completed review" : "Finish the review to prepare its source"}</p><form onSubmit={e => { e.preventDefault(); if (!question.trim() || item.status !== "Completed") return; setAnswer(/total|amount/i.test(question) ? `The reviewed total is AED ${item.fields.find(f => f.label === "Total")?.value}.` : "This example answers total or amount questions only. Other questions need the real question service."); }}><label>Your question<input value={question} onChange={e => setQuestion(e.target.value)} placeholder="What is the invoice total?"/></label><button className="proposal-btn proposal-primary" disabled={!question.trim() || item.status !== "Completed"}>Ask question</button></form>{answer && <div className="proposal-answer" role="status"><strong>Sample answer</strong><p>{answer}</p><button className="proposal-btn" onClick={() => setPanel("Details")}>View source and reviewed values</button></div>}</section>}
              {panel === "Activity" && <section className="proposal-card"><h2>Document activity</h2><p className="proposal-muted">Sample decisions made during this visit.</p><ol className="proposal-timeline">{item.activity.map((event, index) => <li key={index}><strong dir="auto">{event}</strong><span>Example user · This proposal session</span></li>)}</ol></section>}
            </>}
          </div>
          {help && <aside className="proposal-help" aria-label="Role guide"><div><span className="proposal-status">{role} guide</span><button ref={helpClose} aria-label="Close role guide" className="proposal-btn proposal-quiet" onClick={() => { setHelp(false); helpTrigger.current?.focus(); }}>×</button></div><h2>{currentGuide.title}</h2><p>A few steps to get you started.</p><ol>{currentGuide.steps.map((step, index) => <li key={step}><span>{index+1}</span><p>{step}</p></li>)}</ol><div className="proposal-callout"><strong>You can always reopen this</strong><p>Choose Help with this page. Closing this guide keeps your work in place.</p></div></aside>}
        </div>
      </div>
    </div>
  </div>;
}




