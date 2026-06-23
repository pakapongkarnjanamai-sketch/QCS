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
]