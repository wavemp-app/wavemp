import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import { apiClient } from '@/api/client'
import type { CreateMediaSource, MediaSource, UpdateMediaSource } from '@/api/generated/models'
import { fetchAllPages } from '@/api/paging'

export type MediaSourcesStatus = 'idle' | 'loading' | 'ready' | 'error'

/**
 * The media folders (media sources) of the library: every profile reads them for the filter of the
 * library, admins manage them. Changes pass errors on to the page, which shows them, and update the
 * list with the answer of the server.
 */
export const useMediaSourcesStore = defineStore('mediaSources', () => {
    const sources = ref<MediaSource[]>([])
    const status = ref<MediaSourcesStatus>('idle')

    /** Whether the library has more than one media folder, so filtering by folder makes sense. */
    const hasSeveral = computed(() => sources.value.length > 1)

    async function load(): Promise<void> {
        status.value = 'loading'
        try {
            sources.value = await fetchAllPages((offset, limit) =>
                apiClient.api.v1.sources.get({ queryParameters: { offset, limit } }),
            )
            status.value = 'ready'
        } catch {
            status.value = 'error'
        }
    }

    /** Loads the folders once, e.g. for the filter of the library; later calls keep the list. */
    async function ensureLoaded(): Promise<void> {
        if (status.value === 'idle' || status.value === 'error') {
            await load()
        }
    }

    function find(id: string | null | undefined): MediaSource | undefined {
        return id ? sources.value.find((source) => source.id === id) : undefined
    }

    function put(source: MediaSource | undefined): void {
        if (!source?.id) {
            return
        }

        const index = sources.value.findIndex((item) => item.id === source.id)
        const list = [...sources.value]
        if (index >= 0) {
            list[index] = source
        } else {
            list.push(source)
        }
        sources.value = list.sort((a, b) =>
            (a.name ?? '').localeCompare(b.name ?? '', undefined, { sensitivity: 'base' }),
        )
    }

    async function create(input: CreateMediaSource): Promise<MediaSource | undefined> {
        const created = await apiClient.api.v1.sources.post(input)
        put(created)
        return created
    }

    async function update(id: string, input: UpdateMediaSource): Promise<MediaSource | undefined> {
        const updated = await apiClient.api.v1.sources.byId(id).patch(input)
        put(updated)
        return updated
    }

    async function remove(id: string): Promise<void> {
        await apiClient.api.v1.sources.byId(id).delete()
        sources.value = sources.value.filter((source) => source.id !== id)
    }

    return { sources, status, hasSeveral, load, ensureLoaded, find, create, update, remove }
})
