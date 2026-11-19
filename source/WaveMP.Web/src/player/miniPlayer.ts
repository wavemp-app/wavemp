// The mini player: cover, title, artist, progress and the main controls in a small window. Where
// the browser has the Document Picture-in-Picture API (Chrome, Edge), it is a floating window that
// stays above the other windows; the music keeps playing in the window of the app, the floating
// window only shows the controls. Other browsers (Firefox, Safari) show it as a compact view in
// the window of the app instead. The floating window is an empty document: it gets the styles of
// the app and the attributes of its root element (theme, variant, language, cover color) and keeps
// them in line while it is open, so the theme of the profile applies there as well.

/** How the mini player is shown: not at all, as floating window or in the window of the app. */
export type MiniPlayerMode = 'closed' | 'window' | 'inline'

/** Options of documentPictureInPicture.requestWindow(). */
export interface PictureInPictureWindowOptions {
    width?: number
    height?: number
}

/** The part of the Document Picture-in-Picture API the mini player uses. */
export interface DocumentPictureInPicture {
    requestWindow(options?: PictureInPictureWindowOptions): Promise<Window>
}

/** Initial size of the floating window in CSS pixels; the user can resize it. */
export const miniPlayerWindowSize: Readonly<Required<PictureInPictureWindowOptions>> = {
    width: 320,
    height: 440,
}

/** Marks the nodes the floating window took over from the app, so they can be renewed. */
export const mirroredAttribute = 'data-wmp-mirrored'

/** The Document Picture-in-Picture API of the window, or null where the browser lacks it. */
export function documentPictureInPictureOf(
    view: Window | undefined = typeof window === 'undefined' ? undefined : window,
): DocumentPictureInPicture | null {
    const api = (
        view as { documentPictureInPicture?: Partial<DocumentPictureInPicture> } | undefined
    )?.documentPictureInPicture
    return api && typeof api.requestWindow === 'function' ? (api as DocumentPictureInPicture) : null
}

/** The mode the mini player opens in: floating where the browser can, otherwise in the app. */
export function miniPlayerModeFor(floatingWindow: boolean): Exclude<MiniPlayerMode, 'closed'> {
    return floatingWindow ? 'window' : 'inline'
}

/** Whether a node of the head carries styles: a stylesheet link or a style element. */
function isStyleNode(node: Element): boolean {
    const name = node.localName
    return (
        name === 'style' ||
        (name === 'link' && (node.getAttribute('rel') ?? '').split(/\s+/).includes('stylesheet'))
    )
}

/** What a style node of the app brings: its stylesheet address or its CSS. */
function styleKey(node: Element): string {
    return node.localName === 'link'
        ? `link ${(node as HTMLLinkElement).href}`
        : `style ${node.textContent ?? ''}`
}

/** The styles last copied into a window, so unchanged styles are not loaded again. */
const copiedStyles = new WeakMap<Document, string>()

/**
 * Copies the styles of the app into the floating window: every stylesheet link (with its absolute
 * address, the window has no address of its own) and every style element of the head, in their
 * order, after a base element with the address of the app for the relative URLs inside them. It
 * can run again after a change: unchanged styles stay, changed ones replace the copies before.
 */
export function copyStyles(source: Document, target: Document): void {
    const nodes = [...source.head.children].filter(isStyleNode)
    const signature = `${source.baseURI}\n${nodes.map(styleKey).join('\n')}`
    if (copiedStyles.get(target) === signature) {
        return
    }

    copiedStyles.set(target, signature)
    const previous = [...target.head.querySelectorAll(`[${mirroredAttribute}]`)]

    const base = target.createElement('base')
    base.href = source.baseURI
    base.setAttribute(mirroredAttribute, '')
    target.head.append(base)

    for (const node of nodes) {
        const copy = target.importNode(node, true) as Element
        if (copy.localName === 'link') {
            copy.setAttribute('href', (node as HTMLLinkElement).href)
        }

        copy.setAttribute(mirroredAttribute, '')
        target.head.append(copy)
    }

    // The copies before go only now, so the window is never without styles in between.
    for (const node of previous) {
        node.remove()
    }
}

/**
 * Gives the root element of the floating window the attributes of the root element of the app:
 * the theme (data-theme, data-theme-variant, data-theme-api), the language and the inline
 * variables such as the cover color. Attributes the app no longer has are removed.
 */
export function copyRootAttributes(source: Document, target: Document): void {
    const from = source.documentElement
    const to = target.documentElement
    for (const name of to.getAttributeNames()) {
        if (!from.hasAttribute(name)) {
            to.removeAttribute(name)
        }
    }

    for (const name of from.getAttributeNames()) {
        const value = from.getAttribute(name) ?? ''
        if (to.getAttribute(name) !== value) {
            to.setAttribute(name, value)
        }
    }
}

/**
 * Keeps the floating window in line with the app: once at once, then after every change of the
 * root element (another theme, variant, language or cover color) and of the styles in the head
 * (another theme stylesheet, styles loaded later). Returns the function that stops it.
 */
export function mirrorDocument(source: Document, target: Document): () => void {
    copyRootAttributes(source, target)
    copyStyles(source, target)

    const view = source.defaultView
    if (!view || typeof view.MutationObserver === 'undefined') {
        return () => undefined
    }

    const root = new view.MutationObserver(() => copyRootAttributes(source, target))
    root.observe(source.documentElement, { attributes: true })

    let pending = false
    const head = new view.MutationObserver(() => {
        if (pending) {
            return
        }

        pending = true
        queueMicrotask(() => {
            pending = false
            copyStyles(source, target)
        })
    })
    head.observe(source.head, {
        childList: true,
        subtree: true,
        attributes: true,
        characterData: true,
    })

    return () => {
        root.disconnect()
        head.disconnect()
    }
}

/** What the mini player needs to know to show the video of the current song. */
export interface MiniPlayerVideoInput {
    /** Whether the current song has a music video. */
    readonly hasVideo: boolean
    /** Whether the profile chose the video view of the stage. */
    readonly videoChosen: boolean
    /** Whether a casting device plays (it shows the video itself). */
    readonly casting: boolean
}

/**
 * Whether the mini player shows the video of the current song instead of its cover: like the stage
 * of Now Playing, while the song has a video, the profile chose the video view and no casting
 * device plays.
 */
export function miniPlayerShowsVideo(input: MiniPlayerVideoInput): boolean {
    return input.hasVideo && input.videoChosen && !input.casting
}

/** data-state tokens of the mini player, e.g. "playing loading video". */
export function miniPlayerStateTokens(
    playback: 'idle' | 'playing' | 'paused',
    loading: boolean,
    videoShown: boolean,
): string {
    return [playback, ...(loading ? ['loading'] : []), ...(videoShown ? ['video'] : [])].join(' ')
}
