import { defineConfig, devices } from '@playwright/test'

/**
 * Creates the screenshots of the README (assets/screenshots) and of the app catalogs
 * (assets/screenshots/catalogs) with the synthetic test media of scripts/generate-media.sh, against
 * a running WaveMP instance like the E2E tests:
 *   npm run screenshots
 *   npm run screenshots:catalogs
 * The project "setup" scans the test media first. The screenshots are not part of `npm test`.
 */
export default defineConfig({
    testDir: '.',
    fullyParallel: false,
    workers: 1,
    reporter: 'list',
    timeout: 120_000,
    use: {
        baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:8080',
    },
    projects: [
        {
            name: 'setup',
            testMatch: /tests[\\/]library\.setup\.ts$/,
            use: { ...devices['Desktop Chrome'] },
        },
        {
            name: 'screenshots',
            testMatch: /screenshots[\\/].*\.screenshots\.ts$/,
            use: {
                ...devices['Desktop Chrome'],
                locale: 'en-US',
                // Headless Chromium draws the visualizations with WebGL 2 in software.
                launchOptions: {
                    args: ['--enable-unsafe-swiftshader', '--use-angle=swiftshader'],
                },
            },
            dependencies: ['setup'],
        },
    ],
})
