import { Menu, PanelLeftClose, PanelLeftOpen, X } from 'lucide-react'
import { useState, type ReactNode } from 'react'
import { Link, useLocation } from 'react-router'
import { navigation } from '@/config/navigation'
import { useSession } from '@/hooks/useSession'
import { IconButton } from '@/components/ui/IconButton'
import { LoadingSurface } from '@/components/ui/Surfaces'
import { Breadcrumbs } from './Breadcrumbs'

export function AppLayout({ children }: { children: ReactNode }) {
  const [collapsed, setCollapsed] = useState(false)
  const [mobileOpen, setMobileOpen] = useState(false)
  const session = useSession()
  const location = useLocation()
  const workspaceView = new URLSearchParams(location.search).get('view')
  const today = new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }).format(new Date())
  const sidebarWidth = collapsed ? 'md:w-16' : 'md:w-56'

  const sidebar = <aside className={`fixed inset-y-0 left-0 z-40 flex w-56 flex-col border-r border-border-subtle bg-white transition-transform md:static md:translate-x-0 ${sidebarWidth} ${mobileOpen ? 'translate-x-0' : '-translate-x-full'}`}>
    <div className="flex h-16 items-center border-b border-border-subtle px-4"><Link to="/" className="truncate text-heading font-semibold text-ink-strong">{collapsed ? 'QCS' : 'QCS Portal'}</Link><IconButton className="ml-auto md:hidden" label="Close menu" onClick={() => setMobileOpen(false)}><X size={18} /></IconButton></div>
    <nav className="grid gap-1 p-2">{navigation.map(({ icon: Icon, label, path }) => { const target = new URL(path, window.location.origin); const active = label === 'Overview' ? location.pathname === '/' && workspaceView !== 'my-requests' : label === 'Requests' ? location.pathname === '/' && workspaceView === 'my-requests' : location.pathname === target.pathname; return <Link key={path} to={path} onClick={() => setMobileOpen(false)} title={collapsed ? label : undefined} className={`flex items-center gap-3 rounded-sm px-3 py-2 text-body focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent ${active ? 'bg-accent-soft text-accent' : 'text-ink-muted hover:bg-surface-muted hover:text-ink-strong'} ${collapsed ? 'md:justify-center md:px-2' : ''}`}><Icon size={18} /><span className={collapsed ? 'md:hidden' : ''}>{label}</span></Link> })}</nav>
  </aside>

  if (session.loading) return <LoadingSurface />
  return <div className="flex h-dvh overflow-hidden bg-surface-app text-ink-strong">
    {sidebar}
    {mobileOpen && <button className="fixed inset-0 z-30 bg-black/25 md:hidden" aria-label="Close menu overlay" onClick={() => setMobileOpen(false)} />}
    <div className="flex min-w-0 flex-1 flex-col overflow-hidden"><header className="flex h-16 shrink-0 items-center justify-between border-b border-border-subtle bg-white px-4 md:px-6"><div className="flex min-w-0 items-center"><IconButton label="Toggle navigation" onClick={() => window.innerWidth < 768 ? setMobileOpen((open) => !open) : setCollapsed((value) => !value)}>{collapsed ? <PanelLeftOpen className="hidden md:block" size={18} /> : <PanelLeftClose className="hidden md:block" size={18} />}<Menu className="md:hidden" size={18} /></IconButton><div className="mx-3 h-5 w-px bg-border-subtle" /><Breadcrumbs /></div><div className="hidden items-center gap-4 text-right md:flex"><div><p className="text-caption text-ink-soft">User</p><p className="max-w-44 truncate text-body font-medium">{session.data?.displayName || 'Unavailable'}</p></div><div className="h-8 w-px bg-border-subtle" /><div><p className="text-caption text-ink-soft">Today</p><p className="text-body font-medium">{today}</p></div></div></header><main className="min-h-0 flex-1 overflow-y-auto p-4 md:p-6">{children}</main></div>
  </div>
}