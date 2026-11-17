<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { coverSizeFor, type CoverSize } from '@/library/covers'
import { useCoversStore } from '@/stores/covers'

/**
 * Avatar of a profile: its picture (uploaded or built-in) if it has one, otherwise its initials on
 * its color. The only place that renders a profile picture. The picture is loaded in the size the
 * theme shows the avatar with (its width times the pixel density); while it loads, and if it
 * cannot be loaded, the initials show. With a picture, the avatar has data-state="image".
 */
const props = defineProps<{
    initials: string
    /** Color of the profile as #rrggbb (runtime variable --wmp-profile-color). */
    color: string
    /** Id of the profile; together with avatarVersion it loads the picture. */
    userId?: string | null
    /** Version of the avatar of the profile; null or missing without an avatar. */
    avatarVersion?: string | null
    /** A picture to show instead of loading one, e.g. in the component overview. */
    image?: string | null
}>()

const avatar = ref<HTMLElement | null>(null)
const loaded = ref<string | null>(null)
const failedImage = ref<string | null>(null)
const src = computed(() => {
    const shown = props.image ?? loaded.value
    return shown && shown !== failedImage.value ? shown : null
})
let held: { id: string; size: CoverSize; version: string } | null = null

/** The size of the API for the shown width of the avatar. */
function measure(): CoverSize {
    const width = avatar.value?.getBoundingClientRect().width ?? 0
    const pixelRatio = typeof window === 'undefined' ? 1 : window.devicePixelRatio
    return coverSizeFor(width, pixelRatio)
}

function release(): void {
    if (held) {
        useCoversStore().release('avatar', held.id, held.size, held.version)
        held = null
    }

    loaded.value = null
}

async function update(): Promise<void> {
    const { userId, avatarVersion } = props
    if (held && (held.id !== userId || held.version !== avatarVersion)) {
        release()
    }

    if (!userId || !avatarVersion || held) {
        return
    }

    const current = { id: userId, size: measure(), version: avatarVersion }
    held = current
    const url = await useCoversStore().acquire('avatar', current.id, current.size, current.version)
    if (held === current) {
        loaded.value = url
    }
}

/** A picture the browser cannot show leaves the initials. */
function failed(): void {
    failedImage.value = src.value
}

watch(() => [props.userId, props.avatarVersion] as const, update, { flush: 'post' })
onMounted(update)
onBeforeUnmount(release)
</script>

<template>
    <span
        ref="avatar"
        data-part="avatar"
        aria-hidden="true"
        :data-state="src ? 'image' : undefined"
        :style="{ '--wmp-profile-color': color }"
    >
        <img
            v-if="src"
            data-part="avatar-image"
            :src="src"
            alt=""
            draggable="false"
            @error="failed"
        />
        <span v-else data-part="avatar-initials">{{ initials }}</span>
    </span>
</template>
