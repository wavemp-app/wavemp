import { describe, expect, it, vi } from 'vitest'
import { backAction, goBack } from '../back'

const songs = { name: 'library-section', params: { section: 'songs' } }
const withPrevious = { back: '/library/songs', current: '/library/tracks/1', position: 3 }
const opened = { back: null, current: '/library/tracks/1', position: 0 }

describe('backAction', () => {
    it('goes back in the history when the app has a previous page', () => {
        expect(backAction(withPrevious, { fallback: songs })).toEqual({ kind: 'history' })
    })

    it('opens the overview of a page that was opened directly', () => {
        expect(backAction(opened, { fallback: songs })).toEqual({ kind: 'route', to: songs })
        expect(backAction(null, { fallback: songs })).toEqual({ kind: 'route', to: songs })
    })

    it('closes without a previous page and without an overview', () => {
        expect(backAction(opened, {})).toEqual({ kind: 'close' })
    })

    it('ignores the history when asked to', () => {
        expect(backAction(withPrevious, { history: false })).toEqual({ kind: 'close' })
        expect(backAction(withPrevious, { history: false, fallback: songs })).toEqual({
            kind: 'route',
            to: songs,
        })
    })
})

describe('goBack', () => {
    function router() {
        return { back: vi.fn(), push: vi.fn().mockResolvedValue(undefined) }
    }

    it('goes back in the history', () => {
        const target = router()

        expect(goBack(target, { fallback: songs }, withPrevious)).toBe(true)
        expect(target.back).toHaveBeenCalledOnce()
        expect(target.push).not.toHaveBeenCalled()
    })

    it('opens the overview', () => {
        const target = router()

        expect(goBack(target, { fallback: songs }, opened)).toBe(true)
        expect(target.push).toHaveBeenCalledWith(songs)
        expect(target.back).not.toHaveBeenCalled()
    })

    it('leaves closing to the caller', () => {
        const target = router()

        expect(goBack(target, {}, opened)).toBe(false)
        expect(target.back).not.toHaveBeenCalled()
        expect(target.push).not.toHaveBeenCalled()
    })
})
