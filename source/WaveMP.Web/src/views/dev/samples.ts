// Sample data of the component overview (/dev/components) in the shape of the API models. Names
// come from the messages (placeholders.*), so they are recognizable as samples in every language.
// Covers are marked as missing, so the overview never calls the API.

import type {
    LibraryIssue,
    LibraryIssueSummary,
    MediaFile,
    Track,
    TrackFileSelection,
    TrackFileSummary,
    TrackReview,
} from '@/api/generated/models'
import type { Tile } from '@/components/library/TileList.vue'
import type { OnlineMatch, OnlineValues } from '@/library/onlineMetadata'
import type { TileGroup } from '@/library/tileGrouping'
import type { LyricsLineTiming, TrackLyrics } from '@/lyrics/timing'

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
                album: {
                    id: `sample-album-${album.number}`,
                    title: t('placeholders.album', { number: album.number }),
                    artist,
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
        ],
    }
}

export function sampleLibraryIssues(t: Translate): LibraryIssue[] {
    const artist = { id: 'artist-1', name: t('placeholders.artist', { number: 1 }) }
    const album = { id: 'album-1', title: t('placeholders.album', { number: 1 }) }
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

/** Position of the music in the lyrics sample, in seconds. */
export const sampleLyricsPosition = 12.9

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
