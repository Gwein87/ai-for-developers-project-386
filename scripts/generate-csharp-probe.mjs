import { spawnSync } from 'node:child_process'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('..', import.meta.url))
const result = spawnSync('dotnet', [
  'test', 'tests/HexletCalendar.Api.Tests/HexletCalendar.Api.Tests.csproj',
  '--configuration', 'Release', '--filter', 'FullyQualifiedName~CSharpDtoToolchainTests',
], {
  cwd: root,
  env: { ...process.env, HEXLET_UPDATE_PROBE_DTOS: '1' },
  stdio: 'inherit',
})
if (result.error) throw result.error
process.exit(result.status ?? 1)
