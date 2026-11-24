import type { Page, Route } from '@playwright/test'

// A fixed organization of the media files for the screenshots of the organize pages (theme
// regression): own albums, a log and the preview of an own album, answered with page.route instead
// of the server. Own albums and the log are shared by all admins, so the real ones would differ
// between runs and between the themes checked at the same time; the fixture keeps every view the
// same and moves no file. The names are invented.

const sourceId = '019a0000-0000-7000-8000-0000000000f1'

/** Id of the own album whose editor and preview the views show. */
export const mockOwnAlbumId = '019a0000-0000-7000-8000-0000000a0001'

const previewId = '019a0000-0000-7000-8000-0000000b0001'

function ownAlbum(
    id: string,
    title: string,
    albumArtist: string,
    trackCount: number,
    status: 'Draft' | 'Applied' | 'Changed',
    year: number | null,
    genre: string | null,
) {
    const applied = status !== 'Draft'
    return {
        id,
        title,
        albumArtist,
        year,
        genre,
        hasCover: false,
        coverUpdatedAt: null,
        trackCount,
        removedTrackCount: 0,
        status,
        appliedAt: applied ? '2026-11-20T18:42:00+00:00' : null,
        libraryAlbumId: null,
        createdAt: '2026-11-18T09:15:00+00:00',
        updatedAt: '2026-11-21T08:30:00+00:00',
    }
}

const ownAlbums = [
    ownAlbum(mockOwnAlbumId, 'Summer Roadtrip', 'Various Artists', 4, 'Draft', 2026, 'Pop'),
    ownAlbum(
        '019a0000-0000-7000-8000-0000000a0002',
        'Kitchen Radio',
        'Various Artists',
        5,
        'Changed',
        null,
        null,
    ),
    ownAlbum(
        '019a0000-0000-7000-8000-0000000a0003',
        'Harbor Lights Live',
        'Harbor Lights',
        9,
        'Applied',
        2025,
        'Rock',
    ),
]

function operation(
    id: string,
    kind: 'OwnAlbum' | 'Library' | 'Undo',
    status: string,
    displayTitle: string | null,
    counts: { total: number; done: number; skipped?: number; failed?: number; conflicts?: number },
    createdAt: string,
    extra: Record<string, unknown> = {},
) {
    return {
        id,
        kind,
        status,
        ownAlbumId: null,
        sourceId: null,
        undoOfId: null,
        undoneById: null,
        template: '{AlbumArtist}/{Album}/{TrackNumber:00} {Title}',
        displayTitle,
        errorCode: null,
        skipConflicts: false,
        totalItems: counts.total,
        doneItems: counts.done,
        skippedItems: counts.skipped ?? 0,
        failedItems: counts.failed ?? 0,
        conflictItems: counts.conflicts ?? 0,
        warningItems: 0,
        canUndo: status === 'Completed' || status === 'Stopped',
        createdAt,
        previewedAt: createdAt,
        expiresAt: null,
        startedAt: createdAt,
        finishedAt: createdAt,
        ...extra,
    }
}

const operations = [
    operation(
        '019a0000-0000-7000-8000-0000000b0011',
        'Undo',
        'Completed',
        'Kitchen Radio',
        { total: 5, done: 5 },
        '2026-11-22T19:05:00+00:00',
        { canUndo: false },
    ),
    operation(
        '019a0000-0000-7000-8000-0000000b0012',
        'Library',
        'Stopped',
        null,
        { total: 240, done: 118, failed: 1 },
        '2026-11-21T07:48:00+00:00',
        { errorCode: 'item_failed' },
    ),
    operation(
        '019a0000-0000-7000-8000-0000000b0013',
        'OwnAlbum',
        'Completed',
        'Kitchen Radio',
        { total: 5, done: 5 },
        '2026-11-20T18:42:00+00:00',
        { undoneById: '019a0000-0000-7000-8000-0000000b0011', canUndo: false },
    ),
    operation(
        '019a0000-0000-7000-8000-0000000b0014',
        'OwnAlbum',
        'Completed',
        'Harbor Lights Live',
        { total: 9, done: 8, skipped: 1, conflicts: 1 },
        '2026-11-19T21:10:00+00:00',
    ),
]

function track(id: string, title: string, artist: string, album: string | null, video: boolean) {
    return {
        id,
        title,
        artist: { id: `${id.slice(0, -4)}a${id.slice(-3)}`, name: artist },
        album: album
            ? {
                  id: `${id.slice(0, -4)}b${id.slice(-3)}`,
                  title: album,
                  artist: { id: `${id.slice(0, -4)}a${id.slice(-3)}`, name: artist },
                  isCompilation: false,
              }
            : null,
        trackNumber: null,
        discNumber: null,
        year: null,
        genre: null,
        durationMilliseconds: 214000,
        hasAudio: true,
        hasVideo: video,
        hasCover: false,
        firstSeenAt: '2026-11-01T12:00:00+00:00',
        isFavorite: false,
        rating: 0,
    }
}

const songs = [
    track(
        '019a0000-0000-7000-8000-0000000c0001',
        'Open Highway',
        'Harbor Lights',
        'Coastline',
        false,
    ),
    track(
        '019a0000-0000-7000-8000-0000000c0002',
        'Paper Planes Tonight',
        'The Velvet Hours',
        'Singles',
        true,
    ),
    track('019a0000-0000-7000-8000-0000000c0003', 'Salt and Sunlight', 'Mira Lund', 'Tidal', false),
    track('019a0000-0000-7000-8000-0000000c0004', 'Last Exit West', 'Harbor Lights', null, true),
]

const ownAlbumTracks = songs.map((song, position) => ({
    position,
    state: 'Member',
    addedAt: '2026-11-18T09:16:00+00:00',
    track: song,
}))

const preview = {
    ...operation(
        previewId,
        'OwnAlbum',
        'Previewed',
        'Summer Roadtrip',
        { total: 5, done: 0, conflicts: 1 },
        '2026-11-24T10:00:00+00:00',
        {
            ownAlbumId: mockOwnAlbumId,
            warningItems: 1,
            canUndo: false,
            startedAt: null,
            finishedAt: null,
            expiresAt: '2026-11-24T11:00:00+00:00',
        },
    ),
}

const tagFields = ['Album', 'AlbumArtist', 'TrackNumber', 'TrackCount', 'Year', 'Genre']

function tags(trackNumber: number) {
    return {
        fields: tagFields,
        album: 'Summer Roadtrip',
        albumArtists: ['Various Artists'],
        trackNumber,
        trackCount: 4,
        discNumber: null,
        year: 2026,
        genres: ['Pop'],
    }
}

const tagsBefore = {
    fields: tagFields,
    album: null,
    albumArtists: [],
    trackNumber: null,
    trackCount: null,
    discNumber: null,
    year: null,
    genres: [],
}

function item(
    sequence: number,
    song: (typeof songs)[number] | null,
    from: { directory: string; base: string } | null,
    to: { directory: string; base: string },
    extensions: Array<[string, string]>,
    extra: Record<string, unknown> = {},
) {
    return {
        id: `019a0000-0000-7000-8000-0000000d000${sequence}`,
        sequence,
        kind: song ? 'Track' : 'AlbumCover',
        status: 'Pending',
        trackId: song?.id ?? null,
        sourceId,
        fromDirectoryPath: from?.directory ?? null,
        fromBaseName: from?.base ?? null,
        toDirectoryPath: to.directory,
        toBaseName: to.base,
        conflict: null,
        warnings: [],
        displayTitle: song?.title ?? 'cover.jpg',
        displayArtist: song?.artist.name ?? null,
        tagChanges: song ? tags(sequence + 1) : null,
        tagsBefore: song ? tagsBefore : null,
        moves: extensions.map(([kind, extension], index) => ({
            id: `019a0000-0000-7000-8000-0000000e00${sequence}${index}`,
            fileKind: kind,
            fromPath: from
                ? `${from.directory ? `${from.directory}/` : ''}${from.base}.${extension}`
                : null,
            toPath: `${to.directory}/${to.base}.${extension}`,
            status: 'Pending',
        })),
        ...extra,
    }
}

const target = 'Various Artists/Summer Roadtrip'

const items = [
    item(
        0,
        songs[0]!,
        { directory: 'Harbor Lights/Coastline', base: '03 - Open Highway' },
        { directory: target, base: '01 Open Highway' },
        [
            ['Audio', 'mp3'],
            ['Lyrics', 'lrc'],
        ],
    ),
    item(
        1,
        songs[1]!,
        { directory: 'The Velvet Hours/Singles', base: 'Paper Planes Tonight' },
        { directory: target, base: '02 Paper Planes Tonight' },
        [
            ['Audio', 'flac'],
            ['Video', 'mp4'],
        ],
        { warnings: ['metadata_changes'] },
    ),
    item(
        2,
        songs[2]!,
        { directory: 'Mira Lund/Tidal', base: '07 - Salt and Sunlight' },
        { directory: target, base: '03 Salt and Sunlight' },
        [['Audio', 'm4a']],
        { status: 'Conflict', conflict: 'target_exists' },
    ),
    item(
        3,
        songs[3]!,
        { directory: '', base: 'Harbor Lights - Last Exit West' },
        { directory: target, base: '04 Last Exit West' },
        [
            ['Audio', 'mp3'],
            ['Video', 'webm'],
            ['Image', 'jpg'],
        ],
    ),
    item(4, null, null, { directory: target, base: 'cover' }, [['AlbumCover', 'jpg']]),
]

const templatePreview = {
    isValid: true,
    isFolderBased: true,
    errors: [],
    paths: [
        ['Harbor Lights', 'Coastline', '01 Open Highway', 'mp3'],
        ['Harbor Lights', 'Coastline', '02 Northern Pier', 'flac'],
        ['The Velvet Hours', 'Singles', 'Paper Planes Tonight', 'mp3'],
    ].map(([artist, album, baseName, extension], index) => ({
        trackId: songs[index]!.id,
        folders: [artist, album],
        baseName,
        extension,
        isTooLong: false,
    })),
}

function json(route: Route, body: unknown, status = 200): Promise<void> {
    return route.fulfill({ status, contentType: 'application/json', json: body })
}

function paged<T>(url: URL, list: readonly T[]) {
    const offset = Number(url.searchParams.get('offset') ?? 0)
    const limit = Number(url.searchParams.get('limit') ?? 50)
    return { items: list.slice(offset, offset + limit), offset, limit, total: list.length }
}

/**
 * Answers the requests of the organize pages from the fixture: the availability (the media folder
 * can be written), the layout template and its live preview, the own albums with the songs of
 * "Summer Roadtrip", the log and the preview of "Summer Roadtrip" with a warning and a conflict.
 * Changes (saving, applying, discarding) are answered without effect.
 */
export async function mockOrganize(page: Page): Promise<void> {
    await page.route(
        (url) => url.pathname === '/api/v1/settings/organizing',
        (route) =>
            json(route, {
                layoutTemplate: '{AlbumArtist}/{Album}/{TrackNumber:00} {Title}',
                isFolderBased: true,
            }),
    )
    await page.route(
        (url) => url.pathname.startsWith('/api/v1/organize/'),
        (route) => {
            const request = route.request()
            const url = new URL(request.url())
            const path = url.pathname.replace('/api/v1/organize/', '')
            if (path === 'availability') {
                return json(route, {
                    sources: [{ sourceId, name: 'Media', status: 'Available' }],
                    isAvailable: true,
                    activeOperationId: null,
                })
            }
            if (path === 'template-preview') {
                return json(route, templatePreview)
            }
            if (path === 'preview') {
                return json(route, preview, 201)
            }
            if (path === 'operations') {
                return json(route, paged(url, operations))
            }
            if (path === `operations/${previewId}`) {
                return request.method() === 'DELETE'
                    ? route.fulfill({ status: 204 })
                    : json(route, preview)
            }
            if (path === `operations/${previewId}/items`) {
                const filter = url.searchParams.get('filter')
                const listed = items.filter((entry) =>
                    filter === 'Conflicts'
                        ? entry.conflict
                        : filter === 'Warnings'
                          ? entry.warnings.length > 0
                          : true,
                )
                return json(route, paged(url, listed))
            }
            return route.fulfill({ status: 404 })
        },
    )
    await page.route(
        (url) =>
            url.pathname === '/api/v1/own-albums' || url.pathname.startsWith('/api/v1/own-albums/'),
        (route) => {
            const url = new URL(route.request().url())
            if (url.pathname === '/api/v1/own-albums') {
                return json(route, paged(url, ownAlbums))
            }
            if (url.pathname === `/api/v1/own-albums/${mockOwnAlbumId}`) {
                return json(route, ownAlbums[0])
            }
            if (url.pathname === `/api/v1/own-albums/${mockOwnAlbumId}/tracks`) {
                return json(route, paged(url, ownAlbumTracks))
            }
            return route.fulfill({ status: 404 })
        },
    )
}

/** A finished scan of the fixture, the newest first. */
function scan(id: string, trigger: string, finishedAt: string, partial: boolean) {
    return {
        id,
        trigger,
        status: 'Succeeded',
        queuedAt: finishedAt,
        startedAt: finishedAt,
        finishedAt,
        durationMilliseconds: 640,
        errorCode: null,
        isPartial: partial,
        progress: null,
        statistics: {
            directoriesScanned: 3,
            directoriesChanged: 0,
            filesFound: 8,
            filesAdded: 0,
            filesUpdated: 0,
            filesRemoved: 0,
            filesUnchanged: 8,
            filesFailed: 0,
            tracksAdded: 0,
            tracksUpdated: 0,
            tracksRemoved: 0,
            tracksTotal: 4,
            albumsTotal: 1,
            artistsTotal: 2,
        },
        issues: [],
    }
}

const scans = [
    scan('019a0000-0000-7000-8000-0000000c0003', 'FileWatcher', '2026-11-22T19:06:00Z', true),
    scan('019a0000-0000-7000-8000-0000000c0002', 'Manual', '2026-11-21T07:40:00Z', false),
    scan('019a0000-0000-7000-8000-0000000c0001', 'Schedule', '2026-11-20T18:30:00Z', false),
]

/**
 * Answers the media folders and the scans of the library page of the administration with the
 * fixture, so the sections above the layout template have the same height in every run (the scan
 * history grows with every test that scans) and the template editor lies at the same place.
 */
export async function mockLibraryPage(page: Page): Promise<void> {
    await page.route(
        (url) => url.pathname === '/api/v1/sources',
        (route) =>
            route.request().method() === 'GET'
                ? json(route, {
                      items: [
                          {
                              id: sourceId,
                              name: 'Media',
                              path: '/media',
                              followsMediaPath: true,
                              isActive: true,
                              isAvailable: true,
                              isWatched: true,
                              trackCount: 4,
                              fileCount: 8,
                              createdAt: '2026-11-19T04:00:00Z',
                              updatedAt: '2026-11-19T04:00:00Z',
                          },
                      ],
                      offset: 0,
                      limit: 50,
                      total: 1,
                  })
                : route.fallback(),
    )
    await page.route(
        (url) => url.pathname === '/api/v1/scans/current',
        (route) =>
            json(
                route,
                {
                    type: 'https://tools.ietf.org/html/rfc9110#section-15.5.5',
                    title: 'Not Found',
                    status: 404,
                    code: 'scan.not_running',
                },
                404,
            ),
    )
    await page.route(
        (url) => url.pathname === '/api/v1/scans',
        (route) =>
            route.request().method() === 'GET'
                ? json(route, paged(new URL(route.request().url()), scans))
                : route.fallback(),
    )
}
