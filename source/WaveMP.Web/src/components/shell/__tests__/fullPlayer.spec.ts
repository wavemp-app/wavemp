import { afterEach, describe, expect, it } from 'vitest'
import { mount, type VueWrapper } from '@vue/test-utils'
import { createPinia } from 'pinia'
import AppTransport from '../AppTransport.vue'
import type { TransportStage, TransportState } from '../transport'
import { createWaveMpI18n } from '@/i18n'
import { PlayerViewModeObject } from '@/api/generated/models'

const playing: TransportState = {
    playback: 'playing',
    position: 0,
    duration: 0,
    shuffle: false,
    repeat: 'off',
    volume: 0.8,
    muted: false,
    trackId: 't1',
    title: 'Song',
    artist: 'Artist',
}

const coverStage: TransportStage = {
    mode: PlayerViewModeObject.Cover,
    view: 'cover',
    hasVideo: true,
    casting: false,
}

let wrapper: VueWrapper | undefined

function mountFullPlayer(stage: TransportStage = coverStage) {
    wrapper = mount(AppTransport, {
        props: { state: playing, expanded: true, stage },
        global: { plugins: [createWaveMpI18n('en'), createPinia()] },
        attachTo: document.body,
    })
    return wrapper
}

afterEach(() => {
    wrapper?.unmount()
    wrapper = undefined
})

describe('full-screen player', () => {
    it('opens and closes the view switch with the cover and title', async () => {
        const player = mountFullPlayer()
        const mini = player.get('[data-part="now-playing-mini"]')
        const viewSwitch = player.get('[data-part="player-view-switch"]')

        expect(mini.attributes('aria-expanded')).toBe('false')
        expect(mini.attributes('aria-controls')).toBe(viewSwitch.attributes('id'))
        expect(mini.get(':scope > [data-part="label"]').text()).toBe('Choose what the player shows')
        expect(viewSwitch.attributes('hidden')).toBeDefined()
        expect(viewSwitch.attributes('data-state')).toBe('closed')

        await mini.trigger('click')
        expect(mini.attributes('aria-expanded')).toBe('true')
        expect(viewSwitch.attributes('hidden')).toBeUndefined()
        expect(viewSwitch.attributes('data-state')).toBe('open')
        expect(player.get('[data-part="transport"]').attributes('data-state')).toBe(
            'playing expanded view-switch',
        )
        expect(player.emitted('expand')).toBeUndefined()

        await mini.trigger('click')
        expect(viewSwitch.attributes('hidden')).toBeDefined()
    })

    it('offers cover, video and visualization and chooses one', async () => {
        const player = mountFullPlayer()
        await player.get('[data-part="now-playing-mini"]').trigger('click')
        const group = player.get('[data-part="player-view-switch"] [data-part="segmented-control"]')

        expect(group.attributes('role')).toBe('radiogroup')
        expect(group.attributes('data-item')).toBe('player-view')
        const segments = group.findAll('[data-part="segment"]')
        expect(segments.map((segment) => segment.attributes('data-item'))).toEqual([
            'cover',
            'video',
            'visualization',
        ])
        expect(segments[0]?.attributes('aria-checked')).toBe('true')

        await segments[1]?.trigger('click')
        expect(player.emitted('selectView')).toEqual([[PlayerViewModeObject.Video]])
    })

    it('disables the video without one and the visualization while casting', () => {
        const player = mountFullPlayer({ ...coverStage, hasVideo: false, casting: true })
        const segments = player.findAll('[data-part="player-view-switch"] [data-part="segment"]')

        expect(segments[0]?.attributes('disabled')).toBeUndefined()
        expect(segments[1]?.attributes('disabled')).toBeDefined()
        expect(segments[2]?.attributes('disabled')).toBeDefined()
    })

    it('shows the chosen video on the cover and hands its element to the player', async () => {
        const player = mountFullPlayer({ ...coverStage, mode: PlayerViewModeObject.Video })
        await player.vm.$nextTick()
        const media = player.get('[data-part="player-media"]')

        expect(media.find('[data-part="cover"]').exists()).toBe(true)
        expect(media.find('[data-part="player-video"]').exists()).toBe(true)
        expect(media.attributes('data-state')).toBe('cover loading')
        expect(player.emitted('attachVideo')).toHaveLength(1)

        await player.setProps({
            stage: { ...coverStage, mode: PlayerViewModeObject.Video, view: 'video' },
        })
        expect(media.attributes('data-state')).toBe('video')

        await player.setProps({ expanded: false })
        expect(media.find('[data-part="player-video"]').exists()).toBe(false)
        expect(player.emitted('detachVideo')).toHaveLength(1)
        expect(media.attributes('data-state')).toBe('cover')
    })

    it('starts with a closed view switch every time it opens', async () => {
        const player = mountFullPlayer()
        await player.get('[data-part="now-playing-mini"]').trigger('click')
        await player.setProps({ expanded: false })
        await player.setProps({ expanded: true })

        expect(player.get('[data-part="player-view-switch"]').attributes('data-state')).toBe(
            'closed',
        )
    })

    it('puts title and artist in a marquee', () => {
        const player = mountFullPlayer()

        expect(player.get('[data-part="now-playing-title"] > [data-part="marquee"]').text()).toBe(
            'Song',
        )
        expect(player.get('[data-part="now-playing-artist"] > [data-part="marquee"]').text()).toBe(
            'Artist',
        )
    })

    it('has a grip to swipe it down', () => {
        const grip = mountFullPlayer().get('[data-part="transport"] > [data-part="swipe-grip"]')

        expect(grip.attributes('aria-hidden')).toBe('true')
    })
})
