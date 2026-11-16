// Quality levels of the visualizations. The profile chooses a level or Auto; with Auto the
// controller measures the frame rate and steps down while the device cannot draw about 60 frames
// per second, and back up after a while of smooth drawing. Pure.

import { VisualizationQualityObject, type VisualizationQuality } from '@/api/generated/models'

/** A level of detail a renderer draws with. */
export type QualityLevel = 'low' | 'medium' | 'high'

/** The levels from least to most detail. */
export const qualityLevels: readonly QualityLevel[] = ['low', 'medium', 'high']

/** The level of a fixed setting of the profile, or null for Auto. */
export function fixedQualityLevel(quality: VisualizationQuality): QualityLevel | null {
    switch (quality) {
        case VisualizationQualityObject.Low:
            return 'low'
        case VisualizationQualityObject.Medium:
            return 'medium'
        case VisualizationQualityObject.High:
            return 'high'
        default:
            return null
    }
}

export interface AdaptiveQualityOptions {
    /** Seconds of frames one measurement covers. */
    readonly windowSeconds?: number
    /** Below this frame rate a measurement steps down. */
    readonly lowFps?: number
    /** From this frame rate on a measurement counts as smooth. */
    readonly smoothFps?: number
    /** Seconds of smooth drawing before the first step up. */
    readonly upgradeSeconds?: number
    /** Longest wait before a step up; every step down doubles the wait up to it. */
    readonly maxUpgradeSeconds?: number
}

const defaults: Required<AdaptiveQualityOptions> = {
    windowSeconds: 2,
    lowFps: 50,
    smoothFps: 57,
    upgradeSeconds: 10,
    maxUpgradeSeconds: 120,
}

/**
 * Frames longer than this (a hidden tab, a pause, the debugger) are not measured. Slow frames below
 * it count, so even a device that draws only a few frames per second steps down.
 */
const maxFrameSeconds = 1

/**
 * Chooses the level by the measured frame rate: one step down after a window below lowFps, one
 * step up after upgradeSeconds of windows at smoothFps or more. Each step down doubles the wait for
 * the next step up, so a device on the edge settles instead of switching back and forth.
 */
export class AdaptiveQuality {
    private readonly options: Required<AdaptiveQualityOptions>
    private index: number
    private frames = 0
    private elapsed = 0
    private smooth = 0
    private upgradeAfter: number

    constructor(start: QualityLevel = 'high', options: AdaptiveQualityOptions = {}) {
        this.options = { ...defaults, ...options }
        this.index = qualityLevels.indexOf(start)
        this.upgradeAfter = this.options.upgradeSeconds
    }

    get level(): QualityLevel {
        return qualityLevels[this.index] ?? 'high'
    }

    /** Counts a drawn frame that took the given seconds; returns the level to draw with. */
    frame(seconds: number): QualityLevel {
        if (!(seconds > 0) || seconds > maxFrameSeconds) {
            return this.level
        }

        this.frames++
        this.elapsed += seconds
        if (this.elapsed < this.options.windowSeconds) {
            return this.level
        }

        const fps = this.frames / this.elapsed
        const window = this.elapsed
        this.frames = 0
        this.elapsed = 0
        if (fps < this.options.lowFps) {
            this.smooth = 0
            if (this.index > 0) {
                this.index--
                this.upgradeAfter = Math.min(this.options.maxUpgradeSeconds, this.upgradeAfter * 2)
            }
        } else if (fps >= this.options.smoothFps) {
            this.smooth += window
            if (this.smooth >= this.upgradeAfter && this.index < qualityLevels.length - 1) {
                this.index++
                this.smooth = 0
            }
        } else {
            this.smooth = 0
        }

        return this.level
    }

    /** Forgets the current measurement, e.g. after a pause; the level stays. */
    restart(): void {
        this.frames = 0
        this.elapsed = 0
    }
}
