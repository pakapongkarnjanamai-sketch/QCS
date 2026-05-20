import { NavLink } from 'react-router-dom'
import { NAV_GROUPS, type NavIcon } from '../../config/navigation.ts'

type SidebarProps = {
  isOpen: boolean
  onClose: () => void
}

type IconProps = {
  active: boolean
}

const iconClassName = (active: boolean) =>
  `h-4 w-4 shrink-0 ${active ? 'text-[var(--ink-strong)]' : 'text-[var(--ink-soft)] group-hover:text-[var(--ink-strong)]'}`

const OverviewIcon = ({ active }: IconProps) => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={iconClassName(active)} aria-hidden="true">
    <rect x="4" y="4" width="6" height="6" rx="1" />
    <rect x="14" y="4" width="6" height="6" rx="1" />
    <rect x="4" y="14" width="6" height="6" rx="1" />
    <rect x="14" y="14" width="6" height="6" rx="1" />
  </svg>
)

const RequestsIcon = ({ active }: IconProps) => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={iconClassName(active)} aria-hidden="true">
    <path d="M7 4.5h7l3 3V19.5H7z" />
    <path d="M10 11h6" />
    <path d="M10 15h6" />
  </svg>
)

const QuotationsIcon = ({ active }: IconProps) => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={iconClassName(active)} aria-hidden="true">
    <path d="M6 6.5h12v11H6z" />
    <path d="M9 10h6" />
    <path d="M9 14h4" />
    <path d="M8 6.5V4" />
    <path d="M16 6.5V4" />
  </svg>
)

const WorkflowIcon = ({ active }: IconProps) => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={iconClassName(active)} aria-hidden="true">
    <circle cx="6.5" cy="7" r="2.5" />
    <circle cx="17.5" cy="12" r="2.5" />
    <circle cx="6.5" cy="17" r="2.5" />
    <path d="M8.8 8.2l6.3 2.5" />
    <path d="M8.8 15.8l6.3-2.5" />
  </svg>
)

const VendorsIcon = ({ active }: IconProps) => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={iconClassName(active)} aria-hidden="true">
    <path d="M4 18.5h16" />
    <path d="M6 18.5V8.5l6-3 6 3v10" />
    <path d="M10 12h4" />
  </svg>
)

const UsersIcon = ({ active }: IconProps) => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={iconClassName(active)} aria-hidden="true">
    <circle cx="9" cy="8.5" r="2.5" />
    <circle cx="16.5" cy="10" r="2" />
    <path d="M5.5 18.5a3.5 3.5 0 0 1 7 0" />
    <path d="M14.5 18.5a2.8 2.8 0 0 1 5 0" />
  </svg>
)

const DepartmentsIcon = ({ active }: IconProps) => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" className={iconClassName(active)} aria-hidden="true">
    <path d="M5 19V7l7-3 7 3v12" />
    <path d="M9 10h6" />
    <path d="M9 14h6" />
    <path d="M12 7v12" />
  </svg>
)

const SustainabilityIcon = ({ active }: IconProps) => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" className={iconClassName(active)} aria-hidden="true">
    <path d="M20 4c-9 0-14 5-14 12 0 1.3.2 2.5.6 3.6" />
    <path d="M6 20c0-7 5-12 14-14" />
  </svg>
)

const CloseIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" className="h-4 w-4" aria-hidden="true">
    <path d="M6 6l12 12" />
    <path d="M18 6L6 18" />
  </svg>
)

const renderIcon = (icon: NavIcon, active: boolean) => {
  switch (icon) {
    case 'overview':
      return <OverviewIcon active={active} />
    case 'requests':
      return <RequestsIcon active={active} />
    case 'quotations':
      return <QuotationsIcon active={active} />
    case 'workflow':
      return <WorkflowIcon active={active} />
    case 'vendors':
      return <VendorsIcon active={active} />
    case 'users':
      return <UsersIcon active={active} />
    case 'departments':
      return <DepartmentsIcon active={active} />
    case 'sustainability':
      return <SustainabilityIcon active={active} />
  }
}

export function Sidebar({ isOpen, onClose }: SidebarProps) {
  return (
    <aside
      className={`fixed inset-y-0 left-0 z-50 flex w-64 shrink-0 flex-col border-r border-[var(--border-subtle)] bg-[var(--surface-panel)] transition-transform duration-200 ease-out lg:static lg:translate-x-0 ${
        isOpen ? 'translate-x-0' : '-translate-x-full'
      }`}
    >
      <div className="flex h-16 items-center justify-between border-b border-[var(--border-subtle)] px-4">
        <div>
          <p
            className="text-[22px] font-semibold uppercase leading-none tracking-[0.08em] text-[var(--ink-strong)]"
            style={{ fontFamily: 'var(--font-display)' }}
          >
            QCS
          </p>
        
        </div>

        <button
          type="button"
          className="focus-ring inline-flex h-8 w-8 items-center justify-center rounded-sm border border-[var(--border-subtle)] text-[var(--ink-muted)] lg:hidden"
          aria-label="Close navigation"
          onClick={onClose}
        >
          <CloseIcon />
        </button>
      </div>

      <nav className="flex-1 overflow-y-auto px-3 py-4" aria-label="Primary navigation">
        <div className="space-y-6">
          {NAV_GROUPS.map((group) => (
            <div key={group.label} className="space-y-2">
              <p className="px-2 text-[11px] font-semibold uppercase tracking-[0.16em] text-[var(--ink-soft)]">
                {group.label}
              </p>

              <div className="space-y-1">
                {group.items.map((item) => (
                  <NavLink
                    key={item.to}
                    to={item.to}
                    end={item.to === '/'}
                    onClick={onClose}
                    className={({ isActive }) =>
                      `focus-ring group flex items-center gap-3 rounded-sm border px-3 py-2.5 ${
                        isActive
                          ? 'border-[var(--border-strong)] bg-[var(--surface-muted)] text-[var(--ink-strong)]'
                          : 'border-transparent text-[var(--ink-muted)] hover:border-[var(--border-subtle)] hover:bg-[var(--surface-muted)] hover:text-[var(--ink-strong)]'
                      }`
                    }
                  >
                    {({ isActive }) => (
                      <>
                        {renderIcon(item.icon, isActive)}
                        <span className="min-w-0 flex-1 truncate text-[13px] font-medium">
                          {item.label}
                        </span>
                      </>
                    )}
                  </NavLink>
                ))}
              </div>
            </div>
          ))}
        </div>
      </nav>

      <div className="border-t border-[var(--border-subtle)] px-4 py-4 text-[11px] text-[var(--ink-soft)]">
       
        <p className="mt-1">© 2026 - Quotation Compare System</p>
      </div>
    </aside>
  )
}