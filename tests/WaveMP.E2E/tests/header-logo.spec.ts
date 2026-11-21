import { expect, test } from '@playwright/test'
import { homeUrl } from './helpers/navigation'

// The logo in the header leads to the start page, on the desktop and on phones.

test('opens the start page with the logo of the header', async ({ page, isMobile }) => {
    await page.goto('/library/songs')
    const logo = page.locator("[data-part='app'] > [data-part='header'] > [data-part='logo']")
    await expect(logo).toBeVisible()
    await expect(logo).toHaveAttribute('href', '/')
    await expect(page.getByRole('link', { name: 'WaveMP, go to the start page' })).toBeVisible()

    if (isMobile) {
        const box = (await logo.boundingBox())!
        expect(box.height).toBeGreaterThanOrEqual(43.5)
        await logo.tap()
    } else {
        await logo.click()
    }

    await expect(page).toHaveURL(homeUrl)
    await expect(page.locator("[data-part='shelf']").first()).toBeVisible()
})
