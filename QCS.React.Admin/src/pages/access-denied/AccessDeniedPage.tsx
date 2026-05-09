import { Link } from 'react-router-dom'

export function AccessDeniedPage() {
  return (
    <main className="flex min-h-screen items-center justify-center bg-(--surface-app) px-4 py-8 text-(--ink-strong)">
      <section className="w-full max-w-140 rounded-sm border border-(--border-subtle) bg-(--surface-panel) p-8">
        <p className="text-[11px] uppercase tracking-[0.16em] text-(--ink-soft)">403</p>
        <h1 className="mt-2 text-[28px] font-semibold leading-tight text-(--ink-strong)">
          Access denied
        </h1>
        <p className="mt-3 text-[13px] leading-6 text-(--ink-muted)">
          Your account does not have permission to open this page. If you believe this is a mistake,
          contact the QCS administrator or return to the overview screen.
        </p>
        <div className="mt-6 flex flex-wrap gap-2">
          <Link
            to="/"
            className="focus-ring inline-flex h-9 items-center justify-center rounded-sm border border-(--border-subtle) px-4 text-[12px] text-(--ink-strong)"
          >
            Go to overview
          </Link>
        </div>
      </section>
    </main>
  )
}
