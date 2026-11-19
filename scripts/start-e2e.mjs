import { spawn, spawnSync } from 'node:child_process'
import { cpSync, mkdtempSync, mkdirSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('..', import.meta.url))
const temporary = mkdtempSync(join(tmpdir(), 'hexlet-e2e-'))
const output = join(temporary, 'app')
const database = join(temporary, 'calendar.db')
const port = process.env.HEXLET_E2E_PORT ?? '15294'
let server

function run(command, args) {
  const result = spawnSync(process.platform === 'win32' && command === 'npm' ? 'npm.cmd' : command, args, { cwd: root, stdio: 'inherit', shell: process.platform === 'win32' && command === 'npm' })
  if (result.error) throw result.error
  if (result.status !== 0) throw new Error(`${command} exited with ${result.status}`)
}

function cleanup() {
  server?.kill()
  rmSync(temporary, { recursive: true, force: true })
}

try {
  run('npm', ['run', 'build'])
  run('dotnet', ['tool', 'restore'])
  run('dotnet', ['ef', 'database', 'update', '--project', 'src/HexletCalendar.Data', '--startup-project', 'src/HexletCalendar.Api', '--connection', `Data Source=${database}`])
  run('dotnet', ['publish', 'src/HexletCalendar.Api/HexletCalendar.Api.csproj', '-c', 'Release', '-o', output])
  mkdirSync(join(output, 'wwwroot'), { recursive: true })
  cpSync(join(root, 'src', 'HexletCalendar.Web', 'dist'), join(output, 'wwwroot'), { recursive: true })
  server = spawn('dotnet', [join(output, 'HexletCalendar.Api.dll'), '--urls', `http://127.0.0.1:${port}`], {
    cwd: output,
    env: { ...process.env, ConnectionStrings__Calendar: `Data Source=${database};Mode=ReadWrite` },
    stdio: 'inherit',
  })
  server.on('exit', code => { process.exitCode = code ?? 1; cleanup() })
  process.on('SIGINT', () => { server.kill(); cleanup(); process.exit(130) })
  process.on('SIGTERM', () => { server.kill(); cleanup(); process.exit(143) })
} catch (error) {
  cleanup()
  throw error
}
