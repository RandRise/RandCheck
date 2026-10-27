"use client";

import { useEffect, useId, useRef, useState } from "react";
import type { Session } from "./enterprise-provider";

import { guideFor, guideLabels, roleGuide, type GuideLanguage } from "./workspace-guide-content";

export function WorkspaceGuide({ session, path }: { session: Session; path: string }) {
  const [open, setOpen] = useState(false);
  const [language, setLanguage] = useState<GuideLanguage>("en");
  const trigger = useRef<HTMLButtonElement>(null);
  const closeButton = useRef<HTMLButtonElement>(null);
  const id = useId();
  const guide = guideFor(session, path);
  const labels = guideLabels[language];
  const role = roleGuide(session, language);
  useEffect(() => { if (open) closeButton.current?.focus(); }, [open]);
  function close() { setOpen(false); trigger.current?.focus(); }
  return <div className="workspace-help-controls">
    <button ref={trigger} className="security-button secondary" aria-expanded={open} aria-controls={open ? id : undefined} onClick={() => open ? close() : setOpen(true)}>Help with this page</button>
    {open && <section id={id} className="workspace-guide" role="region" aria-label="Role guide" onKeyDown={event => { if (event.key === "Escape") { event.stopPropagation(); close(); } }}>
      <div className="workspace-guide-language" role="group" aria-label="Guide language / لغة الدليل">
        <button type="button" lang="en" className="security-button secondary" aria-pressed={language === "en"} onClick={() => setLanguage("en")}>English</button>
        <button type="button" lang="ar" className="security-button secondary" aria-pressed={language === "ar"} onClick={() => setLanguage("ar")}>العربية</button>
      </div>
      <div className="workspace-guide-content" lang={language} dir={language === "ar" ? "rtl" : "ltr"}>
        <div className="workspace-guide-heading"><div><p>{role.heading}</p><h2>{guide.title[language]}</h2></div><button ref={closeButton} type="button" className="security-button secondary" onClick={close}>{labels.close}</button></div>
        <h3>{labels.responsibility}</h3><p>{role.responsibility}</p>
        <figure className="workspace-guide-flow" aria-label={labels.diagram}>
          <figcaption>{labels.diagram}</figcaption>
          <ol>{guide.steps.map((step, index) => <li key={index}>
            <span className="workspace-guide-node"><span className="workspace-guide-number" aria-hidden="true">{index + 1}</span><span>{step.label[language]}</span></span>
            {index < guide.steps.length - 1 && <span className="workspace-guide-arrow" aria-hidden="true">{language === "ar" ? "←" : "→"}</span>}
          </li>)}</ol>
        </figure>
        <h3>{labels.steps}</h3>
        <ol className="workspace-guide-steps">{guide.steps.map((step, index) => <li key={index}><strong>{step.label[language]}</strong><p>{step.detail[language]}</p></li>)}</ol>
        <div className="workspace-guide-access"><h3>{labels.limits}</h3><p>{role.limits}</p></div>
      </div>
    </section>}
  </div>;
}

