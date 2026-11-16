// The module A-Force: the own visualization of WaveMP with flowing waveforms, particles, color
// gradients and camera movements that react to bass, mids, highs and beats. Its presets are its
// style variants. The renderer (WebGL 2) loads on demand in a chunk of its own.

import { VisualizationModuleObject } from '@/api/generated/models'
import type {
    LoadedVisualizationModule,
    VisualizationModule,
    VisualizationPreset,
    VisualizationRenderer,
    VisualizationRendererOptions,
} from '../../module'
import { supportsWebGl2Visualization } from '../../support'
import { aforceStyles } from './styles'

export type AForceRendererFactory = (
    options: VisualizationRendererOptions,
    now: () => number,
) => VisualizationRenderer

/** What the module needs from the browser; tests replace it. */
export interface AForceEnvironment {
    /** Whether WebGL 2 and Web Audio exist. */
    supported(): boolean
    loadRenderer(): Promise<AForceRendererFactory>
    /** Milliseconds of a steady clock. */
    now(): number
}

export const browserAForceEnvironment: AForceEnvironment = {
    supported: supportsWebGl2Visualization,
    loadRenderer: async () => (await import('./renderer')).createAForceRenderer,
    now: () => performance.now(),
}

/** The style variants as presets, in their own order. */
export const aforcePresets: readonly VisualizationPreset[] = aforceStyles.map((style) => ({
    id: style.id,
    name: style.name,
    labelKey: style.labelKey,
}))

/** Creates the module A-Force; the app uses aforceModule, tests pass their own environment. */
export function createAForceModule(
    environment: AForceEnvironment = browserAForceEnvironment,
): VisualizationModule {
    let supported: boolean | undefined
    let loading: Promise<LoadedVisualizationModule> | undefined

    async function load(): Promise<LoadedVisualizationModule> {
        const createRenderer = await environment.loadRenderer()
        return {
            presets: aforcePresets,
            createRenderer: (options) => createRenderer(options, () => environment.now()),
        }
    }

    return {
        id: VisualizationModuleObject.AForce,
        isSupported: () => (supported ??= environment.supported()),
        load: () => {
            loading ??= load().catch((error: unknown) => {
                // A later attempt (e.g. after a network error) loads again.
                loading = undefined
                throw error
            })
            return loading
        },
    }
}

export const aforceModule = createAForceModule()
