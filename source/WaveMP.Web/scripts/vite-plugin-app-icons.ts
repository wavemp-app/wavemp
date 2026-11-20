// Vite plugin that generates the icons of the web app from the logo and its dark variant during the
// build (and serves them in development): favicon, SVG logos, PWA icons and Apple touch icon, see
// app-icons.ts. It also adds the icon links to the head of index.html.

import { readFileSync } from 'node:fs'
import path from 'node:path'
import type { Plugin } from 'vite'

import { createAppIcons, createHeadLinks, type AppIconFile } from './app-icons.ts'

export interface AppIconsOptions {
    /** Path of the logo (SVG) all icons are generated from. */
    logo: string
    /** Path of the dark variant of the logo (SVG), for the dark mode of the favicon and themes. */
    darkLogo: string
}

export function appIcons(options: AppIconsOptions): Plugin {
    let icons: Map<string, AppIconFile> | undefined
    const sources = [options.logo, options.darkLogo]

    // Rendering takes a moment, so the icons are created once, on first use.
    function getIcons(): Map<string, AppIconFile> {
        icons ??= new Map(
            createAppIcons(
                readFileSync(options.logo, 'utf8'),
                readFileSync(options.darkLogo, 'utf8'),
            ).map((icon) => [`/${icon.fileName}`, icon]),
        )
        return icons
    }

    return {
        name: 'wavemp:app-icons',

        configureServer(server) {
            server.watcher.add(sources)
            server.watcher.on('change', (file) => {
                if (sources.some((source) => path.resolve(file) === path.resolve(source))) {
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
            for (const source of sources) {
                this.addWatchFile(source)
            }
        },

        generateBundle() {
            for (const icon of getIcons().values()) {
                this.emitFile({ type: 'asset', fileName: icon.fileName, source: icon.data })
            }
        },
    }
}
