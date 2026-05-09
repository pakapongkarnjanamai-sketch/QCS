import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import DataGrid, {
  Column,
  FilterRow,
  HeaderFilter,
  Lookup,
  RemoteOperations,
  Scrolling,
} from 'devextreme-react/data-grid'
import type { RowClickEvent } from 'devextreme/ui/data_grid'
import { createDataSource } from '../../lib/createDataSource.ts'
import { appConfig } from '../../config/appConfig.ts'
import { fetchWithAccessControl } from '../../lib/apiClient.ts'

const portalBase = appConfig.portalBaseUrl

type QuotationRow = {
  id: number
  code: string
  title: string
  vendorCode: string
  vendorName: string
  requestDate: string
  currentStepId: number
  requesterName: string
  remark: string
  validFrom: string | null
  validUntil: string | null
}

type QuotationItem = {
  id: number
  fileName: string
  originalFileName: string
  documentTypeId: number
}

type RequestDetail = {
  requestId: number
  code: string
  title: string
  vendorCode: string
  vendorName: string
  requestDate: string
  validFrom: string | null
  validUntil: string | null
  remark: string | null
  requesterName: string
  quotations: QuotationItem[]
}

type ViewMode = 'original' | 'merged'

type PdfDocumentState = {
  url: string
  fileName: string
  source: ViewMode
}

function getFileNameFromContentDisposition(value: string | null): string | null {
  if (!value) return null

  const utf8Match = value.match(/filename\*=UTF-8''([^;]+)/i)
  if (utf8Match?.[1]) {
    try {
      return decodeURIComponent(utf8Match[1])
    } catch {
      return utf8Match[1]
    }
  }

  const basicMatch = value.match(/filename="?([^";]+)"?/i)
  return basicMatch?.[1] ?? null
}

function DetailPane({ code, onClose }: { code: string; onClose: () => void }) {
  const [detail, setDetail] = useState<RequestDetail | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [activeQuotationId, setActiveQuotationId] = useState<number | null>(null)
  const [viewMode, setViewMode] = useState<ViewMode>('original')
  const [pdfDoc, setPdfDoc] = useState<PdfDocumentState | null>(null)
  const [pdfLoading, setPdfLoading] = useState(false)
  const [pdfError, setPdfError] = useState<string | null>(null)
  const currentObjectUrlRef = useRef<string | null>(null)

  useEffect(() => {
    return () => {
      if (currentObjectUrlRef.current) {
        URL.revokeObjectURL(currentObjectUrlRef.current)
        currentObjectUrlRef.current = null
      }
    }
  }, [])

  function setPdfDocument(blob: Blob, fallbackFileName: string, source: ViewMode, responseFileName: string | null) {
    if (currentObjectUrlRef.current) {
      URL.revokeObjectURL(currentObjectUrlRef.current)
    }

    const objectUrl = URL.createObjectURL(blob)
    currentObjectUrlRef.current = objectUrl

    setPdfDoc({
      url: objectUrl,
      fileName: responseFileName || fallbackFileName,
      source,
    })
  }

  const fetchPdf = useCallback(async (endpoint: string, fallbackFileName: string, source: ViewMode) => {
    setPdfLoading(true)
    setPdfError(null)

    try {
      const response = await fetchWithAccessControl(`${appConfig.apiBaseUrl}${endpoint}`, {
        credentials: 'include',
      })

      if (!response.ok) {
        throw new Error(`Cannot load PDF (${response.status})`)
      }

      const fileName = getFileNameFromContentDisposition(
        response.headers.get('content-disposition'),
      )
      const blob = await response.blob()
      setPdfDocument(blob, fallbackFileName, source, fileName)
    } catch (e: unknown) {
      const message = e instanceof Error ? e.message : 'Cannot load PDF'
      setPdfError(message)
    } finally {
      setPdfLoading(false)
    }
  }, [])

  const loadOriginalPdf = useCallback((item: QuotationItem) => {
    setViewMode('original')
    setActiveQuotationId(item.id)
    const displayName = item.originalFileName || item.fileName || `quotation-${item.id}.pdf`
    void fetchPdf(`/api/Request/ViewFile/${item.id}`, displayName, 'original')
  }, [fetchPdf])

  const loadMergedPdf = useCallback((requestId: number, docCode: string) => {
    setViewMode('merged')
    setActiveQuotationId(null)
    void fetchPdf(`/api/Quotation/ViewFile/${requestId}`, `Approved_${docCode}.pdf`, 'merged')
  }, [fetchPdf])

  function downloadCurrentPdf() {
    if (!pdfDoc) return
    const anchor = document.createElement('a')
    anchor.href = pdfDoc.url
    anchor.download = pdfDoc.fileName
    document.body.appendChild(anchor)
    anchor.click()
    document.body.removeChild(anchor)
  }

  // Fetch detail whenever code changes
  useEffect(() => {
    if (!code) return

    if (currentObjectUrlRef.current) {
      URL.revokeObjectURL(currentObjectUrlRef.current)
      currentObjectUrlRef.current = null
    }

    fetchWithAccessControl(`${appConfig.apiBaseUrl}/api/Quotation/ByCode/${encodeURIComponent(code)}`, {
      credentials: 'include',
    })
      .then((r) => {
        if (!r.ok) throw new Error(`${r.status} ${r.statusText}`)
        return r.json() as Promise<RequestDetail>
      })
      .then((data) => {
        setDetail(data)
        setLoading(false)
        if (data.quotations?.length > 0) {
          loadOriginalPdf(data.quotations[0])
        }
      })
      .catch((e: Error) => {
        setError(e.message)
        setLoading(false)
      })
  }, [code, loadOriginalPdf])

  return (
    <div className="flex h-full flex-col border-l border-(--border-subtle) bg-(--surface-panel)">
      {/* Header */}
      <div className="flex shrink-0 items-center justify-between border-b border-(--border-subtle) px-4 py-3">
        <div className="min-w-0">
          <p className="truncate text-[13px] font-medium text-(--ink-strong)">{code}</p>
          {detail && (
            <p className="truncate text-[12px] text-(--ink-muted)">{detail.title}</p>
          )}
        </div>
        <button
          type="button"
          onClick={onClose}
          aria-label="Close"
          className="ml-3 shrink-0 rounded-sm p-1 text-(--ink-soft) hover:text-(--ink-strong)"
        >
          <svg
            viewBox="0 0 16 16"
            fill="none"
            stroke="currentColor"
            strokeWidth="1.6"
            className="h-4 w-4"
            aria-hidden="true"
          >
            <path d="M3 3l10 10M13 3L3 13" />
          </svg>
        </button>
      </div>

      {detail && (
        <div className="flex shrink-0 items-center justify-between border-b border-(--border-subtle) px-3 py-2">
          <div className="flex items-center gap-1">
            <button
              type="button"
              onClick={() => setViewMode('original')}
              className={`rounded-sm px-3 py-1.5 text-[12px] ${
                viewMode === 'original'
                  ? 'bg-(--surface-muted) font-medium text-(--ink-strong)'
                  : 'text-(--ink-muted) hover:bg-(--surface-muted) hover:text-(--ink-strong)'
              }`}
            >
              Original
            </button>
            <button
              type="button"
              onClick={() => loadMergedPdf(detail.requestId, detail.code)}
              className={`rounded-sm px-3 py-1.5 text-[12px] ${
                viewMode === 'merged'
                  ? 'bg-(--surface-muted) font-medium text-(--ink-strong)'
                  : 'text-(--ink-muted) hover:bg-(--surface-muted) hover:text-(--ink-strong)'
              }`}
            >
              Merged & Stamped
            </button>
          </div>
          <div className="flex items-center gap-2">
            {portalBase && (
              <a
                href={`${portalBase}/Quotation/View/${encodeURIComponent(code)}`}
                target="_blank"
                rel="noopener noreferrer"
                className="rounded-sm border border-(--border-subtle) px-3 py-1.5 text-[12px] text-(--ink-strong) hover:bg-(--surface-muted)"
              >
                Open Page
              </a>
            )}
            <button
              type="button"
              onClick={downloadCurrentPdf}
              disabled={!pdfDoc || pdfLoading}
              className="rounded-sm border border-(--border-subtle) px-3 py-1.5 text-[12px] text-(--ink-strong) disabled:cursor-not-allowed disabled:opacity-50"
            >
              Download
            </button>
          </div>
        </div>
      )}

      {loading && (
        <div className="flex flex-1 items-center justify-center text-[12px] text-(--ink-soft)">
          Loading…
        </div>
      )}

      {error && <div className="p-4 text-[12px] text-red-600">{error}</div>}

      {detail && (
        <div className="flex min-h-0 flex-1 flex-col">
          {/* Attachment file tabs */}
          {viewMode === 'original' && detail.quotations?.length > 0 && (
            <div className="flex shrink-0 gap-1 overflow-x-auto border-b border-(--border-subtle) px-3 py-2">
              {detail.quotations.map((q) => (
                <button
                  key={q.id}
                  type="button"
                  onClick={() => loadOriginalPdf(q)}
                  className={`whitespace-nowrap rounded-sm px-3 py-1.5 text-[12px] transition-colors ${
                    activeQuotationId === q.id
                      ? 'bg-(--surface-muted) font-medium text-(--ink-strong)'
                      : 'text-(--ink-muted) hover:bg-(--surface-muted) hover:text-(--ink-strong)'
                  }`}
                  title={q.originalFileName || q.fileName}
                >
                  {q.originalFileName || q.fileName}
                </button>
              ))}
            </div>
          )}

          {/* PDF viewer */}
          <div className="relative min-h-0 flex-1 bg-(--surface-muted)">
            {pdfLoading && (
              <div className="absolute inset-0 z-10 flex items-center justify-center bg-(--surface-panel) text-[12px] text-(--ink-soft)">
                Loading PDF…
              </div>
            )}
            {pdfError && !pdfLoading && (
              <div className="flex h-full items-center justify-center px-4 text-[12px] text-red-600">
                {pdfError}
              </div>
            )}
            {pdfDoc?.url && !pdfLoading && !pdfError && (
              <iframe
                src={pdfDoc.url}
                className="h-full w-full border-0"
                title="Quotation PDF"
              />
            )}
            {!pdfDoc && !pdfLoading && !pdfError && detail.quotations?.length === 0 && viewMode === 'original' && (
              <div className="flex h-full items-center justify-center text-[12px] text-(--ink-soft)">
                No attachments
              </div>
            )}
            {!pdfDoc && !pdfLoading && !pdfError && viewMode === 'merged' && (
              <div className="flex h-full items-center justify-center text-[12px] text-(--ink-soft)">
                Click "Merged & Stamped" to generate and view final PDF
              </div>
            )}
          </div>

          {/* Info strip */}
          <div className="shrink-0 border-t border-(--border-subtle) bg-(--surface-muted) px-4 py-2">
            <dl className="grid grid-cols-2 gap-x-4 gap-y-1.5">
              {[
                { label: 'Vendor', value: detail.vendorName },
                { label: 'Requester', value: detail.requesterName },
                {
                  label: 'Valid From',
                  value: detail.validFrom
                    ? new Date(detail.validFrom).toLocaleDateString('en-GB')
                    : '—',
                },
                {
                  label: 'Valid Until',
                  value: detail.validUntil
                    ? new Date(detail.validUntil).toLocaleDateString('en-GB')
                    : '—',
                },
              ].map(({ label, value }) => (
                <div key={label}>
                  <dt className="text-[11px] text-(--ink-soft)">{label}</dt>
                  <dd className="text-[12px] text-(--ink-strong)">{value || '—'}</dd>
                </div>
              ))}
              {detail.remark && (
                <div className="col-span-2">
                  <dt className="text-[11px] text-(--ink-soft)">Remark</dt>
                  <dd className="text-[12px] text-(--ink-strong)">{detail.remark}</dd>
                </div>
              )}
            </dl>
          </div>
        </div>
      )}
    </div>
  )
}

export function QuotationsPage() {
  const [searchParams] = useSearchParams()
  const [selectedCode, setSelectedCode] = useState<string | null>(null)

  const vendorCodeFilter = (searchParams.get('vendorCode') ?? '').trim()
  const requesterNIdFilter = (searchParams.get('requesterNId') ?? '').trim()
  const vendorNameFilter = (searchParams.get('vendorName') ?? '').trim()
  const requesterNameFilter = (searchParams.get('requesterName') ?? '').trim()

  const dataPath = useMemo(() => {
    if (vendorCodeFilter) {
      return `/api/Request/Admin/ApprovedByVendor/${encodeURIComponent(vendorCodeFilter)}`
    }

    if (requesterNIdFilter) {
      return `/api/Request/Admin/ApprovedByRequesterNId/${encodeURIComponent(requesterNIdFilter)}`
    }

    if (requesterNameFilter) {
      return `/api/Request/Admin/ApprovedByRequester/${encodeURIComponent(requesterNameFilter)}`
    }

    return '/api/Request/Admin/Approved'
  }, [requesterNIdFilter, requesterNameFilter, vendorCodeFilter])

  const dataSource = useMemo(
    () => createDataSource<QuotationRow>(dataPath, 'id'),
    [dataPath],
  )
  const vendorLookupDataSource = useMemo(
    () => createDataSource<{ vendorCode: string; vendorName: string }>('/api/Vendor/Lookup', 'vendorCode'),
    [],
  )

  const handleRowClick = useCallback((e: RowClickEvent<QuotationRow>) => {
    const code = e.data?.code ?? null
    setSelectedCode((prev) => (prev === code ? null : code))
  }, [])

  return (
    <div className="flex flex-col gap-3 flex-1 min-h-0">
      {(vendorCodeFilter || requesterNameFilter) && (
        <section className="shrink-0 rounded-sm border border-(--border-subtle) bg-(--surface-panel) px-4 py-2 text-[13px] text-(--ink-muted)">
          {vendorCodeFilter ? (
            <>
              Showing quotations for vendor:
              <span className="ml-1 font-medium text-(--ink-strong)">
                {vendorNameFilter || vendorCodeFilter}
              </span>
              <span className="ml-1 text-(--ink-soft)">({vendorCodeFilter})</span>
            </>
          ) : (
            <>
              Showing quotations for requester:
              <span className="ml-1 font-medium text-(--ink-strong)">
                {requesterNameFilter || requesterNIdFilter}
              </span>
              {requesterNIdFilter && <span className="ml-1 text-(--ink-soft)">({requesterNIdFilter})</span>}
            </>
          )}
        </section>
      )}

      <div className="flex flex-1 min-h-0 gap-0">
      {/* Grid — narrows when detail pane is open */}
      <div
        className={`min-w-0 transition-all duration-150 ${selectedCode ? 'w-95 shrink-0' : 'flex-1'}`}
      >
        <section className="h-full overflow-hidden rounded-sm border border-(--border-subtle) bg-(--surface-panel)">
          <DataGrid
            dataSource={dataSource}
            showBorders={false}
            showColumnLines={false}
            showRowLines={true}
            rowAlternationEnabled={false}
            columnAutoWidth={true}
            wordWrapEnabled={false}
            height="100%"
            onRowClick={handleRowClick}
            focusedRowEnabled={true}
            hoverStateEnabled={true}
          >
            <RemoteOperations filtering paging sorting grouping={false} summary={false} />
            <FilterRow visible={true} />
            <HeaderFilter visible={true} />
            <Scrolling mode="virtual" rowRenderingMode="virtual" />

            <Column dataField="code" caption="Doc No." width={130} />
            <Column dataField="vendorCode" caption="Vendor" minWidth={130}>
              <Lookup
                dataSource={vendorLookupDataSource}
                valueExpr="vendorCode"
                displayExpr="vendorName"
              />
            </Column>
            <Column
              dataField="requestDate"
              caption="Date"
              dataType="date"
              format="dd/MM/yyyy"
              width={105}
              alignment="center"
            />
            {!selectedCode && (
              <>
                <Column dataField="title" caption="Title" minWidth={180} />
                <Column dataField="requesterName" caption="Requester" width={150} />
                <Column
                  dataField="validFrom"
                  caption="Valid From"
                  dataType="date"
                  format="dd/MM/yyyy"
                  width={105}
                  alignment="center"
                />
                <Column
                  dataField="validUntil"
                  caption="Valid Until"
                  dataType="date"
                  format="dd/MM/yyyy"
                  width={105}
                  alignment="center"
                />
              </>
            )}
          </DataGrid>
        </section>
      </div>

      {/* Detail + PDF pane */}
      {selectedCode && (
        <div className="min-w-0 flex-1 overflow-hidden rounded-r-sm border border-l-0 border-(--border-subtle)">
          <DetailPane key={selectedCode} code={selectedCode} onClose={() => setSelectedCode(null)} />
        </div>
      )}
      </div>
    </div>
  )
}
