import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const apiTarget =
  process.env.services__api__https__0 ??
  process.env.services__api__http__0 ??
  'http://localhost:5000'

export default defineConfig({
  plugins: [react()],
  server: {
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
