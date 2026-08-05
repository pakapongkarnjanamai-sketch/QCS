import type { PortalWorkflowStep } from './types'

function formatDate(value?: string): string {
  return value
    ? new Intl.DateTimeFormat('en-GB', {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : 'Awaiting action'
}

export function WorkflowTimeline({ steps }: { steps: PortalWorkflowStep[] }) {
  if (steps.length === 0)
    return (
      <p className="text-body text-ink-muted">
        No workflow steps are available.
      </p>
    )
  return (
    <ol className="space-y-3">
      {steps.map((step) => (
        <li key={step.id} className="flex gap-3 text-body">
          <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-surface-muted text-caption font-medium">
            {step.sequenceNo}
          </span>
          <div className="min-w-0">
            <div className="flex flex-wrap items-baseline justify-between gap-x-3 gap-y-1">
              <p className="text-body font-medium">
                {step.stepName}
              </p>
              <p className="text-caption text-ink-soft">
                {formatDate(step.actionDate)}
              </p>
            </div>
            <p className="text-caption text-ink-muted">
              {step.approverName ||
                step.approverNId ||
                step.assignments
                  .map((item) => item.employeeName || item.nId)
                  .filter(Boolean)
                  .join(', ') ||
                'Not assigned'}
              {step.statusName ? ` · ${step.statusName}` : ''}
            </p>
            {step.comment && (
              <p className="mt-1 text-body text-ink-muted">{step.comment}</p>
            )}
          </div>
        </li>
      ))}
    </ol>
  )
}
