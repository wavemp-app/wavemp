import { expect, type Locator, type Page, type Route } from '@playwright/test'

// The quick jump of grouped lists (THEMING.md: btn-group-jump, group-overview, group-tile): the
// headings that open the overview, the overview itself, and a long list of songs answered by
// page.route instead of the server, so jumps far into a list can be checked without a library of
// thousands of songs.

export function appPart(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

/** The open overview of the groups. */
export function openOverview(page: Page): Locator {
    return page.locator("[data-part='group-overview'][open]")
}

/** The heading button of the group that starts at the position (list-group or tile-group). */
export function groupHeading(page: Page, position: number): Locator {
    return page.locator(
        `[data-part='app'] :is([data-part='list-group'], [data-part='tile-group'])[data-position='${position}'] [data-part='btn-group-jump']`,
    )
}

/** The tile of the overview whose label is exactly the text. */
export function groupTile(page: Page, label: string | RegExp): Locator {
    const text = typeof label === 'string' ? new RegExp(`^${escapeRegExp(label)}$`) : label
    return openOverview(page)
        .locator("[data-part='group-tile']")
        .filter({ has: page.locator("[data-part='label']", { hasText: text }) })
}

function escapeRegExp(text: string): string {
    return text.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')
}

/** Waits until the overview is open with its groups loaded. */
export async function expectOverviewReady(page: Page): Promise<Locator> {
    const overview = openOverview(page)
    await expect(overview).toBeVisible()
    await expect(overview).toHaveAttribute('data-state', 'ready')
    await expect(overview.locator("[data-part='group-overview-title']")).toHaveText('Jump to')
    return overview
}

// A long list of songs: 6000 songs in 900 albums of 6 or 7 songs, sorted by album artist; the
// album artists start with the letters A to Z except Q (870 albums), the last 30 with a digit (#).

export const mockTrackTotal = 6000
export const mockGroupTotal = 900
const lettered = 870
const letters = 'ABCDEFGHIJKLMNOPRSTUVWXYZ'

export interface MockGroup {
    readonly index: number
    readonly position: number
    readonly count: number
    readonly letter: string
    readonly albumId: string
    readonly albumTitle: string
    readonly artistId: string
    readonly artistName: string
}

function uuid(kind: number, index: number): string {
    return `00000000-0000-4000-8${kind}00-${index.toString().padStart(12, '0')}`
}

function buildGroups(): MockGroup[] {
    const groups: MockGroup[] = []
    let position = 0
    for (let index = 0; index < mockGroupTotal; index++) {
        const count = index % 3 === 0 ? 6 : 7
        const letter =
            index < lettered ? letters[Math.floor((index * letters.length) / lettered)]! : '#'
        groups.push({
            index,
            position,
            count,
            letter,
            albumId: uuid(1, index),
            albumTitle: `Album ${index}`,
            artistId: uuid(2, index),
            artistName: `${letter === '#' ? '1' : letter} Band ${index}`,
        })
        position += count
    }

    return groups
}

export const mockGroups: readonly MockGroup[] = buildGroups()

/** The group of the song at the position. */
function groupAt(position: number): MockGroup {
    let low = 0
    let high = mockGroups.length - 1
    while (low < high) {
        const middle = Math.ceil((low + high) / 2)
        if (mockGroups[middle]!.position <= position) {
            low = middle
        } else {
            high = middle - 1
        }
    }

    return mockGroups[low]!
}

function track(position: number) {
    const group = groupAt(position)
    const artist = { id: group.artistId, name: group.artistName }
    return {
        id: uuid(3, position),
        title: `Song ${position}`,
        artist,
        album: { id: group.albumId, title: group.albumTitle, artist, isCompilation: false },
        trackNumber: position - group.position + 1,
        discNumber: null,
        year: null,
        genre: null,
        durationMilliseconds: 2000,
        hasAudio: true,
        hasVideo: false,
        hasCover: false,
        firstSeenAt: '2026-01-01T00:00:00+00:00',
        isFavorite: false,
        rating: 0,
    }
}

function page(url: URL, total: number, maxLimit: number) {
    const offset = Number(url.searchParams.get('offset') ?? 0)
    const limit = Math.min(Number(url.searchParams.get('limit') ?? maxLimit), maxLimit)
    return { offset, limit, end: Math.min(offset + limit, total) }
}

function fulfill(route: Route, body: unknown): Promise<void> {
    return route.fulfill({ status: 200, contentType: 'application/json', json: body })
}

export interface MockedTrackList {
    /** The offsets of the requested pages of songs, in the order of the requests. */
    readonly offsets: number[]
    /** The offsets of the requested pages of groups. */
    readonly groupOffsets: number[]
}

/**
 * Answers GET /api/v1/tracks and GET /api/v1/tracks/groups with the long list above (paged like
 * the server: songs up to 100, groups up to 1000 per page) and records the requested offsets.
 */
export async function mockLongTrackList(target: Page): Promise<MockedTrackList> {
    const offsets: number[] = []
    const groupOffsets: number[] = []
    await target.route(
        (url) => url.pathname === '/api/v1/tracks',
        (route) => {
            const url = new URL(route.request().url())
            const { offset, limit, end } = page(url, mockTrackTotal, 100)
            offsets.push(offset)
            const items = []
            for (let position = offset; position < end; position++) {
                items.push(track(position))
            }
            return fulfill(route, { items, offset, limit, total: mockTrackTotal })
        },
    )
    await target.route(
        (url) => url.pathname === '/api/v1/tracks/groups',
        (route) => {
            const url = new URL(route.request().url())
            const { offset, limit, end } = page(url, mockGroupTotal, 1000)
            groupOffsets.push(offset)
            const items = mockGroups.slice(offset, end).map((group) => ({
                kind: 'Album',
                position: group.position,
                count: group.count,
                album: {
                    id: group.albumId,
                    title: group.albumTitle,
                    artist: { id: group.artistId, name: group.artistName },
                    isCompilation: false,
                },
                artist: null,
                letter: group.letter,
                year: null,
                hasCover: false,
            }))
            return fulfill(route, { items, offset, limit, total: mockGroupTotal })
        },
    )
    return { offsets, groupOffsets }
}

// Albums sorted by album artist: the test media has one album only, so the album tiles come from
// page.route here as well: four album artists with three albums each.

export const mockAlbumArtists = ['Amber Choir', 'Brass Harbor', 'Cobalt Lines', 'Dune Static']

export async function mockAlbumsByArtist(target: Page): Promise<void> {
    const albums = mockAlbumArtists.flatMap((name, artistIndex) =>
        [0, 1, 2].map((albumIndex) => ({
            id: uuid(4, artistIndex * 3 + albumIndex),
            title: `${name} Record ${albumIndex + 1}`,
            artist: { id: uuid(5, artistIndex), name },
            isCompilation: false,
            year: 2020 + albumIndex,
            genre: null,
            trackCount: 1,
            durationMilliseconds: 2000,
            hasCover: false,
            firstSeenAt: '2026-01-01T00:00:00+00:00',
            isFavorite: false,
        })),
    )
    await target.route(
        (url) => url.pathname === '/api/v1/albums',
        (route) => {
            const url = new URL(route.request().url())
            const { offset, limit, end } = page(url, albums.length, 100)
            return fulfill(route, {
                items: albums.slice(offset, end),
                offset,
                limit,
                total: albums.length,
            })
        },
    )
    await target.route(
        (url) => url.pathname === '/api/v1/albums/groups',
        (route) => {
            const url = new URL(route.request().url())
            const { offset, limit, end } = page(url, mockAlbumArtists.length, 1000)
            const items = mockAlbumArtists.map((name, index) => ({
                kind: 'Artist',
                position: index * 3,
                count: 3,
                album: null,
                artist: { id: uuid(5, index), name },
                letter: name[0],
                year: null,
                hasCover: false,
            }))
            return fulfill(route, {
                items: items.slice(offset, end),
                offset,
                limit,
                total: items.length,
            })
        },
    )
}

/**
 * Records whether the next keydown events (without the modifier keys themselves) were default
 * prevented by the app: a listener on the window in the bubble phase, after the listeners of the
 * app on the document and the window.
 */
export async function recordKeydowns(page: Page): Promise<void> {
    await page.evaluate(() => {
        const seen: { key: string; prevented: boolean }[] = []
        ;(window as unknown as { keydowns: typeof seen }).keydowns = seen
        const modifiers = new Set(['Control', 'Meta', 'Shift', 'Alt'])
        window.addEventListener('keydown', (event) => {
            if (modifiers.has(event.key)) {
                return
            }

            // Read after the dispatch: listeners added after this one may still prevent it.
            setTimeout(() => seen.push({ key: event.key, prevented: event.defaultPrevented }))
        })
    })
}

/** The keydown events recorded since recordKeydowns. */
export function keydowns(page: Page): Promise<{ key: string; prevented: boolean }[]> {
    return page.evaluate(
        () => (window as unknown as { keydowns: { key: string; prevented: boolean }[] }).keydowns,
    )
}
