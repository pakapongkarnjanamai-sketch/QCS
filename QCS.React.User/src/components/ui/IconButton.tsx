import type { ButtonHTMLAttributes, ReactNode } from 'react'

/**
 * Sizing and tones match QRS.Web's IconButton — see PLANS/README.md rule 8. QCS's was fixed at
 * `size-9`, one step below QRS's default, with no tones at all.
 *
 * The focus-visible ring and disabled styling are QCS's own and are kept deliberately: they are
 * additive, they do not change how the button looks at rest, and dropping them to match QRS
 * exactly would trade a real affordance for nothing. Parity is a shared visual language, not a
 * pact to reproduce each other's omissions.
 */
interface IconButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  label: string
  tone?: 'neutral' | 'primary' | 'danger' | 'success'
  size?: 'sm' | 'md' | 'lg'
  children: ReactNode
}

export function IconButton({ className = '', label, tone = 'neutral', size = 'md', type = 'button', children, ...props }: IconButtonProps) {
  const toneClass = {
    neutral: 'text-ink-muted hover:bg-surface-muted hover:text-ink-strong',
    primary: 'text-accent hover:bg-surface-muted',
    danger: 'text-danger hover:bg-surface-muted',
    success: 'text-success hover:bg-surface-muted',
  }[tone]
  const sizeClass = { sm: 'size-9', md: 'size-10', lg: 'size-11' }[size]

  return <button type={type} aria-label={label} title={label} className={`inline-flex items-center justify-center rounded-sm ${sizeClass} ${toneClass} focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:cursor-not-allowed disabled:opacity-50 ${className}`} {...props}>{children}</button>
}