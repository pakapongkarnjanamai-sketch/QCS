import type { ReactNode } from 'react'

export type TableColumn<TRow> = {
  key: keyof TRow & string
  label: string
  align?: 'left' | 'right'
  render?: (value: TRow[keyof TRow], row: TRow) => ReactNode
}

type TableSurfaceProps<TRow extends Record<string, unknown>> = {
  columns: TableColumn<TRow>[]
  rows: TRow[]
  rowKey: keyof TRow & string
  actionLabel?: string
  onAction?: (row: TRow) => void
}

export function TableSurface<TRow extends Record<string, unknown>>({
  columns,
  rows,
  rowKey,
  actionLabel = 'Inspect',
  onAction,
}: TableSurfaceProps<TRow>) {
  return (
    <div className="overflow-x-auto">
      <table className="w-full min-w-[640px] border-collapse text-left">
        <thead>
          <tr className="border-b border-[var(--border-subtle)] bg-[var(--surface-muted)]">
            {columns.map((col) => (
              <th
                key={col.key}
                className={`px-4 py-3 text-[11px] font-semibold uppercase tracking-[0.14em] text-[var(--ink-soft)] ${
                  col.align === 'right' ? 'text-right' : 'text-left'
                }`}
              >
                {col.label}
              </th>
            ))}
            {onAction && (
              <th className="px-4 py-3 text-right text-[11px] font-semibold uppercase tracking-[0.14em] text-[var(--ink-soft)]">
                Action
              </th>
            )}
          </tr>
        </thead>
        <tbody>
          {rows.map((row) => (
            <tr
              key={String(row[rowKey])}
              className="border-b border-[var(--border-subtle)] last:border-b-0"
            >
              {columns.map((col, colIndex) => (
                <td
                  key={col.key}
                  className={`px-4 py-4 align-top text-[13px] ${
                    colIndex === 0
                      ? 'font-medium text-[var(--ink-strong)]'
                      : col.align === 'right'
                        ? 'text-right text-[var(--ink-muted)]'
                        : 'leading-6 text-[var(--ink-muted)]'
                  }`}
                >
                  {col.render
                    ? col.render(row[col.key], row)
                    : (row[col.key] as ReactNode)}
                </td>
              ))}
              {onAction && (
                <td className="px-4 py-4 text-right align-top">
                  <button
                    type="button"
                    onClick={() => onAction(row)}
                    className="focus-ring inline-flex h-8 items-center justify-center rounded-sm border border-[var(--border-subtle)] px-3 text-[12px] font-medium text-[var(--ink-strong)]"
                  >
                    {actionLabel}
                  </button>
                </td>
              )}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}
