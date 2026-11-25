// A remote shows the playback of the active device: the live state of the hub (PlaybackChanged) and
// its queue (GetQueue). Pure functions, so the store and its tests share the rules.

import type { PlaybackState as SavedPlaybackState, Track } from '@/api/generated/models'
import type { PlaybackState, TransportState } from '@/components/shell/transport'
import type { Queue, QueueItem } from '@/player/queue'
import { fromSavedState, playerSourceOf, repeatFromApi, type RestoredState } from '@/player/resume'
import {
    DeviceCommandTypeObject,
    LivePlaybackStatusObject,
    type DeviceCommand,
    type DeviceQueue,
    type LivePlayback,
    type LivePlaybackStatus,
} from './protocol'

/** Key of an entry of the mirrored queue: the queue version and its position, unique per version. */
export function mirrorKey(queueVersion: number, position: number): string {
    return `r${queueVersion}-${position}`
}

/** A placeholder for a song that left the library; the queue shows an unknown entry. */
function missingTrack(trackId: string): Track {
    return { id: trackId, hasAudio: true }
}

/**
 * The live queue as the player shows it: one entry per position (keys of mirrorKey), the current
 * entry and shuffle. The order before shuffling is not known to remotes.
 */
export function mirrorQueue(queue: DeviceQueue | null, currentIndex?: number): Queue {
    if (!queue) {
        return { items: [], index: -1, shuffle: false, original: null }
    }

    const items: QueueItem[] = queue.items.map((entry) => ({
        key: mirrorKey(queue.queueVersion, entry.position),
        track: entry.track ?? missingTrack(entry.trackId),
        ...(entry.autoplay ? { autoplay: true } : {}),
    }))
    const index = currentIndex ?? queue.currentIndex
    return {
        items,
        index: index >= 0 && index < items.length ? index : -1,
        shuffle: queue.shuffle,
        original: null,
    }
}

/** Position of an entry of the mirrored queue in the live queue; null for an unknown key. */
export function positionOfKey(queue: Queue, key: string): number | null {
    const at = queue.items.findIndex((item) => item.key === key)
    return at < 0 ? null : at
}

/**
 * The position of the live playback in milliseconds at now: while it plays, the time since the
 * state arrived is added, up to the end of the song.
 */
export function livePositionMilliseconds(
    live: LivePlayback,
    receivedAt: number,
    now: number,
): number {
    if (live.status !== LivePlaybackStatusObject.Playing || now <= receivedAt) {
        return live.positionMilliseconds
    }

    const position = live.positionMilliseconds + (now - receivedAt)
    return live.durationMilliseconds > 0 ? Math.min(position, live.durationMilliseconds) : position
}

/** The playback state of the transport bar for a live status. */
export function transportPlayback(status: LivePlaybackStatus): PlaybackState {
    switch (status) {
        case LivePlaybackStatusObject.Playing:
        case LivePlaybackStatusObject.Loading:
            return 'playing'
        case LivePlaybackStatusObject.Paused:
            return 'paused'
        default:
            return 'idle'
    }
}

/** What the transport bar of a remote shows of the live playback. */
export type RemoteTransportState = Pick<
    TransportState,
    | 'playback'
    | 'loading'
    | 'trackId'
    | 'hasCover'
    | 'title'
    | 'artist'
    | 'isFavorite'
    | 'rating'
    | 'position'
    | 'duration'
    | 'shuffle'
    | 'repeat'
    | 'volume'
    | 'muted'
    | 'crossfade'
    | 'autoplay'
>

/**
 * The state of the transport bar of a remote: the live playback with the position at now and the
 * song of the current entry of the mirrored queue (if it is the live one).
 */
export function remoteTransportState(
    live: LivePlayback,
    mirror: Queue,
    receivedAt: number,
    now: number,
): RemoteTransportState {
    const item = mirror.index >= 0 ? mirror.items[mirror.index] : undefined
    const track = item && item.track.id === live.currentTrackId ? item.track : undefined
    const duration =
        live.durationMilliseconds > 0
            ? live.durationMilliseconds
            : (track?.durationMilliseconds ?? 0)
    return {
        playback: transportPlayback(live.status),
        loading: live.status === LivePlaybackStatusObject.Loading,
        trackId: live.currentTrackId ?? undefined,
        hasCover: track?.hasCover ?? false,
        title: track?.title ?? undefined,
        artist: track?.artist?.name ?? undefined,
        isFavorite: track?.isFavorite ?? false,
        rating: track?.rating ?? 0,
        position: livePositionMilliseconds(live, receivedAt, now) / 1000,
        duration: Math.max(0, duration) / 1000,
        shuffle: live.shuffle,
        repeat: repeatFromApi(live.repeat),
        volume: live.volume,
        muted: live.muted,
        crossfade: live.crossfade,
        autoplay: live.autoplay,
    }
}

/**
 * The live playback as a remote shows it right after it sent a command, until the active device
 * publishes the result (which has a higher version and replaces it): play, pause, seek, volume and
 * the switches show at once. The position is measured at now. Null for commands that change
 * nothing the remote can tell beforehand.
 */
export function optimisticLive(
    live: LivePlayback,
    command: DeviceCommand,
    receivedAt: number,
    now: number,
): LivePlayback | null {
    const position = livePositionMilliseconds(live, receivedAt, now)
    switch (command.type) {
        case DeviceCommandTypeObject.Play:
            return live.currentIndex < 0
                ? null
                : {
                      ...live,
                      status: LivePlaybackStatusObject.Playing,
                      positionMilliseconds: position,
                  }
        case DeviceCommandTypeObject.Pause:
            return live.status === LivePlaybackStatusObject.Idle
                ? null
                : {
                      ...live,
                      status: LivePlaybackStatusObject.Paused,
                      positionMilliseconds: position,
                  }
        case DeviceCommandTypeObject.Seek:
            return command.positionMilliseconds === null ||
                command.positionMilliseconds === undefined
                ? null
                : { ...live, positionMilliseconds: command.positionMilliseconds }
        case DeviceCommandTypeObject.SetVolume:
            return {
                ...live,
                positionMilliseconds: position,
                volume: command.volume ?? live.volume,
                muted: command.muted ?? live.muted,
            }
        case DeviceCommandTypeObject.SetShuffle:
            return {
                ...live,
                positionMilliseconds: position,
                shuffle: command.enabled ?? live.shuffle,
            }
        case DeviceCommandTypeObject.SetRepeat:
            return {
                ...live,
                positionMilliseconds: position,
                repeat: command.repeat ?? live.repeat,
            }
        case DeviceCommandTypeObject.SetCrossfade:
            return {
                ...live,
                positionMilliseconds: position,
                crossfade: command.enabled ?? live.crossfade,
            }
        case DeviceCommandTypeObject.SetAutoplay:
            return {
                ...live,
                positionMilliseconds: position,
                autoplay: command.enabled ?? live.autoplay,
            }
        default:
            return null
    }
}

/**
 * The live playback as this device takes it over when it stops being a remote (the active device
 * closed or forbade remote control): its queue with new entries (songs that left the library are
 * left out), the current entry at the position of now, repeat and the source. The player waits
 * paused there.
 */
export function restoredFromLive(
    live: LivePlayback,
    queue: DeviceQueue | null,
    receivedAt: number,
    now: number,
): RestoredState | null {
    if (!queue || queue.items.length === 0) {
        return null
    }

    const sameQueue = queue.queueVersion === live.queueVersion
    const state: SavedPlaybackState = {
        items: queue.items.map((entry) =>
            entry.track
                ? { track: entry.track, autoplay: entry.autoplay }
                : { autoplay: entry.autoplay },
        ),
        currentIndex: sameQueue ? live.currentIndex : queue.currentIndex,
        positionMilliseconds: sameQueue ? livePositionMilliseconds(live, receivedAt, now) : 0,
        shuffle: queue.shuffle,
        originalOrder: [],
        repeat: live.repeat,
        source: queue.source,
    }
    const restored = fromSavedState(state)
    return { ...restored, source: playerSourceOf(queue.source) }
}
