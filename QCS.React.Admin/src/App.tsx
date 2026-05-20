import { Navigate, Route, Routes } from 'react-router-dom'
import { AppLayout } from './components/layout/AppLayout.tsx'
import { WorkspacePage } from './pages/WorkspacePage.tsx'
import { AccessDeniedPage } from './pages/access-denied/AccessDeniedPage.tsx'
import { OverviewPage } from './pages/overview/OverviewPage.tsx'
import { RequestsPage } from './pages/requests/RequestsPage.tsx'
import { QuotationsPage } from './pages/quotations/QuotationsPage.tsx'
import { WorkflowPage } from './pages/workflow/WorkflowPage.tsx'
import { VendorsPage } from './pages/vendors/VendorsPage.tsx'
import { UserAccessPage } from './pages/users/UsersPage.tsx'
import { SustainabilityPage } from './pages/sustainability/SustainabilityPage.tsx'
import { workspacePages } from './pages/pageData.ts'

function App() {
  return (
    <Routes>
      <Route path="access-denied" element={<AccessDeniedPage />} />
      <Route element={<AppLayout />}>
        <Route index element={<OverviewPage />} />
        {workspacePages.map((page) =>
          <Route
            key={page.path}
            path={page.path.slice(1)}
            element={<WorkspacePage page={page} />}
          />,
        )}
        <Route path="requests" element={<RequestsPage />} />
        <Route path="quotations" element={<QuotationsPage />} />
        <Route path="workflow" element={<WorkflowPage />} />
        <Route path="vendors" element={<VendorsPage />} />
        <Route path="users" element={<UserAccessPage />} />
        <Route path="sustainability" element={<SustainabilityPage />} />
        <Route path="requester" element={<Navigate to="/users" replace />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  )
}

export default App
