import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test'
import { adminHeaders } from './helpers/api'

// The review inbox of the admins with the synthetic test media of scripts/generate-media.sh. Sorted
// by title, the inbox starts with First Song, Loose Song and Second Song (all with an audio file),
// followed by Video Only. "Save and mark as reviewed" without changes writes no file.

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

function inboxButton(page: Page): Locator {
    return page.locator("[data-part='app'] > [data-part='header'] > [data-part='btn-review-inbox']")
}

async function trackIds(request: APIRequestContext): Promise<string[]> {
    const response = await request.get('/api/v1/tracks?limit=100', {
        headers: await adminHeaders(request),
    })
    expect(response.status()).toBe(200)
    return ((await response.json()).items as { id: string }[]).map((track) => track.id)
}

/** Puts every song back into the review inbox. */
async function resetReviews(request: APIRequestContext): Promise<void> {
    const response = await request.post('/api/v1/track-reviews/batch', {
        data: { trackIds: await trackIds(request), status: 'Unreviewed' },
        headers: await adminHeaders(request),
    })
    expect(response.status(), await response.text()).toBe(200)
}

async function unreviewedCount(request: APIRequestContext): Promise<number> {
    const response = await request.get('/api/v1/track-reviews/summary', {
        headers: await adminHeaders(request),
    })
    expect(response.status()).toBe(200)
    return (await response.json()).unreviewedCount
}

async function expectNoHorizontalScroll(page: Page): Promise<void> {
    const overflow = await page.evaluate(
        () => document.documentElement.scrollWidth - document.documentElement.clientWidth,
    )
    expect(overflow).toBeLessThanOrEqual(1)
}

test('shows the review inbox to admins in the header and the administration', async ({ page }) => {
    await page.goto('/library/songs')

    const button = inboxButton(page)
    await expect(button).toBeVisible()
    await expect(button.locator("[data-part='icon']")).toHaveAttribute('data-icon', 'review-inbox')
    await button.click()

    await expect(page).toHaveURL(/\/admin\/review$/)
    await expect(page.locator("[data-part='view'][data-view='admin-review']")).toBeVisible()
    await expect(button).toHaveAttribute('aria-current', 'page')
    await expect(
        part(page, 'admin-nav-item').and(page.locator("[data-item='admin-review']")),
    ).toHaveAttribute('aria-current', 'page')
    await expect(part(page, 'review-filters')).toBeVisible()
    await expect(page.locator("[data-item='review-filter'][role='radiogroup']")).toBeVisible()
    await expect(page.locator("[data-item='review-sort'][role='radiogroup']")).toBeVisible()
    await expectNoHorizontalScroll(page)
})

test('reviews three songs in a row and counts the badge down', async ({
    page,
    request,
}, testInfo) => {
    test.skip(
        testInfo.project.name !== 'chromium',
        'Changes the review status; runs in one project only',
    )

    await resetReviews(request)
    const total = await unreviewedCount(request)
    expect(total).toBeGreaterThanOrEqual(4)

    try {
        await page.goto('/admin/review')
        const badge = inboxButton(page).locator("[data-part='badge']")
        await expect(badge).toHaveText(String(total))

        await page.locator("[data-item='review-sort'] [data-item='title']").click()
        const items = part(page, 'review-item')
        await expect(items.first().locator("[data-part='review-title']")).toHaveText('First Song')
        // The untagged song shows which tags its file lacks.
        await expect(
            items.filter({ hasText: 'Loose Song' }).locator("[data-part='review-tags']"),
        ).toBeVisible()

        await items.first().locator("[data-part='review-title']").click()
        await expect(page).toHaveURL(/\/admin\/review\/[0-9a-f-]+$/)

        const titles = ['First Song', 'Loose Song', 'Second Song']
        for (const [index, title] of titles.entries()) {
            await expect(part(page, 'detail-title')).toHaveText(title)
            const editor = page.locator("[data-part='field'][data-field='title'] input")
            await expect(editor).toHaveValue(title)
            await expect(page.locator("[data-field='position']")).toHaveText(
                `Song ${index + 1} of ${total}`,
            )

            await page.locator("[data-item='save-and-review']").click()

            await expect(badge).toHaveText(String(total - index - 1))
        }

        // The next song opened in place of the last one: Video Only, which has no audio file to edit.
        await expect(part(page, 'detail-title')).toHaveText('Video Only')
        await expect(page.locator("[data-item='save-and-review']")).toBeDisabled()
        expect(await unreviewedCount(request)).toBe(total - 3)

        // Back returns to the list, which marks the reviewed songs.
        await page.goBack()
        await expect(page).toHaveURL(/\/admin\/review$/)
        await expect(items.and(page.locator("[data-state~='reviewed']"))).toHaveCount(3)
        await expect(items.first()).toHaveAttribute('data-state', /(^| )reviewed( |$)/)
    } finally {
        await resetReviews(request)
    }
})
