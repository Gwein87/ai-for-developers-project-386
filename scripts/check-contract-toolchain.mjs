import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { readFileSync, mkdtempSync, rmSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { basename, dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'
import { compile, NodeHost } from '@typespec/compiler'
import { getAllHttpServices } from '@typespec/http'
import { getOperationId } from '@typespec/openapi'
import { getOpenAPI3 } from '@typespec/openapi3'

const root = fileURLToPath(new URL('..', import.meta.url))
const contract = join(root, 'src', 'HexletCalendar.Contracts')
const generated = join(contract, 'generated')
const program = await compile(NodeHost, join(contract, 'main.tsp'), { noEmit: true })
assert.equal(program.diagnostics.length, 0)
const [services, diagnostics] = getAllHttpServices(program)
assert.equal(diagnostics.length, 0)
assert.equal(services.length, 1)
assert.equal(services[0].operations.length, 1)
assert.equal(getOperationId(program, services[0].operations[0].operation), 'getProbe')
const documents = await getOpenAPI3(program, { 'openapi-versions': ['3.0.0'] })
assert.equal(documents.length, 1)

const temporary = mkdtempSync(join(tmpdir(), 'hexlet-contract-'))
try {
  for (const attempt of ['first', 'second']) {
    const output = join(temporary, attempt)
    execFileSync(process.execPath, [
      join(root, 'node_modules', '@typespec', 'compiler', 'cmd', 'tsp.js'),
      'compile', contract, '--warn-as-error', '--option',
      `@typespec/openapi3.emitter-output-dir=${output}`,
    ], { cwd: root, stdio: 'inherit' })
    execFileSync(process.execPath, [
      join(root, 'node_modules', 'openapi-typescript', 'bin', 'cli.js'),
      join(output, 'openapi.json'), '-o', join(output, 'schema.d.ts'),
    ], { cwd: root, stdio: 'inherit' })
    for (const file of ['openapi.json', 'schema.d.ts']) {
      assert.deepEqual(readFileSync(join(output, file)), readFileSync(join(generated, file)),
        `${file}: generated output differs from the checked-in artifact`)
    }
  }
  console.log('contract probe: two independent generations match both artifacts')
} finally {
  assert.equal(dirname(resolve(temporary)), resolve(tmpdir()))
  assert.ok(basename(temporary).startsWith('hexlet-contract-'))
  rmSync(temporary, { recursive: true, force: true })
}
