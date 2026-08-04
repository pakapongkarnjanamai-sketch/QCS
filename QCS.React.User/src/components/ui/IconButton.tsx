import type { ButtonHTMLAttributes, ReactNode } from 'react'

interface IconButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> { label: string; children: ReactNode }

export function IconButton({ className = '', label, type = 'button', children, ...props }: IconButtonProps) {
  return <button type={type} aria-label={label} title={label} className={`grid size-9 place-items-center rounded-sm text-ink-muted hover:bg-surface-muted hover:text-ink-strong focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:cursor-not-allowed disabled:opacity-50 ${className}`} {...props}>{children}</button>
}