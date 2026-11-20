// Designs of the recap. A design is an id with a name and a kind of decoration; its colors and
// font live in the base CSS (src/styles/recaps.css) as variables on [data-recap-design='<id>'],
// so a theme can change them as well. The pictures and videos read the same variables from the
// shown story, so they look like the page. A new design needs an entry here, its block of
// variables in recaps.css and its name in the messages (recap.designs.<id>).

/** How the background of a design is decorated, on the page (base CSS) and in the pictures. */
export type RecapDecoration = 'glow' | 'glass' | 'bands'

export interface RecapDesign {
    readonly id: string
    readonly labelKey: string
    readonly decoration: RecapDecoration
}

export const recapDesigns = [
    { id: 'aurora', labelKey: 'recap.designs.aurora', decoration: 'glow' },
    { id: 'frost', labelKey: 'recap.designs.frost', decoration: 'glass' },
    { id: 'prism', labelKey: 'recap.designs.prism', decoration: 'bands' },
] as const satisfies readonly RecapDesign[]

export type RecapDesignId = (typeof recapDesigns)[number]['id']

export const defaultRecapDesign: RecapDesignId = 'aurora'

/** The design with the id; unknown ids (e.g. of a newer client) show the default design. */
export function findRecapDesign(id: string | null | undefined): RecapDesign {
    return (
        recapDesigns.find((design) => design.id === id) ??
        recapDesigns.find((design) => design.id === defaultRecapDesign)!
    )
}

/** Colors and font of a design, as the pictures and videos need them. */
export interface RecapPalette {
    readonly backgroundStart: string
    readonly backgroundEnd: string
    readonly glow: string
    readonly accent: string
    readonly text: string
    readonly textMuted: string
    readonly surface: string
    readonly fontFamily: string
}

/** Used where an element has no design variables, e.g. in tests without the base CSS. */
export const fallbackPalette: RecapPalette = {
    backgroundStart: '#0b1636',
    backgroundEnd: '#2a1660',
    glow: '#5b43d9',
    accent: '#5ee8d2',
    text: '#f4f7ff',
    textMuted: '#c1c9ea',
    surface: 'rgba(255, 255, 255, 0.12)',
    fontFamily: 'system-ui, sans-serif',
}

const paletteVariables: Record<keyof RecapPalette, string> = {
    backgroundStart: '--wmp-recap-background-start',
    backgroundEnd: '--wmp-recap-background-end',
    glow: '--wmp-recap-glow',
    accent: '--wmp-recap-accent',
    text: '--wmp-recap-text',
    textMuted: '--wmp-recap-text-muted',
    surface: '--wmp-recap-surface',
    fontFamily: '--wmp-recap-font-family',
}

/** The palette of the design an element shows (the element with data-recap-design or inside it). */
export function readRecapPalette(element: Element | null | undefined): RecapPalette {
    if (!element || typeof getComputedStyle !== 'function') {
        return fallbackPalette
    }

    const style = getComputedStyle(element)
    const entries = Object.entries(paletteVariables).map(([key, variable]) => {
        const value = style.getPropertyValue(variable).trim()
        return [key, value || fallbackPalette[key as keyof RecapPalette]]
    })
    return Object.fromEntries(entries) as unknown as RecapPalette
}

/** Key in localStorage: the design belongs to the device, like the volume. */
export const recapDesignStorageKey = 'wavemp.recap.design.v1'

function storage(): Storage | null {
    try {
        return window.localStorage
    } catch {
        return null
    }
}

export function loadRecapDesign(store: Storage | null = storage()): RecapDesignId {
    try {
        return findRecapDesign(store?.getItem(recapDesignStorageKey)).id as RecapDesignId
    } catch {
        return defaultRecapDesign
    }
}

export function saveRecapDesign(id: string, store: Storage | null = storage()): void {
    try {
        store?.setItem(recapDesignStorageKey, id)
    } catch {
        // Blocked storage: the design is not remembered.
    }
}
