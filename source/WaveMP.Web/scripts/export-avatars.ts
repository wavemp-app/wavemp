// Creates the pictures of the built-in avatars under /assets/avatars from their SVG sources:
//   <id>.svg              source, drawn for WaveMP
//   <id>-<size>.webp      square WebP pictures in the sizes the API delivers avatars in
// The service embeds the WebP pictures and delivers them as the avatar of profiles that chose one.
// resvg renders the SVG to PNG, ffmpeg (on the PATH) encodes it as WebP. Files under /assets are
// never overwritten: existing files are skipped unless --force is given (only after the maintainer
// allowed it).
//
// Usage: npm run avatars:export [-- --force]

import { execFileSync } from 'node:child_process'
import { existsSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { renderPng } from './app-icons.ts'

/** Sizes of the avatars of the API (the sizes of covers): 96, 300 and 1000 pixels. */
export const avatarExportSizes = [96, 300, 1000] as const

/** WebP quality of ffmpeg's libwebp encoder (0 to 100). */
const webpQuality = 90

const avatarDirectory = fileURLToPath(new URL('../../../assets/avatars/', import.meta.url))
const force = process.argv.includes('--force')

function encodeWebp(png: Uint8Array, output: string, workDirectory: string): void {
    const input = path.join(workDirectory, 'avatar.png')
    writeFileSync(input, png)
    execFileSync(
        'ffmpeg',
        [
            '-hide_banner',
            '-loglevel',
            'error',
            '-y',
            '-i',
            input,
            '-map_metadata',
            '-1',
            '-c:v',
            'libwebp',
            '-quality',
            String(webpQuality),
            '-f',
            'webp',
            output,
        ],
        { stdio: 'inherit' },
    )
}

const workDirectory = mkdtempSync(path.join(tmpdir(), 'wavemp-avatars-'))
try {
    const sources = readdirSync(avatarDirectory).filter((name) => name.endsWith('.svg'))
    for (const source of sources) {
        const id = path.basename(source, '.svg')
        const svg = readFileSync(path.join(avatarDirectory, source), 'utf8')
        for (const size of avatarExportSizes) {
            const output = path.join(avatarDirectory, `${id}-${size}.webp`)
            if (existsSync(output) && !force) {
                console.log(`Skipped ${path.basename(output)} (exists; --force overwrites it)`)
                continue
            }

            encodeWebp(renderPng(svg, size), output, workDirectory)
            console.log(`Wrote ${path.basename(output)}`)
        }
    }
} finally {
    rmSync(workDirectory, { recursive: true, force: true })
}
