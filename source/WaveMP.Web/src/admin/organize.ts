// Pure helpers of the organization of the media files: the placeholders and presets of the layout
// template, inserting a placeholder at the cursor, the routes of a preview (old and new path as
// segments with what changes), and the message keys of the codes the API sends.

import {
    OrganizeItemKindObject,
    OrganizeItemStatusObject,
    OrganizeOperationStatusObject,
    SourceAvailabilityStatusObject,
    type LayoutTemplateSample,
    type OrganizeAvailability,
    type OrganizeItem,
    type OrganizeOperation,
    type OrganizeOperationStatus,
    type SourceAvailabilityStatus,
} from '@/api/generated/models'

/** A placeholder of the layout template and the text a click inserts. */
export interface TemplatePlaceholder {
    /** Name as the API spells it; also the key of its label (organize.placeholders.*). */
    readonly name: string
    readonly token: string
}

/** The placeholders in the order of the token row; numbers with two digits by default. */
export const templatePlaceholders: readonly TemplatePlaceholder[] = [
    { name: 'albumArtist', token: '{AlbumArtist}' },
    { name: 'artist', token: '{Artist}' },
    { name: 'album', token: '{Album}' },
    { name: 'year', token: '{Year}' },
    { name: 'discNumber', token: '{DiscNumber}' },
    { name: 'trackNumber', token: '{TrackNumber:00}' },
    { name: 'title', token: '{Title}' },
    { name: 'genre', token: '{Genre}' },
]

/** The two layouts of the issue: folders per artist and album, or flat file names. */
export const templatePresets = {
    folders: '{AlbumArtist}/{Album}/{TrackNumber:00} {Title}',
    flat: '{Artist} - {Title}',
} as const

export type TemplatePreset = keyof typeof templatePresets | 'custom'

/** Longest template the API accepts. */
export const templateMaxLength = 512

/** Which preset a template is, or custom for any other text. */
export function presetOf(template: string): TemplatePreset {
    const trimmed = template.trim()
    for (const [preset, text] of Object.entries(templatePresets)) {
        if (trimmed === text) {
            return preset as TemplatePreset
        }
    }

    return 'custom'
}

/**
 * The text with the token in place of the selection from start to end (both clamped to the text),
 * and where the cursor goes: right after the inserted token.
 */
export function insertToken(
    text: string,
    start: number | null | undefined,
    end: number | null | undefined,
    token: string,
): { text: string; cursor: number } {
    const from = Math.min(Math.max(start ?? text.length, 0), text.length)
    const to = Math.min(Math.max(end ?? from, from), text.length)
    return { text: text.slice(0, from) + token + text.slice(to), cursor: from + token.length }
}

/** Message key of a problem of a layout template (LayoutTemplateErrorCode). */
export function templateErrorKey(code: string | null | undefined): string {
    const known = [
        'required',
        'max_length',
        'unknown_placeholder',
        'unbalanced_brace',
        'unbalanced_section',
        'nested_section',
        'invalid_number_format',
        'missing_title',
        'rooted',
        'empty_folder',
        'parent_folder',
        'folder_in_section',
    ]
    return known.includes(code ?? '')
        ? `organize.templateErrors.${camel(code!)}`
        : 'organize.templateErrors.invalid'
}

/** Example songs of the template preview while the library has no songs. */
export function templateSamples(translate: (key: string) => string): LayoutTemplateSample[] {
    return [
        {
            artist: translate('organize.samples.first.artist'),
            albumArtist: translate('organize.samples.first.artist'),
            album: translate('organize.samples.first.album'),
            title: translate('organize.samples.first.title'),
            genre: translate('organize.samples.first.genre'),
            year: 2019,
            trackNumber: 3,
            discNumber: 1,
            extension: 'flac',
        },
        {
            artist: translate('organize.samples.second.artist'),
            albumArtist: translate('organize.samples.second.artist'),
            album: translate('organize.samples.second.album'),
            title: translate('organize.samples.second.title'),
            genre: translate('organize.samples.second.genre'),
            year: 2023,
            trackNumber: 11,
            discNumber: 2,
            extension: 'mp3',
        },
    ]
}

export type SegmentState = 'same' | 'changed' | 'new'

/** One folder or the file name of a path, with whether it changes. */
export interface PathSegment {
    readonly name: string
    readonly kind: 'folder' | 'file'
    readonly state: SegmentState
}

/** Old and new path of an item as segments; null where the item has no such path. */
export interface Route {
    readonly from: readonly PathSegment[] | null
    readonly to: readonly PathSegment[] | null
}

/** The names of a relative path; empty for the folder of the media source. */
export function splitPath(path: string | null | undefined): string[] {
    return (path ?? '').split('/').filter((name) => name !== '')
}

function withFile(directory: string | null | undefined, file: string): string[] {
    return [...splitPath(directory), ...(file ? [file] : [])]
}

/**
 * The segments of an old and a new path (folders, then the file name if fileNames). A folder of
 * the new path is the same as the old one at the same depth, changed if it differs, or new if the
 * old path had no folder that deep; the file names are compared with each other. The old path is
 * marked the other way round: what does not stay is changed.
 */
export function routeSegments(
    from: readonly string[] | null,
    to: readonly string[] | null,
    fileNames = true,
): Route {
    const folders = (names: readonly string[]) => (fileNames ? names.slice(0, -1) : [...names])
    const fileOf = (names: readonly string[]) => (fileNames ? names.at(-1) : undefined)

    function mark(
        names: readonly string[] | null,
        other: readonly string[] | null,
        deeper: SegmentState,
    ): PathSegment[] | null {
        if (!names) {
            return null
        }

        const otherFolders = other ? folders(other) : []
        const own = folders(names)
        const segments: PathSegment[] = own.map((name, index) => ({
            name,
            kind: 'folder',
            state:
                !other || index >= otherFolders.length
                    ? deeper
                    : otherFolders[index] === name
                      ? 'same'
                      : 'changed',
        }))
        const file = fileOf(names)
        if (file !== undefined) {
            segments.push({
                name: file,
                kind: 'file',
                state: !other ? deeper : fileOf(other) === file ? 'same' : 'changed',
            })
        }

        return segments
    }

    return { from: mark(from, to, 'changed'), to: mark(to, from, 'new') }
}

/** The extension of a path without dot, lower case; empty without one. */
export function extensionOf(path: string | null | undefined): string {
    const name = splitPath(path).at(-1) ?? ''
    const dot = name.lastIndexOf('.')
    return dot > 0 ? name.slice(dot + 1).toLowerCase() : ''
}

/**
 * The route of an item: for a song its folder and base name before and after (all its files keep
 * the base name), for a cover or a single file its path, for the files of a folder the folder.
 */
export function itemRoute(item: OrganizeItem): Route {
    const moves = item.moves ?? []
    if (item.kind === OrganizeItemKindObject.Track || (!item.kind && item.fromBaseName)) {
        return routeSegments(
            withFile(item.fromDirectoryPath, item.fromBaseName ?? ''),
            withFile(item.toDirectoryPath, item.toBaseName ?? ''),
        )
    }

    const single = moves.length === 1 ? moves[0] : undefined
    if (single) {
        return routeSegments(
            single.fromPath ? splitPath(single.fromPath) : null,
            single.toPath ? splitPath(single.toPath) : null,
        )
    }

    return routeSegments(splitPath(item.fromDirectoryPath), splitPath(item.toDirectoryPath), false)
}

/** The extensions of the files of an item, each once, in the order of its moves. */
export function itemExtensions(item: OrganizeItem): string[] {
    return [
        ...new Set(
            (item.moves ?? [])
                .map((move) => extensionOf(move.fromPath ?? move.toPath))
                .filter((extension) => extension !== ''),
        ),
    ]
}

/** data-state tokens of an item: its status, and conflict or warning. */
export function itemStates(item: OrganizeItem): string[] {
    const states = [kebab(item.status ?? OrganizeItemStatusObject.Pending)]
    if (item.conflict) {
        states.push('conflict')
    }
    if ((item.warnings ?? []).length > 0) {
        states.push('warning')
    }
    return [...new Set(states)]
}

export function conflictKey(code: string | null | undefined): string {
    return code ? `organize.conflicts.${camel(code)}` : 'organize.conflicts.unknown'
}

export function warningKey(code: string | null | undefined): string {
    return code ? `organize.warnings.${camel(code)}` : 'organize.warnings.unknown'
}

const activeStatuses: readonly string[] = [
    OrganizeOperationStatusObject.Planning,
    OrganizeOperationStatusObject.Queued,
    OrganizeOperationStatusObject.Running,
]

/** Whether an operation is computing its preview or moving files. */
export function isOperationActive(status: OrganizeOperationStatus | null | undefined): boolean {
    return activeStatuses.includes(status ?? '')
}

/** data-state token of the status of an operation, e.g. running or rolled-back. */
export function operationState(status: string | null | undefined): string {
    return kebab(status ?? 'unknown')
}

export function operationStatusKey(status: string | null | undefined): string {
    return `organize.statuses.${camel(kebab(status ?? 'unknown'))}`
}

/** Message key of the status of an item, e.g. organize.itemStatuses.rolledBack. */
export function itemStatusKey(status: string | null | undefined): string {
    return `organize.itemStatuses.${camel(kebab(status ?? 'Pending'))}`
}

/** Share of the items done of a running operation, or null before it is known. */
export function operationProgress(operation: OrganizeOperation | null | undefined): number | null {
    const total = operation?.totalItems ?? 0
    if (!operation || total <= 0) {
        return null
    }

    const done =
        (operation.doneItems ?? 0) + (operation.skippedItems ?? 0) + (operation.failedItems ?? 0)
    return Math.min(1, done / total)
}

/** Why organizing is not possible now; the message key is organize.blocked.<reason>. */
export type BlockedReason = 'readOnly' | 'missing' | 'inactive' | 'busy' | 'unavailable'

/**
 * Why the files of the sources (all sources without ids) cannot be organized now, or null. A
 * queued or running operation blocks everything; otherwise the worst state of the sources counts.
 */
export function blockedReason(
    availability: OrganizeAvailability | null | undefined,
    sourceIds?: readonly string[],
): BlockedReason | null {
    if (!availability) {
        return null
    }

    if (availability.activeOperationId) {
        return 'busy'
    }

    const sources = (availability.sources ?? []).filter(
        (source) => !sourceIds || sourceIds.includes(source.sourceId ?? ''),
    )
    if (sources.length === 0) {
        return sourceIds ? null : 'unavailable'
    }

    const order: readonly SourceAvailabilityStatus[] = [
        SourceAvailabilityStatusObject.ReadOnly,
        SourceAvailabilityStatusObject.Missing,
        SourceAvailabilityStatusObject.Inactive,
    ]
    // Without chosen sources, organizing is possible while one source can be written.
    if (!sourceIds && sources.some((source) => source.status === 'Available')) {
        return null
    }

    for (const status of order) {
        if (sources.some((source) => source.status === status)) {
            return availabilityReason(status)
        }
    }

    return null
}

/** The reason of a source state that blocks organizing, or null for an available source. */
export function availabilityReason(
    status: SourceAvailabilityStatus | null | undefined,
): BlockedReason | null {
    switch (status) {
        case SourceAvailabilityStatusObject.ReadOnly:
            return 'readOnly'
        case SourceAvailabilityStatusObject.Missing:
            return 'missing'
        case SourceAvailabilityStatusObject.Inactive:
            return 'inactive'
        default:
            return null
    }
}

/** Message key of the reason an operation ended without success, e.g. organize.errors.itemFailed. */
export function operationErrorKey(code: string | null | undefined): string | null {
    return code ? `organize.errors.${camel(code)}` : null
}

/** Problem codes of organizing and own albums that have a message of their own. */
const organizeProblems: Readonly<Record<string, string>> = {
    'organize.own_album_not_found': 'ownAlbumNotFound',
    'organize.own_album_empty': 'ownAlbumEmpty',
    'organize.track_in_other_own_album': 'trackInOtherOwnAlbum',
    'organize.own_album_too_many_tracks': 'ownAlbumTooManyTracks',
    'organize.operation_not_found': 'operationNotFound',
    'organize.media_read_only': 'mediaReadOnly',
    'organize.template_invalid': 'templateInvalid',
    'organize.busy': 'busy',
    'organize.not_undoable': 'notUndoable',
    'organize.already_undone': 'alreadyUndone',
    'organize.not_preview': 'notPreview',
    'organize.not_previewed': 'notPreviewed',
    'organize.preview_expired': 'previewExpired',
    'organize.conflicts': 'conflicts',
    'organize.preview_outdated': 'previewOutdated',
    'organize.not_cancelable': 'notCancelable',
    'track.not_found': 'trackNotFound',
    'track.no_audio': 'trackNoAudio',
    'cover.unsupported_format': 'coverType',
    'cover.too_large': 'coverSize',
    'cover.unreadable': 'coverUnreadable',
    'cover.not_found': 'coverNotFound',
}

/** Message key of a failed request of organizing, by the code of the problem; fallback otherwise. */
export function organizeProblemKey(code: string | null | undefined, fallback: string): string {
    const name = organizeProblems[code ?? '']
    return name ? `organize.problems.${name}` : fallback
}

/** "track_not_found" or "TrackNotFound" to "trackNotFound". */
function camel(code: string): string {
    const words = code.split(/[_\-.\s]+/).filter(Boolean)
    return words
        .map((word, index) =>
            index === 0
                ? word.charAt(0).toLowerCase() + word.slice(1)
                : word.charAt(0).toUpperCase() + word.slice(1),
        )
        .join('')
}

/** "RolledBack" or "rolled_back" to "rolled-back". */
function kebab(value: string): string {
    return value
        .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
        .replaceAll('_', '-')
        .toLowerCase()
}
