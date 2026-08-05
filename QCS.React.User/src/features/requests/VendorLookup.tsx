import { useEffect, useState } from 'react'
import { apiClient, toApiError } from '@/lib/apiClient'
import { Field } from '@/components/ui/Field'
import { appInputClassName } from '@/components/ui/inputStyles'
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
  return <fieldset className="grid gap-2" data-invalid={errors.vendor ? 'true' : undefined}><legend className="mb-1 text-caption font-medium uppercase tracking-[0.08em] text-ink-muted">Vendor<span className="ml-0.5 text-danger">*</span></legend><div className="grid gap-3 sm:grid-cols-2"><Field label="Code"><input value={value.vendorCode} onChange={(event) => onChange({ vendorCode: event.target.value })} className={appInputClassName('md', 'w-full')} autoComplete="off" /></Field><Field label="Name"><input value={value.vendorName} onChange={(event) => onChange({ vendorName: event.target.value })} className={appInputClassName('md', 'w-full')} autoComplete="off" /></Field></div>{options.length > 0 && <ul className="max-h-40 overflow-auto rounded-sm border border-border-subtle bg-white" aria-label="Vendor suggestions">{options.map((option) => <li key={`${option.code}-${option.name}`}><button type="button" onClick={() => onChange({ vendorCode: option.code, vendorName: option.name })} className="w-full px-3 py-2 text-left text-body hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-accent">{option.code} {option.name && `- ${option.name}`}</button></li>)}</ul>}{warning && <p className="text-caption text-ink-muted">Vendor lookup is unavailable. You can enter a vendor manually.</p>}{errors.vendor && <p className="text-caption text-danger">{errors.vendor}</p>}</fieldset>
}