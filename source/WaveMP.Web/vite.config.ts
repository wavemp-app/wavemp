import { readFileSync } from 'node:fs'
import { fileURLToPath, URL } from 'node:url'

import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'
import VueI18nPlugin from '@intlify/unplugin-vue-i18n/vite'
import { VitePWA } from 'vite-plugin-pwa'

import { createManifestIcons } from './scripts/app-icons.ts'
import { appIcons } from './scripts/vite-plugin-app-icons.ts'

// Address of WaveMP.Service during development (see Properties/launchSettings.json).
const serviceUrl = process.env.WAVEMP_SERVICE_URL ?? 'http://localhost:5080'

// A theme preview server (scripts/theme-preview.ts) shows this theme instead of the theme of the
// profile and leaves out the overlay of the Vue devtools, which would show in its screenshots.
const previewTheme = process.env.WAVEMP_PREVIEW_THEME ?? ''

/**
 * Version the web app reports, like the service: WAVEMP_VERSION from the CI (<major>.<minor>.<run
 * number>), otherwise a development build <major>.<minor>.0-dev with the VersionPrefix of
 * Directory.Build.props.
 */
function resolveAppVersion(): string {
    const version = process.env.WAVEMP_VERSION?.trim()
    if (version) {
        return version
    }

    const props = readFileSync(
        fileURLToPath(new URL('../../Directory.Build.props', import.meta.url)),
        'utf8',
    )
    const prefix = /<VersionPrefix>\s*(\d+\.\d+)\s*<\/VersionPrefix>/.exec(props)?.[1]
    if (!prefix) {
        throw new Error('Directory.Build.props has no VersionPrefix of the form <major>.<minor>.')
    }

    return `${prefix}.0-dev`
}

// https://vite.dev/config/
export default defineConfig({
    // Graphics, icons and themes from /assets/web are served unchanged at the root (e.g.
    // /themes/midnight/theme.css), in development and in the build.
    publicDir: fileURLToPath(new URL('../../assets/web', import.meta.url)),
    define: {
        __WAVEMP_VERSION__: JSON.stringify(resolveAppVersion()),
        __WAVEMP_PREVIEW_THEME__: JSON.stringify(previewTheme),
    },
    plugins: [
        vue(),
        ...(previewTheme ? [] : [vueDevTools()]),
        // Favicon, PWA icons and Apple touch icon, generated from the logo during the build (served
        // under /logo and /favicon.ico).
        appIcons({
            logo: fileURLToPath(new URL('../../assets/logo/wavemp-logo.svg', import.meta.url)),
        }),
        // Precompiles the messages in src/locales, so the app uses the runtime-only build of
        // vue-i18n (no message compiler and no eval at runtime).
        VueI18nPlugin({
            include: fileURLToPath(new URL('./src/locales/**', import.meta.url)),
            strictMessage: true,
        }),
        // Installable web app: manifest and a service worker that precaches the app shell (HTML,
        // scripts, styles, themes, icons). API responses and media are never cached.
        VitePWA({
            // New versions activate on the next load without asking and without a forced reload.
            registerType: 'autoUpdate',
            // Registration through /registerSW.js, no inline script.
            injectRegister: 'script-defer',
            manifest: {
                id: '/',
                name: 'WaveMP',
                short_name: 'WaveMP',
                description:
                    'A nostalgic media player for your self-hosted music and music videos.',
                lang: 'en',
                dir: 'ltr',
                start_url: '/',
                scope: '/',
                display: 'standalone',
                background_color: '#0b0d10',
                theme_color: '#43484f',
                icons: createManifestIcons(),
            },
            workbox: {
                globPatterns: ['**/*.{js,css,html,svg,png,ico,webp,woff2}'],
                navigateFallback: '/index.html',
                // The API, the OpenAPI document and paths that look like files (as on the server)
                // always come from the network.
                navigateFallbackDenylist: [
                    /^\/api(?:\/|$)/,
                    /^\/openapi(?:\/|$)/,
                    /\/[^/?]+\.[^/?]+$/,
                ],
                cleanupOutdatedCaches: true,
                // A new service worker takes over at once (the registration script sends no
                // message for that); the app shell of the new version is used on the next load.
                skipWaiting: true,
                clientsClaim: true,
                // No runtime caching: API responses and media are never cached.
                runtimeCaching: [],
            },
            devOptions: { enabled: false },
        }),
    ],
    build: {
        // The preset pack of the visualization (butterchurn-presets, about 650 kB) is one chunk of
        // data that only loads when the visualization is shown; everything else stays far below.
        chunkSizeWarningLimit: 700,
    },
    resolve: {
        alias: {
            '@': fileURLToPath(new URL('./src', import.meta.url)),
        },
    },
    server: {
        proxy: {
            '/api': serviceUrl,
            '/openapi': serviceUrl,
        },
    },
})
