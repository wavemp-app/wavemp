// @vitest-environment node
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { Resvg } from '@resvg/resvg-js'
import { describe, expect, it } from 'vitest'

import {
    composeIconSvg,
    createAppIcons,
    createHeadLinks,
    createManifestIcons,
    encodeIco,
    faviconSizes,
    logoExportSizes,
    logoPath,
    optimizeLogo,
    parseSvg,
    pngSize,
    renderPng,
} from '../app-icons.ts'
import { appIcons } from '../vite-plugin-app-icons.ts'

const logoDirectory = fileURLToPath(new URL('../../../../assets/logo/', import.meta.url))
const sourceLogo = readFileSync(path.join(logoDirectory, 'wavemp-logo-teal-wave-round.svg'), 'utf8')
const logo = readFileSync(path.join(logoDirectory, 'wavemp-logo.svg'), 'utf8')

/** RGBA pixels of an SVG rendered at its own size. */
function pixels(svg: string): Uint8Array {
    return new Uint8Array(new Resvg(svg, { font: { loadSystemFonts: false } }).render().pixels)
}

/** Largest difference of a color channel between two renderings. */
function maxDifference(a: Uint8Array, b: Uint8Array): number {
    expect(a.length).toBe(b.length)
    let max = 0
    for (let index = 0; index < a.length; index++) {
        max = Math.max(max, Math.abs(a[index]! - b[index]!))
    }
    return max
}

/** Alpha value of a pixel of a rendered SVG. */
function alphaAt(svg: string, size: number, x: number, y: number): number {
    const rendered = new Resvg(svg, { fitTo: { mode: 'width', value: size } }).render()
    return rendered.pixels[(y * rendered.width + x) * 4 + 3]!
}

function rootElement(svg: string): string {
    return /<svg\b[^>]*>/.exec(svg)?.[0] ?? ''
}

describe('optimizeLogo', () => {
    it('keeps the viewBox and the accessible name', () => {
        const root = rootElement(optimizeLogo(sourceLogo))

        expect(root).toContain('viewBox="0 0 512 512"')
        expect(root).toContain('role="img"')
        expect(root).toContain('aria-label="WaveMP"')
    })

    it('makes the logo smaller without changing a pixel', () => {
        const optimized = optimizeLogo(sourceLogo)

        expect(optimized.length).toBeLessThan(sourceLogo.length)
        expect(maxDifference(pixels(optimized), pixels(sourceLogo))).toBe(0)
    })
})

describe('logo files under /assets/logo', () => {
    it('contain the optimized logo, visually identical to the source', () => {
        const root = rootElement(logo)

        expect(root).toContain('viewBox="0 0 512 512"')
        expect(root).toContain('role="img"')
        expect(root).toContain('aria-label="WaveMP"')
        expect(logo.length).toBeLessThan(sourceLogo.length)
        expect(maxDifference(pixels(logo), pixels(sourceLogo))).toBe(0)
    })

    it.each(logoExportSizes)('contain a PNG export of %i pixels', (size) => {
        const png = new Uint8Array(
            readFileSync(path.join(logoDirectory, `wavemp-logo-${size}.png`)),
        )

        expect(pngSize(png)).toEqual({ width: size, height: size })
    })

    it.each([
        'wavemp-logo-teal-wave-round.svg',
        'wavemp-logo-fluent.svg',
        'wavemp-logo-fluent-dark.svg',
        'wavemp-logo-mono-white.svg',
    ])('contain the original %s', (file) => {
        expect(existsSync(path.join(logoDirectory, file))).toBe(true)
    })
})

describe('parseSvg', () => {
    it('returns the viewBox and the content of the root element', () => {
        expect(parseSvg('<svg viewBox="0 0 10 20" role="img"><rect/></svg>\n')).toEqual({
            viewBox: '0 0 10 20',
            content: '<rect/>',
        })
    })

    it('rejects documents without an svg root or viewBox', () => {
        expect(() => parseSvg('<html></html>')).toThrow(/<svg> root/)
        expect(() => parseSvg('<svg width="10"><rect/></svg>')).toThrow(/viewBox/)
    })
})

describe('composeIconSvg', () => {
    it('keeps the transparent corners of the logo', () => {
        const svg = composeIconSvg(logo, 'transparent')

        expect(alphaAt(svg, 64, 0, 0)).toBe(0)
        expect(alphaAt(svg, 64, 32, 32)).toBe(255)
    })

    it('fills the whole square for icons that the platform masks', () => {
        const svg = composeIconSvg(logo, 'full-bleed')

        for (const [x, y] of [
            [0, 0],
            [63, 0],
            [0, 63],
            [63, 63],
            [32, 32],
        ] as const) {
            expect(alphaAt(svg, 64, x, y), `${x},${y}`).toBe(255)
        }
    })
})

describe('renderPng', () => {
    it('renders a square PNG of the given size', () => {
        const png = renderPng(composeIconSvg(logo, 'transparent'), 37)

        expect(pngSize(png)).toEqual({ width: 37, height: 37 })
    })
})

describe('pngSize', () => {
    it('rejects data that is not a PNG file', () => {
        expect(() => pngSize(new Uint8Array(32))).toThrow(/not a PNG/)
    })
})

describe('encodeIco', () => {
    it('writes the directory and the PNG images one after the other', () => {
        const small = new Uint8Array([1, 2, 3])
        const large = new Uint8Array([4, 5, 6, 7, 8])
        const ico = encodeIco([
            { size: 16, png: small },
            { size: 256, png: large },
        ])
        const view = new DataView(ico.buffer)

        expect(ico.length).toBe(6 + 2 * 16 + 3 + 5)
        expect([view.getUint16(0, true), view.getUint16(2, true), view.getUint16(4, true)]).toEqual(
            [0, 1, 2],
        )
        // First entry: 16x16, 32 bits per pixel, 3 bytes at offset 38.
        expect([ico[6], ico[7]]).toEqual([16, 16])
        expect(view.getUint16(6 + 6, true)).toBe(32)
        expect(view.getUint32(6 + 8, true)).toBe(3)
        expect(view.getUint32(6 + 12, true)).toBe(38)
        // Second entry: 256 is written as 0, 5 bytes at offset 41.
        expect([ico[22], ico[23]]).toEqual([0, 0])
        expect(view.getUint32(22 + 8, true)).toBe(5)
        expect(view.getUint32(22 + 12, true)).toBe(41)
        expect([...ico.subarray(38)]).toEqual([1, 2, 3, 4, 5, 6, 7, 8])
    })

    it('rejects an empty list and sizes above 256 pixels', () => {
        expect(() => encodeIco([])).toThrow(/at least one/)
        expect(() => encodeIco([{ size: 512, png: new Uint8Array(1) }])).toThrow(/1 to 256/)
    })
})

describe('createAppIcons', () => {
    const icons = createAppIcons(logo)
    const byName = new Map(icons.map((icon) => [icon.fileName, icon]))

    it('contains the logo unchanged', () => {
        const svg = byName.get(logoPath.slice(1))

        expect(svg?.contentType).toBe('image/svg+xml')
        expect(new TextDecoder().decode(svg?.data)).toBe(logo)
    })

    it('contains a favicon.ico with all favicon sizes', () => {
        const favicon = byName.get('favicon.ico')!
        const view = new DataView(favicon.data.buffer, favicon.data.byteOffset)

        expect(favicon.contentType).toBe('image/x-icon')
        expect(view.getUint16(4, true)).toBe(faviconSizes.length)
        faviconSizes.forEach((size, index) => {
            const entry = 6 + 16 * index
            const length = view.getUint32(entry + 8, true)
            const offset = view.getUint32(entry + 12, true)
            const png = favicon.data.subarray(offset, offset + length)

            expect(favicon.data[entry]).toBe(size)
            expect(pngSize(png)).toEqual({ width: size, height: size })
        })
    })

    it('renders every PNG icon in the size of its name', () => {
        const pngs = icons.filter((icon) => icon.contentType === 'image/png')

        expect(pngs.map((icon) => icon.fileName)).toEqual([
            'logo/apple-touch-icon-180x180.png',
            'logo/pwa-192x192.png',
            'logo/pwa-512x512.png',
            'logo/pwa-maskable-192x192.png',
            'logo/pwa-maskable-512x512.png',
        ])
        for (const icon of pngs) {
            const size = Number(/-(\d+)x\1\.png$/.exec(icon.fileName)?.[1])
            expect(pngSize(icon.data), icon.fileName).toEqual({ width: size, height: size })
        }
    })
})

describe('createManifestIcons', () => {
    const files = new Set(createAppIcons(logo).map((icon) => `/${icon.fileName}`))
    const icons = createManifestIcons()

    it('contains the sizes and purposes that installable web apps need', () => {
        expect(icons.map((icon) => `${icon.sizes} ${icon.purpose}`)).toEqual([
            '192x192 any',
            '512x512 any',
            '192x192 maskable',
            '512x512 maskable',
            'any any',
        ])
    })

    it('only refers to generated icons', () => {
        for (const icon of icons) {
            expect(files, icon.src).toContain(icon.src)
        }
    })
})

describe('createHeadLinks', () => {
    it('links the favicon, the SVG logo and the Apple touch icon, all generated', () => {
        const files = new Set(createAppIcons(logo).map((icon) => `/${icon.fileName}`))
        const links = createHeadLinks()

        expect(links.map((link) => `${link.rel} ${link.href}`)).toEqual([
            'icon /favicon.ico',
            `icon ${logoPath}`,
            'apple-touch-icon /logo/apple-touch-icon-180x180.png',
        ])
        for (const link of links) {
            expect(files, link.href).toContain(link.href)
        }
    })
})

describe('appIcons (Vite plugin)', () => {
    it('adds the icon links to the head of index.html', () => {
        const plugin = appIcons({ logo: path.join(logoDirectory, 'wavemp-logo.svg') })
        const transform = plugin.transformIndexHtml as unknown as () => {
            attrs: Record<string, string>
        }[]

        expect(transform().map((tag) => tag.attrs.href)).toEqual(
            createHeadLinks().map((link) => link.href),
        )
    })
})
