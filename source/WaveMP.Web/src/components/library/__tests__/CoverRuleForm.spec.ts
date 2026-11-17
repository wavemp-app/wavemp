import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createWaveMpI18n } from '@/i18n'

const api = vi.hoisted(() => ({
    getCoverRule: vi.fn(),
    setCoverRule: vi.fn(),
    fetchCoverRulePreview: vi.fn(),
    fetchAlbumCover: vi.fn(),
    fetchTrackCover: vi.fn(),
}))

vi.mock('@/library/api', () => api)

const CoverRuleForm = (await import('../CoverRuleForm.vue')).default
const { useCoversStore } = await import('@/stores/covers')

async function mountForm(props: { owner?: 'album' | 'track'; available?: boolean } = {}) {
    const wrapper = mount(CoverRuleForm, {
        props: { owner: props.owner ?? 'track', id: 'song', available: props.available ?? true },
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return wrapper
}

type Wrapper = Awaited<ReturnType<typeof mountForm>>

function segments(wrapper: Wrapper) {
    return wrapper.get('[data-field="cover-rule"]').findAll('[data-part="segment"]')
}

function checked(wrapper: Wrapper): string | undefined {
    return segments(wrapper)
        .find((segment) => segment.attributes('aria-checked') === 'true')
        ?.attributes('data-item')
}

function preview(wrapper: Wrapper) {
    return wrapper.get('[data-part="cover-preview"]')
}

describe('CoverRuleForm', () => {
    let created = 0
    const createObjectURL = vi.fn(() => `blob:preview-${++created}`)
    const revokeObjectURL = vi.fn()

    beforeEach(() => {
        setActivePinia(createPinia())
        created = 0
        createObjectURL.mockClear()
        revokeObjectURL.mockClear()
        vi.stubGlobal('URL', { ...URL, createObjectURL, revokeObjectURL })
        api.getCoverRule.mockReset().mockResolvedValue({
            rule: 'Inherit',
            inheritedPriority: 'AlbumFirst',
            effectivePriority: 'AlbumFirst',
        })
        api.setCoverRule
            .mockReset()
            .mockImplementation((_owner: string, _id: string, rule: string) =>
                Promise.resolve({
                    rule,
                    inheritedPriority: 'AlbumFirst',
                    effectivePriority: rule === 'Inherit' ? 'AlbumFirst' : rule,
                }),
            )
        api.fetchCoverRulePreview.mockReset().mockResolvedValue(new Uint8Array([1, 2]).buffer)
    })

    afterEach(() => {
        vi.unstubAllGlobals()
    })

    it('shows the stored rule as segmented control with the inherited priority', async () => {
        const wrapper = await mountForm()

        expect(api.getCoverRule).toHaveBeenCalledWith('track', 'song')
        expect(segments(wrapper).map((segment) => segment.attributes('data-item'))).toEqual([
            'inherit',
            'album-first',
            'track-first',
        ])
        expect(segments(wrapper).map((segment) => segment.text())).toEqual([
            'Inherited (album cover first)',
            'Album cover',
            'Song cover',
        ])
        expect(wrapper.find('[data-field="cover-rule"] [role="radiogroup"]').exists()).toBe(true)
        expect(checked(wrapper)).toBe('inherit')
        expect(wrapper.get('[data-item="save-cover-rule"]').attributes('disabled')).toBeDefined()
        wrapper.unmount()
    })

    it('previews the cover of the chosen rule before it is saved', async () => {
        const wrapper = await mountForm()

        expect(api.fetchCoverRulePreview).toHaveBeenCalledWith('track', 'song', 'Inherit', 300)
        expect(preview(wrapper).attributes('data-state')).toBe('ready')
        expect(preview(wrapper).get('img').attributes('src')).toBe('blob:preview-1')
        expect(preview(wrapper).get('[data-part="label"]').text()).toBe('Preview')

        await segments(wrapper)[2]!.trigger('click')
        await flushPromises()

        expect(api.fetchCoverRulePreview).toHaveBeenLastCalledWith(
            'track',
            'song',
            'TrackFirst',
            300,
        )
        expect(preview(wrapper).get('img').attributes('src')).toBe('blob:preview-2')
        expect(api.setCoverRule).not.toHaveBeenCalled()

        // Every rule is loaded once while the form is open.
        await segments(wrapper)[0]!.trigger('click')
        await flushPromises()
        expect(api.fetchCoverRulePreview).toHaveBeenCalledTimes(2)
        expect(preview(wrapper).get('img').attributes('src')).toBe('blob:preview-1')

        wrapper.unmount()
        expect(revokeObjectURL).toHaveBeenCalledWith('blob:preview-1')
        expect(revokeObjectURL).toHaveBeenCalledWith('blob:preview-2')
    })

    it('shows no preview without a cover', async () => {
        const wrapper = await mountForm({ available: false })

        expect(api.fetchCoverRulePreview).not.toHaveBeenCalled()
        expect(preview(wrapper).attributes('data-state')).toBe('empty')
        expect(preview(wrapper).get('[data-part="label"]').text()).toBe('No cover')
        wrapper.unmount()
    })

    it('treats a missing preview as no cover', async () => {
        api.fetchCoverRulePreview.mockRejectedValue({ responseStatusCode: 404 })
        const wrapper = await mountForm()

        expect(preview(wrapper).attributes('data-state')).toBe('empty')
        wrapper.unmount()
    })

    it('saves the rule and loads the covers again', async () => {
        const wrapper = await mountForm({ owner: 'album' })
        const invalidate = vi.spyOn(useCoversStore(), 'invalidate')

        await segments(wrapper)[2]!.trigger('click')
        await wrapper.get('form').trigger('submit')
        await flushPromises()

        expect(api.setCoverRule).toHaveBeenCalledWith('album', 'song', 'TrackFirst')
        expect(invalidate).toHaveBeenCalledTimes(1)
        expect(checked(wrapper)).toBe('track-first')
        expect(wrapper.get('[data-part="alert"][data-state="success"]').text()).toBe(
            'Saved. The covers are updated.',
        )
        expect(wrapper.emitted('saved')?.[0]?.[0]).toMatchObject({ rule: 'TrackFirst' })
        expect(wrapper.get('[data-item="save-cover-rule"]').attributes('disabled')).toBeDefined()
        wrapper.unmount()
    })

    it('explains the rule of an album', async () => {
        const wrapper = await mountForm({ owner: 'album' })

        expect(wrapper.get('[data-part="field-hint"]').text()).toContain(
            'its songs without a rule of their own',
        )
        wrapper.unmount()
    })

    it('reports a failed save', async () => {
        api.setCoverRule.mockRejectedValue(new Error('offline'))
        const wrapper = await mountForm()
        const invalidate = vi.spyOn(useCoversStore(), 'invalidate')

        await segments(wrapper)[1]!.trigger('click')
        await wrapper.get('form').trigger('submit')
        await flushPromises()

        expect(invalidate).not.toHaveBeenCalled()
        expect(wrapper.get('[data-part="alert"][data-state="error"]').text()).toBe(
            'The cover rule could not be saved.',
        )
        wrapper.unmount()
    })

    it('reports a rule that cannot be loaded', async () => {
        api.getCoverRule.mockRejectedValue(new Error('offline'))
        const wrapper = await mountForm()

        expect(wrapper.find('form').exists()).toBe(false)
        expect(wrapper.get('[data-part="alert"][data-state="error"]').text()).toBe(
            'The cover rule could not be loaded.',
        )
        wrapper.unmount()
    })
})
