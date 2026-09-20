/// <reference types="vitest/config" />
import { existsSync, readFileSync } from 'node:fs'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

const CERT_PATH = './certs/todo-app.crt'
const KEY_PATH = './certs/todo-app.key'
const hasDevCerts = existsSync(CERT_PATH) && existsSync(KEY_PATH)

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    https: hasDevCerts
      ? { cert: readFileSync(CERT_PATH), key: readFileSync(KEY_PATH) }
      : undefined,
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./tests/setup.ts'],
    globals: true,
    env: {
      VITE_API_BASE_URL: 'https://localhost:7020',
    },
  },
})
