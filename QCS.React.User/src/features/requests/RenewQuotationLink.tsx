import { RefreshCw } from 'lucide-react'
import { AppLinkButton } from '@/components/ui/AppLinkButton'

export function RenewQuotationLink({ code }: { code: string }) {
  return <AppLinkButton to={`/requests/new?renewedFromCode=${encodeURIComponent(code)}`} variant="secondary" size="sm"><RefreshCw className="size-3.5" aria-hidden />Renew quotation</AppLinkButton>
}
