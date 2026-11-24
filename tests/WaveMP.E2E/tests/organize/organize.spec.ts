import { expect, test, type APIRequestContext, type Locator, type Page } from '@playwright/test'
import { adminToken, createUser, deleteUser, findTrackId, type TestUser } from '../helpers/api'

// Own albums and the organization of the media files with the synthetic test media of
// scripts/generate-media.sh. The test moves real files of the shared media: First Song, Second
// Song (FLAC and MP4) and Loose Song (MP3, WebM and LRC) into the own album "E2E Organize Album",
// checks the library, undoes the move and checks that every file is back at its original path
// with its tags. It runs in the project "organize" after all other projects and before the theme
// regression, with one worker, so no other test sees the files while they are moved; afterAll
// undoes every run of this spec that was not undone (an interrupted or retried run) and removes
// its own album and profile. The media folder and its folders must be writable for the container
// (generate-media.sh). The read-only state is covered by the unit, service and Vitest tests.

test.describe.configure({ mode: 'serial' })

const albumTitle = 'E2E Organize Album'
const albumArtist = 'E2E Organize Artist'
const songs = ['First Song', 'Second Song', 'Loose Song'] as const
const newFolder = `${albumArtist}/${albumTitle}`

/** The files of the songs as generate-media.sh creates them. */
const generatedFiles: Record<(typeof songs)[number], string[]> = {
    'First Song': ['E2E Artist/E2E Album/01 - First Song.mp3'],
    'Second Song': [
        'E2E Artist/E2E Album/02 - Second Song.flac',
        'E2E Artist/E2E Album/02 - Second Song.mp4',
    ],
    'Loose Song': [
        'Loose Artist - Loose Song.lrc',
        'Loose Artist - Loose Song.mp3',
        'Loose Artist - Loose Song.webm',
    ],
}

/** Where the default layout template puts the files of the own album. */
const movedFiles: Record<(typeof songs)[number], string[]> = {
    'First Song': [`${newFolder}/01 First Song.mp3`],
    'Second Song': [`${newFolder}/02 Second Song.flac`, `${newFolder}/02 Second Song.mp4`],
    'Loose Song': [
        `${newFolder}/03 Loose Song.lrc`,
        `${newFolder}/03 Loose Song.mp3`,
        `${newFolder}/03 Loose Song.webm`,
    ],
}

const defaultTemplate = '{AlbumArtist}/{Album}/{TrackNumber:00} {Title}'

interface Operation {
    id: string
    kind: string
    status: string
    displayTitle?: string | null
    undoneById?: string | null
    canUndo?: boolean
    conflictItems?: number
    totalItems?: number
    doneItems?: number
}

async function admin(request: APIRequestContext): Promise<Record<string, string>> {
    return { Authorization: `Bearer ${await adminToken(request)}` }
}

async function getJson<T>(
    request: APIRequestContext,
    url: string,
    headers: Record<string, string>,
): Promise<T> {
    const response = await request.get(url, { headers })
    expect(response.status(), `GET ${url}: ${await response.text()}`).toBe(200)
    return (await response.json()) as T
}

/** The paths of all files of a song, sorted. */
async function filesOf(request: APIRequestContext, trackId: string): Promise<string[]> {
    const page = await getJson<{ items: { relativePath: string }[] }>(
        request,
        `/api/v1/tracks/${trackId}/files?limit=100`,
        await admin(request),
    )
    return page.items.map((file) => file.relativePath).sort()
}

async function operations(request: APIRequestContext): Promise<Operation[]> {
    const page = await getJson<{ items: Operation[] }>(
        request,
        '/api/v1/organize/operations?limit=100',
        await admin(request),
    )
    return page.items
}

/** Waits until an operation is no longer planned, queued or running and returns it. */
async function settled(request: APIRequestContext, id: string): Promise<Operation> {
    let operation: Operation | undefined
    await expect
        .poll(
            async () => {
                operation = await getJson<Operation>(
                    request,
                    `/api/v1/organize/operations/${id}`,
                    await admin(request),
                )
                return operation.status
            },
            { timeout: 120_000, intervals: [500] },
        )
        .not.toMatch(/^(Planning|Queued|Running)$/)
    return operation!
}

/** The newest operation of a kind for the own album of this spec. */
async function newest(request: APIRequestContext, kind: string): Promise<Operation | undefined> {
    return (await operations(request)).find(
        (operation) => operation.kind === kind && operation.displayTitle === albumTitle,
    )
}

/**
 * Puts the media back after an interrupted run: undoes every run of the own album of this spec
 * that moved files and was not undone (newest first), discards its open previews and deletes
 * the own album.
 */
async function restoreMedia(request: APIRequestContext): Promise<void> {
    const headers = await admin(request)
    for (const operation of await operations(request)) {
        if (operation.displayTitle !== albumTitle) {
            continue
        }
        if (/^(Planning|Queued|Running)$/.test(operation.status)) {
            await settled(request, operation.id)
        }
    }
    for (const operation of await operations(request)) {
        if (operation.displayTitle !== albumTitle) {
            continue
        }
        if (operation.status === 'Previewed') {
            await request.delete(`/api/v1/organize/operations/${operation.id}`, { headers })
            continue
        }
        if (operation.kind !== 'OwnAlbum' || operation.undoneById || !operation.canUndo) {
            continue
        }
        const preview = await request.post(`/api/v1/organize/undo/${operation.id}`, { headers })
        expect(preview.status(), await preview.text()).toBe(201)
        const undo = (await preview.json()) as Operation
        const applied = await request.post('/api/v1/organize/apply', {
            data: { operationId: undo.id, skipConflicts: true },
            headers,
        })
        expect(applied.status(), await applied.text()).toBe(202)
        expect((await settled(request, undo.id)).status).toBe('Completed')
    }
    const albums = await getJson<{ items: { id: string; title: string }[] }>(
        request,
        '/api/v1/own-albums?limit=100',
        headers,
    )
    for (const album of albums.items.filter((item) => item.title === albumTitle)) {
        const deleted = await request.delete(`/api/v1/own-albums/${album.id}`, { headers })
        expect([204, 404]).toContain(deleted.status())
    }
}

function app(page: Page, name: string): Locator {
    return page.locator(`[data-part='app'] [data-part='${name}']`)
}

function openDialog(page: Page, item: string): Locator {
    return page.locator(`[data-part='dialog'][data-item='${item}'][open]`)
}

let listener: TestUser | undefined
let listenerHeaders: Record<string, string> = {}
let playlistId = ''
const trackIds: Record<string, string> = {}
const originalFiles: Record<string, string[]> = {}

test.beforeAll(async ({ request }) => {
    await restoreMedia(request)
    const headers = await admin(request)
    const settings = await getJson<{ layoutTemplate: string }>(
        request,
        '/api/v1/settings/organizing',
        headers,
    )
    expect(settings.layoutTemplate).toBe(defaultTemplate)
    const availability = await getJson<{ isAvailable: boolean }>(
        request,
        '/api/v1/organize/availability',
        headers,
    )
    expect(availability.isAvailable, 'The media folder must be writable').toBe(true)

    // The snapshot of the original paths, which must be the ones of generate-media.sh.
    for (const title of songs) {
        trackIds[title] = await findTrackId(request, title)
        originalFiles[title] = await filesOf(request, trackIds[title]!)
        expect(originalFiles[title]).toEqual(generatedFiles[title])
    }

    // A listener whose favorite, rating, history and playlist must survive every move.
    listener = await createUser(request, `Organize Listener ${Date.now().toString(36)}`)
    const session = await request.post('/api/v1/sessions', { data: { profileId: listener.id } })
    expect(session.status()).toBe(201)
    listenerHeaders = { Authorization: `Bearer ${(await session.json()).token}` }
    const loose = trackIds['Loose Song']!
    for (const [method, url, data] of [
        ['put', `/api/v1/me/favorites/tracks/${loose}`, undefined],
        ['put', `/api/v1/tracks/${loose}/rating`, { rating: 4 }],
        ['post', '/api/v1/me/history', { trackId: loose }],
    ] as const) {
        const response = await request[method](url, { data, headers: listenerHeaders })
        expect(response.ok(), `${url}: ${await response.text()}`).toBe(true)
    }
    const playlist = await request.post('/api/v1/me/playlists', {
        data: { name: 'Organize Mix', trackIds: songs.map((title) => trackIds[title]) },
        headers: listenerHeaders,
    })
    expect(playlist.status(), await playlist.text()).toBe(201)
    playlistId = (await playlist.json()).id
})

test.afterAll(async ({ request }) => {
    await restoreMedia(request)
    if (listener) {
        await deleteUser(request, listener.id)
    }
})

/** The favorite, rating, history and playlist of the listener still point to the same songs. */
async function expectLibraryDataKept(request: APIRequestContext): Promise<void> {
    const loose = trackIds['Loose Song']!
    const track = await getJson<{ id: string; isFavorite: boolean; rating: number }>(
        request,
        `/api/v1/tracks/${loose}`,
        listenerHeaders,
    )
    expect(track).toMatchObject({ id: loose, isFavorite: true, rating: 4 })

    const favorites = JSON.stringify(
        await getJson(request, '/api/v1/me/favorites?limit=100', listenerHeaders),
    )
    expect(favorites).toContain(loose)

    const history = JSON.stringify(
        await getJson(request, '/api/v1/me/history?limit=100', listenerHeaders),
    )
    expect(history).toContain(loose)

    const entries = await getJson<{ items: { track?: { id: string } | null }[] }>(
        request,
        `/api/v1/me/playlists/${playlistId}/tracks?limit=100`,
        listenerHeaders,
    )
    expect(entries.items.map((entry) => entry.track?.id)).toEqual(
        songs.map((title) => trackIds[title]),
    )
}

test('shows that the media folder can be organized', async ({ page }) => {
    await page.goto('/admin/organize')
    await expect(app(page, 'organize-availability')).toHaveAttribute('data-state', 'available')
    await expect(page.locator("button[data-item='preview-library']")).toBeEnabled()
    await expect(page.locator("button[data-item='new-own-album']")).toBeEnabled()
})

test('previews a layout template live without saving it', async ({ page, request }) => {
    await page.goto('/admin/library')
    const section = app(page, 'section').and(page.locator("[data-item='file-layout']"))
    const field = section.locator("[data-part='field'][data-field='layout-template'] input")
    const preview = section.locator("[data-part='template-preview-item']")
    await expect(field).toHaveValue(defaultTemplate)
    await expect(preview.filter({ hasText: '01 First Song.mp3' })).toBeVisible()

    // The flat preset: one folder, the artist and the title in the name.
    await section
        .locator("[data-part='segmented-control'] [data-part='segment'][data-item='flat']")
        .click()
    await expect(section.locator("[data-part='template-field']")).toHaveAttribute(
        'data-state',
        'flat',
    )
    await expect(preview.filter({ hasText: 'Loose Artist - Loose Song.mp3' })).toBeVisible()

    // A placeholder inserted at the cursor makes the template custom; a wrong one shows a code.
    await field.fill('{Album')
    await expect(
        section.locator("[data-part='field'][data-field='layout-template']"),
    ).toHaveAttribute('data-state', /invalid/)
    await field.fill(defaultTemplate)
    await expect(section.locator("[data-part='template-field']")).toHaveAttribute(
        'data-state',
        'folders',
    )
    await expect(preview.filter({ hasText: '01 First Song.mp3' })).toBeVisible()

    // Nothing was saved.
    const settings = await getJson<{ layoutTemplate: string }>(
        request,
        '/api/v1/settings/organizing',
        await admin(request),
    )
    expect(settings.layoutTemplate).toBe(defaultTemplate)
})

test('creates an own album and moves its files after the preview', async ({ page, request }) => {
    test.setTimeout(180_000)
    await page.goto('/admin/organize')
    await page.locator("button[data-item='new-own-album']").click()
    await expect(page.locator("[data-view='admin-own-album']")).toHaveAttribute(
        'data-state',
        'create',
    )
    await page.locator("[data-part='field'][data-field='title'] input").fill(albumTitle)
    await page.locator("[data-part='field'][data-field='album-artist'] input").fill(albumArtist)

    // The songs come from the picker, in this order.
    await page.locator("[data-item='own-album-tracks'] button[data-item='add-songs']").click()
    const picker = openDialog(page, 'track-picker')
    await picker.locator('input').fill('Song')
    for (const title of songs) {
        await picker
            .locator("[data-part='track-picker-item']")
            .filter({ has: page.locator(`[data-field='title']`, { hasText: title }) })
            .click()
    }
    await picker.locator("button[data-item='add-songs']").click()
    await expect(picker).toHaveCount(0)
    const rows = page.locator(
        "[data-part='own-album-track'] [data-part='own-album-track-info'] [data-field='title']",
    )
    await expect(rows).toHaveText([...songs])

    // The preview saves the album first and shows every move before anything happens.
    await page.locator("button[data-item='preview-changes']").click()
    const dialog = openDialog(page, 'organize-preview')
    await expect(dialog.locator("[data-part='organize-summary']")).toHaveAttribute(
        'data-state',
        /clean/,
    )
    const items = dialog.locator("[data-part='organize-item']")
    for (const title of songs) {
        const item = items.filter({
            has: page.locator("[data-part='organize-item-title']", { hasText: title }),
        })
        await expect(item).toHaveAttribute('data-kind', 'Track')
        for (const path of movedFiles[title]) {
            const name = path
                .split('/')
                .pop()!
                .replace(/\.[^.]+$/, '')
            await expect(item.locator("[data-part='path-to']").first()).toContainText(name)
        }
    }
    // The files stay where they are until the move is confirmed.
    for (const title of songs) {
        expect(await filesOf(request, trackIds[title]!)).toEqual(originalFiles[title])
    }

    await dialog.locator("button[data-item='apply']").click()
    await expect(page).toHaveURL(/\/admin\/organize$/)
    const run = await newest(request, 'OwnAlbum')
    expect(run).toBeDefined()
    expect((await settled(request, run!.id)).status).toBe('Completed')
    await expect(
        app(page, 'organize-entry').filter({ hasText: albumTitle }).first(),
    ).toHaveAttribute('data-state', /completed/)

    // Same songs at their new paths, with the tags of the album.
    for (const [index, title] of songs.entries()) {
        const id = await findTrackId(request, title)
        expect(id).toBe(trackIds[title])
        expect(await filesOf(request, id)).toEqual([...movedFiles[title]].sort())
        const track = await getJson<{
            album?: { title: string; artist?: { name: string } } | null
            trackNumber?: number | null
        }>(request, `/api/v1/tracks/${id}`, await admin(request))
        expect(track.album?.title).toBe(albumTitle)
        expect(track.album?.artist?.name).toBe(albumArtist)
        expect(track.trackNumber).toBe(index + 1)
    }
    const lyrics = await request.get(`/api/v1/tracks/${trackIds['Loose Song']}/lyrics`, {
        headers: await admin(request),
    })
    expect(lyrics.status()).toBe(200)
    await expectLibraryDataKept(request)
})

test('undoes the move and puts every file and tag back', async ({ page, request }) => {
    test.setTimeout(180_000)
    const run = await newest(request, 'OwnAlbum')
    expect(run?.status).toBe('Completed')

    await page.goto('/admin/organize')
    const entry = app(page, 'organize-entry').filter({ hasText: albumTitle }).first()
    await entry.locator("button[data-item='undo']").click()
    const dialog = openDialog(page, 'organize-preview')
    await expect(dialog.locator("[data-part='organize-item']").first()).toBeVisible()
    await dialog.locator("button[data-item='apply']").click()
    await expect(dialog).toHaveCount(0)

    const undo = await newest(request, 'Undo')
    expect(undo).toBeDefined()
    expect((await settled(request, undo!.id)).status).toBe('Completed')
    await expect(
        app(page, 'organize-entry').filter({ hasText: albumTitle }).nth(1),
    ).toHaveAttribute('data-state', /undone/)

    // Every file is back at its original path, compared with the snapshot of the start.
    for (const title of songs) {
        expect(await filesOf(request, trackIds[title]!)).toEqual(originalFiles[title])
    }

    // The tags are back: First Song has its own, Second Song and Loose Song none.
    const headers = await admin(request)
    const first = await getJson<{
        album?: { title: string } | null
        trackNumber?: number | null
        year?: number | null
        genre?: string | null
    }>(request, `/api/v1/tracks/${trackIds['First Song']}`, headers)
    expect(first).toMatchObject({ trackNumber: 1, year: 2026, genre: 'E2E Genre' })
    expect(first.album?.title).toBe('E2E Album')
    const second = await getJson<{ album?: { title: string } | null; year?: number | null }>(
        request,
        `/api/v1/tracks/${trackIds['Second Song']}`,
        headers,
    )
    expect(second.album?.title).toBe('E2E Album')
    expect(second.year ?? null).toBeNull()
    const loose = await getJson<{ album?: unknown; genre?: string | null }>(
        request,
        `/api/v1/tracks/${trackIds['Loose Song']}`,
        headers,
    )
    expect(loose.album ?? null).toBeNull()
    expect(loose.genre ?? null).toBeNull()

    // The album keeps its cover (cover.jpg went along and came back).
    const albums = await getJson<{ items: { title: string; hasCover: boolean }[] }>(
        request,
        '/api/v1/albums?limit=100',
        headers,
    )
    expect(albums.items.filter((album) => album.title === albumTitle)).toEqual([])
    expect(albums.items.find((album) => album.title === 'E2E Album')?.hasCover).toBe(true)
    await expectLibraryDataKept(request)
})

test('deletes the own album without touching the files', async ({ page, request }) => {
    await page.goto('/admin/organize')
    await app(page, 'own-album-title').filter({ hasText: albumTitle }).click()
    await expect(page.locator("[data-view='admin-own-album']")).toHaveAttribute(
        'data-state',
        'edit',
    )
    await page.locator("button[data-item='delete']").click()
    await openDialog(page, 'delete-own-album').locator("button[data-item='confirm-delete']").click()
    await expect(page).toHaveURL(/\/admin\/organize$/)
    await expect(app(page, 'own-album-item').filter({ hasText: albumTitle })).toHaveCount(0)
    for (const title of songs) {
        expect(await filesOf(request, trackIds[title]!)).toEqual(originalFiles[title])
    }
})
