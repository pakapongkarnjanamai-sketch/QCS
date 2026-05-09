export type NavIcon =
  | 'overview'
  | 'requests'
  | 'quotations'
  | 'workflow'
  | 'vendors'
  | 'users'
  | 'departments'

export type NavItem = {
  to: string
  label: string
  icon: NavIcon
}

export type NavGroup = {
  label: string
  items: NavItem[]
}

export const NAV_GROUPS: NavGroup[] = [
  {
    label: 'Workspace',
    items: [
      { to: '/', label: 'Overview', icon: 'overview' },
      { to: '/requests', label: 'Requests', icon: 'requests' },
      { to: '/quotations', label: 'Quotations', icon: 'quotations' },
      { to: '/workflow', label: 'Workflow', icon: 'workflow' },
        { to: '/vendors', label: 'Vendors', icon: 'vendors' },
      { to: '/users', label: 'Users', icon: 'users' },
      { to: '/departments', label: 'Departments', icon: 'departments' },
    ],
  },
 
]

export const PAGE_TITLES = Object.fromEntries(
  NAV_GROUPS.flatMap((group) =>
    group.items.map((item) => [item.to, item.label] as const),
  ),
) as Record<string, string>

const titleEntries = Object.entries(PAGE_TITLES).sort(
  ([leftRoute], [rightRoute]) => rightRoute.length - leftRoute.length,
)

export const getPageTitle = (pathname: string) => {
  const match = titleEntries.find(([route]) => {
    if (route === '/') {
      return pathname === '/'
    }

    return pathname === route || pathname.startsWith(`${route}/`)
  })

  return match?.[1] ?? 'QCS Admin'
}