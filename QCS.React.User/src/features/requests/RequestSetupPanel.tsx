import { ArrowRight, Building2, CheckCircle2, FilePlus2, RefreshCw, Waypoints } from 'lucide-react'
import { useState } from 'react'
import { AppButton } from '@/components/ui/AppButton'
import { QrsSourceTable } from './QrsSourceTable'
import { RenewalCandidateTable } from './RenewalCandidateTable'
import type { DiscriminatedSetupState, RenewalCandidate, SetupFlow } from './types'

interface RequestSetupPanelProps {
  selectedFlow?: SetupFlow
  onFlowChange: (flow: SetupFlow) => void
  onComplete: (setup: DiscriminatedSetupState) => void
}

const setupFlows = [
  {
    value: 'new-qcs',
    title: 'New quotation',
    origin: 'Start in QCS',
    description: 'Create a request from scratch and choose a vendor in the form.',
    icon: FilePlus2,
  },
  {
    value: 'new-qrs',
    title: 'New quotation',
    origin: 'From QRS',
    description: 'Start from one completed QRS sourcing request.',
    icon: Waypoints,
  },
  {
    value: 'renewal-qcs',
    title: 'Renew quotation',
    origin: 'Start in QCS',
    description: 'Renew one expired completed QCS quotation.',
    icon: RefreshCw,
  },
  {
    value: 'renewal-qrs',
    title: 'Renew quotation',
    origin: 'From QRS',
    description: 'Link a new QRS request to one expired QCS quotation.',
    icon: Building2,
  },
] as const satisfies ReadonlyArray<{
  value: SetupFlow
  title: string
  origin: string
  description: string
  icon: typeof FilePlus2
}>

export function RequestSetupPanel({ selectedFlow, onFlowChange, onComplete }: RequestSetupPanelProps) {
  const [selectedCandidate, setSelectedCandidate] = useState<RenewalCandidate>()
  const [selectedQrsSource, setSelectedQrsSource] = useState<{ code: string; title?: string }>()

  const handleFlowChange = (flow: SetupFlow) => {
    setSelectedCandidate(undefined)
    setSelectedQrsSource(undefined)
    onFlowChange(flow)
  }

  const handleSelectCandidate = (candidate: RenewalCandidate) => {
    setSelectedCandidate(candidate)
  }

  const handleSelectQrsSource = (row: { code: string; title?: string }) => {
    setSelectedQrsSource(row)
  }

  const setup = (() => {
    if (selectedFlow === 'new-qcs') {
      return { intent: 'New', origin: 'QCS' } satisfies DiscriminatedSetupState
    }
    if (selectedFlow === 'new-qrs' && selectedQrsSource) {
      return {
        intent: 'New',
        origin: 'QRS',
        qrsSourceCode: selectedQrsSource.code,
        qrsTitle: selectedQrsSource.title,
      } satisfies DiscriminatedSetupState
    }
    if (selectedFlow === 'renewal-qcs' && selectedCandidate) {
      return {
        intent: 'Renewal',
        origin: 'QCS',
        renewedFromRequestId: selectedCandidate.id,
        renewedFromCode: selectedCandidate.code,
        vendorCode: selectedCandidate.vendorCode,
        vendorName: selectedCandidate.vendorName,
        title: selectedCandidate.title,
      } satisfies DiscriminatedSetupState
    }
    if (selectedFlow === 'renewal-qrs' && selectedCandidate && selectedQrsSource) {
      return {
        intent: 'Renewal',
        origin: 'QRS',
        renewedFromRequestId: selectedCandidate.id,
        renewedFromCode: selectedCandidate.code,
        vendorCode: selectedCandidate.vendorCode,
        vendorName: selectedCandidate.vendorName,
        qrsSourceCode: selectedQrsSource.code,
        qrsTitle: selectedQrsSource.title,
      } satisfies DiscriminatedSetupState
    }
    return undefined
  })()

  const requiresRenewal = selectedFlow === 'renewal-qcs' || selectedFlow === 'renewal-qrs'
  const requiresQrs = selectedFlow === 'new-qrs' || selectedFlow === 'renewal-qrs'
  const selectionRequirement = selectedFlow === 'new-qcs'
    ? 'No source record is required. Continue to enter the request.'
    : selectedFlow === 'new-qrs'
      ? 'Select one completed QRS sourcing request or enter its Code.'
      : selectedFlow === 'renewal-qcs'
        ? 'Select one expired completed QCS quotation.'
        : selectedFlow === 'renewal-qrs'
          ? 'Select one QRS source and one expired completed QCS quotation.'
          : 'Choose the request flow that matches the work you need to do.'

  return (
    <section className="space-y-6">
      <div>
        <h2 className="text-heading font-semibold text-ink-strong">Choose request flow</h2>
        <p className="text-body text-ink-muted">
          Select one option before entering request details.
        </p>
      </div>

      <div className="grid gap-3 sm:grid-cols-2" role="radiogroup" aria-label="Request flow">
        {setupFlows.map((flow) => {
          const selected = selectedFlow === flow.value
          const Icon = flow.icon
          return (
            <label
              key={flow.value}
              className={`relative flex min-h-32 cursor-pointer gap-3 rounded-sm border p-4 transition-colors focus-within:outline-2 focus-within:outline-offset-2 focus-within:outline-accent ${
                selected
                  ? 'border-accent bg-accent-subtle/30'
                  : 'border-border-subtle bg-surface-panel hover:border-ink-soft hover:bg-surface-muted'
              }`}
            >
              <input
                type="radio"
                name="requestSetupFlow"
                value={flow.value}
                checked={selected}
                onChange={() => handleFlowChange(flow.value)}
                className="sr-only"
              />
              <span className={`grid size-9 shrink-0 place-items-center rounded-sm ${selected ? 'bg-accent text-white' : 'bg-surface-muted text-ink-muted'}`}>
                <Icon className="size-4.5" aria-hidden />
              </span>
              <span className="min-w-0 flex-1">
                <span className="block text-heading font-semibold text-ink-strong">{flow.title}</span>
                <span className="mt-0.5 block text-caption font-medium text-accent">{flow.origin}</span>
                <span className="mt-2 block text-body text-ink-muted">{flow.description}</span>
              </span>
              {selected && <CheckCircle2 className="size-4 shrink-0 text-accent" aria-label="Selected" />}
            </label>
          )
        })}
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3 border-y border-border-subtle bg-surface-muted px-4 py-3 text-caption">
        <p className="text-ink-muted">{selectionRequirement}</p>
        {setup && (
          <div className="flex items-center gap-1 font-medium text-positive">
            <CheckCircle2 className="h-4 w-4" />
            <span>Ready to continue</span>
          </div>
        )}
      </div>

      {requiresRenewal && (
        <RenewalCandidateTable
          selectedId={selectedCandidate?.id}
          onSelect={handleSelectCandidate}
        />
      )}

      {requiresQrs && (
        <QrsSourceTable
          selectedCode={selectedQrsSource?.code}
          onSelect={handleSelectQrsSource}
        />
      )}

      <div className="flex justify-end border-t border-border-subtle pt-4">
        <AppButton
          type="button"
          onClick={() => setup && onComplete(setup)}
          disabled={!setup}
          className="gap-2"
        >
          <span>Continue to Form</span>
          <ArrowRight className="h-4 w-4" />
        </AppButton>
      </div>
    </section>
  )
}
