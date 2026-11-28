// Sample data of the component overview (/dev/components) in the shape of the API models. Names
// come from the messages (placeholders.*), so they are recognizable as samples in every language.
// Covers are marked as missing, so the overview never calls the API.

import {
    ArtistRoleObject,
    type LayoutTemplatePath,
    type LibraryIssue,
    type LibraryIssueSummary,
    type MediaFile,
    type OrganizeItem,
    type OrganizeOperation,
    type OwnAlbum,
    type Track,
    type TrackFileSelection,
    type TrackFileSummary,
    type TrackReview,
} from '@/api/generated/models'
import type { Tile } from '@/components/library/TileList.vue'
import type { OverviewGroup } from '@/library/groupOverview'
import type { OnlineMatch, OnlineValues } from '@/library/onlineMetadata'
import type { TileGroup } from '@/library/tileGrouping'
import type { LyricsMatch } from '@/lyrics/api'
import type { LyricsLineTiming, TrackLyrics } from '@/lyrics/timing'
import type { VideoDiagnosticsSnapshot } from '@/player/videoDiagnostics'

type Translate = (key: string, values?: Record<string, unknown>) => string

interface SampleAlbum {
    readonly number: number
    readonly artistNumber: number
    readonly genreNumber: number
    readonly year: number
    readonly tracks: readonly {
        readonly title: number
        readonly seconds: number
        readonly video?: 'with-audio' | 'only'
    }[]
}

const albums: readonly SampleAlbum[] = [
    {
        number: 1,
        artistNumber: 1,
        genreNumber: 1,
        year: 2006,
        tracks: [
            { title: 1, seconds: 225, video: 'with-audio' },
            { title: 2, seconds: 198 },
            { title: 3, seconds: 254 },
            { title: 4, seconds: 187, video: 'only' },
        ],
    },
    {
        number: 2,
        artistNumber: 2,
        genreNumber: 2,
        year: 2007,
        tracks: [
            { title: 5, seconds: 242 },
            { title: 6, seconds: 3725, video: 'with-audio' },
        ],
    },
]

/** Tracks of two sample albums. */
export function sampleTracks(t: Translate): Track[] {
    return albums.flatMap((album) =>
        album.tracks.map((track, index) => {
            const artist = {
                id: `sample-artist-${album.artistNumber}`,
                name: t('placeholders.artist', { number: album.artistNumber }),
            }
            return {
                id: `sample-track-${track.title}`,
                title: t('placeholders.track', { number: track.title }),
                artist,
                // The second song of every album has a guest, so the lists show several artists.
                artists:
                    index === 1
                        ? [
                              { ...artist, role: ArtistRoleObject.Main },
                              {
                                  id: 'sample-artist-guest',
                                  name: t('placeholders.artist', { number: 9 }),
                                  role: ArtistRoleObject.Featured,
                              },
                          ]
                        : [{ ...artist, role: ArtistRoleObject.Main }],
                album: {
                    id: `sample-album-${album.number}`,
                    title: t('placeholders.album', { number: album.number }),
                    artist,
                    artists: [{ ...artist, role: ArtistRoleObject.Main }],
                    isCompilation: false,
                },
                trackNumber: index + 1,
                discNumber: 1,
                year: album.year,
                genre: t('placeholders.genre', { number: album.genreNumber }),
                durationMilliseconds: track.seconds * 1000,
                hasAudio: track.video !== 'only',
                hasVideo: track.video !== undefined,
                hasCover: false,
                // Some songs are rated or favorites, so the overview shows every state.
                isFavorite: track.title % 2 === 1,
                rating: track.title % 6,
            }
        }),
    )
}

/** One tile of every kind. */
export function sampleTiles(t: Translate): Tile[] {
    return [
        {
            id: 'album',
            kind: 'album',
            title: t('placeholders.album', { number: 1 }),
            subtitle: t('placeholders.artist', { number: 1 }),
            to: { name: 'library-album', params: { id: 'sample-album-1' } },
            cover: { id: 'sample-album-1', available: false },
        },
        {
            id: 'artist',
            kind: 'artist',
            title: t('placeholders.artist', { number: 1 }),
            subtitle: t('library.songCount', { count: 4 }),
            to: { name: 'library-artist', params: { id: 'sample-artist-1' } },
        },
        {
            id: 'genre',
            kind: 'genre',
            title: t('placeholders.genre', { number: 1 }),
            subtitle: t('library.songCount', { count: 6 }),
            to: { name: 'library-genre', params: { genre: 'sample' } },
        },
        {
            id: 'year',
            kind: 'year',
            title: '2006',
            subtitle: t('library.songCount', { count: 4 }),
            to: { name: 'library-year', params: { year: '2006' } },
        },
        {
            id: 'playlist',
            kind: 'playlist',
            title: t('placeholders.playlist', { number: 1 }),
            subtitle: t('library.songCount', { count: 3 }),
            to: { name: 'playlist', params: { id: 'sample-playlist-1' } },
        },
        {
            id: 'smart-playlist',
            kind: 'smart-playlist',
            title: t('placeholders.playlist', { number: 2 }),
            subtitle: t('library.songCount', { count: 12 }),
            to: { name: 'smart-playlist', params: { id: 'sample-smart-playlist-1' } },
            badge: 'smart-playlist',
        },
        {
            id: 'recently-played',
            kind: 'recently-played',
            title: t('playlists.recentlyPlayed'),
            subtitle: t('playlists.automatic'),
            to: { name: 'playlists-recently-played' },
        },
    ]
}

/**
 * Artist tiles grouped by initial letter, as in the artists sorted by name: two letters and the
 * group of digits and special characters, the first group starting further down a long list.
 */
export function sampleTileGroups(t: Translate): TileGroup<Tile>[] {
    const artist = (number: number): Tile => ({
        id: `artist-${number}`,
        kind: 'artist',
        title: t('placeholders.artist', { number }),
        subtitle: t('library.songCount', { count: number + 2 }),
        to: { name: 'library-artist', params: { id: `sample-artist-${number}` } },
    })
    return [
        {
            id: 'letter:P',
            kind: 'letter',
            letter: 'P',
            position: 120,
            items: [artist(1), artist(2)],
        },
        { id: 'letter:Q', kind: 'letter', letter: 'Q', position: 122, items: [artist(3)] },
        { id: 'letter:#', kind: 'letter', letter: '#', position: 123, items: [artist(4)] },
    ]
}

/**
 * The groups of a long list of songs for the overview of the groups: 40 albums of artists whose
 * names start with 18 different letters (so the letter bar shows, with letters that have no
 * group), then the songs without album of an artist and those without album and artist. The
 * first groups are at the top of the list, the current one is the third.
 */
export function sampleOverviewGroups(t: Translate): OverviewGroup[] {
    const letters = 'ABCDEFGHKLMNPRSTW#'
    const albums = Array.from({ length: 40 }, (_, index): OverviewGroup => {
        const number = index + 1
        const artistNumber = Math.floor(index / 3) + 1
        return {
            id: `album-${number}`,
            kind: 'album',
            position: index * 10,
            count: 10,
            album: {
                id: `sample-album-${number}`,
                title: t('placeholders.album', { number }),
                artist: {
                    id: `sample-artist-${artistNumber}`,
                    name: t('placeholders.artist', { number: artistNumber }),
                },
                isCompilation: false,
            },
            hasCover: false,
            letter: letters[Math.floor((index * letters.length) / 40)],
        }
    })
    return [
        ...albums,
        {
            id: 'without-album',
            kind: 'unknown',
            unknown: 'album',
            position: 400,
            count: 3,
            artist: { id: 'sample-artist-20', name: t('placeholders.artist', { number: 20 }) },
            letter: 'W',
        },
        { id: 'unknown', kind: 'unknown', unknown: 'album', position: 403, count: 1, letter: '#' },
    ]
}

/** The groups of the artists by letter for the overview: the letters as short tiles. */
export function sampleLetterGroups(): OverviewGroup[] {
    let position = 0
    return [...'ABCDEFGHIJKLMNOPRSTUVWZ#'].map((letter, index) => {
        const count = (index % 5) + 1
        const group: OverviewGroup = {
            id: `letter-${letter}`,
            kind: 'letter',
            position,
            count,
            letter,
        }
        position += count
        return group
    })
}

/** The files of a sample track: lossless audio, a lower quality copy, a video and a picture. */
export function sampleFiles(): MediaFile[] {
    const modifiedAt = new Date(Date.UTC(2026, 10, 1, 9, 30))
    const base = { modifiedAt, isWritable: false, isLossless: false, hasEmbeddedPicture: false }
    return [
        {
            ...base,
            id: 'flac',
            kind: 'Audio',
            relativePath: 'Artist/Album/02 - Song.flac',
            isPrimary: true,
            codec: 'flac',
            bitrate: 912_000,
            sampleRate: 44_100,
            bitDepth: 16,
            channels: 2,
            isLossless: true,
            isWritable: true,
            durationMilliseconds: 225_000,
            size: 25_600_000,
        },
        {
            ...base,
            id: 'mp3',
            kind: 'Audio',
            relativePath: 'Artist/Album/02 - Song.mp3',
            isPrimary: false,
            codec: 'mp3',
            bitrate: 320_000,
            sampleRate: 44_100,
            channels: 2,
            durationMilliseconds: 225_000,
            size: 9_000_000,
            issue: 'file.tags_unreadable',
        },
        {
            ...base,
            id: 'mkv',
            kind: 'Video',
            relativePath: 'Artist/Album/02 - Song.mkv',
            isPrimary: true,
            codec: 'h264',
            width: 1920,
            height: 1080,
            durationMilliseconds: 226_000,
            size: 152_000_000,
        },
        {
            ...base,
            id: 'jpg',
            kind: 'Image',
            relativePath: 'Artist/Album/02 - Song.jpg',
            isPrimary: false,
            width: 600,
            height: 600,
            size: 84_000,
        },
    ]
}

/** Songs of the file selection: one with a selected audio file and a missing cover, one plain. */
/** The result of a library check: every kind of problem once, the duplicate with its twin. */
export function sampleLibraryIssueSummary(): LibraryIssueSummary {
    return {
        checkedAt: new Date(Date.UTC(2026, 10, 19, 8, 30)),
        trackCount: 1240,
        problemCount: 12,
        hintCount: 31,
        counts: [
            { type: 'UnreadableFile', severity: 'Problem', count: 1 },
            { type: 'MissingTags', severity: 'Problem', count: 4 },
            { type: 'UnknownAlbum', severity: 'Problem', count: 3 },
            { type: 'MissingCover', severity: 'Problem', count: 2 },
            { type: 'PossibleDuplicate', severity: 'Problem', count: 2 },
            { type: 'VideoWithoutAudio', severity: 'Hint', count: 0 },
            { type: 'AudioWithoutVideo', severity: 'Hint', count: 31 },
            { type: 'PossibleMultipleArtists', severity: 'Hint', count: 2 },
        ],
    }
}

export function sampleLibraryIssues(t: Translate): LibraryIssue[] {
    const artist = { id: 'artist-1', name: t('placeholders.artist', { number: 1 }) }
    const album = { id: 'album-1', title: t('placeholders.album', { number: 1 }) }
    const duo = [1, 2].map((number) => t('placeholders.artist', { number })).join(' & ')
    const track = (number: number, directoryPath: string, values = {}) => ({
        id: `sample-track-${number}`,
        title: t('placeholders.track', { number }),
        artist,
        album,
        sourceId: 'source-1',
        sourceName: 'Music',
        directoryPath,
        baseName: `0${number} - Song`,
        durationMilliseconds: 201_000 + number * 1_000,
        hasAudio: true,
        hasVideo: false,
        ...values,
    })
    const detectedAt = new Date(Date.UTC(2026, 10, 19, 8, 30))
    return [
        {
            id: 'sample-issue-tags',
            type: 'MissingTags',
            severity: 'Problem',
            detectedAt,
            track: track(2, 'Artist/Album', { album: null }),
            missingTags: ['Album', 'Year'],
        },
        {
            id: 'sample-issue-duplicate',
            type: 'PossibleDuplicate',
            severity: 'Problem',
            detectedAt,
            track: track(3, 'Artist/Album'),
            missingTags: [],
            relatedTrack: track(3, 'Artist/Best Of'),
        },
        {
            id: 'sample-issue-unreadable',
            type: 'UnreadableFile',
            severity: 'Problem',
            detectedAt,
            track: track(4, 'Artist/Album'),
            file: {
                id: 'sample-file',
                sourceId: 'source-1',
                sourceName: 'Music',
                relativePath: 'Artist/Album/04 - Song.flac',
                kind: 'Audio',
            },
            fileIssue: 'file.probe_failed',
            missingTags: [],
        },
        {
            id: 'sample-issue-artists',
            type: 'PossibleMultipleArtists',
            severity: 'Hint',
            detectedAt,
            track: track(6, 'Artist/Album', {
                artist: { id: 'artist-duo', name: duo },
                artists: [{ id: 'artist-duo', name: duo, role: ArtistRoleObject.Main }],
            }),
            missingTags: [],
            suggestedArtists: [
                { name: t('placeholders.artist', { number: 1 }), role: ArtistRoleObject.Main },
                { name: t('placeholders.artist', { number: 2 }), role: ArtistRoleObject.Main },
            ],
        },
        {
            id: 'sample-issue-hint',
            type: 'AudioWithoutVideo',
            severity: 'Hint',
            detectedAt,
            track: track(5, 'Artist/Album'),
            missingTags: [],
        },
    ]
}

export function sampleFileSummaries(t: Translate): TrackFileSummary[] {
    return [
        {
            trackId: 'sample-selected',
            title: t('placeholders.track', { number: 2 }),
            artist: { id: 'artist-1', name: t('placeholders.artist', { number: 1 }) },
            album: { id: 'album-1', title: t('placeholders.album', { number: 1 }) },
            directoryPath: 'Artist/Album',
            baseName: '02 - Song',
            audioCount: 2,
            videoCount: 1,
            coverCount: 2,
            audio: 'Selected',
            video: 'Automatic',
            cover: 'Missing',
        },
        {
            trackId: 'sample-automatic',
            title: t('placeholders.track', { number: 3 }),
            artist: { id: 'artist-1', name: t('placeholders.artist', { number: 1 }) },
            album: null,
            directoryPath: 'Artist/Album',
            baseName: '03 - Song',
            audioCount: 1,
            videoCount: 0,
            coverCount: 0,
            audio: 'Automatic',
            video: 'Automatic',
            cover: 'Automatic',
        },
    ]
}

/** Songs of the review inbox: one with missing tags, one with complete tags, one reviewed. */
export function sampleTrackReviews(t: Translate): TrackReview[] {
    const artist = { id: 'artist-1', name: t('placeholders.artist', { number: 1 }) }
    const album = { id: 'album-1', title: t('placeholders.album', { number: 1 }) }
    return [
        {
            trackId: 'sample-review-missing',
            title: t('placeholders.track', { number: 4 }),
            artist,
            album: null,
            directoryPath: 'Unsorted',
            baseName: 'Track 04',
            hasAudio: true,
            hasVideo: false,
            hasCover: false,
            firstSeenAt: new Date(Date.UTC(2026, 10, 16, 18, 30)),
            missingTags: ['Album', 'Year', 'TrackNumber', 'Genre'],
            status: 'Unreviewed',
        },
        {
            trackId: 'sample-review-complete',
            title: t('placeholders.track', { number: 5 }),
            artist,
            album,
            directoryPath: 'Artist/Album',
            baseName: '05 - Song',
            hasAudio: true,
            hasVideo: true,
            hasCover: false,
            firstSeenAt: new Date(Date.UTC(2026, 10, 15, 9, 0)),
            missingTags: [],
            status: 'Unreviewed',
        },
        {
            trackId: 'sample-review-done',
            title: t('placeholders.track', { number: 6 }),
            artist,
            album,
            directoryPath: 'Artist/Album',
            baseName: '06 - Song',
            hasAudio: true,
            hasVideo: false,
            hasCover: false,
            firstSeenAt: new Date(Date.UTC(2026, 10, 14, 12, 0)),
            missingTags: ['Genre'],
            status: 'Reviewed',
        },
    ]
}

/**
 * Word-synchronized lyrics of four seconds per line with a part without singing after the second
 * line; at sampleLyricsPosition the third line is sung, in its second word.
 */
export function sampleLyrics(t: Translate, trackId: string): TrackLyrics {
    const texts = [1, 2, null, 3, 4, 5].map((number) =>
        number === null ? '' : t('placeholders.lyricsLine', { number }),
    )
    const lines: LyricsLineTiming[] = texts.map((text, index) => {
        const start = index * 4
        const parts = text === '' ? [] : text.split(' ')
        const share = 4 / Math.max(1, parts.length)
        return {
            start,
            end: index < texts.length - 1 ? start + 4 : null,
            text,
            words: parts.map((part, wordIndex) => ({
                start: start + wordIndex * share,
                end: wordIndex < parts.length - 1 ? start + (wordIndex + 1) * share : null,
                text: wordIndex < parts.length - 1 ? `${part} ` : part,
            })),
        }
    })
    return { trackId, type: 'WordSynchronized', source: 'File', lines }
}

/** Plain lyrics of another version of the song: they carry the note and do not follow it. */
export function sampleOtherVersionLyrics(t: Translate, trackId: string): TrackLyrics {
    const lines = [1, 2, null, 3, 4].map((number) => ({
        start: null,
        end: null,
        text: number === null ? '' : t('placeholders.lyricsLine', { number }),
        words: [],
    }))
    return { trackId, type: 'Static', source: 'Download', lines, fromOtherVersion: true }
}

/** Three tracks of the search of lyrics: synced and fitting, plain text, and another version. */
export function sampleLyricsMatches(t: Translate): LyricsMatch[] {
    const title = t('placeholders.track', { number: 2 })
    const artist = t('placeholders.artist', { number: 1 })
    return [
        {
            id: 1,
            title,
            artist,
            album: t('placeholders.album', { number: 1 }),
            duration: 203,
            synchronized: true,
            fitsDuration: true,
        },
        {
            id: 2,
            title,
            artist,
            album: null,
            duration: 204,
            synchronized: false,
            fitsDuration: true,
        },
        {
            id: 3,
            title,
            artist,
            album: t('placeholders.album', { number: 2 }),
            duration: 251,
            synchronized: true,
            fitsDuration: false,
        },
    ]
}

/** Position of the music in the lyrics sample, in seconds. */
export const sampleLyricsPosition = 12.9

/** Diagnostics of a transcoded video that stuttered once and plays smoothly again. */
export function sampleVideoDiagnostics(): VideoDiagnosticsSnapshot {
    const event = { position: 61.2, rate: 1, readyState: 4, bufferedAhead: 8.4, action: 'rate' }
    return {
        delivery: 'hls',
        elapsed: 74.5,
        drift: -0.062,
        rate: 1.02,
        readyState: 4,
        action: 'rate',
        buffered: { start: 54, end: 84 },
        bufferedAhead: 9.6,
        seeks: 2,
        missedSeeks: 0,
        seekLatency: 0.38,
        droppedFrames: 14,
        totalFrames: 1862,
        stuttering: false,
        stutters: 1,
        events: [
            { ...event, at: 58.1, kind: 'waiting', drift: -0.21, readyState: 2, value: null },
            { ...event, at: 58.4, kind: 'stutter-start', drift: -0.34, readyState: 2, value: 6 },
            { ...event, at: 60.2, kind: 'seek', drift: -0.58, value: 61.6 },
            { ...event, at: 61.5, kind: 'rate', drift: -0.09, value: 1.02 },
            { ...event, at: 66.3, kind: 'stutter-end', drift: -0.05, value: 2.1 },
        ],
    }
}

/** The selection of the sample files: the mp3 selected, the video automatic, a cover missing. */
export function sampleFileSelection(): TrackFileSelection {
    return {
        trackId: 'sample-selected',
        audio: {
            state: 'Selected',
            selectedFileId: 'mp3',
            selectedPath: 'Artist/Album/02 - Song.mp3',
            activeFileId: 'mp3',
            automaticFileId: 'flac',
            candidateFileIds: ['flac', 'mp3'],
        },
        video: {
            state: 'Automatic',
            activeFileId: 'mkv',
            automaticFileId: 'mkv',
            candidateFileIds: ['mkv'],
        },
        cover: {
            state: 'Missing',
            selectedFileId: 'png',
            selectedPath: 'Artist/Album/02 - Song.png',
            activeFileId: 'jpg',
            automaticFileId: 'jpg',
            candidateFileIds: ['jpg'],
        },
    }
}

/** Two matches of the online search for a song: one with a release, one without. */
export function sampleOnlineMatches(t: Translate): OnlineMatch[] {
    const values = {
        title: t('placeholders.track', { number: 2 }),
        artist: t('placeholders.artist', { number: 1 }),
        albumArtist: t('placeholders.artist', { number: 1 }),
        trackNumber: 2,
        discNumber: 1,
        genre: null,
    }
    return [
        {
            key: 'sample-match-1',
            release: { id: 'sample-release-1', date: '2006-05-01', country: 'DE', trackCount: 4 },
            agreement: 92,
            values: { ...values, album: t('placeholders.album', { number: 1 }), year: 2006 },
        },
        {
            key: 'sample-match-2',
            release: null,
            agreement: 47,
            values: { ...values, album: null, year: null },
        },
    ]
}

/** The current values of the song the sample matches belong to. */
export function sampleOnlineCurrent(t: Translate): OnlineValues {
    return {
        title: t('placeholders.track', { number: 2 }),
        artist: t('placeholders.artist', { number: 1 }),
        album: t('placeholders.album', { number: 1 }),
        albumArtist: null,
        year: 2007,
        trackNumber: 2,
        discNumber: null,
        genre: t('placeholders.genre', { number: 1 }),
    }
}

/** Own albums in every state; without covers, so nothing is loaded. */
export function sampleOwnAlbums(t: Translate): OwnAlbum[] {
    const album = (number: number, status: OwnAlbum['status'], trackCount: number): OwnAlbum => ({
        id: `sample-own-album-${number}`,
        title: t('placeholders.album', { number }),
        albumArtist: t('placeholders.artist', { number }),
        year: 2020 + number,
        status,
        trackCount,
        removedTrackCount: status === 'Changed' ? 1 : 0,
        hasCover: false,
    })
    return [album(7, 'Draft', 4), album(8, 'Applied', 12), album(9, 'Changed', 9)]
}

/** Operations of the organize log: a run that can be undone, an undone one and a stopped one. */
export function sampleOrganizeOperations(t: Translate): OrganizeOperation[] {
    const createdAt = new Date(Date.UTC(2026, 10, 19, 8, 30))
    return [
        {
            id: 'sample-organize-1',
            kind: 'OwnAlbum',
            status: 'Completed',
            displayTitle: t('placeholders.album', { number: 8 }),
            totalItems: 12,
            doneItems: 12,
            conflictItems: 0,
            warningItems: 2,
            canUndo: true,
            createdAt,
        },
        {
            id: 'sample-organize-2',
            kind: 'Library',
            status: 'Completed',
            totalItems: 240,
            doneItems: 236,
            conflictItems: 4,
            warningItems: 0,
            skipConflicts: true,
            undoneById: 'sample-organize-3',
            createdAt,
        },
        {
            id: 'sample-organize-3',
            kind: 'Undo',
            status: 'Stopped',
            errorCode: 'item_failed',
            totalItems: 236,
            doneItems: 120,
            failedItems: 1,
            createdAt,
        },
        {
            id: 'sample-organize-4',
            kind: 'Library',
            status: 'Previewed',
            totalItems: 18,
            conflictItems: 1,
            warningItems: 1,
            createdAt,
        },
    ]
}

/** A running operation for organize-progress. */
export function sampleRunningOperation(): OrganizeOperation {
    return {
        id: 'sample-organize-5',
        kind: 'Library',
        status: 'Running',
        totalItems: 240,
        doneItems: 96,
    }
}

/** Items of a preview: a move with tags, a conflict, a warning and the cover of an own album. */
export function sampleOrganizeItems(t: Translate): OrganizeItem[] {
    const artist = t('placeholders.artist', { number: 1 })
    const album = t('placeholders.album', { number: 8 })
    const title = (number: number) => t('placeholders.track', { number })
    return [
        {
            id: 'sample-item-1',
            kind: 'Track',
            status: 'Pending',
            displayTitle: title(1),
            displayArtist: artist,
            fromDirectoryPath: 'Downloads/Mixed',
            fromBaseName: title(1),
            toDirectoryPath: `${artist}/${album}`,
            toBaseName: `01 ${title(1)}`,
            moves: [
                { id: 'sample-move-1', fileKind: 'Audio', fromPath: 'a.flac', toPath: 'b.flac' },
                { id: 'sample-move-2', fileKind: 'Video', fromPath: 'a.mkv', toPath: 'b.mkv' },
            ],
            tagChanges: {
                fields: ['Album', 'TrackNumber', 'Year'],
                album,
                trackNumber: 1,
                year: 2028,
            },
        },
        {
            id: 'sample-item-2',
            kind: 'Track',
            status: 'Conflict',
            conflict: 'target_exists',
            displayTitle: title(2),
            displayArtist: artist,
            fromDirectoryPath: `${artist}/Singles`,
            fromBaseName: title(2),
            toDirectoryPath: `${artist}/${album}`,
            toBaseName: `02 ${title(2)}`,
            moves: [{ id: 'sample-move-3', fileKind: 'Audio', fromPath: 'c.mp3', toPath: 'd.mp3' }],
        },
        {
            id: 'sample-item-3',
            kind: 'Track',
            status: 'Pending',
            warnings: ['album_merges'],
            displayTitle: title(3),
            displayArtist: artist,
            fromDirectoryPath: `${artist}/${album}`,
            fromBaseName: title(3),
            toDirectoryPath: `${artist}/${album}`,
            toBaseName: `03 ${title(3)}`,
            moves: [{ id: 'sample-move-4', fileKind: 'Audio', fromPath: 'e.ogg', toPath: 'f.ogg' }],
        },
        {
            id: 'sample-item-4',
            kind: 'AlbumCover',
            status: 'Pending',
            displayTitle: album,
            toDirectoryPath: `${artist}/${album}`,
            moves: [
                {
                    id: 'sample-move-5',
                    fileKind: 'AlbumCover',
                    toPath: `${artist}/${album}/cover.jpg`,
                },
            ],
        },
    ]
}

/** Paths of the template preview, one of them too long. */
export function sampleTemplatePaths(t: Translate): LayoutTemplatePath[] {
    const artist = t('placeholders.artist', { number: 1 })
    const album = t('placeholders.album', { number: 1 })
    return [
        {
            folders: [artist, album],
            baseName: `01 ${t('placeholders.track', { number: 1 })}`,
            extension: 'flac',
        },
        {
            folders: [artist, album],
            baseName: `02 ${t('placeholders.track', { number: 2 })}`,
            extension: 'mp3',
            isTooLong: true,
        },
    ]
}
