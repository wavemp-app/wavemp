<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useMediaQuery } from '@/composables/useMediaQuery'
import {
    MarqueeCycle,
    marqueeDistance,
    marqueeScrollDuration,
    marqueeStateTokens,
    type MarqueePhase,
} from './marquee'

/**
 * A line of text that never overflows its place: a text too long for it rests cut off for a moment,
 * scrolls slowly to its end, rests there and starts again from the beginning (marquee.ts); with
 * prefers-reduced-motion it ends with an ellipsis and does not move. The outer element is the part
 * of the place (e.g. now-playing-title), the inner one the part marquee with the phase as data-state
 * and the distance and duration of the scrolling as --wmp-marquee-distance and
 * --wmp-marquee-duration, so the theme moves it with CSS only.
 */
const props = defineProps<{
    /** data-part of the outer element. */
    part: string
    text: string
}>()

const box = ref<HTMLElement | null>(null)
const inner = ref<HTMLElement | null>(null)
const distance = ref(0)
const phase = ref<MarqueePhase | null>(null)
const reducedMotion = useMediaQuery('(prefers-reduced-motion: reduce)')

const cycle = new MarqueeCycle((value) => (phase.value = value))

const tokens = computed(() => marqueeStateTokens(distance.value, phase.value))
const duration = computed(() =>
    distance.value > 0 ? `${marqueeScrollDuration(distance.value)}ms` : '0ms',
)

/** The width the text has in its place, without the padding of the place. */
function boxWidth(element: HTMLElement): number {
    const style = getComputedStyle(element)
    return (
        element.clientWidth -
        (Number.parseFloat(style.paddingInlineStart) || 0) -
        (Number.parseFloat(style.paddingInlineEnd) || 0)
    )
}

/** Measures the text again and starts the cycle anew when the distance changed. */
function measure(restart = false): void {
    const element = box.value
    const text = inner.value
    const next = element && text ? marqueeDistance(text.scrollWidth, boxWidth(element)) : 0
    const changed = next !== distance.value
    distance.value = next
    if (next <= 0 || reducedMotion.value) {
        if (cycle.running || phase.value !== null) {
            cycle.stop()
        }
        return
    }

    if (changed || restart || !cycle.running) {
        cycle.start(next)
    }
}

let observer: ResizeObserver | null = null

// Another text starts at its beginning.
watch(
    () => props.text,
    async () => {
        cycle.stop()
        await nextTick()
        measure(true)
    },
)
watch(reducedMotion, () => measure(true))

onMounted(() => {
    measure()
    if (typeof ResizeObserver !== 'undefined') {
        observer = new ResizeObserver(() => measure())
        if (box.value) {
            observer.observe(box.value)
        }
        if (inner.value) {
            observer.observe(inner.value)
        }
    }

    // Web fonts change the width of the text, not the size of its place.
    void document.fonts?.ready.then(() => measure())
})

onBeforeUnmount(() => {
    observer?.disconnect()
    observer = null
    cycle.stop()
})
</script>

<template>
    <span ref="box" :data-part="part">
        <span
            ref="inner"
            data-part="marquee"
            :data-state="tokens"
            :style="{
                '--wmp-marquee-distance': `${distance}px`,
                '--wmp-marquee-duration': duration,
            }"
            >{{ text }}</span
        >
    </span>
</template>
