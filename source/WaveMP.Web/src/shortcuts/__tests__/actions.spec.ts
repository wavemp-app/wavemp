import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
    bindPlayerShortcuts,
    seekStep,
    seekTarget,
    volumeTarget,
    type ShortcutPlayer,
} from '../actions'
import { registerDefaultShortcuts } from '../defaults'
import { createShortcutRegistry, type ShortcutRegistry } from '../registry'

describe('seekTarget', () => {
    it('moves within the song', () => {
        expect(seekTarget(30, 200, seekStep)).toBe(35)
        expect(seekTarget(30, 200, -seekStep)).toBe(25)
        expect(seekTarget(2, 200, -seekStep)).toBe(0)
        expect(seekTarget(198, 200, seekStep)).toBe(200)
    })

    it('does not seek without a length', () => {
        expect(seekTarget(0, 0, seekStep)).toBeNull()
        expect(seekTarget(0, Number.NaN, seekStep)).toBeNull()
    })
})

describe('volumeTarget', () => {
    it('steps on the grid of 5 % between 0 and 1', () => {
        expect(volumeTarget(0.8, 0.05)).toBe(0.85)
        expect(volumeTarget(0.8, -0.05)).toBe(0.75)
        expect(volumeTarget(0.62, 0.05)).toBe(0.65)
        expect(volumeTarget(0.98, 0.05)).toBe(1)
        expect(volumeTarget(1, 0.05)).toBe(1)
        expect(volumeTarget(0.02, -0.05)).toBe(0)
    })
})

function fakePlayer(state: Partial<ShortcutPlayer['state']> = {}): ShortcutPlayer {
    return {
        state: {
            playback: 'paused',
            trackId: 'track-1',
            position: 30,
            duration: 200,
            volume: 0.5,
            muted: false,
            repeat: 'off',
            ...state,
        },
        togglePlay: vi.fn(),
        seek: vi.fn(),
        previous: vi.fn(),
        next: vi.fn(),
        toggleShuffle: vi.fn(),
        cycleRepeat: vi.fn(),
        setVolume: vi.fn(),
        toggleMute: vi.fn(),
    }
}

function press(key: string, options: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key, bubbles: true, cancelable: true, ...options })
    document.body.dispatchEvent(event)
    return event
}

describe('bindPlayerShortcuts', () => {
    let registry: ShortcutRegistry
    let unbind: (() => void) | undefined
    const listener = (event: Event) => registry.handle(event as KeyboardEvent)

    beforeEach(() => {
        registry = createShortcutRegistry(false)
        registerDefaultShortcuts(registry)
        document.addEventListener('keydown', listener)
    })

    afterEach(() => {
        unbind?.()
        document.removeEventListener('keydown', listener)
    })

    it('controls the player like the transport bar', () => {
        const player = fakePlayer()
        unbind = bindPlayerShortcuts(registry, player)

        expect(press(' ').defaultPrevented).toBe(true)
        press('ArrowLeft')
        press('ArrowRight')
        press('ArrowLeft', { ctrlKey: true })
        press('ArrowRight', { ctrlKey: true })
        press('s')
        press('r')
        press('ArrowUp')
        press('ArrowDown')
        press('m')

        expect(player.togglePlay).toHaveBeenCalledOnce()
        expect(player.seek).toHaveBeenNthCalledWith(1, 25)
        expect(player.seek).toHaveBeenNthCalledWith(2, 35)
        expect(player.previous).toHaveBeenCalledOnce()
        expect(player.next).toHaveBeenCalledOnce()
        expect(player.toggleShuffle).toHaveBeenCalledOnce()
        expect(player.cycleRepeat).toHaveBeenCalledOnce()
        expect(player.setVolume).toHaveBeenNthCalledWith(1, 0.55)
        expect(player.setVolume).toHaveBeenNthCalledWith(2, 0.45)
        expect(player.toggleMute).toHaveBeenCalledOnce()
    })

    it('seeks and changes the volume while the key is held', () => {
        const player = fakePlayer()
        unbind = bindPlayerShortcuts(registry, player)

        press('ArrowRight', { repeat: true })
        press('ArrowUp', { repeat: true })
        press(' ', { repeat: true })

        expect(player.seek).toHaveBeenCalledOnce()
        expect(player.setVolume).toHaveBeenCalledOnce()
        expect(player.togglePlay).not.toHaveBeenCalled()
    })

    it('neither plays nor seeks without a song, but still takes the keys', () => {
        const player = fakePlayer({ trackId: undefined, duration: 0, position: 0 })
        unbind = bindPlayerShortcuts(registry, player)

        expect(press(' ').defaultPrevented).toBe(true)
        press('ArrowRight')

        expect(player.togglePlay).not.toHaveBeenCalled()
        expect(player.seek).not.toHaveBeenCalled()
    })

    it('stops after unbinding', () => {
        const player = fakePlayer()
        bindPlayerShortcuts(registry, player)()

        expect(press('m').defaultPrevented).toBe(false)
        expect(player.toggleMute).not.toHaveBeenCalled()
    })
})
