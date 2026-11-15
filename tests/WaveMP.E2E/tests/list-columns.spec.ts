import { expect, test, type Page } from '@playwright/test'
import { adminHeaders, findTrackId, uniqueSuffix } from './helpers/api'

// Column headers and cells of the song lists share the columns of the list (desktop layout, from
// 64rem on): on the desktop, on an iPad in landscape, after a change of the width and while the
// content scrolls.
test.skip(({ isMobile }) => isMobile, 'Column headers are shown on the desktop only')

interface Edges {
    left: number
    right: number
    text: number
}

interface ListColumns {
    list: Edges
    header: Edges
    columns: Edges[]
    cells: Edges[][]
}

/** Edges of the visible column headers and of the cells of the first rows of the list. */
async function listColumns(page: Page): Promise<ListColumns> {
    return page.locator("[data-part='app'] [data-part='list']").evaluate((list) => {
        const edges = (element: Element): Edges => {
            const box = element.getBoundingClientRect()
            const style = getComputedStyle(element)
            return {
                left: Math.round(box.left * 10) / 10,
                right: Math.round(box.right * 10) / 10,
                text:
                    Math.round(
                        (box.left +
                            parseFloat(style.paddingLeft) +
                            parseFloat(style.borderLeftWidth)) *
                            10,
                    ) / 10,
            }
        }
        const visible = (element: Element) => getComputedStyle(element).display !== 'none'
        const header = list.querySelector("[data-part='list-header']")!
        return {
            list: edges(list),
            header: edges(header),
            columns: [...header.querySelectorAll("[data-part='list-column']")]
                .filter(visible)
                .map(edges),
            cells: [...list.querySelectorAll("[data-part='list-row']")]
                .slice(0, 3)
                .map((row) =>
                    [...row.querySelectorAll(":scope > [data-part='list-cell']")]
                        .filter(visible)
                        .map(edges),
                ),
        }
    })
}

async function expectAligned(page: Page): Promise<void> {
    const result = await listColumns(page)
    expect(result.cells.length).toBeGreaterThan(0)
    expect(result.header.left).toBe(result.list.left)
    expect(result.header.right).toBe(result.list.right)
    for (const cells of result.cells) {
        expect(cells).toEqual(result.columns)
    }

    const last = result.columns.at(-1)!
    expect(last.right).toBeLessThanOrEqual(result.list.right)
    const overflow = await page
        .locator("[data-part='app'] [data-part='content']")
        .evaluate((content) => content.scrollWidth - content.clientWidth)
    expect(overflow).toBe(0)
}

for (const viewport of [
    { width: 1440, height: 900 },
    { width: 1180, height: 820 },
]) {
    test(`lines the column headers up with the cells at ${viewport.width}px`, async ({ page }) => {
        await page.setViewportSize(viewport)
        await page.goto('/library/songs')
        await expect(page.locator("[data-part='app'] [data-part='list-row']").first()).toBeVisible()
        await expectAligned(page)

        await page.setViewportSize({ width: viewport.width - 113, height: viewport.height })
        await expectAligned(page)

        await page
            .locator("[data-part='app'] > [data-part='content']")
            .evaluate((content) => content.scrollTo(0, 80))
        await expectAligned(page)
    })
}

test('lines the column headers up with the cells of a playlist', async ({ page, request }) => {
    const name = `Columns ${uniqueSuffix()}`
    const trackIds = [
        await findTrackId(request, 'First Song'),
        await findTrackId(request, 'Second Song'),
    ]
    const created = await request.post('/api/v1/me/playlists', {
        data: { name, trackIds },
        headers: await adminHeaders(request),
    })
    expect(created.status()).toBe(201)
    const { id } = await created.json()

    try {
        await page.goto(`/playlists/${id}`)
        await expect(page.locator("[data-part='app'] [data-part='list-row']")).toHaveCount(2)
        await expectAligned(page)
        expect((await listColumns(page)).columns).toHaveLength(8)
    } finally {
        await request.delete(`/api/v1/me/playlists/${id}`, { headers: await adminHeaders(request) })
    }
})
