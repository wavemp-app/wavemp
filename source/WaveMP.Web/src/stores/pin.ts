import { defineStore } from 'pinia'
import { ref } from 'vue'
import { apiClient } from '@/api/client'
import type { PinStatus } from '@/api/generated/models'

export type PinLoadStatus = 'idle' | 'loading' | 'ready' | 'error'

/**
 * The PIN of the current profile: whether it is set and whether ADMIN_PIN manages it. Setting and
 * removing pass errors on to the page, which shows them.
 */
export const usePinStore = defineStore('pin', () => {
    const isSet = ref(false)
    const isManagedByEnvironment = ref(false)
    const status = ref<PinLoadStatus>('idle')

    function apply(pin: PinStatus | undefined): void {
        isSet.value = !!pin?.isSet
        isManagedByEnvironment.value = !!pin?.isManagedByEnvironment
    }

    async function load(): Promise<void> {
        status.value = 'loading'
        try {
            apply(await apiClient.api.v1.me.pin.get())
            status.value = 'ready'
        } catch {
            status.value = 'error'
        }
    }

    /** Sets or changes the PIN; other devices of the profile have to enter it afterwards. */
    async function set(pin: string): Promise<void> {
        apply(await apiClient.api.v1.me.pin.put({ pin }))
        status.value = 'ready'
    }

    async function remove(): Promise<void> {
        await apiClient.api.v1.me.pin.delete()
        isSet.value = false
    }

    return { isSet, isManagedByEnvironment, status, load, set, remove }
})
