import { shallowRef, watch, type ShallowRef } from 'vue'
import { useRouter, type RouteLocationNormalizedLoaded } from 'vue-router'

/**
 * The route of the page a component belongs to. It follows the changes of the query and the hash
 * of the page (sorting, the scope of the library, the search text) but not the other pages: a page
 * kept alive in the background (see AppPage.vue) keeps its route while another page is shown, so
 * its lists do not load again for the filters of that page. A new path is a new page.
 */
export function useViewRoute(): Readonly<ShallowRef<RouteLocationNormalizedLoaded>> {
    const router = useRouter()
    const route = shallowRef(router.currentRoute.value)

    watch(router.currentRoute, (current) => {
        if (current.path === route.value.path) {
            route.value = current
        }
    })

    return route
}
