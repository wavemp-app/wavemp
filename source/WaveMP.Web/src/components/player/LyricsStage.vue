<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { useI18n } from 'vue-i18n'
import { useMediaQuery } from '@/composables/useMediaQuery'
import {
    activeLineIndex,
    activeWordIndex,
    isSynchronized,
    itemStateOf,
    lineEnd,
    progressBetween,
    staticScrollShare,
    wordEnd,
    type LyricsItemState,
    type TrackLyrics,
} from '@/lyrics/timing'

/**
 * The lyrics of the current track on the Now Playing stage. Synchronized lyrics follow the music:
 * the current line is active (data-state), the view scrolls it into the middle, and with word
 * timestamps the current word fills while it is sung (--wmp-word-progress); a click or tap on a
 * line seeks there. Static lyrics scroll calmly from the top to the bottom over the track. The view
 * only sets states and runtime variables; how they look is up to the theme. With
 * prefers-reduced-motion the view jumps instead of scrolling smoothly and static lyrics do not move
 * on their own. After the listener scrolls, the view waits a moment before it follows again.
 * Lyrics of another version of the song carry a note (lyrics-note) that they do not follow it.
 */
const props = withDefaults(
    defineProps<{
        lyrics?: TrackLyrics | null
        /** Whether the stage shows the lyrics view; a hidden view does no work. */
        active?: boolean
        /** Whether the music plays; the view follows it frame by frame only then. */
        playing?: boolean
        /** The position of the music in seconds, updated a few times per second. */
        position?: number
        /** Duration of the track in seconds; 0 if unknown. */
        duration?: number
        /** The exact position of the music right now, in seconds. */
        clock?: () => number
    }>(),
    {
        lyrics: null,
        active: true,
        playing: false,
        position: 0,
        duration: 0,
        clock: undefined,
    },
)

const emit = defineEmits<{
    /** A line was chosen: the music goes to its start, in seconds. */
    seek: [seconds: number]
}>()

const { t } = useI18n()
const reducedMotion = useMediaQuery('(prefers-reduced-motion: reduce)')

/** How long the view waits after the listener scrolled before it follows the music again. */
const userScrollPause = 4000

const root = ref<HTMLElement | null>(null)
const list = ref<HTMLElement | null>(null)
const activeIndex = ref(-1)
const activeWord = ref(-1)

const lines = computed(() => props.lyrics?.lines ?? [])
const synchronized = computed(() => isSynchronized(props.lyrics))
const words = computed(
    () => synchronized.value && lines.value.some((line) => line.words.length > 0),
)

let frame = 0
let userScrolledAt = Number.NEGATIVE_INFINITY

function now(): number {
    return typeof performance === 'undefined' ? Date.now() : performance.now()
}

function currentPosition(): number {
    return props.clock ? props.clock() : props.position
}

function lineState(index: number): LyricsItemState {
    return itemStateOf(index, activeIndex.value)
}

function wordState(lineIndex: number, wordIndex: number): LyricsItemState {
    if (lineIndex !== activeIndex.value) {
        return lineState(lineIndex)
    }

    return itemStateOf(wordIndex, activeWord.value)
}

function lineElement(index: number): HTMLElement | null {
    return list.value?.querySelector<HTMLElement>(`[data-index="${index}"]`) ?? null
}

function followsMusic(): boolean {
    return now() - userScrolledAt >= userScrollPause
}

function scrollTo(top: number, smooth: boolean): void {
    const element = list.value
    if (!element) {
        return
    }

    const target = Math.max(0, top)
    if (typeof element.scrollTo === 'function') {
        element.scrollTo({ top: target, behavior: smooth ? 'smooth' : 'auto' })
    } else {
        element.scrollTop = target
    }
}

/** Scrolls the active line into the middle of the view. */
function centerActiveLine(smooth: boolean): void {
    const element = list.value
    const line = lineElement(activeIndex.value)
    if (!element || !line) {
        return
    }

    scrollTo(line.offsetTop - (element.clientHeight - line.offsetHeight) / 2, smooth)
}

/** Sets the progress of the active line and word as runtime variables. */
function updateProgress(position: number): void {
    const index = activeIndex.value
    const line = lines.value[index]
    const element = lineElement(index)
    if (!line || line.start === null || !element) {
        return
    }

    const end = lineEnd(line, props.duration)
    element.style.setProperty(
        '--wmp-line-progress',
        progressBetween(line.start, end, position).toFixed(4),
    )

    const word = line.words[activeWord.value]
    const wordElement = element.querySelectorAll<HTMLElement>('[data-part="lyrics-word"]')[
        activeWord.value
    ]
    if (word && wordElement) {
        wordElement.style.setProperty(
            '--wmp-word-progress',
            progressBetween(word.start, wordEnd(word, end), position).toFixed(4),
        )
    }
}

/**
 * Brings the view to a position of the music. The position is NaN outside of the song (intro or
 * outro of a music video): no line is active then and plain lyrics keep their scroll position.
 */
function update(smooth: boolean): void {
    const position = currentPosition()
    if (!synchronized.value) {
        activeIndex.value = -1
        activeWord.value = -1
        const element = list.value
        const inSong = Number.isFinite(position)
        if (element && inSong && props.playing && !reducedMotion.value && followsMusic()) {
            const share = staticScrollShare(position, props.duration)
            element.scrollTop = share * Math.max(0, element.scrollHeight - element.clientHeight)
        }

        return
    }

    const index = activeLineIndex(lines.value, position)
    const line = lines.value[index]
    const word = line ? activeWordIndex(line.words, position) : -1
    const changed = index !== activeIndex.value
    activeIndex.value = index
    activeWord.value = word
    if (changed && followsMusic()) {
        void nextTick(() => centerActiveLine(smooth && !reducedMotion.value))
    }

    void nextTick(() => updateProgress(position))
}

function stop(): void {
    if (frame !== 0 && typeof cancelAnimationFrame === 'function') {
        cancelAnimationFrame(frame)
    }

    frame = 0
}

function tick(): void {
    frame = 0
    update(true)
    if (props.active && props.playing && typeof requestAnimationFrame === 'function') {
        frame = requestAnimationFrame(tick)
    }
}

/** Follows the music frame by frame while the view is shown and the music plays. */
function restart(): void {
    stop()
    if (props.active && props.lyrics) {
        tick()
    }
}

function onUserScroll(): void {
    userScrolledAt = now()
}

function seek(seconds: number | null): void {
    if (seconds !== null) {
        userScrolledAt = Number.NEGATIVE_INFINITY
        emit('seek', seconds)
    }
}

watch(
    () => [props.active, props.playing] as const,
    () => restart(),
)

onMounted(restart)

// A new track or new lyrics: start at the top and at the current line, without an animation.
watch(
    () => props.lyrics,
    () => {
        activeIndex.value = -1
        activeWord.value = -1
        userScrolledAt = Number.NEGATIVE_INFINITY
        void nextTick(() => {
            scrollTo(0, false)
            restart()
        })
    },
)

// The view is shown again: jump to the current line.
watch(
    () => props.active,
    (active) => {
        if (active) {
            void nextTick(() => centerActiveLine(false))
        }
    },
)

// A seek or a time update while paused moves the view as well.
watch(
    () => props.position,
    () => {
        if (props.active && !props.playing) {
            update(true)
        }
    },
)

watch(
    [root, () => lines.value.length],
    ([element, count]) => element?.style.setProperty('--wmp-item-count', String(count)),
    { immediate: true },
)

watch(
    [root, activeIndex, synchronized],
    ([element, index, follows]) => {
        if (follows) {
            element?.style.setProperty('--wmp-active-index', String(index))
        } else {
            element?.style.removeProperty('--wmp-active-index')
        }
    },
    { immediate: true },
)

onBeforeUnmount(stop)
</script>

<template>
    <div
        ref="root"
        data-part="lyrics"
        :data-state="synchronized ? 'synchronized' : 'static'"
        :data-words="words ? 'true' : undefined"
    >
        <p
            v-if="lyrics?.fromOtherVersion && !synchronized"
            data-part="lyrics-note"
            data-item="other-version"
        >
            <span data-part="label">{{ t('nowPlaying.lyricsOtherVersion') }}</span>
        </p>
        <ol
            ref="list"
            data-part="lyrics-lines"
            :aria-label="t('nowPlaying.lyricsLabel')"
            @wheel.passive="onUserScroll"
            @touchmove.passive="onUserScroll"
        >
            <li
                v-for="(line, index) in lines"
                :key="index"
                data-part="lyrics-line"
                :data-index="index"
                :data-state="synchronized ? lineState(index) : undefined"
                :data-empty="line.text === '' ? 'true' : undefined"
                :aria-current="synchronized && index === activeIndex ? 'true' : undefined"
            >
                <button
                    v-if="synchronized"
                    type="button"
                    data-part="lyrics-line-button"
                    :aria-label="line.text === '' ? t('nowPlaying.lyricsBreak') : undefined"
                    @click="seek(line.start)"
                >
                    <template v-if="line.words.length > 0">
                        <span
                            v-for="(word, wordIndex) in line.words"
                            :key="wordIndex"
                            data-part="lyrics-word"
                            :data-state="wordState(index, wordIndex)"
                            >{{ word.text }}</span
                        >
                    </template>
                    <span v-else data-part="label">{{ line.text }}</span>
                </button>
                <span v-else data-part="label">{{ line.text }}</span>
            </li>
        </ol>
    </div>
</template>
