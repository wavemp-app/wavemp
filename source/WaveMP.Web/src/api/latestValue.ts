// Saving a setting that can change faster than the server answers (e.g. a click on another view
// while the previous one is still being saved). Requests sent in parallel may be committed in any
// order, so the server could keep an older value. Values are therefore sent one after the other:
// at most one request is in flight, and of the values chosen meanwhile only the latest follows.

/** Result of a value handed to a LatestValueSender. */
export type LatestValueOutcome<R> =
    | {
          readonly status: 'sent'
          /** The answer of the server. */
          readonly result: R
          /** Whether no newer value waited when the answer came; only then is it current. */
          readonly latest: boolean
      }
    | {
          /** A newer value replaced this one before it was sent, or the sender was reset. */
          readonly status: 'superseded'
      }

export interface LatestValueSender<T, R> {
    /**
     * Sends the value now or, while a request is in flight, after it; a newer value replaces one
     * that still waits. Errors of its own request are passed on.
     */
    send(value: T): Promise<LatestValueOutcome<R>>
    /**
     * Drops a waiting value and forgets the request in flight, e.g. when another profile is
     * selected, so nothing chosen for the previous profile is sent for the next one.
     */
    reset(): void
}

interface Waiting<T, R> {
    readonly value: T
    readonly resolve: (outcome: LatestValueOutcome<R>) => void
    readonly reject: (error: unknown) => void
}

/** Creates a sender that passes values to `request` one after the other, see LatestValueSender. */
export function createLatestValueSender<T, R>(
    request: (value: T) => Promise<R>,
): LatestValueSender<T, R> {
    let running = false
    let waiting: Waiting<T, R> | null = null
    /** Increases with every reset, so a loop of an earlier generation stops after its request. */
    let generation = 0

    async function run(first: Waiting<T, R>, own: number): Promise<void> {
        let current: Waiting<T, R> | null = first
        while (current) {
            try {
                const result = await request(current.value)
                current.resolve({
                    status: 'sent',
                    result,
                    latest: own === generation && waiting === null,
                })
            } catch (error) {
                current.reject(error)
            }

            if (own !== generation) {
                return
            }

            current = waiting
            waiting = null
        }

        running = false
    }

    function send(value: T): Promise<LatestValueOutcome<R>> {
        return new Promise((resolve, reject) => {
            const entry: Waiting<T, R> = { value, resolve, reject }
            if (running) {
                waiting?.resolve({ status: 'superseded' })
                waiting = entry
                return
            }

            running = true
            void run(entry, generation)
        })
    }

    function reset(): void {
        generation++
        running = false
        waiting?.resolve({ status: 'superseded' })
        waiting = null
    }

    return { send, reset }
}
