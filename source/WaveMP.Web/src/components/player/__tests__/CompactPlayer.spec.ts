import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import CompactPlayer from '../CompactPlayer.vue'
import type { TransportState } from '@/components/shell/transport'
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

const playing: Partial<TransportState> = {
    playback: 'playing',
    trackId: 't1',
    title: 'Song',
    artist: 'Artist',
    position: 65,
    duration: 200,
}

function mountPlayer(
    state: Partial<TransportState> = {},
    props: { kind?: 'window' | 'inline'; video?: boolean; videoReady?: boolean } = {},
) {
    return mount(CompactPlayer, {
        props: { state: { ...idle, ...state }, kind: 'inline', ...props },
        global: { plugins: [createWaveMpI18n('en'), createPinia()] },
    })
}

describe('CompactPlayer', () => {
    it('shows that nothing plays and disables the controls without a song', () => {
        const wrapper = mountPlayer()
        const root = wrapper.get('[data-part="compact-player"]')

        expect(root.attributes('data-kind')).toBe('inline')
        expect(root.attributes('data-state')).toBe('idle')
        expect(root.attributes('aria-label')).toBe('Mini player')
        expect(wrapper.get('[data-part="compact-player-title"]').text()).toBe('Nothing playing')
        expect(wrapper.get('[data-part="compact-player-info"]').attributes('data-state')).toBe(
            'empty',
        )
        expect(wrapper.get('[data-part="compact-player-seek"]').attributes('data-state')).toBe(
            'disabled',
        )
        for (const part of ['btn-prev', 'btn-play', 'btn-next']) {
            expect(wrapper.get(`[data-part="${part}"]`).attributes('disabled')).toBeDefined()
        }
        expect(
            wrapper.get('[data-part="btn-compact-close"]').attributes('disabled'),
        ).toBeUndefined()
        expect(wrapper.find('[data-part="compact-player-video"]').exists()).toBe(false)
    })

    it('shows the current song with its progress', () => {
        const wrapper = mountPlayer(playing, { kind: 'window' })

        expect(wrapper.get('[data-part="compact-player"]').attributes('data-kind')).toBe('window')
        expect(wrapper.get('[data-part="compact-player"]').attributes('data-state')).toBe('playing')
        expect(wrapper.get('[data-part="compact-player-title"]').text()).toBe('Song')
        expect(wrapper.get('[data-part="compact-player-artist"]').text()).toBe('Artist')
        expect(wrapper.get('[data-part="time-elapsed"]').text()).toBe('1:05')
        expect(wrapper.get('[data-part="time-duration"]').text()).toBe('3:20')
        expect(
            wrapper
                .get('[data-part="compact-player-seek"] [data-part="slider-thumb"]')
                .attributes('aria-valuetext'),
        ).toBe('1:05 of 3:20')
        const play = wrapper.get('[data-part="btn-play"]')
        expect(play.attributes('data-state')).toBe('playing')
        expect(play.get('[data-part="icon"]').attributes('data-icon')).toBe('pause')
    })

    it('controls the running playback', async () => {
        const wrapper = mountPlayer(playing)

        await wrapper.get('[data-part="btn-play"]').trigger('click')
        await wrapper.get('[data-part="btn-prev"]').trigger('click')
        await wrapper.get('[data-part="btn-next"]').trigger('click')
        await wrapper.get('[data-part="btn-mute"]').trigger('click')
        await wrapper.get('[data-part="btn-compact-close"]').trigger('click')
        await wrapper
            .get('[data-part="compact-player-volume"] [data-part="slider-thumb"]')
            .trigger('keydown', { key: 'ArrowLeft' })
        await wrapper
            .get('[data-part="compact-player-seek"] [data-part="slider-thumb"]')
            .trigger('keydown', { key: 'ArrowRight' })

        expect(wrapper.emitted('pause')).toHaveLength(1)
        expect(wrapper.emitted('previous')).toHaveLength(1)
        expect(wrapper.emitted('next')).toHaveLength(1)
        expect(wrapper.emitted('toggleMute')).toHaveLength(1)
        expect(wrapper.emitted('close')).toHaveLength(1)
        expect(wrapper.emitted('changeVolume')?.[0]?.[0]).toBeCloseTo(0.75)
        expect(wrapper.emitted('seek')).toEqual([[66]])

        await wrapper.setProps({ state: { ...idle, ...playing, playback: 'paused' } })
        await wrapper.get('[data-part="btn-play"]').trigger('click')
        expect(wrapper.emitted('play')).toHaveLength(1)
    })

    it('hands its video element to the player and takes it back', async () => {
        const wrapper = mountPlayer(playing, { video: true })
        await wrapper.vm.$nextTick()
        const video = wrapper.get('[data-part="compact-player-video"]').element

        expect(wrapper.emitted('attachVideo')).toEqual([[video]])
        expect(wrapper.get('[data-part="compact-player-media"]').attributes('data-state')).toBe(
            'cover',
        )

        await wrapper.setProps({ videoReady: true })
        expect(wrapper.get('[data-part="compact-player-media"]').attributes('data-state')).toBe(
            'video',
        )
        expect(wrapper.get('[data-part="compact-player"]').attributes('data-state')).toBe(
            'playing video',
        )

        await wrapper.setProps({ video: false })
        expect(wrapper.emitted('detachVideo')).toEqual([[video]])

        await wrapper.setProps({ video: true })
        const again = wrapper.get('[data-part="compact-player-video"]').element
        const events = wrapper.emitted()
        wrapper.unmount()
        expect(events.detachVideo).toEqual([[video], [again]])
    })
})
