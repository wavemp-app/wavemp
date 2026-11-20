import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, nextTick, ref, type Ref } from 'vue'
import { mount } from '@vue/test-utils'
import { layoutWidthOf, pixelRatioOf, useShownSize } from '../useShownSize'

type Report = (entries: { target: Element }[]) => void

/** A ResizeObserver that the test triggers by hand; counts how many were created. */
function stubResizeObserver() {
    const observer = {
        created: 0,
        observed: [] as Element[],
        report: (() => {}) as Report,
    }
    vi.stubGlobal(
        'ResizeObserver',
        class {
            constructor(callback: Report) {
                observer.created++
                observer.report = callback
            }

            observe(element: Element) {
                observer.observed.push(element)
            }

            unobserve(element: Element) {
                observer.observed = observer.observed.filter((other) => other !== element)
            }

            disconnect() {
                observer.observed = []
            }
        },
    )
    return observer
}

/** A matchMedia whose queries the test can change by hand. */
function stubMatchMedia() {
    const media = {
        queries: [] as string[],
        change: () => {},
    }
    vi.stubGlobal('matchMedia', (query: string) => {
        media.queries.push(query)
        return {
            matches: true,
            addEventListener: (_: string, listener: () => void) => {
                media.change = listener
            },
            removeEventListener: vi.fn(),
        }
    })
    return media
}

function mountProbes(count = 1) {
    const probes: { changes: Ref<number>; element: Ref<HTMLElement | null> }[] = []
    const wrapper = mount(
        defineComponent({
            setup() {
                return () =>
                    Array.from({ length: count }, (_, index) =>
                        h(
                            defineComponent({
                                setup() {
                                    const element = ref<HTMLElement | null>(null)
                                    probes[index] = { changes: useShownSize(element), element }
                                    return () => h('span', { ref: element })
                                },
                            }),
                        ),
                    )
            },
        }),
        { attachTo: document.body },
    )
    return { wrapper, probes }
}

describe('useShownSize', () => {
    afterEach(() => {
        vi.unstubAllGlobals()
        vi.restoreAllMocks()
    })

    it('counts the changes of the size of the element with one observer for all elements', async () => {
        const observer = stubResizeObserver()

        const { wrapper, probes } = mountProbes(3)
        await nextTick()

        expect(observer.created).toBe(1)
        expect(observer.observed).toHaveLength(3)
        expect(probes.map((probe) => probe.changes.value)).toEqual([0, 0, 0])

        observer.report([{ target: probes[1]!.element.value! }])
        expect(probes.map((probe) => probe.changes.value)).toEqual([0, 1, 0])

        wrapper.unmount()
        expect(observer.observed).toEqual([])
    })

    it('counts a change of the pixel density, e.g. on another screen', async () => {
        vi.stubGlobal('devicePixelRatio', 2)
        const media = stubMatchMedia()

        const { wrapper, probes } = mountProbes()
        await nextTick()
        expect(media.queries).toEqual(['(resolution: 2dppx)'])

        vi.stubGlobal('devicePixelRatio', 3)
        media.change()

        expect(probes[0]!.changes.value).toBe(1)
        expect(media.queries).toEqual(['(resolution: 2dppx)', '(resolution: 3dppx)'])
        wrapper.unmount()
    })

    it('works without ResizeObserver and matchMedia', async () => {
        vi.stubGlobal('ResizeObserver', undefined)
        vi.stubGlobal('matchMedia', undefined)

        const { wrapper, probes } = mountProbes()
        await nextTick()

        expect(probes[0]!.changes.value).toBe(0)
        wrapper.unmount()
    })
})

describe('layoutWidthOf', () => {
    afterEach(() => {
        vi.restoreAllMocks()
    })

    it('reads the layout width, which transforms such as an opening animation do not change', () => {
        const element = document.createElement('span')
        vi.spyOn(element, 'offsetWidth', 'get').mockReturnValue(320)
        vi.spyOn(element, 'getBoundingClientRect').mockReturnValue({ width: 32 } as DOMRect)

        expect(layoutWidthOf(element)).toBe(320)
        expect(layoutWidthOf(null)).toBe(0)
    })
})

describe('pixelRatioOf', () => {
    afterEach(() => {
        vi.unstubAllGlobals()
    })

    it('reads the pixel density of the window of the element, 1 if it is unknown', () => {
        vi.stubGlobal('devicePixelRatio', 3)
        expect(pixelRatioOf(document.createElement('span'))).toBe(3)
        expect(pixelRatioOf(null)).toBe(3)

        vi.stubGlobal('devicePixelRatio', Number.NaN)
        expect(pixelRatioOf(null)).toBe(1)
    })
})
