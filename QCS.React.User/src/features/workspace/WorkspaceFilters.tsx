import { Search, X } from 'lucide-react'
import { IconButton } from '@/components/ui/IconButton'
import { workspaceViewLabels, workspaceViews, type WorkspaceView } from './types'

interface WorkspaceFiltersProps {
  view: WorkspaceView
  search: string
  sortBy?: string
  sortDescending: boolean
  onViewChange: (view: WorkspaceView) => void
  onSearchChange: (value: string) => void
  onSearchSubmit: () => void
  onSearchClear: () => void
  onSortChange: (sortBy?: string, sortDescending?: boolean) => void
}

export function WorkspaceFilters({ view, search, sortBy, sortDescending, onViewChange, onSearchChange, onSearchSubmit, onSearchClear, onSortChange }: WorkspaceFiltersProps) {
  return <section className="flex flex-wrap items-center gap-3 border-y border-border-subtle py-3">
    <label className="relative min-w-56 flex-1"><span className="sr-only">Search requests</span><Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-soft" aria-hidden /><input type="search" value={search} onChange={(event) => onSearchChange(event.target.value)} onKeyDown={(event) => { if (event.key === 'Enter') onSearchSubmit() }} placeholder="Search requests" className="w-full rounded-sm border border-border-subtle bg-white py-2 pl-9 pr-9 text-body focus:border-accent focus:outline-2 focus:outline-offset-1 focus:outline-accent" />{search && <IconButton className="absolute right-0 top-1/2 -translate-y-1/2" label="Clear search" onClick={onSearchClear}><X size={16} /></IconButton>}</label>
    <label className="grid gap-1 text-caption text-ink-muted"><span className="sr-only">Request view</span><select value={view} onChange={(event) => onViewChange(event.target.value as WorkspaceView)} className="rounded-sm border border-border-subtle bg-white px-3 py-2 text-body text-ink-strong focus:border-accent focus:outline-2 focus:outline-offset-1 focus:outline-accent">{workspaceViews.map((option) => <option key={option} value={option}>{workspaceViewLabels[option]}</option>)}</select></label>
    <label className="grid gap-1 text-caption text-ink-muted"><span className="sr-only">Sort requests</span><select value={`${sortBy ?? 'requestdate'}:${sortDescending ? 'desc' : 'asc'}`} onChange={(event) => { const [nextSortBy, direction] = event.target.value.split(':'); onSortChange(nextSortBy, direction === 'desc') }} className="rounded-sm border border-border-subtle bg-white px-3 py-2 text-body text-ink-strong focus:border-accent focus:outline-2 focus:outline-offset-1 focus:outline-accent"><option value="requestdate:desc">Newest first</option><option value="requestdate:asc">Oldest first</option><option value="code:asc">Code A-Z</option><option value="title:asc">Title A-Z</option><option value="vendorname:asc">Vendor A-Z</option><option value="status:asc">Status</option></select></label>
  </section>
}