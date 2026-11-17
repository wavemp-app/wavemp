import { computed, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { setLocale } from '@/i18n'
import { appLocale } from '@/i18n/locales'
import { useSessionStore } from '@/stores/session'

/**
 * Keeps the language of the app in line with the selected profile: its stored language applies
 * once a profile is selected (English if the web app has no translation for it); before that (and
 * after switching profiles) the language of the browser, otherwise English. Call it once, in
 * App.vue.
 */
export function useLocaleSync(): void {
    // useI18n only works during setup, so the composer is taken here and not in the watcher.
    const composer = useI18n({ useScope: 'global' })
    const session = useSessionStore()

    const locale = computed(() => appLocale(session.isAuthenticated ? session.language : null))

    watch(locale, (value) => setLocale(composer, value), { immediate: true })
}
