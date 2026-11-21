import { describe, expect, it } from 'vitest'
import { ref } from 'vue'
import { useSettingsLock } from '../useSettingsLock'

describe('useSettingsLock', () => {
    it('locks the form until the settings are loaded', () => {
        const settings = ref<{ value: number } | null>(null)
        const locked = useSettingsLock(ref(false), () => !!settings.value)

        expect(locked.value).toBe(true)

        settings.value = { value: 1 }

        expect(locked.value).toBe(false)
    })

    it('locks the form while it saves', () => {
        const saving = ref(false)
        const locked = useSettingsLock(saving, () => true)

        expect(locked.value).toBe(false)

        saving.value = true

        expect(locked.value).toBe(true)
    })
})
