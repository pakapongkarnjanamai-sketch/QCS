import type { ButtonHTMLAttributes } from 'react'

type AppButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & { tone?: 'primary' | 'secondary' | 'danger' }

export function AppButton({ className = '', tone = 'primary', type = 'button', ...props }: AppButtonProps) {
  const toneClass = tone === 'primary'
    ? 'bg-accent text-white hover:bg-accent-hover'
    : tone === 'danger'
      ? 'border border-danger bg-white text-danger hover:bg-red-50'
      : 'border border-border-subtle bg-white text-ink-strong hover:bg-surface-muted'
  return <button type={type} className={`rounded-sm px-3 py-2 text-body font-medium focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:cursor-not-allowed disabled:opacity-50 ${toneClass} ${className}`} {...props} />
}