import { expect, test, type Page } from '@playwright/test'
import { adminHeaders, findTrackId } from './helpers/api'
import { adminNavItem } from './helpers/navigation'

// The file selection per song in the administration: the overview with its filters and the page of
// a song where its files are chosen. Selecting changes the shared library, so only the desktop
// project selects a file (the file the quality rule picks anyway, so other specs see no change);
// the phone checks the layout.

async function horizontalOverflow(page: Page): Promise<number> {
    return page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    )
}

test('lists the songs and opens the files of a song without horizontal scrolling', async ({
    page,
}) => {
    await page.goto('/admin/files')

    await expect(page.locator("[data-view='admin-files']")).toBeVisible()
    await expect(adminNavItem(page, 'admin-files')).toHaveAttribute('aria-current', 'page')
    await page.getByRole('searchbox', { name: 'Search songs' }).fill('Second Song')
    const items = page.locator("[data-part='file-selection-item']")
    await expect(items).toHaveCount(1)
    await expect(items.first().locator("[data-part='file-selection-title']")).toHaveText(
        'Second Song',
    )
    expect(await horizontalOverflow(page)).toBeLessThanOrEqual(0)

    await items.first().locator("[data-part='file-selection-title']").click()

    await expect(page.locator("[data-view='admin-track-files']")).toBeVisible()
    await expect(adminNavItem(page, 'admin-files')).toHaveAttribute('aria-current', 'page')
    const audio = page.locator("[data-part='file-choices'][data-item='audio']")
    await expect(audio.locator("[data-part='file-choice']")).toHaveCount(2)
    // The desktop project may select the file at the same time, so only one option is checked.
    await expect(audio.locator("[data-part='file-choice'][aria-checked='true']")).toHaveCount(1)
    await expect(audio.locator("[data-item='file']")).toContainText('02 - Second Song.flac')
    await expect(page.locator("[data-part='file-choices'][data-item='video']")).toContainText(
        '02 - Second Song.mp4',
    )
    expect(await horizontalOverflow(page)).toBeLessThanOrEqual(0)
})

test('selects a file of a song, keeps it in the overview and resets it', async ({
    page,
    request,
}, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium', 'Changes the library; runs on the desktop.')

    const id = await findTrackId(request, 'Second Song')
    try {
        await page.goto(`/admin/files/${id}`)
        const audio = page.locator("[data-part='file-choices'][data-item='audio']")
        const flac = audio.locator("[data-item='file']")

        await flac.click()

        await expect(flac).toHaveAttribute('aria-checked', 'true')
        await expect(flac).toHaveAttribute('data-state', /\bactive\b/)
        await expect(page.locator("[data-part='alert']")).toHaveText('The selection was saved.')

        await page.goto('/admin/files')
        await page
            .locator("[data-item='file-filter'] [data-part='segment'][data-item='selected']")
            .click()
        const item = page.locator("[data-part='file-selection-item']", {
            hasText: 'Second Song',
        })
        await expect(item).toHaveAttribute('data-state', /\bselected\b/)
        await expect(item.locator("[data-part='badge'][data-state='selected audio']")).toHaveText(
            'Audio selected',
        )

        await item.locator("[data-part='file-selection-title']").click()
        await page.locator("[data-item='reset-file-selection']").click()

        await expect(audio.locator("[data-item='automatic']")).toHaveAttribute(
            'aria-checked',
            'true',
        )
        await expect(page.locator("[data-item='reset-file-selection']")).toHaveCount(0)
    } finally {
        await request.delete(`/api/v1/tracks/${id}/file-selection`, {
            headers: await adminHeaders(request),
        })
    }
})
