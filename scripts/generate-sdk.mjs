import { execFileSync } from 'node:child_process'
import { createClient } from '@hey-api/openapi-ts'
import { readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('..', import.meta.url))
if (typeof createClient !== 'function') throw new Error('SDK generator is unavailable')
const [input = join(root, 'src/HexletCalendar.Contracts/generated/openapi.json'), output = join(root, 'src/HexletCalendar.Contracts/generated/sdk')] = process.argv.slice(2)
execFileSync(process.execPath, [join(root, 'node_modules/@hey-api/openapi-ts/bin/run.js'), '-i', input, '-o', output], { cwd: root, stdio: 'inherit' })
const client = join(output, 'client.gen.ts')
writeFileSync(client, readFileSync(client, 'utf8').replace(/createConfig<ClientOptions2>\([^)]*\)/, 'createConfig<ClientOptions2>()'))
