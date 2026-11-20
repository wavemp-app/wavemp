// Creates the derived logo files under /assets/logo from the source logos of the maintainer:
//   wavemp-logo-default.svg        optimized copy (svgo) of wavemp-logo-fluent.svg, the default
//                                  logo used everywhere
//   wavemp-logo-default-dark.svg   optimized copy of wavemp-logo-fluent-dark.svg, its dark variant
//   wavemp-logo-default-<n>.png    PNG exports of wavemp-logo-default.svg (e.g. for app catalogs)
// The earlier logo (wavemp-logo.svg and its PNG exports, from wavemp-logo-teal-wave-round.svg) is
// kept unchanged as an alternative and is not written here any more.
// Files under /assets are never overwritten: existing files are skipped unless --force is given
// (only after the maintainer allowed it). The icons of the web app are not written here; the
// build generates them from the default logo and its dark variant.
//
// Usage: npm run logo:export [-- --force]

import { existsSync, readFileSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import {
    composeIconSvg,
    defaultLogoFiles,
    logoExportSizes,
    optimizeLogo,
    renderPng,
} from './app-icons.ts'

const logoDirectory = fileURLToPath(new URL('../../../assets/logo/', import.meta.url))
const force = process.argv.includes('--force')

function write(file: string, data: string | Uint8Array): void {
    if (existsSync(file) && !force) {
        console.log(`Skipped ${path.basename(file)} (exists; --force overwrites it)`)
        return
    }

    writeFileSync(file, data)
    console.log(`Wrote ${path.basename(file)}`)
}

for (const { source, target } of [defaultLogoFiles.light, defaultLogoFiles.dark]) {
    write(
        path.join(logoDirectory, target),
        optimizeLogo(readFileSync(path.join(logoDirectory, source), 'utf8')),
    )
}

const logo = composeIconSvg(
    readFileSync(path.join(logoDirectory, defaultLogoFiles.light.target), 'utf8'),
    'transparent',
)
for (const size of logoExportSizes) {
    write(path.join(logoDirectory, defaultLogoFiles.pngExport(size)), renderPng(logo, size))
}
