/**
 * One place decides what a status is called and what colour it is — and it agrees with QRS.
 *
 * Both portals now mirror the central Approval Service's seven document statuses, so this maps by
 * NAME rather than by meaning as it used to. Tones and labels are copied from `QRS.Web`'s
 * `StatusBadge` and `REQUEST_STATUS_LABEL`: a user moving between the portals must not see the
 * same state in two colours or under two names. See PLANS/README.md rule 8.
 *
 * The API sends the enum name (`InProcess`, `WaitingEffective`), so the label is looked up rather
 * than printed — "WaitingEffective" is not what a reader should be shown.
 */
const CENTRAL: Record<string, { label: string; tone: string }> = {
  draft: { label: 'Draft', tone: 'bg-surface-panel text-ink-muted ring-border-subtle' },
  inprocess: { label: 'In process', tone: 'bg-accent-soft text-accent ring-accent/25' },
  returned: { label: 'Returned', tone: 'bg-amber-50 text-amber-800 ring-amber-200' },
  rejected: { label: 'Rejected', tone: 'bg-red-50 text-red-700 ring-red-200' },
  waitingeffective: { label: 'Waiting effective', tone: 'bg-violet-50 text-violet-700 ring-violet-200' },
  completed: { label: 'Completed', tone: 'bg-emerald-50 text-emerald-700 ring-emerald-200' },
  cancelled: { label: 'Cancelled', tone: 'bg-slate-200 text-slate-600 ring-slate-300' },
}

/**
 * Words this portal no longer produces for a REQUEST, kept because the same badge renders workflow
 * STEP statuses, which the central service words freely, and retained terminal legacy rows. A new
 * central status belongs in CENTRAL above, never here.
 */
const LEGACY: Record<string, string> = {
  pending: CENTRAL.inprocess.tone,
  'in process': CENTRAL.inprocess.tone,
  approved: CENTRAL.completed.tone,
  skipped: CENTRAL.draft.tone,
  inreview: CENTRAL.inprocess.tone,
}

export function StatusBadge({ status }: { status: string }) {
  const key = (status ?? '').trim().toLowerCase()
  const central = CENTRAL[key]

  const tone =
    central?.tone ??
    LEGACY[key] ??
    (key.includes('reject') || key.includes('cancel')
      ? CENTRAL.rejected.tone
      : key.includes('complet') || key.includes('approv')
        ? CENTRAL.completed.tone
        : key.includes('return')
          ? CENTRAL.returned.tone
          : CENTRAL.inprocess.tone)

  return <span className={`inline-flex items-center rounded-sm px-2 py-0.5 text-caption font-medium ring-1 ring-inset ${tone}`}>{central?.label ?? status ?? 'Unknown'}</span>
}
