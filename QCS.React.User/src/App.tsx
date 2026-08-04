import { Navigate, Route, Routes } from 'react-router'
import { AppLayout } from '@/components/layout/AppLayout'
import { AccessDeniedPage } from '@/pages/AccessDeniedPage'
import { PlaceholderPage } from '@/pages/PlaceholderPage'
import { QuotationDetailPage } from '@/features/quotations/QuotationDetailPage'
import { RequestDetailPage } from '@/features/requests/RequestDetailPage'
import { WorkspacePage } from '@/features/workspace/WorkspacePage'

function PortalRoutes() {
  return <AppLayout><Routes>
    <Route index element={<WorkspacePage />} />
    <Route path="requests" element={<Navigate to="/?view=my-requests" replace />} />
    <Route path="requests/new" element={<PlaceholderPage title="New request" />} />
    <Route path="requests/:id/edit" element={<PlaceholderPage title="Edit request" />} />
    <Route path="requests/:id" element={<RequestDetailPage />} />
    <Route path="quotations/:code" element={<QuotationDetailPage />} />
  </Routes></AppLayout>
}

export function App() {
  return <Routes><Route path="/access-denied" element={<AccessDeniedPage />} /><Route path="/*" element={<PortalRoutes />} /></Routes>
}