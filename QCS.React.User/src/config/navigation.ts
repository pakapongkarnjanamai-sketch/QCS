import { ClipboardList, FileText, House, type LucideIcon } from 'lucide-react'

export interface NavigationItem { label: string; path: string; icon: LucideIcon }

export const navigation: NavigationItem[] = [
  { label: 'Overview', path: '/', icon: House },
  { label: 'Requests', path: '/?view=my-requests', icon: ClipboardList },
  { label: 'Quotations', path: '/quotations', icon: FileText },
]