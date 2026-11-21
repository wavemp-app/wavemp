// @vitest-environment node
import { existsSync, readFileSync } from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

import { Resvg } from '@resvg/resvg-js'
import { describe, expect, it } from 'vitest'

import {
    adaptiveSvgStyle,
    composeAdaptiveSvg,
    composeIconSvg,
    createAppIcons,
    createHeadLinks,
    createManifestIcons,
    darkLogoPath,
    defaultLogoFiles,
    encodeIco,
    faviconSizes,
    faviconSvgPath,
    logoExportSizes,
    logoPath,
    optimizeLogo,
    parseSvg,
    pngSize,
    renderPng,
    squareTileCorners,
} from '../app-icons.ts'
import { appIcons } from '../vite-plugin-app-icons.ts'

const logoDirectory = fileURLToPath(new URL('../../../../assets/logo/', import.meta.url))
const readLogo = (file: string) => readFileSync(path.join(logoDirectory, file), 'utf8')
const sourceLogo = readLogo(defaultLogoFiles.light.source)
const sourceDarkLogo = readLogo(defaultLogoFiles.dark.source)
const logo = readLogo(defaultLogoFiles.light.target)
const darkLogo = readLogo(defaultLogoFiles.dark.target)

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

/** RGBA value of a pixel of a rendered SVG. */
function pixelAt(svg: string, size: number, x: number, y: number): number[] {
    const rendered = new Resvg(svg, { fitTo: { mode: 'width', value: size } }).render()
    const index = (y * rendered.width + x) * 4
    return [...rendered.pixels.subarray(index, index + 4)]
}

/** Alpha value of a pixel of a rendered SVG. */
function alphaAt(svg: string, size: number, x: number, y: number): number {
    return pixelAt(svg, size, x, y)[3]!
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

    it.each([
        ['light', sourceLogo],
        ['dark', sourceDarkLogo],
    ])('makes the %s logo smaller without changing a pixel', (_, source) => {
        const optimized = optimizeLogo(source)

        expect(optimized.length).toBeLessThan(source.length)
        expect(maxDifference(pixels(optimized), pixels(source))).toBe(0)
    })
})

describe('logo files under /assets/logo', () => {
    it('make the Fluent logo and its dark variant the default logo', () => {
        expect(defaultLogoFiles.light).toEqual({
            source: 'wavemp-logo-fluent.svg',
            target: 'wavemp-logo-default.svg',
        })
        expect(defaultLogoFiles.dark).toEqual({
            source: 'wavemp-logo-fluent-dark.svg',
            target: 'wavemp-logo-default-dark.svg',
        })
    })

    it.each([
        ['wavemp-logo-default.svg', logo, sourceLogo],
        ['wavemp-logo-default-dark.svg', darkLogo, sourceDarkLogo],
    ])('contain the optimized %s, visually identical to its source', (_, optimized, source) => {
        const root = rootElement(optimized)

        expect(root).toContain('viewBox="0 0 512 512"')
        expect(root).toContain('role="img"')
        expect(root).toContain('aria-label="WaveMP"')
        expect(optimized.length).toBeLessThan(source.length)
        expect(maxDifference(pixels(optimized), pixels(source))).toBe(0)
    })

    it.each(logoExportSizes)('contain a PNG export of the default logo of %i pixels', (size) => {
        const file = defaultLogoFiles.pngExport(size)
        const png = new Uint8Array(readFileSync(path.join(logoDirectory, file)))

        expect(file).toBe(`wavemp-logo-default-${size}.png`)
        expect(pngSize(png)).toEqual({ width: size, height: size })
        // npm run logo:export renders the default logo the same way.
        expect(png).toEqual(renderPng(composeIconSvg(logo, 'transparent'), size))
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

describe('squareTileCorners', () => {
    it('removes the round corners of every rect that covers the tile', () => {
        const content =
            '<clipPath id="b"><rect width="480" height="480" x="16" y="16" rx="108"/></clipPath>' +
            '<rect x="16" y="16" rx="108" ry="90" width="480" height="480" fill="red"/>' +
            '<rect width="100" height="100" x="16" y="16" rx="20"/>'

        expect(squareTileCorners(content)).toBe(
            '<clipPath id="b"><rect width="480" height="480" x="16" y="16"/></clipPath>' +
                '<rect x="16" y="16" width="480" height="480" fill="red"/>' +
                '<rect width="100" height="100" x="16" y="16" rx="20"/>',
        )
    })

    it('rejects a logo without a tile', () => {
        expect(() => squareTileCorners('<circle r="10"/>')).toThrow(/no tile/)
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

    it('continues the tile and the water seamlessly into the corners', () => {
        const svg = composeIconSvg(logo, 'full-bleed')
        // Each corner and a pixel further inside, which the round tile covers as well.
        const pairs = [
            [0, 0, 6, 6],
            [63, 0, 57, 6],
            [0, 63, 6, 59],
            [63, 63, 57, 59],
        ] as const

        for (const [x, y, insideX, insideY] of pairs) {
            const corner = pixelAt(svg, 64, x, y)
            const inside = pixelAt(svg, 64, insideX, insideY)
            const difference = Math.max(...corner.map((value, i) => Math.abs(value - inside[i]!)))
            expect(difference, `${x},${y}`).toBeLessThanOrEqual(16)
        }
    })

    it('shows the tile of the logo unchanged inside its round corners', () => {
        const fullBleed = pixelAt(composeIconSvg(logo, 'full-bleed'), 480, 240, 240)
        const original = pixelAt(logo, 512, 256, 256)

        expect(fullBleed).toEqual(original)
    })
})

describe('composeAdaptiveSvg', () => {
    const adaptive = composeAdaptiveSvg(logo, darkLogo)
    /** The favicon as the browser shows it in dark mode. */
    const darkScheme = adaptive.replace(
        adaptiveSvgStyle,
        '[data-scheme=light]{display:none}[data-scheme=dark]{display:inline}',
    )

    it('switches to the dark logo with the dark mode of the browser', () => {
        expect(adaptive).toContain('@media (prefers-color-scheme:dark)')
        expect(rootElement(adaptive)).toContain('viewBox="0 0 512 512"')
        expect(rootElement(adaptive)).toContain('aria-label="WaveMP"')
    })

    it('shows the light logo by default and the dark logo in dark mode, both unchanged', () => {
        expect(maxDifference(pixels(adaptive), pixels(logo))).toBe(0)
        expect(maxDifference(pixels(darkScheme), pixels(darkLogo))).toBe(0)
    })

    it('gives the ids of both logos their own prefix', () => {
        const ids = [...adaptive.matchAll(/\sid="([^"]+)"/g)].map((match) => match[1])

        expect(ids.length).toBeGreaterThan(0)
        expect(new Set(ids).size).toBe(ids.length)
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
    const icons = createAppIcons(logo, darkLogo)
    const byName = new Map(icons.map((icon) => [icon.fileName, icon]))

    it.each([
        [logoPath, logo],
        [darkLogoPath, darkLogo],
    ])('contains %s unchanged', (iconPath, expected) => {
        const svg = byName.get(iconPath.slice(1))

        expect(svg?.contentType).toBe('image/svg+xml')
        expect(new TextDecoder().decode(svg?.data)).toBe(expected)
    })

    it('contains the SVG favicon that follows the dark mode', () => {
        const svg = byName.get(faviconSvgPath.slice(1))

        expect(svg?.contentType).toBe('image/svg+xml')
        expect(new TextDecoder().decode(svg?.data)).toBe(composeAdaptiveSvg(logo, darkLogo))
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
    const files = new Set(createAppIcons(logo, darkLogo).map((icon) => `/${icon.fileName}`))
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
    it('links the favicon, the SVG favicon and the Apple touch icon, all generated', () => {
        const files = new Set(createAppIcons(logo, darkLogo).map((icon) => `/${icon.fileName}`))
        const links = createHeadLinks()

        expect(links.map((link) => `${link.rel} ${link.href}`)).toEqual([
            'icon /favicon.ico',
            'icon /logo/favicon.svg',
            'apple-touch-icon /logo/apple-touch-icon-180x180.png',
        ])
        for (const link of links) {
            expect(files, link.href).toContain(link.href)
        }
    })
})

describe('appIcons (Vite plugin)', () => {
    it('adds the icon links to the head of index.html', () => {
        const plugin = appIcons({
            logo: path.join(logoDirectory, defaultLogoFiles.light.target),
            darkLogo: path.join(logoDirectory, defaultLogoFiles.dark.target),
        })
        const transform = plugin.transformIndexHtml as unknown as () => {
            attrs: Record<string, string>
        }[]

        expect(transform().map((tag) => tag.attrs.href)).toEqual(
            createHeadLinks().map((link) => link.href),
        )
    })
})
