import { computed, type ComputedRef, type Ref } from 'vue'

/**
 * Whether the fields of a settings form are locked: while it saves and until its settings are
 * loaded. A field that could be changed before the settings arrive would get the loaded value
 * without notice, and a save would send that value back.
 */
export function useSettingsLock(
    saving: Readonly<Ref<boolean>>,
    loaded: () => boolean,
): ComputedRef<boolean> {
    return computed(() => saving.value || !loaded())
}
