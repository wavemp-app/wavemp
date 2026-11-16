import { onActivated, onBeforeUnmount, onDeactivated, watch } from 'vue'
import { usePageTitleStore } from '@/stores/pageTitle'

/**
 * Shows the name of a detail page (album, artist, genre, year, song, playlist) as page title while
 * the page is shown: again when back and forward return to a page kept alive in the background,
 * never from a page in the background (e.g. a name that finishes loading after the page was left).
 */
export function useDetailTitle(title: () => string | null | undefined): void {
    const store = usePageTitleStore()
    let shown = true

    function apply(): void {
        if (shown) {
            store.set(title() ?? null)
        }
    }

    // A page dropped from the background while another page is shown leaves its title alone.
    function hide(): void {
        if (shown) {
            shown = false
            store.clear()
        }
    }

    watch(title, apply, { immediate: true })
    onActivated(() => {
        shown = true
        apply()
    })
    onDeactivated(hide)
    onBeforeUnmount(hide)
}
