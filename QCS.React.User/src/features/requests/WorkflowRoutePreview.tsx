import type { PortalWorkflowStep } from './types'

export function WorkflowRoutePreview({ steps }: { steps: PortalWorkflowStep[] }) {
  if (steps.length === 0) return null
  return <section className="border-t border-border-subtle pt-4"><h2 className="text-heading font-semibold">Approval route</h2><ol className="mt-3 grid gap-2">{steps.map((step) => <li key={step.id} className="text-body text-ink-muted"><span className="mr-2 text-ink-soft">{step.sequenceNo}.</span>{step.stepName} {step.approverName && `- ${step.approverName}`}</li>)}</ol></section>
}