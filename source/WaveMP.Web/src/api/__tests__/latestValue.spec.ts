import { describe, expect, it, vi } from 'vitest'
import { createLatestValueSender } from '../latestValue'

/** A request whose answers the test gives one by one, in the order the requests were made. */
function controlledRequest() {
    const calls: string[] = []
    const answers: { resolve: (value: string) => void; reject: (error: unknown) => void }[] = []
    const request = vi.fn((value: string) => {
        calls.push(value)
        return new Promise<string>((resolve, reject) => answers.push({ resolve, reject }))
    })

    return {
        request,
        calls,
        answer(index: number, result: string) {
            answers[index]!.resolve(result)
        },
        fail(index: number, error: unknown) {
            answers[index]!.reject(error)
        },
    }
}

/** Lets pending promise callbacks run. */
async function settle(): Promise<void> {
    for (let index = 0; index < 5; index++) {
        await Promise.resolve()
    }
}

describe('createLatestValueSender', () => {
    it('sends a value at once and passes the answer on as the latest one', async () => {
        const control = controlledRequest()
        const sender = createLatestValueSender(control.request)

        const sending = sender.send('Cover')
        expect(control.calls).toEqual(['Cover'])
        control.answer(0, 'saved Cover')

        await expect(sending).resolves.toEqual({
            status: 'sent',
            result: 'saved Cover',
            latest: true,
        })
    })

    it('sends a value chosen meanwhile only after the previous request finished', async () => {
        const control = controlledRequest()
        const sender = createLatestValueSender(control.request)

        const first = sender.send('Cover')
        const second = sender.send('Visualization')
        await settle()
        expect(control.calls).toEqual(['Cover'])

        control.answer(0, 'saved Cover')
        await expect(first).resolves.toEqual({
            status: 'sent',
            result: 'saved Cover',
            latest: false,
        })
        await settle()
        expect(control.calls).toEqual(['Cover', 'Visualization'])

        control.answer(1, 'saved Visualization')
        await expect(second).resolves.toEqual({
            status: 'sent',
            result: 'saved Visualization',
            latest: true,
        })
    })

    it('skips values replaced by a newer one before they were sent', async () => {
        const control = controlledRequest()
        const sender = createLatestValueSender(control.request)

        const first = sender.send('Cover')
        const skipped = sender.send('Visualization')
        const last = sender.send('Video')
        await expect(skipped).resolves.toEqual({ status: 'superseded' })

        control.answer(0, 'saved Cover')
        await first
        await settle()
        control.answer(1, 'saved Video')

        await expect(last).resolves.toMatchObject({ status: 'sent', latest: true })
        expect(control.calls).toEqual(['Cover', 'Video'])
    })

    it('passes an error on and still sends the value that waits', async () => {
        const control = controlledRequest()
        const sender = createLatestValueSender(control.request)

        const failing = sender.send('Cover')
        const waiting = sender.send('Visualization')
        control.fail(0, new TypeError('Failed to fetch'))

        await expect(failing).rejects.toThrow('Failed to fetch')
        await settle()
        expect(control.calls).toEqual(['Cover', 'Visualization'])
        control.answer(1, 'saved Visualization')
        await expect(waiting).resolves.toMatchObject({ status: 'sent', latest: true })
    })

    it('sends again at once after the last request finished', async () => {
        const control = controlledRequest()
        const sender = createLatestValueSender(control.request)

        const first = sender.send('Cover')
        control.answer(0, 'saved Cover')
        await first
        await settle()

        void sender.send('Video')
        expect(control.calls).toEqual(['Cover', 'Video'])
    })

    it('drops the waiting value on a reset and sends the next one at once', async () => {
        const control = controlledRequest()
        const sender = createLatestValueSender(control.request)

        const inFlight = sender.send('Cover')
        const dropped = sender.send('Visualization')
        sender.reset()
        await expect(dropped).resolves.toEqual({ status: 'superseded' })

        const next = sender.send('Video')
        expect(control.calls).toEqual(['Cover', 'Video'])

        // The earlier request is no longer the latest and starts nothing after it.
        control.answer(0, 'saved Cover')
        await expect(inFlight).resolves.toMatchObject({ status: 'sent', latest: false })
        await settle()
        expect(control.calls).toEqual(['Cover', 'Video'])

        control.answer(1, 'saved Video')
        await expect(next).resolves.toMatchObject({ status: 'sent', latest: true })
    })
})
