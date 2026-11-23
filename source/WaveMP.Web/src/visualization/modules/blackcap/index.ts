// The module BlackCap: the own visualization of WaveMP with organic, floating 3D shapes and light
// effects, calm but clearly alive: the shapes breathe with the tempo, swell from below with the
// bass and on a beat, twist with the mids, ripple and glint with the highs and contract and dim in
// quiet passages. Its presets are its modes (forms and motion: drift, orbit, cluster, ribbon,
// bloom); its color schemes are chosen apart from them. The renderer (WebGL 2) loads on demand in
// a chunk of its own.

import { VisualizationModuleObject } from '@/api/generated/models'
import type {
    LoadedVisualizationModule,
    VisualizationModule,
    VisualizationPreset,
    VisualizationRenderer,
    VisualizationRendererOptions,
} from '../../module'
import { supportsWebGl2Visualization } from '../../support'
import { blackcapModes } from './modes'
import { blackcapColorSchemes } from './schemes'

export type BlackCapRendererFactory = (
    options: VisualizationRendererOptions,
    now: () => number,
) => VisualizationRenderer

/** What the module needs from the browser; tests replace it. */
export interface BlackCapEnvironment {
    /** Whether WebGL 2 and Web Audio exist. */
    supported(): boolean
    loadRenderer(): Promise<BlackCapRendererFactory>
    /** Milliseconds of a steady clock. */
    now(): number
}

export const browserBlackCapEnvironment: BlackCapEnvironment = {
    supported: supportsWebGl2Visualization,
    loadRenderer: async () => (await import('./renderer')).createBlackCapRenderer,
    now: () => performance.now(),
}

/** The modes as presets, in their own order. */
export const blackcapPresets: readonly VisualizationPreset[] = blackcapModes.map((mode) => ({
    id: mode.id,
    name: mode.name,
    labelKey: mode.labelKey,
}))

/** The color schemes, in their own order. */
export const blackcapColorSchemePresets: readonly VisualizationPreset[] = blackcapColorSchemes.map(
    (scheme) => ({ id: scheme.id, name: scheme.name, labelKey: scheme.labelKey }),
)

/** Creates the module BlackCap; the app uses blackcapModule, tests pass their own environment. */
export function createBlackCapModule(
    environment: BlackCapEnvironment = browserBlackCapEnvironment,
): VisualizationModule {
    let supported: boolean | undefined
    let loading: Promise<LoadedVisualizationModule> | undefined

    async function load(): Promise<LoadedVisualizationModule> {
        const createRenderer = await environment.loadRenderer()
        return {
            presets: blackcapPresets,
            colorSchemes: blackcapColorSchemePresets,
            createRenderer: (options) => createRenderer(options, () => environment.now()),
        }
    }

    return {
        id: VisualizationModuleObject.BlackCap,
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

export const blackcapModule = createBlackCapModule()
