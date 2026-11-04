import { execFileSync, spawnSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import { fileURLToPath } from 'node:url'

const root = fileURLToPath(new URL('..', import.meta.url))
const baseline = readFileSync(new URL('../.commitlint-baseline', import.meta.url), 'utf8').trim()
const eventBefore = process.env.GITHUB_EVENT_BEFORE
const eventAfter = process.env.GITHUB_SHA ?? 'HEAD'

function isCommit(reference) {
  return spawnSync('git', ['cat-file', '-e', `${reference}^{commit}`], {
    cwd: root,
    stdio: 'ignore',
  }).status === 0
}

const before = eventBefore && !/^0+$/.test(eventBefore) && isCommit(eventBefore)
  ? eventBefore
  : baseline

if (!isCommit(before) || !isCommit(eventAfter)) {
  throw new Error('Unable to resolve the commit range for commitlint.')
}

const commits = execFileSync(
  'git',
  ['rev-list', '--reverse', `${before}..${eventAfter}`],
  { cwd: root, encoding: 'utf8' },
).trim().split(/\r?\n/u).filter(Boolean)

if (commits.length === 0) {
  console.log('commitlint: no commits after the project baseline')
  process.exit(0)
}

const commitlintCli = fileURLToPath(
  new URL('../node_modules/@commitlint/cli/cli.js', import.meta.url),
)

for (const commit of commits) {
  const message = execFileSync('git', ['show', '-s', '--format=%B', commit], {
    cwd: root,
    encoding: 'utf8',
  })
  const result = spawnSync(process.execPath, [commitlintCli, '--verbose'], {
    cwd: root,
    input: message,
    stdio: ['pipe', 'inherit', 'inherit'],
  })
  if (result.status !== 0) {
    process.exit(result.status ?? 1)
  }
}
