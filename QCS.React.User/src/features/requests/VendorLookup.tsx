import { useEffect, useState } from 'react'
import { apiClient, toApiError } from '@/lib/apiClient'
import type { SavePortalRequest } from './types'

interface VendorOption { code: string; name: string }

function toOptions(payload: unknown): VendorOption[] {
  const rows = Array.isArray(payload) ? payload : []
  return rows.map((row) => {
    const value = row as Record<string, unknown>
    return { code: String(value.vendorCode ?? value.VendorCode ?? value.code ?? value.Code ?? ''), name: String(value.vendorName ?? value.VendorName ?? value.name ?? value.Name ?? '') }
  }).filter((option) => option.code || option.name)
}

export function VendorLookup({ value, errors, onChange }: { value: SavePortalRequest; errors: Record<string, string>; onChange: (patch: Partial<SavePortalRequest>) => void }) {
  const [options, setOptions] = useState<VendorOption[]>([]); const [warning, setWarning] = useState('')
  const search = `${value.vendorCode} ${value.vendorName}`.trim()
  useEffect(() => {
    if (search.length < 2) { setOptions([]); return undefined }
    const controller = new AbortController()
    const timer = window.setTimeout(() => {
      void apiClient.get<unknown>('/Vendor', { params: { search }, signal: controller.signal }).then(({ data }) => { setOptions(toOptions(data)); setWarning('') }).catch((reason: unknown) => { if (!controller.signal.aborted) { setOptions([]); setWarning(toApiError(reason).title) } })
    }, 300)
    return () => { window.clearTimeout(timer); controller.abort() }
  }, [search])
  return <fieldset className="grid gap-3" data-invalid={errors.vendor ? 'true' : undefined}><legend className="text-body font-medium text-ink-strong">Vendor <span className="text-danger">*</span></legend><div className="grid gap-3 sm:grid-cols-2"><label className="grid gap-1.5 text-body">Code<input value={value.vendorCode} onChange={(event) => onChange({ vendorCode: event.target.value })} className="rounded-sm border border-border-subtle px-3 py-2" autoComplete="off" /></label><label className="grid gap-1.5 text-body">Name<input value={value.vendorName} onChange={(event) => onChange({ vendorName: event.target.value })} className="rounded-sm border border-border-subtle px-3 py-2" autoComplete="off" /></label></div>{options.length > 0 && <ul className="max-h-40 overflow-auto border border-border-subtle" aria-label="Vendor suggestions">{options.map((option) => <li key={`${option.code}-${option.name}`}><button type="button" onClick={() => onChange({ vendorCode: option.code, vendorName: option.name })} className="w-full px-3 py-2 text-left text-body hover:bg-surface-muted">{option.code} {option.name && `- ${option.name}`}</button></li>)}</ul>}{warning && <p className="text-caption text-ink-muted">Vendor lookup is unavailable. You can enter a vendor manually.</p>}{errors.vendor && <p className="text-caption text-danger">{errors.vendor}</p>}</fieldset>
}