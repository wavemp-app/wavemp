import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createWaveMpI18n } from '@/i18n'

const api = vi.hoisted(() => ({
    getCoverRule: vi.fn(),
    setCoverRule: vi.fn(),
    fetchCoverRulePreview: vi.fn(),
    getAlbumCoverFallback: vi.fn(),
    setAlbumCoverFallback: vi.fn(),
}))

vi.mock('@/library/api', () => api)

const AlbumMetadataDialog = (await import('../AlbumMetadataDialog.vue')).default
const { useCoversStore } = await import('@/stores/covers')

async function mountDialog() {
    const wrapper = mount(AlbumMetadataDialog, {
        props: { albumId: 'album', open: false },
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await wrapper.setProps({ open: true })
    await flushPromises()
    return wrapper
}

type Wrapper = Awaited<ReturnType<typeof mountDialog>>

function segments(wrapper: Wrapper, field: string) {
    return wrapper.findAll(`[data-field="${field}"] [data-part="segment"]`)
}

function saveButton(wrapper: Wrapper) {
    return wrapper.get('[data-part="dialog-footer"] button[data-variant="primary"]')
}

async function save(wrapper: Wrapper) {
    await saveButton(wrapper).trigger('click')
    await flushPromises()
}

describe('AlbumMetadataDialog', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        for (const mock of Object.values(api)) {
            mock.mockReset()
        }
        api.getCoverRule.mockResolvedValue({
            rule: 'AlbumFirst',
            inheritedPriority: 'TrackFirst',
            effectivePriority: 'AlbumFirst',
        })
        api.getAlbumCoverFallback.mockResolvedValue({
            fallback: 'Inherit',
            inheritedFallback: 'ArtistPicture',
            effectiveFallback: 'ArtistPicture',
        })
    })

    it('shows what the album shows without a cover and the rule of its songs', async () => {
        const wrapper = await mountDialog()

        expect(wrapper.get('[data-part="dialog-title"]').text()).toBe('Edit album')
        expect(api.getCoverRule).toHaveBeenCalledWith('album', 'album')
        expect(api.getAlbumCoverFallback).toHaveBeenCalledWith('album')
        expect(api.fetchCoverRulePreview).not.toHaveBeenCalled()
        expect(wrapper.find('[data-part="cover-preview"]').exists()).toBe(false)
        const fallback = segments(wrapper, 'cover-fallback')
        expect(fallback.map((segment) => segment.text())).toEqual([
            'Default',
            'Artist picture',
            'No cover',
        ])
        expect(fallback[0]?.attributes('aria-checked')).toBe('true')
        expect(
            wrapper.get('[data-field="cover-fallback"] [data-part="field-hint"]').text(),
        ).toContain('Default: Artist picture')
        const rule = segments(wrapper, 'cover-rule')
        expect(rule.map((segment) => segment.attributes('data-item'))).toEqual([
            'inherit',
            'track-first',
            'album-first',
            'track-only',
        ])
        expect(rule[2]?.attributes('aria-checked')).toBe('true')
        wrapper.unmount()
    })

    it('saves only what changed and loads the covers again', async () => {
        api.setAlbumCoverFallback.mockResolvedValue({
            fallback: 'None',
            inheritedFallback: 'ArtistPicture',
            effectiveFallback: 'None',
        })
        const wrapper = await mountDialog()
        const invalidate = vi.spyOn(useCoversStore(), 'invalidate')

        await segments(wrapper, 'cover-fallback')[2]!.trigger('click')
        await save(wrapper)

        expect(api.setAlbumCoverFallback).toHaveBeenCalledWith('album', 'None')
        expect(api.setCoverRule).not.toHaveBeenCalled()
        expect(invalidate).toHaveBeenCalledTimes(1)
        expect(wrapper.emitted('saved')).toHaveLength(1)
        expect(wrapper.emitted('update:open')?.at(-1)).toEqual([false])
        wrapper.unmount()
    })

    it('removes the exception of the songs with Default', async () => {
        api.setCoverRule.mockResolvedValue({
            rule: 'Inherit',
            inheritedPriority: 'TrackFirst',
            effectivePriority: 'TrackFirst',
        })
        const wrapper = await mountDialog()

        await segments(wrapper, 'cover-rule')[0]!.trigger('click')
        await save(wrapper)

        expect(api.setCoverRule).toHaveBeenCalledWith('album', 'album', 'Inherit')
        expect(api.setAlbumCoverFallback).not.toHaveBeenCalled()
        wrapper.unmount()
    })

    it('closes without a request if nothing changed', async () => {
        const wrapper = await mountDialog()

        await save(wrapper)

        expect(api.setCoverRule).not.toHaveBeenCalled()
        expect(api.setAlbumCoverFallback).not.toHaveBeenCalled()
        expect(wrapper.emitted('saved')).toBeUndefined()
        expect(wrapper.emitted('update:open')?.at(-1)).toEqual([false])
        wrapper.unmount()
    })

    it('shows an error and keeps saving disabled when the rules cannot be loaded', async () => {
        api.getAlbumCoverFallback.mockRejectedValue(new Error('offline'))
        const wrapper = await mountDialog()

        expect(wrapper.get('[data-part="alert"]').text()).toBe(
            'The cover rule could not be loaded.',
        )
        expect(saveButton(wrapper).attributes('disabled')).toBeDefined()
        wrapper.unmount()
    })
})
