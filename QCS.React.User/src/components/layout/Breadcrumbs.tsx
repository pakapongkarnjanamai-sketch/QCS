import { ChevronRight } from 'lucide-react'
import { useLocation } from 'react-router'

export function Breadcrumbs() {
  const { pathname } = useLocation()
  const label = pathname.startsWith('/quotations/')
    ? 'Quotation'
    : pathname.startsWith('/quotations')
      ? 'Quotations'
      : pathname.startsWith('/requests')
        ? 'Requests'
        : 'Dashboard'
  return (
    <nav
      aria-label="Breadcrumb"
      className="flex min-w-0 items-center gap-1 text-body"
    >
      <span className="truncate text-ink-muted">Dashboard</span>
      {label !== 'Dashboard' && (
        <>
          <ChevronRight className="size-4 shrink-0 text-ink-soft" aria-hidden />
          <span className="truncate font-medium text-ink-strong">{label}</span>
        </>
      )}
    </nav>
  )
}
