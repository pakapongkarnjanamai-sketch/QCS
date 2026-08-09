import { Loader2, Search } from 'lucide-react'
import type { ReactNode } from 'react'
import { AppButton } from './AppButton'
import { appInputClassName } from './inputStyles'
import { ErrorSurface } from './Surfaces'

interface LookupTableShellProps {
  title: string
  search: string
  searchPlaceholder: string
  onSearchChange: (value: string) => void
  loading: boolean
  refreshing: boolean
  error?: string
  hasData: boolean
  isEmpty: boolean
  emptyMessage: string
  onRetry: () => void
  children: ReactNode
  footer?: ReactNode
  after?: ReactNode
}

export function LookupTableShell({
  title,
  search,
  searchPlaceholder,
  onSearchChange,
  loading,
  refreshing,
  error,
  hasData,
  isEmpty,
  emptyMessage,
  onRetry,
  children,
  footer,
  after,
}: LookupTableShellProps) {
  const errorMessage = error && /[.!?]$/.test(error) ? error : error ? `${error}.` : undefined

  return (
    <section className="flex flex-col gap-3 rounded-sm border border-border-subtle bg-surface-panel p-4">
      <header className="flex flex-col items-stretch gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex min-w-0 items-center gap-2">
          <h3 className="text-heading font-medium text-ink-strong">{title}</h3>
          {refreshing && <span className="inline-flex items-center gap-1.5 text-caption text-ink-muted"><Loader2 className="size-3.5 animate-spin" aria-hidden />Refreshing</span>}
        </div>
        <label className="relative w-full sm:max-w-72">
          <span className="sr-only">Search {title}</span>
          <Search className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-ink-muted" aria-hidden />
          <input type="search" value={search} onChange={(event) => onSearchChange(event.target.value)} placeholder={searchPlaceholder} className={appInputClassName('sm', 'w-full pl-8')} />
        </label>
      </header>

      {loading && !hasData && <div className="grid min-h-32 place-items-center text-body text-ink-muted" role="status"><span className="inline-flex items-center gap-2"><Loader2 className="size-4 animate-spin" aria-hidden />Loading...</span></div>}

      {errorMessage && (
        <ErrorSurface>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <span>{errorMessage}{hasData ? ' Showing the previous results.' : ''}</span>
            <AppButton variant="secondary" size="sm" onClick={onRetry}>Try again</AppButton>
          </div>
        </ErrorSurface>
      )}

      {hasData && (
        <div aria-busy={refreshing} className={refreshing ? 'pointer-events-none opacity-60' : ''}>
          {isEmpty ? <p className="px-4 py-6 text-center text-body text-ink-muted">{emptyMessage}</p> : children}
          {!isEmpty && footer}
        </div>
      )}

      {after}
    </section>
  )
}