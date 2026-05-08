import type { ReactNode } from 'react'

export type SidePanelItem = {
  label: string
  value: ReactNode
}

type SidePanelProps = {
  title: string
  items: SidePanelItem[]
}

export function SidePanel({ title, items }: SidePanelProps) {
  return (
    <aside className="overflow-hidden rounded-sm border border-[var(--border-subtle)] bg-[var(--surface-panel)]">
      <div className="border-b border-[var(--border-subtle)] px-4 py-4">
        <h3 className="text-[16px] font-semibold text-[var(--ink-strong)]">{title}</h3>
      </div>

      <dl className="divide-y divide-[var(--border-subtle)]">
        {items.map((item) => (
          <div key={item.label} className="px-4 py-4">
            <dt className="text-[11px] font-semibold uppercase tracking-[0.14em] text-[var(--ink-soft)]">
              {item.label}
            </dt>
            <dd className="mt-1 text-[13px] leading-6 text-[var(--ink-strong)]">{item.value}</dd>
          </div>
        ))}
      </dl>
    </aside>
  )
}
