// Autoplay: when the queue has ended (and repeat is off), songs that fit the list go on playing.
// The server chooses them (POST /api/v1/me/autoplay/next) from the songs of the list; the player
// keeps the chosen songs waiting below the queue and moves the first one into the queue when it is
// due. Pure functions, so the store and its tests share the rules.

import type { AutoplayTrack, Track, TrackPlayback } from '@/api/generated/models'
import type { Queue, QueueItem } from './queue'

/** The autoplay settings of a profile. */
export interface AutoplaySettings {
    /** Whether similar songs play after the queue has ended. */
    readonly enabled: boolean
    /** Whether autoplay also chooses songs that only have a music video. */
    readonly includeVideoOnly: boolean
}

/** On, like on the server, until the settings are loaded. */
export const defaultAutoplaySettings: AutoplaySettings = { enabled: true, includeVideoOnly: true }

/** The autoplay settings from an answer of the server, or null if they are incomplete. */
export function toAutoplaySettings(value: unknown): AutoplaySettings | null {
    const settings = value as Partial<Record<keyof AutoplaySettings, unknown>> | null | undefined
    if (
        !settings ||
        typeof settings.enabled !== 'boolean' ||
        typeof settings.includeVideoOnly !== 'boolean'
    ) {
        return null
    }

    return { enabled: settings.enabled, includeVideoOnly: settings.includeVideoOnly }
}

/** A change of the autoplay settings, as the API takes it: missing or null values stay unchanged. */
export interface AutoplaySettingsChange {
    readonly enabled?: boolean | null
    readonly includeVideoOnly?: boolean | null
}

/** The settings after a change, as the server applies it. */
export function applyAutoplayChange(
    settings: AutoplaySettings,
    change: AutoplaySettingsChange,
): AutoplaySettings {
    return {
        enabled: change.enabled ?? settings.enabled,
        includeVideoOnly: change.includeVideoOnly ?? settings.includeVideoOnly,
    }
}

/** Songs requested at once. */
export const autoplayBatchSize = 10
/** Fewer waiting songs than this load more. */
export const autoplayRefillBelow = 3
/** Most songs of the list and of excluded songs a request carries, as the API allows it. */
export const autoplayMaxTracks = 500
/** Failed songs in a row after which autoplay gives up, like the queue does after all failed. */
export const autoplayMaxSkips = 5
/** A prefetched stream URL is only used while it stays valid at least this long. */
export const autoplayUrlMargin = 60_000

/** A song waiting below the queue, with the playback the server decided for it. */
export interface AutoplayEntry {
    /** Unique; the entry keeps it in the queue. */
    readonly key: string
    readonly track: Track
    readonly playback?: TrackPlayback
}

let sequence = 0

/**
 * New waiting songs from an answer; songs without id and known songs (waiting, in the queue or
 * removed by the user) are left out.
 */
export function toAutoplayEntries(
    items: readonly AutoplayTrack[],
    known: Iterable<string | null | undefined>,
): AutoplayEntry[] {
    const ids = new Set(known)
    const entries: AutoplayEntry[] = []
    for (const item of items) {
        const id = item.track?.id
        if (!item.track || !id || ids.has(id)) {
            continue
        }

        ids.add(id)
        entries.push({
            key: `a${++sequence}`,
            track: item.track,
            ...(item.playback ? { playback: item.playback } : {}),
        })
    }

    return entries
}

/** The ids of the songs of the list: the entries of the queue that autoplay did not add, the last ones. */
export function autoplayContext(queue: Queue): string[] {
    return queue.items
        .filter((item) => !item.autoplay && !!item.track.id)
        .map((item) => item.track.id!)
        .slice(-autoplayMaxTracks)
}

/** The songs of a list in a fixed order: moving or shuffling the list does not change it. */
export function autoplaySignature(trackIds: readonly string[]): string {
    return [...trackIds].sort().join(',')
}

/**
 * Songs autoplay must not choose again: those it added to the queue, those waiting and those the
 * user removed or skipped.
 */
export function autoplayExcluded(
    queue: Queue,
    waiting: readonly AutoplayEntry[],
    dismissed: Iterable<string> = [],
): string[] {
    const ids = [
        ...dismissed,
        ...queue.items.filter((item) => item.autoplay).map((item) => item.track.id),
        ...waiting.map((entry) => entry.track.id),
    ].filter((id): id is string => !!id)
    return [...new Set(ids)].slice(-autoplayMaxTracks)
}

/**
 * Puts a waiting song into the queue: at the end (when the queue has ended) or right after the
 * current entry (when it is to play now). With shuffle, it keeps that place when the order is restored.
 */
export function insertAutoplay(queue: Queue, entry: AutoplayEntry, afterCurrent = false): Queue {
    const item: QueueItem = { key: entry.key, track: entry.track, autoplay: true }
    const at = afterCurrent ? queue.index + 1 : queue.items.length
    const items = [...queue.items.slice(0, at), item, ...queue.items.slice(at)]
    let original = queue.original
    if (original) {
        const currentKey = queue.items[queue.index]?.key
        const position =
            afterCurrent && currentKey ? original.indexOf(currentKey) + 1 : original.length
        original = [...original.slice(0, position), item.key, ...original.slice(position)]
    }

    return { ...queue, items, original }
}

/** Removes the songs autoplay added after the current entry; the current one stays. */
export function withoutUpcomingAutoplay(queue: Queue): Queue {
    const removed = new Set(
        queue.items.filter((item, at) => item.autoplay && at > queue.index).map((item) => item.key),
    )
    if (removed.size === 0) {
        return queue
    }

    return {
        ...queue,
        items: queue.items.filter((item) => !removed.has(item.key)),
        original: queue.original?.filter((key) => !removed.has(key)) ?? null,
    }
}

/** Whether only songs of autoplay (or none) follow the current entry: the list has ended. */
export function isEndOfList(queue: Queue): boolean {
    return queue.items.slice(queue.index + 1).every((item) => item.autoplay)
}

/** Whether a prefetched playback can still be played: it has a stream URL that stays valid a while. */
export function isPlaybackFresh(playback: TrackPlayback | undefined, now: number): boolean {
    if (!playback?.streamUrl) {
        return false
    }

    const expiresAt = playback.expiresAt ? new Date(playback.expiresAt).getTime() : Number.NaN
    return Number.isFinite(expiresAt) && expiresAt - now > autoplayUrlMargin
}
