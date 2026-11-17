// Creates the derived logo files under /assets/logo from the source logo of the maintainer:
//   wavemp-logo.svg       optimized copy (svgo), the logo used everywhere
//   wavemp-logo-<n>.png   PNG exports of wavemp-logo.svg (e.g. for app catalogs)
// Files under /assets are never overwritten: existing files are skipped unless --force is given
// (only after the maintainer allowed it). The icons of the web app are not written here; the
// build generates them from wavemp-logo.svg.
//
// Usage: npm run logo:export [-- --force]

import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { composeIconSvg, logoExportSizes, optimizeLogo, renderPng } from './app-icons.ts'

const logoDirectory = fileURLToPath(new URL('../../../assets/logo/', import.meta.url))
const sourceFile = path.join(logoDirectory, 'wavemp-logo-teal-wave-round.svg')
const logoFile = path.join(logoDirectory, 'wavemp-logo.svg')
const force = process.argv.includes('--force')

function write(file: string, data: string | Uint8Array): void {
    if (existsSync(file) && !force) {
        console.log(`Skipped ${path.basename(file)} (exists; --force overwrites it)`)
        return
    }

    writeFileSync(file, data)
    console.log(`Wrote ${path.basename(file)}`)
}

write(logoFile, optimizeLogo(readFileSync(sourceFile, 'utf8')))

const logo = composeIconSvg(readFileSync(logoFile, 'utf8'), 'transparent')
for (const size of logoExportSizes) {
    write(path.join(logoDirectory, `wavemp-logo-${size}.png`), renderPng(logo, size))
}
