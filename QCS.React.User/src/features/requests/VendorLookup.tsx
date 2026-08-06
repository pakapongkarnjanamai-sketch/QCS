import { useEffect, useState } from 'react'
import { Field } from '@/components/ui/Field'
import { appInputClassName } from '@/components/ui/inputStyles'
import { apiClient, toApiError } from '@/lib/apiClient'
import type { SavePortalRequest } from './types'

// Mirrors QCS.Domain.DTOs.ActiveVendorLookupDto.
interface ActiveVendorOption {
  id: number
  name: string
  code: string
}

const minimumQueryLength = 2
const maximumSuggestions = 20

interface VendorLookupProps {
  value: SavePortalRequest
  errors: Record<string, string>
  disabled?: boolean
  onChange: (patch: Partial<SavePortalRequest>) => void
}

export function VendorLookup({ value, errors, disabled = false, onChange }: VendorLookupProps) {
  const [vendors, setVendors] = useState<ActiveVendorOption[]>([])
  const [query, setQuery] = useState('')
  const [loading, setLoading] = useState(true)
  const [warning, setWarning] = useState('')

  useEffect(() => {
    if (disabled) {
      setLoading(false)
      return undefined
    }
    const controller = new AbortController()
    void apiClient.get<ActiveVendorOption[]>('/Vendor/ActiveLookup', { signal: controller.signal })
      .then(({ data }) => {
        setVendors(data)
        setWarning('')
      })
      .catch((reason: unknown) => {
        if (!controller.signal.aborted) {
          setVendors([])
          setWarning(toApiError(reason).title)
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [disabled])

  const normalizedQuery = query.trim().toLocaleLowerCase()
  const options = disabled || normalizedQuery.length < minimumQueryLength
    ? []
    : vendors
      .filter((vendor) => vendor.code.toLocaleLowerCase().includes(normalizedQuery)
        || vendor.name.toLocaleLowerCase().includes(normalizedQuery))
      .slice(0, maximumSuggestions)

  const updateField = (field: 'vendorCode' | 'vendorName', nextValue: string) => {
    setQuery(nextValue)
    onChange({ [field]: nextValue })
  }

  const showLoading = loading && normalizedQuery.length >= minimumQueryLength

  return (
    <fieldset className="grid gap-2" data-invalid={errors.vendor ? 'true' : undefined}>
      <legend className="mb-1 text-caption font-medium uppercase tracking-[0.08em] text-ink-muted">
        Vendor<span className="ml-0.5 text-danger">*</span>
      </legend>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Code">
          <input
            value={value.vendorCode}
            disabled={disabled}
            onChange={(event) => updateField('vendorCode', event.target.value)}
            className={appInputClassName('md', 'w-full')}
            autoComplete="off"
            aria-controls="vendor-suggestions"
            aria-expanded={options.length > 0}
          />
        </Field>
        <Field label="Name">
          <input
            value={value.vendorName}
            disabled={disabled}
            onChange={(event) => updateField('vendorName', event.target.value)}
            className={appInputClassName('md', 'w-full')}
            autoComplete="off"
            aria-controls="vendor-suggestions"
            aria-expanded={options.length > 0}
          />
        </Field>
      </div>
      {showLoading && <p className="text-caption text-ink-muted">Loading active vendors...</p>}
      {options.length > 0 && (
        <ul id="vendor-suggestions" className="max-h-48 overflow-auto rounded-sm border border-border-subtle bg-white" aria-label="Vendor suggestions">
          {options.map((option) => (
            <li key={option.id}>
              <button
                type="button"
                onClick={() => {
                  onChange({ vendorCode: option.code, vendorName: option.name })
                  setQuery('')
                }}
                className="w-full px-3 py-2 text-left text-body hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-offset-[-2px] focus-visible:outline-accent"
              >
                <span className="font-medium">{option.code}</span>
                <span className="ml-2 text-ink-muted">{option.name}</span>
              </button>
            </li>
          ))}
        </ul>
      )}
      {warning && <p className="text-caption text-ink-muted">Vendor lookup is unavailable. You can enter a vendor manually.</p>}
      {errors.vendor && <p className="text-caption text-danger">{errors.vendor}</p>}
    </fieldset>
  )
}