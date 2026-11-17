<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import BaseCover from '@/components/base/BaseCover.vue'
import { useInView } from '@/composables/useInView'
import { coverSizeFor, type CoverSize } from '@/library/covers'
import { useCoversStore, type CoverKind } from '@/stores/covers'

/**
 * Cover of an album, track or playlist from the API. It is loaded once it comes near the viewport,
 * in the size the theme shows it with (its width times the pixel density), and freed when the
 * component goes; until then, and without a cover, the placeholder of the theme shows. After a
 * change of a cover rule the store raises the revision of covers of albums and tracks, and the
 * cover is loaded again.
 */
const props = defineProps<{
    kind: CoverKind
    id: string | null | undefined
    /** Whether the API has a cover (hasCover); otherwise nothing is loaded. */
    available: boolean
    /** Version of the own cover of a playlist; a new version loads the new picture. */
    version?: string
}>()

const covers = useCoversStore()
const cover = ref<InstanceType<typeof BaseCover> | null>(null)
const visible = useInView(cover)
const src = ref<string | null>(null)
let held: { kind: CoverKind; id: string; size: CoverSize; version?: string } | null = null

/** The version of the picture in the store: of the own cover and of the cover rules. */
const version = computed(() => covers.versionOf(props.kind, props.version))

/** The size of the API for the shown width of the cover. */
function measure(): CoverSize {
    const element: unknown = cover.value?.$el
    const width = element instanceof Element ? element.getBoundingClientRect().width : 0
    const pixelRatio = typeof window === 'undefined' ? 1 : window.devicePixelRatio
    return coverSizeFor(width, pixelRatio)
}

/** Frees the held picture; a new version of the same cover keeps showing it until it is loaded. */
function release(keepPicture = false): void {
    if (held) {
        covers.release(held.kind, held.id, held.size, held.version)
        held = null
    }

    if (!keepPicture) {
        src.value = null
    }
}

/** Loads the cover once it is visible; runs after rendering, so the shown width is known. */
async function update(): Promise<void> {
    const { kind, id, available } = props
    const current = version.value
    if (held && (held.kind !== kind || held.id !== id || held.version !== current || !available)) {
        release(held.kind === kind && held.id === id && available)
    }

    if (!visible.value || !available || !id || held) {
        return
    }

    const wanted = { kind, id, size: measure(), version: current }
    held = wanted
    const url = await covers.acquire(kind, id, wanted.size, current)
    if (held === wanted) {
        src.value = url
    }
}

watch(
    () => [props.kind, props.id, props.available, version.value, visible.value] as const,
    update,
    { flush: 'post' },
)

onMounted(update)
onBeforeUnmount(() => release())
</script>

<template>
    <BaseCover ref="cover" :src="src" />
</template>
