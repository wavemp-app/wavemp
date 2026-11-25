import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import type { Device } from '@/api/generated/models'
import DeviceSettings from '../DeviceSettings.vue'
import KnownDeviceList from '../KnownDeviceList.vue'
import { forgetDevice, listDevices, renameDevice } from '@/devices/api'
import { createWaveMpI18n } from '@/i18n'
import { useDevicesStore } from '@/stores/devices'

vi.mock('@/devices/api', () => ({
    createHubTicket: vi.fn(),
    getHandoverState: vi.fn(),
    listDevices: vi.fn(),
    renameDevice: vi.fn(),
    forgetDevice: vi.fn(),
}))

function device(id: string, extra: Partial<Device> = {}): Device {
    return {
        id,
        name: id,
        reportedName: id,
        customName: null,
        type: 'Desktop',
        online: true,
        remoteControl: false,
        active: false,
        lastActiveAt: new Date('2026-11-20T08:00:00Z'),
        ...extra,
    }
}

let wrapper: VueWrapper | null = null

beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(listDevices).mockReset()
    vi.mocked(renameDevice).mockReset()
    vi.mocked(forgetDevice).mockReset()
})

afterEach(() => {
    wrapper?.unmount()
    wrapper = null
})

async function mountSettings(devices: Device[]) {
    vi.mocked(listDevices).mockResolvedValue({ items: devices, total: devices.length })
    wrapper = mount(DeviceSettings, {
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return wrapper
}

function rows(view: VueWrapper) {
    return view.findAll('[data-part="known-device"]')
}

async function rename(view: VueWrapper, index: number, name: string) {
    // Typing and Enter: the field commits its value once (change), the form is not sent.
    const input = rows(view)[index]!.find('input')
    input.element.value = name
    await input.trigger('input')
    await input.trigger('change')
    await rows(view)[index]!.find('form').trigger('submit')
    await flushPromises()
}

describe('DeviceSettings', () => {
    it('lists the devices of the profile with their state', async () => {
        const self = useDevicesStore().self.id
        const view = await mountSettings([
            device(self, { name: 'My PC', type: 'Desktop' }),
            device('phone', { type: 'Phone' }),
            device('tv', {
                type: 'Tv',
                online: false,
                customName: 'Kitchen TV',
                name: 'Kitchen TV',
            }),
        ])

        const items = rows(view)
        expect(items.map((item) => item.attributes('data-state'))).toEqual([
            'current online',
            'online',
            'offline renamed',
        ])
        expect(items.map((item) => item.find('input').element.value)).toEqual([
            'My PC',
            'phone',
            'Kitchen TV',
        ])
        expect(items.map((item) => item.find('[data-part="device-meta"]').text())).toEqual([
            'This device, open now',
            'Open now',
            expect.stringMatching(/^Last active /),
        ])
        expect(items[2]?.find('[data-icon="device-tv"]').exists()).toBe(true)
        // Only a closed device can be forgotten.
        expect(items.map((item) => item.find('[data-item="forget-device"]').exists())).toEqual([
            false,
            false,
            true,
        ])
    })

    it('says so when there are no devices or they cannot be loaded', async () => {
        const empty = await mountSettings([])
        expect(empty.find('[data-part="form-note"]').text()).toBe('No devices yet.')
        empty.unmount()

        vi.mocked(listDevices).mockRejectedValue(new Error('offline'))
        wrapper = mount(DeviceSettings, { global: { plugins: [createWaveMpI18n('en')] } })
        await flushPromises()
        expect(wrapper.find('[data-part="alert"]').text()).toBe('The devices could not be loaded.')

        vi.mocked(listDevices).mockResolvedValue({ items: [device('tv')], total: 1 })
        await wrapper.find('[data-item="retry"]').trigger('click')
        await flushPromises()
        expect(rows(wrapper)).toHaveLength(1)
    })

    it('renames a device and brings back its own name for an empty field', async () => {
        const view = await mountSettings([device('pc', { reportedName: 'Edge · Windows' })])
        vi.mocked(renameDevice).mockResolvedValueOnce(
            device('pc', { name: 'Desk', customName: 'Desk', reportedName: 'Edge · Windows' }),
        )

        await rename(view, 0, '  Desk ')
        expect(renameDevice).toHaveBeenLastCalledWith('pc', 'Desk')
        expect(rows(view)[0]?.attributes('data-state')).toBe('online renamed')
        expect(view.find('[data-part="alert"]').text()).toBe('The name is saved.')

        vi.mocked(renameDevice).mockResolvedValueOnce(
            device('pc', { name: 'Edge · Windows', reportedName: 'Edge · Windows' }),
        )
        await rename(view, 0, '')
        expect(renameDevice).toHaveBeenLastCalledWith('pc', 'Edge · Windows')
        expect(rows(view)[0]?.find('input').element.value).toBe('Edge · Windows')
    })

    it('saves nothing when the name did not change', async () => {
        const view = await mountSettings([device('pc')])
        await rename(view, 0, ' pc ')
        expect(renameDevice).not.toHaveBeenCalled()
        expect(rows(view)[0]?.find('input').element.value).toBe('pc')
    })

    it('shows the errors of the server in the words of the app', async () => {
        const view = await mountSettings([device('pc'), device('tv')])
        vi.mocked(renameDevice).mockRejectedValueOnce({
            code: 'request.invalid',
            responseStatusCode: 400,
            errors: [{ field: 'name', code: 'max_length', max: 100 }],
        })
        await rename(view, 0, 'x'.repeat(10))
        expect(rows(view)[0]?.find('[data-part="field-error"]').text()).toBe(
            'This text is too long (maximum length: 100).',
        )

        vi.mocked(listDevices).mockResolvedValue({ items: [device('pc')], total: 1 })
        vi.mocked(renameDevice).mockRejectedValueOnce({
            code: 'device.not_found',
            responseStatusCode: 404,
        })
        await rename(view, 1, 'Kitchen')
        expect(view.find('[data-part="alert"]').text()).toBe('This device is gone already.')
        expect(rows(view)).toHaveLength(1)
    })

    it('forgets a closed device after asking', async () => {
        const view = await mountSettings([device('tv', { online: false })])
        await rows(view)[0]!.find('[data-item="forget-device"]').trigger('click')
        const dialog = view.find('dialog[data-item="forget-device"]')
        expect(dialog.text()).toContain('Remove tv from your devices?')

        vi.mocked(forgetDevice).mockResolvedValueOnce(undefined)
        await dialog.find('[data-item="confirm-forget-device"]').trigger('click')
        await flushPromises()
        expect(forgetDevice).toHaveBeenCalledWith('tv')
        expect(view.find('[data-part="known-device"]').exists()).toBe(false)
        expect(view.find('[data-part="alert"]').text()).toBe('tv is forgotten.')
    })

    it('explains why an open device cannot be forgotten', async () => {
        const view = await mountSettings([device('tv', { online: false })])
        await rows(view)[0]!.find('[data-item="forget-device"]').trigger('click')
        vi.mocked(forgetDevice).mockRejectedValueOnce({
            code: 'device.online',
            responseStatusCode: 409,
        })
        await view.find('[data-item="confirm-forget-device"]').trigger('click')
        await flushPromises()
        expect(view.find('[data-part="alert"]').text()).toBe(
            'This device is open right now. Close WaveMP there to forget it.',
        )
    })
})

describe('KnownDeviceList', () => {
    it('keeps a name being typed until the device gets a new name', async () => {
        const view = mount(KnownDeviceList, {
            props: { devices: [device('pc'), device('tv')], currentId: 'pc' },
            global: { plugins: [createWaveMpI18n('en')] },
        })
        const inputs = () => view.findAll('input')
        await inputs()[0]!.setValue('Typing')
        await view.setProps({ devices: [device('pc'), device('tv', { name: 'Kitchen' })] })
        expect(inputs().map((input) => input.element.value)).toEqual(['Typing', 'Kitchen'])
        await view.setProps({ devices: [device('pc', { name: 'Desk' }), device('tv')] })
        expect(inputs()[0]?.element.value).toBe('Desk')
    })

    it('marks the device being saved and greys out the others', () => {
        const view = mount(KnownDeviceList, {
            props: {
                devices: [device('pc'), device('tv', { online: false })],
                currentId: 'pc',
                busy: 'pc',
                errors: { pc: 'This field is required.' },
            },
            global: { plugins: [createWaveMpI18n('en')] },
        })
        const items = view.findAll('[data-part="known-device"]')
        expect(items[0]?.attributes('data-state')).toBe('current online busy')
        expect(items[0]?.find('input').attributes('disabled')).toBeDefined()
        expect(items[0]?.find('[data-part="field-error"]').text()).toBe('This field is required.')
        expect(items[1]?.find('[data-item="forget-device"]').attributes('disabled')).toBeDefined()
    })
})
