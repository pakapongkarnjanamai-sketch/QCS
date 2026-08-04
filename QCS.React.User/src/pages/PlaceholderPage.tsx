import { EmptySurface } from '@/components/ui/Surfaces'

export function PlaceholderPage({ title }: { title: string }) {
  return <div className="grid gap-6"><header><h1 className="text-title font-semibold">{title}</h1></header><EmptySurface>This view is not available yet.</EmptySurface></div>
}