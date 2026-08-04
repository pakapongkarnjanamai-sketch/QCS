import type { InputHTMLAttributes, ReactNode } from 'react'

interface FieldProps extends InputHTMLAttributes<HTMLInputElement> { label: string; error?: string; endAdornment?: ReactNode }

export function Field({ label, error, endAdornment, className = '', id, ...props }: FieldProps) {
  const inputId = id ?? label.toLowerCase().replaceAll(/[^a-z0-9]+/g, '-')
  return <label className="grid gap-1.5 text-body text-ink-strong" htmlFor={inputId}>
    <span>{label}</span>
    <span className="relative">
      <input id={inputId} className={`rounded-sm border border-border-subtle bg-white px-3 py-2 text-body text-ink-strong focus:border-accent focus:outline-2 focus:outline-offset-1 focus:outline-accent disabled:cursor-not-allowed disabled:opacity-50 ${className}`} {...props} />
      {endAdornment}
    </span>
    {error && <span className="text-caption text-danger">{error}</span>}
  </label>
}