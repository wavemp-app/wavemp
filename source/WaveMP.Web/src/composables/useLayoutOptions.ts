import { computed, onMounted, watch, type ComputedRef } from 'vue'
import { useMediaQuery } from '@/composables/useMediaQuery'
import { desktopMediaQuery } from '@/themes/breakpoints'
import {
    effectiveLayoutOptions,
    layoutOptions,
    syncLayoutOptions,
    type LayoutOptions,
} from '@/themes/layoutOptions'

/**
 * The layout options of the active theme that apply on the current width (THEMING.md, section
 * "Layout options"): those of the theme from the desktop breakpoint on, the defaults below it, so
 * phones keep the fixed mobile base. The options are read again when the component is mounted and
 * whenever the breakpoint is crossed, because a theme may set them inside its desktop media query.
 */
export function useLayoutOptions(): ComputedRef<LayoutOptions> {
    const desktop = useMediaQuery(desktopMediaQuery)

    onMounted(() => syncLayoutOptions())
    watch(desktop, () => syncLayoutOptions())

    return computed(() => effectiveLayoutOptions(layoutOptions.value, desktop.value))
}
