import { expect, test, type Page } from '@playwright/test'
import {
    appPart,
    expectOverviewReady,
    groupHeading,
    groupTile,
    openOverview,
} from '../helpers/groupJump'
import { settledBox } from '../helpers/motion'
import { withoutSavedPlayback } from '../helpers/player'

// The quick jump of grouped lists (WMP-45) on an iPhone, an iPad in portrait and an iPad in
// landscape (projects "iphone", "ipad" and "ipad-landscape"): a tap on a group heading or a pinch
// with two fingers on the list opens the overview of the groups, a tap on a group jumps there, the
// close button closes it. With the synthetic test media of scripts/generate-media.sh: three groups
// of songs and the artists in two letters (E and L). Nothing is changed in the library.

test.beforeEach(async ({ page }) => {
    await withoutSavedPlayback(page)
})

/**
 * Pinches the element with two fingers of the touch screen (Chrome DevTools Protocol, real touch
 * events of the browser): both start inside it, 60 % of its width apart, and move together to a
 * fifth of that distance.
 */
async function pinchIn(page: Page, selector: string): Promise<void> {
    const box = await page.locator(selector).first().boundingBox()
    expect(box).not.toBeNull()
    const viewport = page.viewportSize()!
    const y = Math.min(box!.y + Math.min(box!.height / 2, 80), viewport.height - 120)
    const middle = box!.x + box!.width / 2
    const start = box!.width * 0.3

    const cdp = await page.context().newCDPSession(page)
    const points = (half: number) => [
        { x: middle - half, y, id: 1 },
        { x: middle + half, y, id: 2 },
    ]
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: points(start) })
    for (let step = 1; step <= 8; step++) {
        await cdp.send('Input.dispatchTouchEvent', {
            type: 'touchMove',
            touchPoints: points(start * (1 - step * 0.1)),
        })
        await page.waitForTimeout(30)
    }
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] })
    await cdp.detach()
}

test('opens the overview with a tap on a heading and jumps to the chosen group', async ({
    page,
}) => {
    await page.goto('/library/songs')
    const heading = groupHeading(page, 0)
    await expect(heading).toBeVisible()

    await heading.tap()

    const overview = await expectOverviewReady(page)
    const tiles = overview.locator("[data-part='group-tile']")
    await expect(tiles).toHaveCount(3)
    await expect(tiles.first()).toHaveAttribute('aria-current', 'true')
    // The overview fits into the screen, once it has come in.
    const box = await settledBox(overview)
    const viewport = page.viewportSize()!
    expect(box.x).toBeGreaterThanOrEqual(0)
    expect(box.x + box.width).toBeLessThanOrEqual(viewport.width + 1)

    await tiles.nth(2).tap()

    await expect(openOverview(page)).toHaveCount(0)
    await expect(groupHeading(page, 3)).toBeFocused()
    await expect(groupHeading(page, 3)).toBeInViewport()
})

test('opens the overview by pinching the list', async ({ page }) => {
    await page.goto('/library/songs')
    await expect(groupHeading(page, 0)).toBeVisible()
    const scale = () => page.evaluate(() => window.visualViewport?.scale ?? 1)
    expect(await scale()).toBe(1)

    await pinchIn(page, "[data-part='app'] [data-part='list']")

    const overview = await expectOverviewReady(page)
    // The pinch opened the overview and did not zoom the page.
    expect(await scale()).toBe(1)
    await expect(appPart(page, 'list')).toHaveAttribute('data-state', /\bzoomed-out\b/)

    await overview.locator("[data-part='btn-close']").tap()

    await expect(openOverview(page)).toHaveCount(0)
    await expect(appPart(page, 'list')).not.toHaveAttribute('data-state', /\bzoomed-out\b/)
})

test('opens the overview of the artists by letter with a tap and a pinch', async ({ page }) => {
    await page.goto('/library/artists')
    const letterL = groupHeading(page, 1)
    await expect(letterL).toBeVisible()

    await letterL.tap()
    await expectOverviewReady(page)
    await groupTile(page, 'E').tap()
    await expect(openOverview(page)).toHaveCount(0)
    await expect(groupHeading(page, 0)).toBeFocused()

    await pinchIn(page, "[data-part='app'] [data-part='tile-groups']")
    const overview = await expectOverviewReady(page)
    await overview.locator("[data-part='btn-close']").tap()
    await expect(openOverview(page)).toHaveCount(0)
})
