import { afterEach, describe, expect, it } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import DevicesControl from '../DevicesControl.vue'
import type { TransportDevices } from '@/components/shell/transport'
import type { DeviceEntry } from '@/devices/presentation'
import { createWaveMpI18n } from '@/i18n'

function entry(id: string, overrides: Partial<DeviceEntry> = {}): DeviceEntry {
    return {
        id,
        name: `Device ${id}`,
        icon: 'device-desktop',
        tokens: 'allowed online',
        status: 'available',
        selectable: true,
        active: false,
        current: false,
        ...overrides,
    }
}

const entries: DeviceEntry[] = [
    entry('pc', { active: true, tokens: 'active allowed online playing', status: 'playing' }),
    entry('me', {
        icon: 'device-phone',
        current: true,
        tokens: 'current allowed online',
        status: 'this-device',
    }),
    entry('tablet', {
        icon: 'device-tablet',
        selectable: false,
        tokens: 'blocked online',
        status: 'remote-control-off',
    }),
]

const remote: TransportDevices = {
    role: 'remote',
    connection: 'connected',
    entries,
    playingOn: 'Device pc',
    remoteControl: false,
    appleMobile: false,
    status: null,
    replaced: false,
}

let wrapper: VueWrapper | null = null

afterEach(() => {
    wrapper?.unmount()
    wrapper = null
})

function mountControl(devices: Partial<TransportDevices> | null = {}) {
    wrapper = mount(DevicesControl, {
        props: { devices: devices === null ? undefined : { ...remote, ...devices } },
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    return wrapper
}

/**
 * Opens or closes the popover the way the browser does: jsdom has no Popover API and keeps the
 * content of a closed popover from the focus, so the stand-in takes the attribute away while it
 * is open, and fires the toggle events.
 */
async function toggle(view: VueWrapper, newState: 'open' | 'closed') {
    const picker = view.find('[data-part="device-picker"]').element
    if (newState === 'open') {
        picker.removeAttribute('popover')
    } else {
        picker.setAttribute('popover', 'auto')
    }
    for (const type of ['beforetoggle', 'toggle']) {
        picker.dispatchEvent(Object.assign(new Event(type), { newState }))
    }
    await new Promise((resolve) => setTimeout(resolve))
}

describe('DevicesControl', () => {
    it('is missing without devices', () => {
        expect(mountControl(null).find('[data-part="devices"]').exists()).toBe(false)
    })

    it('names the device that plays while this one is its remote', () => {
        const view = mountControl()
        expect(view.find('[data-part="devices"]').attributes('data-state')).toBe('remote connected')
        const button = view.find('[data-part="btn-devices"]')
        expect(button.attributes('data-state')).toBe('remote')
        expect(button.attributes('aria-haspopup')).toBe('dialog')
        expect(button.attributes('aria-expanded')).toBe('false')
        expect(button.attributes('popovertarget')).toBe(
            view.find('[data-part="device-picker"]').attributes('id'),
        )
        expect(button.text()).toBe('Playing on Device pc. Choose a device')
        expect(button.find('[data-icon="devices"]').exists()).toBe(true)
    })

    it('calls the button "Devices" while this device plays', () => {
        const view = mountControl({ role: 'local', playingOn: null })
        expect(view.find('[data-part="btn-devices"]').text()).toBe('Devices')
        expect(view.find('[data-part="devices"]').attributes('data-state')).toBe('local connected')
    })

    it('lists the devices as a radio group with the one that plays checked', () => {
        const view = mountControl()
        const picker = view.find('[data-part="device-picker"]')
        expect(picker.attributes('role')).toBe('dialog')
        expect(picker.attributes('popover')).toBe('auto')
        expect(picker.find('[data-part="device-picker-title"]').text()).toBe('Play on')

        const list = picker.find('[data-part="device-list"]')
        expect(list.attributes('role')).toBe('radiogroup')
        expect(list.element.firstElementChild?.getAttribute('data-part')).toBe('indicator')
        expect(list.attributes('style')).toContain('--wmp-item-count: 3')
        expect(list.attributes('style')).toContain('--wmp-active-index: 0')

        const items = list.findAll('[data-part="device-item"]')
        expect(items.map((item) => item.attributes('aria-checked'))).toEqual([
            'true',
            'false',
            'false',
        ])
        expect(items.map((item) => item.attributes('aria-disabled'))).toEqual([
            undefined,
            undefined,
            'true',
        ])
        expect(items.map((item) => item.attributes('data-state'))).toEqual([
            'active allowed online playing',
            'current allowed online',
            'blocked online',
        ])
        expect(items.map((item) => item.find('[data-part="device-meta"]').text())).toEqual([
            'Playing',
            'This device',
            'Remote control off',
        ])
        expect(items[1]?.find('[data-icon="device-phone"]').exists()).toBe(true)
        // One tab stop: the device that plays.
        expect(items.map((item) => item.attributes('tabindex'))).toEqual(['0', '-1', '-1'])
    })

    it('hands playback over to a chosen device, never to a blocked or the playing one', async () => {
        const view = mountControl()
        const items = view.findAll('[data-part="device-item"]')
        await items[0]?.trigger('click')
        await items[2]?.trigger('click')
        expect(view.emitted('transfer')).toBeUndefined()

        await items[1]?.trigger('click')
        expect(view.emitted('transfer')).toEqual([['me']])
    })

    it('moves the focus with the arrow keys without choosing', async () => {
        const view = mountControl()
        await toggle(view, 'open')
        const items = view.findAll('[data-part="device-item"]')
        expect(document.activeElement).toBe(items[0]?.element)

        await items[0]?.trigger('keydown', { key: 'ArrowDown' })
        expect(document.activeElement).toBe(items[1]?.element)
        await items[1]?.trigger('keydown', { key: 'ArrowDown' })
        expect(document.activeElement).toBe(items[2]?.element)
        await items[2]?.trigger('keydown', { key: 'ArrowDown' })
        expect(document.activeElement).toBe(items[0]?.element)
        await items[0]?.trigger('keydown', { key: 'End' })
        expect(document.activeElement).toBe(items[2]?.element)
        expect(view.emitted('transfer')).toBeUndefined()
    })

    it('switches remote control of this device within the click', async () => {
        const view = mountControl()
        const toggleButton = view.find('[data-part="remote-control-toggle"]')
        expect(toggleButton.attributes('role')).toBe('switch')
        expect(toggleButton.attributes('aria-checked')).toBe('false')
        expect(toggleButton.attributes('data-state')).toBe('off')
        expect(
            toggleButton.find('[data-part="switch-track"] [data-part="switch-thumb"]').exists(),
        ).toBe(true)
        const hint = view.find('[data-part="device-hint"][data-kind="remote-control"]')
        expect(toggleButton.attributes('aria-describedby')).toBe(hint.attributes('id'))
        expect(hint.text()).toContain('until you reload this page')

        await toggleButton.trigger('click')
        expect(view.emitted('remoteControl')).toEqual([[true]])

        await view.setProps({ devices: { ...remote, remoteControl: true } })
        expect(toggleButton.attributes('aria-checked')).toBe('true')
        await toggleButton.trigger('click')
        expect(view.emitted('remoteControl')).toEqual([[true], [false]])
    })

    it('tells iPhones and iPads to stay in the foreground', () => {
        expect(mountControl().find('[data-kind="apple"]').exists()).toBe(false)
        expect(mountControl({ appleMobile: true }).find('[data-kind="apple"]').text()).toContain(
            'foreground',
        )
    })

    it('says when the server is not reachable or another tab took over', () => {
        expect(mountControl({ connection: 'offline' }).find('[data-kind="offline"]').exists()).toBe(
            true,
        )
        expect(mountControl({ replaced: true }).find('[data-kind="replaced"]').exists()).toBe(true)
    })

    it('shows why a command was not accepted and forgets it when the picker closes', async () => {
        const view = mountControl({ status: 'RemoteControlNotAllowed' })
        expect(view.find('[data-part="device-picker"] [data-part="alert"]').text()).toContain(
            'Remote control is off on that device',
        )

        await toggle(view, 'open')
        expect(view.find('[data-part="devices"]').attributes('data-state')).toBe(
            'remote connected open',
        )
        expect(view.find('[data-part="device-picker"]').attributes('data-state')).toBe('open')
        expect(view.find('[data-part="btn-devices"]').attributes('aria-expanded')).toBe('true')
        // The device that plays has the focus.
        expect(document.activeElement).toBe(view.find('[data-part="device-item"]').element)

        await toggle(view, 'closed')
        expect(view.emitted('dismissStatus')).toHaveLength(1)
        expect(document.activeElement).toBe(view.find('[data-part="btn-devices"]').element)
    })
})
