import { useEffect, useMemo, useState } from 'react'
import { Outlet, useLocation } from 'react-router-dom'
import { getPageTitle } from '../../config/navigation.ts'
import { appConfig } from '../../config/appConfig.ts'
import { Sidebar } from './Sidebar.tsx'

const formatToday = () =>
  new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    year: 'numeric',
  }).format(new Date())

const MenuIcon = () => (
  <svg
    viewBox="0 0 24 24"
    fill="none"
    stroke="currentColor"
    strokeWidth="1.8"
    strokeLinecap="round"
    strokeLinejoin="round"
    className="h-4 w-4"
    aria-hidden="true"
  >
    <path d="M4 7h16" />
    <path d="M4 12h16" />
    <path d="M4 17h16" />
  </svg>
)

export function AppLayout() {
  const location = useLocation()
  const [isSidebarOpen, setIsSidebarOpen] = useState(false)
  const [userName, setUserName] = useState('Unknown User')
  const pageTitle = useMemo(() => getPageTitle(location.pathname), [location.pathname])
  const today = useMemo(() => formatToday(), [])

  useEffect(() => {
    let isCancelled = false

    const loadCurrentUser = async () => {
      try {
        const response = await fetch(`${appConfig.apiBaseUrl}/api/Session/Me`, {
          credentials: 'include',
        })

        if (!response.ok) {
          return
        }

        const payload = (await response.json()) as { displayName?: string }
        const nextName = payload.displayName?.trim()
        if (!isCancelled && nextName) {
          setUserName(nextName)
        }
      } catch {
        // Keep fallback display name when profile endpoint is unavailable.
      }
    }

    void loadCurrentUser()

    return () => {
      isCancelled = true
    }
  }, [])

  return (
    <div className="h-screen overflow-hidden bg-[var(--surface-app)] text-[var(--ink-strong)]">
      <div className="flex h-full min-h-0">
        <Sidebar isOpen={isSidebarOpen} onClose={() => setIsSidebarOpen(false)} />

        <button
          type="button"
          aria-label="Close navigation"
          className={`fixed inset-0 z-40 bg-black/20 lg:hidden ${
            isSidebarOpen ? 'opacity-100' : 'pointer-events-none opacity-0'
          }`}
          onClick={() => setIsSidebarOpen(false)}
        />

        <div className="flex min-w-0 flex-1 flex-col">
          <header className="flex h-16 shrink-0 items-center justify-between border-b border-[var(--border-subtle)] bg-[var(--surface-panel)] px-4 sm:px-6">
            <div className="flex min-w-0 items-center gap-3">
              <button
                type="button"
                className="focus-ring inline-flex h-9 w-9 items-center justify-center rounded-sm border border-[var(--border-subtle)] bg-[var(--surface-panel)] text-[var(--ink-strong)] lg:hidden"
                aria-label="Open navigation"
                onClick={() => setIsSidebarOpen(true)}
              >
                <MenuIcon />
              </button>

              <div className="min-w-0">
               
                <h1
                  className="truncate text-[20px] font-semibold leading-none text-[var(--ink-strong)]"
                >
                  {pageTitle}
                </h1>
              </div>
            </div>

            <div className="hidden items-center gap-4 md:flex">
              <div className="text-right">
                <p className="text-[11px] uppercase tracking-[0.14em] text-[var(--ink-soft)]">
                  User
                </p>
                <p className="text-[13px] font-medium text-[var(--ink-strong)]">{userName}</p>
              </div>

              <div className="h-8 w-px bg-[var(--border-subtle)]" aria-hidden="true" />

              <div className="text-right">
                <p className="text-[11px] uppercase tracking-[0.14em] text-[var(--ink-soft)]">
                  Today
                </p>
                <p className="text-[13px] font-medium text-[var(--ink-strong)]">{today}</p>
              </div>
            </div>
          </header>

          <main className="min-h-0 flex-1 flex flex-col">
            <div className="flex flex-1 min-h-0 flex-col overflow-y-auto p-4 sm:p-6">
              <div className="shell-enter mx-auto max-w-[1560px] flex flex-1 min-h-0 flex-col w-full">
                <Outlet />
              </div>
            </div>
          </main>
        </div>
      </div>
    </div>
  )
}