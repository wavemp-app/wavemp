import { describe, expect, it } from 'vitest'
import type { MediaFile, TrackFileSelection } from '@/api/generated/models'
import {
    categoryOfField,
    categoryStateKey,
    changedSelection,
    choiceStateTokens,
    fileChoices,
    fileSelectionFilters,
    fileSelectionSorts,
    hasSelection,
    isFileSelectionFilter,
    isFileSelectionSort,
    summaryStates,
    summaryStateTokens,
} from '../fileSelections'
import { createWaveMpI18n } from '@/i18n'

const flac: MediaFile = { id: 'flac', kind: 'Audio', relativePath: 'A/Song.flac' }
const mp3: MediaFile = { id: 'mp3', kind: 'Audio', relativePath: 'A/Song.mp3' }

const selection: TrackFileSelection = {
    trackId: 'track',
    audio: {
        state: 'Selected',
        selectedFileId: 'mp3',
        selectedPath: 'A/Song.mp3',
        activeFileId: 'mp3',
        automaticFileId: 'flac',
        candidateFileIds: ['flac', 'mp3'],
    },
    video: { state: 'Automatic', candidateFileIds: [] },
    cover: {
        state: 'Missing',
        selectedFileId: 'gone',
        selectedPath: 'A/Song.png',
        activeFileId: 'jpg',
        automaticFileId: 'jpg',
        candidateFileIds: ['jpg'],
    },
}

const i18n = createWaveMpI18n('en')

function exists(key: string): boolean {
    return i18n.global.te(key, 'en')
}

describe('file selection', () => {
    it('offers every filter and sort order of the API once, each with a message', () => {
        expect(fileSelectionFilters.map((option) => option.value)).toEqual([
            'All',
            'Choices',
            'SeveralAudio',
            'SeveralVideo',
            'SeveralCovers',
            'Selected',
            'Missing',
        ])
        expect(fileSelectionSorts.map((option) => option.value)).toEqual([
            'Path',
            'Title',
            'FileCount',
        ])
        for (const option of [...fileSelectionFilters, ...fileSelectionSorts]) {
            expect(exists(option.labelKey), option.labelKey).toBe(true)
        }

        expect(isFileSelectionFilter('Missing')).toBe(true)
        expect(isFileSelectionFilter('Other')).toBe(false)
        expect(isFileSelectionSort('FileCount')).toBe(true)
        expect(isFileSelectionSort('Duration')).toBe(false)
    })

    it('lists the categories that do not use the automatic choice', () => {
        const summary = {
            audio: 'Selected' as const,
            video: null,
            cover: 'Missing' as const,
            audioCount: 2,
            videoCount: 0,
            coverCount: 1,
        }

        expect(summaryStates(summary)).toEqual([
            { category: 'audio', state: 'Selected' },
            { category: 'cover', state: 'Missing' },
        ])
        expect(summaryStateTokens(summary)).toBe('selected missing choices')
        expect(summaryStateTokens({ audio: 'Automatic', audioCount: 1 })).toBe('')
        for (const { category, state } of summaryStates(summary)) {
            expect(exists(categoryStateKey(category, state))).toBe(true)
        }
    })

    it('offers the automatic choice and every file, and checks the selected one', () => {
        const choices = fileChoices(selection.audio, [flac, mp3])

        expect(
            choices.map(({ key, kind, fileId, checked, active, best }) => ({
                key,
                kind,
                fileId,
                checked,
                active,
                best,
            })),
        ).toEqual([
            {
                key: 'automatic',
                kind: 'automatic',
                fileId: null,
                checked: false,
                active: false,
                best: false,
            },
            {
                key: 'flac',
                kind: 'file',
                fileId: 'flac',
                checked: false,
                active: false,
                best: true,
            },
            { key: 'mp3', kind: 'file', fileId: 'mp3', checked: true, active: true, best: false },
        ])
        expect(choices[0]?.file).toBe(flac)
        expect(choiceStateTokens(choices[2]!)).toBe('checked active')
        expect(choiceStateTokens(choices[1]!)).toBe('best')
    })

    it('checks the automatic choice without a selection', () => {
        const choices = fileChoices(
            {
                state: 'Automatic',
                automaticFileId: 'flac',
                activeFileId: 'flac',
                candidateFileIds: ['flac'],
            },
            [flac],
        )

        expect(choices.map((choice) => choice.checked)).toEqual([true, false])
        expect(fileChoices(undefined, [])).toHaveLength(1)
    })

    it('shows a selected file that is gone as checked option with its path', () => {
        const choices = fileChoices(selection.cover, [])

        expect(choices.at(-1)).toMatchObject({
            key: 'missing',
            kind: 'missing',
            fileId: 'gone',
            path: 'A/Song.png',
            checked: true,
        })
        expect(choices.filter((choice) => choice.checked)).toHaveLength(1)
        expect(choiceStateTokens(choices.at(-1)!)).toBe('checked missing')
    })

    it('changes one category and keeps the others, including a missing selection', () => {
        expect(changedSelection(selection, 'video', 'mkv')).toEqual({
            audioFileId: 'mp3',
            videoFileId: 'mkv',
            coverFileId: 'gone',
        })
        expect(changedSelection(selection, 'audio', null)).toEqual({
            audioFileId: null,
            videoFileId: null,
            coverFileId: 'gone',
        })
    })

    it('knows whether a song has a selection and the category of a request field', () => {
        expect(hasSelection(selection)).toBe(true)
        expect(hasSelection({ audio: { state: 'Automatic' } })).toBe(false)
        expect(hasSelection(null)).toBe(false)

        expect(categoryOfField('audioFileId')).toBe('audio')
        expect(categoryOfField('coverFileId')).toBe('cover')
        expect(categoryOfField('title')).toBeUndefined()
    })
})
