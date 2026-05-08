import { useEffect, useMemo, useState } from 'react'
import DataGrid, { Column, FilterRow, HeaderFilter, Scrolling } from 'devextreme-react/data-grid'
import { appConfig } from '../../config/appConfig.ts'

type VendorRow = {
  id: string
  vendorCode: string
  vendorName: string
  taxId: string
  contactName: string
  phone: string
  email: string
  address: string
}

function toValue(source: Record<string, unknown>, keys: string[]): string {
  for (const key of keys) {
    const value = source[key]
    if (value !== undefined && value !== null && String(value).trim() !== '') {
      return String(value)
    }
  }
  return '-'
}

function normalizeVendorRows(payload: unknown): VendorRow[] {
  const list = Array.isArray(payload)
    ? payload
    : payload && typeof payload === 'object' && Array.isArray((payload as { data?: unknown[] }).data)
      ? (payload as { data: unknown[] }).data
      : []

  return list
    .filter((item): item is Record<string, unknown> => !!item && typeof item === 'object')
    .map((item, index) => {
      const vendorCode = toValue(item, ['vendorCode', 'VendorCode', 'code', 'Code', 'supplierCode'])
      const vendorName = toValue(item, ['vendorName', 'VendorName', 'name', 'Name', 'supplierName'])
      const id = toValue(item, ['id', 'Id', 'vendorId', 'VendorId'])

      return {
        id: id !== '-' ? id : `${vendorCode}_${index}`,
        vendorCode,
        vendorName,
        taxId: toValue(item, ['taxId', 'TaxId', 'taxNo', 'TaxNo']),
        contactName: toValue(item, ['contactName', 'ContactName', 'contact', 'Contact']),
        phone: toValue(item, ['phone', 'Phone', 'tel', 'Tel', 'telephone', 'Telephone']),
        email: toValue(item, ['email', 'Email', 'mail', 'Mail']),
        address: toValue(item, ['address', 'Address']),
      }
    })
}

export function VendorsPage() {
  const [rows, setRows] = useState<VendorRow[]>([])
  const [isLoading, setIsLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const totalVendors = useMemo(() => rows.length, [rows.length])

  useEffect(() => {
    let isCancelled = false

    const loadVendors = async () => {
      setIsLoading(true)
      setError(null)

      try {
        const response = await fetch(`${appConfig.apiBaseUrl}/api/Vendor`, {
          credentials: 'include',
        })

        if (!response.ok) {
          throw new Error(`Cannot load vendors (${response.status}).`)
        }

        const payload = (await response.json()) as unknown
        const normalized = normalizeVendorRows(payload)

        if (!isCancelled) {
          setRows(normalized)
        }
      } catch (fetchError: unknown) {
        const message = fetchError instanceof Error ? fetchError.message : 'Cannot load vendors.'
        if (!isCancelled) {
          setRows([])
          setError(message)
        }
      } finally {
        if (!isCancelled) {
          setIsLoading(false)
        }
      }
    }

    void loadVendors()

    return () => {
      isCancelled = true
    }
  }, [])

  return (
    <div className="space-y-3">
      <section className="flex flex-wrap items-end justify-between gap-2">
        <div>
          <h2 className="text-[20px] font-semibold text-(--ink-strong)">Vendor Registry</h2>
          <p className="text-[13px] text-(--ink-muted)">
            Data source: Vendor API proxy endpoint (`/api/Vendor`)
          </p>
        </div>
        <p className="text-[12px] text-(--ink-soft)">Total vendors: {totalVendors}</p>
      </section>

      {error && (
        <div className="rounded-sm border border-(--border-subtle) bg-(--surface-panel) px-4 py-3 text-[13px] text-(--ink-strong)">
          {error}
        </div>
      )}

      <section className="overflow-hidden rounded-sm border border-(--border-subtle) bg-(--surface-panel)">
        <DataGrid
          dataSource={rows}
          keyExpr="id"
          showBorders={false}
          showColumnLines={false}
          showRowLines={true}
          rowAlternationEnabled={false}
          columnAutoWidth={true}
          wordWrapEnabled={false}
          noDataText={isLoading ? 'Loading vendors...' : 'No vendor data'}
          height="calc(100vh - 250px)"
        >
          <FilterRow visible={true} />
          <HeaderFilter visible={true} />
          <Scrolling mode="virtual" rowRenderingMode="virtual" />

          <Column dataField="vendorCode" caption="Vendor Code" width={150} />
          <Column dataField="vendorName" caption="Vendor Name" minWidth={220} />
          <Column dataField="taxId" caption="Tax ID" width={160} />
          <Column dataField="contactName" caption="Contact" width={170} />
          <Column dataField="phone" caption="Phone" width={160} />
          <Column dataField="email" caption="Email" minWidth={200} />
          <Column dataField="address" caption="Address" minWidth={240} />
        </DataGrid>
      </section>
    </div>
  )
}
