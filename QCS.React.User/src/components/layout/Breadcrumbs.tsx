import { useLocation } from 'react-router'
import { navigation } from '@/config/navigation'

export function Breadcrumbs() {
  const { pathname } = useLocation()
  const match = navigation.find((item) => item.path !== '/' && pathname.startsWith(item.path.split('?')[0]))
  return <p className="truncate text-body font-medium text-ink-strong">{match?.label ?? 'Overview'}</p>
}