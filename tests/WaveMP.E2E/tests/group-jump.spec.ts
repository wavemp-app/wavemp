import { expect, test, type Page } from '@playwright/test'
import { withoutSavedPlayback } from './helpers/player'
import {
    appPart,
    expectOverviewReady,
    groupHeading,
    groupTile,
    keydowns,
    mockAlbumArtists,
    mockAlbumsByArtist,
    mockGroups,
    mockLongTrackList,
    openOverview,
    recordKeydowns,
} from './helpers/groupJump'

// The quick jump of grouped lists (WMP-45) on the desktop ("chromium") and on a phone ("mobile"):
// a click on a group heading, Ctrl+Minus or Escape opens the overview of all groups of the list,
// the current group is highlighted, choosing a group closes the overview and shows that group at
// the top with the focus on its heading. With the synthetic test media of scripts/generate-media.sh
// the songs have three groups (E2E Album, the video of E2E Artist without album and Loose Artist)
// and the artists two letters (E and L); the albums sorted by artist and a long list of 6000 songs
// come from page.route. Nothing is changed in the library.

test.beforeEach(async ({ page }) => {
    // An empty player: a song of another test must not open the full-screen player or the queue.
    await withoutSavedPlayback(page)
})

/** The content area of the app shell, which scrolls the pages. */
function contentArea(page: Page) {
    return page.locator("[data-part='app'] > [data-part='content']")
}

/** Leaves the focus on the page itself, like after a click into an empty area. */
async function blur(page: Page): Promise<void> {
    await page.evaluate(() => (document.activeElement as HTMLElement | null)?.blur())
}

async function openSongs(page: Page): Promise<void> {
    await page.goto('/library/songs')
    await expect(groupHeading(page, 0)).toBeVisible()
}

test.describe('songs grouped by album', () => {
    test('opens the overview from a heading and jumps to the chosen group', async ({ page }) => {
        await openSongs(page)
        const headings = page.locator(
            "[data-part='app'] [data-part='list-group-title'] > [data-part='btn-group-jump']",
        )
        await expect(headings).toHaveCount(3)
        const second = groupHeading(page, 2)
        await expect(second).toHaveAttribute('aria-haspopup', 'dialog')
        await expect(second).toHaveAttribute('aria-expanded', 'false')
        await expect(second.locator("[data-part='icon'][data-icon='group-jump']")).toHaveCount(1)

        await second.click()

        const overview = await expectOverviewReady(page)
        await expect(second).toHaveAttribute('aria-expanded', 'true')
        await expect(appPart(page, 'list')).toHaveAttribute('data-state', /\bzoomed-out\b/)
        const tiles = overview.locator("[data-part='group-tile']")
        await expect(tiles).toHaveCount(3)
        // The current group is the one of the heading: the second, the video of E2E Artist
        // without album.
        const current = tiles.nth(1)
        await expect(current).toHaveAttribute('aria-current', 'true')
        await expect(current).toHaveAttribute('data-state', 'current')
        await expect(current).toHaveAttribute('data-kind', 'unknown')
        await expect(current.locator("[data-part='label']")).toHaveText('Unknown album')
        await expect(current.locator("[data-part='group-tile-detail']")).toHaveText('E2E Artist')
        await expect(current).toBeFocused()
        await expect(overview.locator("[data-part='group-tile'][aria-current]")).toHaveCount(1)
        // The album shows its cover, the count of its songs and its artist.
        const album = tiles.nth(0)
        await expect(album).toHaveAttribute('data-kind', 'album')
        await expect(album.locator("[data-part='label']")).toHaveText('E2E Album')
        await expect(album.locator("[data-part='cover']")).toBeVisible()
        await expect(album.locator("[data-part='group-tile-count']")).toHaveText('Songs: 2')
        // Few groups: no letter bar.
        await expect(overview.locator("[data-part='letter-bar']")).toHaveCount(0)

        await tiles.nth(2).click()

        await expect(openOverview(page)).toHaveCount(0)
        await expect(appPart(page, 'list')).not.toHaveAttribute('data-state', /\bzoomed-out\b/)
        const loose = groupHeading(page, 3)
        await expect(loose).toBeFocused()
        await expect(loose).toBeInViewport()
        await expect(loose).toHaveAttribute('aria-expanded', 'false')
    })

    test('moves through the groups with the keyboard', async ({ page, isMobile }) => {
        test.skip(isMobile, 'Keyboard navigation of the overview is checked on the desktop.')
        await openSongs(page)
        await groupHeading(page, 0).click()
        const overview = await expectOverviewReady(page)
        const tiles = overview.locator("[data-part='group-tile']")
        await expect(tiles.nth(0)).toBeFocused()

        await page.keyboard.press('End')
        await expect(tiles.nth(2)).toBeFocused()
        await page.keyboard.press('Home')
        await expect(tiles.nth(0)).toBeFocused()
        await page.keyboard.press('ArrowRight')
        await expect(tiles.nth(1)).toBeFocused()
        // One tab stop for all tiles.
        await expect(overview.locator("[data-part='group-tile'][tabindex='0']")).toHaveCount(1)

        await page.keyboard.press('Enter')
        await expect(openOverview(page)).toHaveCount(0)
        await expect(groupHeading(page, 2)).toBeFocused()
    })

    test('closes with the close button and with Escape and returns the focus', async ({ page }) => {
        await openSongs(page)
        const heading = groupHeading(page, 0)

        await heading.click()
        const overview = await expectOverviewReady(page)
        await overview.locator("[data-part='btn-close']").click()
        await expect(openOverview(page)).toHaveCount(0)
        await expect(heading).toBeFocused()
        await expect(appPart(page, 'list')).not.toHaveAttribute('data-state', /\bzoomed-out\b/)

        await heading.click()
        await expectOverviewReady(page)
        await page.keyboard.press('Escape')
        await expect(openOverview(page)).toHaveCount(0)
        await expect(heading).toBeFocused()
        await expect(heading).toHaveAttribute('aria-expanded', 'false')
    })

    test('opens with Ctrl+Minus and with Escape', async ({ page }) => {
        await openSongs(page)
        await recordKeydowns(page)
        await blur(page)

        await page.keyboard.press('ControlOrMeta+Minus')
        const overview = await expectOverviewReady(page)
        // The group at the top of the content area is the current one.
        await expect(
            overview.locator("[data-part='group-tile'][aria-current='true']"),
        ).toHaveAttribute('data-kind', 'album')
        await expect
            .poll(() => keydowns(page))
            .toEqual(expect.arrayContaining([{ key: '-', prevented: true }]))

        await page.keyboard.press('Escape')
        await expect(openOverview(page)).toHaveCount(0)

        await blur(page)
        await page.keyboard.press('Escape')
        await expectOverviewReady(page)
        await page.keyboard.press('Escape')
        await expect(openOverview(page)).toHaveCount(0)
    })

    test('leaves the keys to a field that has the focus', async ({ page }) => {
        await openSongs(page)
        const search = page
            .locator("[data-part='app'] [data-part='search'] input")
            .filter({ visible: true })
        test.skip((await search.count()) === 0, 'The search field is behind a button here.')
        await search.focus()

        await page.keyboard.press('Escape')
        await page.keyboard.press('ControlOrMeta+Minus')

        await expect(openOverview(page)).toHaveCount(0)
    })

    test('zooms the list out while the overview is open', async ({ page }) => {
        await openSongs(page)
        const scale = () => contentArea(page).evaluate((element) => getComputedStyle(element).scale)

        await groupHeading(page, 0).click()
        await expectOverviewReady(page)
        await expect.poll(async () => Number(await scale())).toBeLessThan(1)
        await expect
            .poll(() => appPart(page, 'list').evaluate((list) => getComputedStyle(list).opacity))
            .not.toBe('1')

        await page.keyboard.press('Escape')
        await expect.poll(scale).toMatch(/^(none|1)$/)
    })

    test('only fades with reduced motion', async ({ page }) => {
        await page.emulateMedia({ reducedMotion: 'reduce' })
        await openSongs(page)

        await groupHeading(page, 0).click()
        await expectOverviewReady(page)
        await expect(appPart(page, 'list')).toHaveAttribute('data-state', /\bzoomed-out\b/)
        const scale = await contentArea(page).evaluate((element) => getComputedStyle(element).scale)
        expect(scale).toMatch(/^(none|1)$/)
        const overviewScale = await openOverview(page).evaluate(
            (element) => getComputedStyle(element).scale,
        )
        expect(overviewScale).toMatch(/^(none|1)$/)
    })
})

test.describe('tiles in groups', () => {
    test('groups the albums sorted by artist', async ({ page }) => {
        await mockAlbumsByArtist(page)
        await page.goto('/library/albums')
        const groups = page.locator("[data-part='app'] [data-part='tile-group']")
        await expect(groups).toHaveCount(4)
        await expect(groups.first()).toHaveAttribute('data-kind', 'artist')

        await groupHeading(page, 0).click()

        const overview = await expectOverviewReady(page)
        await expect(page.locator("[data-part='app'] [data-part='tile-groups']")).toHaveAttribute(
            'data-state',
            /\bzoomed-out\b/,
        )
        const tiles = overview.locator("[data-part='group-tile']")
        await expect(tiles).toHaveCount(4)
        await expect(tiles.first()).toHaveAttribute('aria-current', 'true')
        await expect(tiles.first()).toHaveAttribute('data-kind', 'artist')
        await expect(tiles.first().locator("[data-part='group-tile-count']")).toHaveText(
            'Albums: 3',
        )

        await groupTile(page, mockAlbumArtists[3]!).click()

        await expect(openOverview(page)).toHaveCount(0)
        await expect(groupHeading(page, 9)).toBeFocused()
        await expect(groupHeading(page, 9)).toBeInViewport()
    })

    test('groups the artists by their initial letter', async ({ page }) => {
        await page.goto('/library/artists')
        const groups = page.locator("[data-part='app'] [data-part='tile-group']")
        await expect(groups).toHaveCount(2)
        await expect(groups.first()).toHaveAttribute('data-kind', 'letter')
        const letterL = groupHeading(page, 1)
        await expect(letterL.locator("[data-part='label']")).toHaveText('L')

        await letterL.click()

        const overview = await expectOverviewReady(page)
        const tiles = overview.locator("[data-part='group-tile']")
        await expect(tiles).toHaveCount(2)
        await expect(tiles.nth(1)).toHaveAttribute('aria-current', 'true')
        await expect(tiles.nth(1)).toHaveAttribute('data-kind', 'letter')
        await expect(tiles.nth(1).locator("[data-part='label']")).toHaveText('L')

        await groupTile(page, 'E').click()

        await expect(openOverview(page)).toHaveCount(0)
        await expect(groupHeading(page, 0)).toBeFocused()
    })
})

test.describe('pages without a grouped list', () => {
    for (const path of ['/library/genres', '/settings']) {
        test(`leaves Ctrl+Minus and Escape to the browser on ${path}`, async ({ page }) => {
            await page.goto(path)
            await expect(appPart(page, 'view').first()).toBeVisible()
            await recordKeydowns(page)
            await blur(page)

            await page.keyboard.press('ControlOrMeta+Minus')
            await page.keyboard.press('Escape')

            await expect
                .poll(() => keydowns(page))
                .toEqual([
                    { key: '-', prevented: false },
                    { key: 'Escape', prevented: false },
                ])
            await expect(openOverview(page)).toHaveCount(0)
        })
    }

    test('lets Escape close the drawer and the full-screen player first', async ({
        page,
        isMobile,
    }) => {
        test.skip(!isMobile, 'The drawer and the full-screen player exist on phones only.')
        await openSongs(page)

        const sidebar = appPart(page, 'sidebar')
        await appPart(page, 'btn-menu').click()
        await expect(sidebar).toHaveAttribute('data-state', /\bopen\b/)
        await page.keyboard.press('Escape')
        await expect(sidebar).toHaveAttribute('data-state', /\bclosed\b/)
        await expect(openOverview(page)).toHaveCount(0)

        const transport = appPart(page, 'transport')
        await appPart(page, 'now-playing-mini').click()
        await expect(transport).toHaveAttribute('data-state', /\bexpanded\b/)
        await page.keyboard.press('Escape')
        await expect(transport).not.toHaveAttribute('data-state', /\bexpanded\b/)
        await expect(openOverview(page)).toHaveCount(0)

        // With nothing else open, Escape opens the overview again.
        await blur(page)
        await page.keyboard.press('Escape')
        await expectOverviewReady(page)
    })

    test('lets Escape close the mini player in the window first', async ({ page, isMobile }) => {
        test.skip(isMobile, 'The compact mode exists on the desktop only.')
        // Like Firefox and Safari: the mini player takes the place of the app.
        await page.addInitScript(() => {
            delete (window as { documentPictureInPicture?: unknown }).documentPictureInPicture
        })
        await openSongs(page)

        await appPart(page, 'btn-compact').click()
        const compact = page.locator("[data-part='compact-player']")
        await expect(compact).toBeVisible()
        await page.keyboard.press('Escape')
        await expect(compact).toHaveCount(0)
        await expect(openOverview(page)).toHaveCount(0)
    })
})

test.describe('a long list', () => {
    test('jumps to a late group without loading the list up to it', async ({ page }) => {
        const requests = await mockLongTrackList(page)
        await page.goto('/library/songs')
        await expect(groupHeading(page, 0)).toBeVisible()
        expect(requests.offsets).toEqual([0])

        await groupHeading(page, 0).click()
        const overview = await expectOverviewReady(page)
        await expect(overview.locator("[data-part='group-tile']")).toHaveCount(mockGroups.length)
        expect(requests.groupOffsets).toEqual([0])

        // Letter bar: A to Z without Q, and # for the albums of artists with a digit.
        const bar = overview.locator("[data-part='letter-bar']")
        await expect(bar).toBeVisible()
        const letters = bar.locator("[data-part='letter']")
        await expect(letters).toHaveCount(27)
        await expect(letters.first()).toHaveAttribute('data-state', 'current')
        await expect(bar.locator("[data-item='Q']")).toHaveAttribute('aria-disabled', 'true')
        await expect(bar.locator("[data-item='#']")).toHaveAccessibleName('Numbers and symbols')
        await bar.locator("[data-item='Z']").click()
        const firstZ = mockGroups.find((group) => group.letter === 'Z')!
        await expect(groupTile(page, firstZ.albumTitle)).toBeFocused()
        await expect(openOverview(page)).toHaveCount(1)

        const target = mockGroups[850]!
        await groupTile(page, target.albumTitle).click()

        await expect(openOverview(page)).toHaveCount(0)
        const heading = groupHeading(page, target.position)
        await expect(heading).toBeFocused()
        await expect(heading).toBeInViewport()
        await expect(heading.locator("[data-part='label']")).toHaveText(target.albumTitle)
        expect(requests.offsets).toContain(target.position)
        // Only the page at the position, no page between the first and the jump.
        expect(
            requests.offsets.filter((offset) => offset > 0 && offset < target.position - 100),
        ).toEqual([])

        // The page before the shown part loads on demand and keeps the group in place.
        const loadPrevious = appPart(page, 'load-previous')
        await expect(loadPrevious).toHaveCount(1)
        await contentArea(page).evaluate((element) => {
            element.scrollTop = 0
        })
        await expect.poll(() => requests.offsets).toContain(target.position - 100)
        const earlier = mockGroups.find(
            (group) => group.position > target.position - 100 && group.position < target.position,
        )!
        await expect(groupHeading(page, earlier.position)).toHaveCount(1)
        await expect(heading).toHaveCount(1)
    })
})
