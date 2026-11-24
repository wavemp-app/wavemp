<script setup lang="ts">
import { onBeforeUnmount, ref, watch } from 'vue'
import { fetchOwnAlbumCover } from '@/admin/ownAlbumApi'
import BaseCover from '@/components/base/BaseCover.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'

/**
 * Cover of an own album: the uploaded picture, a picture chosen but not uploaded yet (file), or
 * the placeholder with the icon own-album. A new version (coverUpdatedAt) loads the picture again.
 */
const props = withDefaults(
    defineProps<{
        id?: string | null
        hasCover?: boolean
        version?: string | null
        /** A picture chosen in the editor that is shown instead of the uploaded one. */
        file?: Blob | null
    }>(),
    { id: null, hasCover: false, version: null, file: null },
)

const src = ref<string | null>(null)
let objectUrl: string | null = null
let request = 0

function release(): void {
    if (objectUrl) {
        URL.revokeObjectURL(objectUrl)
        objectUrl = null
    }
}

function show(blob: Blob | null): void {
    release()
    objectUrl = blob ? URL.createObjectURL(blob) : null
    src.value = objectUrl
}

async function update(): Promise<void> {
    const own = ++request
    if (props.file) {
        show(props.file)
        return
    }

    if (!props.id || !props.hasCover) {
        show(null)
        return
    }

    try {
        const picture = await fetchOwnAlbumCover(props.id)
        if (own === request) {
            show(picture ? new Blob([picture]) : null)
        }
    } catch {
        if (own === request) {
            show(null)
        }
    }
}

watch(() => [props.id, props.hasCover, props.version, props.file] as const, update, {
    immediate: true,
})
onBeforeUnmount(() => {
    request++
    release()
})
</script>

<template>
    <BaseCover :src="src" :placeholder="false" data-kind="own-album">
        <BaseIcon name="own-album" />
    </BaseCover>
</template>
