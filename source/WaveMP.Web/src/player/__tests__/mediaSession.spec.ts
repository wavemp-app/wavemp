import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import {
    connectMediaSession,
    refreshMediaSession,
    updateMediaSession,
    type MediaSessionHandlers,
} from '../mediaSession'

class FakeMetadata {
    title: string
    artist: string
    album: string
    artwork: { src: string; sizes?: string }[]

    constructor(init: {
        title: string
        artist: string
        album: string
        artwork: { src: string; sizes?: string }[]
    }) {
        this.title = init.title
        this.artist = init.artist
        this.album = init.album
        this.artwork = init.artwork
    }
}

function fakeSession() {
    const handlers = new Map<string, MediaSessionActionHandler | null>()
    return {
        handlers,
        metadata: null as FakeMetadata | null,
        playbackState: 'none' as MediaSessionPlaybackState,
        setActionHandler: vi.fn((action: string, handler: MediaSessionActionHandler | null) => {
            if (action === 'stop') {
                throw new TypeError('unsupported')
            }
            handlers.set(action, handler)
        }),
        setPositionState: vi.fn(),
    }
}

describe('media session', () => {
    beforeEach(() => {
        vi.stubGlobal('MediaMetadata', FakeMetadata)
    })

    afterEach(() => {
        vi.unstubAllGlobals()
    })

    it('passes the buttons to the player and removes them again', () => {
        const session = fakeSession()
        const handlers: MediaSessionHandlers = {
            play: vi.fn(),
            pause: vi.fn(),
            stop: vi.fn(),
            previous: vi.fn(),
            next: vi.fn(),
            seekTo: vi.fn(),
        }

        const disconnect = connectMediaSession(handlers, session as unknown as MediaSession)
        session.handlers.get('play')?.({ action: 'play' })
        session.handlers.get('nexttrack')?.({ action: 'nexttrack' })
        session.handlers.get('seekto')?.({ action: 'seekto', seekTime: 12 })
        disconnect()

        expect(handlers.play).toHaveBeenCalledOnce()
        expect(handlers.next).toHaveBeenCalledOnce()
        expect(handlers.seekTo).toHaveBeenCalledWith(12)
        expect([...session.handlers.values()].every((handler) => handler === null)).toBe(true)
    })

    it('offers play, pause, previous, next and seeking, but no skipping by seconds', () => {
        const session = fakeSession()
        const handlers: MediaSessionHandlers = {
            play: vi.fn(),
            pause: vi.fn(),
            stop: vi.fn(),
            previous: vi.fn(),
            next: vi.fn(),
            seekTo: vi.fn(),
        }

        connectMediaSession(handlers, session as unknown as MediaSession)
        session.handlers.get('previoustrack')?.({ action: 'previoustrack' })
        session.handlers.get('pause')?.({ action: 'pause' })
        session.handlers.get('seekto')?.({ action: 'seekto' })

        expect([...session.handlers.keys()]).toEqual([
            'play',
            'pause',
            'previoustrack',
            'nexttrack',
            'seekto',
        ])
        expect(session.handlers.has('seekbackward')).toBe(false)
        expect(session.handlers.has('seekforward')).toBe(false)
        expect(handlers.previous).toHaveBeenCalledOnce()
        expect(handlers.pause).toHaveBeenCalledOnce()
        expect(handlers.seekTo).not.toHaveBeenCalled()
    })

    it('shows a new cover once its sizes are loaded', () => {
        const session = fakeSession()
        const state = { playback: 'playing', title: 'Song', position: 1, duration: 10 } as const

        updateMediaSession(state, session as unknown as MediaSession)
        const without = session.metadata
        updateMediaSession(
            { ...state, artwork: [{ src: 'blob:cover-96', sizes: '96x96' }] },
            session as unknown as MediaSession,
        )

        expect(without?.artwork).toEqual([])
        expect(session.metadata).not.toBe(without)
        expect(session.metadata?.artwork).toEqual([{ src: 'blob:cover-96', sizes: '96x96' }])
    })

    it('does nothing without the API', () => {
        expect(() => connectMediaSession({} as MediaSessionHandlers, null)()).not.toThrow()
        expect(() =>
            updateMediaSession({ playback: 'idle', position: 0, duration: 0 }, null),
        ).not.toThrow()
    })

    it('shows the track, its state and position', () => {
        const session = fakeSession()

        updateMediaSession(
            {
                playback: 'playing',
                title: 'Song',
                artist: 'Band',
                album: 'Album',
                artwork: [
                    { src: 'blob:cover-96', sizes: '96x96' },
                    { src: 'blob:cover-192', sizes: '192x192' },
                    { src: 'blob:cover-300', sizes: '300x300' },
                    { src: 'blob:cover-600', sizes: '600x600' },
                ],
                position: 12,
                duration: 10,
            },
            session as unknown as MediaSession,
        )

        expect(session.playbackState).toBe('playing')
        expect(session.metadata).toMatchObject({
            title: 'Song',
            artist: 'Band',
            album: 'Album',
            artwork: [
                { src: 'blob:cover-96', sizes: '96x96' },
                { src: 'blob:cover-192', sizes: '192x192' },
                { src: 'blob:cover-300', sizes: '300x300' },
                { src: 'blob:cover-600', sizes: '600x600' },
            ],
        })
        expect(session.setPositionState).toHaveBeenCalledWith({
            duration: 10,
            position: 10,
            playbackRate: 1,
        })
    })

    it('keeps the metadata while the track stays and clears it when idle', () => {
        const session = fakeSession()
        const state = { playback: 'paused', title: 'Song', position: 1, duration: 10 } as const

        updateMediaSession(state, session as unknown as MediaSession)
        const first = session.metadata
        updateMediaSession({ ...state, position: 2 }, session as unknown as MediaSession)
        expect(session.metadata).toBe(first)

        updateMediaSession(
            { playback: 'idle', position: 0, duration: 0 },
            session as unknown as MediaSession,
        )
        expect(session.metadata).toBeNull()
        expect(session.playbackState).toBe('none')
    })

    it('shows the state and the metadata again after the full screen of a video', () => {
        const session = fakeSession()
        const state = { playback: 'playing', title: 'Song', position: 1, duration: 10 } as const
        updateMediaSession(state, session as unknown as MediaSession)
        const first = session.metadata
        // The native full screen of the video showed its own state meanwhile.
        session.metadata = new FakeMetadata({ title: 'Video', artist: '', album: '', artwork: [] })
        session.playbackState = 'paused'

        refreshMediaSession(state, session as unknown as MediaSession)

        expect(session.metadata).not.toBe(first)
        expect(session.metadata.title).toBe('Song')
        expect(session.playbackState).toBe('playing')
    })
})
