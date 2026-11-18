import { defineStore } from 'pinia'
import { ref } from 'vue'
import { apiClient } from '@/api/client'
import type { ScrobbleService, ScrobblingService } from '@/api/generated/models'

export type ScrobblingLoadStatus = 'idle' | 'loading' | 'ready' | 'error'

/**
 * The scrobbling services of the current profile with their connection. Connecting and
 * disconnecting pass errors on to the page, which shows them.
 */
export const useScrobblingStore = defineStore('scrobbling', () => {
    const services = ref<ScrobblingService[]>([])
    const status = ref<ScrobblingLoadStatus>('idle')

    function replace(service: ScrobblingService | undefined): void {
        if (!service?.service) {
            return
        }

        const index = services.value.findIndex((item) => item.service === service.service)
        if (index < 0) {
            services.value = [...services.value, service]
        } else {
            services.value = services.value.map((item, position) =>
                position === index ? service : item,
            )
        }
    }

    async function load(): Promise<void> {
        status.value = 'loading'
        try {
            const scrobbling = await apiClient.api.v1.me.scrobbling.get()
            services.value = scrobbling?.services ?? []
            status.value = 'ready'
        } catch {
            status.value = 'error'
        }
    }

    /**
     * Connects a service: the token of the Last.fm authorization or the personal token of
     * ListenBrainz. The server keeps only what it needs, encrypted.
     */
    async function connect(service: ScrobbleService, token: string): Promise<void> {
        replace(await apiClient.api.v1.me.scrobbling.byService(service).put({ token }))
    }

    /** Disconnects a service; plays that were not sent yet are dropped. */
    async function disconnect(service: ScrobbleService): Promise<void> {
        await apiClient.api.v1.me.scrobbling.byService(service).delete()
        await load()
    }

    return { services, status, load, connect, disconnect }
})
