import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { defineComponent, h } from 'vue'
import { createMemoryHistory, createRouter } from 'vue-router'

const review = vi.hoisted(() => ({
    listTrackReviews: vi.fn(),
    getTrackReviewSummary: vi.fn(),
    getTrackReview: vi.fn(),
    setTrackReview: vi.fn(),
    setTrackReviews: vi.fn(),
}))

vi.mock('@/review/api', () => review)

const { default: AppHeader } = await import('../AppHeader.vue')
const { default: AppToolbar } = await import('../AppToolbar.vue')
const { createWaveMpI18n } = await import('@/i18n')
const { createRoutes } = await import('@/router/routes')
const { useSessionStore } = await import('@/stores/session')
const { defaultLayoutOptions, layoutOptions } = await import('@/themes/layoutOptions')

function stubDesktop(desktop: boolean): void {
    vi.stubGlobal(
        'matchMedia',
        (query: string) =>
            ({
                matches: query === '(width >= 64rem)' && desktop,
                addEventListener: () => undefined,
                removeEventListener: () => undefined,
            }) as unknown as MediaQueryList,
    )
}

/** Header and toolbar as the shell renders them, on the songs of the library. */
async function mountBars(role: 'Admin' | 'User' = 'User') {
    const router = createRouter({ history: createMemoryHistory(), routes: createRoutes() })
    await router.push('/library/songs')
    useSessionStore().$patch({
        token: 'token',
        sessionId: 'session',
        expiresAt: new Date(Date.now() + 60_000).toISOString(),
        profile: {
            id: 'profile',
            name: 'Anna',
            initials: 'A',
            color: '#2e7d32',
            role,
            isBuiltIn: false,
            hasPin: false,
            avatarVersion: null,
            avatarPresetId: null,
        },
        language: 'en',
    })
    const Bars = defineComponent({ setup: () => () => [h(AppHeader), h(AppToolbar)] })
    const wrapper = mount(Bars, {
        global: { plugins: [router, createWaveMpI18n('en')] },
        attachTo: document.body,
    })
    await flushPromises()
    return wrapper
}

function childParts(wrapper: Awaited<ReturnType<typeof mountBars>>, parent: string): string[] {
    return wrapper
        .findAll(`[data-part="${parent}"] > [data-part]`)
        .map((part) => part.attributes('data-part') ?? '')
}

describe('placement of the breadcrumb', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        review.getTrackReviewSummary.mockReset().mockResolvedValue({ unreviewedCount: 0 })
    })

    afterEach(() => {
        vi.unstubAllGlobals()
        document.documentElement.style.removeProperty('--wmp-layout-breadcrumb')
        layoutOptions.value = defaultLayoutOptions
        document.body.innerHTML = ''
    })

    it('stays in the toolbar while the theme sets no layout option', async () => {
        stubDesktop(true)
        const wrapper = await mountBars()

        expect(wrapper.findAll('[data-part="breadcrumb"]')).toHaveLength(1)
        expect(childParts(wrapper, 'toolbar')[0]).toBe('breadcrumb')
        expect(childParts(wrapper, 'header')).not.toContain('breadcrumb')
        wrapper.unmount()
    })

    it('moves into the header after the history buttons where the theme asks for it', async () => {
        stubDesktop(true)
        document.documentElement.style.setProperty('--wmp-layout-breadcrumb', 'header')
        const wrapper = await mountBars('Admin')

        expect(wrapper.findAll('[data-part="breadcrumb"]')).toHaveLength(1)
        const header = childParts(wrapper, 'header')
        expect(header.indexOf('breadcrumb')).toBe(header.indexOf('nav-history') + 1)
        expect(header.indexOf('tabs')).toBe(header.indexOf('breadcrumb') + 1)
        expect(childParts(wrapper, 'toolbar')).not.toContain('breadcrumb')
        expect(childParts(wrapper, 'toolbar')[0]).toBe('view-switch')

        const breadcrumb = wrapper.get('[data-part="header"] > [data-part="breadcrumb"]')
        expect(breadcrumb.attributes('aria-label')).toBe('Location')
        expect(
            breadcrumb
                .findAll('[data-part="breadcrumb-link"] [data-part="label"]')
                .map((label) => label.text()),
        ).toEqual(['Music', 'Library', 'Songs'])
        wrapper.unmount()
    })

    it('stays in the toolbar on phones, whatever the theme sets', async () => {
        stubDesktop(false)
        document.documentElement.style.setProperty('--wmp-layout-breadcrumb', 'header')
        const wrapper = await mountBars()

        expect(wrapper.findAll('[data-part="breadcrumb"]')).toHaveLength(1)
        expect(childParts(wrapper, 'toolbar')[0]).toBe('breadcrumb')
        expect(childParts(wrapper, 'header')).not.toContain('breadcrumb')
        wrapper.unmount()
    })
})
