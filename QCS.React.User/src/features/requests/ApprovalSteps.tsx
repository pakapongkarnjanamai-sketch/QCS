import { useState } from 'react'
import { History } from 'lucide-react'
import { AppButton } from '@/components/ui/AppButton'
import { SectionCard } from '@/components/ui/SectionCard'
import { HistoryList } from './HistoryList'
import { WorkflowTimeline } from './WorkflowTimeline'
import type { PortalHistory, PortalWorkflowStep } from './types'

/**
 * One panel, one name.
 *
 * "Workflow" and "Approval steps" were two headings over the same thing: the route this request
 * takes through its approvers. They sat in separate cards, and the history of those same steps
 * sat in a third. A reader had to work out that all three described one process.
 *
 * History is now a toggle inside this panel rather than a section of its own, because it is the
 * same list seen backwards — what already happened to these steps. It stays collapsed by default:
 * the question people open a request to answer is "where is it now", not "where has it been".
 */
export function ApprovalSteps({ steps, histories }: { steps: PortalWorkflowStep[]; histories: PortalHistory[] }) {
  const [showHistory, setShowHistory] = useState(false)

  return (
    <SectionCard
      title="Approval steps"
      action={<AppButton
          variant="ghost"
          size="sm"
          aria-expanded={showHistory}
          onClick={() => setShowHistory((shown) => !shown)}
        >
          <History className="size-3.5" aria-hidden />
          {showHistory ? 'Hide history' : `History${histories.length > 0 ? ` (${histories.length})` : ''}`}
        </AppButton>}
    >

      <div className="p-4">
        <WorkflowTimeline steps={steps} />
      </div>

      {showHistory && (
        <div className="border-t border-border-subtle bg-surface-muted p-4">
          <h3 className="mb-3 text-caption font-semibold uppercase tracking-[0.12em] text-ink-muted">
            History
          </h3>
          <HistoryList histories={histories} />
        </div>
      )}
    </SectionCard>
  )
}
