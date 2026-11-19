import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import AppTransport from '../AppTransport.vue'
import {
    castStateTokens,
    nextRepeatMode,
    sleepTimerStateTokens,
    transportIcons,
    transportStateTokens,
    type TransportCast,
    type TransportState,
} from '../transport'
import { createWaveMpI18n } from '@/i18n'

const idle: TransportState = {
    playback: 'idle',
    position: 0,
    duration: 0,
    shuffle: false,
    repeat: 'off',
    volume: 0.8,
    muted: false,
}

function mountTransport(state: Partial<TransportState> = {}, expanded = false, compact = false) {
    return mount(AppTransport, {
        props: { state: { ...idle, ...state }, expanded, compact },
        global: { plugins: [createWaveMpI18n('en'), createPinia()] },
    })
}

describe('transport logic', () => {
    it('cycles the repeat mode', () => {
        expect(nextRepeatMode('off')).toBe('all')
        expect(nextRepeatMode('all')).toBe('one')
        expect(nextRepeatMode('one')).toBe('off')
    })

    it('chooses the icons from the state', () => {
        expect(transportIcons({ playback: 'idle', repeat: 'off', muted: false })).toEqual({
            play: 'play',
            repeat: 'repeat',
            volume: 'volume',
        })
        expect(transportIcons({ playback: 'playing', repeat: 'one', muted: true })).toEqual({
            play: 'pause',
            repeat: 'repeat-one',
            volume: 'muted',
        })
        expect(transportIcons({ playback: 'paused', repeat: 'all', muted: false }).play).toBe(
            'play',
        )
    })

    it('builds the state tokens of the bar', () => {
        expect(transportStateTokens('idle', false)).toBe('idle')
        expect(transportStateTokens('playing', true)).toBe('playing expanded')
        expect(transportStateTokens('playing', false, true)).toBe('playing loading')
    })

    it('builds the state tokens of the sleep timer', () => {
        expect(sleepTimerStateTokens(undefined)).toBe('off')
        expect(sleepTimerStateTokens({ mode: 'duration', remaining: 60, fading: false })).toBe(
            'duration',
        )
        expect(sleepTimerStateTokens({ mode: 'endOfTrack', remaining: 0, fading: true })).toBe(
            'end-of-track fading',
        )
        expect(sleepTimerStateTokens({ mode: 'endOfQueue', remaining: 0, fading: false })).toBe(
            'end-of-queue',
        )
    })
})

describe('AppTransport', () => {
    it('disables playback without a track', () => {
        const wrapper = mountTransport()

        expect(wrapper.get('[data-part="transport"]').attributes('data-state')).toBe('idle')
        for (const part of ['btn-play', 'btn-stop', 'btn-prev', 'btn-next']) {
            expect(wrapper.get(`[data-part="${part}"]`).attributes('disabled')).toBeDefined()
        }
        expect(wrapper.get('[data-part="seek"]').attributes('data-state')).toBe('disabled')
        expect(wrapper.get('[data-part="btn-shuffle"]').attributes('disabled')).toBeUndefined()
        expect(wrapper.get('[data-part="now-playing-mini"]').attributes('data-state')).toBe('empty')
        expect(wrapper.get('[data-part="now-playing-title"]').text()).toBe('Nothing playing')
        expect(wrapper.get('[data-part="transport-message"]').attributes('data-state')).toBe(
            'empty',
        )
    })

    it('opens the mini player with the compact mode button, also without a song', async () => {
        const closed = mountTransport()
        const button = closed.get('[data-part="btn-compact"]')

        expect(button.attributes('disabled')).toBeUndefined()
        expect(button.attributes('aria-pressed')).toBe('false')
        expect(button.attributes('data-state')).toBe('off')
        expect(button.get('[data-part="label"]').text()).toBe('Mini player')
        await button.trigger('click')
        expect(closed.emitted('compact')).toHaveLength(1)

        const open = mountTransport({}, false, true).get('[data-part="btn-compact"]')
        expect(open.attributes('aria-pressed')).toBe('true')
        expect(open.attributes('data-state')).toBe('on')
    })

    it('enables play, previous and next for a stopped track', () => {
        const wrapper = mountTransport({ trackId: 't1', title: 'Song' })

        for (const part of ['btn-play', 'btn-prev', 'btn-next']) {
            expect(wrapper.get(`[data-part="${part}"]`).attributes('disabled')).toBeUndefined()
        }
        expect(wrapper.get('[data-part="btn-stop"]').attributes('disabled')).toBeDefined()
    })

    it('shows loading and the notice of the player', () => {
        const wrapper = mountTransport({
            trackId: 't1',
            playback: 'playing',
            loading: true,
            message: 'unsupported',
        })
        const message = wrapper.get('[data-part="transport-message"]')

        expect(wrapper.get('[data-part="transport"]').attributes('data-state')).toBe(
            'playing loading',
        )
        expect(message.attributes('role')).toBe('status')
        expect(message.attributes('data-state')).toBe('unsupported')
        expect(message.text()).toContain('cannot be played')
    })

    it('shows no notice to continue from another device by default', () => {
        expect(mountTransport().find('[data-part="resume-notice"]').exists()).toBe(false)
    })

    it('offers to continue the state of another device', async () => {
        const wrapper = mount(AppTransport, {
            props: {
                state: { ...idle, trackId: 't1', title: 'Song', playback: 'paused' },
                resume: { device: 'Firefox · Windows' },
            },
            global: { plugins: [createWaveMpI18n('en'), createPinia()] },
        })
        const notice = wrapper.get('[data-part="resume-notice"]')

        expect(notice.attributes('role')).toBe('status')
        expect(notice.get('[data-part="icon"]').attributes('data-icon')).toBe('resume-device')
        expect(notice.get(':scope > [data-part="label"]').text()).toBe(
            'Continue listening from Firefox · Windows',
        )
        expect(notice.get('[data-part="resume-device"]').text()).toBe('Firefox · Windows')

        await notice.get('[data-part="btn-resume"]').trigger('click')
        await notice.get('[data-part="btn-dismiss-resume"]').trigger('click')

        expect(wrapper.emitted('resume')).toHaveLength(1)
        expect(wrapper.emitted('dismissResume')).toHaveLength(1)
        expect(notice.get('[data-part="btn-resume"] [data-part="label"]').text()).toBe('Continue')
        expect(notice.get('[data-part="btn-dismiss-resume"]').attributes('title')).toBe('Dismiss')
    })

    it('shows the pause icon while playing', () => {
        const wrapper = mountTransport({
            playback: 'playing',
            trackId: 't1',
            title: 'Song',
            artist: 'Artist',
            duration: 200,
            position: 65,
        })
        const play = wrapper.get('[data-part="btn-play"]')

        expect(wrapper.get('[data-part="transport"]').attributes('data-state')).toContain('playing')
        expect(play.attributes('data-state')).toBe('playing')
        expect(play.attributes('disabled')).toBeUndefined()
        expect(play.get('[data-part="icon"]').attributes('data-icon')).toBe('pause')
        expect(play.get('[data-part="label"]').text()).toBe('Pause')
        expect(wrapper.get('[data-part="now-playing-artist"]').text()).toBe('Artist')
        expect(
            wrapper
                .get('[data-part="seek"] [data-part="slider-thumb"]')
                .attributes('aria-valuetext'),
        ).toBe('1:05 of 3:20')
    })

    it('shows repeat one and shuffle as pressed toggles', () => {
        const wrapper = mountTransport({ repeat: 'one', shuffle: true })
        const repeat = wrapper.get('[data-part="btn-repeat"]')

        expect(repeat.attributes('aria-pressed')).toBe('true')
        expect(repeat.attributes('data-state')).toBe('one')
        expect(repeat.get('[data-part="icon"]').attributes('data-icon')).toBe('repeat-one')
        expect(wrapper.get('[data-part="btn-shuffle"]').attributes('aria-pressed')).toBe('true')
        expect(wrapper.get('[data-part="btn-shuffle"]').attributes('data-state')).toBe('on')
    })

    it('shows the crossfade switch between repeat and stop, pressed while it is on', async () => {
        const off = mountTransport()
        const buttons = off
            .findAll('[data-part="transport-controls"] > button')
            .map((button) => button.attributes('data-part'))
        expect(buttons.slice(0, 5)).toEqual([
            'btn-shuffle',
            'btn-repeat',
            'btn-crossfade',
            'btn-autoplay',
            'btn-stop',
        ])
        const crossfade = off.get('[data-part="btn-crossfade"]')
        expect(crossfade.attributes('aria-pressed')).toBe('false')
        expect(crossfade.attributes('data-state')).toBe('off')
        expect(crossfade.get('[data-part="icon"]').attributes('data-icon')).toBe('crossfade')
        expect(crossfade.get('[data-part="label"]').text()).toBe('Crossfade')

        await crossfade.trigger('click')
        expect(off.emitted('toggleCrossfade')).toHaveLength(1)

        const on = mountTransport({ crossfade: true }).get('[data-part="btn-crossfade"]')
        expect(on.attributes('aria-pressed')).toBe('true')
        expect(on.attributes('data-state')).toBe('on')
    })

    it('shows the autoplay switch between crossfade and stop, pressed while it is on', async () => {
        const off = mountTransport({ autoplay: false })
        const autoplay = off.get('[data-part="btn-autoplay"]')
        expect(autoplay.attributes('aria-pressed')).toBe('false')
        expect(autoplay.attributes('data-state')).toBe('off')
        expect(autoplay.get('[data-part="icon"]').attributes('data-icon')).toBe('autoplay')
        expect(autoplay.get('[data-part="label"]').text()).toBe('Autoplay')

        await autoplay.trigger('click')
        expect(off.emitted('toggleAutoplay')).toHaveLength(1)

        const on = mountTransport({ autoplay: true }).get('[data-part="btn-autoplay"]')
        expect(on.attributes('aria-pressed')).toBe('true')
        expect(on.attributes('data-state')).toBe('on')
    })

    it('shows the muted state', () => {
        const wrapper = mountTransport({ muted: true })
        const mute = wrapper.get('[data-part="btn-mute"]')

        expect(mute.attributes('aria-pressed')).toBe('true')
        expect(mute.attributes('data-state')).toBe('muted')
        expect(mute.get('[data-part="icon"]').attributes('data-icon')).toBe('muted')
        expect(wrapper.get('[data-part="volume"]').attributes('data-state')).toBe('muted')
    })

    it('marks the expanded full-screen player', () => {
        const wrapper = mountTransport({}, true)

        expect(wrapper.get('[data-part="transport"]').attributes('data-state')).toBe(
            'idle expanded',
        )
    })

    it('emits the controls', async () => {
        const wrapper = mountTransport({ trackId: 't1', playback: 'paused', duration: 100 })

        await wrapper.get('[data-part="btn-shuffle"]').trigger('click')
        await wrapper.get('[data-part="btn-repeat"]').trigger('click')
        await wrapper.get('[data-part="btn-mute"]').trigger('click')
        await wrapper.get('[data-part="volume"] [data-part="slider-thumb"]').trigger('keydown', {
            key: 'ArrowLeft',
        })
        await wrapper.get('[data-part="now-playing-mini"]').trigger('click')
        await wrapper.get('[data-part="btn-collapse"]').trigger('click')
        await wrapper.get('[data-part="btn-play"]').trigger('click')
        await wrapper.get('[data-part="btn-next"]').trigger('click')
        await wrapper.get('[data-part="btn-prev"]').trigger('click')
        await wrapper.get('[data-part="btn-stop"]').trigger('click')

        expect(wrapper.emitted('toggleShuffle')).toHaveLength(1)
        expect(wrapper.emitted('cycleRepeat')).toHaveLength(1)
        expect(wrapper.emitted('toggleMute')).toHaveLength(1)
        expect(wrapper.emitted('changeVolume')?.[0]).toEqual([0.75])
        // jsdom has no matchMedia, so the transport behaves like on mobile.
        expect(wrapper.emitted('expand')).toHaveLength(1)
        expect(wrapper.emitted('collapse')).toHaveLength(1)
        expect(wrapper.emitted('play')).toHaveLength(1)
        expect(wrapper.emitted('next')).toHaveLength(1)
        expect(wrapper.emitted('previous')).toHaveLength(1)
        expect(wrapper.emitted('stop')).toHaveLength(1)
    })
})

describe('AppTransport equalizer and sleep timer', () => {
    it('opens the equalizer and shows whether it is on', async () => {
        const wrapper = mountTransport({ equalizer: true })

        const button = wrapper.get('[data-part="btn-equalizer"]')
        expect(button.attributes('data-state')).toBe('on')
        expect(button.find('[data-part="icon"]').attributes('data-icon')).toBe('equalizer')
        await button.trigger('click')

        expect(wrapper.emitted('openEqualizer')).toHaveLength(1)
        expect(mountTransport().get('[data-part="btn-equalizer"]').attributes('data-state')).toBe(
            'off',
        )
    })

    it('shows a stopped sleep timer without remaining time', () => {
        const wrapper = mountTransport()

        const timer = wrapper.get('[data-part="sleep-timer"]')
        expect(timer.attributes('data-state')).toBe('off')
        expect(timer.get('[data-part="btn-sleep-timer"]').attributes('aria-pressed')).toBe('false')
        expect(timer.get('[data-part="btn-sleep-timer"]').attributes('aria-haspopup')).toBe('menu')
        expect(timer.find('[data-part="sleep-timer-remaining"]').exists()).toBe(false)
    })

    it('shows the remaining time and the fade-out of a running timer', () => {
        const wrapper = mountTransport({
            sleepTimer: { mode: 'duration', remaining: 754, fading: true },
        })

        const timer = wrapper.get('[data-part="sleep-timer"]')
        expect(timer.attributes('data-state')).toBe('duration fading')
        expect(timer.get('[data-part="btn-sleep-timer"]').attributes('aria-pressed')).toBe('true')
        expect(timer.get('[data-part="sleep-timer-remaining"]').text()).toBe('12:34')
        expect(timer.get('[data-part="btn-sleep-timer"] [data-part="label"]').text()).toBe(
            'Sleep timer: Stops in 12:34',
        )
    })

    it('offers the choices of the sleep timer and emits the chosen one', async () => {
        const wrapper = mountTransport({
            sleepTimer: { mode: 'endOfTrack', remaining: 0, fading: false },
        })

        const items = wrapper.findAll('[data-part="sleep-timer"] [data-part="menu-item"]')
        expect(items.map((item) => item.attributes('data-item'))).toEqual([
            'sleep-off',
            'sleep-15',
            'sleep-30',
            'sleep-45',
            'sleep-60',
            'sleep-90',
            'sleep-120',
            'sleep-end-of-track',
            'sleep-end-of-queue',
        ])
        expect(
            items
                .filter((item) => item.attributes('aria-checked') === 'true')
                .map((item) => item.text()),
        ).toEqual(['At the end of the song'])
        expect(wrapper.get('[data-part="sleep-timer-remaining"]').text()).toBe('End of song')

        await wrapper.get('[data-item="sleep-30"]').trigger('click')
        await wrapper.get('[data-item="sleep-end-of-queue"]').trigger('click')
        await wrapper.get('[data-item="sleep-off"]').trigger('click')

        expect(wrapper.emitted('sleepTimer')).toEqual([
            [{ mode: 'duration', minutes: 30 }],
            [{ mode: 'endOfQueue' }],
            [{ mode: 'off' }],
        ])
    })
})

describe('AppTransport casting', () => {
    const chromecast: TransportCast = {
        availability: 'available',
        connection: 'disconnected',
        technology: 'chromecast',
        device: null,
        devicesFound: true,
    }

    it('describes the casting state as tokens', () => {
        expect(castStateTokens(undefined)).toBe('unsupported')
        expect(castStateTokens(chromecast)).toBe('chromecast available disconnected')
        expect(castStateTokens({ ...chromecast, devicesFound: false })).toBe(
            'chromecast available disconnected no-devices',
        )
        expect(
            castStateTokens({ ...chromecast, technology: 'airplay', connection: 'connected' }),
        ).toBe('airplay available connected')
    })

    it('hides the cast button in browsers that cannot cast', () => {
        expect(mountTransport().find('[data-part="cast"]').exists()).toBe(false)
        expect(
            mountTransport({
                cast: { ...chromecast, availability: 'unsupported', technology: null },
            })
                .find('[data-part="cast"]')
                .exists(),
        ).toBe(false)
    })

    it('opens the device selection with the cast button', async () => {
        const wrapper = mountTransport({ cast: chromecast })

        const cast = wrapper.get('[data-part="mode-switch"] [data-part="cast"]')
        expect(cast.attributes('data-state')).toBe('chromecast available disconnected')
        const button = cast.get('[data-part="btn-cast"]')
        expect(button.find('[data-part="icon"]').attributes('data-icon')).toBe('cast')
        expect(button.get('[data-part="label"]').text()).toBe('Cast')
        expect(button.attributes('aria-disabled')).toBeUndefined()
        await button.trigger('click')

        expect(wrapper.emitted('cast')).toHaveLength(1)
        expect(cast.find('[data-part="cast-hint"]').exists()).toBe(false)
    })

    it('greys Chromecast out without HTTPS and shows why instead', async () => {
        const wrapper = mountTransport({ cast: { ...chromecast, availability: 'insecure' } })

        const button = wrapper.get('[data-part="btn-cast"]')
        const hint = wrapper.get('[data-part="cast-hint"]')
        expect(button.attributes('aria-disabled')).toBe('true')
        expect(button.attributes('aria-describedby')).toBe(hint.attributes('id'))
        expect(button.attributes('aria-expanded')).toBe('false')
        expect(hint.attributes('data-state')).toBe('closed')
        expect(hint.text()).toContain('HTTPS')
        await button.trigger('click')

        expect(wrapper.emitted('cast')).toBeUndefined()
        expect(button.attributes('aria-expanded')).toBe('true')
        expect(hint.attributes('data-state')).toBe('open')
        expect(wrapper.get('[data-part="cast"]').attributes('data-state')).toContain('hint')
    })

    it('names the device that plays and greys out crossfade and equalizer', () => {
        const wrapper = mountTransport({
            playback: 'playing',
            title: 'A',
            artist: 'Band',
            effects: false,
            cast: { ...chromecast, connection: 'connected', device: 'Living Room' },
        })

        expect(wrapper.get('[data-part="cast-status"]').text()).toBe('Playing on Living Room')
        expect(wrapper.get('[data-part="btn-cast"]').attributes('data-state')).toBe('connected')
        expect(wrapper.get('[data-part="btn-cast"] [data-part="label"]').text()).toBe(
            'Casting to Living Room',
        )
        for (const part of ['btn-crossfade', 'btn-equalizer']) {
            const button = wrapper.get(`[data-part="${part}"]`)
            expect(button.attributes('disabled')).toBeDefined()
            expect(button.get('[data-part="label"]').text()).toContain(
                'not available while casting',
            )
        }
    })

    it('shows no device notice and keeps the effects without casting', () => {
        const wrapper = mountTransport({ playback: 'playing', title: 'A', cast: chromecast })

        expect(wrapper.find('[data-part="cast-status"]').exists()).toBe(false)
        expect(wrapper.get('[data-part="btn-equalizer"]').attributes('disabled')).toBeUndefined()
    })
})
