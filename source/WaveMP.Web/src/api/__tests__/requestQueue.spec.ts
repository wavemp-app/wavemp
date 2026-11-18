import { describe, expect, it, vi } from 'vitest'
import { createRequestQueue } from '../requestQueue'

/** A request whose answer the test gives. */
function held<T>() {
    let resolve: (value: T) => void = () => undefined
    let reject: (error: unknown) => void = () => undefined
    const request = vi.fn(
        () =>
            new Promise<T>((done, fail) => {
                resolve = done
                reject = fail
            }),
    )
    return {
        request,
        resolve: (value: T) => resolve(value),
        reject: (error: unknown) => reject(error),
    }
}

/** Lets pending promise callbacks run. */
async function settle(): Promise<void> {
    for (let index = 0; index < 5; index++) {
        await Promise.resolve()
    }
}

describe('createRequestQueue', () => {
    it('starts a request at once while nothing is in flight', async () => {
        const queue = createRequestQueue()
        const first = held<string>()

        const result = queue.run(first.request)
        expect(first.request).toHaveBeenCalledOnce()

        first.resolve('saved')
        await expect(result).resolves.toBe('saved')
    })

    it('starts the next request only after the previous one finished', async () => {
        const queue = createRequestQueue()
        const first = held<string>()
        const second = held<string>()
        const third = held<string>()

        const results = [
            queue.run(first.request),
            queue.run(second.request),
            queue.run(third.request),
        ]
        await settle()
        expect(second.request).not.toHaveBeenCalled()

        first.resolve('first')
        await settle()
        expect(second.request).toHaveBeenCalledOnce()
        expect(third.request).not.toHaveBeenCalled()

        second.resolve('second')
        await settle()
        third.resolve('third')
        await expect(Promise.all(results)).resolves.toEqual(['first', 'second', 'third'])
    })

    it('goes on after a failed request and passes its error on', async () => {
        const queue = createRequestQueue()
        const failing = held<string>()
        const next = held<string>()

        const failed = queue.run(failing.request)
        const result = queue.run(next.request)
        failing.reject(new TypeError('Failed to fetch'))

        await expect(failed).rejects.toThrow('Failed to fetch')
        await settle()
        expect(next.request).toHaveBeenCalledOnce()
        next.resolve('saved')
        await expect(result).resolves.toBe('saved')
    })

    it('passes on an error thrown before the request started', async () => {
        const queue = createRequestQueue()

        await expect(
            queue.run(() => {
                throw new Error('invalid')
            }),
        ).rejects.toThrow('invalid')
        const next = held<string>()
        const result = queue.run(next.request)
        await settle()
        next.resolve('saved')
        await expect(result).resolves.toBe('saved')
    })
})
