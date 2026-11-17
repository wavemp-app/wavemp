import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createWaveMpI18n } from '@/i18n'
import type { AvatarDialogProfile } from '../AvatarDialog.vue'

const api = vi.hoisted(() => ({
    listAvatarPresets: vi.fn(),
    chooseAvatarPreset: vi.fn(),
    uploadAvatar: vi.fn(),
    removeAvatar: vi.fn(),
    fetchAvatar: vi.fn(),
    fetchAvatarPreset: vi.fn(),
}))

vi.mock('@/profiles/avatarApi', () => api)

const image = vi.hoisted(() => ({
    loadPicture: vi.fn(),
    releasePicture: vi.fn(),
    drawCrop: vi.fn(),
}))

vi.mock('@/profiles/avatarImage', () => image)

const covers = vi.hoisted(() => ({
    acquire: vi.fn(() => Promise.resolve('blob:preset')),
    release: vi.fn(),
}))

vi.mock('@/stores/covers', () => ({ useCoversStore: () => covers }))

const { default: AvatarDialog } = await import('../AvatarDialog.vue')

const anna: AvatarDialogProfile = {
    id: 'anna',
    name: 'Anna Bell',
    initials: 'AB',
    color: '#2e7d32',
    avatarVersion: null,
    avatarPresetId: null,
}

const picture = { url: 'blob:chosen', image: {}, width: 800, height: 400 }

async function mountDialog(profile: AvatarDialogProfile = anna) {
    const wrapper = mount(AvatarDialog, {
        props: { target: { kind: 'me' as const }, profile, open: false },
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await wrapper.setProps({ open: true })
    await flushPromises()
    return wrapper
}

function button(wrapper: Awaited<ReturnType<typeof mountDialog>>, item: string) {
    return wrapper.get(`button[data-item="${item}"]`)
}

async function chooseFile(wrapper: Awaited<ReturnType<typeof mountDialog>>, file: File) {
    const input = wrapper.get('input[type="file"]')
    Object.defineProperty(input.element, 'files', { value: [file], configurable: true })
    await input.trigger('change')
    await flushPromises()
}

describe('AvatarDialog', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        api.listAvatarPresets.mockReset().mockResolvedValue(['bubble', 'leaf', 'sun'])
        api.chooseAvatarPreset.mockReset().mockResolvedValue({})
        api.uploadAvatar.mockReset().mockResolvedValue({})
        api.removeAvatar.mockReset().mockResolvedValue(undefined)
        image.loadPicture.mockReset().mockResolvedValue(picture)
        image.releasePicture.mockReset()
        image.drawCrop.mockReset().mockResolvedValue(new Blob(['png'], { type: 'image/png' }))
        document.body.innerHTML = ''
    })

    it('keeps the editor out of the page while it is closed', async () => {
        const wrapper = mount(AvatarDialog, {
            props: { target: { kind: 'me' as const }, profile: anna, open: false },
            global: { plugins: [createWaveMpI18n('en')] },
            attachTo: document.body,
        })
        await flushPromises()
        expect(wrapper.find('[data-part="avatar-editor"]').exists()).toBe(false)
        expect(wrapper.find('[data-part="segmented-control"]').exists()).toBe(false)

        await wrapper.setProps({ open: true })
        await flushPromises()
        expect(wrapper.find('[data-part="avatar-editor"]').exists()).toBe(true)

        await wrapper.setProps({ open: false })
        await flushPromises()
        expect(wrapper.find('[data-part="avatar-editor"]').exists()).toBe(false)
        wrapper.unmount()
    })

    it('chooses a built-in avatar', async () => {
        const wrapper = await mountDialog()

        const presets = wrapper.findAll('[data-part="avatar-preset"]')
        expect(presets.map((preset) => preset.attributes('data-item'))).toEqual([
            'bubble',
            'leaf',
            'sun',
        ])
        expect(presets[0]!.get('[data-part="label"]').text()).toBe('Bubble')
        expect(wrapper.get('[data-part="avatar-presets"]').attributes('role')).toBe('radiogroup')
        expect(button(wrapper, 'save-avatar').attributes('disabled')).toBeDefined()
        expect(wrapper.find('button[data-item="remove-avatar"]').exists()).toBe(false)

        await presets[1]!.trigger('click')
        expect(presets[1]!.attributes('aria-checked')).toBe('true')
        await button(wrapper, 'save-avatar').trigger('click')
        await flushPromises()

        expect(api.chooseAvatarPreset).toHaveBeenCalledWith({ kind: 'me' }, 'leaf')
        expect(wrapper.emitted('saved')).toHaveLength(1)
        expect(wrapper.emitted('update:open')?.at(-1)).toEqual([false])
    })

    it('crops and uploads an own picture', async () => {
        const wrapper = await mountDialog()
        await wrapper.get('[data-item="avatar-mode"] [data-item="upload"]').trigger('click')

        expect(wrapper.get('[data-part="avatar-editor"]').attributes('data-state')).toBe('upload')
        expect(button(wrapper, 'save-avatar').attributes('disabled')).toBeDefined()
        await chooseFile(wrapper, new File(['x'], 'me.png', { type: 'image/png' }))

        const crop = wrapper.get('[data-part="avatar-crop"]')
        expect(crop.attributes('style')).toContain('--wmp-crop-scale: 2')
        expect(crop.attributes('style')).toContain('--wmp-crop-x: 0.5')
        expect(wrapper.findAll('[data-part="avatar-crop-image"]')[0]!.attributes('src')).toBe(
            'blob:chosen',
        )

        await button(wrapper, 'save-avatar').trigger('click')
        await flushPromises()

        expect(image.drawCrop).toHaveBeenCalledWith(picture, {
            x: 200,
            y: 0,
            side: 400,
            outputSize: 400,
        })
        expect(api.uploadAvatar).toHaveBeenCalledWith(
            { kind: 'me' },
            expect.any(Blob),
            'avatar.png',
        )
        expect(wrapper.emitted('saved')).toHaveLength(1)
        expect(image.releasePicture).toHaveBeenCalledWith(picture)
    })

    it('refuses other files before reading them', async () => {
        const wrapper = await mountDialog()
        await wrapper.get('[data-item="avatar-mode"] [data-item="upload"]').trigger('click')

        await chooseFile(wrapper, new File(['x'], 'me.gif', { type: 'image/gif' }))

        expect(wrapper.get('[data-part="alert"]').text()).toContain('JPEG, PNG or WebP')
        expect(image.loadPicture).not.toHaveBeenCalled()
    })

    it('shows the error of the server', async () => {
        api.uploadAvatar.mockRejectedValue({ code: 'avatar.unreadable' })
        const wrapper = await mountDialog()
        await wrapper.get('[data-item="avatar-mode"] [data-item="upload"]').trigger('click')
        await chooseFile(wrapper, new File(['x'], 'me.png', { type: 'image/png' }))

        await button(wrapper, 'save-avatar').trigger('click')
        await flushPromises()

        expect(wrapper.get('[data-part="alert"]').text()).toContain('damaged')
        expect(wrapper.emitted('saved')).toBeUndefined()
    })

    it('opens with the current kind of picture and removes the avatar', async () => {
        const wrapper = await mountDialog({ ...anna, avatarVersion: 'v1', avatarPresetId: null })

        expect(wrapper.get('[data-part="avatar-editor"]').attributes('data-state')).toBe('upload')
        await button(wrapper, 'remove-avatar').trigger('click')
        await flushPromises()

        expect(api.removeAvatar).toHaveBeenCalledWith({ kind: 'me' })
        expect(wrapper.emitted('saved')).toHaveLength(1)
    })

    it('keeps the current built-in avatar checked', async () => {
        const wrapper = await mountDialog({ ...anna, avatarVersion: 'v1', avatarPresetId: 'sun' })

        expect(wrapper.get('[data-part="avatar-editor"]').attributes('data-state')).toBe('preset')
        expect(
            wrapper.get('[data-part="avatar-preset"][aria-checked="true"]').attributes('data-item'),
        ).toBe('sun')
        expect(button(wrapper, 'save-avatar').attributes('disabled')).toBeDefined()
    })
})
