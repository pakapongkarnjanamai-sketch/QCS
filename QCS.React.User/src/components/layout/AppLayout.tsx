import { Menu, ShieldCheck, X } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Link, NavLink } from 'react-router'
import { navigation } from '@/config/navigation'
import { useSession } from '@/hooks/useSession'
import { IconButton } from '@/components/ui/IconButton'
import { LoadingSurface } from '@/components/ui/Surfaces'
import { Breadcrumbs } from './Breadcrumbs'

export function AppLayout({ children }: { children: ReactNode }) {
  const [mobileOpen, setMobileOpen] = useState(false)
  const session = useSession()
  const today = new Intl.DateTimeFormat('en-GB', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  }).format(new Date())
  const sidebar = (
    <aside className={`fixed inset-y-0 left-0 z-40 flex w-60 flex-col border-r border-border-subtle bg-surface-panel transition-transform lg:static lg:translate-x-0 ${mobileOpen ? 'translate-x-0' : '-translate-x-full'}`}>
      <div className="flex h-16 items-center gap-2 border-b border-border-subtle px-5">
        <ShieldCheck className="size-6 shrink-0 text-accent" aria-hidden />
        <Link to="/" className="min-w-0">
          <p className="truncate text-heading font-semibold leading-tight text-ink-strong">QCS</p>
          <p className="truncate text-caption text-ink-muted">Quotation Request System</p>
        </Link>
        <IconButton className="ml-auto lg:hidden" label="Close menu" onClick={() => setMobileOpen(false)}>
          <X size={18} />
        </IconButton>
      </div>
      <nav className="grid gap-1 p-3">
        {navigation.map(({ icon: Icon, label, path }) => (
          <NavLink key={path} to={path} end={path === '/'} onClick={() => setMobileOpen(false)} className={({ isActive }) => `flex min-h-11 items-center gap-3 rounded-sm px-3 text-body font-medium focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent ${isActive ? 'bg-accent-soft text-accent' : 'text-ink-muted hover:bg-surface-muted hover:text-ink-strong'}`}>
            <Icon size={16} aria-hidden />
            <span>{label}</span>
          </NavLink>
        ))}
      </nav>
    </aside>
  )

  if (session.loading) return <LoadingSurface />
  return (
    <div className="flex h-dvh overflow-hidden bg-surface-app text-ink-strong">
      {sidebar}
      {mobileOpen && <button className="fixed inset-0 z-30 bg-black/25 lg:hidden" aria-label="Close menu overlay" onClick={() => setMobileOpen(false)} />}
      <div className="flex min-w-0 flex-1 flex-col overflow-hidden">
        <header className="flex h-16 shrink-0 items-center justify-between border-b border-border-subtle bg-surface-panel px-4 sm:px-6">
          <div className="flex min-w-0 items-center gap-3">
            <IconButton className="lg:hidden" label="Open navigation" onClick={() => setMobileOpen((open) => !open)}>
              <Menu size={18} />
            </IconButton>
            <Breadcrumbs />
          </div>
          <div className="hidden items-center gap-4 text-right md:flex">
            <div>
              <p className="text-caption uppercase tracking-[0.14em] text-ink-muted">User</p>
              <p className="max-w-44 truncate text-body font-medium">{session.data?.displayName?.toUpperCase() || 'Unavailable'}</p>
            </div>
            <div className="h-8 w-px bg-border-subtle" />
            <div>
              <p className="text-caption uppercase tracking-[0.14em] text-ink-muted">Today</p>
              <p className="text-body font-medium">{today}</p>
            </div>
          </div>
        </header>
        <main className="min-h-0 flex-1 overflow-y-auto p-4 sm:p-8">{children}</main>
      </div>
    </div>
  )
}
