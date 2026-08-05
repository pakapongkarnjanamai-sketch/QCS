import type { ReactNode } from 'react'

export function Field({ label, required, error, children }: { label: string; required?: boolean; error?: string; children: ReactNode }) {
  return <label className="block" data-invalid={error ? 'true' : undefined}>
    <span className="mb-1 block text-caption font-medium uppercase tracking-[0.08em] text-ink-muted">
      {label}{required && <span className="ml-0.5 text-danger">*</span>}
    </span>
    {children}
    {error && <span className="mt-1 block text-caption text-danger">{error}</span>}
  </label>
}