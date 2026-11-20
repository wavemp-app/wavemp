// App icons of WaveMP, all derived from the default logo /assets/logo/wavemp-logo-default.svg (the
// Fluent logo) and its dark variant wavemp-logo-default-dark.svg: the optimized logos themselves
// (svgo), their PNG exports and the icons of the web app (favicon, PWA and Apple touch icons). Used
// by the Vite plugin (vite-plugin-app-icons.ts) during the build and by export-logo.ts for the
// files under /assets/logo.

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
 * corners; "full-bleed" lets the tile of the logo fill the whole square, for icons the platform
 * masks itself (maskable PWA icons, Apple touch icon).
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

/** Path of the dark variant of the logo in the web app (dark variants of themes). */
export const darkLogoPath = `/${appIconDirectory}/wavemp-logo-dark.svg`

/**
 * Path of the SVG favicon: the logo, which shows its dark variant while the browser or the system
 * is in dark mode (prefers-color-scheme).
 */
export const faviconSvgPath = `/${appIconDirectory}/favicon.svg`

/** Sizes of the favicon.ico (the browser picks the size it needs). */
export const faviconSizes = [16, 32, 48] as const

/** Sizes of the PNG exports under /assets/logo (e.g. for app catalogs). */
export const logoExportSizes = [256, 512] as const

/**
 * The default logo under /assets/logo: the source logos of the maintainer (never changed) and the
 * optimized copies and PNG exports made from them by export-logo.ts.
 */
export const defaultLogoFiles = {
    light: { source: 'wavemp-logo-fluent.svg', target: 'wavemp-logo-default.svg' },
    dark: { source: 'wavemp-logo-fluent-dark.svg', target: 'wavemp-logo-default-dark.svg' },
    pngExport: (size: number) => `wavemp-logo-default-${size}.png`,
} as const

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

// The tile of the logo (viewBox 0 0 512 512) is the square from 16 to 496 with round corners: a
// rect that fills it, and the same rect as clip path of the water. Full-bleed icons show exactly
// this square with square corners, so the gradient and the water reach the edges and masks of any
// shape cut into the tile, never into empty space.
const logoTile = { x: 16, y: 16, size: 480 }
const logoTileViewBox = `${logoTile.x} ${logoTile.y} ${logoTile.size} ${logoTile.size}`

/**
 * Removes the round corners (rx, ry) of every rect that covers exactly the tile of the logo.
 * Throws if the logo has no such rect, so a new logo without a tile is noticed.
 */
export function squareTileCorners(content: string): string {
    let found = false
    const result = content.replace(/<rect\b[^>]*>/g, (rect) => {
        const attribute = (name: string) =>
            Number(new RegExp(`\\s${name}="([^"]*)"`).exec(rect)?.[1] ?? Number.NaN)
        const isTile =
            attribute('x') === logoTile.x &&
            attribute('y') === logoTile.y &&
            attribute('width') === logoTile.size &&
            attribute('height') === logoTile.size
        if (!isTile) {
            return rect
        }

        found = true
        return rect.replace(/\s(?:rx|ry)="[^"]*"/g, '')
    })
    if (!found) {
        throw new Error(`The logo has no tile (a rect at ${logoTileViewBox}).`)
    }

    return result
}

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
 * edge with square corners ("full-bleed").
 */
export function composeIconSvg(logoSvg: string, style: IconStyle): string {
    const { viewBox, content } = parseSvg(logoSvg)
    if (style === 'transparent') {
        return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${viewBox}">${content}</svg>`
    }

    return (
        `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${logoTileViewBox}">` +
        `${squareTileCorners(content)}</svg>`
    )
}

/** Style of the SVG favicon that switches between its light and its dark logo. */
export const adaptiveSvgStyle =
    '[data-scheme=dark]{display:none}' +
    '@media (prefers-color-scheme:dark){[data-scheme=light]{display:none}[data-scheme=dark]{display:inline}}'

/** Gives every id of an SVG document (and every reference to it) the prefix. */
function prefixIds(svg: string, prefix: string): string {
    return optimize(svg, {
        plugins: [{ name: 'prefixIds', params: { prefix, delim: '-' } }],
    }).data
}

/**
 * The SVG favicon: the light logo, and the dark one while the browser or the system is in dark
 * mode. The ids of both logos get their own prefix, so they do not collide in one document.
 */
export function composeAdaptiveSvg(lightSvg: string, darkSvg: string): string {
    const light = parseSvg(prefixIds(lightSvg, 'wmp-light'))
    const dark = parseSvg(prefixIds(darkSvg, 'wmp-dark'))

    return (
        `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${light.viewBox}" role="img" ` +
        `aria-label="WaveMP">` +
        `<style>${adaptiveSvgStyle}</style>` +
        `<svg data-scheme="light" viewBox="${light.viewBox}">${light.content}</svg>` +
        `<svg data-scheme="dark" viewBox="${dark.viewBox}">${dark.content}</svg>` +
        `</svg>\n`
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

/**
 * All icons of the web app, generated from the optimized logo and its dark variant. The dark
 * variant is used where the platform switches the icon with its dark mode (the SVG favicon) and
 * by the dark variants of themes; all other icons show the light logo.
 */
export function createAppIcons(logoSvg: string, darkLogoSvg: string): AppIconFile[] {
    const transparent = composeIconSvg(logoSvg, 'transparent')
    const fullBleed = composeIconSvg(logoSvg, 'full-bleed')
    const png = (icon: PngIcon): AppIconFile => ({
        fileName: icon.fileName,
        contentType: 'image/png',
        data: renderPng(icon.style === 'transparent' ? transparent : fullBleed, icon.size),
    })
    const svg = (iconPath: string, data: string): AppIconFile => ({
        fileName: iconPath.slice(1),
        contentType: 'image/svg+xml',
        data: new TextEncoder().encode(data),
    })

    return [
        svg(logoPath, logoSvg),
        svg(darkLogoPath, darkLogoSvg),
        svg(faviconSvgPath, composeAdaptiveSvg(logoSvg, darkLogoSvg)),
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
        // sizes on the ICO lets browsers that support SVG icons prefer the SVG, which follows the
        // dark mode of the browser.
        { rel: 'icon', href: '/favicon.ico', sizes: '32x32' },
        { rel: 'icon', href: faviconSvgPath, type: 'image/svg+xml', sizes: 'any' },
        { rel: 'apple-touch-icon', href: `/${appleTouchIcon.fileName}` },
    ]
}
