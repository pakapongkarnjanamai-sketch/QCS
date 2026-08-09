import type { ReactNode } from 'react'

export function FormPage({ children }: { children: ReactNode }) {
  return <div className="mx-auto max-w-4xl space-y-6">{children}</div>
}

export function FormPageHeader({
  title,
  description,
  status,
  actions,
}: {
  title: ReactNode
  description: ReactNode
  status?: ReactNode
  actions?: ReactNode
}) {
  return (
    <header className="flex flex-wrap items-start justify-between gap-3">
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-2">
          <h1 className="text-title font-semibold">{title}</h1>
          {status}
        </div>
        <p className="mt-1 text-body text-ink-muted">{description}</p>
      </div>
      {actions && <div className="flex flex-wrap items-center justify-end gap-2">{actions}</div>}
    </header>
  )
}

export function FormSummary({ children }: { children: ReactNode }) {
  return (
    <dl className="grid gap-x-6 gap-y-3 border border-border-subtle bg-surface-muted px-4 py-3 text-body sm:grid-cols-3">
      {children}
    </dl>
  )
}

export function FormSummaryItem({
  label,
  children,
  title,
  truncate = false,
}: {
  label: string
  children: ReactNode
  title?: string
  truncate?: boolean
}) {
  return (
    <div className="min-w-0">
      <dt className="text-caption font-medium text-ink-muted">{label}</dt>
      <dd className={`mt-0.5 font-medium text-ink-strong ${truncate ? 'truncate' : 'wrap-break-word'}`} title={title}>
        {children}
      </dd>
    </div>
  )
}

export function FormSection({ children }: { children: ReactNode }) {
  return <div className="space-y-4 rounded-sm border border-border-subtle bg-surface-panel p-4">{children}</div>
}

export function FormActions({ children }: { children: ReactNode }) {
  return <footer className="flex flex-wrap justify-end gap-2">{children}</footer>
}