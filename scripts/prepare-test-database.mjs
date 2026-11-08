import { mkdirSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'
import { spawnSync } from 'node:child_process'

const root = fileURLToPath(new URL('..', import.meta.url))
const fixtures = join(root, 'tests', 'HexletCalendar.Api.Tests', 'Fixtures')
mkdirSync(fixtures, { recursive: true })
const result = spawnSync('dotnet', [
  'ef', 'database', 'update', '--project', 'src/HexletCalendar.Data', '--startup-project', 'src/HexletCalendar.Api',
  '--connection', `Data Source=${join(fixtures, 'calendar.db')}`,
], { cwd: root, stdio: 'inherit' })
if (result.error) throw result.error
process.exit(result.status ?? 1)
