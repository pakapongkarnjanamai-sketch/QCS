import type { ButtonHTMLAttributes, ReactNode } from 'react'

export interface IconButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  title?: string
  label?: string
  tone?: 'neutral' | 'primary' | 'danger' | 'success'
  size?: 'sm' | 'md' | 'lg'
  children: ReactNode
}

export function IconButton({
  title,
  label,
  tone = 'neutral',
  size = 'md',
  type = 'button',
  className = '',
  children,
  ...props
}: IconButtonProps) {
  const labelText = title ?? label ?? ''
  const toneClass = {
    neutral: 'text-ink-muted hover:bg-surface-muted hover:text-ink-strong',
    primary: 'text-accent hover:bg-surface-muted',
    danger: 'text-danger hover:bg-surface-muted',
    success: 'text-success hover:bg-surface-muted',
  }[tone]
  const sizeClass = { sm: 'size-9', md: 'size-10', lg: 'size-11' }[size]

  return (
    <button
      {...props}
      type={type}
      title={labelText || undefined}
      aria-label={props['aria-label'] ?? labelText}
      className={`inline-flex items-center justify-center rounded-sm ${sizeClass} ${toneClass} focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:cursor-not-allowed disabled:opacity-50 ${className}`}
    >
      {children}
    </button>
  )
}