import { Menu, ShieldCheck, X } from 'lucide-react'
import { useEffect, useState, type ReactNode } from 'react'
import { Link, NavLink, Outlet, useLocation } from 'react-router'
import { navigation } from '@/config/navigation'
import { useSession } from '@/hooks/useSession'
import { IconButton } from '@/components/ui/IconButton'
import { LoadingSurface } from '@/components/ui/Surfaces'
import { Breadcrumbs } from './Breadcrumbs'

export function AppLayout({ children }: { children?: ReactNode } = {}) {
  const [mobileOpen, setMobileOpen] = useState(false)
  const location = useLocation()
  const session = useSession()

  useEffect(() => setMobileOpen(false), [location.pathname])
  useEffect(() => {
    if (!mobileOpen) return
    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setMobileOpen(false)
    }
    document.addEventListener('keydown', closeOnEscape)
    return () => document.removeEventListener('keydown', closeOnEscape)
  }, [mobileOpen])

  const today = new Intl.DateTimeFormat('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(new Date())

  if (session.loading) return <LoadingSurface />

  return (
    <div className="flex h-dvh overflow-hidden bg-surface-app text-ink-strong">
      <aside className={`fixed inset-y-0 left-0 z-40 flex w-60 flex-col border-r border-border-subtle bg-surface-panel transition-transform lg:static lg:translate-x-0 ${mobileOpen ? 'translate-x-0' : '-translate-x-full'}`}>
        <div className="flex h-16 items-center gap-2 border-b border-border-subtle px-5">
          <ShieldCheck className="size-6 shrink-0 text-accent" aria-hidden />
          <Link to="/" className="min-w-0">
            <p className="truncate text-heading font-semibold leading-tight text-ink-strong">QCS</p>
            <p className="truncate text-caption text-ink-muted">Purchasing Workspace</p>
          </Link>
          <IconButton className="ml-auto lg:hidden" label="Close menu" onClick={() => setMobileOpen(false)}>
            <X className="size-5" />
          </IconButton>
        </div>
        <nav className="grid gap-1 p-3">
          {navigation.map(({ icon: Icon, label, path }) => (
            <NavLink key={path} to={path} end={path === '/'} onClick={() => setMobileOpen(false)} className={({ isActive }) => `flex min-h-11 items-center gap-3 rounded-sm px-3 text-body font-medium focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent ${isActive ? 'bg-accent-soft text-accent' : 'text-ink-muted hover:bg-surface-muted hover:text-ink-strong'}`}>
              <Icon className="size-4" aria-hidden />
              <span>{label}</span>
            </NavLink>
          ))}
        </nav>

        <div className="mt-auto border-t border-border-subtle p-4 text-caption text-ink-muted">
          <p>Quotation Comparison System - © {new Date().getFullYear()}</p>
        </div>
      </aside>

      {mobileOpen && <button className="fixed inset-0 z-30 bg-black/25 lg:hidden" aria-label="Close menu overlay" onClick={() => setMobileOpen(false)} />}

      <div className="flex min-w-0 flex-1 flex-col overflow-hidden">
        <header className="flex h-16 shrink-0 items-center justify-between border-b border-border-subtle bg-surface-panel px-4 sm:px-6">
          <div className="flex min-w-0 items-center gap-3">
            <IconButton className="lg:hidden" label={mobileOpen ? 'Close navigation' : 'Open navigation'} onClick={() => setMobileOpen((open) => !open)}>
              {mobileOpen ? <X className="size-5" aria-hidden /> : <Menu className="size-5" aria-hidden />}
            </IconButton>
            <Breadcrumbs />
          </div>
          <div className="flex items-center gap-4 text-right">
            {session.error && (
              <p className="text-caption text-danger">
                Not signed in{session.error.status ? ` (${session.error.status})` : ''}
              </p>
            )}
            <div className="hidden text-right md:block">
              <p className="text-caption uppercase tracking-[0.14em] text-ink-muted">User</p>
              <p className="max-w-44 truncate text-body font-medium text-ink-strong">{session.data?.displayName || 'Unavailable'}</p>
            </div>
            <div className="hidden h-8 w-px bg-border-subtle md:block" aria-hidden />
            <div className="hidden text-right md:block">
              <p className="text-caption uppercase tracking-[0.14em] text-ink-muted">Today</p>
              <p className="text-body font-medium text-ink-strong">{today}</p>
            </div>
          </div>
        </header>
        <main className="min-h-0 flex-1 overflow-y-auto p-4 sm:p-8">{children ?? <Outlet />}</main>
      </div>
    </div>
  )
}
