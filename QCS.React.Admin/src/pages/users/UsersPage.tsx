import { useMemo, useState } from 'react'
import DataGrid, { Column, FilterRow, HeaderFilter, RemoteOperations, Scrolling } from 'devextreme-react/data-grid'
import { appConfig } from '../../config/appConfig.ts'
import { fetchWithAccessControl } from '../../lib/apiClient.ts'
import { createDataSource } from '../../lib/createDataSource.ts'
import { toast } from '../../lib/toast.ts'

type UserAccessRow = {
  id: number
  nId: string
  fullName: string
  employeeId: string
  division: string
  department: string
  section: string
  position: string
  costCenter: string
  email: string
  accessLevel: 'User' | 'Manager' | 'Admin' | 'SuperAdmin'
  isActive: boolean
  lastSyncedAt: string
}

type AccessLevel = UserAccessRow['accessLevel']

type UserAccessPreview = {
  nId: string
  employeeId: string
  fullName: string
  division: string
  department: string
  section: string
  position: string
  costCenter: string
  email: string
}

const ACCESS_LEVELS: AccessLevel[] = ['User', 'Manager', 'Admin', 'SuperAdmin']

const normalizeNId = (value: string) => value.trim().toUpperCase()

export function UserAccessPage() {
  const [nIdInput, setNIdInput] = useState('')
  const [accessLevelInput, setAccessLevelInput] = useState<AccessLevel>('User')
  const [preview, setPreview] = useState<UserAccessPreview | null>(null)
  const [previewLoading, setPreviewLoading] = useState(false)
  const [busy, setBusy] = useState(false)
  const [refreshKey, setRefreshKey] = useState(0)

  const normalizedNId = useMemo(() => normalizeNId(nIdInput), [nIdInput])
  const dataSource = useMemo(
    () => createDataSource<UserAccessRow>('/api/UserAccess/Grid', 'id'),
    [],
  )

  const previewIsCurrent = preview?.nId === normalizedNId
  const canRegister = Boolean(normalizedNId && previewIsCurrent)

  const runMutation = async (executor: () => Promise<void>) => {
    setBusy(true)

    try {
      await executor()
      setRefreshKey((prev) => prev + 1)
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Request failed.'
      toast.error(message)
    } finally {
      setBusy(false)
    }
  }

  const previewUser = async () => {
    if (!normalizedNId) {
      toast.warning('Please provide NID.')
      return
    }

    setPreviewLoading(true)

    try {
      const response = await fetchWithAccessControl(
        `${appConfig.apiBaseUrl}/api/UserAccess/Preview?nId=${encodeURIComponent(normalizedNId)}`,
        {
          credentials: 'include',
        },
      )

      if (!response.ok) {
        const text = await response.text().catch(() => response.statusText)
        throw new Error(`Preview failed (${response.status}): ${text}`)
      }

      const payload = (await response.json()) as UserAccessPreview
      setPreview(payload)
    } catch (error) {
      setPreview(null)
      const message = error instanceof Error ? error.message : 'Preview failed.'
      toast.error(message)
    } finally {
      setPreviewLoading(false)
    }
  }

  const registerUser = async () => {
    if (!normalizedNId) {
      toast.warning('Please provide NID.')
      return
    }

    if (!previewIsCurrent) {
      toast.warning('Load a preview before registering.')
      return
    }

    await runMutation(async () => {
      const response = await fetchWithAccessControl(`${appConfig.apiBaseUrl}/api/UserAccess/Register`, {
        method: 'POST',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ nId: normalizedNId, accessLevel: accessLevelInput }),
      })

      if (!response.ok) {
        const text = await response.text().catch(() => response.statusText)
        throw new Error(`Register failed (${response.status}): ${text}`)
      }

      setNIdInput('')
      setPreview(null)
    })
  }

  const updateAccessLevel = async (row: UserAccessRow, level: AccessLevel) => {
    await runMutation(async () => {
      const response = await fetchWithAccessControl(`${appConfig.apiBaseUrl}/api/UserAccess/${row.id}/AccessLevel`, {
        method: 'PUT',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ accessLevel: level, isActive: row.isActive }),
      })

      if (!response.ok) {
        const text = await response.text().catch(() => response.statusText)
        throw new Error(`Update failed (${response.status}): ${text}`)
      }
    })
  }

  const toggleActive = async (row: UserAccessRow) => {
    await runMutation(async () => {
      const response = await fetchWithAccessControl(`${appConfig.apiBaseUrl}/api/UserAccess/${row.id}/AccessLevel`, {
        method: 'PUT',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ accessLevel: row.accessLevel, isActive: !row.isActive }),
      })

      if (!response.ok) {
        const text = await response.text().catch(() => response.statusText)
        throw new Error(`Update failed (${response.status}): ${text}`)
      }
    })
  }

  const refreshProfile = async (row: UserAccessRow) => {
    await runMutation(async () => {
      const response = await fetchWithAccessControl(`${appConfig.apiBaseUrl}/api/UserAccess/${row.id}/Refresh`, {
        method: 'POST',
        credentials: 'include',
      })

      if (!response.ok) {
        const text = await response.text().catch(() => response.statusText)
        throw new Error(`Refresh failed (${response.status}): ${text}`)
      }
    })
  }

  const removeUser = async (row: UserAccessRow) => {
    if (!window.confirm(`Delete ${row.nId} from QCS access list?`)) {
      return
    }

    await runMutation(async () => {
      const response = await fetchWithAccessControl(`${appConfig.apiBaseUrl}/api/UserAccess/${row.id}`, {
        method: 'DELETE',
        credentials: 'include',
      })

      if (!response.ok) {
        const text = await response.text().catch(() => response.statusText)
        throw new Error(`Delete failed (${response.status}): ${text}`)
      }
    })
  }

  return (
    <div className="flex flex-col gap-3 flex-1 min-h-0">
      <section className="shrink-0 rounded-sm border border-(--border-subtle) bg-(--surface-panel) p-4">
        <div className="flex flex-col gap-3">
          <div className="flex flex-col gap-2 lg:flex-row lg:items-end">
            <label className="flex min-w-35 flex-col gap-1 text-[12px] text-(--ink-muted)">
              NID
              <input
                type="text"
                value={nIdInput}
                onChange={(event) => {
                  setNIdInput(event.target.value.toUpperCase())
                  setPreview(null)
                }}
                placeholder="N4734"
                className="h-9 rounded-sm border border-(--border-subtle) bg-white px-3 text-[13px] text-(--ink-strong)"
                disabled={busy || previewLoading}
              />
            </label>

            <label className="flex min-w-40 flex-col gap-1 text-[12px] text-(--ink-muted)">
              Access Level
              <select
                value={accessLevelInput}
                onChange={(event) => setAccessLevelInput(event.target.value as AccessLevel)}
                className="h-9 rounded-sm border border-(--border-subtle) bg-white px-3 text-[13px] text-(--ink-strong)"
                disabled={busy || previewLoading}
              >
                {ACCESS_LEVELS.map((level) => (
                  <option key={level} value={level}>
                    {level}
                  </option>
                ))}
              </select>
            </label>

            <button
              type="button"
              onClick={() => void previewUser()}
              disabled={busy || previewLoading}
              className="focus-ring h-9 rounded-sm border border-(--border-subtle) px-4 text-[12px] text-(--ink-strong) disabled:cursor-not-allowed disabled:opacity-50"
            >
              {previewLoading ? 'Loading preview...' : 'Preview profile'}
            </button>

            <button
              type="button"
              onClick={() => void registerUser()}
              disabled={busy || previewLoading || !canRegister}
              className="focus-ring h-9 rounded-sm border border-(--border-subtle) px-4 text-[12px] text-(--ink-strong) disabled:cursor-not-allowed disabled:opacity-50"
            >
              Register access
            </button>
          </div>

          <div className="rounded-sm border border-(--border-subtle) bg-(--surface-muted) p-3">
            <div className="flex items-center justify-between gap-3">
              <p className="text-[12px] font-medium text-(--ink-strong)">Preview</p>
              <p className="text-[11px] uppercase tracking-[0.14em] text-(--ink-soft)">
                {preview?.nId || 'Not loaded'}
              </p>
            </div>

            {preview ? (
              <dl className="mt-3 grid gap-3 sm:grid-cols-2 lg:grid-cols-4">
                <div>
                  <dt className="text-[11px] uppercase tracking-[0.12em] text-(--ink-soft)">Name</dt>
                  <dd className="mt-1 text-[13px] text-(--ink-strong)">{preview.fullName || '—'}</dd>
                </div>
                <div>
                  <dt className="text-[11px] uppercase tracking-[0.12em] text-(--ink-soft)">Employee ID</dt>
                  <dd className="mt-1 text-[13px] text-(--ink-strong)">{preview.employeeId || '—'}</dd>
                </div>
                <div>
                  <dt className="text-[11px] uppercase tracking-[0.12em] text-(--ink-soft)">Department</dt>
                  <dd className="mt-1 text-[13px] text-(--ink-strong)">{preview.department || '—'}</dd>
                </div>
                <div>
                  <dt className="text-[11px] uppercase tracking-[0.12em] text-(--ink-soft)">Position</dt>
                  <dd className="mt-1 text-[13px] text-(--ink-strong)">{preview.position || '—'}</dd>
                </div>
                <div>
                  <dt className="text-[11px] uppercase tracking-[0.12em] text-(--ink-soft)">Division</dt>
                  <dd className="mt-1 text-[13px] text-(--ink-strong)">{preview.division || '—'}</dd>
                </div>
                <div>
                  <dt className="text-[11px] uppercase tracking-[0.12em] text-(--ink-soft)">Section</dt>
                  <dd className="mt-1 text-[13px] text-(--ink-strong)">{preview.section || '—'}</dd>
                </div>
                <div>
                  <dt className="text-[11px] uppercase tracking-[0.12em] text-(--ink-soft)">Cost Center</dt>
                  <dd className="mt-1 text-[13px] text-(--ink-strong)">{preview.costCenter || '—'}</dd>
                </div>
                <div>
                  <dt className="text-[11px] uppercase tracking-[0.12em] text-(--ink-soft)">Email</dt>
                  <dd className="mt-1 text-[13px] text-(--ink-strong)">{preview.email || '—'}</dd>
                </div>
              </dl>
            ) : (
              <p className="mt-3 text-[12px] text-(--ink-muted)">
                Load a preview first to confirm the employee profile before registering access.
              </p>
            )}
          </div>
        </div>

      </section>

      <section className="flex-1 min-h-0 overflow-hidden rounded-sm border border-(--border-subtle) bg-(--surface-panel)">
        <DataGrid
          key={refreshKey}
          dataSource={dataSource}
          showBorders={false}
          showColumnLines={false}
          showRowLines={true}
          rowAlternationEnabled={false}
          columnAutoWidth={true}
          wordWrapEnabled={false}
          noDataText="No registered users"
          height="100%"
        >
          <RemoteOperations filtering paging sorting grouping={false} summary={false} />
          <FilterRow visible={true} />
          <HeaderFilter visible={true} />
          <Scrolling mode="virtual" rowRenderingMode="virtual" />

          <Column dataField="nId" caption="NID" width={120} />
          <Column dataField="fullName" caption="Name" minWidth={180} />
          <Column dataField="department" caption="Department" minWidth={160} />
          <Column dataField="position" caption="Position" minWidth={160} />
          <Column dataField="email" caption="Email" minWidth={180} />
          <Column dataField="accessLevel" caption="Access" width={120} />
          <Column dataField="isActive" caption="Active" width={90} />
          <Column
            dataField="lastSyncedAt"
            caption="Synced"
            width={160}
            dataType="datetime"
            format="yyyy-MM-dd HH:mm"
          />
          <Column
            caption="Action"
            minWidth={300}
            allowFiltering={false}
            allowSorting={false}
            cellRender={(cell) => {
              const row = cell.data as UserAccessRow
              const isRoot = row.nId.toUpperCase() === 'N4734'

              return (
                <div className="flex items-center gap-2">
                  <select
                    value={row.accessLevel}
                    disabled={busy || isRoot}
                    onChange={(event) => void updateAccessLevel(row, event.target.value as AccessLevel)}
                    className="h-8 rounded-sm border border-(--border-subtle) bg-white px-2 text-[12px] text-(--ink-strong)"
                  >
                    {ACCESS_LEVELS.map((level) => (
                      <option key={level} value={level}>
                        {level}
                      </option>
                    ))}
                  </select>

                  <button
                    type="button"
                    disabled={busy || isRoot}
                    onClick={() => void toggleActive(row)}
                    className="focus-ring h-8 rounded-sm border border-(--border-subtle) px-2 text-[11px] text-(--ink-strong) disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    {row.isActive ? 'Disable' : 'Enable'}
                  </button>

                  <button
                    type="button"
                    disabled={busy}
                    onClick={() => void refreshProfile(row)}
                    className="focus-ring h-8 rounded-sm border border-(--border-subtle) px-2 text-[11px] text-(--ink-strong) disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    Refresh
                  </button>

                  <button
                    type="button"
                    disabled={busy || isRoot}
                    onClick={() => void removeUser(row)}
                    className="focus-ring h-8 rounded-sm border border-(--border-subtle) px-2 text-[11px] text-(--ink-strong) disabled:cursor-not-allowed disabled:opacity-50"
                  >
                    Delete
                  </button>
                </div>
              )
            }}
          />
        </DataGrid>
      </section>
    </div>
  )
}
