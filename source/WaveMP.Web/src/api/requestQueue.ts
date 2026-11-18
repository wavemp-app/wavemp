// Requests that change the same resource (e.g. the settings of the profile) one after the other.
// Requests sent in parallel may reach the server in any order. A queue starts a request only after
// the previous one finished, whatever its outcome; while it is idle, a request starts at once.

export interface RequestQueue {
    /** Starts the request after all requests queued before it finished; passes its result on. */
    run<T>(request: () => Promise<T>): Promise<T>
}

export function createRequestQueue(): RequestQueue {
    let pending = 0
    let tail: Promise<void> = Promise.resolve()

    function invoke<T>(request: () => Promise<T>): Promise<T> {
        try {
            return request()
        } catch (error) {
            return Promise.reject(error)
        }
    }

    function run<T>(request: () => Promise<T>): Promise<T> {
        const result = pending === 0 ? invoke(request) : tail.then(() => invoke(request))
        pending++
        tail = result.then(
            () => {
                pending--
            },
            () => {
                pending--
            },
        )
        return result
    }

    return { run }
}
