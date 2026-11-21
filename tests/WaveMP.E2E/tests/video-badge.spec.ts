import { expect, test, type Page } from '@playwright/test'

// The video badge of a song sits left of its title, in a column of its own: the titles of songs
// with and without a video start at the same place, on the desktop and on phones.

interface TitleCell {
    title: string
    titleLeft: number
    badgeRight: number | null
}

async function titleCells(page: Page): Promise<TitleCell[]> {
    return page.locator("[data-part='app'] [data-part='list']").evaluate((list) =>
        [...list.querySelectorAll("[data-part='list-cell'][data-column='title']")].map((cell) => {
            const label = cell.querySelector(":scope > [data-part='label']")!
            const badge = cell.querySelector(":scope > [data-part='badge']")
            return {
                title: label.textContent ?? '',
                titleLeft: Math.round(label.getBoundingClientRect().left),
                badgeRight: badge ? Math.round(badge.getBoundingClientRect().right) : null,
            }
        }),
    )
}

test('lines up the titles and shows the video badge left of them', async ({ page }) => {
    await page.goto('/library/songs')
    const list = page.locator("[data-part='app'] [data-part='list']")
    await expect(
        list.locator("[data-part='list-row']").filter({ hasText: 'Video Only' }),
    ).toBeVisible()
    await expect(list).toHaveAttribute('data-state', /\bhas-video\b/)

    const cells = await titleCells(page)
    const withVideo = cells.filter((cell) => cell.badgeRight !== null)
    const withoutVideo = cells.filter((cell) => cell.badgeRight === null)
    expect(withVideo.map((cell) => cell.title)).toEqual(
        expect.arrayContaining(['Second Song', 'Video Only']),
    )
    expect(withoutVideo.map((cell) => cell.title)).toContain('First Song')

    const left = cells[0]!.titleLeft
    for (const cell of cells) {
        expect(cell.titleLeft, cell.title).toBe(left)
    }
    for (const cell of withVideo) {
        expect(cell.badgeRight!, cell.title).toBeLessThanOrEqual(cell.titleLeft)
    }
})
