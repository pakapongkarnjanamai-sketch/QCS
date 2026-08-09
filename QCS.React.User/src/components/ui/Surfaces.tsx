import { Loader2 } from 'lucide-react'
import type { ReactNode } from 'react'
import { AppButton } from './AppButton'

export function LoadingSurface({ children = 'Loading…' }: { children?: ReactNode }) {
  return (
    <div role="status" className="flex min-h-48 items-center justify-center gap-2 rounded-sm border border-border-subtle bg-surface-panel p-6 text-body text-ink-muted">
      <Loader2 className="size-4 animate-spin" aria-hidden />
      {children}
    </div>
  )
}

export function EmptySurface({ children }: { children: ReactNode }) {
  return (
    <div className="flex min-h-48 items-center justify-center rounded-sm border border-dashed border-border-subtle bg-surface-panel p-6 text-center text-body text-ink-muted">
      {children}
    </div>
  )
}

export function ErrorSurface({
  children,
  onRetry,
  retryLabel = 'Try again',
}: {
  children: ReactNode
  onRetry?: () => void
  retryLabel?: string
}) {
  return (
    <div role="alert" className="flex flex-wrap items-center justify-between gap-3 rounded-sm border border-border-subtle bg-danger-soft p-4 text-body text-danger">
      <p>{children}</p>
      {onRetry && (
        <AppButton variant="secondary" size="sm" onClick={onRetry}>
          {retryLabel}
        </AppButton>
      )}
    </div>
  )
}