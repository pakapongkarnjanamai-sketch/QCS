import { Route, Routes } from 'react-router'
import { AppLayout } from '@/components/layout/AppLayout'
import { AccessDeniedPage } from '@/pages/AccessDeniedPage'
import { PlaceholderPage } from '@/pages/PlaceholderPage'

function PortalRoutes() {
  return <AppLayout><Routes>
    <Route index element={<PlaceholderPage title="Overview" />} />
    <Route path="requests" element={<PlaceholderPage title="Requests" />} />
    <Route path="requests/new" element={<PlaceholderPage title="New request" />} />
    <Route path="requests/:id/edit" element={<PlaceholderPage title="Edit request" />} />
    <Route path="requests/:id" element={<PlaceholderPage title="Request" />} />
    <Route path="quotations/:code" element={<PlaceholderPage title="Quotation" />} />
  </Routes></AppLayout>
}

export function App() {
  return <Routes><Route path="/access-denied" element={<AccessDeniedPage />} /><Route path="/*" element={<PortalRoutes />} /></Routes>
}