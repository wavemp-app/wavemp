// Hands a picture or video of a recap to the person: on phones and tablets through the share sheet
// of the system (Web Share API with files), on the desktop as a download.

export type ShareOutcome = 'shared' | 'downloaded' | 'cancelled'

/** Whether the device shares files through the system: a touch screen and the Web Share API. */
export function sharesThroughSystem(file: File, nav: Navigator = navigator): boolean {
    const coarse =
        typeof window !== 'undefined' &&
        typeof window.matchMedia === 'function' &&
        window.matchMedia('(pointer: coarse)').matches
    try {
        return coarse && typeof nav.share === 'function' && !!nav.canShare?.({ files: [file] })
    } catch {
        return false
    }
}

/** Saves the file through a temporary link. */
export function downloadFile(file: Blob, name: string): void {
    const url = URL.createObjectURL(file)
    const link = document.createElement('a')
    link.href = url
    link.download = name
    link.rel = 'noopener'
    document.body.append(link)
    link.click()
    link.remove()
    window.setTimeout(() => URL.revokeObjectURL(url), 30_000)
}

/** Shares the file through the system where it can, otherwise downloads it. */
export async function shareOrDownload(file: File, title: string): Promise<ShareOutcome> {
    if (sharesThroughSystem(file)) {
        try {
            await navigator.share({ files: [file], title })
            return 'shared'
        } catch (error) {
            if (error instanceof DOMException && error.name === 'AbortError') {
                return 'cancelled'
            }
            // Sharing failed (e.g. not allowed without a gesture any more): download instead.
        }
    }

    downloadFile(file, file.name)
    return 'downloaded'
}

/** File name of a picture or video, e.g. wavemp-recap-last-month-top-artists.png. */
export function recapFileName(period: string, part: string, extension: string): string {
    const slug = (value: string) =>
        value
            .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
            .toLowerCase()
            .replace(/[^a-z0-9]+/g, '-')
            .replace(/^-|-$/g, '')
    return ['wavemp-recap', slug(period), slug(part)].filter(Boolean).join('-') + `.${extension}`
}
