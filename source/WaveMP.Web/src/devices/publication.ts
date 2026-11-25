// The active device publishes the state of its player to the hub (PublishPlayback). Pure functions
// for the message: the queue as a window of at most 2000 entries around the current one (the same
// window the saved playback state uses), the status, position, volume and the switches.

import type { PlaybackState, RepeatMode } from '@/components/shell/transport'
import type { Queue } from '@/player/queue'
import { apiSourceOf, repeatToApi, savedWindow, type PlayerSource } from '@/player/resume'
import {
    LivePlaybackStatusObject,
    type LivePlayback,
    type LivePlaybackInput,
    type LivePlaybackStatus,
    type LiveQueueInput,
} from './protocol'

/** Longest position or duration the hub accepts: 24 hours. */
export const maxLiveMilliseconds = 86_400_000

/** The state of the player of this device as it is published. */
export interface PlayerLiveSnapshot {
    readonly queue: Queue
    readonly repeat: RepeatMode
    /** Position and duration in seconds. */
    readonly position: number
    readonly duration: number
    readonly playback: PlaybackState
    readonly loading: boolean
    readonly volume: number
    readonly muted: boolean
    readonly crossfade: boolean
    readonly autoplay: boolean
    readonly source: PlayerSource | null
}

/** A publication: the message, the signature of its queue and where its window starts. */
export interface Publication {
    readonly input: LivePlaybackInput
    /** Compares queues, so an unchanged queue is not sent again. */
    readonly queueSignature: string
    /** Index in the queue of the player of the first published entry; commands name positions. */
    readonly windowStart: number
}

/** The live status of the player. */
export function liveStatus(playback: PlaybackState, loading: boolean): LivePlaybackStatus {
    switch (playback) {
        case 'playing':
            return loading ? LivePlaybackStatusObject.Loading : LivePlaybackStatusObject.Playing
        case 'paused':
            return LivePlaybackStatusObject.Paused
        default:
            return LivePlaybackStatusObject.Idle
    }
}

function milliseconds(seconds: number): number {
    const value = Number.isFinite(seconds) ? Math.round(seconds * 1000) : 0
    return Math.min(maxLiveMilliseconds, Math.max(0, value))
}

function unit(value: number): number {
    return Number.isFinite(value) ? Math.min(1, Math.max(0, value)) : 0
}

/** The queue of the player as it is published, and where its window starts. */
export function toLiveQueue(queue: Queue, source: PlayerSource | null): [LiveQueueInput, number] {
    const [start, end] = savedWindow(queue.items.length, queue.index)
    const items = queue.items.slice(start, end)
    const positions = new Map(items.map((item, at) => [item.key, at]))
    const order =
        queue.shuffle && queue.original
            ? queue.original.flatMap((key) => {
                  const at = positions.get(key)
                  return at === undefined ? [] : [at]
              })
            : []
    return [
        {
            items: items.map((item) => ({
                trackId: item.track.id ?? '',
                autoplay: item.autoplay === true,
            })),
            shuffle: queue.shuffle,
            originalOrder: order.length === items.length ? order : [],
            source: source ? apiSourceOf(source) : null,
        },
        start,
    ]
}

/**
 * The publication of the state of the player. The queue is only part of the message when it
 * differs from the one published last (`publishedSignature`) or when the message must name it.
 */
export function toPublication(
    snapshot: PlayerLiveSnapshot,
    publishedSignature: string | null,
    includeQueue = false,
): Publication {
    const [queue, windowStart] = toLiveQueue(snapshot.queue, snapshot.source)
    const queueSignature = JSON.stringify(queue)
    const index = snapshot.queue.index - windowStart
    const currentIndex = index >= 0 && index < queue.items.length ? index : -1
    const input: LivePlaybackInput = {
        status:
            currentIndex < 0
                ? LivePlaybackStatusObject.Idle
                : liveStatus(snapshot.playback, snapshot.loading),
        queue: includeQueue || queueSignature !== publishedSignature ? queue : null,
        currentIndex,
        positionMilliseconds: currentIndex < 0 ? 0 : milliseconds(snapshot.position),
        durationMilliseconds: currentIndex < 0 ? 0 : milliseconds(snapshot.duration),
        repeat: repeatToApi[snapshot.repeat],
        volume: unit(snapshot.volume),
        muted: snapshot.muted,
        crossfade: snapshot.crossfade,
        autoplay: snapshot.autoplay,
    }
    return { input, queueSignature, windowStart }
}

/**
 * The live playback after the hub accepted a publication of this device: the server sends the
 * change only to the other devices, so this device applies it itself.
 */
export function liveAfterPublication(
    input: LivePlaybackInput,
    queue: LiveQueueInput,
    deviceId: string,
    version: number,
    queueVersion: number,
): LivePlayback {
    return {
        version,
        queueVersion,
        activeDeviceId: deviceId,
        status: input.status,
        currentIndex: input.currentIndex,
        currentTrackId: queue.items[input.currentIndex]?.trackId ?? null,
        queueLength: queue.items.length,
        positionMilliseconds: input.positionMilliseconds,
        durationMilliseconds: input.durationMilliseconds,
        shuffle: queue.shuffle,
        repeat: input.repeat,
        source: queue.source ?? null,
        volume: input.volume,
        muted: input.muted,
        crossfade: input.crossfade,
        autoplay: input.autoplay,
        handover: null,
    }
}
