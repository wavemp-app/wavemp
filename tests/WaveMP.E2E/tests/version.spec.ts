import { expect, test } from '@playwright/test'

// Images of the CI report <major>.<minor>.<run number>; the CI passes it as E2E_EXPECTED_VERSION.
// Images built without a version (locally, for pull requests) report <major>.<minor>.0-dev.
const expectedVersion = process.env.E2E_EXPECTED_VERSION || undefined

function expectImageVersion(version: string): void {
    if (expectedVersion) {
        expect(version).toBe(expectedVersion)
    } else {
        expect(version).toMatch(/^\d+\.\d+\.(\d+|0-dev)$/)
    }
}

test('health reports the version of the image', async ({ request }) => {
    const response = await request.get('/api/v1/health')

    expect(response.status()).toBe(200)
    expectImageVersion((await response.json()).version)
})

test('the settings show the version of the web app and of the server', async ({
    page,
    request,
}) => {
    const { version } = await (await request.get('/api/v1/health')).json()
    expectImageVersion(version)

    await page.goto('/settings')

    // The web app and the service are built together into one image with the same version.
    const about = page.locator("[data-part='section'][data-item='about']")
    await expect(
        about.locator("[data-field='server-version'] [data-part='stat-value']"),
    ).toHaveText(version)
    await expect(about.locator("[data-field='app-version'] [data-part='stat-value']")).toHaveText(
        version,
    )
})
