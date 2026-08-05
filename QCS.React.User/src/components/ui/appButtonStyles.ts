/*
  Ported verbatim from QRS.Web's appButtonStyles.ts — see PLANS/README.md rule 8.

  QCS's own button was a different control: no size scale, so every button was px-3 py-2 with no
  minimum height; a danger tone drawn as an outline where QRS draws it solid; a white background
  instead of the surface-panel token; and no inline-flex, gap or transition, so an icon and its
  label sat flush and the hover had no easing. Buttons are the most repeated element on any
  screen, which made it one of the loudest differences between the two portals.
*/
export type AppButtonVariant = 'primary' | 'secondary' | 'danger' | 'ghost'
export type AppButtonSize = 'sm' | 'md'

export function appButtonClassName(
  variant: AppButtonVariant = 'primary',
  size: AppButtonSize = 'md',
  className = '',
) {
  const variantClass = {
    primary: 'bg-accent text-white hover:bg-accent-hover',
    secondary: 'border border-border-subtle bg-surface-panel text-ink-strong hover:bg-surface-muted',
    danger: 'bg-danger text-white hover:opacity-90',
    ghost: 'text-ink-muted hover:bg-surface-muted hover:text-ink-strong',
  }[variant]
  const sizeClass = size === 'sm' ? 'min-h-9 px-3 text-caption' : 'min-h-11 px-4 text-body'

  return `inline-flex items-center justify-center gap-2 rounded-sm font-medium transition-colors focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent disabled:cursor-not-allowed disabled:opacity-50 ${variantClass} ${sizeClass} ${className}`
}
