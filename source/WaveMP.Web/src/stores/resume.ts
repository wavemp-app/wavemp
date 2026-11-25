import { defineStore } from 'pinia'
import { ref, watch } from 'vue'
import type { PlaybackState } from '@/api/generated/models'
import { createLatestValueSender } from '@/api/latestValue'
import { statusOf } from '@/api/problems'
import { clearPlaybackState, getPlaybackState, savePlaybackState } from '@/player/api'
import { deviceId, deviceName } from '@/player/device'
import {
    fromSavedState,
    isFromOtherDevice,
    saveDelayMilliseconds,
    saveIntervalSeconds,
    savedStateSignature,
    toSavedState,
    type RestoredState,
    type SavedState,
} from '@/player/resume'
import { useDevicesStore } from './devices'
import { usePlayerStore } from './player'
import { useSessionStore } from './session'

/** Parts of the browser the store uses; tests replace them. */
export const resumeDependencies = {
    deviceId: (): string => deviceId(),
    deviceName: (): string =>
        deviceName(typeof navigator === 'undefined' ? '' : navigator.userAgent),
    setTimeout: (handler: () => void, milliseconds: number): number =>
        window.setTimeout(handler, milliseconds),
    clearTimeout: (id: number): void => window.clearTimeout(id),
    setInterval: (handler: () => void, milliseconds: number): number =>
        window.setInterval(handler, milliseconds),
    clearInterval: (id: number): void => window.clearInterval(id),
    /** Whether the page listens to visibility and focus; off in tests. */
    listen: true,
}

/** The notice after the state of another device was taken over. */
export interface ResumeNotice {
    /** Name of the device the state comes from. */
    readonly device: string
}

/**
 * Resuming playback: where the profile stopped listening is saved on the server and continues
 * after a reload or on another device (see src/player/resume.ts).
 *
 * On start (and after a change of profile) the saved state loads into the player, which waits
 * paused at the saved position: browsers block playback without an action of the user, so play
 * goes on there. A state of another device shows the notice "Continue listening from <device>".
 * A restored song that cannot be loaded waits paused and saves nothing; play requests it again.
 *
 * The state is saved when the song changes, on pause and stop, every 10 seconds while playing and
 * shortly after a change of the queue, shuffle or repeat; never on every progress update, and only
 * when something changed. Saves go out one after the other, only the latest waits. The most
 * recently changed state wins: when another device saved a newer one, or when the page comes back
 * to the front while nothing plays here, the player takes over the newer state of the other device.
 *
 * Clearing the saved state (in the settings) stops the player here and empties its queue first, so
 * this device does not save it again; the next start begins without a queue.
 *
 * While this device is a remote of another device (see src/stores/devices.ts), it neither saves nor
 * takes over saved states: the active device plays and saves. When it stops being a remote because
 * the active device closed, the state of that device waits in the player with the same notice.
 */
export const useResumeStore = defineStore('resume', () => {
    const session = useSessionStore()
    const player = usePlayerStore()
    const devices = useDevicesStore()

    const notice = ref<ResumeNotice | null>(null)

    const device = { id: resumeDependencies.deviceId(), name: resumeDependencies.deviceName() }
    /** Whether the saved state of the session was loaded (or there is none): only then saves go out. */
    let ready = false
    /** Increases with every new session and every load, so late answers are ignored. */
    let generation = 0
    /** The last state sent (or taken over), so an unchanged state is not sent again. */
    let lastSignature: string | null = null
    /** Time of change of the state of another device that was taken over last. */
    let adoptedChangedAt: number | null = null
    /** Current entry of the state taken over last; its change is no new song. */
    let adoptedKey: string | null = null
    /** Whether the state in the player was saved by another device and not yet by this one. */
    let foreign = false
    let delayed: number | null = null
    let ticker: number | null = null
    /** The latest save handed to the sender; clearing waits until it has reached the server. */
    let pending: Promise<unknown> = Promise.resolve()

    const sender = createLatestValueSender((state: SavedState) =>
        // Without a time of change the server takes the time the save arrives, so clocks of the
        // devices that differ do not matter.
        savePlaybackState({ ...state, device: { id: device.id, name: device.name } }),
    )

    function snapshot(): SavedState {
        return toSavedState({
            queue: player.queue,
            repeat: player.repeat,
            position: player.position,
            source: player.source,
        })
    }

    /** Takes over a saved state; a state of another device shows the notice. */
    function adopt(state: PlaybackState): void {
        const restored = fromSavedState(state)
        player.restore(restored)
        adoptedKey = player.current?.key ?? null
        lastSignature = savedStateSignature(snapshot())
        adoptedChangedAt = state.changedAt?.getTime() ?? null
        foreign = !!state.device?.id && state.device.id !== device.id
        notice.value =
            state.device?.id && state.device.id !== device.id && restored.queue.items.length > 0
                ? { device: state.device.name ?? '' }
                : null
    }

    /** Whether the player is in use here, so a state of another device must not replace it. */
    function busy(): boolean {
        return player.playback === 'playing' || (player.loading && player.playback !== 'paused')
    }

    /** Whether this device is a remote: its player shows the state of the active device. */
    function remote(): boolean {
        return devices.role === 'remote'
    }

    /** Takes over the state of the device that played before this device stopped being its remote. */
    function adoptFromRemote(state: RestoredState, deviceName: string | null): boolean {
        player.restore(state)
        adoptedKey = player.current?.key ?? null
        lastSignature = savedStateSignature(snapshot())
        foreign = true
        notice.value = deviceName && state.queue.items.length > 0 ? { device: deviceName } : null
        return true
    }

    devices.onResumeFromRemote(adoptFromRemote)

    /** Loads the saved state of a new session into the empty player. */
    async function load(): Promise<void> {
        const own = ++generation
        ready = false
        lastSignature = null
        adoptedChangedAt = null
        foreign = false
        notice.value = null
        sender.reset()
        cancelDelayed()
        if (!session.token) {
            return
        }

        let state: PlaybackState | undefined
        try {
            state = await getPlaybackState()
        } catch {
            state = undefined
        }

        if (own !== generation) {
            return
        }

        // Something started meanwhile: the action of the user wins over the saved state; a remote
        // shows the state of the active device instead.
        if (state && !remote() && player.queue.items.length === 0 && player.playback === 'idle') {
            adopt(state)
        }

        ready = true
    }

    /**
     * Takes over the saved state of another device if it is newer than the one known here and
     * nothing plays here; called when the page comes back to the front or a save was too old.
     */
    async function checkOtherDevices(): Promise<void> {
        if (!ready || busy() || remote()) {
            return
        }

        const own = generation
        let state: PlaybackState | undefined
        try {
            state = await getPlaybackState()
        } catch {
            return
        }

        if (
            own === generation &&
            state &&
            !busy() &&
            !remote() &&
            isFromOtherDevice(state, device.id, adoptedChangedAt)
        ) {
            adopt(state)
        }
    }

    /** Saves the state of the player now, if it changed since the last save. */
    function saveNow(): void {
        cancelDelayed()
        if (!ready || !session.token || player.queue.items.length === 0 || remote()) {
            return
        }

        const state = snapshot()
        const signature = savedStateSignature(state)
        if (signature === lastSignature) {
            return
        }

        lastSignature = signature
        const own = generation
        foreign = false
        const saved = sender.send(state)
        pending = saved.catch(() => undefined)
        saved.catch((error: unknown) => {
            if (own !== generation) {
                return
            }

            // Not saved: the next change tries again.
            if (lastSignature === signature) {
                lastSignature = null
            }

            if (statusOf(error) === 409) {
                void checkOtherDevices()
            }
        })
    }

    /** Saves shortly after a change, so quick changes save once. */
    function saveSoon(): void {
        cancelDelayed()
        delayed = resumeDependencies.setTimeout(() => {
            delayed = null
            saveNow()
        }, saveDelayMilliseconds)
    }

    function cancelDelayed(): void {
        if (delayed !== null) {
            resumeDependencies.clearTimeout(delayed)
            delayed = null
        }
    }

    function updateTicker(playing: boolean): void {
        if (playing && ticker === null) {
            ticker = resumeDependencies.setInterval(saveNow, saveIntervalSeconds * 1000)
        } else if (!playing && ticker !== null) {
            resumeDependencies.clearInterval(ticker)
            ticker = null
        }
    }

    /** Plays the state that was taken over from the saved position. */
    function resume(): void {
        notice.value = null
        player.play()
    }

    function dismiss(): void {
        notice.value = null
    }

    /**
     * Forgets where the profile stopped listening: stops the player and empties its queue, waits
     * for a save on its way (it would bring the state back) and clears the saved state. Fails if
     * the server cannot clear it; the player stays empty then.
     */
    async function clear(): Promise<void> {
        cancelDelayed()
        notice.value = null
        player.clear()
        await pending
        sender.reset()
        lastSignature = null
        adoptedChangedAt = null
        adoptedKey = null
        foreign = false
        await clearPlaybackState()
    }

    // A new session (another profile, or none) loads its own state.
    watch(
        () => session.token,
        () => void load(),
        { immediate: true },
    )

    // A new song: saved at once; the notice of another device is done.
    watch(
        () => player.current?.key ?? null,
        (key, previous) => {
            if (previous !== null && key !== previous && key !== adoptedKey) {
                notice.value = null
            }

            saveNow()
        },
    )

    // Pause and stop save at once; while playing, every few seconds.
    watch(
        () => player.playback,
        (playback, previous) => {
            updateTicker(playback === 'playing')
            if (playback === 'playing') {
                notice.value = null
                // Playing here makes this device the one the profile listens on.
                if (foreign) {
                    lastSignature = null
                    saveNow()
                }
            } else if (previous === 'playing' || playback === 'idle') {
                saveNow()
            }
        },
        { immediate: true },
    )

    // The queue, shuffle, repeat or the position while nothing plays (seeking) change: saved soon.
    watch(
        () => [player.queue, player.repeat, player.source] as const,
        () => saveSoon(),
    )
    watch(
        () => player.position,
        () => {
            if (player.playback !== 'playing') {
                saveSoon()
            }
        },
    )

    if (resumeDependencies.listen && typeof document !== 'undefined') {
        // Leaving the page saves the latest position; coming back looks for newer states.
        document.addEventListener('visibilitychange', () => {
            if (document.visibilityState === 'hidden') {
                saveNow()
            } else {
                void checkOtherDevices()
            }
        })
        window.addEventListener('pagehide', saveNow)
        window.addEventListener('focus', () => void checkOtherDevices())
    }

    return { notice, device, resume, dismiss, clear, saveNow, checkOtherDevices }
})
