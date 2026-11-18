// Takes the screenshots of a theme and checks its contrast (THEMING.md, "Checking a theme"):
//   npm run theme:screenshots -- [options]
//   npm run theme:contrast -- [options]        (the same with --contrast-only)
// Options:
//   --theme <id>[:<variant>]  theme to check, repeatable or separated by commas; default: all
//   --variant light|dark      variant of the named themes; default: all variants of the theme
//   --base-url <url>          WaveMP or a theme preview; default: E2E_BASE_URL or
//                             http://localhost:8080
//   --out <folder>            folder of the screenshots; default: theme-screenshots (one folder
//                             per theme and variant below it, with contrast.txt)
//   --views <a,b>             only these views (names of the files without .png)
//   --preview                 also writes the preview picture of the theme picker to
//                             assets/web/themes/<id>/preview-<variant>.webp
//   --contrast-only           checks the contrast without writing screenshots
//   --no-contrast             skips the contrast check
// The run fails if a text or icon misses the contrast of WCAG 2.2 AA; contrast.txt lists them.

import { spawnSync } from 'node:child_process'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const e2eDirectory = fileURLToPath(new URL('..', import.meta.url))

function fail(message: string): never {
    console.error(message)
    process.exit(1)
}

const args = process.argv.slice(2)
const themes: string[] = []
let variant: string | undefined
let baseUrl = process.env.E2E_BASE_URL ?? 'http://localhost:8080'
let out: string | undefined
let views = ''
const tasks = new Set(['screenshots', 'contrast'])

while (args.length > 0) {
    const arg = args.shift()!
    const value = () => args.shift() ?? fail(`${arg} needs a value.`)
    switch (arg) {
        case '--theme':
            themes.push(...value().split(','))
            break
        case '--variant':
            variant = value()
            if (variant !== 'light' && variant !== 'dark') {
                fail('--variant is light or dark.')
            }
            break
        case '--base-url':
            baseUrl = value()
            break
        case '--out':
            out = path.resolve(value())
            break
        case '--views':
            views = value()
            break
        case '--preview':
            tasks.add('preview')
            break
        case '--contrast-only':
            tasks.delete('screenshots')
            break
        case '--no-contrast':
            tasks.delete('contrast')
            break
        default:
            fail(`Unknown argument: ${arg}`)
    }
}

const targets = themes
    .map((theme) => theme.trim())
    .filter(Boolean)
    .map((theme) => (variant && !theme.includes(':') ? `${theme}:${variant}` : theme))

const result = spawnSync(
    process.execPath,
    [
        path.join(e2eDirectory, 'node_modules', '@playwright', 'test', 'cli.js'),
        'test',
        '--config',
        path.join(e2eDirectory, 'playwright.themes.config.ts'),
    ],
    {
        cwd: e2eDirectory,
        stdio: 'inherit',
        env: {
            ...process.env,
            E2E_BASE_URL: baseUrl,
            WAVEMP_THEME_TARGETS: targets.join(','),
            WAVEMP_THEME_TASKS: [...tasks].join(','),
            WAVEMP_THEME_VIEWS: views,
            ...(out ? { WAVEMP_THEME_OUT: out } : {}),
        },
    },
)
process.exit(result.status ?? 1)
