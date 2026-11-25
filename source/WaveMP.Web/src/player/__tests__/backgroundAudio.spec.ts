import { describe, expect, it, vi } from 'vitest'
import {
    applyAudioSession,
    backgroundPathSupported,
    pageLifecycle,
    wantedPath,
    type LifecycleDocument,
} from '../backgroundAudio'

describe('backgroundPathSupported', () => {
    const iPhone = { webAudio: true, maxTouchPoints: 5, gestureEvents: true }

    it('recognizes WebKit on a touch device by its gesture events', () => {
        expect(backgroundPathSupported(iPhone)).toBe(true)
    })

    it('leaves out a Mac, Android, desktops and browsers without Web Audio', () => {
        expect(backgroundPathSupported({ ...iPhone, maxTouchPoints: 0 })).toBe(false)
        expect(backgroundPathSupported({ ...iPhone, gestureEvents: false })).toBe(false)
        expect(backgroundPathSupported({ ...iPhone, webAudio: false })).toBe(false)
    })

    it('reads the capabilities of the browser', () => {
        // jsdom has neither gesture events nor touch points.
        expect(backgroundPathSupported()).toBe(false)
    })
})

describe('wantedPath', () => {
    it('wants the plain path only in the background where it is needed', () => {
        expect(wantedPath({ supported: true, hidden: true, interrupted: false })).toBe('plain')
        expect(wantedPath({ supported: true, hidden: false, interrupted: false })).toBe('effects')
        expect(wantedPath({ supported: false, hidden: true, interrupted: false })).toBe('effects')
    })

    it('wants the plain path in the background after the browser interrupted the audio graph', () => {
        expect(wantedPath({ supported: false, hidden: true, interrupted: true })).toBe('plain')
        expect(wantedPath({ supported: false, hidden: false, interrupted: true })).toBe('effects')
    })
})

describe('applyAudioSession', () => {
    it('declares the audio as playback where the browser has an audio session', () => {
        const target = { audioSession: { type: 'auto' } }

        expect(applyAudioSession(target)).toBe(true)
        expect(target.audioSession.type).toBe('playback')
    })

    it('does nothing without one or when the browser refuses it', () => {
        expect(applyAudioSession({})).toBe(false)
        expect(applyAudioSession(undefined)).toBe(false)
        const refusing = {
            audioSession: {
                get type(): string {
                    return 'auto'
                },
                set type(_value: string) {
                    throw new Error('not allowed')
                },
            },
        }
        expect(applyAudioSession(refusing)).toBe(false)
    })
})

describe('pageLifecycle', () => {
    class FakeDocument extends EventTarget implements LifecycleDocument {
        visibilityState: DocumentVisibilityState = 'visible'

        change(state: DocumentVisibilityState): void {
            this.visibilityState = state
            this.dispatchEvent(new Event('visibilitychange'))
        }
    }

    function setup() {
        const document = new FakeDocument()
        const window = new EventTarget()
        const onChange = vi.fn<(hidden: boolean) => void>()
        const stop = pageLifecycle(document, window, onChange)
        return { document, window, onChange, stop }
    }

    it('reports the page going to the background and coming back', () => {
        const s = setup()

        s.document.change('hidden')
        s.document.change('hidden')
        s.document.change('visible')

        expect(s.onChange.mock.calls).toEqual([[true], [false]])
    })

    it('treats pagehide as the background and pageshow as the return', () => {
        const s = setup()

        s.window.dispatchEvent(new Event('pagehide'))
        s.document.change('hidden')
        // Back from the back/forward cache: the document is visible again.
        s.document.visibilityState = 'visible'
        s.window.dispatchEvent(new Event('pageshow'))

        expect(s.onChange.mock.calls).toEqual([[true], [false]])
    })

    it('stops listening', () => {
        const s = setup()
        s.stop()

        s.document.change('hidden')
        s.window.dispatchEvent(new Event('pagehide'))

        expect(s.onChange).not.toHaveBeenCalled()
    })
})
