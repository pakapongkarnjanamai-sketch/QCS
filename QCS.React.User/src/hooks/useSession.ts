import { useEffect, useState } from 'react'
import { apiClient, toApiError, type ApiError } from '@/lib/apiClient'
import type { SessionResponse } from '@/types'

interface SessionState {
  data?: SessionResponse
  error?: ApiError
  loading: boolean
}

export function useSession(): SessionState {
  const [state, setState] = useState<SessionState>({ loading: true })

  useEffect(() => {
    let active = true
    void apiClient.get<SessionResponse>('/Session/Me')
      .then(({ data }) => { if (active) setState({ data, loading: false }) })
      .catch((error: unknown) => { if (active) setState({ error: toApiError(error), loading: false }) })
    return () => { active = false }
  }, [])

  return state
}