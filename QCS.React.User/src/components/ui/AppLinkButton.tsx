import type { ReactNode } from 'react'
import { Link, type LinkProps } from 'react-router'
import { appButtonClassName, type AppButtonSize, type AppButtonVariant } from './appButtonStyles'

/**
 * Ported from QRS.Web — see PLANS/README.md rule 8.
 *
 * Exists so a link that looks like a button gets the button's styles from the same place a button
 * does. Hand-rolling those classes at the call site is how the "New request" action ended up two
 * pixels of padding away from every actual button beside it.
 */
type AppLinkButtonProps = Omit<LinkProps, 'className' | 'children'> & {
  variant?: AppButtonVariant
  size?: AppButtonSize
  className?: string
  children: ReactNode
}

export function AppLinkButton({ variant = 'primary', size = 'md', className = '', children, ...props }: AppLinkButtonProps) {
  return <Link {...props} className={appButtonClassName(variant, size, className)}>{children}</Link>
}
