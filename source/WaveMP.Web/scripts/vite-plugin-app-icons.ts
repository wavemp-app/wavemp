// Vite plugin that generates the icons of the web app from the logo during the build (and serves
// them in development): favicon, SVG logo, PWA icons and Apple touch icon, see app-icons.ts. It
// also adds the icon links to the head of index.html.

import { readFileSync } from 'node:fs'
import path from 'node:path'
import type { Plugin } from 'vite'

import { createAppIcons, createHeadLinks, type AppIconFile } from './app-icons.ts'

export interface AppIconsOptions {
    /** Path of the logo (SVG) all icons are generated from. */
    logo: string
}

export function appIcons(options: AppIconsOptions): Plugin {
    let icons: Map<string, AppIconFile> | undefined

    // Rendering takes a moment, so the icons are created once, on first use.
    function getIcons(): Map<string, AppIconFile> {
        icons ??= new Map(
            createAppIcons(readFileSync(options.logo, 'utf8')).map((icon) => [
                `/${icon.fileName}`,
                icon,
            ]),
        )
        return icons
    }

    return {
        name: 'wavemp:app-icons',

        configureServer(server) {
            server.watcher.add(options.logo)
            server.watcher.on('change', (file) => {
                if (path.resolve(file) === path.resolve(options.logo)) {
                    icons = undefined
                }
            })
            server.middlewares.use((request, response, next) => {
                const pathname = new URL(request.url ?? '/', 'http://localhost').pathname
                const icon = getIcons().get(pathname)
                if (!icon) {
                    next()
                    return
                }

                response.setHeader('Content-Type', icon.contentType)
                response.setHeader('Cache-Control', 'no-cache')
                response.end(icon.data)
            })
        },

        transformIndexHtml() {
            return createHeadLinks().map((link) => ({
                tag: 'link',
                attrs: { ...link },
                injectTo: 'head' as const,
            }))
        },

        buildStart() {
            this.addWatchFile(options.logo)
        },

        generateBundle() {
            for (const icon of getIcons().values()) {
                this.emitFile({ type: 'asset', fileName: icon.fileName, source: icon.data })
            }
        },
    }
}
