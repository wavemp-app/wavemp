import { expect, test } from '@playwright/test'

// Scrobbling on the settings page. No test connects a service: that would send requests to Last.fm
// or ListenBrainz. The API tests cover connecting with simulated services.
test.use({ locale: 'en-US' })

test('shows the scrobbling services with their state', async ({ page }) => {
    await page.goto('/settings')

    const section = page.locator("[data-part='section'][data-item='scrobbling']")
    await expect(section.locator("[data-part='section-title']")).toHaveText('Scrobbling')
    const items = section.locator("[data-part='scrobble-item']")
    await expect(items).toHaveCount(2)
    await expect(items.nth(0)).toHaveAttribute('data-item', 'last-fm')
    await expect(items.nth(1)).toHaveAttribute('data-item', 'listen-brainz')

    // Without LASTFM_API_KEY and LASTFM_SHARED_SECRET the server cannot offer Last.fm.
    await expect(items.nth(0)).toHaveAttribute(
        'data-state',
        /^(not-connected|unavailable|connected|authorization-failed)$/,
    )
    await expect(items.nth(1)).toHaveAttribute('data-state', 'not-connected')
    await expect(items.nth(1).locator("[data-part='badge']")).toHaveText('Not connected')
})

test('asks for the token of ListenBrainz', async ({ page }) => {
    await page.goto('/settings')

    await page
        .locator("[data-part='scrobble-item'][data-item='listen-brainz'] [data-item='connect']")
        .click()

    const dialog = page.locator("[data-part='dialog'][data-item='scrobbling-token']")
    await expect(dialog).toBeVisible()
    await expect(dialog.locator("[data-item='listenbrainz-token']")).toHaveAttribute(
        'href',
        'https://listenbrainz.org/settings/',
    )
    await expect(dialog.locator("[data-field='scrobbling-token'] input")).toHaveAttribute(
        'type',
        'password',
    )

    // An empty token is not sent.
    await dialog.locator("[data-part='dialog-footer'] button[type='submit']").click()
    await expect(dialog.locator("[data-field='scrobbling-token']")).toContainText(
        'Please enter your token.',
    )

    await dialog.locator("[data-part='dialog-footer'] button[type='button']").click()
    await expect(dialog).toBeHidden()
})

test('reports when Last.fm returns without token', async ({ page }) => {
    await page.goto('/settings/scrobbling/last-fm')

    const view = page.locator("[data-view='settings-scrobbling-lastfm']")
    await expect(view).toHaveAttribute('data-state', 'failed')
    await expect(view.locator("[data-part='alert']")).toContainText('Last.fm did not allow')

    await view.locator("[data-item='back-to-settings']").click()
    await expect(page).toHaveURL(/\/settings$/)
})
