const TONE: Record<string, string> = {
  draft: 'bg-surface-panel text-ink-muted ring-border-subtle',
  pending: 'bg-accent-soft text-accent ring-accent/25',
  approved: 'bg-emerald-50 text-emerald-700 ring-emerald-200',
  rejected: 'bg-red-50 text-red-700 ring-red-200',
}

export function StatusBadge({ status }: { status: string }) {
  const normalizedStatus = status.trim().toLowerCase()
  const tone = TONE[normalizedStatus]
    ?? (normalizedStatus.includes('reject') ? TONE.rejected : normalizedStatus.includes('approv') ? TONE.approved : TONE.pending)

  return <span className={`inline-flex items-center rounded-sm px-2 py-0.5 text-caption font-medium ring-1 ring-inset ${tone}`}>{status || 'Unknown'}</span>
}