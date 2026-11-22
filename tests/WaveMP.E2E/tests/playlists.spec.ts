import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test'
import { adminHeaders, findTrackId, uniqueSuffix } from './helpers/api'
import { withoutSavedPlayback } from './helpers/player'

// Playlists of the built-in admin profile with the synthetic test media of
// scripts/generate-media.sh: E2E Album (with cover.jpg) holds First Song and Second Song.
// Every test uses its own playlist name, so the tests can run in parallel.

function part(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

function openDialog(page: Page): Locator {
    return page.locator("[data-part='dialog'][open]")
}

function rowTitles(page: Page): Locator {
    return part(page, 'list-row').locator("[data-column='title'] > [data-part='label']")
}

/**
 * Waits until the page of the playlist has finished its last change: a move or a removal shows at
 * once, but the page stays busy until it has loaded the playlist again, and its buttons that change
 * the playlist wait for that.
 */
async function expectPlaylistIdle(page: Page): Promise<void> {
    await expect(page.locator("[data-part='view'][data-view='playlist']")).not.toHaveAttribute(
        'aria-busy',
        'true',
    )
}

interface PlaylistSummary {
    id: string
    name: string
}

async function findPlaylist(
    request: APIRequestContext,
    name: string,
): Promise<PlaylistSummary | undefined> {
    const response = await request.get(
        `/api/v1/me/playlists?limit=100&q=${encodeURIComponent(name)}`,
        {
            headers: await adminHeaders(request),
        },
    )
    expect(response.status()).toBe(200)
    return ((await response.json()).items as PlaylistSummary[]).find(
        (playlist) => playlist.name === name,
    )
}

async function createPlaylist(
    request: APIRequestContext,
    name: string,
    trackIds: string[],
): Promise<string> {
    const response = await request.post('/api/v1/me/playlists', {
        data: { name, trackIds },
        headers: await adminHeaders(request),
    })
    expect(response.status()).toBe(201)
    return (await response.json()).id
}

async function deletePlaylist(request: APIRequestContext, name: string): Promise<void> {
    const playlist = await findPlaylist(request, name)
    if (playlist) {
        const response = await request.delete(`/api/v1/me/playlists/${playlist.id}`, {
            headers: await adminHeaders(request),
        })
        expect([204, 404]).toContain(response.status())
    }
}

interface EntrySummary {
    id: string
    track: { title: string }
}

async function listEntries(request: APIRequestContext, id: string): Promise<EntrySummary[]> {
    const response = await request.get(`/api/v1/me/playlists/${id}/tracks?limit=100`, {
        headers: await adminHeaders(request),
    })
    expect(response.status()).toBe(200)
    return (await response.json()).items
}

async function entryTitles(request: APIRequestContext, id: string): Promise<string[]> {
    return (await listEntries(request, id)).map((entry) => entry.track.title)
}

async function entryIds(request: APIRequestContext, id: string): Promise<string[]> {
    return (await listEntries(request, id)).map((entry) => entry.id)
}

/** Holds back the GET requests of the playlist itself, the reload after a change, until release. */
async function holdPlaylist(page: Page, id: string): Promise<() => void> {
    let release!: () => void
    const released = new Promise<void>((resolve) => (release = resolve))
    await page.route(
        (url) => url.pathname === `/api/v1/me/playlists/${id}`,
        async (route) => {
            if (route.request().method() !== 'GET') {
                await route.fallback()
                return
            }

            await released
            await route.continue()
        },
    )
    return release
}

/** Where the open menu is in the layout, which its opening animation does not change. */
function menuBox(menu: Locator): Promise<number[]> {
    return menu.evaluate((element: HTMLElement) => [
        element.offsetLeft,
        element.offsetTop,
        element.offsetWidth,
        element.offsetHeight,
    ])
}

interface MenuInput {
    /** Opens the menu of a row. */
    open(row: Locator): Promise<void>
    /** Chooses move up in the menu after it got enabled. */
    moveUp(page: Page, menu: Locator): Promise<void>
}

/**
 * The race of a menu opened while the page saves: the reload after a move is held back, the menu
 * of the middle row opens meanwhile and follows the page when the answer arrives.
 */
async function checkMenuOfBusyPlaylist(
    page: Page,
    request: APIRequestContext,
    input: MenuInput,
): Promise<void> {
    const name = `E2E Busy ${uniqueSuffix()}`
    const first = await findTrackId(request, 'First Song')
    const second = await findTrackId(request, 'Second Song')
    const id = await createPlaylist(request, name, [first, second, first])
    let release: (() => void) | undefined
    // The notice of a queue another test saved covers the last row of a short list on phones.
    await withoutSavedPlayback(page)
    try {
        // The songs repeat, so the entries are compared by their ids.
        const [top, middle, last] = await entryIds(request, id)
        await page.goto(`/playlists/${id}`)
        const rows = part(page, 'list-row')
        await expect(rows).toHaveCount(3)
        await expectPlaylistIdle(page)
        release = await holdPlaylist(page, id)

        const menu = page.locator("[data-part='context-menu'][data-state='open']:not([data-kind])")
        await input.open(rows.nth(2))
        await menu.locator("[data-item='move-up']").click()
        const view = page.locator("[data-part='view'][data-view='playlist']")
        await expect(view).toHaveAttribute('aria-busy', 'true')
        await expect.poll(() => entryIds(request, id)).toEqual([top, last, middle])
        await expect(menu).toHaveCount(0)

        await input.open(rows.nth(1))
        await expect(menu).toBeVisible()
        const play = menu.locator("[data-item='play']")
        await expect(play).toBeFocused()
        const changes = ['move-up', 'move-down', 'remove-from-playlist'].map((item) =>
            menu.locator(`[data-item='${item}']`),
        )
        for (const entry of changes) {
            await expect(entry).toBeDisabled()
        }
        await expect(view).toHaveAttribute('aria-busy', 'true')
        const box = await menuBox(menu)

        release()

        for (const entry of changes) {
            await expect(entry).toBeEnabled()
        }
        await expect(menu).toBeVisible()
        await expect(play).toBeFocused()
        expect(await menuBox(menu)).toEqual(box)

        await input.moveUp(page, menu)
        await expect.poll(() => entryIds(request, id)).toEqual([last, top, middle])
        await expectPlaylistIdle(page)
    } finally {
        release?.()
        await deletePlaylist(request, name)
    }
}

async function addFromSongMenu(page: Page, title: string, playlist: string): Promise<void> {
    await part(page, 'list-row').filter({ hasText: title }).first().click({ button: 'right' })
    await page.locator("[data-part='context-menu'] [data-item='add-to-playlist']").click()
    const dialog = openDialog(page)
    await expect(dialog.locator("[data-part='dialog-title']")).toHaveText('Add to playlist')
    await dialog
        .locator("[data-part='playlist-choice']")
        .filter({ has: page.locator("[data-part='label']", { hasText: playlist }) })
        .click()
    await expect(openDialog(page)).toHaveCount(0)
}

test.describe('on the desktop', () => {
    test.skip(({ isMobile }) => isMobile, 'The context menu of a row opens with a right click.')

    test('creates a playlist, adds songs from their menu, reorders, plays and deletes it', async ({
        page,
        request,
    }) => {
        const name = `E2E Playlist ${uniqueSuffix()}`
        // The tests of the built-in admin play at the same time; the player here must not take
        // over what they saved, so it shows only what this test plays.
        await withoutSavedPlayback(page)
        try {
            // Create
            await page.goto('/playlists')
            await part(page, 'view-actions').locator("[data-item='create-playlist']").click()
            const dialog = openDialog(page)
            await expect(dialog.locator("[data-part='dialog-title']")).toHaveText('New playlist')
            await dialog.getByLabel('Name', { exact: true }).fill(name)
            await dialog.locator("button[type='submit']").click()

            await expect(page).toHaveURL(/\/playlists\/[0-9a-f-]+$/)
            const id = page.url().split('/').pop()!
            await expect(part(page, 'detail-title')).toHaveText(name)
            await expect(part(page, 'empty-state')).toBeVisible()

            // Add two songs, one of them twice
            await page.goto('/library/songs')
            await addFromSongMenu(page, 'First Song', name)
            await addFromSongMenu(page, 'Second Song', name)
            await addFromSongMenu(page, 'First Song', name)
            expect(await entryTitles(request, id)).toEqual([
                'First Song',
                'Second Song',
                'First Song',
            ])

            // The sidebar lists the playlist and leads to it.
            await part(page, 'sidebar').getByRole('treeitem', { name }).click()
            await expect(page).toHaveURL(new RegExp(`/playlists/${id}$`))
            await expect(rowTitles(page)).toHaveText(['First Song', 'Second Song', 'First Song'])
            // Both songs come from the same album, so its cover fills all four tiles of the grid,
            // which has nothing to change.
            const cover = part(page, 'detail-header').locator("[data-part='playlist-cover']")
            await expect(cover).toHaveAttribute('data-count', '1')
            await expect(cover).not.toHaveAttribute('data-state', 'rotating')
            const tiles = cover.locator("> [data-part='playlist-cover-tile']")
            await expect(tiles).toHaveCount(4)
            await expect(
                tiles.locator("> [data-part='playlist-cover-slide'][data-state='active']"),
            ).toHaveCount(4)

            // Move the second entry up with its menu
            await part(page, 'list-row').nth(1).click({ button: 'right' })
            await page.locator("[data-part='context-menu'] [data-item='move-up']").click()
            await expect(rowTitles(page)).toHaveText(['Second Song', 'First Song', 'First Song'])
            await expect
                .poll(() => entryTitles(request, id))
                .toEqual(['Second Song', 'First Song', 'First Song'])

            // Remove the last entry
            await part(page, 'list-row').nth(2).click({ button: 'right' })
            await page
                .locator("[data-part='context-menu'] [data-item='remove-from-playlist']")
                .click()
            await expect(rowTitles(page)).toHaveText(['Second Song', 'First Song'])
            await expect.poll(() => entryTitles(request, id)).toEqual(['Second Song', 'First Song'])
            await expectPlaylistIdle(page)

            // Play; pausing right away keeps the short song from ending during the test.
            await part(page, 'detail-actions').locator("[data-item='play']").click()
            await part(page, 'btn-play').click()
            await expect(part(page, 'btn-play')).toHaveAttribute('data-state', 'paused')
            await expect(part(page, 'now-playing-title')).toHaveText('Second Song')

            // Delete
            await part(page, 'detail-actions').locator("[data-item='delete']").click()
            await expect(openDialog(page)).toContainText(name)
            await openDialog(page)
                .locator("[data-part='dialog-footer'] button", { hasText: 'Delete' })
                .click()
            await expect(page).toHaveURL(/\/playlists$/)
            await expect(part(page, 'tile').filter({ hasText: name })).toHaveCount(0)
            expect(await findPlaylist(request, name)).toBeUndefined()
        } finally {
            await deletePlaylist(request, name)
        }
    })

    test('enables the open menu of an entry when the change is saved', async ({
        page,
        request,
    }) => {
        await checkMenuOfBusyPlaylist(page, request, {
            open: (row) => row.click({ button: 'right' }),
            moveUp: (_, menu) => menu.locator("[data-item='move-up']").click(),
        })
    })

    test('reaches the entries that became enabled with the keyboard', async ({ page, request }) => {
        await checkMenuOfBusyPlaylist(page, request, {
            async open(row) {
                await row.focus()
                await row.press('Shift+F10')
            },
            async moveUp(page, menu) {
                await page.keyboard.press('End')
                await expect(menu.locator("[data-item='remove-from-playlist']")).toBeFocused()
                await page.keyboard.press('ArrowUp')
                await expect(menu.locator("[data-item='move-down']")).toBeFocused()
                await page.keyboard.press('ArrowUp')
                await expect(menu.locator("[data-item='move-up']")).toBeFocused()
                await page.keyboard.press('Enter')
            },
        })
    })
})

test.describe('on the phone', () => {
    test.skip(({ isMobile }) => !isMobile, 'The button of a row shows on touch screens.')

    test('enables the open menu of an entry when the change is saved', async ({
        page,
        request,
    }) => {
        await checkMenuOfBusyPlaylist(page, request, {
            open: (row) => row.locator("[data-part='btn-item-menu']").tap(),
            moveUp: (_, menu) => menu.locator("[data-item='move-up']").tap(),
        })
    })
})

test('shows a playlist among the tiles and renames it', async ({ page, request }) => {
    const name = `E2E Tiles ${uniqueSuffix()}`
    const renamed = `${name} renamed`
    const first = await findTrackId(request, 'First Song')
    await createPlaylist(request, name, [first, first])
    try {
        await page.goto('/playlists')
        await expect(part(page, 'tile').filter({ hasText: 'Recently Played' })).toBeVisible()
        const tile = part(page, 'tile').filter({ hasText: name })
        await expect(tile).toHaveAttribute('data-kind', 'playlist')
        await tile.click()

        await expect(part(page, 'detail-title')).toHaveText(name)
        await expect(rowTitles(page)).toHaveText(['First Song', 'First Song'])

        await part(page, 'detail-actions').locator("[data-item='rename']").click()
        const dialog = openDialog(page)
        await expect(dialog.locator("[data-part='dialog-title']")).toHaveText('Rename playlist')
        await dialog.getByLabel('Name', { exact: true }).fill(renamed)
        await dialog.locator("button[type='submit']").click()

        await expect(openDialog(page)).toHaveCount(0)
        await expect(part(page, 'detail-title')).toHaveText(renamed)
        expect(await findPlaylist(request, renamed)).toBeDefined()
    } finally {
        await deletePlaylist(request, name)
        await deletePlaylist(request, renamed)
    }
})
