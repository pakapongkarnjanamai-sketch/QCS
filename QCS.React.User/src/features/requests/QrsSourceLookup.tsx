import { useEffect, useState } from 'react'
import { apiClient, toApiError } from '@/lib/apiClient'
import type { SavePortalRequest } from './types'

interface QrsSource { code: string; title: string }

function toSources(payload: unknown): QrsSource[] {
  const root = payload as { items?: unknown[] } | unknown[]
  const rows = Array.isArray(root) ? root : root.items ?? []
  return rows.map((row) => { const value = row as Record<string, unknown>; return { code: String(value.code ?? value.requestCode ?? ''), title: String(value.title ?? value.name ?? '') } }).filter((source) => source.code)
}

export function QrsSourceLookup({ value, onChange }: { value: SavePortalRequest; onChange: (patch: Partial<SavePortalRequest>) => void }) {
  const [sources, setSources] = useState<QrsSource[]>([]); const [warning, setWarning] = useState('')
  useEffect(() => {
    if (value.sourceCode.trim().length < 2) { setSources([]); return undefined }
    const controller = new AbortController()
    const timer = window.setTimeout(() => { void apiClient.get<unknown>('/QrsSourcing/Requests', { params: { search: value.sourceCode }, signal: controller.signal }).then(({ data }) => { setSources(toSources(data)); setWarning('') }).catch((reason: unknown) => { if (!controller.signal.aborted) { setSources([]); setWarning(toApiError(reason).title) } }) }, 300)
    return () => { window.clearTimeout(timer); controller.abort() }
  }, [value.sourceCode])
  return <fieldset className="grid gap-2"><legend className="text-body font-medium text-ink-strong">QRS source (optional)</legend><input value={value.sourceCode} onChange={(event) => onChange({ sourceSystem: event.target.value ? 'QRS' : '', sourceCode: event.target.value })} className="rounded-sm border border-border-subtle px-3 py-2 text-body" placeholder="Enter a QRS request code" autoComplete="off" />{sources.length > 0 && <ul className="max-h-40 overflow-auto border border-border-subtle" aria-label="QRS request suggestions">{sources.map((source) => <li key={source.code}><button type="button" onClick={() => onChange({ sourceSystem: 'QRS', sourceCode: source.code })} className="w-full px-3 py-2 text-left text-body hover:bg-surface-muted">{source.code}{source.title && ` - ${source.title}`}</button></li>)}</ul>}{warning && <p className="text-caption text-ink-muted">QRS lookup is unavailable. You can still enter a QRS code manually.</p>}</fieldset>
}