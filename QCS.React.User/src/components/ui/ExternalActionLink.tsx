import { ExternalLink } from 'lucide-react'
import type { AnchorHTMLAttributes, ReactNode } from 'react'
import { appButtonClassName, type AppButtonSize, type AppButtonVariant } from './appButtonStyles'

type ExternalActionLinkProps = Omit<AnchorHTMLAttributes<HTMLAnchorElement>, 'children' | 'href' | 'rel' | 'target'> & {
  href: string
  children: ReactNode
  variant?: AppButtonVariant
  size?: AppButtonSize
}

export function ExternalActionLink({ href, children, variant = 'secondary', size = 'sm', className = '', ...props }: ExternalActionLinkProps) {
  return (
    <a href={href} target="_blank" rel="noreferrer" className={appButtonClassName(variant, size, className)} {...props}>
      {children}
      <ExternalLink className="size-3.5" aria-hidden />
    </a>
  )
}