/**
 * One place decides what colour a status is — and it agrees with QRS.
 *
 * Every tone below is copied from `QRS.Web`'s `StatusBadge`, mapped by MEANING rather than by
 * name, because the two systems label the same thing differently: QCS's `Pending` is QRS's
 * `In process`, and QCS's `Approved` is QRS's `Completed`. A user moving between the portals
 * must not see the same state in two different colours. See PLANS/README.md rule 8.
 */
const TONE: Record<string, string> = {
  draft: 'bg-surface-panel text-ink-muted ring-border-subtle',
  pending: 'bg-accent-soft text-accent ring-accent/25',
  'in process': 'bg-accent-soft text-accent ring-accent/25',
  returned: 'bg-amber-50 text-amber-800 ring-amber-200',
  approved: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  completed: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  rejected: 'bg-red-50 text-red-700 ring-red-200',
  cancelled: 'bg-slate-200 text-slate-600 ring-slate-300',
}

export function StatusBadge({ status }: { status: string }) {
  const normalizedStatus = status.trim().toLowerCase()
  const tone = TONE[normalizedStatus]
    ?? (normalizedStatus.includes('reject') || normalizedStatus.includes('cancel')
      ? TONE.rejected
      : normalizedStatus.includes('approv') || normalizedStatus.includes('complet')
        ? TONE.approved
        : normalizedStatus.includes('process')
          ? TONE['in process']
          : TONE.pending)

  return <span className={`inline-flex items-center rounded-sm px-2 py-0.5 text-caption font-medium ring-1 ring-inset ${tone}`}>{status || 'Unknown'}</span>
}