import { describe, expect, it, vi } from 'vitest'
import type { AudioEnvironment, EngineEvents } from '../engine'
import type { PlaybackPath } from '../backgroundAudio'
import { PlaybackPathEngine } from '../pathEngine'

/** An audio element without a browser: the tests drive time, data and events. */
class FakeAudio extends EventTarget {
    src = ''
    currentTime = 0
    duration = Number.NaN
    readyState = 4
    seeking = false
    paused = true
    ended = false
    volume = 1
    muted = false
    preload = ''
    error: MediaError | null = null
    readonly play = vi.fn(() => {
        this.paused = false
        this.dispatchEvent(new Event('playing'))
        return Promise.resolve()
    })
    readonly pause = vi.fn(() => {
        if (!this.paused) {
            this.paused = true
            this.dispatchEvent(new Event('pause'))
        }
    })
    readonly load = vi.fn()

    getAttribute(name: string): string | null {
        return name === 'src' && this.src !== '' ? this.src : null
    }

    removeAttribute(name: string): void {
        if (name === 'src') {
            this.src = ''
        }
    }

    advance(time: number, duration = 10): void {
        this.duration = duration
        this.currentTime = time
        this.dispatchEvent(new Event('timeupdate'))
    }

    /** The element got enough data to play on. */
    ready(): void {
        this.readyState = 4
        this.dispatchEvent(new Event('canplay'))
    }
}

class FakeNode {
    readonly gain = {
        value: 1,
        setTargetAtTime: vi.fn(),
        setValueAtTime: vi.fn(),
        linearRampToValueAtTime: vi.fn(),
        cancelScheduledValues: vi.fn(),
        setValueCurveAtTime: vi.fn(),
    }
    readonly frequency = { value: 0, setTargetAtTime: vi.fn(), setValueAtTime: vi.fn() }
    readonly Q = { value: 0, setTargetAtTime: vi.fn(), setValueAtTime: vi.fn() }
    type = ''
    readonly connect = vi.fn()
}

class FakeContext extends EventTarget {
    state = 'suspended'
    currentTime = 0
    readonly destination = {}
    /** The elements routed through Web Audio. */
    readonly routed: unknown[] = []
    readonly resume = vi.fn(() => {
        this.state = 'running'
        return Promise.resolve()
    })
    readonly close = vi.fn(() => Promise.resolve())
    createGain = () => new FakeNode()
    createBiquadFilter = () => new FakeNode()
    createAnalyser = () => new FakeNode()
    createMediaElementSource = (element: unknown) => {
        this.routed.push(element)
        return new FakeNode()
    }

    /** The browser changes the state of the graph, e.g. interrupts it in the background. */
    change(state: string): void {
        this.state = state
        this.dispatchEvent(new Event('statechange'))
    }
}

/** Lets all pending promises of the engine settle. */
async function settle(): Promise<void> {
    await new Promise((resolve) => setTimeout(resolve, 0))
}

function setup(supported = true) {
    const decks: FakeAudio[] = []
    const timers: (() => void)[] = []
    const context = new FakeContext()
    const environment: AudioEnvironment = {
        createAudio: () => {
            const audio = new FakeAudio()
            decks.push(audio)
            return audio as unknown as HTMLAudioElement
        },
        createContext: () => context as unknown as AudioContext,
        webAudio: true,
        setTimeout: (callback) => timers.push(callback),
        clearTimeout: (handle) => {
            timers[handle - 1] = () => undefined
        },
    }
    const events = {
        timeupdate: vi.fn<(position: number, duration: number) => void>(),
        playing: vi.fn<() => void>(),
        paused: vi.fn<() => void>(),
        waiting: vi.fn<() => void>(),
        ended: vi.fn<() => void>(),
        advanced: vi.fn<() => void>(),
        error: vi.fn<(error: unknown) => void>(),
        interrupted: vi.fn<() => void>(),
    } satisfies Required<EngineEvents>
    const paths: PlaybackPath[] = []
    const pending: boolean[] = []
    const engine = new PlaybackPathEngine(events, {
        supported,
        environment,
        onPathChange: (path) => paths.push(path),
        onEffectsPending: (value) => pending.push(value),
    })
    // The effects path owns the first two elements, the plain path the other two.
    const [effectsA, effectsB, plainA, plainB] = decks as [
        FakeAudio,
        FakeAudio,
        FakeAudio,
        FakeAudio,
    ]
    return { engine, events, context, timers, paths, pending, effectsA, effectsB, plainA, plainB }
}

type Setup = ReturnType<typeof setup>

/** A track that plays on the effects path at the position. */
async function playing(s: Setup, position = 42): Promise<void> {
    s.engine.load({ url: '/a', gain: 1 })
    await s.engine.play()
    s.effectsA.advance(position, 200)
}

/** Simulates the page going to the background or coming back and lets the handover run. */
async function visibility(s: Setup, hidden: boolean): Promise<void> {
    s.engine.setPageHidden(hidden)
    await settle()
}

describe('PlaybackPathEngine', () => {
    it('plays through Web Audio in the foreground', async () => {
        const s = setup()
        await playing(s)

        expect(s.engine.path).toBe('effects')
        expect(s.effectsA.play).toHaveBeenCalledOnce()
        expect(s.engine.analyser).not.toBeNull()
        expect(s.engine.equalizerAvailable).toBe(true)
        expect(s.events.playing).toHaveBeenCalledOnce()
    })

    it('keeps the effects path in the background where the browser does not need the plain one', async () => {
        const s = setup(false)
        await playing(s)

        await visibility(s, true)

        expect(s.engine.path).toBe('effects')
        expect(s.paths).toEqual([])
        expect(s.plainA.src).toBe('')
        expect(s.plainA.load).not.toHaveBeenCalled()
        expect(s.effectsA.paused).toBe(false)
    })

    describe('in the background', () => {
        it('hands a playing track over to the plain path at its position without a gap', async () => {
            const s = setup()
            await playing(s, 42)
            s.plainA.readyState = 1

            s.engine.setPageHidden(true)
            await settle()
            expect(s.plainA.src).toBe('/a')
            expect(s.plainA.play).not.toHaveBeenCalled()
            expect(s.engine.path).toBe('effects')

            // The effects path plays on while the plain one loads; the plain one catches up.
            s.effectsA.advance(43.5, 200)
            s.plainA.ready()
            await settle()

            expect(s.plainA.currentTime).toBe(43.5)
            expect(s.plainA.play).toHaveBeenCalledOnce()
            expect(s.plainA.play.mock.invocationCallOrder[0]!).toBeLessThan(
                s.effectsA.pause.mock.invocationCallOrder.at(-1)!,
            )
            expect(s.effectsA.paused).toBe(true)
            expect(s.effectsA.src).toBe('')
            expect(s.engine.path).toBe('plain')
            expect(s.paths).toEqual(['plain'])
            expect(s.events.paused).not.toHaveBeenCalled()
        })

        it('plays the plain path without Web Audio, crossfade or visualization', async () => {
            const s = setup()
            await playing(s)
            await visibility(s, true)

            expect(s.context.routed).toEqual([s.effectsA, s.effectsB])
            expect(s.engine.analyser).toBeNull()
            // The store takes the analyser of the new path with the playing event.
            expect(s.events.playing).toHaveBeenCalledTimes(2)
            expect(s.engine.equalizerAvailable).toBe(true)
        })

        it('takes the prepared track along and changes to it gapless', async () => {
            const s = setup()
            await playing(s)
            s.engine.prepare({ url: '/b', gain: 1, crossfade: 5 })
            await visibility(s, true)

            expect(s.plainB.src).toBe('/b')
            expect(s.effectsB.src).toBe('')
            expect(s.engine.hasPrepared).toBe(true)

            // No crossfade without Web Audio: the next track starts right at the end.
            s.plainA.advance(9.5, 10)
            expect(s.timers).not.toHaveLength(0)
            s.timers.at(-1)!()
            expect(s.plainB.play).toHaveBeenCalledOnce()
            expect(s.events.advanced).toHaveBeenCalledOnce()
        })

        it('moves a paused track silently', async () => {
            const s = setup()
            await playing(s, 10)
            s.engine.pause()
            s.events.paused.mockClear()

            await visibility(s, true)

            expect(s.engine.path).toBe('plain')
            expect(s.plainA.src).toBe('/a')
            expect(s.engine.position).toBe(10)
            expect(s.plainA.play).not.toHaveBeenCalled()
            expect(s.effectsA.src).toBe('')
            expect(s.events.paused).not.toHaveBeenCalled()
        })

        it('only changes the path while nothing is loaded', async () => {
            const s = setup()

            await visibility(s, true)

            expect(s.engine.path).toBe('plain')
            expect(s.plainA.load).not.toHaveBeenCalled()
        })

        it('starts a new track right on the plain path', async () => {
            const s = setup()
            await visibility(s, true)

            s.engine.load({ url: '/b', gain: 1 })
            await s.engine.play()

            expect(s.plainA.src).toBe('/b')
            expect(s.plainA.play).toHaveBeenCalledOnce()
            expect(s.effectsA.src).toBe('')
        })

        it('switches when the browser interrupts the audio graph, also where the probe saw no need', async () => {
            const s = setup(false)
            await playing(s)
            await visibility(s, true)
            expect(s.engine.path).toBe('effects')

            s.context.change('interrupted')
            await settle()

            expect(s.engine.path).toBe('plain')
            expect(s.plainA.play).toHaveBeenCalledOnce()
            expect(s.effectsA.src).toBe('')
        })

        it('switches when the audio graph was interrupted before the page was hidden', async () => {
            const s = setup(false)
            await playing(s)
            s.context.state = 'interrupted'

            await visibility(s, true)

            expect(s.engine.path).toBe('plain')
        })

        it('ignores an interruption in the foreground where the probe saw no need', async () => {
            const s = setup(false)
            await playing(s)

            s.context.change('interrupted')
            await settle()

            expect(s.engine.path).toBe('effects')
        })

        it('stays on the effects path when the plain one does not start', async () => {
            const s = setup()
            await playing(s)
            s.plainA.play.mockRejectedValueOnce(new DOMException('blocked', 'NotAllowedError'))

            await visibility(s, true)

            expect(s.engine.path).toBe('effects')
            expect(s.effectsA.paused).toBe(false)
            expect(s.plainA.src).toBe('')
            expect(s.events.error).not.toHaveBeenCalled()
        })

        it('hands over within the time even when the plain path never reports data', async () => {
            const s = setup()
            await playing(s)
            s.plainA.readyState = 1

            s.engine.setPageHidden(true)
            await settle()
            s.timers.at(-1)!()
            await settle()

            expect(s.engine.path).toBe('plain')
            expect(s.plainA.play).toHaveBeenCalledOnce()
        })
    })

    describe('back in the foreground', () => {
        async function inBackground(): Promise<Setup> {
            const s = setup()
            await playing(s)
            await visibility(s, true)
            s.plainA.advance(60, 200)
            return s
        }

        it('hands the track back to the effects path with all features', async () => {
            const s = await inBackground()
            s.effectsA.play.mockClear()

            await visibility(s, false)

            expect(s.engine.path).toBe('effects')
            expect(s.paths).toEqual(['plain', 'effects'])
            expect(s.effectsA.src).toBe('/a')
            expect(s.effectsA.currentTime).toBe(60)
            expect(s.effectsA.play).toHaveBeenCalledOnce()
            expect(s.plainA.src).toBe('')
            expect(s.engine.analyser).not.toBeNull()
        })

        it('waits for a gesture when the browser keeps the audio graph suspended', async () => {
            const s = await inBackground()
            s.context.state = 'interrupted'
            s.context.resume.mockImplementation(() => Promise.resolve())

            await visibility(s, false)

            expect(s.engine.path).toBe('plain')
            expect(s.engine.effectsPending).toBe(true)
            expect(s.pending).toEqual([true])
            expect(s.plainA.paused).toBe(false)

            // A new track stays on the plain path, too, until the gesture.
            s.context.resume.mockImplementation(() => {
                s.context.state = 'running'
                return Promise.resolve()
            })
            s.engine.resumeEffects()
            await settle()

            expect(s.engine.effectsPending).toBe(false)
            expect(s.pending).toEqual([true, false])
            expect(s.engine.path).toBe('effects')
            expect(s.effectsA.paused).toBe(false)
        })

        it('waits no longer than the time for the audio graph', async () => {
            const s = await inBackground()
            s.context.state = 'interrupted'
            s.context.resume.mockImplementation(() => new Promise<void>(() => undefined))

            s.engine.setPageHidden(false)
            await settle()
            s.timers.at(-1)!()
            await settle()

            expect(s.engine.path).toBe('plain')
            expect(s.engine.effectsPending).toBe(true)
        })

        it('moves a paused track back silently', async () => {
            const s = await inBackground()
            s.engine.pause()
            s.effectsA.play.mockClear()

            await visibility(s, false)

            expect(s.engine.path).toBe('effects')
            expect(s.effectsA.src).toBe('/a')
            expect(s.engine.position).toBe(60)
            expect(s.effectsA.play).not.toHaveBeenCalled()
        })
    })

    describe('commands during a handover', () => {
        /** A handover to the plain path that waits for the data of the plain path. */
        async function handingOver(): Promise<Setup> {
            const s = setup()
            await playing(s)
            s.plainA.readyState = 1
            s.engine.setPageHidden(true)
            await settle()
            expect(s.plainA.src).toBe('/a')
            return s
        }

        it('cancel it when a new track is loaded, which starts on the wanted path', async () => {
            const s = await handingOver()

            s.engine.load({ url: '/b', gain: 1 })
            s.plainA.ready()
            await s.engine.play()
            await settle()

            expect(s.engine.path).toBe('plain')
            expect(s.plainA.src).toBe('/b')
            expect(s.plainA.play).toHaveBeenCalledOnce()
            expect(s.effectsA.src).toBe('')
        })

        it('start it again at the new position after a seek', async () => {
            const s = await handingOver()

            s.engine.seek(5)
            expect(s.effectsA.currentTime).toBe(5)
            s.plainA.ready()
            await settle()

            expect(s.engine.path).toBe('plain')
            expect(s.plainA.currentTime).toBe(5)
            expect(s.plainA.play).toHaveBeenCalledOnce()
        })

        it('move the track silently after a pause', async () => {
            const s = await handingOver()

            s.engine.pause()
            await settle()

            expect(s.engine.path).toBe('plain')
            expect(s.plainA.src).toBe('/a')
            expect(s.plainA.play).not.toHaveBeenCalled()
            expect(s.effectsA.src).toBe('')
        })

        it('start it again with the next track when the current one advances', async () => {
            const s = await handingOver()
            s.engine.prepare({ url: '/b', gain: 1 })

            s.effectsA.advance(199.6, 200)
            s.timers.at(-1)!()
            expect(s.events.advanced).toHaveBeenCalledOnce()
            await settle()
            s.plainA.ready()
            await settle()

            expect(s.engine.path).toBe('plain')
            expect(s.plainA.src).toBe('/b')
            expect(s.plainA.play).toHaveBeenCalledOnce()
        })

        it('end when the page comes back before it completed', async () => {
            const s = await handingOver()

            await visibility(s, false)
            s.plainA.ready()
            await settle()

            expect(s.engine.path).toBe('effects')
            expect(s.paths).toEqual([])
            expect(s.plainA.src).toBe('')
            expect(s.plainA.play).not.toHaveBeenCalled()
            expect(s.effectsA.paused).toBe(false)
        })

        it('stop both paths on a stop and keep the wanted one', async () => {
            const s = await handingOver()

            s.engine.stop()
            await settle()

            expect(s.engine.path).toBe('plain')
            expect(s.plainA.src).toBe('')
            expect(s.effectsA.src).toBe('')
        })
    })

    it('forwards volume, track gain, equalizer and the sleep timer fade to both paths', async () => {
        const s = setup()
        await playing(s)
        s.engine.setVolume(0.5)
        s.engine.setTrackGain(0.5)
        await visibility(s, true)

        expect(s.plainA.volume).toBe(0.25)

        s.engine.fadeOut(0.1)
        s.timers.at(-1)!()
        expect(s.plainA.volume).toBe(0)
        expect(s.plainA.muted).toBe(true)

        s.engine.restoreOutput()
        expect(s.plainA.volume).toBe(0.25)
        expect(s.plainA.muted).toBe(false)
    })

    it('drops the events of the path that does not play', async () => {
        const s = setup()
        await playing(s)
        await visibility(s, true)
        const updates = s.events.timeupdate.mock.calls.length

        s.engine.load({ url: '/b', gain: 1 })
        s.effectsA.advance(3)
        s.effectsA.dispatchEvent(new Event('error'))

        expect(s.events.timeupdate.mock.calls.length).toBe(updates)
        expect(s.events.error).not.toHaveBeenCalled()
    })

    it('unlocks the plain path within the first play where it is needed', async () => {
        const s = setup()
        s.engine.load({ url: '/a', gain: 1 })
        await s.engine.play()
        await s.engine.play()

        expect(s.plainA.load).toHaveBeenCalledOnce()
        expect(s.plainB.load).toHaveBeenCalledOnce()
        expect(s.plainA.play).not.toHaveBeenCalled()

        const desktop = setup(false)
        desktop.engine.load({ url: '/a', gain: 1 })
        await desktop.engine.play()
        desktop.engine.unlock()
        expect(desktop.plainA.load).not.toHaveBeenCalled()
    })
})
