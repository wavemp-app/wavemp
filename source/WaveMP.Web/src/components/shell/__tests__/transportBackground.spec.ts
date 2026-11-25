import { describe, expect, it } from 'vitest'
import { mount } from '@vue/test-utils'
import { createPinia } from 'pinia'
import AppTransport from '../AppTransport.vue'
import { transportStateTokens, type TransportState } from '../transport'
import { createWaveMpI18n } from '@/i18n'

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
    effects: true,
}

function transportState(state: Partial<TransportState> = {}): string | undefined {
    return mount(AppTransport, {
        props: { state: { ...playing, ...state }, devices: null, stage: null, expanded: false },
        global: { plugins: [createWaveMpI18n('en'), createPinia()] },
    })
        .get('[data-part="transport"]')
        .attributes('data-state')
}

describe('transport on the plain path', () => {
    it('adds the state token plain-audio', () => {
        expect(transportStateTokens('playing', false, false, { plainAudio: true })).toBe(
            'playing plain-audio',
        )
        expect(transportStateTokens('paused', true, true, { remote: true, plainAudio: true })).toBe(
            'paused loading expanded remote plain-audio',
        )
        expect(transportStateTokens('playing', false, false, { plainAudio: false })).toBe('playing')
    })

    it('shows the token while the browser plays on the plain path only', () => {
        expect(transportState({ audioPath: 'plain' })).toBe('playing plain-audio')
        expect(transportState({ audioPath: 'plain', playback: 'paused' })).toBe(
            'paused plain-audio',
        )
        expect(transportState({ audioPath: 'effects' })).toBe('playing')
        expect(transportState()).toBe('playing')
    })
})
