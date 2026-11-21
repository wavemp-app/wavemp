// Logic of the controls of the visualization (VisualizationControls.vue).

import { roomAbove, type Rect } from '@/components/base/menu'

/** A preset of the visualization as the controls offer it: its id and its label. */
export interface PresetChoice {
    readonly id: string
    readonly label: string
}

/**
 * Room above the preset button that the list needs at least, in pixels (about eight entries); with
 * less, the page first scrolls the button down.
 */
export const minimumRoomAbove = 240

export interface PresetMenuPlacement {
    /** Above the button (desktop) or as a sheet at the bottom of the screen (phones). */
    readonly placement: 'above' | 'sheet'
    /** Whether the button has to come further down the screen first, to leave room above it. */
    readonly scrollIntoView: boolean
}

/**
 * Where the preset list opens: on phones as a sheet at the bottom of the screen, otherwise always
 * above its button. If the button is so close to the top of the screen that the list would hardly
 * fit above it, the page first scrolls the button down, so the list keeps its direction.
 */
export function presetMenuPlacement(phone: boolean, button: Rect): PresetMenuPlacement {
    if (phone) {
        return { placement: 'sheet', scrollIntoView: false }
    }

    return { placement: 'above', scrollIntoView: roomAbove(button) < minimumRoomAbove }
}
