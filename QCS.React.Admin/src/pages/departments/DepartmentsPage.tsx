export function DepartmentsPage() {
  return (
    <div className="flex min-h-90 items-center justify-center">
      <section className="w-full max-w-2xl rounded-sm border border-(--border-subtle) bg-(--surface-panel) px-6 py-8">
        <p className="text-[11px] font-semibold uppercase tracking-[0.14em] text-(--ink-soft)">
          Departments
        </p>
        <h2 className="mt-2 text-[24px] font-semibold leading-none text-(--ink-strong)">
          Under Development
        </h2>
        <p className="mt-3 text-[13px] leading-6 text-(--ink-muted)">
          This page is currently under development. Department management features will be
          available soon.
        </p>
      </section>
    </div>
  )
}