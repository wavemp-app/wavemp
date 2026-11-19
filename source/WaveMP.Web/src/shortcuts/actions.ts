// What the default shortcuts of the player do: play and pause, seeking, previous and next song,
// shuffle, repeat, volume and mute. They act on the player like the buttons of the transport bar,
// while casting on the device. Pure helpers plus the binding, so tests use a fake player.

import type { PlaybackState, RepeatMode } from '@/components/shell/transport'
import type { ShortcutRegistry } from './registry'

/** Seconds a seek shortcut moves the music. */
export const seekStep = 5

/** Step of the volume shortcuts (the volume goes from 0 to 1). */
export const volumeStep = 0.05

/** The part of the player store the shortcuts use. */
export interface ShortcutPlayer {
    readonly state: {
        readonly playback: PlaybackState
        readonly trackId?: string
        readonly position: number
        readonly duration: number
        readonly volume: number
        readonly muted: boolean
        readonly repeat: RepeatMode
    }
    togglePlay(): void
    seek(to: number): void
    previous(): void
    next(): void
    toggleShuffle(): void
    cycleRepeat(): void
    setVolume(value: number): void
    toggleMute(): void
}

/** The position after a seek by the given seconds, within the song; null without a length. */
export function seekTarget(position: number, duration: number, delta: number): number | null {
    if (!(duration > 0)) {
        return null
    }

    return Math.min(duration, Math.max(0, position + delta))
}

/** The volume after a step up or down, on the grid of the step and between 0 and 1. */
export function volumeTarget(volume: number, delta: number): number {
    const steps = Math.round((volume + delta) / volumeStep)
    return Math.min(1, Math.max(0, Math.round(steps * volumeStep * 100) / 100))
}

/** Binds the player shortcuts; returns the function that unbinds them. */
export function bindPlayerShortcuts(
    registry: ShortcutRegistry,
    player: ShortcutPlayer,
): () => void {
    const hasTrack = () => !!player.state.trackId

    function seekBy(delta: number): void {
        const target = seekTarget(player.state.position, player.state.duration, delta)
        if (hasTrack() && target !== null) {
            player.seek(target)
        }
    }

    const unbind = [
        registry.bind('play-pause', () => {
            if (hasTrack()) {
                player.togglePlay()
            }
        }),
        registry.bind('seek-backward', () => seekBy(-seekStep)),
        registry.bind('seek-forward', () => seekBy(seekStep)),
        registry.bind('previous', () => player.previous()),
        registry.bind('next', () => player.next()),
        registry.bind('shuffle', () => player.toggleShuffle()),
        registry.bind('repeat', () => player.cycleRepeat()),
        registry.bind('volume-up', () =>
            player.setVolume(volumeTarget(player.state.volume, volumeStep)),
        ),
        registry.bind('volume-down', () =>
            player.setVolume(volumeTarget(player.state.volume, -volumeStep)),
        ),
        registry.bind('mute', () => player.toggleMute()),
    ]
    return () => unbind.forEach((release) => release())
}
