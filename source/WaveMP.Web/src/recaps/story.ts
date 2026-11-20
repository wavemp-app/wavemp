// Playback of the recap story: which section is shown and how far its time has run. The sections
// advance on their own; a tap, a swipe, the arrow keys and the buttons go back and forth, and the
// story pauses on request or while a finger rests on it. Pure functions plus a composable.

import { getCurrentScope, onScopeDispose, ref, watch, type Ref } from 'vue'

/** Milliseconds each section is shown before the next one follows. */
export const storySectionMilliseconds = 7000

export interface StoryState {
    readonly index: number
    /** Share of the time of the section that has run, 0 to 1. */
    readonly progress: number
    readonly playing: boolean
}

/** The state after some time has run; after the last section the story stops at its end. */
export function advanceStory(
    state: StoryState,
    elapsed: number,
    count: number,
    duration = storySectionMilliseconds,
): StoryState {
    if (!state.playing || count === 0 || elapsed <= 0) {
        return state
    }

    const progress = state.progress + elapsed / duration
    if (progress < 1) {
        return { ...state, progress }
    }
    if (state.index < count - 1) {
        return { index: state.index + 1, progress: 0, playing: true }
    }
    return { index: count - 1, progress: 1, playing: false }
}

export function nextSection(state: StoryState, count: number): StoryState {
    if (state.index < count - 1) {
        return { ...state, index: state.index + 1, progress: 0 }
    }
    return { index: Math.max(0, count - 1), progress: 1, playing: false }
}

export function previousSection(state: StoryState): StoryState {
    return { ...state, index: Math.max(0, state.index - 1), progress: 0 }
}

/** Plays or pauses; a story that has ended starts again from the beginning. */
export function togglePlaying(state: StoryState, count: number): StoryState {
    if (!state.playing && state.index >= count - 1 && state.progress >= 1) {
        return { index: 0, progress: 0, playing: true }
    }
    return { ...state, playing: !state.playing }
}

export type StoryGesture = 'previous' | 'next' | 'none'

/**
 * What a pointer that went down and up again means: a swipe to the left goes on, to the right
 * back; a tap on the left third goes back, elsewhere on; a long press (holding) means nothing.
 */
export function storyGesture(
    start: { readonly x: number; readonly y: number },
    end: { readonly x: number; readonly y: number },
    width: number,
    left: number,
    held: boolean,
): StoryGesture {
    const dx = end.x - start.x
    const dy = end.y - start.y
    if (Math.abs(dx) > 40 && Math.abs(dx) > Math.abs(dy)) {
        return dx < 0 ? 'next' : 'previous'
    }
    if (held || Math.hypot(dx, dy) > 12) {
        return 'none'
    }
    return end.x - left < width / 3 ? 'previous' : 'next'
}

/** The story of a number of sections; it starts again from the first when the count changes. */
export function useStory(count: Ref<number>, duration = storySectionMilliseconds) {
    const state = ref<StoryState>({ index: 0, progress: 0, playing: true })
    /** A finger or the mouse rests on the story: its time stands still. */
    const holding = ref(false)
    let frame = 0
    let last = 0

    function tick(now: number): void {
        const elapsed = last ? now - last : 0
        last = now
        const hidden = typeof document !== 'undefined' && document.hidden
        if (!holding.value && !hidden) {
            state.value = advanceStory(state.value, elapsed, count.value, duration)
        }
        frame = requestAnimationFrame(tick)
    }

    function start(): void {
        if (typeof requestAnimationFrame === 'function' && frame === 0) {
            last = 0
            frame = requestAnimationFrame(tick)
        }
    }

    function stop(): void {
        if (frame !== 0) {
            cancelAnimationFrame(frame)
            frame = 0
        }
    }

    watch(count, () => {
        state.value = { index: 0, progress: 0, playing: true }
    })

    if (getCurrentScope()) {
        onScopeDispose(stop)
    }

    return {
        state,
        holding,
        start,
        stop,
        next: () => (state.value = nextSection(state.value, count.value)),
        previous: () => (state.value = previousSection(state.value)),
        toggle: () => (state.value = togglePlaying(state.value, count.value)),
        pause: () => (state.value = { ...state.value, playing: false }),
        goTo: (index: number) =>
            (state.value = {
                ...state.value,
                index: Math.max(0, Math.min(index, count.value - 1)),
                progress: 0,
            }),
    }
}
