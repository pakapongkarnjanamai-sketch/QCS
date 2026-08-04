import { Component, type ErrorInfo, type ReactNode } from 'react'

interface ErrorBoundaryProps { children: ReactNode }
interface ErrorBoundaryState { hasError: boolean }

export class ErrorBoundary extends Component<ErrorBoundaryProps, ErrorBoundaryState> {
  state: ErrorBoundaryState = { hasError: false }
  static getDerivedStateFromError(): ErrorBoundaryState { return { hasError: true } }
  componentDidCatch(error: Error, info: ErrorInfo): void { console.error('QCS portal render error.', error, info) }
  render(): ReactNode { return this.state.hasError ? <div className="grid min-h-full place-items-center p-8 text-body text-ink-muted">The portal could not load this view.</div> : this.props.children }
}