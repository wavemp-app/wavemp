import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { miniPlayerWindowSize, type DocumentPictureInPicture } from '@/player/miniPlayer'
import { miniPlayerDependencies, miniPlayerRootId, useMiniPlayerStore } from '../miniPlayer'

/** A floating window: its own document, focus, close and the event of closing. */
class FakeWindow extends EventTarget {
    readonly document = document.implementation.createHTMLDocument('')
    readonly focus = vi.fn()
    readonly close = vi.fn(() => this.dispatchEvent(new Event('pagehide')))
}

describe('mini player store', () => {
    const original = { ...miniPlayerDependencies }
    let floating: FakeWindow
    let requestWindow: ReturnType<typeof vi.fn<DocumentPictureInPicture['requestWindow']>>

    beforeEach(() => {
        setActivePinia(createPinia())
        floating = new FakeWindow()
        requestWindow = vi.fn(() => Promise.resolve(floating as unknown as Window))
        document.head.innerHTML = '<link rel="stylesheet" href="/themes/midnight/theme.css">'
        document.title = 'WaveMP'
    })

    afterEach(() => {
        Object.assign(miniPlayerDependencies, original)
        document.head.innerHTML = ''
    })

    function withFloatingWindows(): void {
        miniPlayerDependencies.pictureInPicture = () => ({ requestWindow })
    }

    it('opens a floating window with the styles of the app where the browser can', async () => {
        withFloatingWindows()
        const miniPlayer = useMiniPlayerStore()

        await miniPlayer.open()

        expect(requestWindow).toHaveBeenCalledWith(miniPlayerWindowSize)
        expect(miniPlayer.mode).toBe('window')
        expect(miniPlayer.isOpen).toBe(true)
        expect(miniPlayer.target?.id).toBe(miniPlayerRootId)
        expect(floating.document.body.contains(miniPlayer.target)).toBe(true)
        expect(floating.document.title).toBe('WaveMP')
        expect(floating.document.head.querySelector('link')?.getAttribute('href')).toBe(
            `${window.location.origin}/themes/midnight/theme.css`,
        )
    })

    it('shows the compact view in the app where the browser has no floating window', async () => {
        miniPlayerDependencies.pictureInPicture = () => null
        const miniPlayer = useMiniPlayerStore()

        await miniPlayer.open()

        expect(miniPlayer.mode).toBe('inline')
        expect(miniPlayer.target).toBeNull()

        miniPlayer.close()
        expect(miniPlayer.mode).toBe('closed')
    })

    it('shows the compact view when the browser refuses the floating window', async () => {
        withFloatingWindows()
        requestWindow.mockRejectedValue(new DOMException('No user activation', 'NotAllowedError'))
        const miniPlayer = useMiniPlayerStore()

        await miniPlayer.open()

        expect(miniPlayer.mode).toBe('inline')
        expect(miniPlayer.target).toBeNull()
    })

    it('closes the floating window and forgets it', async () => {
        withFloatingWindows()
        const miniPlayer = useMiniPlayerStore()
        await miniPlayer.toggle()

        await miniPlayer.toggle()

        expect(floating.close).toHaveBeenCalledOnce()
        expect(miniPlayer.mode).toBe('closed')
        expect(miniPlayer.target).toBeNull()
    })

    it('notices when the user closes the floating window itself', async () => {
        withFloatingWindows()
        const miniPlayer = useMiniPlayerStore()
        await miniPlayer.open()

        floating.dispatchEvent(new Event('pagehide'))

        expect(miniPlayer.mode).toBe('closed')
        expect(miniPlayer.target).toBeNull()
    })

    it('opens one window only and brings it to the front instead', async () => {
        withFloatingWindows()
        const miniPlayer = useMiniPlayerStore()
        await Promise.all([miniPlayer.open(), miniPlayer.open()])

        await miniPlayer.open()

        expect(requestWindow).toHaveBeenCalledOnce()
        expect(floating.focus).toHaveBeenCalled()
    })
})
