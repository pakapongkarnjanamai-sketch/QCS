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
    path: '/',
    title: 'Overview',
    eyebrow: 'Operations shell',
    description:
      'Single route shell for day-to-day administration, review queues, and operational follow-up.',
    primaryAction: 'New request',
    secondaryAction: 'Open board',
    focusItems: [
      {
        label: 'Focus',
        value:
          'Daily procurement control surface for request, quote, and vendor follow-up.',
      },
      { label: 'Review path', value: 'Intake -> sourcing -> approval -> release.' },
      { label: 'Operator', value: 'Admin and procurement coordinators.' },
    ],
    toolbarFilters: ['All queues', 'Needs review', 'Due today'],
    tableTitle: 'Active work surface',
    tableDescription:
      'Placeholder grid structure for the first layout slice. Real data sources plug in later.',
    rows: [
      {
        name: 'Pending request intake',
        context: 'New demand from departments waiting for initial triage.',
        owner: 'Procurement desk',
        updated: '08:45',
      },
      {
        name: 'Vendor follow-up',
        context:
          'Open quotation windows that still need supplier response tracking.',
        owner: 'Sourcing team',
        updated: '09:10',
      },
      {
        name: 'Approval handoff',
        context:
          'Reviewed items ready to move from commercial check to approval.',
        owner: 'Admin lead',
        updated: '09:35',
      },
      {
        name: 'Workflow exceptions',
        context:
          'Schedules and escalations that need manual intervention today.',
        owner: 'Operations',
        updated: '10:00',
      },
    ],
    sideTitle: 'Shell notes',
    sideItems: [
      {
        label: 'Header ownership',
        value:
          'Page title, environment, and mobile navigation stay in the global shell.',
      },
      {
        label: 'Sidebar ownership',
        value:
          'Primary routes only. No page-local filters or workflow actions belong here.',
      },
      {
        label: 'Content ownership',
        value:
          'Every page keeps its own toolbar, filters, and main working surface.',
      },
    ],
  },
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