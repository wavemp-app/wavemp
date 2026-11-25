import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import AppTransport from '../AppTransport.vue'
import {
    transportNeedsVideo,
    transportNeedsVisualization,
    transportStateTokens,
    transportViewChoice,
    type TransportDevices,
    type TransportStage,
    type TransportState,
} from '../transport'
import { createWaveMpI18n } from '@/i18n'
import { PlayerViewModeObject } from '@/api/generated/models'

const playing: TransportState = {
    playback: 'playing',
    trackId: 'track',
    title: 'Song',
    artist: 'Artist',
    position: 10,
    duration: 200,
    shuffle: false,
    repeat: 'off',
    volume: 0.8,
    muted: false,
    crossfade: true,
    effects: false,
}

const devices: TransportDevices = {
    role: 'remote',
    connection: 'connected',
    entries: [
        {
            id: 'pc',
            name: 'Living room PC',
            icon: 'device-desktop',
            tokens: 'active allowed online playing',
            status: 'playing',
            selectable: true,
            active: true,
            current: false,
        },
    ],
    playingOn: 'Living room PC',
    remoteControl: false,
    appleMobile: false,
    status: null,
    replaced: false,
}

function mountTransport(
    state: Partial<TransportState> = {},
    transportDevices: TransportDevices | null = devices,
    stage: TransportStage | null = null,
) {
    return mount(AppTransport, {
        props: {
            state: { ...playing, ...state },
            devices: transportDevices,
            stage,
            expanded: stage !== null,
        },
        global: { plugins: [createWaveMpI18n('en'), createPinia()] },
    })
}

describe('transport of a remote', () => {
    it('adds the state token remote', () => {
        expect(transportStateTokens('playing', false, false, { remote: true })).toBe(
            'playing remote',
        )
        expect(mountTransport().find('[data-part="transport"]').attributes('data-state')).toContain(
            'remote',
        )
        expect(
            mountTransport({ effects: true }, { ...devices, role: 'local', playingOn: null })
                .find('[data-part="transport"]')
                .attributes('data-state'),
        ).toBe('playing')
    })

    it('shows the cover instead of the video and the visualization of another device', () => {
        const stage: TransportStage = {
            mode: PlayerViewModeObject.Visualization,
            view: 'cover',
            hasVideo: true,
            casting: false,
            remote: true,
        }
        expect(transportViewChoice(stage)).toBe(PlayerViewModeObject.Cover)
        expect(transportNeedsVisualization(stage)).toBe(false)
        expect(transportNeedsVideo({ ...stage, mode: PlayerViewModeObject.Video })).toBe(false)
        expect(
            transportNeedsVideo({ ...stage, mode: PlayerViewModeObject.Video, remote: false }),
        ).toBe(true)
    })

    it('names the device that plays below the artist', () => {
        const view = mountTransport()
        expect(view.find('[data-part="now-playing-meta"] [data-part="remote-status"]').text()).toBe(
            'Playing on Living room PC',
        )
        expect(view.find('[data-part="mode-switch"] [data-part="btn-devices"]').text()).toBe(
            'Playing on Living room PC. Choose a device',
        )
    })

    it('greys out the equalizer, the sleep timer and the visualization of the device that plays', () => {
        const view = mountTransport({}, devices, {
            mode: PlayerViewModeObject.Cover,
            view: 'cover',
            hasVideo: false,
            casting: false,
            remote: true,
        })
        const equalizer = view.find('[data-part="btn-equalizer"]')
        expect(equalizer.attributes('disabled')).toBeDefined()
        expect(equalizer.text()).toBe('Equalizer (playing on another device)')
        const sleepTimer = view.find('[data-part="btn-sleep-timer"]')
        expect(sleepTimer.attributes('disabled')).toBeDefined()
        expect(sleepTimer.text()).toBe('Sleep timer (playing on another device)')
        const visualization = view.find('[data-part="segment"][data-item="visualization"]')
        expect(visualization.attributes('disabled')).toBeDefined()
        expect(visualization.text()).toContain('playing on another device')
    })

    it('keeps the crossfade of the profile and a running sleep timer switchable', () => {
        const view = mountTransport({
            sleepTimer: { mode: 'duration', remaining: 60, fading: false },
        })
        const crossfade = view.find('[data-part="btn-crossfade"]')
        expect(crossfade.attributes('disabled')).toBeUndefined()
        expect(crossfade.attributes('aria-pressed')).toBe('true')
        expect(view.find('[data-part="btn-sleep-timer"]').attributes('disabled')).toBeUndefined()
    })

    it('hands playback over and switches remote control through events', async () => {
        const view = mountTransport(
            {},
            {
                ...devices,
                entries: [
                    ...devices.entries,
                    {
                        id: 'me',
                        name: 'This phone',
                        icon: 'device-phone',
                        tokens: 'current allowed online',
                        status: 'this-device',
                        selectable: true,
                        active: false,
                        current: true,
                    },
                ],
            },
        )
        await view.findAll('[data-part="device-item"]')[1]?.trigger('click')
        await view.find('[data-part="remote-control-toggle"]').trigger('click')
        expect(view.emitted('transfer')).toEqual([['me']])
        expect(view.emitted('remoteControl')).toEqual([[true]])
    })

    it('hides the devices button without devices and while this device plays shows no status', () => {
        expect(mountTransport({ effects: true }, null).find('[data-part="devices"]').exists()).toBe(
            false,
        )
        const local = mountTransport(
            { effects: true },
            { ...devices, role: 'local', playingOn: null },
        )
        expect(local.find('[data-part="remote-status"]').exists()).toBe(false)
        expect(local.find('[data-part="btn-equalizer"]').attributes('disabled')).toBeUndefined()
    })
})
