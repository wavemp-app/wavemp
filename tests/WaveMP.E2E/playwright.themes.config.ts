import { defineConfig, devices } from '@playwright/test'

/**
 * Screenshots and contrast check of themes for theme designers (screenshots/theme.screenshots.ts),
 * against WaveMP or a theme preview with the synthetic test media already scanned:
 *   npm run theme:screenshots -- --theme meadow --base-url http://localhost:5201
 * See screenshots/theme-run.ts for the options. Not part of `npm test`; the shipped themes are
 * checked there by tests/themes/themes.spec.ts.
 */
export default defineConfig({
    testDir: './screenshots',
    testMatch: /theme\.screenshots\.ts$/,
    fullyParallel: true,
    reporter: 'list',
    timeout: 300_000,
    use: {
        ...devices['Desktop Chrome'],
        baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:8080',
    },
})
