import { beforeEach, describe, expect, it, vi } from 'vitest'
import {
    browserHlsSupport,
    hlsContentType,
    MediaSourceSlot,
    type HlsAttachment,
    type HlsSupport,
} from '../hls'

/** An hls.js instance of the fake below. */
interface FakeHlsInstance {
    readonly handlers: Map<string, (event: string, data: unknown) => void>
    readonly loadSource: ReturnType<typeof vi.fn>
    readonly attachMedia: ReturnType<typeof vi.fn>
    readonly destroy: ReturnType<typeof vi.fn>
}

const fake = vi.hoisted(() => ({
    state: { supported: true, instances: [] as FakeHlsInstance[] },
}))

vi.mock('hls.js', () => {
    class Hls {
        static Events = { ERROR: 'hlsError' }
        static isSupported = () => fake.state.supported
        readonly handlers = new Map<string, (event: string, data: unknown) => void>()
        readonly loadSource = vi.fn()
        readonly attachMedia = vi.fn()
        readonly destroy = vi.fn()

        constructor(readonly config: unknown) {
            fake.state.instances.push(this)
        }

        on(event: string, handler: (event: string, data: unknown) => void): void {
            this.handlers.set(event, handler)
        }
    }

    return { default: Hls }
})

function media(canPlay = ''): HTMLMediaElement {
    const element = document.createElement('video')
    element.canPlayType = vi.fn(() => canPlay as CanPlayTypeResult)
    return element
}

describe('browserHlsSupport', () => {
    beforeEach(() => {
        fake.state.supported = true
        fake.state.instances.length = 0
    })

    it('plays through hls.js where Media Source Extensions exist', async () => {
        const element = media()
        const onError = vi.fn()

        const attachment = await browserHlsSupport.attach(element, '/master.m3u8', onError)

        const hls = fake.state.instances[0]!
        expect(hls.loadSource).toHaveBeenCalledWith('/master.m3u8')
        expect(hls.attachMedia).toHaveBeenCalledWith(element)
        attachment.destroy()
        expect(hls.destroy).toHaveBeenCalledOnce()
    })

    it('reports fatal errors only', async () => {
        const onError = vi.fn()
        await browserHlsSupport.attach(media(), '/master.m3u8', onError)
        const handler = fake.state.instances[0]!.handlers.get('hlsError')!

        handler('hlsError', { fatal: false, details: 'fragLoadError' })
        expect(onError).not.toHaveBeenCalled()
        handler('hlsError', { fatal: true, details: 'manifestLoadError' })
        expect(onError).toHaveBeenCalledOnce()
    })

    it('falls back to native HLS', async () => {
        fake.state.supported = false
        const element = media('maybe')

        const attachment = await browserHlsSupport.attach(element, '/master.m3u8', vi.fn())

        expect(element.canPlayType).toHaveBeenCalledWith(hlsContentType)
        expect(element.getAttribute('src')).toBe('/master.m3u8')
        attachment.destroy()
        expect(element.getAttribute('src')).toBeNull()
    })

    it('fails without any HLS support', async () => {
        fake.state.supported = false

        await expect(browserHlsSupport.attach(media(), '/master.m3u8', vi.fn())).rejects.toThrow()
    })
})

/** HLS support whose attachments the tests complete or fail. */
class ControlledHls implements HlsSupport {
    readonly attachments: {
        resolve: (attachment: HlsAttachment) => void
        reject: (error: unknown) => void
        destroy: ReturnType<typeof vi.fn<() => void>>
    }[] = []

    attach(): Promise<HlsAttachment> {
        return new Promise((resolve, reject) => {
            this.attachments.push({ resolve, reject, destroy: vi.fn<() => void>() })
        })
    }

    complete(index: number): void {
        const entry = this.attachments[index]!
        entry.resolve({ destroy: entry.destroy })
    }
}

describe('MediaSourceSlot', () => {
    let element: HTMLMediaElement
    let hls: ControlledHls
    let slot: MediaSourceSlot

    beforeEach(() => {
        element = media()
        element.load = vi.fn()
        hls = new ControlledHls()
        slot = new MediaSourceSlot(element, hls)
    })

    it('is ready at once for a file', async () => {
        slot.set('/song.mp3', false, vi.fn())

        expect(slot.pending).toBe(false)
        await expect(slot.ready()).resolves.toBe(true)
        expect(element.getAttribute('src')).toBe('/song.mp3')
    })

    it('removes the previous file while a stream attaches and is ready once it is attached', async () => {
        slot.set('/song.mp3', false, vi.fn())
        slot.set('/master.m3u8', true, vi.fn())

        expect(element.getAttribute('src')).toBeNull()
        expect(slot.pending).toBe(true)
        const ready = slot.ready()
        hls.complete(0)

        await expect(ready).resolves.toBe(true)
        expect(slot.pending).toBe(false)
    })

    it('is not ready when the stream fails to attach', async () => {
        const onError = vi.fn()
        slot.set('/master.m3u8', true, onError)
        const ready = slot.ready()
        hls.attachments[0]!.reject(new Error('no HLS'))

        await expect(ready).resolves.toBe(false)
        await expect(slot.ready()).resolves.toBe(false)
        expect(onError).toHaveBeenCalledOnce()
    })

    it('waits for the source that replaced the one it waited for', async () => {
        slot.set('/a.m3u8', true, vi.fn())
        const ready = slot.ready()
        slot.set('/b.m3u8', true, vi.fn())
        hls.complete(0)
        let settled = false
        void ready.then(() => {
            settled = true
        })
        await Promise.resolve()
        await Promise.resolve()
        expect(settled).toBe(false)
        expect(hls.attachments[0]!.destroy).toHaveBeenCalledOnce()

        hls.complete(1)

        await expect(ready).resolves.toBe(true)
    })
})
