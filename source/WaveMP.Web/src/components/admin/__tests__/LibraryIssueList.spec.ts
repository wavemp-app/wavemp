import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { LibraryIssue } from '@/api/generated/models'
import { createWaveMpI18n } from '@/i18n'
import { createRoutes } from '@/router/routes'

const api = vi.hoisted(() => ({ updateTrackMetadata: vi.fn() }))

vi.mock('@/library/api', () => api)

const LibraryIssueList = (await import('../LibraryIssueList.vue')).default

const duo: LibraryIssue = {
    id: 'issue-duo',
    type: 'PossibleMultipleArtists',
    severity: 'Hint',
    missingTags: [],
    track: {
        id: 'song',
        title: 'The Boxer',
        artist: { id: 'duo', name: 'Simon & Garfunkel' },
        artists: [{ id: 'duo', name: 'Simon & Garfunkel', role: 'Main' }],
        directoryPath: 'Simon & Garfunkel/Bridge',
        baseName: '01 The Boxer',
        hasAudio: true,
    },
    suggestedArtists: [
        { name: 'Simon', role: 'Main' },
        { name: 'Garfunkel', role: 'Main' },
    ],
}

function mountList(items: LibraryIssue[] = [duo]) {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    return mount(LibraryIssueList, {
        props: { items, label: 'Problems' },
        global: { plugins: [createWaveMpI18n('en'), router] },
    })
}

describe('LibraryIssueList', () => {
    beforeEach(() => {
        api.updateTrackMetadata.mockReset()
    })

    it('shows the suggested split of an artist name with its answers', () => {
        const wrapper = mountList()

        const item = wrapper.get('[data-part="library-issue-item"]')
        expect(item.attributes('data-item')).toBe('possible-multiple-artists')
        expect(item.attributes('data-state')).toBe('hint')
        expect(item.get('[data-part="badge"]').text()).toBe('May have several artists')
        expect(item.get('[data-part="artist-split"]').text()).toBe(
            'Suggested artists: Simon, Garfunkel',
        )
        const actions = item.findAll('[data-part="library-issue-actions"] > *')
        expect(actions.map((action) => action.attributes('data-item'))).toEqual([
            'split-artists',
            'keep-artist',
            'edit-metadata',
            'show-files',
        ])
    })

    it.each([
        [
            'split-artists',
            [
                { name: 'Simon', role: 'Main' },
                { name: 'Garfunkel', role: 'Main' },
            ],
            'split',
        ],
        ['keep-artist', [{ name: 'Simon & Garfunkel', role: 'Main' }], 'keep'],
    ])('writes the answer %s into the file', async (button, artists, state) => {
        api.updateTrackMetadata.mockResolvedValue({})
        const wrapper = mountList()

        await wrapper.get(`[data-item="${button}"]`).trigger('click')
        await flushPromises()

        expect(api.updateTrackMetadata).toHaveBeenCalledWith('song', { artists })
        expect(wrapper.get('[data-part="artist-split"]').attributes('data-state')).toBe(state)
        expect(wrapper.get('[data-part="alert"]').attributes('data-state')).toBe('success')
        expect(wrapper.get('[data-item="split-artists"]').attributes('disabled')).toBeDefined()
        expect(wrapper.get('[data-item="keep-artist"]').attributes('disabled')).toBeDefined()
    })

    it('says when the file cannot be written and allows another try', async () => {
        api.updateTrackMetadata.mockRejectedValue({
            code: 'track.not_writable',
            responseStatusCode: 409,
        })
        const wrapper = mountList()

        await wrapper.get('[data-item="split-artists"]').trigger('click')
        await flushPromises()

        expect(wrapper.get('[data-part="alert"]').text()).toBe('The audio file cannot be written.')
        expect(wrapper.get('[data-part="alert"]').attributes('data-state')).toBe('error')
        expect(wrapper.get('[data-item="split-artists"]').attributes('disabled')).toBeUndefined()
    })

    it('offers no answer for songs without an audio file', () => {
        const wrapper = mountList([{ ...duo, track: { ...duo.track, hasAudio: false } }])

        expect(wrapper.find('[data-part="artist-split"]').exists()).toBe(false)
        expect(wrapper.find('[data-item="split-artists"]').exists()).toBe(false)
    })
})
