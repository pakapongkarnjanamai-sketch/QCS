import '@fontsource-variable/noto-sans-thai'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { BrowserRouter } from 'react-router'
import { App } from './App'
import { ErrorBoundary } from './components/ui/ErrorBoundary'
import { appConfig } from './config/appConfig'
import './index.css'

createRoot(document.getElementById('root')!).render(<StrictMode><BrowserRouter basename={appConfig.appBasePath}><ErrorBoundary><App /></ErrorBoundary></BrowserRouter></StrictMode>)