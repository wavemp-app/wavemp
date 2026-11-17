// App icons of WaveMP, all derived from the logo /assets/logo/wavemp-logo.svg: the optimized logo
// itself (svgo), its PNG exports and the icons of the web app (favicon, PWA and Apple touch
// icons). Used by the Vite plugin (vite-plugin-app-icons.ts) during the build and by
// export-logo.ts for the files under /assets/logo.

import { Resvg } from '@resvg/resvg-js'
import { optimize } from 'svgo'

/** A generated file of the web app, with its path relative to the web root. */
export interface AppIconFile {
    fileName: string
    contentType: string
    data: Uint8Array
}

/** An icon entry of the web app manifest. */
export interface ManifestIcon {
    src: string
    sizes: string
    type: string
    purpose?: 'any' | 'maskable'
}

/** A link element in the head of index.html. */
export interface HeadLink {
    rel: string
    href: string
    type?: string
    sizes?: string
}

/**
 * How an icon shows the logo: "transparent" keeps the logo with its transparent margin and round
 * corners; "full-bleed" lets the glass tile of the logo fill the whole square on its own
 * background, for icons the platform masks itself (maskable PWA icons, Apple touch icon).
 */
export type IconStyle = 'transparent' | 'full-bleed'

interface PngIcon {
    fileName: string
    size: number
    style: IconStyle
}

/** Folder of the generated icons in the web app. */
export const appIconDirectory = 'logo'

/** Path of the logo in the web app; the default of the theme variable --wmp-image-logo. */
export const logoPath = `/${appIconDirectory}/wavemp-logo.svg`

/** Sizes of the favicon.ico (the browser picks the size it needs). */
export const faviconSizes = [16, 32, 48] as const

/** Sizes of the PNG exports under /assets/logo (e.g. for app catalogs). */
export const logoExportSizes = [256, 512] as const

const appleTouchIcon: PngIcon = {
    fileName: `${appIconDirectory}/apple-touch-icon-180x180.png`,
    size: 180,
    style: 'full-bleed',
}

const manifestPngIcons: PngIcon[] = [
    { fileName: `${appIconDirectory}/pwa-192x192.png`, size: 192, style: 'transparent' },
    { fileName: `${appIconDirectory}/pwa-512x512.png`, size: 512, style: 'transparent' },
    { fileName: `${appIconDirectory}/pwa-maskable-192x192.png`, size: 192, style: 'full-bleed' },
    { fileName: `${appIconDirectory}/pwa-maskable-512x512.png`, size: 512, style: 'full-bleed' },
]

// The glass tile of the logo (viewBox 0 0 512 512) is the square from 16 to 496 with round
// corners. Full-bleed icons show exactly this square and fill the round corners with the
// gradient of the tile, so masks of any shape cut into the tile, never into empty space.
const logoTileViewBox = '16 16 480 480'
const tileGradientStops: [offset: number, color: string][] = [
    [0, '#33b4e4'],
    [0.55, '#1786c6'],
    [1, '#0b5c98'],
]

/**
 * Optimizes the source logo with svgo: smaller, visually identical, and it keeps the viewBox
 * and the accessible name (role="img", aria-label).
 */
export function optimizeLogo(sourceSvg: string): string {
    const result = optimize(sourceSvg, {
        multipass: true,
        plugins: [
            {
                name: 'preset-default',
                params: {
                    overrides: {
                        removeUnknownsAndDefaults: { keepRoleAttr: true, keepAriaAttrs: true },
                    },
                },
            },
        ],
    })

    return `${result.data}\n`
}

/** Splits an SVG document into the viewBox of its root element and its content. */
export function parseSvg(svg: string): { viewBox: string; content: string } {
    const root = /<svg\b[^>]*>/.exec(svg)
    const end = svg.lastIndexOf('</svg>')
    if (!root || end < root.index) {
        throw new Error('The logo is not an SVG document with an <svg> root element.')
    }

    const viewBox = /\sviewBox\s*=\s*["']([^"']+)["']/.exec(root[0])?.[1]
    if (!viewBox) {
        throw new Error('The root element of the logo has no viewBox.')
    }

    return { viewBox, content: svg.slice(root.index + root[0].length, end) }
}

/**
 * The SVG of an icon in the given style: the logo as it is ("transparent") or its tile edge to
 * edge on the tile gradient ("full-bleed").
 */
export function composeIconSvg(logoSvg: string, style: IconStyle): string {
    const { viewBox, content } = parseSvg(logoSvg)
    if (style === 'transparent') {
        return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${viewBox}">${content}</svg>`
    }

    const stops = tileGradientStops
        .map(([offset, color]) => `<stop offset="${offset}" stop-color="${color}"/>`)
        .join('')

    return (
        `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 512 512">` +
        `<defs><linearGradient id="wmp-app-icon-tile" x1="0" y1="0" x2="0.35" y2="1">` +
        `${stops}</linearGradient></defs>` +
        `<rect width="512" height="512" fill="url(#wmp-app-icon-tile)"/>` +
        `<svg width="512" height="512" viewBox="${logoTileViewBox}">${content}</svg>` +
        `</svg>`
    )
}

/** Renders an SVG document to a square PNG of the given size (width and height in pixels). */
export function renderPng(svg: string, size: number): Uint8Array {
    const resvg = new Resvg(svg, {
        fitTo: { mode: 'width', value: size },
        font: { loadSystemFonts: false },
    })

    return new Uint8Array(resvg.render().asPng())
}

/** Width and height of a PNG file, read from its IHDR chunk. */
export function pngSize(png: Uint8Array): { width: number; height: number } {
    const signature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]
    if (png.length < 24 || signature.some((byte, index) => png[index] !== byte)) {
        throw new Error('The data is not a PNG file.')
    }

    const view = new DataView(png.buffer, png.byteOffset, png.byteLength)
    return { width: view.getUint32(16), height: view.getUint32(20) }
}

/**
 * Packs square PNG images into an ICO file (PNG-compressed entries, supported by all current
 * browsers and Windows Vista or later).
 */
export function encodeIco(images: { size: number; png: Uint8Array }[]): Uint8Array {
    if (images.length === 0) {
        throw new Error('An ICO file needs at least one image.')
    }

    const headerSize = 6
    const entrySize = 16
    const totalSize =
        headerSize + entrySize * images.length + images.reduce((sum, i) => sum + i.png.length, 0)
    const ico = new Uint8Array(totalSize)
    const view = new DataView(ico.buffer)
    view.setUint16(0, 0, true) // reserved
    view.setUint16(2, 1, true) // type: icon
    view.setUint16(4, images.length, true)

    let offset = headerSize + entrySize * images.length
    images.forEach(({ size, png }, index) => {
        if (size < 1 || size > 256) {
            throw new Error(`An ICO image must be 1 to 256 pixels in size, not ${size}.`)
        }

        const entry = headerSize + entrySize * index
        view.setUint8(entry, size === 256 ? 0 : size) // width, 0 means 256
        view.setUint8(entry + 1, size === 256 ? 0 : size) // height
        view.setUint8(entry + 2, 0) // colors in the palette: none
        view.setUint8(entry + 3, 0) // reserved
        view.setUint16(entry + 4, 1, true) // color planes
        view.setUint16(entry + 6, 32, true) // bits per pixel
        view.setUint32(entry + 8, png.length, true)
        view.setUint32(entry + 12, offset, true)
        ico.set(png, offset)
        offset += png.length
    })

    return ico
}

/** All icons of the web app, generated from the optimized logo. */
export function createAppIcons(logoSvg: string): AppIconFile[] {
    const transparent = composeIconSvg(logoSvg, 'transparent')
    const fullBleed = composeIconSvg(logoSvg, 'full-bleed')
    const png = (icon: PngIcon): AppIconFile => ({
        fileName: icon.fileName,
        contentType: 'image/png',
        data: renderPng(icon.style === 'transparent' ? transparent : fullBleed, icon.size),
    })

    return [
        {
            fileName: logoPath.slice(1),
            contentType: 'image/svg+xml',
            data: new TextEncoder().encode(logoSvg),
        },
        {
            fileName: 'favicon.ico',
            contentType: 'image/x-icon',
            data: encodeIco(
                faviconSizes.map((size) => ({ size, png: renderPng(transparent, size) })),
            ),
        },
        png(appleTouchIcon),
        ...manifestPngIcons.map(png),
    ]
}

/** Icons of the web app manifest. */
export function createManifestIcons(): ManifestIcon[] {
    return [
        ...manifestPngIcons.map((icon) => ({
            src: `/${icon.fileName}`,
            sizes: `${icon.size}x${icon.size}`,
            type: 'image/png',
            purpose: icon.style === 'full-bleed' ? ('maskable' as const) : ('any' as const),
        })),
        { src: logoPath, sizes: 'any', type: 'image/svg+xml', purpose: 'any' },
    ]
}

/** Icon links in the head of index.html (favicon, SVG icon, Apple touch icon). */
export function createHeadLinks(): HeadLink[] {
    return [
        // sizes on the ICO lets browsers that support SVG icons prefer the SVG.
        { rel: 'icon', href: '/favicon.ico', sizes: '32x32' },
        { rel: 'icon', href: logoPath, type: 'image/svg+xml', sizes: 'any' },
        { rel: 'apple-touch-icon', href: `/${appleTouchIcon.fileName}` },
    ]
}
