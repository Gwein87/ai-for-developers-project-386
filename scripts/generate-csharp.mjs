import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('..', import.meta.url))
const result = spawnSync('dotnet', [
  'run', '--project', 'src/HexletCalendar.ContractGenerator', '--configuration', 'Release', '--',
  'src/HexletCalendar.Contracts/generated/openapi.json',
  'src/HexletCalendar.Contracts/generated/ApiDtos.g.cs',
], { cwd: root, stdio: 'inherit' })
if (result.error) throw result.error
process.exit(result.status ?? 1)
