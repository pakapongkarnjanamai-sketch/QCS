import { CheckCircle2 } from 'lucide-react'
import type { PortalHistory } from './types'

function formatDate(value?: string): string {
  return value
    ? new Intl.DateTimeFormat('en-GB', {
        dateStyle: 'medium',
        timeStyle: 'short',
      }).format(new Date(value))
    : '-'
}

export function HistoryList({ histories }: { histories: PortalHistory[] }) {
  if (histories.length === 0)
    return (
      <p className="text-body text-ink-muted">
        No workflow history is available.
      </p>
    )
  return (
    <ol className="space-y-2.5">
      {histories.map((history) => (
        <li
          key={`${history.sequenceNo}-${history.actionDate ?? ''}`}
          className="flex gap-2 text-body"
        >
          <CheckCircle2 className="mt-0.5 size-3.5 shrink-0 text-ink-soft" aria-hidden />
          <div className="min-w-0">
            <p>
              <span className="font-medium">{history.stepName}</span> ·{' '}
              {history.statusName}
            </p>
            <p className="text-caption text-ink-muted">
              {history.approverName || history.approverNId || '-'} ·{' '}
              {formatDate(history.actionDate)}
            </p>
            {history.comment && (
              <p className="text-caption text-ink-muted">{history.comment}</p>
            )}
          </div>
        </li>
      ))}
    </ol>
  )
}
