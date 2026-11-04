import { fileURLToPath, URL } from 'node:url'
import { defineConfig, mergeConfig } from 'vitest/config'
import viteConfig from './src/HexletCalendar.Web/vite.config.ts'

export default mergeConfig(viteConfig, defineConfig({
  test: {
    environment: 'jsdom',
    include: ['tests/HexletCalendar.Web.Tests/**/*.test.{ts,tsx}'],
    setupFiles: [fileURLToPath(new URL('./tests/HexletCalendar.Web.Tests/setup.ts', import.meta.url))],
  },
}))
