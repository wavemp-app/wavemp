import { beforeEach, describe, expect, it, vi } from 'vitest'
import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
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
const { createWaveMpI18n } = await import('@/i18n')
const { createRoutes } = await import('@/router/routes')
const { useSessionStore } = await import('@/stores/session')

async function mountHeader(role: 'Admin' | 'User') {
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
    const wrapper = mount(AppHeader, { global: { plugins: [router, createWaveMpI18n('en')] } })
    await flushPromises()
    return wrapper
}

describe('AppHeader', () => {
    beforeEach(() => {
        setActivePinia(createPinia())
        review.getTrackReviewSummary.mockReset().mockResolvedValue({ unreviewedCount: 5 })
    })

    it('shows the review inbox with the number of unreviewed songs to admins', async () => {
        const wrapper = await mountHeader('Admin')

        const inbox = wrapper.get('[data-part="header"] > [data-part="btn-review-inbox"]')
        expect(inbox.get('[data-part="badge"]').text()).toBe('5')
        // Before the server status, so the order of the header matches the base layout.
        const order = wrapper
            .findAll('[data-part="header"] > [data-part]')
            .map((part) => part.attributes('data-part'))
        expect(order.indexOf('btn-review-inbox')).toBe(order.indexOf('server-status') - 1)
        wrapper.unmount()
    })

    it('hides the review inbox from other profiles', async () => {
        const wrapper = await mountHeader('User')

        expect(wrapper.find('[data-part="btn-review-inbox"]').exists()).toBe(false)
        expect(review.getTrackReviewSummary).not.toHaveBeenCalled()
        wrapper.unmount()
    })
})
