import { computed, watch } from 'vue'
import { useMediaQuery } from '@/composables/useMediaQuery'
import { useSessionStore } from '@/stores/session'
import { defaultThemeChoice, type ThemeChoice } from '@/themes/choice'
import { previewThemeId } from '@/themes/preview'
import {
    applyTheme,
    darkModeMediaQuery,
    resolveThemeId,
    resolveThemeVariant,
    themeById,
    type ThemeVariant,
} from '@/themes/themes'

/** The theme and variant to show for a choice of a profile and the mode of the device. */
export function shownTheme(
    choice: ThemeChoice,
    systemDark: boolean,
    preview: string = previewThemeId,
): { id: string; variant: ThemeVariant } {
    const theme = themeById(resolveThemeId(preview || choice.id))
    const appearance = preview ? 'system' : choice.appearance
    return { id: theme.id, variant: resolveThemeVariant(theme, appearance, systemDark) }
}

/**
 * Keeps the theme of the app in line with the selected profile: its theme and appearance apply as
 * soon as they are known or changed, without a reload; before a profile is selected the default
 * theme. The appearance "system" follows the light or dark mode of the device while the app is
 * open. Call it once, in App.vue.
 */
export function useThemeSync(): void {
    const session = useSessionStore()
    const systemDark = useMediaQuery(darkModeMediaQuery)

    const shown = computed(() =>
        shownTheme(session.isAuthenticated ? session.theme : defaultThemeChoice, systemDark.value),
    )

    watch(shown, ({ id, variant }) => applyTheme(id, variant), { immediate: true })
}
