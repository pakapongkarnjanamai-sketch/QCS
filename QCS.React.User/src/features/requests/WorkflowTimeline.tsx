import type { PortalWorkflowStep } from './types'

function formatDate(value?: string): string { return value ? new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : 'Awaiting action' }

export function WorkflowTimeline({ steps }: { steps: PortalWorkflowStep[] }) {
  if (steps.length === 0) return <p className="text-body text-ink-muted">No workflow steps are available.</p>
  return <ol className="grid gap-0">{steps.map((step) => <li key={step.id} className="grid grid-cols-[1.5rem_minmax(0,1fr)] gap-3 pb-4 last:pb-0"><div className="grid justify-items-center"><span className={`mt-1.5 h-2.5 w-2.5 rounded-full ${step.statusName ? 'bg-accent' : 'bg-border-subtle'}`} /><span className="w-px flex-1 bg-border-subtle last:hidden" /></div><div className="min-w-0"><div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1"><p className="text-body font-medium">{step.sequenceNo}. {step.stepName}</p><p className="text-caption text-ink-soft">{formatDate(step.actionDate)}</p></div><p className="text-caption text-ink-muted">{step.approverName || step.approverNId || step.assignments.map((item) => item.employeeName || item.nId).filter(Boolean).join(', ') || 'Not assigned'}{step.statusName ? ` · ${step.statusName}` : ''}</p>{step.comment && <p className="mt-1 text-body text-ink-muted">{step.comment}</p>}</div></li>)}</ol>
}