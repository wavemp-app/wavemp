<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import type { PlaylistCover } from '@/api/generated/models'
import BaseCover from '@/components/base/BaseCover.vue'
import BaseIcon from '@/components/base/BaseIcon.vue'
import LibraryCover from '@/components/library/LibraryCover.vue'
import { useInView } from '@/composables/useInView'
import {
    applyChange,
    canRotate,
    coverChangeGate,
    coverLoadTimeoutMilliseconds,
    coverSlideTimeoutMilliseconds,
    coversStandStill,
    nextChangeDelay,
    planChange,
    skipChange,
    startRotation,
    type CoverChange,
    type CoverEdge,
    type CoverRotation,
} from '@/playlists/coverGrid'
import { coverViewOf } from '@/playlists/presentation'
import type { IconSlotName } from '@/themes/iconSlots'

/**
 * Cover of a playlist: its own picture, otherwise a 2x2 grid of the covers of its tracks
 * (playlist-cover-tile), otherwise the placeholder with the icon. With more than four covers the
 * tiles change one at a time: the new cover (a playlist-cover-slide) waits invisibly until its
 * picture is loaded (state loading), then slides in over the old one from an outer edge of its
 * tile (state entering, data-from), and becomes the shown one (state active). The theme does the
 * movement in CSS; with reduced motion the base crossfades instead. The tiles only change while
 * the cover is in view and the page is shown, and one change at a time on the whole page.
 */
const props = withDefaults(
    defineProps<{
        id: string | null | undefined
        cover: PlaylistCover | null | undefined
        /** Icon of the placeholder, e.g. the symbol of smart playlists. */
        icon?: IconSlotName
    }>(),
    { icon: 'playlist' },
)

type SlideState = 'active' | 'loading' | 'entering'

interface Slide {
    readonly key: number
    readonly trackId: string
    state: SlideState
    readonly from?: CoverEdge
    readonly change?: CoverChange
}

const view = computed(() => coverViewOf(props.cover))
const trackIds = computed(() => (view.value.kind === 'tracks' ? view.value.trackIds : []))
const grid = ref<HTMLElement | null>(null)
const visible = useInView(grid)
const pageShown = ref(typeof document === 'undefined' || document.visibilityState !== 'hidden')

/** The slides of each tile: the shown one, and while it changes the new one above it. */
const tiles = ref<Slide[][]>([])
const rotating = computed(
    () => tiles.value.length > 0 && visible.value && pageShown.value && !coversStandStill(),
)
let rotation: CoverRotation = startRotation([])
let nextKey = 0
let timer: ReturnType<typeof setTimeout> | null = null
const pending = new Map<number, ReturnType<typeof setTimeout>>()

function slide(trackId: string, state: SlideState, change?: CoverChange): Slide {
    nextKey += 1
    return { key: nextKey, trackId, state, from: change?.from, change }
}

function stopTimer(): void {
    if (timer !== null) {
        clearTimeout(timer)
        timer = null
    }
}

function clearPending(tile: number): void {
    const waiting = pending.get(tile)
    if (waiting !== undefined) {
        clearTimeout(waiting)
        pending.delete(tile)
    }
}

function reset(ids: readonly string[]): void {
    stopTimer()
    for (const waiting of pending.values()) {
        clearTimeout(waiting)
    }
    pending.clear()

    rotation = startRotation(ids)
    tiles.value = rotation.tiles.map((trackId) => [slide(trackId, 'active')])
    if (rotating.value) {
        schedule(nextChangeDelay(Math.random, true))
    }
}

function schedule(delay: number): void {
    stopTimer()
    if (rotating.value && canRotate(rotation)) {
        timer = setTimeout(change, delay)
    }
}

function busyTiles(): Set<number> {
    return new Set(tiles.value.flatMap((slides, index) => (slides.length > 1 ? [index] : [])))
}

/** Starts the next change: the new cover is added to its tile and loads. */
function change(): void {
    timer = null
    if (!rotating.value) {
        return
    }

    const wait = coverChangeGate.claim(Date.now())
    if (wait > 0) {
        schedule(wait + Math.round(Math.random() * 500))
        return
    }

    const planned = planChange(rotation, Math.random, busyTiles())
    if (planned) {
        const { tile, trackId } = planned.change
        rotation = planned.rotation
        tiles.value[tile]?.push(slide(trackId, 'loading', planned.change))
        pending.set(
            tile,
            setTimeout(() => settled(tile, trackId, false), coverLoadTimeoutMilliseconds),
        )
    }

    schedule(nextChangeDelay(Math.random))
}

/** The picture of a slide was loaded (or not): a waiting slide enters, or the change is dropped. */
function settled(tile: number, trackId: string, loaded: boolean): void {
    const slides = tiles.value[tile]
    const incoming = slides?.find((item) => item.state === 'loading' && item.trackId === trackId)
    if (!slides || !incoming?.change) {
        return
    }

    clearPending(tile)
    if (!loaded) {
        rotation = skipChange(rotation, incoming.change)
        slides.splice(slides.indexOf(incoming), 1)
        return
    }

    rotation = applyChange(rotation, incoming.change)
    incoming.state = 'entering'
    pending.set(
        tile,
        setTimeout(() => entered(tile, incoming.key), coverSlideTimeoutMilliseconds),
    )
}

/** The new cover is in place: it is the shown one, the covers below it go. */
function entered(tile: number, key: number): void {
    const slides = tiles.value[tile]
    const incoming = slides?.find((item) => item.key === key)
    if (!slides || incoming?.state !== 'entering') {
        return
    }

    clearPending(tile)
    incoming.state = 'active'
    tiles.value[tile] = [incoming]
}

function onAnimationEnd(event: AnimationEvent, tile: number, key: number): void {
    // Only the animation of the slide itself, not one of a part inside it.
    if (event.target === event.currentTarget) {
        entered(tile, key)
    }
}

function onVisibilityChange(): void {
    pageShown.value = document.visibilityState !== 'hidden'
}

if (typeof document !== 'undefined') {
    document.addEventListener('visibilitychange', onVisibilityChange)
}

watch(trackIds, (ids) => reset(ids), { immediate: true })

watch(
    rotating,
    (active) => {
        if (active) {
            schedule(nextChangeDelay(Math.random, true))
        } else {
            stopTimer()
        }
    },
    { immediate: true },
)

onBeforeUnmount(() => {
    reset([])
    if (typeof document !== 'undefined') {
        document.removeEventListener('visibilitychange', onVisibilityChange)
    }
})
</script>

<template>
    <LibraryCover
        v-if="view.kind === 'custom'"
        kind="playlist"
        :id="id"
        :available="true"
        :version="view.version"
    />
    <span
        v-else-if="view.kind === 'tracks'"
        ref="grid"
        data-part="playlist-cover"
        :data-count="trackIds.length"
        :data-state="rotating && trackIds.length > tiles.length ? 'rotating' : undefined"
        aria-hidden="true"
        :style="{ '--wmp-item-count': trackIds.length }"
    >
        <span
            v-for="(slides, tile) in tiles"
            :key="tile"
            data-part="playlist-cover-tile"
            :data-index="tile"
            :data-state="slides.length > 1 ? 'changing' : undefined"
        >
            <span
                v-for="item in slides"
                :key="item.key"
                data-part="playlist-cover-slide"
                :data-state="item.state"
                :data-from="item.from"
                @animationend="onAnimationEnd($event, tile, item.key)"
            >
                <LibraryCover
                    kind="track"
                    :id="item.trackId"
                    :available="true"
                    @settled="(loaded) => settled(tile, item.trackId, loaded)"
                />
            </span>
        </span>
    </span>
    <BaseCover v-else :placeholder="false">
        <BaseIcon :name="icon" />
    </BaseCover>
</template>
