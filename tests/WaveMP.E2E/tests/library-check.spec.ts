import { expect, test, type Page } from '@playwright/test'
import { adminNavItem } from './helpers/navigation'

// The library check of the administration: the scan of the setup checked the synthetic test media,
// so the page lists their problems by kind and leads to where they are fixed. Starting a check
// changes the shared result only in its time, so only the desktop project starts one.

async function horizontalOverflow(page: Page): Promise<number> {
    return page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    )
}

function stat(page: Page, item: string) {
    return page.locator(
        `[data-part='library-check-status'] [data-item='${item}'] [data-part='stat-value']`,
    )
}

function issues(page: Page) {
    return page.locator("[data-part='library-issue-item']")
}

test('lists the problems of the library by kind without horizontal scrolling', async ({ page }) => {
    await page.goto('/admin/checks')

    await expect(page.locator("[data-view='admin-checks']")).toBeVisible()
    await expect(adminNavItem(page, 'admin-checks')).toHaveAttribute('aria-current', 'page')
    await expect(stat(page, 'checked-at')).not.toHaveText('Not yet')
    await expect(stat(page, 'tracks')).not.toHaveText('0')
    await expect(issues(page).first()).toBeVisible()

    // The video of the test media has no audio file of the same name: a hint.
    const filters = page.locator("[data-part='segmented-control'][data-item='issue-type']")
    await filters.locator("[data-item='video-without-audio']").click()
    await expect(issues(page)).toHaveCount(1)
    const video = issues(page).first()
    await expect(video).toHaveAttribute('data-item', 'video-without-audio')
    await expect(video).toHaveAttribute('data-state', 'hint')
    await expect(video.locator("[data-part='library-issue-title']")).toHaveText('Video Only')
    await expect(
        video.locator("[data-part='library-issue-actions'] [data-part='link']"),
    ).toHaveText(['Show files'])
    expect(await horizontalOverflow(page)).toBeLessThanOrEqual(0)

    await filters.locator("[data-item='all']").click()
    await page.getByRole('searchbox', { name: 'Search problems' }).fill('Video Only')
    await expect(issues(page).locator("[data-part='library-issue-title']").first()).toHaveText(
        'Video Only',
    )
    expect(await horizontalOverflow(page)).toBeLessThanOrEqual(0)
})

test('opens the metadata editor of a song from its problem and returns', async ({ page }) => {
    await page.goto('/admin/checks')
    const edit = page.locator("[data-part='library-issue-actions'] [data-item='edit-metadata']")
    await expect(edit.first()).toBeVisible()

    await edit.first().click()

    await expect(page).toHaveURL(/\/library\/tracks\/[^/?]+\?edit=metadata$/)
    const dialog = page.locator("[data-part='dialog'][open]")
    await expect(dialog).toBeVisible()
    await dialog.locator("[data-part='dialog-header'] [data-part='btn-detail-back']").click()

    await expect(page).toHaveURL(/\/admin\/checks$/)
    await expect(page.locator("[data-view='admin-checks']")).toBeVisible()
})

test('checks the library again on request', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium', 'Starts a job; runs on the desktop.')

    await page.goto('/admin/checks')
    const before = await stat(page, 'checked-at').textContent()
    const button = page.locator("[data-part='library-check-status'] [data-item='run-check']")
    await expect(button).toBeEnabled()

    await button.click()

    // The time has minutes only, so wait for the run to end instead of a new time.
    await expect(button).toBeEnabled({ timeout: 30_000 })
    await expect(page.locator("[data-part='library-check-status']")).toHaveAttribute(
        'data-state',
        'idle',
    )
    await expect(page.locator("[data-part='alert']")).toHaveCount(0)
    expect(before).not.toBe('Not yet')
})
