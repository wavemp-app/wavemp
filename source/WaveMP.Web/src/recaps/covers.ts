// Covers for the pictures and videos of a recap: the covers of the sections, loaded through the
// cover store (which shares them with the page) and decoded into images a canvas can draw.

import type { CoverSize } from '@/library/covers'
import { useCoversStore } from '@/stores/covers'
import type { RecapSlide } from './slides'

const pictureCoverSize: CoverSize = 600

/** The tracks whose covers the sections show. */
export function coverTrackIdsOf(slides: readonly RecapSlide[]): string[] {
    const ids = new Set<string>()
    for (const slide of slides) {
        if (slide.coverTrackId) {
            ids.add(slide.coverTrackId)
        }
        for (const item of slide.items ?? []) {
            if (item.coverTrackId) {
                ids.add(item.coverTrackId)
            }
        }
    }
    return [...ids]
}

export interface LoadedCovers {
    readonly images: ReadonlyMap<string, CanvasImageSource>
    /** Gives the covers back to the store. */
    release(): void
}

/** Loads the covers of the sections; covers that fail to load are left out. */
export async function loadRecapCovers(slides: readonly RecapSlide[]): Promise<LoadedCovers> {
    const covers = useCoversStore()
    const version = covers.versionOf('track')
    const ids = coverTrackIdsOf(slides)
    const loaded = await Promise.all(
        ids.map(async (id) => {
            try {
                const url = await covers.acquire('track', id, pictureCoverSize, version)
                if (!url) {
                    return null
                }
                const image = new Image()
                image.src = url
                await image.decode()
                return [id, image] as const
            } catch {
                return null
            }
        }),
    )

    let released = false
    return {
        images: new Map(loaded.filter((entry) => entry !== null)),
        release() {
            if (!released) {
                released = true
                ids.forEach((id) => covers.release('track', id, pictureCoverSize, version))
            }
        },
    }
}
