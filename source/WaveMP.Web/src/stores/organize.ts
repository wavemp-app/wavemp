import { defineStore } from 'pinia'
import { computed, ref } from 'vue'
import {
    OrganizePreviewKindObject,
    type OrganizeAvailability,
    type OrganizeOperation,
    type OwnAlbum,
} from '@/api/generated/models'
import { blockedReason, isOperationActive } from '@/admin/organize'
import {
    applyOrganizeOperation,
    cancelOrganizeOperation,
    createOrganizePreview,
    createOrganizeUndoPreview,
    discardOrganizeOperation,
    getOrganizeAvailability,
    getOrganizeOperation,
    listOrganizeOperations,
} from '@/admin/organizeApi'
import { listOwnAlbums } from '@/admin/ownAlbumApi'
import { fetchAllPages } from '@/api/paging'
import { usePolling } from '@/composables/usePolling'

export type LoadStatus = 'idle' | 'loading' | 'ready' | 'error'

/** How often the state of an operation that plans or moves files is asked for. */
export const organizePollIntervalMs = 1000

/** Entries of the log per page. */
export const organizeLogPageSize = 20

/**
 * The organization of the media files for the admin pages: whether the media folders can be
 * written, the own albums, the log of the operations and the operation the page follows. While
 * that operation computes its preview or moves files, the store asks for its state every second;
 * once it has ended, the availability and the log are loaded again.
 */
export const useOrganizeStore = defineStore('organize', () => {
    const availability = ref<OrganizeAvailability | null>(null)
    const availabilityStatus = ref<LoadStatus>('idle')
    const ownAlbums = ref<OwnAlbum[]>([])
    const ownAlbumsStatus = ref<LoadStatus>('idle')
    const operations = ref<OrganizeOperation[]>([])
    const operationsTotal = ref(0)
    const logStatus = ref<LoadStatus>('idle')
    /** The operation the page follows: a preview of the library being computed, or a run. */
    const current = ref<OrganizeOperation | null>(null)
    /** Songs chosen in a menu for a new own album; the editor takes them once. */
    const pendingTrackIds = ref<string[]>([])

    const polling = usePolling(refresh, organizePollIntervalMs)
    const active = computed(() => isOperationActive(current.value?.status))
    /** Why nothing can be organized now (all sources), or null. */
    const blocked = computed(() => blockedReason(availability.value))
    const hasMoreOperations = computed(() => operations.value.length < operationsTotal.value)

    function replace(operation: OrganizeOperation): void {
        const index = operations.value.findIndex((item) => item.id === operation.id)
        if (index >= 0) {
            operations.value.splice(index, 1, operation)
        }
    }

    /** Follows the operation; while it plans or moves files, its state is polled. */
    function follow(operation: OrganizeOperation | null | undefined): void {
        current.value = operation ?? null
        if (operation) {
            replace(operation)
        }

        if (isOperationActive(operation?.status)) {
            polling.start()
        } else {
            polling.stop()
        }
    }

    async function refresh(): Promise<void> {
        const id = current.value?.id
        if (!id) {
            polling.stop()
            return
        }

        const operation = (await getOrganizeOperation(id)) ?? null
        if (current.value?.id !== id) {
            return
        }

        follow(operation)
        if (!isOperationActive(operation?.status)) {
            await Promise.all([
                loadAvailability(),
                loadOperations(),
                loadOwnAlbums().catch(() => {}),
            ])
        }
    }

    /** Loads whether the files can be organized; follows an operation that is queued or running. */
    async function loadAvailability(): Promise<void> {
        if (!availability.value) {
            availabilityStatus.value = 'loading'
        }

        try {
            availability.value = (await getOrganizeAvailability()) ?? null
            availabilityStatus.value = 'ready'
        } catch {
            availabilityStatus.value = 'error'
            return
        }

        const activeId = availability.value?.activeOperationId
        if (activeId && current.value?.id !== activeId) {
            follow(await getOrganizeOperation(activeId).catch(() => null))
        }
    }

    async function loadOwnAlbums(): Promise<void> {
        if (ownAlbums.value.length === 0) {
            ownAlbumsStatus.value = 'loading'
        }

        try {
            ownAlbums.value = await fetchAllPages((offset, limit) =>
                listOwnAlbums({ offset, limit }),
            )
            ownAlbumsStatus.value = 'ready'
        } catch {
            ownAlbumsStatus.value = 'error'
        }
    }

    /** Loads the first page of the log again, or the next page (reset false). */
    async function loadOperations(options: { reset?: boolean } = {}): Promise<void> {
        const reset = options.reset ?? true
        logStatus.value = 'loading'
        try {
            const page = await listOrganizeOperations({
                offset: reset ? 0 : operations.value.length,
                limit: organizeLogPageSize,
            })
            const items = page?.items ?? []
            operations.value = reset ? items : [...operations.value, ...items]
            operationsTotal.value = page?.total ?? operations.value.length
            logStatus.value = 'ready'
        } catch {
            logStatus.value = 'error'
        }
    }

    /** Starts the preview of the whole library or of one source; it is computed in the background. */
    async function previewLibrary(sourceId?: string): Promise<OrganizeOperation | null> {
        const operation = await createOrganizePreview({
            kind: OrganizePreviewKindObject.Library,
            ...(sourceId ? { sourceId } : {}),
        })
        follow(operation)
        return operation ?? null
    }

    /** Computes the preview of an own album; it is ready at once. */
    async function previewOwnAlbum(id: string): Promise<OrganizeOperation | null> {
        return (
            (await createOrganizePreview({
                kind: OrganizePreviewKindObject.OwnAlbum,
                ownAlbumId: id,
            })) ?? null
        )
    }

    /** Computes the preview that undoes an operation of the log. */
    async function previewUndo(id: string): Promise<OrganizeOperation | null> {
        return (await createOrganizeUndoPreview(id)) ?? null
    }

    /** Moves the files of a preview; the store follows the run. */
    async function apply(id: string, skipConflicts: boolean): Promise<OrganizeOperation | null> {
        const operation = (await applyOrganizeOperation(id, skipConflicts)) ?? null
        if (operation && availability.value) {
            availability.value = { ...availability.value, activeOperationId: operation.id }
        }

        follow(operation)
        return operation
    }

    /** Asks the run to stop after the song it is moving; polling shows when it has stopped. */
    async function cancel(id: string): Promise<void> {
        const operation = await cancelOrganizeOperation(id)
        if (operation && current.value?.id === id) {
            current.value = operation
        }
    }

    /** Throws a preview away. */
    async function discard(id: string): Promise<void> {
        await discardOrganizeOperation(id)
        operations.value = operations.value.filter((operation) => operation.id !== id)
        if (current.value?.id === id) {
            follow(null)
        }
    }

    /** Stops following the current operation, e.g. after its preview was shown. */
    function forgetCurrent(): void {
        follow(null)
    }

    /** Takes the songs chosen in a menu for a new own album; they are given out once. */
    function takePendingTracks(): string[] {
        const ids = pendingTrackIds.value
        pendingTrackIds.value = []
        return ids
    }

    return {
        availability,
        availabilityStatus,
        ownAlbums,
        ownAlbumsStatus,
        operations,
        operationsTotal,
        logStatus,
        current,
        pendingTrackIds,
        active,
        blocked,
        hasMoreOperations,
        polling: polling.active,
        follow,
        refresh,
        loadAvailability,
        loadOwnAlbums,
        loadOperations,
        previewLibrary,
        previewOwnAlbum,
        previewUndo,
        apply,
        cancel,
        discard,
        forgetCurrent,
        takePendingTracks,
    }
})
