// Commands between the devices: what a remote sends for an action of its user (commandFor), and
// what the active device does with a command it received (runCommand), through the same functions
// its own user uses. Pure functions, so the stores and their tests share the rules.

import type { RepeatMode as ApiRepeatMode, Track } from '@/api/generated/models'
import { nextRepeatMode, type RepeatMode } from '@/components/shell/transport'
import type { Queue } from '@/player/queue'
import {
    apiSourceOf,
    maxSavedEntries,
    playerSourceOf,
    repeatFromApi,
    repeatToApi,
    savedWindow,
    type PlayerSource,
} from '@/player/resume'
import { maxLiveMilliseconds } from './publication'
import { positionOfKey } from './remote'
import {
    DeviceCommandTypeObject,
    LivePlaybackStatusObject,
    type DeviceCommand,
    type LivePlayback,
    type ReceivedCommand,
} from './protocol'

/** An action of the user of a remote, named like the function of the player store. */
export type RemoteAction =
    | { readonly kind: 'play' }
    | { readonly kind: 'pause' }
    | { readonly kind: 'togglePlay' }
    | { readonly kind: 'stop' }
    | { readonly kind: 'next' }
    | { readonly kind: 'previous' }
    | { readonly kind: 'seek'; readonly seconds: number }
    | { readonly kind: 'setVolume'; readonly volume: number }
    | { readonly kind: 'toggleMute' }
    | { readonly kind: 'toggleShuffle' }
    | { readonly kind: 'cycleRepeat' }
    | { readonly kind: 'toggleCrossfade' }
    | { readonly kind: 'toggleAutoplay' }
    | {
          readonly kind: 'playTracks'
          readonly tracks: readonly Track[]
          readonly start: number
          readonly shuffle?: boolean
          readonly source?: PlayerSource
      }
    | { readonly kind: 'playNext'; readonly tracks: readonly Track[] }
    | { readonly kind: 'addToQueue'; readonly tracks: readonly Track[] }
    | { readonly kind: 'playItem'; readonly key: string }
    | { readonly kind: 'removeItem'; readonly key: string }
    | { readonly kind: 'moveItem'; readonly key: string; readonly offset: -1 | 1 }
    | { readonly kind: 'moveItemNext'; readonly key: string }
    | { readonly kind: 'clearQueue' }

/** What a remote knows when it builds a command. */
export interface CommandContext {
    /** The active device, which gets the command. */
    readonly target: string
    readonly live: LivePlayback
    /** The mirrored queue the user acts on. */
    readonly mirror: Queue
    /** The queue version of the mirrored queue; commands on its entries name it. */
    readonly queueVersion: number
}

/** The ids of the songs a list command sends: playable songs, at most 2000, and the new start. */
export function trackIdsOf(
    tracks: readonly Track[],
    start = 0,
    max = maxSavedEntries,
): { trackIds: string[]; startIndex: number } {
    const startTrack = tracks[start]
    const playable = tracks.filter((track) => !!track.id && track.hasAudio !== false)
    const index = Math.max(0, startTrack ? playable.indexOf(startTrack) : 0)
    const [from, to] = savedWindow(playable.length, index, max)
    return {
        trackIds: playable.slice(from, to).map((track) => track.id!),
        startIndex: Math.max(0, index - from),
    }
}

function clampedMilliseconds(seconds: number): number {
    const value = Number.isFinite(seconds) ? Math.round(seconds * 1000) : 0
    return Math.min(maxLiveMilliseconds, Math.max(0, value))
}

function unit(value: number): number {
    return Number.isFinite(value) ? Math.min(1, Math.max(0, value)) : 0
}

/**
 * The command for an action of the user of a remote, or null if it does nothing (e.g. an entry
 * that is not in the queue, or no songs to play).
 */
export function commandFor(action: RemoteAction, context: CommandContext): DeviceCommand | null {
    const { target, live, mirror, queueVersion } = context
    const command = (type: DeviceCommand['type'], members: Partial<DeviceCommand> = {}) => ({
        targetDeviceId: target,
        type,
        ...members,
    })
    const entry = (key: string) => positionOfKey(mirror, key)

    switch (action.kind) {
        case 'play':
            return command(DeviceCommandTypeObject.Play)
        case 'pause':
            return command(DeviceCommandTypeObject.Pause)
        case 'togglePlay':
            return command(
                live.status === LivePlaybackStatusObject.Playing ||
                    live.status === LivePlaybackStatusObject.Loading
                    ? DeviceCommandTypeObject.Pause
                    : DeviceCommandTypeObject.Play,
            )
        case 'stop':
            return command(DeviceCommandTypeObject.Stop)
        case 'next':
            return command(DeviceCommandTypeObject.Next)
        case 'previous':
            return command(DeviceCommandTypeObject.Previous)
        case 'seek':
            return command(DeviceCommandTypeObject.Seek, {
                positionMilliseconds: clampedMilliseconds(action.seconds),
            })
        case 'setVolume': {
            const volume = unit(action.volume)
            // Like on the device itself, turning the volume up unmutes.
            return command(DeviceCommandTypeObject.SetVolume, {
                volume,
                muted: volume > 0 ? false : null,
            })
        }
        case 'toggleMute':
            return command(DeviceCommandTypeObject.SetVolume, {
                volume: unit(live.volume),
                muted: !live.muted,
            })
        case 'toggleShuffle':
            return command(DeviceCommandTypeObject.SetShuffle, { enabled: !live.shuffle })
        case 'cycleRepeat':
            return command(DeviceCommandTypeObject.SetRepeat, {
                repeat: repeatToApi[nextRepeatMode(repeatFromApi(live.repeat))],
            })
        case 'toggleCrossfade':
            return command(DeviceCommandTypeObject.SetCrossfade, { enabled: !live.crossfade })
        case 'toggleAutoplay':
            return command(DeviceCommandTypeObject.SetAutoplay, { enabled: !live.autoplay })
        case 'playTracks': {
            const { trackIds, startIndex } = trackIdsOf(action.tracks, action.start)
            return trackIds.length === 0
                ? null
                : command(DeviceCommandTypeObject.PlayTracks, {
                      trackIds,
                      startIndex,
                      shuffle: action.shuffle ?? null,
                      source: action.source ? apiSourceOf(action.source) : null,
                  })
        }
        case 'playNext':
        case 'addToQueue': {
            const { trackIds } = trackIdsOf(action.tracks)
            return trackIds.length === 0
                ? null
                : command(
                      action.kind === 'playNext'
                          ? DeviceCommandTypeObject.PlayNext
                          : DeviceCommandTypeObject.AddToQueue,
                      { trackIds },
                  )
        }
        case 'playItem':
        case 'removeItem': {
            const index = entry(action.key)
            return index === null
                ? null
                : command(
                      action.kind === 'playItem'
                          ? DeviceCommandTypeObject.PlayQueueEntry
                          : DeviceCommandTypeObject.RemoveQueueEntry,
                      { index, expectedQueueVersion: queueVersion },
                  )
        }
        case 'moveItem': {
            const index = entry(action.key)
            const toIndex = index === null ? -1 : index + action.offset
            return index === null || toIndex < 0 || toIndex >= mirror.items.length
                ? null
                : command(DeviceCommandTypeObject.MoveQueueEntry, {
                      index,
                      toIndex,
                      expectedQueueVersion: queueVersion,
                  })
        }
        case 'moveItemNext': {
            const index = entry(action.key)
            const current = mirror.index
            if (index === null || index === current || index === current + 1) {
                return null
            }

            // Removed first and inserted after the current entry, as on the device itself.
            const toIndex = index < current ? current : current + 1
            return command(DeviceCommandTypeObject.MoveQueueEntry, {
                index,
                toIndex,
                expectedQueueVersion: queueVersion,
            })
        }
        case 'clearQueue':
            return command(DeviceCommandTypeObject.ClearQueue, {
                expectedQueueVersion: queueVersion,
            })
    }
}

/** The functions of the player of the active device that commands run. */
export interface CommandTarget {
    play(): void
    pause(): void
    stop(): void
    next(): void
    previous(): void
    /** Position in seconds. */
    seek(seconds: number): void
    setVolume(volume: number): void
    setMuted(muted: boolean): void
    setShuffle(enabled: boolean): void
    setRepeat(mode: RepeatMode): void
    setCrossfade(enabled: boolean): void
    setAutoplay(enabled: boolean): void
    playTracks(
        tracks: readonly Track[],
        start: number,
        options: { shuffle?: boolean; source?: PlayerSource },
    ): void
    addToQueue(tracks: readonly Track[]): void
    playNext(tracks: readonly Track[]): void
    /** Entries by their index in the queue of the player. */
    playAt(index: number): void
    removeAt(index: number): void
    moveTo(index: number, toIndex: number): void
    clearQueue(): void
}

function repeatOf(value: ApiRepeatMode | null): RepeatMode | null {
    return value === null ? null : repeatFromApi(value)
}

/**
 * Runs a received command on the player. Positions of the live queue start at windowStart in the
 * queue of the player (a longer queue is published as a window). Returns false for a command that
 * lacks what its type needs (the server checks it, so this only guards against bugs).
 */
export function runCommand(
    command: ReceivedCommand,
    target: CommandTarget,
    windowStart = 0,
): boolean {
    const at = (index: number | null) => (index === null ? null : index + windowStart)
    switch (command.type) {
        case DeviceCommandTypeObject.Play:
            target.play()
            return true
        case DeviceCommandTypeObject.Pause:
            target.pause()
            return true
        case DeviceCommandTypeObject.Stop:
            target.stop()
            return true
        case DeviceCommandTypeObject.Next:
            target.next()
            return true
        case DeviceCommandTypeObject.Previous:
            target.previous()
            return true
        case DeviceCommandTypeObject.Seek:
            if (command.positionMilliseconds === null) {
                return false
            }

            target.seek(command.positionMilliseconds / 1000)
            return true
        case DeviceCommandTypeObject.SetVolume:
            if (command.volume === null) {
                return false
            }

            target.setVolume(command.volume)
            if (command.muted !== null) {
                target.setMuted(command.muted)
            }

            return true
        case DeviceCommandTypeObject.SetShuffle:
        case DeviceCommandTypeObject.SetCrossfade:
        case DeviceCommandTypeObject.SetAutoplay: {
            const enabled = command.enabled
            if (enabled === null) {
                return false
            }

            if (command.type === DeviceCommandTypeObject.SetShuffle) {
                target.setShuffle(enabled)
            } else if (command.type === DeviceCommandTypeObject.SetCrossfade) {
                target.setCrossfade(enabled)
            } else {
                target.setAutoplay(enabled)
            }

            return true
        }
        case DeviceCommandTypeObject.SetRepeat: {
            const mode = repeatOf(command.repeat)
            if (mode === null) {
                return false
            }

            target.setRepeat(mode)
            return true
        }
        case DeviceCommandTypeObject.PlayTracks:
            if (!command.tracks?.length) {
                return false
            }

            target.playTracks(command.tracks, command.startIndex ?? 0, {
                ...(command.shuffle === null ? {} : { shuffle: command.shuffle }),
                ...(playerSourceOf(command.source)
                    ? { source: playerSourceOf(command.source)! }
                    : {}),
            })
            return true
        case DeviceCommandTypeObject.AddToQueue:
        case DeviceCommandTypeObject.PlayNext:
            if (!command.tracks?.length) {
                return false
            }

            if (command.type === DeviceCommandTypeObject.AddToQueue) {
                target.addToQueue(command.tracks)
            } else {
                target.playNext(command.tracks)
            }

            return true
        case DeviceCommandTypeObject.PlayQueueEntry:
        case DeviceCommandTypeObject.RemoveQueueEntry: {
            const index = at(command.index)
            if (index === null) {
                return false
            }

            if (command.type === DeviceCommandTypeObject.PlayQueueEntry) {
                target.playAt(index)
            } else {
                target.removeAt(index)
            }

            return true
        }
        case DeviceCommandTypeObject.MoveQueueEntry: {
            const index = at(command.index)
            const toIndex = at(command.toIndex)
            if (index === null || toIndex === null) {
                return false
            }

            target.moveTo(index, toIndex)
            return true
        }
        case DeviceCommandTypeObject.ClearQueue:
            target.clearQueue()
            return true
        default:
            return false
    }
}
