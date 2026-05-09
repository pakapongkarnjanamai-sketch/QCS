import CustomStore from 'devextreme/data/custom_store'
import type { LoadOptions } from 'devextreme/data'
import { appConfig } from '../config/appConfig.ts'
import { fetchWithAccessControl } from './apiClient.ts'

type LoadResult<T> = {
  data: T[]
  totalCount: number
  summary?: unknown[]
  groupCount?: number
}

const DX_LOAD_OPTION_KEYS: (keyof LoadOptions)[] = [
  'filter',
  'group',
  'groupSummary',
  'parentIds',
  'requireGroupCount',
  'requireTotalCount',
  'searchExpr',
  'searchOperation',
  'searchValue',
  'select',
  'sort',
  'skip',
  'take',
  'totalSummary',
  'userData',
]

function isNotEmpty(value: unknown): boolean {
  return value !== undefined && value !== null && value !== ''
}

/**
 * Build a DevExtreme CustomStore that forwards LoadOptions to a
 * DevExtreme.AspNet.Data endpoint (DataSourceLoader.Load on the server).
 *
 * @param path  API path relative to appConfig.apiBaseUrl, e.g. "/api/Request/MyRequests"
 * @param key   Primary key field name (default: "id")
 */
export function createDataSource<T extends object>(path: string, key: keyof T = 'id' as keyof T) {
  return new CustomStore<T>({
    key: key as string,
    load: async (loadOptions: LoadOptions): Promise<LoadResult<T>> => {
      const params = new URLSearchParams()

      DX_LOAD_OPTION_KEYS.forEach((k) => {
        const value = loadOptions[k]
        if (k in loadOptions && isNotEmpty(value)) {
          params.set(k, JSON.stringify(value))
        }
      })

      const url = `${appConfig.apiBaseUrl}${path}?${params.toString()}`
      const response = await fetchWithAccessControl(url, { credentials: 'include' })

      if (!response.ok) {
        const text = await response.text().catch(() => response.statusText)
        throw new Error(`API error ${response.status}: ${text}`)
      }

      const contentType = response.headers.get('content-type') ?? ''
      if (!contentType.includes('application/json')) {
        const preview = await response.text().catch(() => '')
        throw new Error(
          `Expected JSON but got "${contentType}" from ${path}.` +
          (preview.trimStart().startsWith('<') ? ' Server returned HTML — check CORS, authentication, and apiBaseUrl in .env.' : ''),
        )
      }

      return response.json() as Promise<LoadResult<T>>
    },
  })
}
