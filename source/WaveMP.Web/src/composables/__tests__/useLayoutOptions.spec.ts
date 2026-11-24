import { afterEach, describe, expect, it, vi } from 'vitest'
import { defineComponent, h, nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import { useLayoutOptions } from '../useLayoutOptions'
import { defaultLayoutOptions, layoutOptions } from '@/themes/layoutOptions'

/** A window that is as wide as the desktop or not; `resize` crosses the breakpoint. */
function stubWidth(desktop: boolean) {
    const listeners: (() => void)[] = []
    let matches = desktop
    vi.stubGlobal(
        'matchMedia',
        (query: string) =>
            ({
                get matches() {
                    return query === '(width >= 64rem)' && matches
                },
                addEventListener: (_: string, listener: () => void) => listeners.push(listener),
                removeEventListener: () => undefined,
            }) as unknown as MediaQueryList,
    )
    return {
        resize(toDesktop: boolean) {
            matches = toDesktop
            listeners.forEach((listener) => listener())
        },
    }
}

function mountUser() {
    let options: ReturnType<typeof useLayoutOptions> | undefined
    const wrapper = mount(
        defineComponent({
            setup() {
                options = useLayoutOptions()
                return () => h('div')
            },
        }),
    )
    return { wrapper, options: () => options!.value }
}

describe('useLayoutOptions', () => {
    afterEach(() => {
        vi.unstubAllGlobals()
        document.documentElement.style.removeProperty('--wmp-layout-breadcrumb')
        layoutOptions.value = defaultLayoutOptions
    })

    it('applies the options of the theme on the desktop', () => {
        stubWidth(true)
        document.documentElement.style.setProperty('--wmp-layout-breadcrumb', 'header')

        const { wrapper, options } = mountUser()

        expect(options().breadcrumb).toBe('header')
        wrapper.unmount()
    })

    it('keeps the defaults below the desktop breakpoint', () => {
        stubWidth(false)
        document.documentElement.style.setProperty('--wmp-layout-breadcrumb', 'header')

        const { wrapper, options } = mountUser()

        expect(options()).toEqual(defaultLayoutOptions)
        wrapper.unmount()
    })

    it('reads the options again when the breakpoint is crossed', async () => {
        const width = stubWidth(false)
        const { wrapper, options } = mountUser()
        expect(options().breadcrumb).toBe('toolbar')

        // The theme sets the option inside its desktop media query.
        document.documentElement.style.setProperty('--wmp-layout-breadcrumb', 'header')
        width.resize(true)
        await nextTick()
        expect(options().breadcrumb).toBe('header')

        width.resize(false)
        await nextTick()
        expect(options().breadcrumb).toBe('toolbar')
        wrapper.unmount()
    })
})
