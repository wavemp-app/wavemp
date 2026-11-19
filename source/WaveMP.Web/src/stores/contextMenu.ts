import { defineStore } from 'pinia'
import { ref, shallowRef } from 'vue'
import type { MenuTarget } from '@/menus/targets'

export interface ContextMenuRequest {
    readonly target: MenuTarget
    readonly x: number
    readonly y: number
    /** Increases with every request, so the same target opens again. */
    readonly sequence: number
}

/**
 * The one context menu of the app: elements ask it to open for a target at a point, the menu host
 * of the shell shows it. Failures of an action (e.g. the songs of an album could not be loaded)
 * are kept as i18n key until the next page.
 */
export const useContextMenuStore = defineStore('contextMenu', () => {
    const request = shallowRef<ContextMenuRequest | null>(null)
    const error = ref<string | null>(null)
    let sequence = 0

    function open(target: MenuTarget, x: number, y: number): void {
        request.value = { target, x, y, sequence: ++sequence }
    }

    function fail(key: string): void {
        error.value = key
    }

    function clearError(): void {
        error.value = null
    }

    return { request, error, open, fail, clearError }
})
