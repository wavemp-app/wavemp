// Reading a setting of the profile again from the server (e.g. the refresh on every start) while the
// profile may change it on the page. The answer of such a read can be older than the choice on the
// page: the request may have been answered before a change was saved, or a change was made after
// it was sent. Such an answer must not replace the choice, the shown value is the newer one.
// Framework-free; the setting saver and the session store use it.

export interface ChangeTracker {
    /**
     * Marks a change whose saving starts now. The returned function is called once saving has
     * finished, also when it failed or was superseded; further calls do nothing.
     */
    change(): () => void
    /**
     * Starts reading the value from the server. The returned function tells, once the answer is
     * there, whether it may replace the shown value: no change was being saved when reading
     * started, none is being saved now, none was made meanwhile and the tracker was not reset.
     */
    read(): () => boolean
    /**
     * Forgets the changes being saved, e.g. when another profile is selected; reads started before
     * do not apply any more.
     */
    reset(): void
}

/** Creates a tracker of the changes of one setting or group of settings, see ChangeTracker. */
export function createChangeTracker(): ChangeTracker {
    /** Increases with every reset, so changes and reads of an earlier profile are ignored. */
    let generation = 0
    /** Increases with every change, so a read knows whether one was made meanwhile. */
    let changes = 0
    /** Changes whose saving has not finished yet. */
    let pending = 0

    function change(): () => void {
        const own = generation
        let finished = false
        changes++
        pending++
        return () => {
            if (!finished && own === generation) {
                pending--
            }

            finished = true
        }
    }

    function read(): () => boolean {
        const own = generation
        const start = changes
        const idle = pending === 0
        return () => idle && own === generation && start === changes && pending === 0
    }

    function reset(): void {
        generation++
        pending = 0
    }

    return { change, read, reset }
}
