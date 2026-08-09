import { AppButton } from '@/components/ui/AppButton'
import { SectionCard } from '@/components/ui/SectionCard'
import type { RoutePreview } from './types'

/**
 * The route this request will take, resolved by the central workflow service — not by this app.
 *
 * It previously rendered the request's own stored `workflowSteps`, which only exist after submit
 * and came from the retired local engine. It now renders whatever `route-preview` returns for the
 * form as it currently stands, which is also the only way a requester can see the route *before*
 * committing to it.
 *
 * Nothing here assumes a step count. A workflow published with one step or with forty renders
 * through this same list.
 */
export function WorkflowRoutePreview({
  preview,
  loading,
  error,
  onLoad,
}: {
  preview?: RoutePreview
  loading: boolean
  error?: string
  onLoad: () => void
}) {
  return (
    <SectionCard
      title="Approval route"
      action={<AppButton variant="secondary" size="sm" disabled={loading} onClick={onLoad}>
          {loading ? 'Resolving...' : preview ? 'Refresh route' : 'Preview route'}
        </AppButton>}
    >

      {error && <p className="px-4 py-3 text-body text-danger">{error}</p>}

      {!error && !preview && (
        <p className="px-4 py-4 text-caption text-ink-muted">See who will approve this before you submit it.</p>
      )}

      {!error && preview && (
        <>
          {preview.workflowName && (
            <p className="border-b border-border-subtle px-4 py-2 text-caption text-ink-muted">
              {preview.workflowName}
              {preview.workflowVersion ? ` · ${preview.workflowVersion}` : ''}
            </p>
          )}
          {preview.steps.length === 0 ? (
            <p className="px-4 py-4 text-body text-ink-muted">The service returned no steps for this request.</p>
          ) : (
            <ol className="space-y-3 p-4">
              {preview.steps.map((step) => (
                <li key={step.sequenceNo} className="flex gap-3 text-body">
                  <span className="flex size-6 shrink-0 items-center justify-center rounded-full bg-surface-muted text-caption font-medium">
                    {step.sequenceNo}
                  </span>
                  <div className="min-w-0">
                    <p className="font-medium">
                      {step.stepName}
                      {step.isFinalStep && <span className="ml-2 text-caption font-normal text-ink-muted">final</span>}
                    </p>
                    <p className="text-caption text-ink-muted">
                      {step.assignees.length > 0
                        ? step.assignees.map((assignee) => assignee.employeeName || assignee.username).join(', ')
                        : /* An unresolved step is a workflow configuration problem the requester
                             cannot fix, so name it rather than leaving the row blank. */
                          'No approver resolved — contact a QCS administrator'}
                    </p>
                  </div>
                </li>
              ))}
            </ol>
          )}
        </>
      )}
    </SectionCard>
  )
}
