import { onScopeDispose, watch } from 'vue'
import { usePlayerStore } from '@/stores/player'
import { useCoversStore } from '@/stores/covers'
import {
    contrastColor,
    coverColorContrastVariable,
    coverColorVariable,
    coverSampleSize,
    cssColor,
    dominantColor,
    type Rgb,
} from '@/themes/coverColor'

/** Smallest size of covers the API delivers; enough for one color. */
const coverSize = 96

/** Loads a picture and returns the dominant color of a small copy of it, or null. */
async function colorOfPicture(url: string): Promise<Rgb | null> {
    const image = new Image()
    image.decoding = 'async'
    image.src = url
    try {
        await image.decode()
    } catch {
        return null
    }

    const canvas = document.createElement('canvas')
    canvas.width = coverSampleSize
    canvas.height = coverSampleSize
    const context = canvas.getContext('2d', { willReadFrequently: true })
    if (!context) {
        return null
    }

    context.drawImage(image, 0, 0, coverSampleSize, coverSampleSize)
    try {
        return dominantColor(context.getImageData(0, 0, coverSampleSize, coverSampleSize).data)
    } catch {
        return null
    }
}

function setColor(color: Rgb | null): void {
    const style = document.documentElement.style
    if (color) {
        style.setProperty(coverColorVariable, cssColor(color))
        style.setProperty(coverColorContrastVariable, contrastColor(color))
    } else {
        style.removeProperty(coverColorVariable)
        style.removeProperty(coverColorContrastVariable)
    }
}

/**
 * Tells themes the color of the cover of the current song (--wmp-cover-color, and black or white
 * for text on it as --wmp-cover-color-contrast) on the root element; both are missing while no song
 * with a cover is current. Call it once, in the app shell (the player belongs to it).
 */
export function useCoverColor(): void {
    const player = usePlayerStore()
    const covers = useCoversStore()
    let request = 0

    watch(
        () =>
            [
                player.current?.track.hasCover ? (player.current.track.id ?? null) : null,
                covers.versionOf('track'),
            ] as const,
        async ([id, version]) => {
            const own = ++request
            if (!id) {
                setColor(null)
                return
            }

            let color: Rgb | null = null
            try {
                const url = await covers.acquire('track', id, coverSize, version)
                color = url ? await colorOfPicture(url) : null
            } catch {
                color = null
            } finally {
                covers.release('track', id, coverSize, version)
            }

            if (own === request) {
                setColor(color)
            }
        },
        { immediate: true },
    )

    onScopeDispose(() => {
        request++
        setColor(null)
    })
}
