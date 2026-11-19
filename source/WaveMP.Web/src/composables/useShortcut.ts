import { onActivated, onBeforeUnmount, onDeactivated, onMounted } from 'vue'
import { registerDefaultShortcuts, shortcutRegistry } from '@/shortcuts/defaults'
import type { ShortcutHandler } from '@/shortcuts/registry'

/**
 * Binds a handler to a keyboard shortcut while the component is shown (also when a page kept in
 * the background comes back). It comes before the handlers bound earlier; returning false hands
 * the key on to them.
 */
export function useShortcut(id: string, handler: ShortcutHandler): void {
    registerDefaultShortcuts()
    let release: (() => void) | null = null

    function bind(): void {
        release ??= shortcutRegistry.bind(id, handler)
    }

    function unbind(): void {
        release?.()
        release = null
    }

    onMounted(bind)
    onActivated(bind)
    onDeactivated(unbind)
    onBeforeUnmount(unbind)
}
