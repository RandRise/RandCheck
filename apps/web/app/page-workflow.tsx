import "./page-workflow.css";

type Step = { title: string; description: string };

/** A reading guide, not completion status; opening a page never verifies its data. */
export function PageWorkflow({ label, steps }: { label: string; steps: readonly Step[] }) {
  return <ol className="page-workflow" aria-label={label} role="list">
    {steps.map((step, index) => <li key={step.title}>
      <span className="page-workflow-number" aria-hidden="true">{index + 1}</span>
      <div><strong>{step.title}</strong><p>{step.description}</p></div>
    </li>)}
  </ol>;
}
