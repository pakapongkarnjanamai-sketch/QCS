import { Search, X } from 'lucide-react'
import { IconButton } from '@/components/ui/IconButton'
import { appInputClassName } from '@/components/ui/inputStyles'
import { workspaceViewLabels, workspaceViews, type WorkspaceView } from './types'

interface WorkspaceFiltersProps {
  view: WorkspaceView
  search: string
  sortBy?: string
  sortDescending: boolean
  showViewFilter?: boolean
  onViewChange: (view: WorkspaceView) => void
  onSearchChange: (value: string) => void
  onSearchSubmit: () => void
  onSearchClear: () => void
  onSortChange: (sortBy?: string, sortDescending?: boolean) => void
}

export function WorkspaceFilters({ view, search, sortBy, sortDescending, showViewFilter = true, onViewChange, onSearchChange, onSearchSubmit, onSearchClear, onSortChange }: WorkspaceFiltersProps) {
  // Card, not a bare strip: QRS's filter row is `rounded-sm border bg-surface-panel p-3` with
  // gap-2. This was listed in PLAN-042's analysis table and never actually done — the section had
  // top-and-bottom rules and no background instead. Controls go through appInputClassName for the
  // same reason, so their padding, height and disabled states come from where QRS's come from
  // rather than being retyped here.
  return <section className="flex shrink-0 flex-wrap items-center gap-2 rounded-sm border border-border-subtle bg-surface-panel p-3">
    <div className="relative min-w-56 flex-1">
      <Search className="pointer-events-none absolute left-2.5 top-1/2 size-4 -translate-y-1/2 text-ink-soft" aria-hidden />
      <input type="search" aria-label="Search requests" value={search} onChange={(event) => onSearchChange(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter') onSearchSubmit() }} placeholder="Search requests" className={appInputClassName('md', `w-full pl-8 ${search ? 'pr-9' : 'pr-3'}`)} />
      {search && <IconButton className="absolute right-0 top-1/2 -translate-y-1/2" label="Clear search" onClick={onSearchClear}><X className="size-4" /></IconButton>}
    </div>
    {showViewFilter && <select aria-label="Request view" value={view} onChange={(event) => onViewChange(event.target.value as WorkspaceView)} className={appInputClassName('md', 'w-auto text-ink-strong')}>{workspaceViews.map((option) => <option key={option} value={option}>{workspaceViewLabels[option]}</option>)}</select>}
    <select aria-label="Sort requests" value={`${sortBy ?? 'requestdate'}:${sortDescending ? 'desc' : 'asc'}`} onChange={(event) => { const [nextSortBy, direction] = event.target.value.split(':'); onSortChange(nextSortBy, direction === 'desc') }} className={appInputClassName('md', 'w-auto text-ink-strong')}>
      <option value="requestdate:desc">Newest first</option>
      <option value="requestdate:asc">Oldest first</option>
      <option value="code:asc">Code A-Z</option>
      <option value="title:asc">Title A-Z</option>
      <option value="vendorname:asc">Vendor A-Z</option>
      <option value="status:asc">Status</option>
    </select>
  </section>
}