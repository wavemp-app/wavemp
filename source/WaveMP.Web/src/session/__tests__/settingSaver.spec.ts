import { describe, expect, it, vi } from 'vitest'
import { createSettingSaver, mergeChanges } from '../settingSaver'

interface Group {
    readonly seconds: number
    readonly albums: boolean
}

type Change = Partial<Group>

/** A request whose answers the test gives one by one, in the order the requests were made. */
function controlledRequest() {
    const calls: Change[] = []
    const answers: { resolve: (value: Group) => void; reject: (error: unknown) => void }[] = []
    const request = vi.fn((change: Change) => {
        calls.push(change)
        return new Promise<Group>((resolve, reject) => answers.push({ resolve, reject }))
    })

    return {
        request,
        calls,
        answer(index: number, result: Group) {
            answers[index]!.resolve(result)
        },
        fail(index: number, error: unknown) {
            answers[index]!.reject(error)
        },
    }
}

/** A saver of a group of two settings, shown in `shown`. */
function groupSaver(initial: Group = { seconds: 5, albums: true }) {
    const control = controlledRequest()
    const state = { shown: initial }
    const saver = createSettingSaver({
        get: () => state.shown,
        set: (value: Group) => {
            state.shown = value
        },
        apply: (value: Group, change: Change) => ({ ...value, ...change }),
        merge: mergeChanges,
        request: control.request,
        read: (result: Group | null) => result,
    })
    return { control, state, saver }
}

/** Lets pending promise callbacks run. */
async function settle(): Promise<void> {
    for (let index = 0; index < 10; index++) {
        await Promise.resolve()
    }
}

describe('createSettingSaver', () => {
    it('shows a change at once and takes over the answer of the server', async () => {
        const { control, state, saver } = groupSaver()

        const saving = saver.save({ seconds: 8 })
        expect(state.shown).toEqual({ seconds: 8, albums: true })
        expect(control.calls).toEqual([{ seconds: 8 }])

        control.answer(0, { seconds: 8, albums: false })
        await saving
        expect(state.shown).toEqual({ seconds: 8, albums: false })
    })

    it('shows quick changes at once and saves only the latest after the one in flight', async () => {
        const { control, state, saver } = groupSaver()

        const first = saver.save({ seconds: 1 })
        const skipped = saver.save({ seconds: 2 })
        const last = saver.save({ seconds: 3 })
        expect(state.shown.seconds).toBe(3)
        await skipped
        expect(control.calls).toEqual([{ seconds: 1 }])

        // The answer of the first change does not take back the newer choice.
        control.answer(0, { seconds: 1, albums: true })
        await first
        expect(state.shown.seconds).toBe(3)
        await settle()
        expect(control.calls).toEqual([{ seconds: 1 }, { seconds: 3 }])

        control.answer(1, { seconds: 3, albums: true })
        await last
        expect(state.shown.seconds).toBe(3)
    })

    it('merges the changes of a group made while one is saved', async () => {
        const { control, state, saver } = groupSaver()

        const first = saver.save({ seconds: 1 })
        void saver.save({ albums: false })
        const last = saver.save({ seconds: 9 })
        expect(state.shown).toEqual({ seconds: 9, albums: false })

        control.answer(0, { seconds: 1, albums: true })
        await first
        await settle()
        expect(control.calls).toEqual([{ seconds: 1 }, { albums: false, seconds: 9 }])

        control.answer(1, { seconds: 9, albums: false })
        await last
        expect(state.shown).toEqual({ seconds: 9, albums: false })
    })

    it('shows the saved value again when saving fails and passes the error on', async () => {
        const { control, state, saver } = groupSaver()

        const saving = saver.save({ seconds: 8 })
        control.fail(0, new TypeError('Failed to fetch'))

        await expect(saving).rejects.toThrow('Failed to fetch')
        expect(state.shown).toEqual({ seconds: 5, albums: true })
    })

    it('keeps a newer change when an older one fails', async () => {
        const { control, state, saver } = groupSaver()

        const failing = saver.save({ seconds: 1 })
        const newer = saver.save({ seconds: 2 })
        control.fail(0, new TypeError('Failed to fetch'))

        await expect(failing).rejects.toThrow('Failed to fetch')
        expect(state.shown.seconds).toBe(2)
        await settle()
        control.answer(1, { seconds: 2, albums: true })
        await newer
        expect(state.shown.seconds).toBe(2)
    })

    it('goes back to the last answer of the server when the latest change fails', async () => {
        const { control, state, saver } = groupSaver()

        const first = saver.save({ seconds: 1 })
        const failing = saver.save({ seconds: 2 })
        control.answer(0, { seconds: 1, albums: true })
        await first
        await settle()
        control.fail(1, new TypeError('Failed to fetch'))

        await expect(failing).rejects.toThrow('Failed to fetch')
        expect(state.shown.seconds).toBe(1)
    })

    it('takes the shown value as saved again once nothing is pending', async () => {
        const { control, state, saver } = groupSaver()
        const first = saver.save({ seconds: 1 })
        control.answer(0, { seconds: 1, albums: true })
        await first

        // E.g. a refresh brought a value of another device meanwhile.
        state.shown = { seconds: 7, albums: false }
        const failing = saver.save({ seconds: 2 })
        control.fail(1, new TypeError('Failed to fetch'))

        await expect(failing).rejects.toThrow('Failed to fetch')
        expect(state.shown).toEqual({ seconds: 7, albums: false })
    })

    it('ignores answers and errors for the time before a reset', async () => {
        const { control, state, saver } = groupSaver()

        const before = saver.save({ seconds: 1 })
        const dropped = saver.save({ seconds: 2 })
        saver.reset()
        state.shown = { seconds: 5, albums: true }
        await dropped

        const after = saver.save({ albums: false })
        expect(control.calls).toEqual([{ seconds: 1 }, { albums: false }])
        control.fail(0, new TypeError('Failed to fetch'))
        await expect(before).rejects.toThrow('Failed to fetch')
        expect(state.shown).toEqual({ seconds: 5, albums: false })

        control.answer(1, { seconds: 5, albums: false })
        await after
        expect(state.shown).toEqual({ seconds: 5, albums: false })
    })

    it('keeps the shown value when the answer does not hold the setting', async () => {
        const { control, state, saver } = groupSaver()

        const saving = saver.save({ seconds: 8 })
        control.answer(0, null as unknown as Group)
        await saving

        expect(state.shown.seconds).toBe(8)
    })

    describe('load', () => {
        it('shows the value read when nothing is being saved', () => {
            const { state, saver } = groupSaver()

            saver.load()({ seconds: 9, albums: false })

            expect(state.shown).toEqual({ seconds: 9, albums: false })
        })

        it('keeps the shown value when the answer does not hold the setting', () => {
            const { state, saver } = groupSaver()

            saver.load()(null as unknown as Group)

            expect(state.shown).toEqual({ seconds: 5, albums: true })
        })

        it('keeps a change made while reading that is still being saved', () => {
            const { state, saver } = groupSaver()
            const show = saver.load()

            void saver.save({ seconds: 8 })
            show({ seconds: 5, albums: true })

            expect(state.shown).toEqual({ seconds: 8, albums: true })
        })

        it('keeps a change made and saved while reading', async () => {
            const { control, state, saver } = groupSaver()
            const show = saver.load()

            const saving = saver.save({ seconds: 8 })
            control.answer(0, { seconds: 8, albums: true })
            await saving
            show({ seconds: 5, albums: true })

            expect(state.shown).toEqual({ seconds: 8, albums: true })
        })

        it('keeps a change that was being saved when reading started', async () => {
            const { control, state, saver } = groupSaver()
            const saving = saver.save({ seconds: 8 })
            const show = saver.load()

            control.answer(0, { seconds: 8, albums: true })
            await saving
            show({ seconds: 5, albums: true })

            expect(state.shown).toEqual({ seconds: 8, albums: true })
        })

        it('shows the value of a read that started after the change was saved', async () => {
            const { control, state, saver } = groupSaver()
            const saving = saver.save({ seconds: 8 })
            control.answer(0, { seconds: 8, albums: true })
            await saving

            saver.load()({ seconds: 8, albums: false })

            expect(state.shown).toEqual({ seconds: 8, albums: false })
        })

        it('ignores a read started before a reset', async () => {
            const { state, saver } = groupSaver()
            const show = saver.load()

            saver.reset()
            show({ seconds: 1, albums: false })

            expect(state.shown).toEqual({ seconds: 5, albums: true })
            saver.load()({ seconds: 2, albums: false })
            expect(state.shown).toEqual({ seconds: 2, albums: false })
        })
    })
})

describe('mergeChanges', () => {
    it('takes the members the newer change names and keeps the others', () => {
        expect(
            mergeChanges<{ a?: number | null; b?: string | null; c?: boolean | null }>(
                { a: 1, b: 'waiting', c: true },
                { a: 2, b: null, c: undefined },
            ),
        ).toEqual({ a: 2, b: 'waiting', c: true })
    })

    it('keeps an empty text and false, which are values of their own', () => {
        expect(
            mergeChanges<{ preset?: string; on?: boolean }>(
                { preset: 'Waves', on: true },
                { preset: '', on: false },
            ),
        ).toEqual({ preset: '', on: false })
    })
})
