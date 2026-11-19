import { onBeforeUnmount, onMounted, watch, type Ref } from 'vue'
import { bindPlayerShortcuts } from '@/shortcuts/actions'
import { registerDefaultShortcuts, shortcutRegistry } from '@/shortcuts/defaults'
import { useMiniPlayerStore } from '@/stores/miniPlayer'
import { usePlayerStore } from '@/stores/player'
import { useShortcutsStore } from '@/stores/shortcuts'

/** The search field of the toolbar. */
const searchInputSelector =
    "[data-part='app'] [data-part='toolbar'] [data-part='search'] [data-part='input']"

/** The document a key event happened in: the app or the floating mini player window. */
function documentOf(event: KeyboardEvent): Document {
    const target = event.target as Node | null
    return target?.ownerDocument ?? (target as Document | null) ?? document
}

/**
 * The keyboard shortcuts of the app shell: listens to the keys in the app and in the floating
 * mini player window and binds the default shortcuts (src/shortcuts/defaults.ts) to the player,
 * the search field of the toolbar, the full screen of the app and the overview. The search, the
 * full screen and the overview need the app: in the floating window and while the mini player
 * replaces the app, their keys do what they do otherwise.
 */
export function useKeyboardShortcuts(appHidden: Readonly<Ref<boolean>>): void {
    registerDefaultShortcuts()
    const player = usePlayerStore()
    const miniPlayer = useMiniPlayerStore()
    const shortcuts = useShortcutsStore()

    /** Whether the key was pressed in the visible app (not in the mini player). */
    function inApp(event: KeyboardEvent): boolean {
        return documentOf(event) === document && !appHidden.value
    }

    const unbind = [
        bindPlayerShortcuts(shortcutRegistry, player),
        shortcutRegistry.bind('search', (event) => {
            const input = inApp(event)
                ? document.querySelector<HTMLInputElement>(searchInputSelector)
                : null
            if (!input || input.getClientRects().length === 0) {
                return false
            }

            input.focus()
            input.select()
        }),
        // The Now Playing stage binds its own full screen for the video and the visualization; this
        // one shows the whole app in full screen and leaves any full screen.
        shortcutRegistry.bind('fullscreen', (event) => {
            if (!inApp(event)) {
                return false
            }

            if (document.fullscreenElement) {
                void document.exitFullscreen().catch(() => undefined)
                return
            }

            const root = document.documentElement
            if (!document.fullscreenEnabled || typeof root.requestFullscreen !== 'function') {
                return false
            }

            void root.requestFullscreen().catch(() => undefined)
        }),
        shortcutRegistry.bind('shortcuts', (event) => {
            if (!inApp(event)) {
                return false
            }

            shortcuts.toggleOverview()
        }),
    ]

    function onKeydown(event: KeyboardEvent): void {
        shortcutRegistry.handle(event)
    }

    // The floating mini player window has a document of its own.
    let floatingDocument: Document | null = null
    function listenToFloating(next: Document | null): void {
        floatingDocument?.removeEventListener('keydown', onKeydown)
        floatingDocument = next && next !== document ? next : null
        floatingDocument?.addEventListener('keydown', onKeydown)
    }

    watch(() => miniPlayer.target?.ownerDocument ?? null, listenToFloating)

    onMounted(() => {
        document.addEventListener('keydown', onKeydown)
        listenToFloating(miniPlayer.target?.ownerDocument ?? null)
    })

    onBeforeUnmount(() => {
        document.removeEventListener('keydown', onKeydown)
        listenToFloating(null)
        unbind.forEach((release) => release())
        shortcuts.overviewOpen = false
    })
}
