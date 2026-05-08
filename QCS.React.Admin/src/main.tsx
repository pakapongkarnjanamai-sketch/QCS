import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import config from 'devextreme/core/config'
import { licenseKey } from './devextreme-license.ts'
import { appConfig } from './config/appConfig.ts'
import 'devextreme/dist/css/dx.light.css'
import './index.css'
import App from './App.tsx'

config({ licenseKey })

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <BrowserRouter basename={appConfig.appBasePath}>
      <App />
    </BrowserRouter>
  </StrictMode>,
)
