import { defineConfig, devices } from '@playwright/test'
import { adminStorageState } from './tests/helpers/storage'

/** Specs that run on the iPhone and iPad projects only (tests/devices). */
const deviceSpecs = /[\\/]devices[\\/].*\.spec\.ts$/

/**
 * A phone or tablet of the test matrix: its viewport, touch and mobile emulation, in Chromium (the
 * only browser the E2E setup installs), with the session of the built-in admin.
 */
function device(name: string, descriptor: string) {
    return {
        name,
        testMatch: deviceSpecs,
        use: {
            ...devices[descriptor],
            browserName: 'chromium' as const,
            storageState: adminStorageState,
        },
        dependencies: ['setup'],
    }
}

/**
 * Runs against a running WaveMP instance, e.g. started with
 * `docker compose -f docker-compose.dev.yml up --build`.
 * The project "setup" selects the built-in admin profile once; the other projects start with its
 * session (see tests/helpers/storage.ts). "chromium" (desktop) and "mobile" (phone) run all specs
 * except tests/devices; "iphone", "ipad" and "ipad-landscape" run tests/devices.
 */
export default defineConfig({
    testDir: './tests',
    fullyParallel: true,
    forbidOnly: !!process.env.CI,
    retries: process.env.CI ? 2 : 0,
    reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
    use: {
        baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:8080',
        trace: 'on-first-retry',
    },
    projects: [
        { name: 'setup', testMatch: /.*\.setup\.ts/, use: { ...devices['Desktop Chrome'] } },
        {
            name: 'chromium',
            testIgnore: deviceSpecs,
            use: { ...devices['Desktop Chrome'], storageState: adminStorageState },
            dependencies: ['setup'],
        },
        {
            name: 'mobile',
            testIgnore: deviceSpecs,
            use: { ...devices['Pixel 7'], storageState: adminStorageState },
            dependencies: ['setup'],
        },
        device('iphone', 'iPhone 13'),
        device('ipad', 'iPad Pro 11'),
        device('ipad-landscape', 'iPad Pro 11 landscape'),
    ],
})
