import { describe, expect, it, vi } from 'vitest'
import { AudioEngine, type AudioEnvironment, type EngineEvents } from '../engine'

/** An audio element that records what the engine does with it. */
class FakeAudio extends EventTarget {
    src = ''
    currentTime = 0
    duration = Number.NaN
    readyState = 4
    seeking = false
    paused = true
    ended = false
    volume = 1
    preload = ''
    error: MediaError | null = null
    readonly play = vi.fn(() => {
        this.paused = false
        this.dispatchEvent(new Event('playing'))
        return Promise.resolve()
    })
    readonly pause = vi.fn(() => {
        this.paused = true
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
    readonly resume = vi.fn(() => {
        this.state = 'running'
        return Promise.resolve()
    })
    readonly close = vi.fn(() => Promise.resolve())
    createGain = () => new FakeNode()
    createBiquadFilter = () => new FakeNode()
    createAnalyser = () => new FakeNode()
    createMediaElementSource = () => new FakeNode()
}

function setup(withContext: boolean) {
    const decks: FakeAudio[] = []
    const context = withContext ? new FakeContext() : null
    const environment: AudioEnvironment = {
        createAudio: () => {
            const audio = new FakeAudio()
            decks.push(audio)
            return audio as unknown as HTMLAudioElement
        },
        createContext: () => context as unknown as AudioContext | null,
        webAudio: withContext,
        setTimeout: () => 0,
        clearTimeout: () => undefined,
    }
    const events: EngineEvents = { playing: vi.fn(), paused: vi.fn() }
    return { engine: new AudioEngine(events, environment), decks, context, events }
}

describe('AudioEngine.unlock', () => {
    it('resumes the audio graph and loads the empty decks without playing', () => {
        const { engine, decks, context, events } = setup(true)
        engine.unlock()

        expect(context?.resume).toHaveBeenCalledOnce()
        expect(decks.every((deck) => deck.load.mock.calls.length === 1)).toBe(true)
        expect(decks.every((deck) => deck.play.mock.calls.length === 0)).toBe(true)
        expect(events.playing).not.toHaveBeenCalled()
    })

    it('leaves a loaded deck as it is and works without Web Audio', async () => {
        const { engine, decks } = setup(false)
        engine.load({ url: '/a', gain: 1 })
        const loads = decks.map((deck) => deck.load.mock.calls.length)
        engine.unlock()

        expect(decks[0]?.load.mock.calls.length).toBe(loads[0])
        expect(decks[1]?.load.mock.calls.length).toBe((loads[1] ?? 0) + 1)
        expect(decks[0]?.src).toBe('/a')
        await engine.play()
        expect(decks[0]?.play).toHaveBeenCalledOnce()
    })
})
