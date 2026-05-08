export type WorkspaceTextItem = {
  label: string
  value: string
}

export type WorkspaceRow = {
  name: string
  context: string
  owner: string
  updated: string
}

export type WorkspaceDefinition = {
  path: string
  title: string
  eyebrow: string
  description: string
  primaryAction: string
  secondaryAction: string
  focusItems: WorkspaceTextItem[]
  toolbarFilters: string[]
  tableTitle: string
  tableDescription: string
  rows: WorkspaceRow[]
  sideTitle: string
  sideItems: WorkspaceTextItem[]
}

export const workspacePages: WorkspaceDefinition[] = [
  {
    path: '/departments',
    title: 'Departments',
    eyebrow: 'Structure and ownership',
    description:
      'Admin page for department master data, request ownership, and approval routing anchors.',
    primaryAction: 'Add department',
    secondaryAction: 'Sync owners',
    focusItems: [
      {
        label: 'Scope',
        value:
          'Department metadata used by requests, approvals, and reporting.',
      },
      {
        label: 'Review path',
        value:
          'Maintain source data first so downstream routing stays predictable.',
      },
      { label: 'Operator', value: 'System and master-data administrators.' },
    ],
    toolbarFilters: ['All departments', 'Mapped owners', 'Needs review'],
    tableTitle: 'Department registry',
    tableDescription:
      'Layout-ready CRUD surface for organizational data and approval anchors.',
    rows: [
      {
        name: 'Facilities',
        context:
          'Owns renovation, maintenance, and site service procurement requests.',
        owner: 'N. Suwan',
        updated: '08:15',
      },
      {
        name: 'Marketing',
        context: 'Campaign production and print purchasing workflows.',
        owner: 'P. Anan',
        updated: '09:05',
      },
      {
        name: 'Operations',
        context:
          'Urgent material requests and recurring vendor service management.',
        owner: 'K. Sirin',
        updated: '09:45',
      },
    ],
    sideTitle: 'Governance notes',
    sideItems: [
      {
        label: 'Data responsibility',
        value:
          'Department ownership data should be maintained here, not inferred inside feature pages.',
      },
      {
        label: 'Layout rule',
        value:
          'Keep shell structure stable while detail tools scale inside the content area.',
      },
      {
        label: 'Future expansion',
        value:
          'This page can grow into forms and side panels without changing nav or header contracts.',
      },
    ],
  },
]