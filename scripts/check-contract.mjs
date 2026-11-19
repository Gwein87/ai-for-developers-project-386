import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { readFileSync, mkdtempSync, rmSync, readdirSync } from 'node:fs'
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
assert.equal(services[0].operations.length, 6)
assert.ok(services[0].operations.every(operation => getOperationId(program, operation.operation)))
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
    execFileSync(process.execPath, [
      join(root, 'scripts', 'generate-sdk.mjs'),
      join(output, 'openapi.json'), join(output, 'sdk'),
    ], { cwd: root, stdio: 'inherit' })
    execFileSync('dotnet', [
      'run', '--project', 'src/HexletCalendar.ContractGenerator', '--configuration', 'Release', '--',
      join(output, 'openapi.json'), join(output, 'ApiDtos.g.cs'),
    ], { cwd: root, stdio: 'inherit' })
    for (const file of ['openapi.json', 'schema.d.ts', 'ApiDtos.g.cs']) {
      assert.deepEqual(readFileSync(join(output, file)), readFileSync(join(generated, file)),
        `${file}: generated output differs from the checked-in artifact`)
    }
    function compareDirectory(relative) {
      const actual = join(output, relative)
      const expected = join(generated, relative)
      const names = readdirSync(expected, { withFileTypes: true })
      assert.deepEqual(readdirSync(actual).sort(), names.map(item => item.name).sort())
      for (const entry of names) {
        const child = join(relative, entry.name)
        if (entry.isDirectory()) compareDirectory(child)
        else assert.deepEqual(readFileSync(join(output, child)), readFileSync(join(generated, child)), child)
      }
    }
    compareDirectory('sdk')
  }
  console.log('contract: two independent generations match all artifacts')
} finally {
  assert.equal(dirname(resolve(temporary)), resolve(tmpdir()))
  assert.ok(basename(temporary).startsWith('hexlet-contract-'))
  rmSync(temporary, { recursive: true, force: true })
}
