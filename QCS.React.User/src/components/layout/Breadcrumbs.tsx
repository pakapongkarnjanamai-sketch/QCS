import { ChevronRight } from 'lucide-react'
import { Link, useLocation } from 'react-router'

export function Breadcrumbs() {
  const { pathname } = useLocation()
  const crumbs = [{ label: 'Dashboard', to: '/' }]
  const requestEdit = /^\/requests\/(\d+)\/edit$/.exec(pathname)
  const requestDetail = /^\/requests\/(\d+)$/.exec(pathname)
  if (pathname.startsWith('/requests')) {
    crumbs.push({ label: 'Requests', to: '/requests' })
    if (pathname === '/requests/new') crumbs.push({ label: 'New request', to: pathname })
    else if (requestEdit) {
      crumbs.push({ label: 'Request', to: `/requests/${requestEdit[1]}` })
      crumbs.push({ label: 'Edit request', to: pathname })
    } else if (requestDetail) crumbs.push({ label: 'Request', to: pathname })
  } else if (pathname.startsWith('/quotations/')) crumbs.push({ label: 'Quotation', to: pathname })
  else if (pathname.startsWith('/quotations')) crumbs.push({ label: 'Quotations', to: pathname })
  return (
    <nav
      aria-label="Breadcrumb"
      className="flex min-w-0 items-center gap-1 text-body"
    >
      {crumbs.map((crumb, index) => <span key={crumb.to} className="flex min-w-0 items-center gap-1">{index > 0 && <ChevronRight className="size-4 shrink-0 text-ink-soft" aria-hidden />}{crumb.to === pathname ? <span className="truncate font-medium text-ink-strong">{crumb.label}</span> : <Link to={crumb.to} className="truncate rounded-sm text-ink-muted hover:text-ink-strong focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">{crumb.label}</Link>}</span>)}
    </nav>
  )
}
