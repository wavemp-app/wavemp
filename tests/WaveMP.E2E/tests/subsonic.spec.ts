import { expect, test, type Page } from '@playwright/test'
import { createUser, deleteUser, uniqueSuffix, type TestUser } from './helpers/api'
import { homeUrl, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// The Subsonic access on the settings page: switch it on, create an app password, sign in with it
// at the Subsonic API like an app and revoke it again. The test changes the access of its own
// profile, so it starts without the shared admin session.
test.use({ storageState: emptyStorageState, locale: 'en-US' })

let user: TestUser

test.beforeEach(async ({ request }) => {
    user = await createUser(request, `Sia ${uniqueSuffix()} Sonic`)
})

test.afterEach(async ({ request }) => {
    await deleteUser(request, user.id)
})

async function selectProfile(page: Page): Promise<void> {
    await page.goto('/profiles')
    await profileTile(page, user.name).click()
    await expect(page).toHaveURL(homeUrl)
}

/** Status of a ping of the Subsonic API with the user name and password of an app. */
async function ping(page: Page, password: string): Promise<string> {
    const response = await page.request.get('/rest/ping', {
        params: { u: user.name, p: password, f: 'json', v: '1.16.1', c: 'e2e' },
    })
    expect(response.status()).toBe(200)
    return (await response.json())['subsonic-response'].status
}

test('creates an app password that Subsonic apps sign in with', async ({ page }) => {
    await selectProfile(page)
    await page.goto('/settings')

    const section = page.locator("[data-part='section'][data-item='subsonic']")
    await expect(section.locator("[data-part='section-title']")).toHaveText('Subsonic apps')
    const enabled = section.locator("[data-field='subsonic-enabled'] input")
    await expect(enabled).not.toBeChecked()
    await expect(
        section.locator("[data-field='subsonic-user-name'] [data-part='stat-value']"),
    ).toHaveText(user.name)
    await expect(
        section.locator("[data-field='subsonic-password-state'] [data-part='stat-value']"),
    ).toHaveText('Not created yet')

    await enabled.check()
    await expect(enabled).toBeChecked()
    await section.locator("[data-item='create-subsonic-password']").click()

    const shown = section.locator("[data-field='subsonic-new-password'] [data-part='stat-value']")
    await expect(shown).toHaveText(/^[A-Za-z0-9]{24}$/)
    const password = (await shown.textContent())!.trim()
    await expect(section.locator("[data-item='subsonic-password-once']")).toContainText(
        'only shown this once',
    )
    expect(await ping(page, password)).toBe('ok')

    // After a reload the password is gone for good.
    await page.reload()
    await expect(section.locator("[data-field='subsonic-new-password']")).toHaveCount(0)
    await expect(
        section.locator("[data-field='subsonic-password-state'] [data-part='stat-value']"),
    ).toContainText('Created on')

    // Switched off, the password no longer signs in.
    await section.locator("[data-field='subsonic-enabled'] input").uncheck()
    await expect(section.locator("[data-field='subsonic-enabled'] input")).not.toBeChecked()
    expect(await ping(page, password)).toBe('failed')

    await section.locator("[data-field='subsonic-enabled'] input").check()
    await section.locator("[data-item='revoke-subsonic-password']").click()
    await expect(section.locator("[data-part='alert']")).toContainText('was revoked')
    expect(await ping(page, password)).toBe('failed')
})
