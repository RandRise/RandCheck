import { Fragment } from "react";

// Isolate whole Latin/numeric runs for display. Text and citation boundaries stay
// unchanged, including when a highlight starts or ends inside a date or ID.
export function BidiText({ text, highlightStart = -1, highlightEnd = -1 }: {
  text: string; highlightStart?: number; highlightEnd?: number;
}) {
  function highlighted(start: number, end: number) {
    const from = Math.max(start, highlightStart), to = Math.min(end, highlightEnd);
    if (from >= to) return text.slice(start, end);
    return <>{text.slice(start, from)}<mark>{text.slice(from, to)}</mark>{text.slice(to, end)}</>;
  }
  const parts = [];
  let cursor = 0;
  const run = String.raw`[A-Za-z0-9]+(?:[-_.,/][A-Za-z0-9]+)*(?:\[[0-9]+\])?`;
  // Keep phrases such as "Net 45 days" together inside an Arabic paragraph.
  for (const match of text.matchAll(new RegExp(`${run}(?:[ \\t]+${run})*`, "g"))) {
    const start = match.index, end = start + match[0].length;
    parts.push(<Fragment key={`text-${start}`}>{highlighted(cursor, start)}</Fragment>);
    parts.push(<bdi dir="ltr" key={`run-${start}`}>{highlighted(start, end)}</bdi>);
    cursor = end;
  }
  parts.push(<Fragment key="tail">{highlighted(cursor, text.length)}</Fragment>);
  return <>{parts}</>;
}
