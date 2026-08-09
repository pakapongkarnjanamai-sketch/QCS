import { ArrowLeft, ArrowRight, Building2, FilePlus2, Loader2, RefreshCw, Waypoints } from 'lucide-react'
import { useState } from 'react'
import { AppButton } from '@/components/ui/AppButton'
import { ErrorSurface } from '@/components/ui/Surfaces'
import { toApiError } from '@/lib/apiClient'
import { QrsSourceTable } from './QrsSourceTable'
import { RenewalCandidateTable } from './RenewalCandidateTable'
import { setupErrorMessage } from './setupErrors'
import type { DiscriminatedSetupState, RenewalCandidate, SetupFlow } from './types'

interface RequestSetupPanelProps {
  selectedFlow?: SetupFlow
  onFlowChange: (flow?: SetupFlow) => void
  onComplete: (setup: DiscriminatedSetupState) => void
  onResolveQrs: (code: string) => Promise<void>
  onResolveQcs: (code: string) => Promise<void>
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
    description: 'Renew one completed QCS quotation that has expired or expires within 30 days.',
    icon: RefreshCw,
  },
  {
    value: 'renewal-qrs',
    title: 'Renew quotation',
    origin: 'From QRS',
    description: 'Start from one completed QRS request marked Renewal; QCS resolves its previous quotation.',
    icon: Building2,
  },
] as const satisfies ReadonlyArray<{
  value: SetupFlow
  title: string
  origin: string
  description: string
  icon: typeof FilePlus2
}>

export function RequestSetupPanel({ selectedFlow, onFlowChange, onComplete, onResolveQrs, onResolveQcs }: RequestSetupPanelProps) {
  const [selectedQrsSource, setSelectedQrsSource] = useState<{ code: string; title?: string }>()
  const [selectedQcCandidate, setSelectedQcCandidate] = useState<RenewalCandidate>()
  const [resolving, setResolving] = useState(false)
  const [error, setError] = useState<string>()

  const handleFlowChange = (flow: SetupFlow) => {
    setSelectedQrsSource(undefined)
    setSelectedQcCandidate(undefined)
    setError(undefined)
    onFlowChange(flow)
  }


  const complete = async () => {
    const code = selectedFlow === 'renewal-qcs' ? selectedQcCandidate?.code : selectedQrsSource?.code
    if (!code) return
    setResolving(true)
    setError(undefined)
    try {
      if (selectedFlow === 'renewal-qcs') await onResolveQcs(code)
      else await onResolveQrs(code)
    } catch (reason) {
      // A predecessor can be consumed between loading this table and pressing
      // Continue. Without toApiError that surfaces as axios's "Request failed with
      // status code 409", which tells the user nothing about what to do next.
      setError(setupErrorMessage(toApiError(reason)))
    } finally {
      setResolving(false)
    }
  }

  if (!selectedFlow) {
    return <section className="space-y-6"><div><h2 className="text-heading font-semibold text-ink-strong">Choose request flow</h2><p className="mt-1 text-body text-ink-muted">Select how this quotation should begin.</p></div><div className="grid gap-3 sm:grid-cols-2">{setupFlows.map((flow) => { const Icon = flow.icon; return <button key={flow.value} type="button" onClick={() => flow.value === 'new-qcs' ? onComplete({ intent: 'New', origin: 'QCS' }) : handleFlowChange(flow.value)} className="flex min-h-32 gap-3 rounded-sm border border-border-subtle bg-surface-panel p-4 text-left hover:border-ink-soft hover:bg-surface-muted focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"><span className="grid size-9 shrink-0 place-items-center rounded-sm bg-surface-muted text-ink-muted"><Icon className="size-4.5" aria-hidden /></span><span className="min-w-0"><span className="block text-heading font-semibold text-ink-strong">{flow.title}</span><span className="mt-0.5 block text-caption font-medium text-accent">{flow.origin}</span><span className="mt-2 block text-body text-ink-muted">{flow.description}</span></span></button> })}</div></section>
  }

  const isRenewalQcs = selectedFlow === 'renewal-qcs'
  const isRenewalQrs = selectedFlow === 'renewal-qrs'
  const canContinue = isRenewalQcs ? Boolean(selectedQcCandidate) : Boolean(selectedQrsSource)
  const title = isRenewalQcs ? 'Select eligible quotation' : isRenewalQrs ? 'Select renewal QRS request' : 'Select QRS request'

  return (
    <section className="space-y-6"><div className="flex items-start justify-between gap-3"><div><h2 className="text-heading font-semibold text-ink-strong">{title}</h2><p className="mt-1 text-body text-ink-muted">{isRenewalQcs ? 'Choose one eligible completed QCS quotation.' : isRenewalQrs ? 'Choose one completed QRS request marked Renewal.' : 'Choose one completed QRS request marked New.'}</p></div><AppButton variant="ghost" size="sm" onClick={() => onFlowChange(undefined)}><ArrowLeft className="size-4" aria-hidden />Back</AppButton></div>{isRenewalQcs ? <RenewalCandidateTable selectedId={selectedQcCandidate?.id} onSelect={setSelectedQcCandidate} /> : <QrsSourceTable selectedCode={selectedQrsSource?.code} intent={isRenewalQrs ? 'Renewal' : 'New'} allowManualCode={!isRenewalQrs} onSelect={setSelectedQrsSource} />}{error && <ErrorSurface>{error}</ErrorSurface>}<div className="flex justify-end border-t border-border-subtle pt-4"><AppButton type="button" onClick={() => void complete()} disabled={!canContinue || resolving} className="gap-2">{resolving ? <Loader2 className="size-4 animate-spin" aria-hidden /> : <ArrowRight className="size-4" aria-hidden />}<span>Continue to form</span></AppButton></div></section>
  )
}
