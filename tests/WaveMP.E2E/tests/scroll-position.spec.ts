import { expect, test, type Locator, type Page } from '@playwright/test'

// The pages scroll in the content area of the app shell, not in the window: a new page starts at
// its top, back and forward return to the position the page had when it was left.

function contentArea(page: Page): Locator {
    return page.locator("[data-part='app'] > [data-part='content']")
}

function scrollTop(content: Locator): Promise<number> {
    return content.evaluate((element) => element.scrollTop)
}

async function scrollToEnd(content: Locator): Promise<number> {
    return content.evaluate((element) => {
        element.scrollTop = element.scrollHeight
        return element.scrollTop
    })
}

test('starts a new page at its top and restores the position on back and forward', async ({
    page,
}) => {
    // A low window, so that the songs of the test media make the content area scroll.
    await page.setViewportSize({
        width: page.viewportSize()!.width,
        height: 360,
    })
    await page.goto('/library/songs')
    await expect(page.locator("[data-part='app'] [data-part='list-row']").first()).toBeVisible()
    const content = contentArea(page)
    const songs = await scrollToEnd(content)
    expect(songs).toBeGreaterThan(0)

    await page.locator("[data-part='app'] a[href='/now-playing']").filter({ visible: true }).click()
    await expect(page).toHaveURL(/\/now-playing$/)
    await expect.poll(() => scrollTop(content)).toBe(0)
    await expect(page.locator("[data-part='app'] [data-part='stage']")).toBeInViewport()
    const nowPlaying = await scrollToEnd(content)

    await page.goBack()
    await expect(page).toHaveURL(/\/library\/songs$/)
    await expect.poll(() => scrollTop(content)).toBe(songs)

    await page.goForward()
    await expect(page).toHaveURL(/\/now-playing$/)
    await expect.poll(() => scrollTop(content)).toBe(nowPlaying)
})
