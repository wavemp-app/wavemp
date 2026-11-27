import { describe, expect, it } from 'vitest'
import type { LibraryIssue, LibraryIssueTrack } from '@/api/generated/models'
import {
    artistSplitChanges,
    isLibraryIssueFilter,
    isLibraryIssueSort,
    issueActions,
    issueDetail,
    issueLocation,
    issueTypeLabelKey,
    issueTypeOptions,
    issueTypeToken,
    libraryIssueTypes,
    severityToken,
    suggestedArtists,
    trackContext,
    typeParameter,
} from '../libraryIssues'

const track: LibraryIssueTrack = {
    id: 'track-1',
    title: 'Song',
    artist: { id: 'band', name: 'Band' },
    album: { id: 'album', title: 'Album' },
    directoryPath: 'Band/Album',
    baseName: '01 Song',
    hasAudio: true,
    hasVideo: false,
}

function issue(values: Partial<LibraryIssue>): LibraryIssue {
    return {
        id: 'issue',
        type: 'MissingCover',
        severity: 'Problem',
        track,
        missingTags: [],
        ...values,
    }
}

describe('kinds of problem', () => {
    it('have a token and a message key each, unknown kinds a fallback', () => {
        expect(libraryIssueTypes.map(issueTypeToken)).toEqual([
            'unreadable-file',
            'missing-tags',
            'unknown-album',
            'missing-cover',
            'possible-duplicate',
            'video-without-audio',
            'audio-without-video',
            'possible-multiple-artists',
        ])
        expect(issueTypeLabelKey('PossibleDuplicate')).toBe('adminChecks.types.possibleDuplicate')
        // A newer server may know more kinds than this app.
        expect(issueTypeToken('Corrupt' as never)).toBe('unknown')
        expect(issueTypeLabelKey(undefined)).toBe('adminChecks.types.unknown')
    })

    it('are offered as filters with their numbers, kinds without problems with 0', () => {
        const options = issueTypeOptions({
            problemCount: 5,
            hintCount: 2,
            counts: [
                { type: 'MissingTags', severity: 'Problem', count: 5 },
                { type: 'AudioWithoutVideo', severity: 'Hint', count: 2 },
            ],
        })

        expect(options.map((option) => [option.item, option.count, option.severity])).toEqual([
            ['all', 7, 'all'],
            ['unreadable-file', 0, 'problem'],
            ['missing-tags', 5, 'problem'],
            ['unknown-album', 0, 'problem'],
            ['missing-cover', 0, 'problem'],
            ['possible-duplicate', 0, 'problem'],
            ['video-without-audio', 0, 'problem'],
            ['audio-without-video', 2, 'hint'],
            ['possible-multiple-artists', 0, 'problem'],
        ])
        expect(issueTypeOptions(null).every((option) => option.count === 0)).toBe(true)
    })

    it('become the type parameter, all kinds none', () => {
        expect(typeParameter('all')).toBeUndefined()
        expect(typeParameter('MissingCover')).toEqual(['MissingCover'])
        expect(isLibraryIssueFilter('all')).toBe(true)
        expect(isLibraryIssueFilter('UnknownAlbum')).toBe(true)
        expect(isLibraryIssueFilter('Unknown')).toBe(false)
        expect(isLibraryIssueSort('DetectedAt')).toBe(true)
        expect(isLibraryIssueSort('Size')).toBe(false)
    })
})

describe('a problem', () => {
    it('is a hint or a problem by its severity', () => {
        expect(severityToken({ severity: 'Hint' })).toBe('hint')
        expect(severityToken({ severity: 'Problem' })).toBe('problem')
        expect(severityToken({ severity: null })).toBe('problem')
    })

    it('is located at the file if it is unreadable, otherwise at the files of the song', () => {
        expect(issueLocation(issue({}))).toBe('Band/Album/01 Song')
        expect(
            issueLocation(
                issue({
                    type: 'UnreadableFile',
                    file: { relativePath: 'Band/Album/01 Song.flac' },
                }),
            ),
        ).toBe('Band/Album/01 Song.flac')
        expect(trackContext(track)).toEqual(['Band', 'Album'])
        expect(trackContext({ ...track, artist: null, album: null })).toEqual([])
        expect(
            trackContext({
                ...track,
                artists: [
                    { id: 'bob', name: 'Bob Sinclar', role: 'Main' },
                    { id: 'kflay', name: 'K.Flay', role: 'Featured' },
                ],
            }),
        ).toEqual(['Bob Sinclar feat. K.Flay', 'Album'])
    })

    it('suggests the split of an artist name and writes the answer as list of artists', () => {
        const duo = issue({
            type: 'PossibleMultipleArtists',
            severity: 'Hint',
            track: {
                ...track,
                artist: { id: 'duo', name: 'Simon & Garfunkel' },
                artists: [{ id: 'duo', name: 'Simon & Garfunkel', role: 'Main' }],
            },
            suggestedArtists: [
                { name: 'Simon', role: 'Main' },
                { name: 'Garfunkel', role: 'Main' },
            ],
        })

        expect(suggestedArtists(duo).map((artist) => artist.name)).toEqual(['Simon', 'Garfunkel'])
        expect(artistSplitChanges(duo, 'split')).toEqual({
            artists: [
                { name: 'Simon', role: 'Main' },
                { name: 'Garfunkel', role: 'Main' },
            ],
        })
        expect(artistSplitChanges(duo, 'keep')).toEqual({
            artists: [{ name: 'Simon & Garfunkel', role: 'Main' }],
        })
        expect(issueDetail(duo).key).toBe('adminChecks.details.possibleMultipleArtists')
        expect(issueActions(duo).map((action) => action.item)).toEqual([
            'edit-metadata',
            'show-files',
        ])
        // Only audio files are written; other kinds of problem have no suggestion.
        expect(suggestedArtists({ ...duo, track: { ...duo.track, hasAudio: false } })).toEqual([])
        expect(suggestedArtists({ ...duo, type: 'MissingCover' })).toEqual([])
    })

    it('tells what is wrong', () => {
        expect(
            issueDetail(issue({ type: 'MissingTags', missingTags: ['Title', 'Album'] })),
        ).toEqual({
            key: 'adminChecks.details.missingTags',
            tagKeys: ['reviewInbox.tags.title', 'reviewInbox.tags.album'],
        })
        expect(
            issueDetail(issue({ type: 'UnreadableFile', fileIssue: 'file.probe_failed' })).key,
        ).toBe('adminLibrary.issues.probeFailed')
        expect(issueDetail(issue({ type: 'PossibleDuplicate' })).key).toBe(
            'adminChecks.details.possibleDuplicate',
        )
        expect(issueDetail(issue({ type: 'Corrupt' as never })).key).toBe(
            'adminChecks.details.unknown',
        )
    })

    it('leads to the editor, the online search and the files of the song', () => {
        expect(issueActions(issue({ type: 'MissingTags' }))).toEqual([
            {
                item: 'edit-metadata',
                labelKey: 'adminChecks.actions.editMetadata',
                to: {
                    name: 'library-track',
                    params: { id: 'track-1' },
                    query: { edit: 'metadata' },
                },
            },
            {
                item: 'search-online',
                labelKey: 'adminChecks.actions.searchOnline',
                to: { name: 'library-track', params: { id: 'track-1' }, query: { edit: 'online' } },
            },
            {
                item: 'show-files',
                labelKey: 'adminChecks.actions.showFiles',
                to: { name: 'admin-track-files', params: { id: 'track-1' } },
            },
        ])
        expect(
            issueActions(issue({ type: 'PossibleDuplicate' })).map((action) => action.item),
        ).toEqual(['edit-metadata', 'show-files'])
        expect(
            issueActions(issue({ type: 'UnreadableFile' })).map((action) => action.item),
        ).toEqual(['show-files'])
    })

    it('of a song without audio file or of a file without song offers less', () => {
        const videoOnly = issue({
            type: 'VideoWithoutAudio',
            track: { ...track, hasAudio: false, hasVideo: true },
        })
        const noAudio = issue({ type: 'MissingCover', track: { ...track, hasAudio: false } })

        expect(issueActions(videoOnly).map((action) => action.item)).toEqual(['show-files'])
        expect(issueActions(noAudio).map((action) => action.item)).toEqual(['show-files'])
        expect(issueActions(issue({ type: 'UnreadableFile', track: null }))).toEqual([])
    })
})
