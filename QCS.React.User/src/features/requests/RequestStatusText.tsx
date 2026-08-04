export function RequestStatusText({ statusName }: { statusName: string }) {
  const tone = statusName.toLowerCase().includes('reject') ? 'bg-red-50 text-danger' : statusName.toLowerCase().includes('approved') ? 'bg-emerald-50 text-emerald-700' : 'bg-accent-soft text-accent'
  return <span className={`inline-flex rounded-sm px-2 py-0.5 text-caption font-medium ${tone}`}>{statusName || 'Unknown'}</span>
}