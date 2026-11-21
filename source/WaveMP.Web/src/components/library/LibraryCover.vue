<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import BaseCover from '@/components/base/BaseCover.vue'
import { useInView } from '@/composables/useInView'
import { layoutWidthOf, pixelRatioOf, useShownSize } from '@/composables/useShownSize'
import { coverSizeFor, type CoverSize } from '@/library/covers'
import { useCoversStore, type CoverKind } from '@/stores/covers'

/**
 * Cover of an album, track or playlist from the API. It is loaded once it comes near the viewport,
 * in the size the theme shows it with (its layout width times the pixel density of the screen),
 * and freed when the component goes; until then, and without a cover, the placeholder of the theme
 * shows. When the cover grows or moves to a screen with a higher density (e.g. the cover of the
 * mini player that becomes the full-screen player on phones), the larger picture is loaded and
 * replaces the smaller one once it is there; a cover that shrinks keeps its picture. After a
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

const emit = defineEmits<{
    /** The picture has been loaded (true) or the API has none (false), each time a load ends. */
    settled: [loaded: boolean]
}>()

interface Held {
    kind: CoverKind
    id: string
    size: CoverSize
    version?: string
}

const covers = useCoversStore()
const cover = ref<InstanceType<typeof BaseCover> | null>(null)
const visible = useInView(cover)
const resized = useShownSize(cover)
const src = ref<string | null>(null)
let held: Held | null = null

/** The version of the picture in the store: of the own cover and of the cover rules. */
const version = computed(() => covers.versionOf(props.kind, props.version))

/** The size of the API for the shown width of the cover. */
function measure(): CoverSize {
    const element: unknown = cover.value?.$el
    const shown = element instanceof Element ? element : null
    return coverSizeFor(layoutWidthOf(shown), pixelRatioOf(shown))
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

    if (!visible.value || !available || !id) {
        return
    }

    const size = measure()
    if (held && held.size >= size) {
        return
    }

    // A larger size of the same picture: the smaller one shows until the larger one is loaded.
    const previous = held
    const wanted: Held = { kind, id, size, version: current }
    held = wanted
    const loading = covers.acquire(kind, id, size, current)
    if (previous) {
        covers.release(previous.kind, previous.id, previous.size, previous.version)
    }

    const url = await loading
    if (held === wanted && (url || !previous)) {
        src.value = url
    }

    if (held === wanted) {
        emit('settled', !!src.value)
    }
}

watch(
    () =>
        [
            props.kind,
            props.id,
            props.available,
            version.value,
            visible.value,
            resized.value,
        ] as const,
    update,
    { flush: 'post' },
)

onMounted(update)
onBeforeUnmount(() => release())
</script>

<template>
    <BaseCover ref="cover" :src="src" />
</template>
