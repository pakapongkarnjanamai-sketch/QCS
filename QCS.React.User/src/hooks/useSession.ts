import { useCallback, useEffect, useState } from 'react'
import { apiClient, toApiError, type ApiError } from '@/lib/apiClient'
import type { SessionResponse } from '@/types'

interface SessionState {
  data?: SessionResponse
  error?: ApiError
  loading: boolean
  reload: () => void
  hasRole: (role: string) => boolean
}

export function useSession(): SessionState {
  const [state, setState] = useState<{ data?: SessionResponse; error?: ApiError; loading: boolean }>({ loading: true })
  const [reloadToken, setReloadToken] = useState(0)

  const reload = useCallback(() => setReloadToken((n) => n + 1), [])

  useEffect(() => {
    let active = true
    void apiClient
      .get<SessionResponse>('/Session/Me')
      .then(({ data }) => {
        if (active) setState({ data, loading: false })
      })
      .catch((error: unknown) => {
        if (active) setState({ error: toApiError(error), loading: false })
      })
    return () => {
      active = false
    }
  }, [reloadToken])

  const hasRole = useCallback(
    (role: string) =>
      state.data?.accessLevel?.some((r) => r.toLowerCase() === role.toLowerCase()) ?? false,
    [state.data],
  )

  return { ...state, reload, hasRole }
}