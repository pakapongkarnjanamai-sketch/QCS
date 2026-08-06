import { ChevronRight } from 'lucide-react'
import { Link, useLocation } from 'react-router'

/**
 * Same shape and same classes as QRS.Web's Breadcrumbs — see PLANS/README.md rule 8.
 *
 * Walks the path generically rather than matching each route by hand. The version this replaces
 * tested the pathname against a chain of regexes, so it had to be edited every time a route was
 * added and silently produced no crumb for anything it did not already know about — which is how
 * /inbox and /quotations went unlabelled when they arrived.
 */
const labels: Record<string, string> = {
  requests: 'Requests',
  new: 'New request',
  inbox: 'My approvals',
  quotations: 'Quotations',
}

export function Breadcrumbs() {
  const { pathname } = useLocation()
  const parts = pathname.split('/').filter(Boolean)
  const crumbs = [{ label: 'Dashboard', to: '/' }]

  let path = ''
  for (const part of parts) {
    path += `/${part}`
    // A numeric id carries no label of its own; it is named after the loop, from its collection.
    if (!/^\d+$/.test(part)) crumbs.push({ label: labels[part] ?? part, to: path })
  }

  const last = parts.at(-1) ?? ''
  if (/^\d+$/.test(last)) crumbs.push({ label: 'Request', to: pathname })
  else if (parts.length > 1 && parts[0] === 'quotations') {
    // /quotations/{code}: the code segment is not numeric, so it was already pushed verbatim.
    crumbs[crumbs.length - 1] = { label: 'Quotation', to: pathname }
  }

  return <nav aria-label="Breadcrumb" className="flex min-w-0 items-center gap-1 text-body text-ink-muted">
    {crumbs.map((crumb, index) => <span key={crumb.to} className="flex items-center gap-1">
      {index > 0 && <ChevronRight className="size-3.5" aria-hidden />}
      {crumb.to === pathname ? <span className="truncate text-ink-strong">{crumb.label}</span> : <Link to={crumb.to} className="rounded-sm hover:text-ink-strong focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">{crumb.label}</Link>}
    </span>)}
  </nav>
}
