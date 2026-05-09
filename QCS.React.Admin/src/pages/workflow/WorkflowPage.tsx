import { useEffect, useMemo, useState } from 'react'
import { appConfig } from '../../config/appConfig.ts'
import { fetchWithAccessControl } from '../../lib/apiClient.ts'

type AssignmentDto = {
  nId: string
  employeeName: string
  assignmentType: string
  isCurrentUser: boolean
}

type WorkflowStepDto = {
  id: number
  sequenceNo: number
  stepName: string
  assignments: AssignmentDto[]
}

type WorkflowRouteDetailDto = {
  id: number
  routeName: string
  canInitiate: boolean
  steps: WorkflowStepDto[]
}

type WorkflowListItem = {
  id: number
  routeName: string
  version: string
  stepCount: number
  eligibleUsers: number
  isActive: boolean
}

const FUTURE_ACTIONS = ['Create', 'Edit', 'Clone', 'Activate', 'Deactivate', 'Set default'] as const

export function WorkflowPage() {
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [routeData, setRouteData] = useState<WorkflowRouteDetailDto | null>(null)
  const [selectedRouteId, setSelectedRouteId] = useState<number | null>(1)

  const myStepCount = useMemo(
    () =>
      (routeData?.steps ?? []).filter((step) =>
        (step.assignments ?? []).some((assignment) => assignment.isCurrentUser),
      ).length,
    [routeData?.steps],
  )

  const eligibleUsers = useMemo(() => {
    const users = new Set<string>()

    ;(routeData?.steps ?? []).forEach((step) => {
      ;(step.assignments ?? []).forEach((assignment) => {
        if (assignment.nId) {
          users.add(assignment.nId.toUpperCase())
        }
      })
    })

    return users.size
  }, [routeData?.steps])

  const workflowList = useMemo<WorkflowListItem[]>(() => {
    if (!routeData) return []

    return [
      {
        id: routeData.id,
        routeName: routeData.routeName,
        version: `Fixed v1 (Route ${routeData.id})`,
        stepCount: routeData.steps?.length ?? 0,
        eligibleUsers,
        isActive: true,
      },
    ]
  }, [eligibleUsers, routeData])

  const selectedRoute = useMemo(
    () => workflowList.find((item) => item.id === selectedRouteId),
    [selectedRouteId, workflowList],
  )

  async function loadRoute(parsedRouteId: number) {
    setIsLoading(true)
    setError(null)

    try {
      const response = await fetchWithAccessControl(`${appConfig.apiBaseUrl}/api/Workflow/route/${parsedRouteId}`, {
        credentials: 'include',
      })

      if (!response.ok) {
        if (response.status === 404) throw new Error('Workflow route not found.')
        throw new Error(`Cannot load workflow route (${response.status}).`)
      }

      const payload = (await response.json()) as WorkflowRouteDetailDto
      setRouteData(payload)
      setSelectedRouteId(payload.id)
    } catch (fetchError: unknown) {
      const message = fetchError instanceof Error ? fetchError.message : 'Cannot load workflow route.'
      setRouteData(null)
      setError(message)
    } finally {
      setIsLoading(false)
    }
  }

  useEffect(() => {
    void loadRoute(1)
  }, [])

  return (
    <div className="space-y-4">
      <section className="overflow-hidden rounded-sm border border-(--border-subtle) bg-(--surface-panel)">
        <div className="flex flex-wrap items-center justify-between gap-2 border-b border-(--border-subtle) px-4 py-3">
          <div>
            <h2 className="text-[16px] font-semibold text-(--ink-strong)">Workflow List</h2>
            <p className="text-[13px] text-(--ink-muted)">
              Read-only list for multi-workflow UX. Currently showing a single active workflow.
            </p>
          </div>
          <div className="flex flex-wrap gap-1">
            {FUTURE_ACTIONS.map((action) => (
              <button
                key={action}
                type="button"
                disabled={true}
                className="inline-flex h-8 items-center justify-center rounded-sm border border-(--border-subtle) bg-(--surface-muted) px-3 text-[12px] text-(--ink-soft)"
                title="Coming soon"
              >
                {action}
              </button>
            ))}
          </div>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full min-w-220 border-collapse text-left">
            <thead>
              <tr className="border-b border-(--border-subtle) bg-(--surface-muted)">
                <th className="px-4 py-3 text-[11px] uppercase tracking-[0.14em] text-(--ink-soft)">Active</th>
                <th className="px-4 py-3 text-[11px] uppercase tracking-[0.14em] text-(--ink-soft)">Route Name</th>
                <th className="px-4 py-3 text-[11px] uppercase tracking-[0.14em] text-(--ink-soft)">Version</th>
                <th className="px-4 py-3 text-[11px] uppercase tracking-[0.14em] text-(--ink-soft)">Step Count</th>
                <th className="px-4 py-3 text-[11px] uppercase tracking-[0.14em] text-(--ink-soft)">Eligible Users</th>
              </tr>
            </thead>
            <tbody>
              {isLoading && (
                <tr>
                  <td colSpan={5} className="px-4 py-6 text-center text-[13px] text-(--ink-soft)">
                    Loading workflow list...
                  </td>
                </tr>
              )}

              {!isLoading && workflowList.map((item) => (
                <tr
                  key={item.id}
                  className={`cursor-pointer border-b border-(--border-subtle) last:border-b-0 ${
                    selectedRouteId === item.id ? 'bg-(--surface-muted)' : 'bg-(--surface-panel)'
                  }`}
                  onClick={() => setSelectedRouteId(item.id)}
                >
                  <td className="px-4 py-3 text-[13px] text-(--ink-strong)">{item.isActive ? 'Yes' : 'No'}</td>
                  <td className="px-4 py-3 text-[13px] font-medium text-(--ink-strong)">{item.routeName}</td>
                  <td className="px-4 py-3 text-[13px] text-(--ink-muted)">{item.version}</td>
                  <td className="px-4 py-3 text-[13px] text-(--ink-muted)">{item.stepCount}</td>
                  <td className="px-4 py-3 text-[13px] text-(--ink-muted)">{item.eligibleUsers}</td>
                </tr>
              ))}

              {!isLoading && !error && workflowList.length === 0 && (
                <tr>
                  <td colSpan={5} className="px-4 py-6 text-center text-[13px] text-(--ink-soft)">
                    No workflow found.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      {error && (
        <div className="rounded-sm border border-(--border-subtle) bg-(--surface-panel) px-4 py-3 text-[13px] text-(--ink-strong)">
          {error}
        </div>
      )}

      {routeData && selectedRoute && (
        <section className="overflow-hidden rounded-sm border border-(--border-subtle) bg-(--surface-panel)">
          <div className="flex flex-wrap items-center justify-between gap-2 border-b border-(--border-subtle) px-4 py-3">
            <h3 className="text-[16px] font-semibold text-(--ink-strong)">
              Step Permissions Detail
            </h3>
          </div>

          <div className="px-4 py-3 text-[13px] text-(--ink-muted)">
            <span className="font-medium text-(--ink-strong)">{selectedRoute.routeName}</span>
            {' | '}
            Can initiate: <span className="font-medium text-(--ink-strong)">{routeData.canInitiate ? 'Yes' : 'No'}</span>
            {' | '}
            My eligible steps: <span className="font-medium text-(--ink-strong)">{myStepCount}</span>
          </div>

          <div className="overflow-x-auto border-t border-(--border-subtle)">
            <table className="w-full min-w-220 border-collapse text-left">
              <thead>
                <tr className="border-b border-(--border-subtle) bg-(--surface-muted)">
                  <th className="px-4 py-3 text-[11px] uppercase tracking-[0.14em] text-(--ink-soft)">Step</th>
                  <th className="px-4 py-3 text-[11px] uppercase tracking-[0.14em] text-(--ink-soft)">Name</th>
                  <th className="px-4 py-3 text-[11px] uppercase tracking-[0.14em] text-(--ink-soft)">Eligible Users</th>
                </tr>
              </thead>
              <tbody>
                {(routeData.steps ?? []).map((step) => (
                  <tr key={step.id} className="border-b border-(--border-subtle) align-top last:border-b-0">
                    <td className="px-4 py-3 text-[13px] font-medium text-(--ink-strong)">{step.sequenceNo}</td>
                    <td className="px-4 py-3 text-[13px] text-(--ink-strong)">{step.stepName}</td>
                    <td className="px-4 py-3 text-[13px] text-(--ink-muted)">
                      <div className="space-y-1">
                        {(step.assignments ?? []).length === 0 && <p>-</p>}
                        {(step.assignments ?? []).map((assignment) => (
                          <p key={`${step.id}_${assignment.nId}_${assignment.assignmentType}`}>
                            {assignment.employeeName} ({assignment.nId})
                            {assignment.assignmentType ? ` - ${assignment.assignmentType}` : ''}
                            {assignment.isCurrentUser ? ' (You)' : ''}
                          </p>
                        ))}
                      </div>
                    </td>
                  </tr>
                ))}
                {(routeData.steps ?? []).length === 0 && (
                  <tr>
                    <td colSpan={3} className="px-4 py-6 text-center text-[13px] text-(--ink-soft)">
                      No step permission data.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </section>
      )}
    </div>
  )
}
