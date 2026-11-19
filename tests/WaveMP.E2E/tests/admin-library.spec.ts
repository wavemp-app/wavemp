import { expect, test } from '@playwright/test'
import { resetLibrarySettings } from './helpers/api'
import { adminNavItem } from './helpers/navigation'

// The library page of the administration on the desktop and on the smartphone.

test('shows the navigation, the scan and the settings without horizontal scrolling', async ({
    page,
}) => {
    await page.goto('/admin/library')

    await expect(page.locator("[data-view='admin-library']")).toBeVisible()
    await expect(adminNavItem(page, 'admin-library')).toBeVisible()
    await expect(adminNavItem(page, 'admin-jobs')).toBeVisible()
    await expect(page.locator("[data-part='scan-status'] button")).toBeVisible()
    await expect(page.locator("form[data-part='form']")).toBeVisible()

    const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    )
    expect(overflow).toBeLessThanOrEqual(0)
})

test('shows the jobs page without horizontal scrolling', async ({ page }) => {
    await page.goto('/admin/jobs')

    await expect(page.locator("[data-part='job-item'][data-item='library-scan']")).toBeVisible()
    const covers = page.locator("[data-part='job-item'][data-item='cover-cache']")
    await expect(covers.locator("[data-part='job-name']")).toHaveText('Prepare covers')
    await expect(covers.locator("[data-part='job-meta']")).toContainText('Runs after scans')
    const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    )
    expect(overflow).toBeLessThanOrEqual(0)
})

test.describe('settings', () => {
    test.beforeEach(({}, testInfo) => {
        test.skip(testInfo.project.name !== 'chromium', 'Settings are changed on one project only.')
    })

    test.afterEach(async ({ request }) => {
        await resetLibrarySettings(request)
    })

    test('saves the scan interval', async ({ page }) => {
        await page.goto('/admin/library')
        const interval = page.getByLabel('Scan the library')
        await expect(interval).toHaveValue('360')

        await interval.selectOption('720')
        await page.getByRole('button', { name: 'Save' }).click()

        await expect(page.locator("[data-part='alert'][data-state='success']")).toHaveText(
            'The settings are saved.',
        )
        await page.reload()
        await expect(page.getByLabel('Scan the library')).toHaveValue('720')
        await adminNavItem(page, 'admin-jobs').click()
        await expect(
            page.locator("[data-part='job-item'][data-item='library-scan'] [data-part='job-meta']"),
        ).toContainText('Every 720 minutes')
    })
})

test.describe('media folders', () => {
    test('lists the media folder from MEDIA_PATH and hides the filter of the library', async ({
        page,
    }) => {
        await page.goto('/admin/library')

        const media = page.locator("[data-part='source-item']").first()
        await expect(media).toHaveAttribute('data-state', /\bactive\b/)
        await expect(media).toHaveAttribute('data-state', /\bmedia-path\b/)
        await expect(media.locator("[data-part='source-path']")).toHaveText('/media')
        await expect(media.locator("[data-part='source-meta']")).toContainText('From MEDIA_PATH')

        // One media folder: the library has nothing to filter by.
        await page.goto('/library/songs')
        await expect(page.locator("[data-part='list-row']").first()).toBeVisible()
        await expect(page.locator("[data-part='source-filter']")).toHaveCount(0)
    })

    test('rejects a folder that does not exist in the container', async ({ page }) => {
        await page.goto('/admin/library')
        await expect(page.locator("[data-part='source-item']").first()).toBeVisible()
        const before = await page.locator("[data-part='source-item']").count()

        await page.locator("[data-item='add-source']").click()
        const dialog = page.locator("dialog[data-part='dialog'][open]")
        await dialog.getByLabel('Name').fill('Missing folder')
        await dialog.getByLabel('Folder in the container').fill('/does-not-exist')
        await dialog.getByRole('button', { name: 'Add' }).click()

        await expect(dialog.locator("[data-part='field-error']")).toHaveText(
            'This folder does not exist in the container. Check the volumes in docker-compose.yml.',
        )
        await dialog.getByRole('button', { name: 'Cancel' }).click()
        await expect(page.locator("[data-part='source-item']")).toHaveCount(before)
    })
})
