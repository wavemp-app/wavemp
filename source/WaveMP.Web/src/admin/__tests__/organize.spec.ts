import { describe, expect, it } from 'vitest'
import type { OrganizeAvailability, OrganizeItem } from '@/api/generated/models'
import {
    blockedReason,
    conflictKey,
    insertToken,
    isOperationActive,
    itemExtensions,
    itemRoute,
    itemStates,
    operationErrorKey,
    operationProgress,
    operationStatusKey,
    organizeProblemKey,
    presetOf,
    routeSegments,
    splitPath,
    templateErrorKey,
    templatePresets,
    templateSamples,
    warningKey,
} from '../organize'

describe('layout template', () => {
    it('knows the presets, also with spaces around them', () => {
        expect(presetOf(templatePresets.folders)).toBe('folders')
        expect(presetOf(` ${templatePresets.flat} `)).toBe('flat')
        expect(presetOf('{Artist}/{Title}')).toBe('custom')
        expect(presetOf('')).toBe('custom')
    })

    it('inserts a placeholder at the cursor and replaces the selection', () => {
        expect(insertToken('{Artist}/', 9, 9, '{Title}')).toEqual({
            text: '{Artist}/{Title}',
            cursor: 16,
        })
        expect(insertToken('{Artist} - x', 11, 12, '{Title}')).toEqual({
            text: '{Artist} - {Title}',
            cursor: 18,
        })
        expect(insertToken('{Album}', 0, 0, '{Year} ')).toEqual({
            text: '{Year} {Album}',
            cursor: 7,
        })
    })

    it('inserts at the end without a cursor and keeps the cursor within the text', () => {
        expect(insertToken('{Artist}', null, null, '/')).toEqual({ text: '{Artist}/', cursor: 9 })
        expect(insertToken('ab', 10, 20, 'c')).toEqual({ text: 'abc', cursor: 3 })
        expect(insertToken('ab', -4, 1, 'c')).toEqual({ text: 'cb', cursor: 1 })
    })

    it('translates the codes of template errors and falls back for unknown ones', () => {
        expect(templateErrorKey('unknown_placeholder')).toBe(
            'organize.templateErrors.unknownPlaceholder',
        )
        expect(templateErrorKey('missing_title')).toBe('organize.templateErrors.missingTitle')
        expect(templateErrorKey('something_new')).toBe('organize.templateErrors.invalid')
        expect(templateErrorKey(null)).toBe('organize.templateErrors.invalid')
    })

    it('builds two example songs from the messages', () => {
        const samples = templateSamples((key) => `[${key}]`)

        expect(samples).toHaveLength(2)
        expect(samples.map((sample) => sample.extension)).toEqual(['flac', 'mp3'])
        expect(samples[0]?.title).toMatch(/^\[organize\.samples\./)
    })
})

describe('routes of files', () => {
    it('splits paths at the slashes and drops empty parts', () => {
        expect(splitPath('Artist/Album//01 Song')).toEqual(['Artist', 'Album', '01 Song'])
        expect(splitPath('')).toEqual([])
        expect(splitPath(null)).toEqual([])
    })

    it('marks which folders and names change', () => {
        const route = routeSegments(['Downloads', 'Song'], ['Artist', 'Album', '01 Song'])

        expect(route.from).toEqual([
            { name: 'Downloads', kind: 'folder', state: 'changed' },
            { name: 'Song', kind: 'file', state: 'changed' },
        ])
        expect(route.to).toEqual([
            { name: 'Artist', kind: 'folder', state: 'changed' },
            { name: 'Album', kind: 'folder', state: 'new' },
            { name: '01 Song', kind: 'file', state: 'changed' },
        ])
    })

    it('keeps what stays the same', () => {
        const route = routeSegments(['Artist', 'Album', 'Song'], ['Artist', 'Album', '01 Song'])

        expect(route.to?.map((segment) => segment.state)).toEqual(['same', 'same', 'changed'])
        expect(route.from?.map((segment) => segment.state)).toEqual(['same', 'same', 'changed'])
    })

    it('marks every segment as new without an old path, and has no new path for a removal', () => {
        expect(routeSegments(null, ['Artist', 'cover.jpg'])).toEqual({
            from: null,
            to: [
                { name: 'Artist', kind: 'folder', state: 'new' },
                { name: 'cover.jpg', kind: 'file', state: 'new' },
            ],
        })
        expect(routeSegments(['Artist', 'cover.jpg'], null).to).toBeNull()
    })

    it('treats all segments as folders for folders', () => {
        const route = routeSegments(['A', 'B'], ['A', 'C'], false)

        expect(route.to).toEqual([
            { name: 'A', kind: 'folder', state: 'same' },
            { name: 'C', kind: 'folder', state: 'changed' },
        ])
    })

    it('builds the route of a song from its folders and base names', () => {
        const item: OrganizeItem = {
            kind: 'Track',
            fromDirectoryPath: 'Downloads',
            fromBaseName: 'Song',
            toDirectoryPath: 'Artist/Album',
            toBaseName: '01 Song',
            moves: [
                { fromPath: 'Downloads/Song.flac', toPath: 'Artist/Album/01 Song.flac' },
                { fromPath: 'Downloads/Song.mkv', toPath: 'Artist/Album/01 Song.mkv' },
                { fromPath: 'Downloads/Song.FLAC.lrc', toPath: 'Artist/Album/01 Song.lrc' },
            ],
        }

        expect(itemRoute(item).to?.map((segment) => segment.name)).toEqual([
            'Artist',
            'Album',
            '01 Song',
        ])
        expect(itemExtensions(item)).toEqual(['flac', 'mkv', 'lrc'])
    })

    it('builds the route of a cover from its single move', () => {
        const item: OrganizeItem = {
            kind: 'AlbumCover',
            moves: [{ toPath: 'Artist/Album/cover.jpg' }],
        }

        const route = itemRoute(item)
        expect(route.from).toBeNull()
        expect(route.to?.map((segment) => segment.name)).toEqual(['Artist', 'Album', 'cover.jpg'])
    })
})

describe('states and messages of operations', () => {
    it('gives every item its status with conflict and warning', () => {
        expect(itemStates({ status: 'RolledBack' })).toEqual(['rolled-back'])
        expect(itemStates({ status: 'Conflict', conflict: 'target_exists' })).toEqual(['conflict'])
        expect(itemStates({ status: 'Pending', warnings: ['album_merges'] })).toEqual([
            'pending',
            'warning',
        ])
    })

    it('translates conflicts, warnings, statuses and errors', () => {
        expect(conflictKey('target_exists')).toBe('organize.conflicts.targetExists')
        expect(warningKey('tags_changed_since')).toBe('organize.warnings.tagsChangedSince')
        expect(operationStatusKey('Previewed')).toBe('organize.statuses.previewed')
        expect(operationErrorKey('item_failed')).toBe('organize.errors.itemFailed')
        expect(operationErrorKey(null)).toBeNull()
    })

    it('knows which operations are still busy', () => {
        expect(
            ['Planning', 'Queued', 'Running'].every((status) => isOperationActive(status as never)),
        ).toBe(true)
        expect(isOperationActive('Previewed')).toBe(false)
        expect(isOperationActive(null)).toBe(false)
    })

    it('computes the progress of a run, and none without items', () => {
        expect(operationProgress({ status: 'Running', totalItems: 8, doneItems: 2 })).toBe(0.25)
        expect(operationProgress({ status: 'Planning', totalItems: 0 })).toBeNull()
    })

    it('translates the problems of organizing and keeps the fallback for others', () => {
        expect(organizeProblemKey('organize.media_read_only', 'x')).toBe(
            'organize.problems.mediaReadOnly',
        )
        expect(organizeProblemKey('track.no_audio', 'x')).toBe('organize.problems.trackNoAudio')
        expect(organizeProblemKey('cover.too_large', 'x')).toBe('organize.problems.coverSize')
        expect(organizeProblemKey('library.unknown', 'fallback')).toBe('fallback')
        expect(organizeProblemKey(null, 'fallback')).toBe('fallback')
    })
})

describe('availability', () => {
    const availability = (
        sources: { id: string; status: string }[],
        activeOperationId: string | null = null,
    ): OrganizeAvailability =>
        ({
            activeOperationId,
            sources: sources.map((source) => ({ sourceId: source.id, status: source.status })),
        }) as OrganizeAvailability

    it('is free while one source can be written', () => {
        expect(
            blockedReason(
                availability([
                    { id: 'a', status: 'ReadOnly' },
                    { id: 'b', status: 'Available' },
                ]),
            ),
        ).toBeNull()
        expect(blockedReason(null)).toBeNull()
    })

    it('tells why nothing can be organized', () => {
        expect(blockedReason(availability([{ id: 'a', status: 'ReadOnly' }]))).toBe('readOnly')
        expect(blockedReason(availability([{ id: 'a', status: 'Missing' }]))).toBe('missing')
        expect(blockedReason(availability([{ id: 'a', status: 'Inactive' }]))).toBe('inactive')
        expect(blockedReason(availability([]))).toBe('unavailable')
        expect(
            blockedReason(
                availability([
                    { id: 'a', status: 'Inactive' },
                    { id: 'b', status: 'ReadOnly' },
                ]),
            ),
        ).toBe('readOnly')
    })

    it('is busy while an operation runs', () => {
        expect(blockedReason(availability([{ id: 'a', status: 'Available' }], 'run'))).toBe('busy')
    })

    it('checks only the sources an operation touches', () => {
        const state = availability([
            { id: 'a', status: 'ReadOnly' },
            { id: 'b', status: 'Available' },
        ])

        expect(blockedReason(state, ['a'])).toBe('readOnly')
        expect(blockedReason(state, ['b'])).toBeNull()
        expect(blockedReason(state, ['other'])).toBeNull()
    })
})
