import { Navigate, Route, Routes, useLocation } from 'react-router'
import { AppLayout } from '@/components/layout/AppLayout'
import { AccessDeniedPage } from '@/pages/AccessDeniedPage'
import { QuotationDetailPage } from '@/features/quotations/QuotationDetailPage'
import { RequestFormPage } from '@/features/requests/RequestFormPage'
import { RequestDetailPage } from '@/features/requests/RequestDetailPage'
import { WorkspacePage } from '@/features/workspace/WorkspacePage'

function LegacyWorkspaceRoute() {
  const location = useLocation()
  const searchParams = new URLSearchParams(location.search)

  if (searchParams.get('view') === 'my-requests') {
    searchParams.delete('view')
    const search = searchParams.toString()
    return <Navigate to={`/requests${search ? `?${search}` : ''}`} replace />
  }

  return <WorkspacePage defaultView="my-tasks" showSummary />
}

function PortalRoutes() {
  return <AppLayout><Routes>
    <Route index element={<LegacyWorkspaceRoute />} />
    <Route path="requests" element={<WorkspacePage defaultView="my-requests" />} />
    <Route path="inbox" element={<WorkspacePage defaultView="my-tasks" title="My approvals" description="Requests waiting on you." showCreateAction={false} lockView returnPath="/inbox" emptyMessage="Nothing waiting on you" emptyIcon="inbox" />} />
    <Route path="requests/new" element={<RequestFormPage />} />
    <Route path="requests/:id/edit" element={<RequestFormPage />} />
    <Route path="requests/:id" element={<RequestDetailPage />} />
    <Route path="quotations/:code" element={<QuotationDetailPage />} />
    {/* The sidebar's Quotations entry lands here: approved quotation documents are the
        all-approved view of the same workspace, not a separate screen. */}
    <Route path="quotations" element={<WorkspacePage defaultView="all-approved" title="Quotations" description="Approved quotation documents." showCreateAction={false} lockView returnPath="/quotations" />} />
    {/* Matches QRS: an unknown path returns to the workspace instead of rendering an empty
        shell. Without this, /quotations rendered a blank page under the chrome. */}
    <Route path="*" element={<Navigate to="/" replace />} />
  </Routes></AppLayout>
}

export function App() {
  return <Routes><Route path="/access-denied" element={<AccessDeniedPage />} /><Route path="/*" element={<PortalRoutes />} /></Routes>
}