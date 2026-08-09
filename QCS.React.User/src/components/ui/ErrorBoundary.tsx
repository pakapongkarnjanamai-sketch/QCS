import { Component, type ErrorInfo, type ReactNode } from 'react'
import { AppButton } from './AppButton'

interface ErrorBoundaryProps { children: ReactNode }
interface ErrorBoundaryState { hasError: boolean }

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { hasError: false }
  static getDerivedStateFromError(): ErrorBoundaryState { return { hasError: true } }
  componentDidCatch(error: Error, info: ErrorInfo): void { console.error('QCS portal render error.', error, info) }
  render(): ReactNode {
    return this.state.hasError
      ? <div className="grid min-h-full place-items-center bg-surface-app p-8"><section className="grid max-w-md gap-4 rounded-sm border border-border-subtle bg-surface-panel p-6"><h1 className="text-heading font-semibold text-ink-strong">The portal could not load this view.</h1><p className="text-body text-ink-muted">Reload the page to try again.</p><AppButton variant="secondary" onClick={() => globalThis.location.reload()}>Reload page</AppButton></section></div>
      : this.props.children
  }
}