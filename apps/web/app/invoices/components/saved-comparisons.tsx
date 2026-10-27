import Link from "next/link";
import type { Case } from "../workflow.types";

export function SavedComparisons({ item }: { item: Case }) {
  if (item.runs.length === 0) return null;

  return (
    <section>
      <h2>Saved comparisons</h2>
      <p>Each result keeps its original document values, evidence and scope.</p>

      {item.runs.map(run => (
        <article key={run.id}>
          <h3>Comparison {run.runNumber}</h3>
          <p>{run.coverage?.conclusion ?? "Check the recorded findings and coverage limits."}</p>
          <p>Coverage: {run.coverage?.status ?? "Not recorded"} · Rule: {run.ruleSetVersion}</p>
          <Link href={`/reconciliation?case=${item.id}&run=${run.id}`}>
            Inspect comparison {run.runNumber}
          </Link>
        </article>
      ))}
    </section>
  );
}
