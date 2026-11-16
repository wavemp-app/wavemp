// The visualization modules of the web app, by their id in the API. A new module (A-Force,
// BlackCap) implements VisualizationModule in a file of its own and is added here.

import { VisualizationModuleObject } from '@/api/generated/models'
import { browserVisualizationClock, type VisualizationClock } from '../controller'
import type { VisualizationModule, VisualizationModuleId } from '../module'
import { defaultVisualizationSettings } from '../settings'
import { milkdropModule } from './milkdrop'

export const visualizationModules: Readonly<Record<VisualizationModuleId, VisualizationModule>> = {
    [VisualizationModuleObject.Milkdrop]: milkdropModule,
}

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
