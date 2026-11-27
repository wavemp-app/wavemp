import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import type { Job, LibraryIssue, LibraryIssueSummary } from '@/api/generated/models'

const api = vi.hoisted(() => ({
    listLibraryIssues: vi.fn(),
    getLibraryIssueSummary: vi.fn(),
    getLibraryCheckJob: vi.fn(),
    startLibraryCheck: vi.fn(),
}))

vi.mock('@/admin/libraryIssueApi', () => api)

const { default: AdminChecksView } = await import('../AdminChecksView.vue')
const { createWaveMpI18n } = await import('@/i18n')
const { createRoutes } = await import('@/router/routes')
const { searchDelay } = await import('@/composables/useSearchField')

const checkedAt = new Date(Date.UTC(2026, 10, 19, 8, 0))

const summary: LibraryIssueSummary = {
    checkedAt,
    trackCount: 12,
    problemCount: 3,
    hintCount: 1,
    counts: [
        { type: 'UnreadableFile', severity: 'Problem', count: 0 },
        { type: 'MissingTags', severity: 'Problem', count: 1 },
        { type: 'UnknownAlbum', severity: 'Problem', count: 0 },
        { type: 'MissingCover', severity: 'Problem', count: 0 },
        { type: 'PossibleDuplicate', severity: 'Problem', count: 2 },
        { type: 'VideoWithoutAudio', severity: 'Hint', count: 0 },
        { type: 'AudioWithoutVideo', severity: 'Hint', count: 1 },
    ],
}

const idle: Job = { id: 'library-check', state: 'Idle' }

const track = (id: string, directoryPath: string) => ({
    id,
    title: 'Twin',
    artist: { id: 'band', name: 'Band' },
    album: { id: 'album', title: 'Album' },
    directoryPath,
    baseName: 'Twin',
    durationMilliseconds: 125_000,
    hasAudio: true,
    hasVideo: false,
})

const duplicate: LibraryIssue = {
    id: 'issue-1',
    type: 'PossibleDuplicate',
    severity: 'Problem',
    track: track('track-1', 'Band/Album'),
    relatedTrack: track('track-2', 'Band/Live'),
    missingTags: [],
}

const missingTags: LibraryIssue = {
    id: 'issue-2',
    type: 'MissingTags',
    severity: 'Problem',
    track: { ...track('track-3', 'Loose'), title: 'Loose', album: null },
    missingTags: ['Artist', 'Album'],
}

async function mountView() {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push('/admin/checks')
    const wrapper = mount(AdminChecksView, {
        global: { plugins: [router, createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return { wrapper, router }
}

describe('AdminChecksView', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        api.listLibraryIssues
            .mockReset()
            .mockResolvedValue({ items: [duplicate, missingTags], total: 2 })
        api.getLibraryIssueSummary.mockReset().mockResolvedValue(summary)
        api.getLibraryCheckJob.mockReset().mockResolvedValue(idle)
        api.startLibraryCheck.mockReset().mockResolvedValue(undefined)
    })

    afterEach(() => {
        vi.useRealTimers()
        document.body.innerHTML = ''
    })

    it('shows the last check and the number of problems per kind', async () => {
        const { wrapper } = await mountView()

        expect(wrapper.get('[data-part="admin-nav-item"][aria-current="page"]').text()).toBe(
            'Check',
        )
        const status = wrapper.get('[data-part="library-check-status"]')
        expect(status.attributes('data-state')).toBe('idle')
        expect(status.get('[data-item="tracks"] [data-part="stat-value"]').text()).toBe('12')
        expect(status.get('[data-item="problems"] [data-part="stat-value"]').text()).toBe('3')
        expect(status.get('[data-item="hints"] [data-part="stat-value"]').text()).toBe('1')
        const segments = wrapper.findAll('[data-item="issue-type"] [data-part="segment"]')
        expect(segments.map((segment) => segment.text())).toEqual([
            'All4',
            'Unreadable0',
            'Missing tags1',
            'No album0',
            'No cover0',
            'Duplicates2',
            'Video only0',
            'Audio only1',
            'Several artists?0',
        ])
        expect(segments[1]!.get('[data-part="badge"]').attributes('data-state')).toBe(
            'count problem empty',
        )
        expect(segments[7]!.get('[data-part="badge"]').attributes('data-state')).toBe('count hint')
    })

    it('lists the problems with what is wrong and where to fix it', async () => {
        const { wrapper } = await mountView()

        expect(api.listLibraryIssues).toHaveBeenCalledWith({ offset: 0, limit: 100, sort: 'Path' })
        const [first, second] = wrapper.findAll('[data-part="library-issue-item"]')
        expect(first!.attributes('data-item')).toBe('possible-duplicate')
        expect(first!.attributes('data-state')).toBe('problem')
        expect(first!.get('[data-part="library-issue-title"]').attributes('href')).toBe(
            '/library/tracks/track-1',
        )
        expect(first!.get('[data-item="related-track"]').attributes('href')).toBe(
            '/library/tracks/track-2',
        )
        expect(
            first!.get('[data-part="library-issue-detail"] [data-part="file-path"]').text(),
        ).toBe('Band/Live/Twin')
        expect(first!.get('[data-field="related-duration"]').text()).toBe('2:05')
        expect(second!.get('[data-part="library-issue-detail"]').text()).toBe(
            'Missing tags: Artist, Album',
        )
        expect(
            second!
                .findAll('[data-part="library-issue-actions"] [data-part="link"]')
                .map((link) => [link.attributes('data-item'), link.attributes('href')]),
        ).toEqual([
            ['edit-metadata', '/library/tracks/track-3?edit=metadata'],
            ['search-online', '/library/tracks/track-3?edit=online'],
            ['show-files', '/admin/files/track-3'],
        ])
    })

    it('filters by kind, searches and sorts', async () => {
        vi.useFakeTimers()
        const { wrapper } = await mountView()

        await wrapper.get('[data-item="issue-type"] [data-item="missing-tags"]').trigger('click')
        await flushPromises()
        expect(api.listLibraryIssues).toHaveBeenLastCalledWith({
            offset: 0,
            limit: 100,
            sort: 'Path',
            type: ['MissingTags'],
        })

        await wrapper.get('[data-item="issue-sort"] [data-item="detected-at"]').trigger('click')
        await wrapper.get('[data-part="search"] input').setValue('  loose ')
        await vi.advanceTimersByTimeAsync(searchDelay)
        await flushPromises()
        expect(api.listLibraryIssues).toHaveBeenLastCalledWith({
            offset: 0,
            limit: 100,
            sort: 'DetectedAt',
            type: ['MissingTags'],
            q: 'loose',
        })
    })

    it('tells when nothing was found, nothing matches or the library was never checked', async () => {
        api.listLibraryIssues.mockResolvedValue({ items: [], total: 0 })
        const clean = await mountView()
        expect(clean.wrapper.get('[data-part="empty-state"]').attributes('data-state')).toBe(
            'clean',
        )
        expect(clean.wrapper.get('[data-part="empty-state"]').text()).toBe(
            'The last check found no problems.',
        )
        clean.wrapper.unmount()

        api.getLibraryIssueSummary.mockResolvedValue({
            ...summary,
            checkedAt: null,
            trackCount: null,
        })
        const unchecked = await mountView()
        expect(unchecked.wrapper.get('[data-part="empty-state"]').attributes('data-state')).toBe(
            'unchecked',
        )
        expect(
            unchecked.wrapper.get('[data-item="checked-at"] [data-part="stat-value"]').text(),
        ).toBe('Not yet')

        await unchecked.wrapper
            .get('[data-item="issue-type"] [data-item="unknown-album"]')
            .trigger('click')
        await flushPromises()
        expect(unchecked.wrapper.get('[data-part="empty-state"]').attributes('data-state')).toBe(
            'filtered',
        )
    })

    it('starts a check, follows it and loads the new result when it ends', async () => {
        vi.useFakeTimers()
        const { wrapper } = await mountView()
        const later = new Date(checkedAt.getTime() + 60_000)
        api.getLibraryCheckJob.mockResolvedValue({ ...idle, state: 'Running' })

        await wrapper.get('[data-item="run-check"]').trigger('click')
        await flushPromises()

        expect(api.startLibraryCheck).toHaveBeenCalledTimes(1)
        const button = wrapper.get('[data-item="run-check"]')
        expect(button.attributes('disabled')).toBeDefined()
        expect(button.text()).toBe('Checking…')
        expect(wrapper.get('[data-part="library-check-status"]').attributes('data-state')).toBe(
            'running',
        )
        expect(api.listLibraryIssues).toHaveBeenCalledTimes(1)

        api.getLibraryCheckJob.mockResolvedValue(idle)
        api.getLibraryIssueSummary.mockResolvedValue({ ...summary, checkedAt: later })
        api.listLibraryIssues.mockResolvedValue({ items: [missingTags], total: 1 })
        await vi.advanceTimersByTimeAsync(2000)
        await flushPromises()

        expect(api.listLibraryIssues).toHaveBeenCalledTimes(2)
        expect(wrapper.findAll('[data-part="library-issue-item"]')).toHaveLength(1)
        expect(wrapper.get('[data-item="run-check"]').attributes('disabled')).toBeUndefined()
    })

    it('says when a check is already running or cannot be started', async () => {
        const { wrapper } = await mountView()
        api.startLibraryCheck.mockRejectedValue(new Error('down'))

        await wrapper.get('[data-item="run-check"]').trigger('click')
        await flushPromises()

        expect(wrapper.get('[data-part="alert"]').text()).toBe(
            'The library check could not be started.',
        )
    })

    it('loads the summary again after an error', async () => {
        api.getLibraryIssueSummary.mockRejectedValueOnce(new TypeError('Failed to fetch'))
        const { wrapper } = await mountView()

        expect(wrapper.get('[data-part="alert"]').text()).toBe(
            'The result of the library check could not be loaded.',
        )
        await wrapper.get('[data-item="retry"]').trigger('click')
        await flushPromises()

        expect(wrapper.find('[data-part="alert"]').exists()).toBe(false)
        expect(wrapper.find('[data-part="library-check-status"]').exists()).toBe(true)
    })
})
