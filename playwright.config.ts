import { defineConfig } from '@playwright/test'

const port = 15294
export default defineConfig({
  testDir: './tests/HexletCalendar.E2E.Tests',
  workers: 1,
  retries: 0,
  use: { baseURL: `http://127.0.0.1:${port}`, trace: 'retain-on-failure', screenshot: 'only-on-failure' },
  webServer: {
    command: 'node scripts/start-e2e.mjs',
    url: `http://127.0.0.1:${port}/api/health`,
    reuseExistingServer: false,
    timeout: 120_000,
  },
})
