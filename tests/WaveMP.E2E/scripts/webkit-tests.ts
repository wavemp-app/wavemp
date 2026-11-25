// Runs the WebKit tests (tests/webkit, projects "webkit-iphone" and "webkit-ipad") in the Playwright
// image of the CI, against a running WaveMP instance with the synthetic test media:
//   npm run test:webkit
// Further arguments go to `playwright test`, e.g. `-- --project webkit-ipad`. Playwright's WebKit
// of Windows has neither Web Audio nor the Media Session API, which these tests need; the WebKit of
// Linux in the image has both. The image is the one pinned in .github/workflows/test.yml. On Linux
// the container uses the host network; on Windows and macOS it forwards
// localhost:<port of E2E_BASE_URL> to the Docker host, so the app keeps its origin (see
// theme-regression.ts, which runs the theme regression the same way). Needs Docker and
// `npm install` in tests/WaveMP.E2E.

import { spawnSync } from 'node:child_process'
import { readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const e2eDirectory = fileURLToPath(new URL('..', import.meta.url))
const repositoryDirectory = path.resolve(e2eDirectory, '..', '..')
const workflow = readFileSync(
    path.join(repositoryDirectory, '.github', 'workflows', 'test.yml'),
    'utf8',
)
const image = /PLAYWRIGHT_IMAGE:\s*(\S+)/.exec(workflow)?.[1]
if (!image) {
    console.error('PLAYWRIGHT_IMAGE not found in .github/workflows/test.yml.')
    process.exit(1)
}

const baseUrl = new URL(process.env.E2E_BASE_URL ?? 'http://localhost:8080')
const port = baseUrl.port || '80'
const args = process.argv.slice(2)
const projects = args.includes('--project')
    ? []
    : ['--project', 'webkit-iphone', '--project', 'webkit-ipad']
const playwrightArgs = [
    'node',
    'node_modules/@playwright/test/cli.js',
    'test',
    ...projects,
    ...args,
]
const quote = (arg: string) => `'${arg.replaceAll("'", "'\\''")}'`
const linux = process.platform === 'linux'
const command = linux
    ? playwrightArgs.map(quote).join(' ')
    : `node scripts/forward-port.mjs ${port} host.docker.internal ${port} & ` +
      `sleep 1; ${playwrightArgs.map(quote).join(' ')}`

const dockerArgs = [
    'run',
    '--rm',
    '--init',
    '--ipc',
    'host',
    ...(linux ? ['--network', 'host'] : ['--add-host', 'host.docker.internal:host-gateway']),
    ...(linux && process.getuid && process.getgid
        ? ['--user', `${process.getuid()}:${process.getgid()}`]
        : []),
    '--env',
    'HOME=/tmp',
    '--env',
    `E2E_BASE_URL=${baseUrl.origin}`,
    '--volume',
    `${repositoryDirectory}:/repo`,
    '--workdir',
    '/repo/tests/WaveMP.E2E',
    image,
    'sh',
    '-c',
    command,
]

console.log(`WebKit tests in ${image.split('@')[0]} against ${baseUrl.origin}`)
const result = spawnSync('docker', dockerArgs, { stdio: 'inherit' })
process.exit(result.status ?? 1)
