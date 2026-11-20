import { describe, expect, it } from 'vitest'
import {
    advanceStory,
    nextSection,
    previousSection,
    storyGesture,
    togglePlaying,
    type StoryState,
} from '../story'

const playing: StoryState = { index: 0, progress: 0, playing: true }

describe('recap story', () => {
    it('runs the time of a section and then shows the next one', () => {
        expect(advanceStory(playing, 3500, 3, 7000)).toEqual({
            index: 0,
            progress: 0.5,
            playing: true,
        })
        expect(advanceStory({ ...playing, progress: 0.9 }, 1400, 3, 7000)).toEqual({
            index: 1,
            progress: 0,
            playing: true,
        })
    })

    it('stops at the end of the last section', () => {
        expect(advanceStory({ index: 2, progress: 0.99, playing: true }, 1000, 3, 7000)).toEqual({
            index: 2,
            progress: 1,
            playing: false,
        })
    })

    it('does not run while paused or without sections', () => {
        const paused = { ...playing, playing: false }
        expect(advanceStory(paused, 1000, 3)).toBe(paused)
        expect(advanceStory(playing, 1000, 0)).toBe(playing)
    })

    it('goes back and on by hand', () => {
        expect(nextSection({ index: 0, progress: 0.4, playing: true }, 3)).toEqual({
            index: 1,
            progress: 0,
            playing: true,
        })
        expect(nextSection({ index: 2, progress: 0.4, playing: true }, 3)).toEqual({
            index: 2,
            progress: 1,
            playing: false,
        })
        expect(previousSection({ index: 1, progress: 0.4, playing: false })).toEqual({
            index: 0,
            progress: 0,
            playing: false,
        })
        expect(previousSection(playing)).toEqual(playing)
    })

    it('pauses, plays and starts an ended story again', () => {
        expect(togglePlaying(playing, 3).playing).toBe(false)
        expect(togglePlaying({ ...playing, playing: false }, 3).playing).toBe(true)
        expect(togglePlaying({ index: 2, progress: 1, playing: false }, 3)).toEqual(playing)
    })

    it('reads taps, swipes and holding', () => {
        const width = 300
        const left = 100
        const at = (x: number, y = 200) => ({ x, y })
        expect(storyGesture(at(150), at(150), width, left, false)).toBe('previous')
        expect(storyGesture(at(300), at(302), width, left, false)).toBe('next')
        expect(storyGesture(at(300), at(200), width, left, false)).toBe('next')
        expect(storyGesture(at(200), at(300), width, left, false)).toBe('previous')
        expect(storyGesture(at(300), at(300), width, left, true)).toBe('none')
        expect(storyGesture(at(300), at(305, 230), width, left, false)).toBe('none')
    })
})
