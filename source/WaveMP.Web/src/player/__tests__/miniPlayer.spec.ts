import { afterEach, describe, expect, it } from 'vitest'
import {
    copyRootAttributes,
    copyStyles,
    documentPictureInPictureOf,
    miniPlayerModeFor,
    miniPlayerShowsVideo,
    miniPlayerStateTokens,
    mirrorDocument,
    mirroredAttribute,
} from '../miniPlayer'

/** An empty document like the one of a new floating window. */
function emptyWindowDocument(): Document {
    return document.implementation.createHTMLDocument('')
}

/** Copied nodes of the head of a window, without the base element. */
function copiedStyles(target: Document): string[] {
    return [...target.head.querySelectorAll(`[${mirroredAttribute}]`)]
        .filter((node) => node.localName !== 'base')
        .map((node) =>
            node.localName === 'link'
                ? `link ${node.getAttribute('href') ?? ''}`
                : `style ${node.textContent ?? ''}`,
        )
}

describe('mini player', () => {
    afterEach(() => {
        document.head.innerHTML = ''
        for (const name of document.documentElement.getAttributeNames()) {
            document.documentElement.removeAttribute(name)
        }
    })

    it('finds the Document Picture-in-Picture API only where the browser has it', () => {
        const requestWindow = () => Promise.resolve(window)
        expect(documentPictureInPictureOf(undefined)).toBeNull()
        expect(documentPictureInPictureOf({} as Window)).toBeNull()
        expect(
            documentPictureInPictureOf({ documentPictureInPicture: {} } as unknown as Window),
        ).toBeNull()
        expect(
            documentPictureInPictureOf({
                documentPictureInPicture: { requestWindow },
            } as unknown as Window)?.requestWindow,
        ).toBe(requestWindow)
    })

    it('opens as floating window where the browser can, otherwise in the app', () => {
        expect(miniPlayerModeFor(true)).toBe('window')
        expect(miniPlayerModeFor(false)).toBe('inline')
    })

    it('shows the video like the stage: chosen, available and not while casting', () => {
        expect(miniPlayerShowsVideo({ hasVideo: true, videoChosen: true, casting: false })).toBe(
            true,
        )
        expect(miniPlayerShowsVideo({ hasVideo: false, videoChosen: true, casting: false })).toBe(
            false,
        )
        expect(miniPlayerShowsVideo({ hasVideo: true, videoChosen: false, casting: false })).toBe(
            false,
        )
        expect(miniPlayerShowsVideo({ hasVideo: true, videoChosen: true, casting: true })).toBe(
            false,
        )
    })

    it('builds the state tokens', () => {
        expect(miniPlayerStateTokens('idle', false, false)).toBe('idle')
        expect(miniPlayerStateTokens('playing', true, false)).toBe('playing loading')
        expect(miniPlayerStateTokens('paused', false, true)).toBe('paused video')
    })

    it('copies the stylesheets and style elements of the app with absolute addresses', () => {
        document.head.innerHTML =
            '<meta name="theme-color" content="#000">' +
            '<link rel="stylesheet" id="wavemp-theme" href="/themes/midnight/theme.css">' +
            '<link rel="icon" href="/favicon.svg">' +
            '<style>body { color: red; }</style>'
        const target = emptyWindowDocument()

        copyStyles(document, target)

        const origin = window.location.origin
        expect(copiedStyles(target)).toEqual([
            `link ${origin}/themes/midnight/theme.css`,
            'style body { color: red; }',
        ])
        const base = target.head.querySelector('base')
        expect(base?.getAttribute('href')).toBe(document.baseURI)
        expect(target.head.querySelector('meta')).toBeNull()
        expect(target.head.querySelector('#wavemp-theme')).not.toBeNull()
    })

    it('keeps unchanged styles and replaces changed ones', () => {
        document.head.innerHTML = '<link rel="stylesheet" href="/themes/midnight/theme.css">'
        const target = emptyWindowDocument()
        copyStyles(document, target)
        const first = target.head.querySelector('link')

        copyStyles(document, target)
        expect(target.head.querySelector('link')).toBe(first)

        document.head.querySelector('link')?.setAttribute('href', '/themes/meadow/theme.css')
        copyStyles(document, target)
        expect(copiedStyles(target)).toEqual([
            `link ${window.location.origin}/themes/meadow/theme.css`,
        ])
        expect(target.head.querySelectorAll('base')).toHaveLength(1)
    })

    it('gives the window the attributes of the root element of the app', () => {
        const root = document.documentElement
        root.setAttribute('lang', 'de')
        root.dataset.theme = 'accent'
        root.dataset.themeVariant = 'dark'
        root.style.setProperty('--wmp-cover-color', 'rgb(1 2 3)')
        const target = emptyWindowDocument()
        target.documentElement.setAttribute('data-stale', 'yes')

        copyRootAttributes(document, target)

        const copy = target.documentElement
        expect(copy.getAttribute('lang')).toBe('de')
        expect(copy.dataset.theme).toBe('accent')
        expect(copy.dataset.themeVariant).toBe('dark')
        expect(copy.style.getPropertyValue('--wmp-cover-color')).toBe('rgb(1 2 3)')
        expect(copy.hasAttribute('data-stale')).toBe(false)
    })

    it('follows changes of the theme and the styles until it is stopped', async () => {
        document.documentElement.dataset.theme = 'midnight'
        document.head.innerHTML = '<link rel="stylesheet" href="/themes/midnight/theme.css">'
        const target = emptyWindowDocument()

        const stop = mirrorDocument(document, target)
        expect(target.documentElement.dataset.theme).toBe('midnight')

        document.documentElement.dataset.theme = 'meadow'
        document.head.querySelector('link')?.setAttribute('href', '/themes/meadow/theme.css')
        await new Promise((resolve) => setTimeout(resolve, 0))
        expect(target.documentElement.dataset.theme).toBe('meadow')
        expect(copiedStyles(target)).toEqual([
            `link ${window.location.origin}/themes/meadow/theme.css`,
        ])

        stop()
        document.documentElement.dataset.theme = 'daylight'
        await new Promise((resolve) => setTimeout(resolve, 0))
        expect(target.documentElement.dataset.theme).toBe('meadow')
    })
})
