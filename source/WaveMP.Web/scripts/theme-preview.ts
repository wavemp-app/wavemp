// Starts or stops the preview of a theme for theme designers (see THEMING.md, "Preview"):
//   npm run theme:preview -- <theme> [--port <n>] [--service <url>]
//   npm run theme:preview -- stop <theme>
// The preview is a Vite development server on the port of the theme (scripts/theme-previews.ts)
// that shows the theme instead of the theme of the profile, serves /assets/web live (a changed
// theme.css shows on the next load) and passes /api to a running WaveMP instance (by default the
// development container on http://localhost:8080). It runs until it is stopped.

import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import {
    parsePreviewArguments,
    previewCacheDirectory,
    previewPidFile,
    type PreviewOptions,
} from './theme-previews.ts'

const webDirectory = fileURLToPath(new URL('..', import.meta.url))

function readPid(file: string): number | null {
    try {
        const pid = Number(readFileSync(file, 'utf8').trim())
        return Number.isInteger(pid) && pid > 0 ? pid : null
    } catch {
        return null
    }
}

function stop(options: PreviewOptions): void {
    const file = path.join(webDirectory, previewPidFile(options.theme))
    const pid = readPid(file)
    if (pid === null) {
        console.log(`No preview of ${options.theme} is running.`)
        return
    }

    try {
        process.kill(pid)
        console.log(`Stopped the preview of ${options.theme} (process ${pid}).`)
    } catch {
        console.log(`The preview of ${options.theme} was not running any more.`)
    }

    rmSync(file, { force: true })
}

async function start(options: PreviewOptions): Promise<void> {
    // Read by vite.config.ts when the server loads it.
    process.env.WAVEMP_PREVIEW_THEME = options.theme
    process.env.WAVEMP_SERVICE_URL = options.serviceUrl

    const { createServer } = await import('vite')
    const server = await createServer({
        root: webDirectory,
        configFile: path.join(webDirectory, 'vite.config.ts'),
        cacheDir: path.join(webDirectory, previewCacheDirectory(options.theme)),
        clearScreen: false,
        server: { host: 'localhost', port: options.port, strictPort: true, open: false },
    })
    await server.listen()

    const pidFile = path.join(webDirectory, previewPidFile(options.theme))
    mkdirSync(path.dirname(pidFile), { recursive: true })
    writeFileSync(pidFile, String(process.pid))

    const close = async () => {
        rmSync(pidFile, { force: true })
        await server.close()
        process.exit(0)
    }
    process.on('SIGINT', () => void close())
    process.on('SIGTERM', () => void close())

    console.log(
        `Preview of ${options.theme}: http://localhost:${options.port}/ (API: ${options.serviceUrl})`,
    )
}

try {
    const options = parsePreviewArguments(process.argv.slice(2), process.env)
    if (options.command === 'stop') {
        stop(options)
    } else {
        await start(options)
    }
} catch (error) {
    console.error(error instanceof Error ? error.message : error)
    process.exit(1)
}
