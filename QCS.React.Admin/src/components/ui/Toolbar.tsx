type ToolbarProps = {
  title: string
  description?: string
  searchPlaceholder?: string
  filters?: string[]
  activeFilterIndex?: number
  onSearch?: (value: string) => void
  onFilterChange?: (index: number) => void
}

export function Toolbar({
  title,
  description,
  searchPlaceholder = 'Search…',
  filters = [],
  activeFilterIndex = 0,
  onSearch,
  onFilterChange,
}: ToolbarProps) {
  return (
    <div className="flex flex-col gap-3 border-b border-[var(--border-subtle)] px-4 py-4 lg:flex-row lg:items-end lg:justify-between">
      <div className="space-y-1">
        <h3 className="text-[16px] font-semibold text-[var(--ink-strong)]">{title}</h3>
        {description && (
          <p className="max-w-[70ch] text-[13px] leading-6 text-[var(--ink-muted)]">{description}</p>
        )}
      </div>

      <div className="flex flex-wrap items-center gap-2 lg:justify-end">
        <label className="min-w-[220px] flex-1 lg:max-w-[260px]">
          <span className="sr-only">{searchPlaceholder}</span>
          <input
            type="search"
            placeholder={searchPlaceholder}
            onChange={(e) => onSearch?.(e.target.value)}
            className="focus-ring h-9 w-full rounded-sm border border-[var(--border-subtle)] bg-[var(--surface-panel)] px-3 text-[13px] text-[var(--ink-strong)] placeholder:text-[var(--ink-soft)]"
          />
        </label>

        {filters.map((filter, index) => (
          <button
            key={filter}
            type="button"
            onClick={() => onFilterChange?.(index)}
            className={`focus-ring inline-flex h-8 items-center justify-center rounded-sm border border-[var(--border-subtle)] px-3 text-[12px] font-medium ${
              index === activeFilterIndex
                ? 'bg-[var(--surface-muted)] text-[var(--ink-strong)]'
                : 'bg-[var(--surface-panel)] text-[var(--ink-muted)]'
            }`}
          >
            {filter}
          </button>
        ))}
      </div>
    </div>
  )
}
