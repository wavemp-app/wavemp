<script setup lang="ts">
import { computed, onActivated, onDeactivated, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import BaseButton from '@/components/base/BaseButton.vue'
import RecapStorySlide from './RecapStorySlide.vue'
import { findRecapDesign } from '@/recaps/designs'
import type { RecapSlide } from '@/recaps/slides'
import { storyGesture, useStory } from '@/recaps/story'

/**
 * The recap as a story: one section after the other, each shown for some seconds, with a progress
 * bar per section. A tap on the left third goes back, elsewhere on; a swipe goes back or on;
 * holding the story pauses it while held; the arrow keys, the space bar and the buttons do the
 * same. data-recap-design names the design whose variables color the story, data-decoration its
 * kind of decoration (the part recap-backdrop). The index of the shown section is v-model:index.
 */
const props = defineProps<{
    slides: readonly RecapSlide[]
    design: string
    label: string
}>()

const index = defineModel<number>('index', { default: 0 })

const { t } = useI18n()
const count = computed(() => props.slides.length)
const story = useStory(count)
const decoration = computed(() => findRecapDesign(props.design).decoration)
const current = computed(() => props.slides[story.state.value.index])
const playing = computed(() => story.state.value.playing)

watch(
    () => story.state.value.index,
    (value) => (index.value = value),
    { immediate: true },
)

function segmentState(position: number): string {
    const { index: shown } = story.state.value
    return position < shown ? 'done' : position === shown ? 'active' : 'upcoming'
}

function segmentValue(position: number): number {
    const { index: shown, progress } = story.state.value
    return position < shown ? 1 : position === shown ? progress : 0
}

let pointer: { x: number; y: number; id: number; time: number } | null = null
const holdMilliseconds = 300
let holdTimer = 0

function onPointerDown(event: PointerEvent): void {
    if (event.button !== 0) {
        return
    }
    pointer = { x: event.clientX, y: event.clientY, id: event.pointerId, time: event.timeStamp }
    window.clearTimeout(holdTimer)
    holdTimer = window.setTimeout(() => (story.holding.value = true), holdMilliseconds)
}

function onPointerUp(event: PointerEvent): void {
    window.clearTimeout(holdTimer)
    const start = pointer
    pointer = null
    const held = story.holding.value
    story.holding.value = false
    if (!start || start.id !== event.pointerId) {
        return
    }
    const box = (event.currentTarget as HTMLElement).getBoundingClientRect()
    const gesture = storyGesture(
        start,
        { x: event.clientX, y: event.clientY },
        box.width,
        box.left,
        held || event.timeStamp - start.time >= holdMilliseconds,
    )
    if (gesture === 'next') {
        story.next()
    } else if (gesture === 'previous') {
        story.previous()
    }
}

function onPointerCancel(): void {
    window.clearTimeout(holdTimer)
    pointer = null
    story.holding.value = false
}

function onKeydown(event: KeyboardEvent): void {
    if (event.target !== event.currentTarget) {
        return
    }
    if (event.key === 'ArrowRight') {
        story.next()
    } else if (event.key === 'ArrowLeft') {
        story.previous()
    } else if (event.key === ' ') {
        story.toggle()
    } else {
        return
    }
    event.preventDefault()
}

const root = ref<HTMLElement | null>(null)

onMounted(story.start)
onActivated(story.start)
onDeactivated(story.stop)

defineExpose({
    pause: story.pause,
    goTo: story.goTo,
    element: root,
})
</script>

<template>
    <section
        ref="root"
        data-part="recap-story"
        :data-recap-design="design"
        :data-decoration="decoration"
        :data-state="playing ? 'playing' : 'paused'"
        :aria-label="label"
        aria-roledescription="carousel"
        tabindex="0"
        @keydown="onKeydown"
    >
        <span data-part="recap-backdrop" aria-hidden="true"></span>
        <ol data-part="recap-progress" aria-hidden="true">
            <li
                v-for="(slide, position) in slides"
                :key="position"
                data-part="recap-progress-segment"
                :data-state="segmentState(position)"
                :style="{ '--wmp-progress-value': segmentValue(position) }"
            >
                <span data-part="recap-progress-fill"></span>
            </li>
        </ol>
        <div
            data-part="recap-slides"
            :aria-live="playing ? 'off' : 'polite'"
            @pointerdown="onPointerDown"
            @pointerup="onPointerUp"
            @pointercancel="onPointerCancel"
            @pointerleave="onPointerCancel"
            @contextmenu.prevent
        >
            <RecapStorySlide
                v-if="current"
                :key="story.state.value.index"
                :slide="current"
                :aria-label="t('recap.sectionOf', { index: story.state.value.index + 1, count })"
                aria-roledescription="slide"
            />
        </div>
        <div data-part="recap-controls">
            <BaseButton
                part="btn-recap-previous"
                variant="icon"
                icon="recap-previous"
                :label="t('recap.previous')"
                :disabled="story.state.value.index === 0"
                @click="story.previous()"
            />
            <BaseButton
                part="btn-recap-pause"
                variant="icon"
                :icon="playing ? 'pause' : 'play'"
                :label="playing ? t('recap.pause') : t('recap.play')"
                :state="playing ? 'playing' : 'paused'"
                @click="story.toggle()"
            />
            <BaseButton
                part="btn-recap-next"
                variant="icon"
                icon="recap-next"
                :label="t('recap.next')"
                :disabled="story.state.value.index >= count - 1"
                @click="story.next()"
            />
        </div>
    </section>
</template>
