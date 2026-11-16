// The visualization modules of the web app, by their id in the API. A further module
// implements VisualizationModule in a folder of its own and is added here.

import { VisualizationModuleObject } from '@/api/generated/models'
import { browserVisualizationClock, type VisualizationClock } from '../controller'
import type { VisualizationModule, VisualizationModuleId } from '../module'
import { defaultVisualizationSettings } from '../settings'
import { aforceModule } from './aforce'
import { blackcapModule } from './blackcap'
import { milkdropModule } from './milkdrop'

export const visualizationModules: Readonly<Record<VisualizationModuleId, VisualizationModule>> = {
    [VisualizationModuleObject.Milkdrop]: milkdropModule,
    [VisualizationModuleObject.AForce]: aforceModule,
    [VisualizationModuleObject.BlackCap]: blackcapModule,
}

/** A module the visualization offers, with the message key of its name. */
export interface VisualizationModuleChoice {
    readonly id: VisualizationModuleId
    readonly labelKey: string
}

/** The modules in the order the visualization offers them. */
export const visualizationModuleChoices: readonly VisualizationModuleChoice[] = [
    {
        id: VisualizationModuleObject.Milkdrop,
        labelKey: 'nowPlaying.visualizationModules.milkdrop',
    },
    { id: VisualizationModuleObject.AForce, labelKey: 'nowPlaying.visualizationModules.aforce' },
    {
        id: VisualizationModuleObject.BlackCap,
        labelKey: 'nowPlaying.visualizationModules.blackcap',
    },
]

/** The module with the id, or the default module for an id this app does not know. */
export function visualizationModule(
    id: VisualizationModuleId | null | undefined,
): VisualizationModule {
    return (
        (id ? visualizationModules[id] : undefined) ??
        visualizationModules[defaultVisualizationSettings.module]
    )
}

/** Parts of the browser the visualization of the stage uses; tests replace them. */
export const visualizationDependencies = {
    module: visualizationModule,
    clock: browserVisualizationClock as VisualizationClock,
    devicePixelRatio: (): number =>
        typeof window === 'undefined' ? 1 : window.devicePixelRatio || 1,
}
