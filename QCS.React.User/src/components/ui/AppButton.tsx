import type { ButtonHTMLAttributes } from 'react'
import { appButtonClassName, type AppButtonSize, type AppButtonVariant } from './appButtonStyles'

/** Matches QRS.Web's AppButton, including its prop names — see PLANS/README.md rule 8. */
type AppButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: AppButtonVariant
  size?: AppButtonSize
}

export function AppButton({ className = '', variant = 'primary', size = 'md', type = 'button', ...props }: AppButtonProps) {
  return <button type={type} className={appButtonClassName(variant, size, className)} {...props} />
}
