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
    path: '/workflow',
    title: 'Workflow',
    eyebrow: 'Execution control',
    description:
      'Operational page for approval paths, escalations, and recurring handoff checks.',
    primaryAction: 'Add rule',
    secondaryAction: 'View audit',
    focusItems: [
      {
        label: 'Scope',
        value:
          'Approval routing, escalation thresholds, and operator-owned interventions.',
      },
      {
        label: 'Review path',
        value:
          'Observe state changes, correct broken paths, then resume processing.',
      },
      { label: 'Operator', value: 'Admins with workflow responsibility.' },
    ],
    toolbarFilters: ['All rules', 'Paused', 'Needs attention'],
    tableTitle: 'Workflow monitor',
    tableDescription:
      'Layout placeholder for execution and approval monitoring screens.',
    rows: [
      {
        name: 'Capital purchase approval',
        context:
          'Escalates to finance lead when amount exceeds policy threshold.',
        owner: 'Approval admin',
        updated: '08:20',
      },
      {
        name: 'Vendor onboarding path',
        context:
          'Compliance review and document validation before activation.',
        owner: 'Vendor admin',
        updated: '09:00',
      },
      {
        name: 'Urgent request exception',
        context:
          'Shortened path used when urgent operational flag is raised.',
        owner: 'Operations',
        updated: '09:30',
      },
    ],
    sideTitle: 'Operational guardrails',
    sideItems: [
      {
        label: 'Execution safety',
        value:
          'Keep rule ownership and intervention controls visible without cluttering the header.',
      },
      {
        label: 'Audit visibility',
        value:
          'Manual changes should land in a detail route or panel, not inside nav chrome.',
      },
      {
        label: 'Recovery path',
        value: 'Failures need clear operator context inside the page body.',
      },
    ],
  },
  {
    path: '/vendors',
    title: 'Vendors',
    eyebrow: 'Master data',
    description:
      'Administration page for supplier records, onboarding readiness, and contact governance.',
    primaryAction: 'Add vendor',
    secondaryAction: 'Import list',
    focusItems: [
      { label: 'Scope', value: 'Approved, onboarding, and inactive vendor records.' },
      {
        label: 'Review path',
        value:
          'Verify compliance, maintain contacts, then keep sourcing lists current.',
      },
      { label: 'Operator', value: 'Vendor management and admin users.' },
    ],
    toolbarFilters: ['All vendors', 'Onboarding', 'Inactive'],
    tableTitle: 'Vendor registry',
    tableDescription:
      'Quiet admin surface for CRUD and review workflows without dashboard decoration.',
    rows: [
      {
        name: 'Northline Packaging',
        context:
          'Primary packaging supplier with annual contract renewal due next month.',
        owner: 'Vendor desk',
        updated: '08:05',
      },
      {
        name: 'Siam Safety Works',
        context:
          'New onboarding record missing insurance certificate attachment.',
        owner: 'M. Chanin',
        updated: '09:15',
      },
      {
        name: 'Delta Industrial Supply',
        context:
          'Legacy supplier flagged for contact and bank detail verification.',
        owner: 'P. Tana',
        updated: '09:55',
      },
    ],
    sideTitle: 'Master data rules',
    sideItems: [
      {
        label: 'Primary interaction',
        value:
          'List-first navigation keeps edits fast and audit trails discoverable.',
      },
      {
        label: 'Detail strategy',
        value:
          'Deep compliance details should open in a drawer or dedicated route.',
      },
      {
        label: 'Visual style',
        value:
          'Neutral borders and text hierarchy carry the structure instead of color blocks.',
      },
    ],
  },
  {
    path: '/users',
    title: 'Users',
    eyebrow: 'Identity administration',
    description:
      'Operator page for user access, ownership mapping, and workflow assignments.',
    primaryAction: 'Add user',
    secondaryAction: 'Review roles',
    focusItems: [
      {
        label: 'Scope',
        value:
          'Internal users, access ownership, and route-level responsibilities.',
      },
      {
        label: 'Review path',
        value: 'Validate identity source, assign role, then map operating area.',
      },
      { label: 'Operator', value: 'System administrators.' },
    ],
    toolbarFilters: ['All users', 'Pending access', 'Inactive'],
    tableTitle: 'User directory',
    tableDescription:
      'Foundation page for future Windows-auth role visibility and administration.',
    rows: [
      {
        name: 'Arisa Jittra',
        context: 'Buyer assigned to packaging and display categories.',
        owner: 'Admin',
        updated: '08:30',
      },
      {
        name: 'Preecha Narin',
        context:
          'Department approver for facilities and maintenance requests.',
        owner: 'Admin',
        updated: '09:20',
      },
      {
        name: 'Sirin Kul',
        context:
          'Procurement coordinator with vendor onboarding scope.',
        owner: 'Admin',
        updated: '09:50',
      },
    ],
    sideTitle: 'Access notes',
    sideItems: [
      {
        label: 'Auth model',
        value:
          'Server-side authorization decisions stay outside the client shell.',
      },
      {
        label: 'Shell behavior',
        value:
          'Header utilities remain global; role mapping tools live inside the page body.',
      },
      {
        label: 'Responsiveness',
        value: 'Text actions remain reachable even on narrow screens.',
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