import { defineStore } from 'pinia'
import { ref } from 'vue'
import { apiClient } from '@/api/client'
import type { SubsonicAccess } from '@/api/generated/models'

export type SubsonicLoadStatus = 'idle' | 'loading' | 'ready' | 'error'

/**
 * The Subsonic access of the current profile: whether Subsonic apps may sign in and whether an
 * app password exists. A new app password is returned once by `createPassword` and never kept
 * here. Changes pass errors on to the page, which shows them.
 */
export const useSubsonicStore = defineStore('subsonic', () => {
    const access = ref<SubsonicAccess | null>(null)
    const status = ref<SubsonicLoadStatus>('idle')

    async function load(): Promise<void> {
        status.value = 'loading'
        try {
            access.value = (await apiClient.api.v1.me.subsonic.get()) ?? null
            status.value = 'ready'
        } catch {
            status.value = 'error'
        }
    }

    async function setEnabled(enabled: boolean): Promise<void> {
        access.value = (await apiClient.api.v1.me.subsonic.patch({ enabled })) ?? access.value
    }

    /** Creates a new app password, which replaces the earlier one; returns it for showing once. */
    async function createPassword(): Promise<string> {
        const created = await apiClient.api.v1.me.subsonic.password.post()
        access.value = created?.access ?? access.value
        return created?.password ?? ''
    }

    async function revokePassword(): Promise<void> {
        await apiClient.api.v1.me.subsonic.password.delete()
        if (access.value) {
            access.value = { ...access.value, hasPassword: false, passwordCreatedAt: null }
        }
    }

    return { access, status, load, setEnabled, createPassword, revokePassword }
})
