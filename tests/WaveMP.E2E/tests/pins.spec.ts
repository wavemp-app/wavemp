import { expect, test, type Page } from '@playwright/test'
import { createUser, deleteUser, setUserPin, uniqueSuffix, type TestUser } from './helpers/api'
import { homeUrl, profileButton, profileTile } from './helpers/navigation'
import { emptyStorageState } from './helpers/storage'

// Profiles with a PIN: the lock on the tile, the digit pad and the PIN page of the settings.
// These tests select their profiles themselves, so they start without the shared admin session.
test.use({ storageState: emptyStorageState, locale: 'en-US' })

const pin = '2580'

let user: TestUser

test.beforeEach(async ({ request }) => {
    user = await createUser(request, `Pia ${uniqueSuffix()} Locked`)
    await setUserPin(request, user.id, pin)
})

test.afterEach(async ({ request }) => {
    await deleteUser(request, user.id)
})

async function enterOnPad(page: Page, digits: string): Promise<void> {
    for (const digit of digits) {
        await page.locator(`[data-part='pin-key'][data-key='${digit}']`).click()
    }
    await page.locator("[data-part='pin-key'][data-key='submit']").click()
}

test('signs in with the PIN on the digit pad', async ({ page }) => {
    await page.goto('/profiles')

    const tile = profileTile(page, user.name)
    await expect(tile).toHaveAttribute('data-state', /\bprotected\b/)
    await expect(tile.locator("[data-part='profile-lock']")).toHaveAttribute(
        'aria-label',
        'Protected with a PIN',
    )

    await tile.click()
    await expect(page.locator("[data-part='pin-entry-title']")).toHaveText(
        `Enter the PIN of ${user.name}`,
    )

    // A wrong PIN keeps the profile locked.
    await enterOnPad(page, '1111')
    await expect(page.locator("[data-part='pin-entry'] [data-part='alert']")).toHaveText(
        'Wrong PIN. Please try again.',
    )
    await expect(page).toHaveURL(/\/profiles/)

    await enterOnPad(page, pin)

    await expect(page).toHaveURL(homeUrl)
    await expect(profileButton(page).locator("[data-part='avatar-initials']")).toHaveText('PL')
})

test('takes the PIN from the keyboard', async ({ page }) => {
    await page.goto('/profiles')
    await profileTile(page, user.name).click()

    await page.keyboard.type(pin)
    await page.keyboard.press('Enter')

    await expect(page).toHaveURL(homeUrl)
})

test('removes the own PIN on the settings page', async ({ page }) => {
    await page.goto('/profiles')
    await profileTile(page, user.name).click()
    await enterOnPad(page, pin)
    await expect(page).toHaveURL(homeUrl)

    await page.goto('/settings')
    await page.locator("[data-item='open-pin']").click()
    await expect(page).toHaveURL(/\/settings\/pin$/)
    await expect(page.locator("[data-view='settings-pin']")).toHaveAttribute('data-state', 'on')

    await page.locator("[data-item='remove-pin']").click()
    await page.locator("[data-item='confirm-remove-pin']").click()
    await expect(page.locator("[data-view='settings-pin']")).toHaveAttribute('data-state', 'off')

    await page.goto('/profiles')
    await expect(profileTile(page, user.name).locator("[data-part='profile-lock']")).toHaveCount(0)
})
