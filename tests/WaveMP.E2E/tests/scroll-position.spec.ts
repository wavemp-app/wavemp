import { expect, test, type Locator, type Page } from '@playwright/test'

// The pages scroll in the content area of the app shell, not in the window: a new page starts at
// its top, back and forward (and the back button of a detail page) return to the position the page
// had when it was left, with the list as it was loaded.

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

// A long list of songs with three pages, answered in the browser (page.route), so the library of
// the test media stays as it is. The songs do not exist on the server.
const longListSize = 250

function longSongId(index: number): string {
    return `00000000-0000-4000-8000-${String(index).padStart(12, '0')}`
}

function longSong(index: number) {
    const album = Math.floor(index / 10)
    return {
        id: longSongId(index),
        title: `Long Song ${index}`,
        artist: {
            id: '00000000-0000-4000-9000-000000000001',
            name: 'Long Artist',
        },
        album: {
            id: `00000000-0000-4000-a000-${String(album).padStart(12, '0')}`,
            title: `Long Album ${album}`,
        },
        trackNumber: (index % 10) + 1,
        discNumber: null,
        year: 2026,
        genre: null,
        durationMilliseconds: 120_000,
        hasAudio: true,
        hasVideo: false,
        hasCover: false,
        firstSeenAt: '2026-01-01T00:00:00Z',
        isFavorite: false,
        rating: 0,
    }
}

/** Answers the list of the songs, a song and its files; returns the offsets of the list requests. */
async function mockLongList(page: Page): Promise<number[]> {
    const offsets: number[] = []
    await page.route(
        (url) => url.pathname === '/api/v1/tracks',
        (route) => {
            const url = new URL(route.request().url())
            const offset = Number(url.searchParams.get('offset') ?? 0)
            const limit = Number(url.searchParams.get('limit') ?? 100)
            offsets.push(offset)
            const items = []
            for (let index = offset; index < Math.min(offset + limit, longListSize); index++) {
                items.push(longSong(index))
            }
            return route.fulfill({
                json: { items, offset, limit, total: longListSize },
            })
        },
    )
    await page.route(
        /\/api\/v1\/tracks\/00000000-0000-4000-8000-\d{12}(\/files)?(\?.*)?$/,
        (route) => {
            const url = new URL(route.request().url())
            if (url.pathname.endsWith('/files')) {
                return route.fulfill({
                    json: { items: [], offset: 0, limit: 100, total: 0 },
                })
            }
            const index = Number(url.pathname.slice(url.pathname.lastIndexOf('-') + 1))
            return route.fulfill({ json: longSong(index) })
        },
    )
    return offsets
}

function backButton(page: Page): Locator {
    return page.locator("[data-part='view'] > [data-part='btn-detail-back']")
}

test.describe('a long list of songs', () => {
    // Requests of the page must reach page.route, which does not see what the service worker
    // fetches.
    test.use({ serviceWorkers: 'block' })

    test('shows the same song at the same place after the page of a song, with the back button and with browser back', async ({
        page,
    }) => {
        const offsets = await mockLongList(page)
        await page.goto('/library/songs')
        const content = contentArea(page)
        const rows = page.locator("[data-part='app'] [data-part='list-row']")
        await expect(rows.first()).toBeVisible()

        // Scroll down until the second page of the list has loaded.
        const song = rows.filter({ hasText: 'Long Song 150' })
        await expect(async () => {
            await scrollToEnd(content)
            await expect(song).toHaveCount(1, { timeout: 1_000 })
        }).toPass()
        await expect(page.locator("[data-part='load-more'][data-state~='loading']")).toHaveCount(0)
        await song.evaluate((element) => element.scrollIntoView({ block: 'center' }))
        const top = await scrollTop(content)
        const place = (await song.boundingBox())!.y
        expect(top).toBeGreaterThan(0)

        async function openSong(): Promise<void> {
            await song.click({ button: 'right' })
            await page.locator("[data-part='context-menu'] [data-item='details']").click()
            await expect(page).toHaveURL(new RegExp(`/library/tracks/${longSongId(150)}$`))
            await expect(page.locator("[data-part='detail-title']")).toHaveText('Long Song 150')
        }

        async function expectSamePlace(): Promise<void> {
            await expect(page).toHaveURL(/\/library\/songs$/)
            await expect.poll(() => scrollTop(content)).toBe(top)
            await expect(song).toBeInViewport()
            expect(Math.abs((await song.boundingBox())!.y - place)).toBeLessThan(1)
            // The list was kept: its first page was loaded only once.
            expect(offsets.filter((offset) => offset === 0)).toHaveLength(1)
        }

        await openSong()
        await backButton(page).click()
        await expectSamePlace()

        await openSong()
        await page.goBack()
        await expectSamePlace()
    })

    test('leads from a song opened directly to the songs', async ({ page }) => {
        await mockLongList(page)
        await page.goto(`/library/tracks/${longSongId(7)}`)
        await expect(page.locator("[data-part='detail-title']")).toHaveText('Long Song 7')

        await backButton(page).click()

        await expect(page).toHaveURL(/\/library\/songs$/)
        await expect(page.locator("[data-part='app'] [data-part='list-row']").first()).toBeVisible()
    })
})
