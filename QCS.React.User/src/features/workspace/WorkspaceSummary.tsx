import { CheckCheck, ClipboardList, CircleX, ListTodo } from 'lucide-react'
import type { WorkspaceSummaryData } from './types'

interface WorkspaceSummaryProps {
  data?: WorkspaceSummaryData
  activeView: string
  onViewChange: (view: string) => void
}

const summaryItems = [
  { view: 'my-tasks', label: 'My tasks', key: 'myTaskCount', icon: ListTodo },
  { view: 'my-requests', label: 'My requests', key: 'myRequestCount', icon: ClipboardList },
  { view: 'my-approved', label: 'My approved', key: 'myApprovedCount', icon: CheckCheck },
  { view: 'rejected', label: 'Rejected', key: 'myRejectedCount', icon: CircleX },
] as const

export function WorkspaceSummary({ data, activeView, onViewChange }: WorkspaceSummaryProps) {
  return <section aria-label="Request summary" className="grid grid-cols-2 gap-px overflow-hidden rounded-sm border border-border-subtle bg-border-subtle lg:grid-cols-4">
    {summaryItems.map(({ view, label, key, icon: Icon }) => <button key={view} type="button" onClick={() => onViewChange(view)} className={`flex min-h-20 items-center gap-3 bg-surface-panel px-4 text-left focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-accent ${activeView === view ? 'bg-accent-soft' : 'hover:bg-surface-muted'}`}>
      <Icon size={18} className="shrink-0 text-ink-soft" aria-hidden />
      <span className="min-w-0"><span className="block text-caption text-ink-muted">{label}</span><span className="block text-heading font-semibold tabular-nums">{data?.[key] ?? '...'}</span></span>
    </button>)}
  </section>
}