import { useMemo, useState } from 'react'
import DataGrid, { Column, FilterRow, HeaderFilter, RemoteOperations, Scrolling } from 'devextreme-react/data-grid'
import { appConfig } from '../../config/appConfig.ts'
import { createDataSource } from '../../lib/createDataSource.ts'

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

const ACCESS_LEVELS: AccessLevel[] = ['User', 'Manager', 'Admin', 'SuperAdmin']

export function UserAccessPage() {
  const [nIdInput, setNIdInput] = useState('')
  const [accessLevelInput, setAccessLevelInput] = useState<AccessLevel>('User')
  const [busy, setBusy] = useState(false)
  const [message, setMessage] = useState('')
  const [refreshKey, setRefreshKey] = useState(0)

  const dataSource = useMemo(
    () => createDataSource<UserAccessRow>('/api/UserAccess/Grid', 'id'),
    [refreshKey],
  )

  const runMutation = async (executor: () => Promise<void>, successMessage: string) => {
    setBusy(true)
    setMessage('')

    try {
      await executor()
      setMessage(successMessage)
      setRefreshKey((prev) => prev + 1)
    } catch (error) {
      setMessage(error instanceof Error ? error.message : 'Request failed.')
    } finally {
      setBusy(false)
    }
  }

  const registerUser = async () => {
    const normalized = nIdInput.trim().toUpperCase()
    if (!normalized) {
      setMessage('Please provide NID.')
      return
    }

    await runMutation(async () => {
      const response = await fetch(`${appConfig.apiBaseUrl}/api/UserAccess/Register`, {
        method: 'POST',
        credentials: 'include',
        headers: {
          'Content-Type': 'application/json',
        },
        body: JSON.stringify({ nId: normalized, accessLevel: accessLevelInput }),
      })

      if (!response.ok) {
        const text = await response.text().catch(() => response.statusText)
        throw new Error(`Register failed (${response.status}): ${text}`)
      }

      setNIdInput('')
    }, `Saved ${normalized}.`)
  }

  const updateAccessLevel = async (row: UserAccessRow, level: AccessLevel) => {
    await runMutation(async () => {
      const response = await fetch(`${appConfig.apiBaseUrl}/api/UserAccess/${row.id}/AccessLevel`, {
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
    }, `Updated ${row.nId} to ${level}.`)
  }

  const toggleActive = async (row: UserAccessRow) => {
    await runMutation(async () => {
      const response = await fetch(`${appConfig.apiBaseUrl}/api/UserAccess/${row.id}/AccessLevel`, {
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
    }, `${row.nId} ${row.isActive ? 'disabled' : 'enabled'}.`)
  }

  const refreshProfile = async (row: UserAccessRow) => {
    await runMutation(async () => {
      const response = await fetch(`${appConfig.apiBaseUrl}/api/UserAccess/${row.id}/Refresh`, {
        method: 'POST',
        credentials: 'include',
      })

      if (!response.ok) {
        const text = await response.text().catch(() => response.statusText)
        throw new Error(`Refresh failed (${response.status}): ${text}`)
      }
    }, `Refreshed ${row.nId} from employee lookup.`)
  }

  const removeUser = async (row: UserAccessRow) => {
    if (!window.confirm(`Delete ${row.nId} from QCS access list?`)) {
      return
    }

    await runMutation(async () => {
      const response = await fetch(`${appConfig.apiBaseUrl}/api/UserAccess/${row.id}`, {
        method: 'DELETE',
        credentials: 'include',
      })

      if (!response.ok) {
        const text = await response.text().catch(() => response.statusText)
        throw new Error(`Delete failed (${response.status}): ${text}`)
      }
    }, `Deleted ${row.nId}.`)
  }

  return (
    <div className="flex flex-col gap-3 flex-1 min-h-0">
      <section className="shrink-0 rounded-sm border border-(--border-subtle) bg-(--surface-panel) p-4">
        <div className="flex flex-col gap-2 lg:flex-row lg:items-end">
          <label className="flex min-w-[140px] flex-col gap-1 text-[12px] text-(--ink-muted)">
            NID
            <input
              type="text"
              value={nIdInput}
              onChange={(event) => setNIdInput(event.target.value.toUpperCase())}
              placeholder="N4734"
              className="h-9 rounded-sm border border-(--border-subtle) bg-white px-3 text-[13px] text-(--ink-strong)"
              disabled={busy}
            />
          </label>

          <label className="flex min-w-[160px] flex-col gap-1 text-[12px] text-(--ink-muted)">
            Access Level
            <select
              value={accessLevelInput}
              onChange={(event) => setAccessLevelInput(event.target.value as AccessLevel)}
              className="h-9 rounded-sm border border-(--border-subtle) bg-white px-3 text-[13px] text-(--ink-strong)"
              disabled={busy}
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
            onClick={() => void registerUser()}
            disabled={busy}
            className="focus-ring h-9 rounded-sm border border-(--border-subtle) px-4 text-[12px] text-(--ink-strong) disabled:cursor-not-allowed disabled:opacity-50"
          >
            Register / Update NID
          </button>
        </div>

        {message ? (
          <p className="mt-2 text-[12px] text-(--ink-muted)">{message}</p>
        ) : null}
      </section>

      <section className="flex-1 min-h-0 overflow-hidden rounded-sm border border-(--border-subtle) bg-(--surface-panel)">
        <DataGrid
          dataSource={dataSource}
          keyExpr="id"
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
