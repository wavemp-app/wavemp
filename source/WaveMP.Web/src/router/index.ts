import { createRouter, createWebHistory } from 'vue-router'
import { trackHistoryEntries } from './entries'
import { resolveAccess } from './guards'
import { createRoutes, devRoutes } from './routes'
import { contentAreaId, createScrollMemory } from './scroll'
import { useSessionStore } from '@/stores/session'

const scroll = createScrollMemory({ container: () => document.getElementById(contentAreaId) })

const router = createRouter({
    history: createWebHistory(import.meta.env.BASE_URL),
    routes: createRoutes(devRoutes),
    scrollBehavior: scroll.scrollBehavior,
})

// The position of the page that is left, before the guards can redirect or cancel the navigation.
router.beforeEach(() => {
    scroll.remember()
})

// Pinia is installed before the router, so the store is available for the first navigation.
router.beforeEach((to) => resolveAccess(to, useSessionStore()))

router.afterEach((to, _from, failure) => {
    if (!failure) {
        scroll.enter(to)
    }
})

/** The entries of the browser history; the app shell keeps their pages alive (see AppPage.vue). */
export const historyEntries = trackHistoryEntries(router)

export default router
