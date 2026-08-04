import { useLocation } from 'react-router'

export function Breadcrumbs() {
  const { pathname } = useLocation()
  const label = pathname.startsWith('/quotations/')
    ? 'Quotation'
    : pathname.startsWith('/quotations')
      ? 'Quotations'
      : pathname.startsWith('/requests')
        ? 'Requests'
        : 'Overview'
  return <p className="truncate text-body font-medium text-ink-strong">{label}</p>
}