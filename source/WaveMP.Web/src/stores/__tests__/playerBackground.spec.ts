import { createPinia, setActivePinia } from 'pinia'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import type { Track, TrackPlayback } from '@/api/generated/models'
import { FakeCastLayer } from '@/casting/__tests__/fakes'
import { getLyrics } from '@/lyrics/api'
import { getPlayback, recordPlay, reportNowPlaying } from '@/player/api'
import type { AudioEnvironment } from '@/player/engine'
import { PlaybackPathEngine } from '@/player/pathEngine'
import { castDependencies } from '../cast'
import { useCoversStore } from '../covers'
import { playerDependencies, usePlayerStore } from '../player'

vi.mock('@/lyrics/api', () => ({
    getLyrics: vi.fn(),
}))

vi.mock('@/player/api', () => ({
    getPlayback: vi.fn(),
    getVideoPlayback: vi.fn(),
    recordPlay: vi.fn(),
    reportNowPlaying: vi.fn(),
    getAutoplayTracks: vi.fn(() => Promise.resolve(undefined)),
}))

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

    advance(time: number, duration = 200): void {
        this.duration = duration
        this.currentTime = time
        this.dispatchEvent(new Event('timeupdate'))
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
    /** Whether the browser lets the graph run again (only within a gesture on iPhone and iPad). */
    resumable = true
    readonly resume = vi.fn(() => {
        if (this.resumable) {
            this.state = 'running'
        }

        return Promise.resolve()
    })
    readonly close = vi.fn(() => Promise.resolve())
    createGain = () => new FakeNode()
    createBiquadFilter = () => new FakeNode()
    createAnalyser = () => new FakeNode()
    createMediaElementSource = () => new FakeNode()

    /** The browser changes the state of the graph, e.g. interrupts it in the background. */
    change(state: string): void {
        this.state = state
        this.dispatchEvent(new Event('statechange'))
    }
}

/** Lets all pending promises of the store and the engine settle. */
async function settle(): Promise<void> {
    for (let round = 0; round < 3; round++) {
        await new Promise((resolve) => setTimeout(resolve, 0))
    }
}

function setVisibility(state: DocumentVisibilityState): void {
    Object.defineProperty(document, 'visibilityState', { value: state, configurable: true })
}

/** Simulates the page going to the background (visibilitychange) and lets the handover run. */
async function hide(): Promise<void> {
    setVisibility('hidden')
    document.dispatchEvent(new Event('visibilitychange'))
    await settle()
}

/** Simulates the page coming back (visibilitychange) and lets the handover run. */
async function show(): Promise<void> {
    setVisibility('visible')
    document.dispatchEvent(new Event('visibilitychange'))
    await settle()
}

const start = 1_700_000_000_000
const urlLifetime = 15 * 60_000

function track(id: string, extra: Partial<Track> = {}): Track {
    return {
        id,
        title: id.toUpperCase(),
        hasAudio: true,
        hasCover: true,
        durationMilliseconds: 200_000,
        artist: { id: 'ar', name: 'Band' },
        album: { id: 'al', title: 'Album' },
        ...extra,
    }
}

const ab = [track('a'), track('b')]

describe('player store in the background', () => {
    const originalPlayer = { ...playerDependencies }
    const originalCast = { ...castDependencies }
    let now: number
    let requests: number
    let context: FakeContext
    let decks: FakeAudio[]
    let supported: boolean

    beforeEach(() => {
        setActivePinia(createPinia())
        window.localStorage.clear()
        setVisibility('visible')
        now = start
        requests = 0
        supported = true
        decks = []
        context = new FakeContext()
        const environment: AudioEnvironment = {
            createAudio: () => {
                const audio = new FakeAudio()
                decks.push(audio)
                return audio as unknown as HTMLAudioElement
            },
            createContext: () => context as unknown as AudioContext,
            webAudio: true,
            setTimeout: (callback, milliseconds) =>
                window.setTimeout(callback, Math.min(milliseconds, 5)),
            clearTimeout: (handle) => window.clearTimeout(handle),
        }
        vi.mocked(getPlayback)
            .mockReset()
            .mockImplementation((id) => {
                requests++
                return Promise.resolve<TrackPlayback>({
                    trackId: id,
                    method: 'DirectPlay',
                    format: 'Mp3',
                    contentType: 'audio/mpeg',
                    streamUrl: `/stream/${id}?n=${requests}`,
                    durationMilliseconds: 200_000,
                    expiresAt: new Date(now + urlLifetime),
                })
            })
        vi.mocked(recordPlay).mockReset().mockResolvedValue(undefined)
        vi.mocked(reportNowPlaying).mockReset().mockResolvedValue(undefined)
        vi.mocked(getLyrics).mockReset().mockResolvedValue(null)
        // The real engine with both paths, on fake elements and a fake audio graph.
        playerDependencies.createEngine = (events, hooks) =>
            new PlaybackPathEngine(events, { supported: false, ...hooks, environment })
        playerDependencies.backgroundAudio = () => supported
        playerDependencies.audioFormats = () => ['Mp3']
        playerDependencies.random = () => 0
        playerDependencies.now = () => now
        playerDependencies.mediaSession = false
    })

    afterEach(() => {
        usePlayerStore().$dispose()
        Object.assign(playerDependencies, originalPlayer)
        Object.assign(castDependencies, originalCast)
        Reflect.deleteProperty(document, 'visibilityState')
        Reflect.deleteProperty(navigator, 'audioSession')
        Reflect.deleteProperty(navigator, 'mediaSession')
        vi.unstubAllGlobals()
    })

    /** The elements of the effects path (the first two) and of the plain path. */
    function paths() {
        const [effectsA, effectsB, plainA, plainB] = decks as [
            FakeAudio,
            FakeAudio,
            FakeAudio,
            FakeAudio,
        ]
        return { effects: [effectsA, effectsB], plain: [plainA, plainB], effectsA }
    }

    /** The element of a path that has a track loaded. */
    function loadedOn(path: 'effects' | 'plain'): FakeAudio | undefined {
        return paths()[path].find((deck) => deck.src !== '')
    }

    /** Plays the album and lets the first track reach the position. */
    async function playing(at = 42) {
        const player = usePlayerStore()
        player.playTracks(ab, 0)
        await settle()
        paths().effectsA.advance(at)
        return player
    }

    describe('on iPhone and iPad', () => {
        it('plays on through the plain path in the background and returns to the effects path', async () => {
            const player = await playing(42)
            expect(player.audioPath).toBe('effects')
            expect(player.analyser).not.toBeNull()

            await hide()

            const plain = loadedOn('plain')!
            expect(player.audioPath).toBe('plain')
            expect(player.state.audioPath).toBe('plain')
            expect(player.state.playback).toBe('playing')
            expect(plain.src).toBe('/stream/a?n=1')
            expect(plain.currentTime).toBe(42)
            expect(plain.paused).toBe(false)
            expect(loadedOn('effects')).toBeUndefined()
            // The visualization pauses: the plain path has no analyser.
            expect(player.analyser).toBeNull()

            plain.advance(50)
            expect(player.position).toBe(50)

            await show()

            const effects = loadedOn('effects')!
            expect(player.audioPath).toBe('effects')
            expect(player.state.audioPath).toBe('effects')
            expect(player.state.playback).toBe('playing')
            expect(effects.currentTime).toBe(50)
            expect(effects.paused).toBe(false)
            expect(loadedOn('plain')).toBeUndefined()
            expect(player.analyser).not.toBeNull()
            // The same playback all the way: no new request, no pause.
            expect(requests).toBe(1)
        })

        it('follows pagehide and pageshow as well', async () => {
            const player = await playing()

            window.dispatchEvent(new Event('pagehide'))
            await settle()
            expect(player.audioPath).toBe('plain')

            window.dispatchEvent(new Event('pageshow'))
            await settle()
            expect(player.audioPath).toBe('effects')
            expect(player.state.playback).toBe('playing')
        })

        it('starts an engine created in the background on the plain path', async () => {
            await hide()
            const player = usePlayerStore()

            player.playTracks(ab, 0)
            await settle()

            expect(player.audioPath).toBe('plain')
            expect(loadedOn('plain')?.paused).toBe(false)
            expect(loadedOn('effects')).toBeUndefined()
        })

        it('declares the audio as music playback once', () => {
            const session = { type: 'auto' }
            Object.defineProperty(navigator, 'audioSession', { value: session, configurable: true })

            usePlayerStore()

            expect(session.type).toBe('playback')
        })

        it('pauses on the return when the browser stopped the playback in the background', async () => {
            const player = await playing(42)
            await hide()
            // A long interruption: the browser stopped the element without telling the page.
            loadedOn('plain')!.paused = true

            await show()

            expect(player.state.playback).toBe('paused')
            expect(player.position).toBe(42)
            expect(player.audioPath).toBe('effects')
            expect(decks.every((deck) => deck.paused)).toBe(true)
            expect(loadedOn('effects')?.currentTime).toBe(42)
            expect(requests).toBe(1)
        })

        it('loads a paused track with an expired stream URL again on the return, paused at its position', async () => {
            const player = await playing(42)
            player.pause()
            await hide()
            now += urlLifetime + 60_000

            await show()

            expect(requests).toBe(2)
            expect(getPlayback).toHaveBeenLastCalledWith('a', ['Mp3'])
            expect(player.state.playback).toBe('paused')
            expect(player.state.loading).toBe(false)
            expect(player.position).toBe(42)
            const effects = loadedOn('effects')!
            expect(effects.src).toBe('/stream/a?n=2')
            expect(effects.paused).toBe(true)
            expect(loadedOn('plain')).toBeUndefined()

            // The next tap plays at once with the fresh URL.
            player.play()
            await settle()
            expect(requests).toBe(2)
            expect(effects.paused).toBe(false)
        })

        it('leaves a fresh paused track alone on the return', async () => {
            const player = await playing(42)
            player.pause()
            await hide()

            await show()

            expect(requests).toBe(1)
            expect(player.state.playback).toBe('paused')
            expect(loadedOn('effects')?.src).toBe('/stream/a?n=1')
        })

        it('waits for a gesture when the browser keeps the audio graph suspended on the return', async () => {
            const player = await playing(42)
            await hide()
            context.change('interrupted')
            context.resumable = false

            await show()

            expect(player.audioPath).toBe('plain')
            expect(loadedOn('plain')?.paused).toBe(false)
            expect(player.state.playback).toBe('playing')

            context.resumable = true
            document.dispatchEvent(new Event('click'))
            await settle()

            expect(player.audioPath).toBe('effects')
            expect(loadedOn('effects')?.paused).toBe(false)
            expect(loadedOn('plain')).toBeUndefined()

            // One gesture is enough: later ones do nothing.
            context.resume.mockClear()
            document.dispatchEvent(new Event('click'))
            expect(context.resume).not.toHaveBeenCalled()
        })

        it('plays the next track on the plain path while the page is hidden', async () => {
            const player = await playing(42)
            await hide()

            player.next()
            await settle()

            expect(player.current?.track.id).toBe('b')
            expect(player.audioPath).toBe('plain')
            expect(loadedOn('plain')?.src).toBe('/stream/b?n=2')
            expect(loadedOn('plain')?.paused).toBe(false)
            expect(loadedOn('effects')).toBeUndefined()
        })
    })

    describe('on other devices', () => {
        beforeEach(() => {
            supported = false
        })

        it('keeps the effects path in the background', async () => {
            const player = await playing(42)

            await hide()

            expect(player.audioPath).toBe('effects')
            expect(player.state.audioPath).toBe('effects')
            expect(paths().plain.every((deck) => deck.src === '')).toBe(true)
            expect(loadedOn('effects')?.paused).toBe(false)
            expect(player.analyser).not.toBeNull()

            await show()

            expect(player.audioPath).toBe('effects')
            expect(loadedOn('effects')?.paused).toBe(false)
            expect(requests).toBe(1)
        })

        it('does not declare an audio session', () => {
            const session = { type: 'auto' }
            Object.defineProperty(navigator, 'audioSession', { value: session, configurable: true })

            usePlayerStore()

            expect(session.type).toBe('auto')
        })

        it('plays a paused track with an expired stream URL with a fresh one', async () => {
            const player = await playing(30)
            player.pause()
            now += urlLifetime + 60_000

            player.play()
            await settle()

            expect(requests).toBe(2)
            expect(player.state.playback).toBe('playing')
            const effects = loadedOn('effects')!
            expect(effects.src).toBe('/stream/a?n=2')
            expect(effects.currentTime).toBe(30)
            expect(effects.paused).toBe(false)
        })

        it('resumes a paused track with a fresh stream URL without a request', async () => {
            const player = await playing(30)
            player.pause()
            now += 60_000

            player.play()
            await settle()

            expect(requests).toBe(1)
            expect(loadedOn('effects')?.paused).toBe(false)
        })
    })

    describe('while casting', () => {
        let layer: FakeCastLayer

        beforeEach(() => {
            layer = new FakeCastLayer()
            castDependencies.createLayer = () => layer
            playerDependencies.origin = () => 'http://wavemp.local'
        })

        it('leaves the device alone when the page goes to the background and comes back', async () => {
            const player = await playing(42)
            layer.connect('Living Room')
            await settle()
            expect(layer.loads).toHaveLength(1)

            await hide()
            await show()

            expect(layer.loads).toHaveLength(1)
            expect(layer.calls).toEqual([])
            expect(player.state.playback).toBe('playing')
            expect(player.state.audioPath).toBe('effects')
            expect(decks.every((deck) => deck.paused)).toBe(true)
        })

        it('moves a track from the plain path to the device at its position', async () => {
            const player = await playing(42)
            await hide()
            loadedOn('plain')!.advance(45)

            layer.connect('Living Room')
            await settle()

            expect(layer.loads[0]).toMatchObject({ position: 45, autoplay: true })
            expect(decks.every((deck) => deck.paused)).toBe(true)
            expect(player.state.audioPath).toBe('effects')
        })
    })

    describe('media session', () => {
        class FakeMetadata {
            constructor(readonly init: MediaMetadataInit) {}
        }

        function fakeSession() {
            return {
                metadata: null as FakeMetadata | null,
                playbackState: 'none',
                setActionHandler: vi.fn(),
                setPositionState: vi.fn(),
            }
        }

        it('shows the track with its cover in four sizes on both paths', async () => {
            vi.stubGlobal('MediaMetadata', FakeMetadata)
            const session = fakeSession()
            Object.defineProperty(navigator, 'mediaSession', { value: session, configurable: true })
            playerDependencies.mediaSession = true
            const covers = useCoversStore()
            const acquire = vi
                .spyOn(covers, 'acquire')
                .mockImplementation((_kind, id, size) => Promise.resolve(`blob:${id}-${size}`))
            const release = vi.spyOn(covers, 'release').mockImplementation(() => undefined)

            const player = await playing(42)
            await settle()

            expect(acquire.mock.calls.map(([, id, size]) => `${id}-${size}`)).toEqual([
                'a-96',
                'a-192',
                'a-300',
                'a-600',
            ])
            expect(session.metadata?.init).toMatchObject({
                title: 'A',
                artist: 'Band',
                album: 'Album',
                artwork: [
                    { src: 'blob:a-96', sizes: '96x96' },
                    { src: 'blob:a-192', sizes: '192x192' },
                    { src: 'blob:a-300', sizes: '300x300' },
                    { src: 'blob:a-600', sizes: '600x600' },
                ],
            })

            await hide()
            loadedOn('plain')!.advance(60)
            await settle()
            expect(session.playbackState).toBe('playing')
            expect(session.setPositionState).toHaveBeenLastCalledWith({
                duration: 200,
                position: 60,
                playbackRate: 1,
            })

            player.next()
            await settle()
            expect(release.mock.calls.map(([, id, size]) => `${id}-${size}`)).toEqual([
                'a-96',
                'a-192',
                'a-300',
                'a-600',
            ])
            expect(session.metadata?.init.title).toBe('B')
            expect(session.metadata?.init.artwork).toHaveLength(4)
        })
    })
})
