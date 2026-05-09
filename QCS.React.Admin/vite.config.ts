import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

function normalizeBuildBasePath(value?: string) {
  const trimmedValue = value?.trim()

  if (!trimmedValue || trimmedValue === '/') {
    return '/'
  }

  return `/${trimmedValue.replace(/^\/+|\/+$/g, '')}/`
}

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  return {
    base: normalizeBuildBasePath(env.VITE_QCS_ADMIN_APP_BASE_PATH),
    plugins: [react(), tailwindcss()],
    build: {
      rollupOptions: {
        output: {
          manualChunks: (id) => {
            if (id.includes('devextreme')) return 'devextreme'
            if (id.includes('react-dom') || id.includes('react-router')) return 'react-vendor'
          },
        },
      },
    },
  }
})
