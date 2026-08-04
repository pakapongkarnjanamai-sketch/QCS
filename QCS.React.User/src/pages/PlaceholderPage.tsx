import { Link, useLocation } from 'react-router'
import { EmptySurface } from '@/components/ui/Surfaces'

export function PlaceholderPage({ title }: { title: string }) {
  const location = useLocation()
  const workspaceSearch = (location.state as { workspaceSearch?: string } | null)?.workspaceSearch
  const workspaceTarget = workspaceSearch ? `/?${workspaceSearch}` : '/'
  return <div className="grid gap-6"><header><h1 className="text-title font-semibold">{title}</h1></header><EmptySurface><div className="grid justify-items-center gap-3"><span>This view is not available yet.</span>{workspaceSearch && <Link to={workspaceTarget} className="text-accent underline decoration-1 underline-offset-2 hover:text-accent-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">Back to requests</Link>}</div></EmptySurface></div>
}