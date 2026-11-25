// Resuming playback: the player saves where the profile stopped listening (queue, current entry,
// position, shuffle, repeat, where the list came from) on the server, so the profile continues
// after a reload or on another device (GET/PUT /api/v1/me/playback-state). Pure functions, so the
// store and its tests share the rules.

import {
    PlaybackSourceKindObject,
    RepeatModeObject,
    type PlaybackSource,
    type PlaybackSourceKind,
    type PlaybackState,
    type RepeatMode as ApiRepeatMode,
    type UpdatePlaybackState,
} from '@/api/generated/models'
import type { RepeatMode } from '@/components/shell/transport'
import { toQueueItems, type Queue, type QueueItem } from './queue'

/** Where a list in the player came from; the same shape as the sources of the menus. */
export interface PlayerSource {
    readonly kind: 'album' | 'artist' | 'playlist' | 'smart-playlist'
    readonly id: string
}

/** Most entries the server keeps of a queue; of a longer queue the entries around the current. */
export const maxSavedEntries = 2000
/** Entries before the current one that a shortened queue keeps. */
export const savedEntriesBefore = 50
/** Seconds between two saves while the player plays. */
export const saveIntervalSeconds = 10
/** Milliseconds a change of the queue waits before it is saved, so quick changes save once. */
export const saveDelayMilliseconds = 2000
/**
 * A song saved closer than this to its end starts again at its beginning: the browser would end it
 * at once and move on to the next song.
 */
export const restartWithinSeconds = 0.5

/** The state of the player that is saved. */
export interface PlayerSnapshot {
    readonly queue: Queue
    readonly repeat: RepeatMode
    /** Position in the current track in seconds. */
    readonly position: number
    readonly source: PlayerSource | null
}

/** The saved state as the player takes it over. */
export interface RestoredState {
    readonly queue: Queue
    readonly repeat: RepeatMode
    /** Position in the current track in seconds. */
    readonly position: number
    readonly source: PlayerSource | null
}

/** The state of the player without device and time, as it is sent to the server. */
export type SavedState = Omit<UpdatePlaybackState, 'device' | 'changedAt'>

/** Repeat modes of the player as the API names them. */
export const repeatToApi: Record<RepeatMode, ApiRepeatMode> = {
    off: RepeatModeObject.Off,
    all: RepeatModeObject.All,
    one: RepeatModeObject.One,
}

const sourceToApi: Record<PlayerSource['kind'], PlaybackSourceKind> = {
    album: PlaybackSourceKindObject.Album,
    artist: PlaybackSourceKindObject.Artist,
    playlist: PlaybackSourceKindObject.Playlist,
    'smart-playlist': PlaybackSourceKindObject.SmartPlaylist,
}

/** A repeat mode of the API as the player names it; off for unknown values. */
export function repeatFromApi(value: ApiRepeatMode | null | undefined): RepeatMode {
    switch (value) {
        case RepeatModeObject.All:
            return 'all'
        case RepeatModeObject.One:
            return 'one'
        default:
            return 'off'
    }
}

/** A source of the API as the player names it; null without one or for unknown kinds. */
export function playerSourceOf(source: PlaybackSource | null | undefined): PlayerSource | null {
    const id = source?.id
    const kind = (Object.keys(sourceToApi) as PlayerSource['kind'][]).find(
        (key) => sourceToApi[key] === source?.kind,
    )
    return id && kind ? { kind, id } : null
}

/** A source of the player as the API names it. */
export function apiSourceOf(source: PlayerSource): PlaybackSource {
    return { kind: sourceToApi[source.kind], id: source.id }
}

function sourceFromApi(state: PlaybackState): PlayerSource | null {
    return playerSourceOf(state.source)
}

/**
 * The entries of the queue that are saved: all of them, or of a longer queue a window of
 * `maxSavedEntries` around the current entry with `savedEntriesBefore` entries before it.
 */
export function savedWindow(count: number, index: number, max = maxSavedEntries): [number, number] {
    if (count <= max) {
        return [0, count]
    }

    const start = Math.min(Math.max(0, index - savedEntriesBefore), count - max)
    return [start, start + max]
}

/** The state of the player as the server saves it. */
export function toSavedState(snapshot: PlayerSnapshot, max = maxSavedEntries): SavedState {
    const { queue } = snapshot
    const [start, end] = savedWindow(queue.items.length, queue.index, max)
    const items = queue.items.slice(start, end)
    const positions = new Map(items.map((item, at) => [item.key, at]))
    const originalOrder =
        queue.shuffle && queue.original
            ? queue.original.flatMap((key) => {
                  const at = positions.get(key)
                  return at === undefined ? [] : [at]
              })
            : []
    const index = queue.index >= start && queue.index < end ? queue.index - start : -1
    const state: SavedState = {
        items: items.map((item) => ({
            trackId: item.track.id ?? '',
            ...(item.autoplay ? { autoplay: true } : {}),
        })),
        currentIndex: index,
        positionMilliseconds: index < 0 ? 0 : Math.max(0, Math.round(snapshot.position * 1000)),
        shuffle: queue.shuffle,
        originalOrder: originalOrder.length === items.length ? originalOrder : [],
        repeat: repeatToApi[snapshot.repeat],
    }
    return snapshot.source
        ? { ...state, source: { kind: sourceToApi[snapshot.source.kind], id: snapshot.source.id } }
        : state
}

/**
 * Compares saved states: equal states need no new save. The position counts in whole seconds,
 * so a pause right after a save does not save again.
 */
export function savedStateSignature(state: SavedState): string {
    return JSON.stringify({
        ...state,
        positionMilliseconds: Math.round((state.positionMilliseconds ?? 0) / 1000),
    })
}

/**
 * The saved state as the player takes it over: new entries for the songs (songs without audio are
 * left out), the current entry, the order before shuffling, repeat and the position in seconds.
 */
export function fromSavedState(state: PlaybackState): RestoredState {
    const saved = state.items ?? []
    const items: QueueItem[] = []
    const newIndex = new Map<number, number>()
    saved.forEach((entry, at) => {
        const [item] = entry.track ? toQueueItems([entry.track]) : []
        if (item) {
            newIndex.set(at, items.length)
            items.push(entry.autoplay ? { ...item, autoplay: true } : item)
        }
    })

    const savedIndex = state.currentIndex ?? -1
    let index = newIndex.get(savedIndex) ?? -1
    let position = Math.max(0, (state.positionMilliseconds ?? 0) / 1000)
    if (index < 0 && savedIndex >= 0 && items.length > 0) {
        // The current song cannot play here: the next one waits at its start.
        const following = [...newIndex.entries()].find(([at]) => at > savedIndex)
        index = following ? following[1] : items.length - 1
        position = 0
    }

    const duration = (items[index]?.track.durationMilliseconds ?? 0) / 1000
    if (duration > 0 && position > duration - restartWithinSeconds) {
        position = 0
    }

    const shuffle = state.shuffle === true
    const order = (state.originalOrder ?? []).flatMap((at) => {
        const item = items[newIndex.get(at) ?? -1]
        return item ? [item.key] : []
    })
    const original = shuffle
        ? order.length === items.length
            ? order
            : items.map((item) => item.key)
        : null
    return {
        queue: { items, index: items.length > 0 ? index : -1, shuffle, original },
        repeat: repeatFromApi(state.repeat),
        position: index < 0 ? 0 : position,
        source: sourceFromApi(state),
    }
}

/**
 * Whether the saved state is one this device should take over: changed on another device and not
 * the state it already took over (or showed at start).
 */
export function isFromOtherDevice(
    state: PlaybackState,
    deviceId: string,
    known: number | null,
): boolean {
    const changedAt = state.changedAt?.getTime() ?? null
    return !!state.device?.id && state.device.id !== deviceId && changedAt !== known
}
