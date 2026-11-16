import { expect, test, type Page } from '@playwright/test'

// Error, loading and empty states of the pages. Failing requests are simulated in the browser
// (page.route), so the server and its data stay untouched.

function part(page: Page, name: string) {
    return page.locator(`[data-part='${name}']`)
}

/** Answers the matching API requests with a server error until the route is removed. */
async function failRequests(page: Page, path: RegExp): Promise<() => Promise<void>> {
    const handler = (route: Parameters<Parameters<Page['route']>[1]>[0]) =>
        route.fulfill({
            status: 500,
            contentType: 'application/problem+json',
            body: JSON.stringify({ status: 500 }),
        })
    await page.route(path, handler)
    return () => page.unroute(path, handler)
}

test('shows an error on the home page and loads it again', async ({ page }) => {
    const restore = await failRequests(page, /\/api\/v1\/me\/home(\?|$)/)
    await page.goto('/')

    const error = page.locator("[data-view='home'] [data-part='empty-state']")
    await expect(error).toContainText('The home page could not be loaded.')

    await restore()
    await error.getByRole('button', { name: 'Try again' }).click()

    await expect(error).toBeHidden()
    await expect(page.locator("[data-view='home']")).not.toHaveAttribute('aria-busy', 'true')
})

test('shows an error in the library and loads it again', async ({ page }) => {
    const restore = await failRequests(page, /\/api\/v1\/albums(\?|$)/)
    await page.goto('/library/albums')

    const error = part(page, 'empty-state')
    await expect(error).toContainText('The library could not be loaded.')

    await restore()
    await error.getByRole('button', { name: 'Try again' }).click()

    await expect(part(page, 'tile').filter({ hasText: 'E2E Album' })).toBeVisible()
})

test('tells when nothing matches the search text', async ({ page }) => {
    await page.goto('/library/albums?q=zzqxv-nothing')

    await expect(part(page, 'empty-state')).toContainText('Nothing found')
    await expect(part(page, 'tile')).toHaveCount(0)
})

test('shows a hint on Now Playing while nothing plays', async ({ page }) => {
    await page.goto('/now-playing')

    await expect(page.locator("[data-part='stage'] [data-part='empty-state']")).toContainText(
        'Nothing is playing',
    )
    await expect(part(page, 'queue')).toHaveCount(0)
})

test('shows an error in the administration and loads the page again', async ({ page }) => {
    const restore = await failRequests(page, /\/api\/v1\/jobs(\?|$)/)
    await page.goto('/admin/jobs')

    const view = page.locator("[data-view='admin-jobs']")
    await expect(view.locator("[data-part='alert'][data-state='error']")).toHaveText(
        'The jobs could not be loaded.',
    )

    await restore()
    await view.getByRole('button', { name: 'Try again' }).click()

    await expect(view.locator("[data-part='alert'][data-state='error']")).toHaveCount(0)
    await expect(view.locator("[data-part='job-item'][data-item='library-scan']")).toBeVisible()
    await expect(view).not.toHaveAttribute('aria-busy', 'true')
})

test('shows an error for settings that cannot be loaded and loads them again', async ({ page }) => {
    const restore = await failRequests(page, /\/api\/v1\/settings\/backup(\?|$)/)
    await page.goto('/admin/backups')

    const settings = page.locator("[data-view='admin-backups'] [data-item='settings']")
    await expect(settings.locator("[data-part='alert']")).toHaveText(
        'The settings could not be loaded.',
    )

    await restore()
    await settings.getByRole('button', { name: 'Try again' }).click()

    await expect(settings.locator("[data-part='alert']")).toHaveCount(0)
    await expect(page.getByLabel('Exports to keep')).toBeVisible()
})
