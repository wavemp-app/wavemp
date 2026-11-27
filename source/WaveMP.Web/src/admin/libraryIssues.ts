// Presentation of the library check: the kinds of problem with their filter, badge and message
// keys, the sort orders of the list, and per problem where it is, what is wrong and where the
// admin can fix it. Pure functions only.

import {
    ArtistRoleObject,
    LibraryIssueSeverityObject,
    LibraryIssueSortObject,
    LibraryIssueTypeObject,
    type ArtistCredit,
    type LibraryIssue,
    type LibraryIssueSort,
    type LibraryIssueSummary,
    type LibraryIssueTrack,
    type LibraryIssueType,
    type UpdateTrackMetadata,
} from '@/api/generated/models'
import type { RouteLocationRaw } from 'vue-router'
import { issueLabelKey } from '@/admin/presentation'
import { artistsOf, artistText, plainArtistSeparators } from '@/library/artists'
import { missingTagHints } from '@/review/inbox'

/** Filter of the list: every kind of problem, or one kind. */
export type LibraryIssueFilter = 'all' | LibraryIssueType

interface LibraryIssueTypePresentation {
    /** data-item of the segment and of the problems, and data-state token of their badge. */
    readonly token: string
    /** Name of the kind on a problem. */
    readonly labelKey: string
    /** Short name of the kind as filter. */
    readonly filterKey: string
}

const types: Readonly<Record<LibraryIssueType, LibraryIssueTypePresentation>> = {
    [LibraryIssueTypeObject.UnreadableFile]: {
        token: 'unreadable-file',
        labelKey: 'adminChecks.types.unreadableFile',
        filterKey: 'adminChecks.filters.unreadableFile',
    },
    [LibraryIssueTypeObject.MissingTags]: {
        token: 'missing-tags',
        labelKey: 'adminChecks.types.missingTags',
        filterKey: 'adminChecks.filters.missingTags',
    },
    [LibraryIssueTypeObject.UnknownAlbum]: {
        token: 'unknown-album',
        labelKey: 'adminChecks.types.unknownAlbum',
        filterKey: 'adminChecks.filters.unknownAlbum',
    },
    [LibraryIssueTypeObject.MissingCover]: {
        token: 'missing-cover',
        labelKey: 'adminChecks.types.missingCover',
        filterKey: 'adminChecks.filters.missingCover',
    },
    [LibraryIssueTypeObject.PossibleDuplicate]: {
        token: 'possible-duplicate',
        labelKey: 'adminChecks.types.possibleDuplicate',
        filterKey: 'adminChecks.filters.possibleDuplicate',
    },
    [LibraryIssueTypeObject.VideoWithoutAudio]: {
        token: 'video-without-audio',
        labelKey: 'adminChecks.types.videoWithoutAudio',
        filterKey: 'adminChecks.filters.videoWithoutAudio',
    },
    [LibraryIssueTypeObject.AudioWithoutVideo]: {
        token: 'audio-without-video',
        labelKey: 'adminChecks.types.audioWithoutVideo',
        filterKey: 'adminChecks.filters.audioWithoutVideo',
    },
    [LibraryIssueTypeObject.PossibleMultipleArtists]: {
        token: 'possible-multiple-artists',
        labelKey: 'adminChecks.types.possibleMultipleArtists',
        filterKey: 'adminChecks.filters.possibleMultipleArtists',
    },
}

/** The kinds of problem in the order of the API: problems first, hints last. */
export const libraryIssueTypes: readonly LibraryIssueType[] = Object.values(LibraryIssueTypeObject)

export function isLibraryIssueFilter(value: unknown): value is LibraryIssueFilter {
    return value === 'all' || libraryIssueTypes.some((type) => type === value)
}

/** Token of a kind of problem, e.g. "missing-cover"; "unknown" for kinds this app does not know. */
export function issueTypeToken(type: LibraryIssueType | null | undefined): string {
    return (type && types[type]?.token) || 'unknown'
}

/** Message key of the name of a kind of problem. */
export function issueTypeLabelKey(type: LibraryIssueType | null | undefined): string {
    return (type && types[type]?.labelKey) || 'adminChecks.types.unknown'
}

/** data-state token of the severity: "problem" or "hint". */
export function severityToken(issue: Pick<LibraryIssue, 'severity'>): 'problem' | 'hint' {
    return issue.severity === LibraryIssueSeverityObject.Hint ? 'hint' : 'problem'
}

export interface LibraryIssueTypeOption {
    readonly value: LibraryIssueFilter
    /** data-item of the segment. */
    readonly item: string
    readonly labelKey: string
    readonly count: number
    readonly severity: 'problem' | 'hint' | 'all'
}

/**
 * The filters of the list with the number of problems of each: all problems and every kind in the
 * order of the API, also kinds without problems (0). Without a summary every number is 0.
 */
export function issueTypeOptions(
    summary: LibraryIssueSummary | null | undefined,
): LibraryIssueTypeOption[] {
    const counts = new Map((summary?.counts ?? []).map((count) => [count.type, count]))
    const all: LibraryIssueTypeOption = {
        value: 'all',
        item: 'all',
        labelKey: 'adminChecks.filters.all',
        count: (summary?.problemCount ?? 0) + (summary?.hintCount ?? 0),
        severity: 'all',
    }
    return [
        all,
        ...libraryIssueTypes.map((type) => {
            const count = counts.get(type)
            return {
                value: type,
                item: types[type].token,
                labelKey: types[type].filterKey,
                count: count?.count ?? 0,
                severity:
                    count?.severity === LibraryIssueSeverityObject.Hint
                        ? ('hint' as const)
                        : ('problem' as const),
            }
        }),
    ]
}

/** The type parameter of the API for a filter; undefined for all problems. */
export function typeParameter(filter: LibraryIssueFilter): LibraryIssueType[] | undefined {
    return filter === 'all' ? undefined : [filter]
}

export interface LibraryIssueSortOption {
    readonly value: LibraryIssueSort
    /** data-item of the segment. */
    readonly item: string
    readonly labelKey: string
}

/** Sort orders of the list; the API sorts by folder by default and the newest problems first. */
export const libraryIssueSorts: readonly LibraryIssueSortOption[] = [
    { value: LibraryIssueSortObject.Path, item: 'path', labelKey: 'adminChecks.sorts.path' },
    { value: LibraryIssueSortObject.Artist, item: 'artist', labelKey: 'adminChecks.sorts.artist' },
    { value: LibraryIssueSortObject.Title, item: 'title', labelKey: 'adminChecks.sorts.title' },
    {
        value: LibraryIssueSortObject.DetectedAt,
        item: 'detected-at',
        labelKey: 'adminChecks.sorts.detectedAt',
    },
]

export function isLibraryIssueSort(value: unknown): value is LibraryIssueSort {
    return libraryIssueSorts.some((option) => option.value === value)
}

/** Folder and base name of the files of a song, e.g. "Band/Album/01 Song". */
export function trackLocation(track: LibraryIssueTrack | null | undefined): string {
    return [track?.directoryPath, track?.baseName].filter((part) => !!part).join('/')
}

/** Where the problem is: the file for an unreadable file, else the files of the song. */
export function issueLocation(issue: LibraryIssue): string {
    return issue.file?.relativePath || trackLocation(issue.track)
}

/** Artists and album of the song, as far as they are known. */
export function trackContext(track: LibraryIssueTrack | null | undefined): string[] {
    return [artistText(artistsOf(track), plainArtistSeparators), track?.album?.title].filter(
        (part): part is string => !!part,
    )
}

export interface LibraryIssueDetail {
    readonly key: string
    /** Named values of the message, e.g. the list of missing tags. */
    readonly tagKeys: readonly string[]
}

/**
 * What is wrong, as a message key: the missing tags (their message keys in tagKeys), the reason
 * a file is unreadable, or the description of the kind of problem.
 */
export function issueDetail(issue: LibraryIssue): LibraryIssueDetail {
    switch (issue.type) {
        case LibraryIssueTypeObject.MissingTags:
            return {
                key: 'adminChecks.details.missingTags',
                tagKeys: missingTagHints(issue.missingTags).map((hint) => hint.labelKey),
            }
        case LibraryIssueTypeObject.UnreadableFile:
            return { key: issueLabelKey(issue.fileIssue), tagKeys: [] }
        case LibraryIssueTypeObject.UnknownAlbum:
            return { key: 'adminChecks.details.unknownAlbum', tagKeys: [] }
        case LibraryIssueTypeObject.MissingCover:
            return { key: 'adminChecks.details.missingCover', tagKeys: [] }
        case LibraryIssueTypeObject.PossibleDuplicate:
            return { key: 'adminChecks.details.possibleDuplicate', tagKeys: [] }
        case LibraryIssueTypeObject.VideoWithoutAudio:
            return { key: 'adminChecks.details.videoWithoutAudio', tagKeys: [] }
        case LibraryIssueTypeObject.AudioWithoutVideo:
            return { key: 'adminChecks.details.audioWithoutVideo', tagKeys: [] }
        case LibraryIssueTypeObject.PossibleMultipleArtists:
            return { key: 'adminChecks.details.possibleMultipleArtists', tagKeys: [] }
        default:
            return { key: 'adminChecks.details.unknown', tagKeys: [] }
    }
}

export interface LibraryIssueAction {
    /** data-item of the link. */
    readonly item: 'edit-metadata' | 'search-online' | 'show-files'
    readonly labelKey: string
    readonly to: RouteLocationRaw
}

/** Kinds of problem the metadata editor and the online search can fix. */
const metadataFixes: readonly LibraryIssueType[] = [
    LibraryIssueTypeObject.MissingTags,
    LibraryIssueTypeObject.UnknownAlbum,
    LibraryIssueTypeObject.MissingCover,
    LibraryIssueTypeObject.PossibleDuplicate,
    LibraryIssueTypeObject.PossibleMultipleArtists,
]

/** Kinds of problem the online search can fix (it finds tags, albums and covers). */
const onlineFixes: readonly LibraryIssueType[] = [
    LibraryIssueTypeObject.MissingTags,
    LibraryIssueTypeObject.UnknownAlbum,
    LibraryIssueTypeObject.MissingCover,
]

/**
 * Where the admin fixes the problem: the metadata editor and the online search on the page of the
 * song (only for songs with an audio file, the only files whose tags are written), and the files
 * of the song. Problems of a file without a song have no action.
 */
export function issueActions(issue: LibraryIssue): LibraryIssueAction[] {
    const id = issue.track?.id
    if (!id || !issue.type) {
        return []
    }

    const actions: LibraryIssueAction[] = []
    const editable = issue.track?.hasAudio === true
    if (editable && metadataFixes.includes(issue.type)) {
        actions.push({
            item: 'edit-metadata',
            labelKey: 'adminChecks.actions.editMetadata',
            to: { name: 'library-track', params: { id }, query: { edit: 'metadata' } },
        })
    }

    if (editable && onlineFixes.includes(issue.type)) {
        actions.push({
            item: 'search-online',
            labelKey: 'adminChecks.actions.searchOnline',
            to: { name: 'library-track', params: { id }, query: { edit: 'online' } },
        })
    }

    actions.push({
        item: 'show-files',
        labelKey: 'adminChecks.actions.showFiles',
        to: { name: 'admin-track-files', params: { id } },
    })
    return actions
}

/** How the admin answers the hint that the artist name of a song may stand for several artists. */
export type ArtistSplitAnswer = 'split' | 'keep'

/**
 * The suggested artists of a song whose artist name may stand for several artists, each with its
 * role, e.g. "Simon" and "Garfunkel" for "Simon & Garfunkel"; empty for other kinds of problem.
 * Only songs with an audio file can take the answer, since only audio files are written.
 */
export function suggestedArtists(issue: LibraryIssue): ArtistCredit[] {
    if (issue.type !== LibraryIssueTypeObject.PossibleMultipleArtists || !issue.track?.hasAudio) {
        return []
    }

    return (issue.suggestedArtists ?? [])
        .filter((artist) => !!artist.name)
        .map((artist) => ({ name: artist.name, role: artist.role ?? ArtistRoleObject.Main }))
}

/**
 * The artists the metadata editor writes for the answer: the suggested split, or the current
 * artists as they are, each name confirmed as one artist (the tags then hold them as separate
 * values and the hint goes away with the next check).
 */
export function artistSplitChanges(
    issue: LibraryIssue,
    answer: ArtistSplitAnswer,
): UpdateTrackMetadata {
    const artists = answer === 'split' ? suggestedArtists(issue) : artistsOf(issue.track)
    return {
        artists: artists.map((artist) => ({
            name: artist.name ?? '',
            role: artist.role ?? ArtistRoleObject.Main,
        })),
    }
}
