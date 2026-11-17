import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'

const covers = vi.hoisted(() => ({
    acquire:
        vi.fn<
            (kind: string, id: string, size: number, version?: string) => Promise<string | null>
        >(),
    release: vi.fn(),
}))

vi.mock('@/stores/covers', () => ({ useCoversStore: () => covers }))

const { default: ProfileAvatar } = await import('../ProfileAvatar.vue')

describe('ProfileAvatar', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        covers.acquire.mockReset().mockResolvedValue('blob:avatar')
        covers.release.mockReset()
    })

    it('shows the initials without an avatar', async () => {
        const wrapper = mount(ProfileAvatar, {
            props: { initials: 'AB', color: '#2e7d32', userId: 'anna', avatarVersion: null },
        })
        await flushPromises()

        expect(wrapper.get('[data-part="avatar-initials"]').text()).toBe('AB')
        expect(wrapper.find('[data-part="avatar-image"]').exists()).toBe(false)
        expect(wrapper.get('[data-part="avatar"]').attributes('data-state')).toBeUndefined()
        expect(covers.acquire).not.toHaveBeenCalled()
    })

    // jsdom has no layout: an avatar without a measured width loads the default size, 300.
    it('shows the picture of the avatar and frees it again', async () => {
        const wrapper = mount(ProfileAvatar, {
            props: { initials: 'AB', color: '#2e7d32', userId: 'anna', avatarVersion: 'v1' },
        })
        await flushPromises()

        const avatar = wrapper.get('[data-part="avatar"]')
        expect(avatar.attributes('data-state')).toBe('image')
        expect(wrapper.get('[data-part="avatar-image"]').attributes('src')).toBe('blob:avatar')
        expect(wrapper.find('[data-part="avatar-initials"]').exists()).toBe(false)
        expect(covers.acquire).toHaveBeenCalledWith('avatar', 'anna', 300, 'v1')

        wrapper.unmount()
        expect(covers.release).toHaveBeenCalledWith('avatar', 'anna', 300, 'v1')
    })

    it('loads the new picture when the version changes', async () => {
        const wrapper = mount(ProfileAvatar, {
            props: { initials: 'AB', color: '#2e7d32', userId: 'anna', avatarVersion: 'v1' },
        })
        await flushPromises()
        covers.acquire.mockResolvedValue('blob:second')

        await wrapper.setProps({ avatarVersion: 'v2' })
        await flushPromises()

        expect(covers.release).toHaveBeenCalledWith('avatar', 'anna', 300, 'v1')
        expect(covers.acquire).toHaveBeenLastCalledWith('avatar', 'anna', 300, 'v2')
        expect(wrapper.get('[data-part="avatar-image"]').attributes('src')).toBe('blob:second')

        await wrapper.setProps({ avatarVersion: null })
        await flushPromises()
        expect(wrapper.get('[data-part="avatar-initials"]').text()).toBe('AB')
    })

    it('keeps the initials if the picture cannot be loaded or shown', async () => {
        covers.acquire.mockResolvedValue(null)
        const missing = mount(ProfileAvatar, {
            props: { initials: 'AB', color: '#2e7d32', userId: 'anna', avatarVersion: 'v1' },
        })
        await flushPromises()
        expect(missing.find('[data-part="avatar-image"]').exists()).toBe(false)

        covers.acquire.mockResolvedValue('blob:broken')
        const broken = mount(ProfileAvatar, {
            props: { initials: 'AB', color: '#2e7d32', userId: 'ben', avatarVersion: 'v1' },
        })
        await flushPromises()
        await broken.get('[data-part="avatar-image"]').trigger('error')

        expect(broken.get('[data-part="avatar-initials"]').text()).toBe('AB')
    })
})
