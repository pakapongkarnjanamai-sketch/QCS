import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import path from 'node:path'

function normalizeBuildBasePath(value: string | undefined): string {
  const raw = (value ?? '').trim()
  if (!raw || raw === '/') return '/'
  const withLeadingSlash = raw.startsWith('/') ? raw : `/${raw}`
  return withLeadingSlash.endsWith('/') ? withLeadingSlash : `${withLeadingSlash}/`
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  return {
    base: normalizeBuildBasePath(env.VITE_QCS_USER_APP_BASE_PATH),
    plugins: [react(), tailwindcss()],
    resolve: { alias: { '@': path.resolve(import.meta.dirname, './src') } },
    build: {
      rollupOptions: {
        output: {
          manualChunks: (id: string) => {
            if (id.includes('react-dom') || id.includes('react-router')) return 'react-vendor'
            if (id.includes('@microsoft/signalr')) return 'signalr'
            return undefined
          },
        },
      },
    },
    server: { port: 5175, strictPort: true },
  }
})