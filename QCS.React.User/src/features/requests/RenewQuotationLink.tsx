import { RefreshCw } from 'lucide-react'
import { Link } from 'react-router'

export function RenewQuotationLink({ code }: { code: string }) {
  return <Link to={`/requests/new?renewedFromCode=${encodeURIComponent(code)}`} className="inline-flex items-center gap-2 rounded-sm text-body text-accent hover:underline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent"><RefreshCw className="size-3.5" aria-hidden />Renew quotation</Link>
}
