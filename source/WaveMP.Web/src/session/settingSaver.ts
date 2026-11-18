// Saving a setting of the profile the moment it is chosen. The choice shows at once, so a control
// never has to wait for the server and keys or clicks that follow quickly are not lost. The changes
// go to the server one after the other (see createLatestValueSender): of the changes made while a
// request is in flight only the latest follows, partial changes of a group merged into one.
// Framework-free; the session store connects it to its state.

import { createLatestValueSender } from '@/api/latestValue'

export interface SettingSaverOptions<S, C, R> {
    /** The value the app shows now. */
    get(): S
    /** Shows a value. */
    set(value: S): void
    /** The value after a change, as the server applies it. */
    apply(value: S, change: C): S
    /** Saves a change on the server; its answer is passed to `read`. */
    request(change: C): Promise<R>
    /** The saved value from an answer of the server, or null if the answer does not hold it. */
    read(result: R): S | null
    /**
     * Merges a change that still waits with a newer one, for partial changes of a group; without it
     * the newer change replaces the waiting one.
     */
    merge?(waiting: C, newer: C): C
}

export interface SettingSaver<C> {
    /**
     * Shows the change at once and saves it. The answer of the server is taken over once no newer
     * change waits. If saving fails and no newer change was made meanwhile, the value the server
     * saved last comes back. Errors are passed on.
     */
    save(change: C): Promise<void>
    /**
     * Forgets the changes in flight and waiting, e.g. when another profile is selected, so nothing
     * chosen for the previous profile is sent or shown for the next one.
     */
    reset(): void
}

/**
 * Merges two partial changes of a group of settings as the API takes them: members of the newer
 * change that are missing or null leave those of the waiting change as they are. Suits groups whose
 * members the server applies independently of each other.
 */
export function mergeChanges<C extends object>(waiting: C, newer: C): C {
    const merged: Record<string, unknown> = { ...(waiting as Record<string, unknown>) }
    for (const [key, value] of Object.entries(newer)) {
        if (value !== null && value !== undefined) {
            merged[key] = value
        }
    }

    return merged as C
}

/** Creates a saver of one setting or group of settings, see SettingSaver. */
export function createSettingSaver<S, C, R>(
    options: SettingSaverOptions<S, C, R>,
): SettingSaver<C> {
    const sender = createLatestValueSender(options.request, options.merge)
    /** Increases with every reset, so answers for an earlier profile are ignored. */
    let generation = 0
    /** Increases with every change, so only the latest change decides what is shown. */
    let changes = 0
    /** Changes whose saving has not finished yet. */
    let pending = 0
    /** The value the server saved last, as far as known; the shown value while nothing is pending. */
    let saved: S | null = null

    async function save(change: C): Promise<void> {
        const own = generation
        const number = ++changes
        if (pending === 0) {
            saved = options.get()
        }

        pending++
        options.set(options.apply(options.get(), change))
        try {
            const outcome = await sender.send(change)
            if (own !== generation || outcome.status !== 'sent') {
                return
            }

            const value = options.read(outcome.result)
            if (value !== null) {
                saved = value
                if (outcome.latest && number === changes) {
                    options.set(value)
                }
            }
        } catch (error) {
            if (own === generation && number === changes && saved !== null) {
                options.set(saved)
            }

            throw error
        } finally {
            if (own === generation) {
                pending--
            }
        }
    }

    function reset(): void {
        generation++
        changes = 0
        pending = 0
        saved = null
        sender.reset()
    }

    return { save, reset }
}
