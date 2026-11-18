// Theme previews for theme designers: one Vite development server per theme, each on its own port,
// that serves the themes live from /assets/web/themes, shows its theme instead of the theme of the
// profile and passes the API on to a running WaveMP instance. Several previews run at the same
// time without touching each other: each has its own port, its own dependency cache of Vite and
// its own pid file. Pure functions only; scripts/theme-preview.ts starts and stops the servers.

/** Fixed ports of the previews, so every designer knows the address of their theme. */
export const previewPorts: Readonly<Record<string, number>> = {
    midnight: 5200,
    meadow: 5201,
    daylight: 5202,
    accent: 5203,
    pinstripe: 5204,
    'clear-glass': 5205,
}

/** WaveMP instance the previews pass /api to: the development container of docker compose. */
export const defaultServiceUrl = 'http://localhost:8080'

export interface PreviewOptions {
    readonly command: 'start' | 'stop'
    readonly theme: string
    readonly port: number
    readonly serviceUrl: string
}

/**
 * The options of the command line: `[stop] <theme> [--port <n>] [--service <url>]`. Throws with a
 * message for the user on invalid input.
 */
export function parsePreviewArguments(
    args: readonly string[],
    env: Readonly<Record<string, string | undefined>> = {},
): PreviewOptions {
    const rest = [...args]
    let command: PreviewOptions['command'] = 'start'
    if (rest[0] === 'stop' || rest[0] === 'start') {
        command = rest.shift() as PreviewOptions['command']
    }

    let theme: string | undefined
    let port: number | undefined
    let serviceUrl = env.WAVEMP_SERVICE_URL || defaultServiceUrl
    while (rest.length > 0) {
        const arg = rest.shift()!
        if (arg === '--port') {
            port = Number(rest.shift())
            if (!Number.isInteger(port) || port < 1024 || port > 65535) {
                throw new Error('--port needs a port from 1024 to 65535.')
            }
        } else if (arg === '--service') {
            serviceUrl = rest.shift() ?? ''
            if (!/^https?:\/\/[^/]+/.test(serviceUrl)) {
                throw new Error(
                    '--service needs the address of WaveMP, e.g. http://localhost:8080.',
                )
            }
        } else if (!arg.startsWith('--') && theme === undefined) {
            theme = arg
        } else {
            throw new Error(`Unknown argument: ${arg}`)
        }
    }

    if (!theme) {
        throw new Error(`Name a theme: ${Object.keys(previewPorts).join(', ')}.`)
    }

    const fixed = previewPorts[theme]
    if (fixed === undefined) {
        throw new Error(`Unknown theme "${theme}"; known: ${Object.keys(previewPorts).join(', ')}.`)
    }

    return { command, theme, port: port ?? fixed, serviceUrl }
}

/** Folder of the dependency cache of Vite of a preview (relative to the web app). */
export function previewCacheDirectory(theme: string): string {
    return `node_modules/.vite-theme-preview/${theme}`
}

/** File with the process id of a running preview (relative to the web app). */
export function previewPidFile(theme: string): string {
    return `node_modules/.vite-theme-preview/${theme}.pid`
}
