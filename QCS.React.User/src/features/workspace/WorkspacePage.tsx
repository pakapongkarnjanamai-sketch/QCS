import { useCallback, useEffect, useRef, useState } from 'react'
import { FileText, Inbox, Plus } from 'lucide-react'
import { Link, useSearchParams } from 'react-router'
import { AppButton } from '@/components/ui/AppButton'
import { EmptySurface, ErrorSurface, LoadingSurface } from '@/components/ui/Surfaces'
import { toApiError, type ApiError } from '@/lib/apiClient'
import { useSignalR } from '@/hooks/useSignalR'
import { getPortalRequests, getWorkspaceSummary } from './api'
import { RequestTable } from './RequestTable'
import { WorkspaceFilters } from './WorkspaceFilters'
import { WorkspaceSummary } from './WorkspaceSummary'
import { workspaceViews, type PortalPage, type PortalRequestListItem, type WorkspaceSummaryData, type WorkspaceView } from './types'

const PAGE_SIZE = 30
function isWorkspaceView(value: string | null): value is WorkspaceView {
  return value !== null && (workspaceViews as readonly string[]).includes(value)
}

interface WorkspacePageProps {
  defaultView: WorkspaceView
  showSummary?: boolean
  title?: string
  description?: string
  showCreateAction?: boolean
  lockView?: boolean
  returnPath?: string
  emptyMessage?: string
  emptyIcon?: 'file' | 'inbox'
}

export function WorkspacePage({ defaultView, showSummary = false, title, description, showCreateAction = true, lockView = false, returnPath = '/requests', emptyMessage = 'No requests found.', emptyIcon = 'file' }: WorkspacePageProps) {
  const [searchParams, setSearchParams] = useSearchParams()
  const [data, setData] = useState<PortalPage<PortalRequestListItem>>()
  const [summary, setSummary] = useState<WorkspaceSummaryData>()
  const [loading, setLoading] = useState(true)
  const [refreshing, setRefreshing] = useState(false)
  const [loadingMore, setLoadingMore] = useState(false)
  const [error, setError] = useState<ApiError>()
  const [loadMoreError, setLoadMoreError] = useState<ApiError>()
  const [retryToken, setRetryToken] = useState(0)
  const [searchInput, setSearchInput] = useState('')
  const hasDataRef = useRef(false)
  const firstPageControllerRef = useRef<AbortController | undefined>(undefined)
  const morePageControllerRef = useRef<AbortController | undefined>(undefined)
  const tableScrollRef = useRef<HTMLDivElement>(null)
  const sentinelRef = useRef<HTMLTableRowElement>(null)

  const view = lockView ? defaultView : isWorkspaceView(searchParams.get('view')) ? (searchParams.get('view') as WorkspaceView) : defaultView
  const search = searchParams.get('q') ?? ''
  const sortBy = searchParams.get('sort') || 'requestdate'
  const sortDescending = searchParams.get('desc') !== 'false'

  const updateParams = useCallback(
    (changes: Record<string, string | null>) => {
      const next = new URLSearchParams(searchParams)
      for (const [key, value] of Object.entries(changes)) {
        if (value) next.set(key, value)
        else next.delete(key)
      }
      setSearchParams(next, { replace: true })
    },
    [searchParams, setSearchParams],
  )

  useEffect(() => setSearchInput(search), [search])
  useEffect(() => {
    if (searchInput === search) return undefined
    const timer = window.setTimeout(() => updateParams({ q: searchInput || null }), 400)
    return () => window.clearTimeout(timer)
  }, [search, searchInput, updateParams])

  useEffect(() => {
    firstPageControllerRef.current?.abort()
    morePageControllerRef.current?.abort()
    const controller = new AbortController()
    firstPageControllerRef.current = controller
    setLoading(!hasDataRef.current)
    setRefreshing(hasDataRef.current)
    setLoadingMore(false)
    setError(undefined)
    setLoadMoreError(undefined)
    void Promise.all([getPortalRequests({ view, search, page: 1, pageSize: PAGE_SIZE, sortBy, sortDescending }, controller.signal), getWorkspaceSummary(controller.signal)])
      .then(([nextData, nextSummary]) => {
        hasDataRef.current = true
        setData(nextData)
        setSummary(nextSummary)
      })
      .catch((reason: unknown) => {
        if (!controller.signal.aborted) setError(toApiError(reason))
      })
      .finally(() => {
        if (!controller.signal.aborted) {
          setLoading(false)
          setRefreshing(false)
        }
      })
    return () => controller.abort()
  }, [view, search, sortBy, sortDescending, retryToken])

  const loadMore = useCallback(
    (retry = false) => {
      if (!data?.hasNextPage || refreshing || loadingMore || (!retry && loadMoreError)) return
      morePageControllerRef.current?.abort()
      const controller = new AbortController()
      morePageControllerRef.current = controller
      setLoadingMore(true)
      setLoadMoreError(undefined)
      void getPortalRequests(
        {
          view,
          search,
          page: data.page + 1,
          pageSize: PAGE_SIZE,
          sortBy,
          sortDescending,
        },
        controller.signal,
      )
        .then((nextPage) => setData((current) => (current && current.page + 1 === nextPage.page ? { ...nextPage, items: [...current.items, ...nextPage.items] } : current)))
        .catch((reason: unknown) => {
          if (!controller.signal.aborted) setLoadMoreError(toApiError(reason))
        })
        .finally(() => {
          if (!controller.signal.aborted) setLoadingMore(false)
        })
    },
    [data, loadMoreError, loadingMore, refreshing, search, sortBy, sortDescending, view],
  )

  useEffect(() => {
    const root = tableScrollRef.current
    const sentinel = sentinelRef.current
    if (!root || !sentinel || !data?.hasNextPage || refreshing || loadingMore || loadMoreError) return undefined
    const observer = new IntersectionObserver(
      ([entry]) => {
        if (entry.isIntersecting) loadMore()
      },
      { root, rootMargin: '0px 0px 120px' },
    )
    observer.observe(sentinel)
    return () => observer.disconnect()
  }, [data, loadMore, loadMoreError, loadingMore, refreshing])

  const invalidate = useCallback(() => setRetryToken((token) => token + 1), [])
  useSignalR('ReceiveUpdate', invalidate)
  useSignalR('RequestUpdated', invalidate)

  const changeSort = (key: string) =>
    updateParams({
      sort: key,
      desc: sortBy === key && sortDescending ? 'false' : 'true',
    })
  const returnSearch = searchParams.toString()

  return (
    <div className="flex min-h-full flex-col gap-6">
      <header className="flex flex-col gap-4 sm:flex-row sm:items-start sm:justify-between">
        <div>
          <h1 className="text-title font-semibold">{title ?? (showSummary ? 'Dashboard' : 'Quotation requests')}</h1>
          <p className="mt-1 text-body text-ink-muted">{description ?? (showSummary ? 'Your quotation request workspace.' : 'Manage quotation requests and sourcing progress.')}</p>
        </div>
        {showCreateAction && <Link to="/requests/new" state={{ workspaceSearch: returnSearch }} className="inline-flex items-center justify-center gap-2 rounded-sm bg-accent px-3 py-2 text-body font-medium text-white hover:bg-accent-hover focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent">
          <Plus size={16} aria-hidden />
          New request
        </Link>}
      </header>
      {showSummary && <WorkspaceSummary data={summary} activeView={view} onViewChange={(nextView) => updateParams({ view: nextView })} />}
      <WorkspaceFilters
        view={view}
        search={searchInput}
        sortBy={sortBy}
        sortDescending={sortDescending}
        onViewChange={(nextView) => updateParams({ view: nextView })}
        showViewFilter={!lockView}
        onSearchChange={setSearchInput}
        onSearchSubmit={() => updateParams({ q: searchInput || null })}
        onSearchClear={() => {
          setSearchInput('')
          updateParams({ q: null })
        }}
        onSortChange={(nextSortBy, nextDescending) =>
          updateParams({
            sort: nextSortBy ?? null,
            desc: nextDescending === false ? 'false' : null,
          })
        }
      />
      {loading && !data && <LoadingSurface />}
      {error && (
        <ErrorSurface>
          <div className="flex items-center justify-between gap-3">
            <span>
              {error.title}
              {data ? ' Showing the previous results.' : ''}
            </span>
            <AppButton tone="secondary" onClick={invalidate}>
              Try again
            </AppButton>
          </div>
        </ErrorSurface>
      )}
      {data &&
        (data.items.length > 0 ? (
          <RequestTable data={data} refreshing={refreshing} loadingMore={loadingMore} loadMoreError={loadMoreError} returnSearch={returnSearch} returnPath={returnPath} sortBy={sortBy} sortDescending={sortDescending} onSort={changeSort} onRetryLoadMore={() => loadMore(true)} tableScrollRef={tableScrollRef} sentinelRef={sentinelRef} />
        ) : (
          <EmptySurface>
            <div className="grid justify-items-center gap-3">
              {emptyIcon === 'inbox' ? <Inbox size={28} className="text-ink-soft" aria-hidden /> : <FileText size={28} className="text-ink-soft" aria-hidden />}
              <span>{emptyMessage}</span>
              {search && (
                <AppButton
                  tone="secondary"
                  onClick={() => {
                    setSearchInput('')
                    updateParams({ q: null })
                  }}
                >
                  Clear search
                </AppButton>
              )}
            </div>
          </EmptySurface>
        ))}
    </div>
  )
}
