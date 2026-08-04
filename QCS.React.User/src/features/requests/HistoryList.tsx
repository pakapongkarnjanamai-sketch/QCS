import type { PortalHistory } from './types'

function formatDate(value?: string): string { return value ? new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value)) : '-' }

export function HistoryList({ histories }: { histories: PortalHistory[] }) {
  if (histories.length === 0) return <p className="text-body text-ink-muted">No workflow history is available.</p>
  return <ol className="divide-y divide-border-subtle">{histories.map((history) => <li key={`${history.sequenceNo}-${history.actionDate ?? ''}`} className="py-3 first:pt-0"><div className="flex flex-wrap justify-between gap-x-3 gap-y-1"><p className="text-body font-medium">{history.stepName} · {history.statusName}</p><p className="text-caption text-ink-soft">{formatDate(history.actionDate)}</p></div><p className="text-caption text-ink-muted">{history.approverName || history.approverNId || '-'}</p>{history.comment && <p className="mt-1 text-body text-ink-muted">{history.comment}</p>}</li>)}</ol>
}