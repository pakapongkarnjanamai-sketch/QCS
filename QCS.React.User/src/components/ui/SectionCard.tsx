import type { ReactNode } from 'react'

interface SectionCardProps {
  title: ReactNode
  description?: ReactNode
  action?: ReactNode
  children: ReactNode
  className?: string
  invalid?: boolean
}

export function SectionCard({ title, description, action, children, className = '', invalid = false }: SectionCardProps) {
  return (
    <section className={`rounded-sm border border-border-subtle bg-surface-panel ${className}`} data-invalid={invalid ? 'true' : undefined}>
      <header className="flex flex-wrap items-center justify-between gap-3 border-b border-border-subtle px-4 py-3">
        <div className="min-w-0">
          <h2 className="text-caption font-semibold uppercase tracking-[0.12em] text-ink-muted">{title}</h2>
          {description && <p className="mt-0.5 text-caption text-ink-muted">{description}</p>}
        </div>
        {action && <div className="flex flex-wrap items-center gap-2">{action}</div>}
      </header>
      {children}
    </section>
  )
}