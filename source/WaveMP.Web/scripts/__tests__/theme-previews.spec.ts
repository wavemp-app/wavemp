// @vitest-environment node
import { readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { describe, expect, it } from 'vitest'
import {
    defaultServiceUrl,
    parsePreviewArguments,
    previewCacheDirectory,
    previewPidFile,
    previewPorts,
} from '../theme-previews'

// The ids of the shipped themes, read from the registry of the web app (a DOM module).
const registry = readFileSync(
    path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../src/themes/themes.ts'),
    'utf8',
)
const themeIds = [...registry.matchAll(/builtIn\('([a-z0-9-]+)'/g)].map((match) => match[1])

describe('theme previews', () => {
    it('have a port of their own for every shipped theme', () => {
        expect(themeIds.length).toBeGreaterThan(1)
        expect(Object.keys(previewPorts).sort()).toEqual([...themeIds].sort())
        expect(previewPorts).toMatchObject({
            meadow: 5201,
            daylight: 5202,
            accent: 5203,
            pinstripe: 5204,
            'clear-glass': 5205,
        })
        expect(new Set(Object.values(previewPorts)).size).toBe(themeIds.length)
    })

    it('start the named theme on its port against the development container', () => {
        expect(parsePreviewArguments(['accent'])).toEqual({
            command: 'start',
            theme: 'accent',
            port: 5203,
            serviceUrl: defaultServiceUrl,
        })
        expect(parsePreviewArguments(['start', 'meadow', '--port', '6001'])).toMatchObject({
            theme: 'meadow',
            port: 6001,
        })
    })

    it('take the address of WaveMP from the environment or the command line', () => {
        expect(
            parsePreviewArguments(['daylight'], { WAVEMP_SERVICE_URL: 'http://localhost:5080' })
                .serviceUrl,
        ).toBe('http://localhost:5080')
        expect(
            parsePreviewArguments(['daylight', '--service', 'http://127.0.0.1:9000']).serviceUrl,
        ).toBe('http://127.0.0.1:9000')
    })

    it('stop the named theme', () => {
        expect(parsePreviewArguments(['stop', 'clear-glass'])).toMatchObject({
            command: 'stop',
            theme: 'clear-glass',
        })
    })

    it('reject missing or unknown themes and invalid options', () => {
        expect(() => parsePreviewArguments([])).toThrow(/Name a theme/)
        expect(() => parsePreviewArguments(['sepia'])).toThrow(/Unknown theme/)
        expect(() => parsePreviewArguments(['accent', '--port', '80'])).toThrow(/--port/)
        expect(() => parsePreviewArguments(['accent', '--service', 'localhost'])).toThrow(
            /--service/,
        )
        expect(() => parsePreviewArguments(['accent', '--verbose'])).toThrow(/Unknown argument/)
    })

    it('keep their caches and pid files apart', () => {
        expect(previewCacheDirectory('accent')).not.toBe(previewCacheDirectory('meadow'))
        expect(previewPidFile('accent')).toBe('node_modules/.vite-theme-preview/accent.pid')
    })
})
