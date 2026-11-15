import { expect, test, type Locator, type Page } from '@playwright/test'

// The queue on Now Playing with the synthetic test media of scripts/generate-media.sh: all songs
// of the library with audio, plus the first three added once more from their context menu, give
// a queue long enough to hide older played entries. The queue is built with the context menu of
// the desktop, so the tests run in the project "chromium" only and change the viewport later.
test.beforeEach(({}, testInfo) => {
    test.skip(testInfo.project.name !== 'chromium', 'Builds the queue with the desktop menu')
})

const iPadLandscape = { width: 1180, height: 820 }
const iPhone = { width: 390, height: 844 }

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

function queueItems(page: Page): Locator {
    return page.locator("[data-part='app'] [data-part='queue'] [data-part='queue-item']")
}

/** Plays all songs of the library plus three added again, paused on the sixth entry. */
async function buildQueue(page: Page): Promise<number> {
    await page.goto('/library/songs')
    // Songs with a video only cannot be queued.
    const rows = page.locator(
        "[data-part='app'] [data-part='list-row']:not([data-state~='video-only'])",
    )
    await expect(rows.first()).toBeVisible()
    const songs = await rows.count()

    // The first song may last only two seconds: pausing right away keeps it from ending.
    await rows.first().dblclick()
    await part(page, 'btn-play').click()
    await expect(part(page, 'btn-play')).toHaveAttribute('data-state', 'paused')

    for (let index = 0; index < 3; index++) {
        await rows.nth(index).click({ button: 'right' })
        await page.locator("[data-part='context-menu'] [data-item='add-to-queue']").click()
    }

    for (let step = 0; step < 5; step++) {
        await part(page, 'btn-next').click()
        await expect(part(page, 'transport')).not.toHaveAttribute('data-state', /\bloading\b/)
    }
    await expect(part(page, 'btn-play')).toHaveAttribute('data-state', 'paused')
    return songs + 3
}

async function openNowPlaying(page: Page): Promise<void> {
    await page.locator("[data-part='app'] a[href='/now-playing']").filter({ visible: true }).click()
    await expect(page).toHaveURL(/\/now-playing$/)
}

async function expectWindow(page: Page, total: number): Promise<void> {
    // Entry 6 is current: entries 3 to 5 are the last played ones, 1 and 2 are not shown.
    await expect(part(page, 'queue-list')).toHaveAttribute('start', '3')
    const items = queueItems(page)
    await expect(items).toHaveCount(total - 2)
    const states = await items.evaluateAll((elements) =>
        elements.map((element) => element.getAttribute('data-state')),
    )
    expect(states.slice(0, 4)).toEqual(['played', 'played', 'played', 'current'])
    expect(states.slice(4).every((state) => state === null)).toBe(true)
    await expect(items.nth(3)).toHaveAttribute('aria-current', 'true')
    await expect(items.locator("[data-part='queue-item-info'] > [data-part='cover']")).toHaveCount(
        total - 2,
    )
}

test('shows the last three played songs, the current one and all that follow', async ({ page }) => {
    const total = await buildQueue(page)
    await openNowPlaying(page)

    await expectWindow(page, total)
    await expect(queueItems(page).nth(3)).toBeInViewport()
})

test('keeps the current entry in view of a short queue beside the stage', async ({ page }) => {
    const total = await buildQueue(page)
    await page.setViewportSize({ width: iPadLandscape.width, height: 420 })
    await openNowPlaying(page)

    await expectWindow(page, total)
    const queue = part(page, 'queue')
    await expect(queue).toHaveCSS('overflow-y', 'auto')
    await expect(queueItems(page).nth(3)).toBeInViewport()
    expect(await queue.evaluate((element) => element.scrollTop)).toBeGreaterThan(0)
})

test('shows the queue on an iPad in landscape beside the stage', async ({ page }) => {
    const total = await buildQueue(page)
    await page.setViewportSize(iPadLandscape)
    await openNowPlaying(page)

    await expectWindow(page, total)
    await expect(queueItems(page).nth(3)).toBeInViewport()
})

test('shows the queue on an iPhone below the stage without scrolling the page', async ({
    page,
}) => {
    const total = await buildQueue(page)
    await page.setViewportSize(iPhone)
    // The content area keeps its scroll position across pages; the queue must not change it.
    const content = page.locator("[data-part='app'] > [data-part='content']")
    const scrolled = await content.evaluate((element) => element.scrollTop)
    await openNowPlaying(page)

    await expectWindow(page, total)
    await expect(part(page, 'queue')).toHaveCSS('overflow-y', 'visible')
    expect(await content.evaluate((element) => element.scrollTop)).toBe(scrolled)
    await expect(part(page, 'stage')).toBeInViewport()
})
