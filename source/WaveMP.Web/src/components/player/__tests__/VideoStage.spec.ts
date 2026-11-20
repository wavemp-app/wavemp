import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createWaveMpI18n } from '@/i18n'
import VideoStage from '../VideoStage.vue'

function mountStage(props: { active?: boolean; ambient?: boolean } = {}) {
    return mount(VideoStage, {
        props,
        global: { plugins: [createWaveMpI18n('en')] },
        attachTo: document.body,
    })
}

type Wrapper = ReturnType<typeof mountStage>

function ambient(wrapper: Wrapper) {
    return wrapper.get('[data-part="video-ambient"]')
}

function canvasStates(wrapper: Wrapper): (string | undefined)[] {
    return wrapper
        .findAll('[data-part="video-ambient-canvas"]')
        .map((canvas) => canvas.attributes('data-state'))
}

/** Lets the video element of the stage report a picture and whether it plays. */
async function showPicture(wrapper: Wrapper, playing: boolean): Promise<void> {
    const video = wrapper.get('[data-part="video"]').element as HTMLVideoElement
    Object.defineProperty(video, 'readyState', { configurable: true, value: 4 })
    Object.defineProperty(video, 'videoWidth', { configurable: true, value: 1280 })
    Object.defineProperty(video, 'videoHeight', { configurable: true, value: 720 })
    Object.defineProperty(video, 'paused', { configurable: true, value: !playing })
    video.dispatchEvent(new Event(playing ? 'playing' : 'loadeddata'))
    await flushPromises()
}

/** matchMedia that answers the query for reduced motion. */
function preferReducedMotion(reduced: boolean): void {
    vi.stubGlobal(
        'matchMedia',
        (query: string) =>
            ({
                matches: reduced && query.includes('prefers-reduced-motion'),
                addEventListener: () => undefined,
                removeEventListener: () => undefined,
            }) as unknown as MediaQueryList,
    )
}

describe('VideoStage ambient light', () => {
    const drawImage = vi.fn()
    let pixel = new Uint8ClampedArray([40, 80, 160, 255])

    beforeEach(() => {
        drawImage.mockReset()
        pixel = new Uint8ClampedArray([40, 80, 160, 255])
        vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockImplementation(
            () =>
                ({
                    drawImage,
                    getImageData: () => ({ data: pixel }),
                }) as unknown as CanvasRenderingContext2D,
        )
    })

    afterEach(() => {
        vi.restoreAllMocks()
        vi.unstubAllGlobals()
        document.body.innerHTML = ''
    })

    it('lies between the backdrop and the video with two hidden canvases', () => {
        const wrapper = mountStage()

        const parts = wrapper
            .get('[data-part="video-stage"]')
            .element.querySelectorAll(':scope > [data-part]')
        expect([...parts].map((element) => element.getAttribute('data-part'))).toEqual([
            'video-backdrop',
            'video-ambient',
            'video',
            'btn-fullscreen',
        ])
        expect(ambient(wrapper).attributes('aria-hidden')).toBe('true')
        expect(ambient(wrapper).attributes('data-state')).toBe('waiting')
        expect(canvasStates(wrapper)).toEqual([undefined, undefined])
    })

    it('draws the playing video and fades the newest picture in', async () => {
        const wrapper = mountStage()

        await showPicture(wrapper, true)

        expect(ambient(wrapper).attributes('data-state')).toBe('running')
        expect(drawImage).toHaveBeenCalledWith(expect.any(HTMLVideoElement), 0, 0, 32, 18)
        expect(canvasStates(wrapper)).toEqual(['current', 'previous'])
        wrapper.unmount()
    })

    it('keeps the last picture while the video is paused or its view is hidden', async () => {
        const wrapper = mountStage()
        await showPicture(wrapper, false)
        expect(ambient(wrapper).attributes('data-state')).toBe('paused')
        expect(drawImage).toHaveBeenCalledTimes(1)

        await wrapper.setProps({ active: false })
        await showPicture(wrapper, true)

        expect(ambient(wrapper).attributes('data-state')).toBe('paused')
        expect(drawImage).toHaveBeenCalledTimes(1)
        wrapper.unmount()
    })

    it('can be switched off by the profile', async () => {
        const wrapper = mountStage({ ambient: false })

        await showPicture(wrapper, true)

        expect(ambient(wrapper).attributes('data-state')).toBe('off')
        expect(drawImage).not.toHaveBeenCalled()

        await wrapper.setProps({ ambient: true })
        expect(ambient(wrapper).attributes('data-state')).toBe('running')
        wrapper.unmount()
    })

    it('shows one static color of the picture for reduced motion', async () => {
        preferReducedMotion(true)
        const wrapper = mountStage()

        await showPicture(wrapper, true)

        expect(ambient(wrapper).attributes('data-state')).toBe('static')
        const element = ambient(wrapper).element as HTMLElement
        expect(element.style.getPropertyValue('--wmp-video-ambient-color')).toBe('rgb(40 80 160)')
        expect(canvasStates(wrapper)).toEqual([undefined, undefined])
        wrapper.unmount()
    })

    it('forgets the pictures of a video when its source changes', async () => {
        const wrapper = mountStage()
        await showPicture(wrapper, true)

        const video = wrapper.get('[data-part="video"]').element as HTMLVideoElement
        Object.defineProperty(video, 'readyState', { configurable: true, value: 0 })
        video.dispatchEvent(new Event('emptied'))
        await flushPromises()

        expect(ambient(wrapper).attributes('data-state')).toBe('waiting')
        expect(canvasStates(wrapper)).toEqual([undefined, undefined])
        wrapper.unmount()
    })
})
